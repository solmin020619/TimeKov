// =====================================================================
// Editor/TableBuildMenu.cs
// Unity 에디터 상단 메뉴 진입점
//
// Tools/TIMEKOV/데이터/코드 다시 생성 (컬럼 바꿨을 때) — 스키마 -> Generated/*.g.cs
// Tools/TIMEKOV/데이터/시트 값 확인               — 지금 게임이 받을 값을 콘솔에 출력 + 검증
//
// ★값만 바꿨을 때는 둘 다 누를 필요 없다. Play 를 누르면 자동으로 최신 시트를 받는다.
//   컬럼/테이블을 추가하거나 지웠을 때만 코드 재생성이 필요하다.
// =====================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TableBuildMenu
{
    // 코드 자동생성 — 스키마를 읽어 Generated/ 폴더에 .g.cs 파일을 생성한다
    [MenuItem("Tools/TIMEKOV/데이터/코드 다시 생성 (컬럼 바꿨을 때)")]
    public static void Generate()
    {
        Debug.Log("[테이블] 코드 생성 시작");
        CodeGenerator.GenerateAll();
    }

    // 지금 시트에서 받아 값을 보여준다.
    //
    // 예전엔 받아서 검증만 하고 값을 버렸다. 그래서 "시트 고친 게 반영됐나"를 확인할 수가 없었다.
    // 구글 게시 CSV 는 캐시 지연이 있어서 그 확인이 실제로 필요하다.
    [MenuItem("Tools/TIMEKOV/데이터/시트 값 확인")]
    public static void InspectSheets()
    {
        var schemas = AllTableSchemas.GetAll();
        var allTables = new Dictionary<string, CsvTable>();
        int total = schemas.Count;

        for (int i = 0; i < schemas.Count; i++)
        {
            var schema = schemas[i];

            EditorUtility.DisplayProgressBar("시트 값 확인", $"[{i + 1}/{total}] {schema.TableName}", (float)(i + 1) / total);

            var table = CsvReader.DownloadSync(schema.GoogleSheetUrl);
            if (table == null)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("다운로드 실패", $"{schema.TableName} 다운로드 실패\nURL 을 확인해주세요.", "확인");
                return;
            }
            allTables[schema.TableName] = table;
        }

        EditorUtility.ClearProgressBar();

        // 콘솔에 전체 내용을 찍는다. 접힌 상태에선 첫 줄만 보이므로 요약을 맨 앞에 둔다.
        var sb = new StringBuilder();
        sb.AppendLine($"[시트 값] {total}개 테이블 - 클릭해서 펼치면 전체 내용");
        foreach (var kv in allTables)
        {
            sb.AppendLine();
            sb.AppendLine($"=== {kv.Key} ({kv.Value.Rows.Count}행) ===");
            foreach (var row in kv.Value.Rows)
                sb.AppendLine("  " + string.Join(" | ", row.Values));
        }
        Debug.Log(sb.ToString());

        bool passed = TableValidator.ValidateAll(schemas, allTables);
        EditorUtility.DisplayDialog(
            passed ? "시트 값 확인 완료" : "검증 실패",
            passed ? $"전체 {total}개 테이블 정상.\n값은 콘솔에 출력했다." : "콘솔에서 오류 확인",
            "확인");
    }

    // 메뉴 활성화 조건 — 플레이 중에는 비활성화
    [MenuItem("Tools/TIMEKOV/데이터/코드 다시 생성 (컬럼 바꿨을 때)", true)]
    [MenuItem("Tools/TIMEKOV/데이터/시트 값 확인", true)]
    private static bool ValidateMenu() => !EditorApplication.isPlaying;
}

#endif
