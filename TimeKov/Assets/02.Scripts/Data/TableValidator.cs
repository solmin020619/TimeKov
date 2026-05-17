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

        // 1. 컬럼 존재 확인
        foreach (var col in schema.Columns)
        {
            if (table.GetColumnIndex(col.Name) < 0)
                errors.Add($"{prefix} 컬럼 없음: {col.Name}");
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

        Debug.Log($"{prefix} 검증 통과 ({table.Rows.Count}행)");
        return true;
    }

    // 전체 테이블 일괄 검증
    public static bool ValidateAll(
        List<SheetSchema> schemas,
        Dictionary<string, CsvTable> allTables)
    {
        bool allPassed = true;
        foreach (var schema in schemas)
        {
            if (!allTables.TryGetValue(schema.TableName, out var table))
            {
                Debug.LogError($"[Validator] 테이블 없음: {schema.TableName}");
                allPassed = false;
                continue;
            }
            bool passed = Validate(schema, table, allTables, out var errors);
            if (!passed) allPassed = false;
        }
        return allPassed;
    }
}