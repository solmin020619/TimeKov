// =====================================================================
// TableValidator.cs
// CSV 데이터 로드 후 유효성 검사
// 컬럼 존재, 필수 값, 타입, 키 중복, FK 무결성을 순서대로 검사한다
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public static class TableValidator
{
    // 단일 테이블 검증
    // allTables: FK 무결성 확인을 위해 전체 테이블을 함께 전달한다
    public static bool Validate(
        SheetSchema schema,
        CsvTable table,
        Dictionary<string, CsvTable> allTables,
        out List<string> errors)
    {
        errors = new List<string>();
        string prefix = $"[{schema.TableName}]";

        // 1. 컬럼 존재 확인 — 필수 컬럼만 에러. 선택(required:false) 컬럼은 시트에 없어도 통과(경고만).
        //    새 선택 컬럼을 시트에 넣기 전에도 데이터 로드가 안 깨지게 하는 점진 마이그레이션용.
        //    (없는 선택 컬럼은 파서에서 Get(-1)=빈문자로 처리되고, 소비 측이 폴백/기본값을 쓴다.)
        foreach (var col in schema.Columns)
        {
            if (table.GetColumnIndex(col.Name) < 0)
            {
                if (col.Required)
                    errors.Add($"{prefix} 필수 컬럼 없음: {col.Name}");
                else
                    Debug.LogWarning($"{prefix} 선택 컬럼 없음(스킵): {col.Name}");
            }
        }
        if (errors.Count > 0) return false;

        var keySet = new HashSet<string>();

        // 2. 행별 검사 (필수 값, 타입, allowDash)
        for (int rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
        {
            var row = table.Rows[rowIdx];
            var keyParts = new List<string>();

            foreach (var col in schema.Columns)
            {
                int colIdx = table.GetColumnIndex(col.Name);
                string raw = row.Get(colIdx);

                // allowDash: "-" 이면 빈 값으로 취급하고 검사 건너뜀
                if (col.AllowDash && raw == "-")
                {
                    if (col.IsKey) keyParts.Add(raw);
                    continue;
                }

                // 필수 값 빈 칸 확인
                if (col.Required && string.IsNullOrEmpty(raw))
                {
                    errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: 필수 값 없음");
                    continue;
                }

                if (string.IsNullOrEmpty(raw))
                {
                    if (col.IsKey) keyParts.Add(raw);
                    continue;
                }

                // 타입 유효성 검사
                switch (col.Type)
                {
                    case ColumnType.Int:
                        if (!int.TryParse(raw, out _))
                            errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: Int 파싱 실패 [{raw}]");
                        break;
                    case ColumnType.Float:
                        if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out _))
                            errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: Float 파싱 실패 [{raw}]");
                        break;
                    case ColumnType.Bool:
                        if (raw != "0" && raw != "1")
                            errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: Bool 은 0 또는 1 [{raw}]");
                        break;
                    case ColumnType.Enum:
                        if (!Enum.IsDefined(col.EnumType, raw))
                            errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: Enum [{col.EnumType.Name}] 에 없는 값 [{raw}]");
                        break;
                }

                if (col.IsKey) keyParts.Add(raw);
            }

            // 3. 키 중복 확인
            if (keyParts.Count > 0)
            {
                string compositeKey = string.Join("_", keyParts);
                if (!keySet.Add(compositeKey))
                    errors.Add($"{prefix} 행{rowIdx + 2}: 키 중복 [{compositeKey}]");
            }
        }

        // 4. FK 무결성 확인
        foreach (var col in schema.Columns)
        {
            if (col.Type != ColumnType.Ref) continue;
            if (!allTables.TryGetValue(col.RefTable, out var refTable)) continue;

            int colIdx = table.GetColumnIndex(col.Name);

            // 참조 대상 테이블의 키 값 집합 빌드
            var refSchema = AllTableSchemas.GetAll().Find(s => s.TableName == col.RefTable);
            if (refSchema == null) continue;

            var refKeys = new HashSet<string>();
            var refKeyColumns = refSchema.GetKeyColumns();

            foreach (var refRow in refTable.Rows)
            {
                var parts = new List<string>();
                foreach (var kc in refKeyColumns)
                {
                    int ki = refTable.GetColumnIndex(kc.Name);
                    parts.Add(refRow.Get(ki));
                }
                refKeys.Add(string.Join("_", parts));
            }

            // 이 테이블의 FK 값이 참조 대상에 존재하는지 확인
            for (int rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
            {
                string raw = table.Rows[rowIdx].Get(colIdx);
                if (string.IsNullOrEmpty(raw) || raw == "-") continue;
                if (!refKeys.Contains(raw))
                    errors.Add($"{prefix} 행{rowIdx + 2} {col.Name}: FK 대상 없음 [{raw}] in {col.RefTable}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var e in errors) Debug.LogError(e);
            return false;
        }

        // ★행이 0개여도 위 검사는 전부 통과한다(컬럼만 맞으면 되니까).
        //   그러면 아이템/몬스터 데이터 없이 게임이 조용히 시작된다. 예전엔 통과 로그의
        //   행 수를 보고 사람이 눈치챘는데, 그 로그를 없앤 만큼 여기서 명시적으로 잡는다.
        if (table.Rows.Count == 0)
            Debug.LogWarning($"{prefix} 시트에 데이터가 0행이다. 게시 설정이나 시트 내용을 확인해라.");

        // 통과는 조용히. 시트마다 한 줄씩 찍으면 부팅 로그가 도배돼서 정작 봐야 할 경고가 묻힌다.
        return true;
    }

    // 전체 테이블 일괄 검증
    public static bool ValidateAll(
        List<SheetSchema> schemas,
        Dictionary<string, CsvTable> allTables)
    {
        bool allPassed = true;
        int ok = 0, rows = 0;
        foreach (var schema in schemas)
        {
            if (!allTables.TryGetValue(schema.TableName, out var table))
            {
                Debug.LogError($"[Validator] 테이블 없음: {schema.TableName}");
                allPassed = false;
                continue;
            }
            bool passed = Validate(schema, table, allTables, out var errors);
            if (passed) { ok++; rows += table.Rows.Count; }
            else allPassed = false;
        }
        // ★성공은 아예 찍지 않는다. 잘 됐다는 건 게임이 도는 걸로 확인되고,
        //   콘솔은 '문제만' 남아 있어야 눈에 띈다. 실패는 위에서 이미 에러로 나갔다.
        if (!allPassed)
            Debug.LogError($"[Validator] 시트 검증 실패 — 통과 {ok}/{schemas.Count} (총 {rows}행)");
        return allPassed;
    }
}