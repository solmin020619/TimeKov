// =====================================================================
// Editor/TableBuildMenu.cs
// Unity 에디터 상단 메뉴 진입점
// Tools/Sheet/Generate        — 코드 자동생성
// Tools/Sheet/Download From Google — 구글 시트 다운로드 + 검증
// =====================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TableBuildMenu
{
    // 코드 자동생성 — 스키마를 읽어 Generated/ 폴더에 .g.cs 파일을 생성한다
    [MenuItem("Tools/Sheet/Generate")]
    public static void Generate()
    {
        Debug.Log("[TableBuildMenu] 코드 생성 시작");
        CodeGenerator.GenerateAll();
    }

    // 구글 시트 다운로드 + 검증
    // 에디터에서 동기적으로 다운로드하고 Validator 를 실행한다
    [MenuItem("Tools/Sheet/Download From Google")]
    public static void DownloadFromGoogle()
    {
        var schemas = AllTableSchemas.GetAll();
        var allTables = new Dictionary<string, CsvTable>();
        int total = schemas.Count;

        for (int i = 0; i < schemas.Count; i++)
        {
            var schema = schemas[i];

            EditorUtility.DisplayProgressBar(
                "구글 시트 다운로드",
                $"[{i + 1}/{total}] {schema.TableName}",
                (float)(i + 1) / total);

            var table = CsvReader.DownloadSync(schema.GoogleSheetUrl);

            if (table == null)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "다운로드 실패",
                    $"{schema.TableName} 다운로드 실패\nURL 을 확인해주세요.",
                    "확인");
                return;
            }
            allTables[schema.TableName] = table;
        }

        EditorUtility.ClearProgressBar();

        // 검증 실행
        bool passed = TableValidator.ValidateAll(schemas, allTables);
        EditorUtility.DisplayDialog(
            passed ? "다운로드 완료" : "검증 실패",
            passed ? $"전체 {total}개 테이블 완료" : "콘솔에서 오류 확인",
            "확인");
    }

    // 메뉴 활성화 조건 — 플레이 중에는 비활성화
    [MenuItem("Tools/Sheet/Generate", true)]
    [MenuItem("Tools/Sheet/Download From Google", true)]
    private static bool ValidateMenu() => !EditorApplication.isPlaying;
}

#endif