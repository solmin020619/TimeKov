// =====================================================================
// LocalizationLoader.cs
// Google Sheets 현지화 CSV를 런타임에 다운로드하고 Loc에 테이블 로드
// DataBoot.LoadAsync()에서 게임 데이터 로드 후 호출된다
//
// 시트 구조 (1행=헤더, A열=한글Key, B열=EN, C열=CN, D열=FR)
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LocalizationLoader
{
    // Google Sheets → 파일 → 공유 → 웹에 게시 → Localization 탭 → CSV 형식 → URL 붙여넣기
    private const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vRooWxOYylx_ivMvZqh___XUA1T6ZP6i815xiOkVxpcNEkYU2PF6FabT9YUr2bTa-8eHs7U5Zh5UCa5/pub?gid=0&single=true&output=csv";

    public static void LoadAsync(MonoBehaviour host, Action<bool> onComplete)
    {
        host.StartCoroutine(LoadCoroutine(onComplete));
    }

    private static IEnumerator LoadCoroutine(Action<bool> onComplete)
    {
        var sources = new List<(string name, string url)>
        {
            ("Localization", URL)
        };

        Dictionary<string, CsvTable> tables = null;
        List<string> failed = null;
        yield return CsvReader.DownloadAllAsync(sources, 1, (res, fail) =>
        {
            tables = res;
            failed = fail;
        });

        if (failed != null && failed.Count > 0 || tables == null || !tables.ContainsKey("Localization"))
        {
            Debug.LogWarning("[LocalizationLoader] 현지화 시트 다운로드 실패 — 한국어로 진행");
            onComplete?.Invoke(false);
            yield break;
        }

        ParseAndLoad(tables["Localization"]);
        onComplete?.Invoke(true);
    }

    private static void ParseAndLoad(CsvTable table)
    {
        int enIdx = table.GetColumnIndex("EN");
        int cnIdx = table.GetColumnIndex("CN");
        int frIdx = table.GetColumnIndex("FR");

        var enTable = new Dictionary<string, string>();
        var cnTable = new Dictionary<string, string>();
        var frTable = new Dictionary<string, string>();

        foreach (var row in table.Rows)
        {
            string key = row.Get(0); // A열: 한글 원문 (Key)
            if (string.IsNullOrEmpty(key)) continue;

            if (enIdx >= 0) { string v = row.Get(enIdx); if (!string.IsNullOrEmpty(v)) enTable[key] = v; }
            if (cnIdx >= 0) { string v = row.Get(cnIdx); if (!string.IsNullOrEmpty(v)) cnTable[key] = v; }
            if (frIdx >= 0) { string v = row.Get(frIdx); if (!string.IsNullOrEmpty(v)) frTable[key] = v; }
        }

        Loc.LoadTable(LanguageCode.EN, enTable);
        Loc.LoadTable(LanguageCode.CN, cnTable);
        Loc.LoadTable(LanguageCode.FR, frTable);

        Debug.Log($"[LocalizationLoader] 번역 로드 완료 — EN:{enTable.Count} CN:{cnTable.Count} FR:{frTable.Count}");
    }
}
