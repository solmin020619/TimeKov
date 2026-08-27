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
    // 다른 테이블과 달리 SheetSchema 가 없어서(자유 텍스트 표) URL 을 여기서 들고 있다.
    // 로컬 사본을 받아두는 쪽(SheetCache)도 이 URL 을 쓴다.
    public const string URL = "https://docs.google.com/spreadsheets/d/e/2PACX-1vRooWxOYylx_ivMvZqh___XUA1T6ZP6i815xiOkVxpcNEkYU2PF6FabT9YUr2bTa-8eHs7U5Zh5UCa5/pub?gid=0&single=true&output=csv";

    /// <summary>테이블 이름(로컬 사본 파일명과 동일).</summary>
    public const string TableName = "Localization";

    /// <summary>받아둔 CSV 원문으로 즉시 로드. 에디터 직행 플레이가 로컬 사본으로 부팅할 때 쓴다.</summary>
    public static void LoadFromCsvText(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText)) return;
        ParseAndLoad(CsvReader.Parse(csvText));
    }

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
            // 로컬 사본으로 폴백. 에디터는 레포 사본/캐시, 빌드는 동봉본(Resources/SheetBackup).
            // ★이게 없으면 오프라인에서 번역이 통째로 빠져 한국어로만 뜬다. 게임 데이터와 달리
            //   여기는 실패해도 진행되는 경로라 조용히 넘어가서 더 늦게 발견된다.
            string local = LocalTableSource.TryRead(TableName);
            if (local != null)
            {
                Debug.LogWarning("[LocalizationLoader] 현지화 시트 다운로드 실패 — 로컬 사본으로 대체");
                ParseAndLoad(CsvReader.Parse(local));
                onComplete?.Invoke(true);
                yield break;
            }

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
            string key = Normalize(row.Get(0)); // A열: 한글 원문 (Key)
            if (string.IsNullOrEmpty(key)) continue;

            if (enIdx >= 0) { string v = Normalize(row.Get(enIdx)); if (!string.IsNullOrEmpty(v)) enTable[key] = v; }
            if (cnIdx >= 0) { string v = Normalize(row.Get(cnIdx)); if (!string.IsNullOrEmpty(v)) cnTable[key] = v; }
            if (frIdx >= 0) { string v = Normalize(row.Get(frIdx)); if (!string.IsNullOrEmpty(v)) frTable[key] = v; }
        }

        Loc.LoadTable(LanguageCode.EN, enTable);
        Loc.LoadTable(LanguageCode.CN, cnTable);
        Loc.LoadTable(LanguageCode.FR, frTable);
        SaveCache(enTable, cnTable, frTable);

        // 부팅 로그는 짧게. 자세한 수치는 문제 생겼을 때만 의미가 있다.
        if (enTable.Count == 0 || cnTable.Count == 0 || frTable.Count == 0)
            Debug.LogWarning($"[Loc] 번역 일부 비었음 — EN:{enTable.Count} CN:{cnTable.Count} FR:{frTable.Count}");
    }

    // ── 로컬 캐시 ────────────────────────────────────────────────────
    // ★로딩 씬 문구는 시트를 받기 '전에' 화면에 뜬다. 그래서 아무리 잘 배선해도
    //   첫 화면만 한국어로 남았다. 직전 실행에서 받아둔 표를 씬이 열리기 전에 깔아두면
    //   로딩 화면부터 올바른 언어로 나온다. 네트워크 결과가 오면 그걸로 덮어쓴다.
    //   덤으로 오프라인/시트 다운로드 실패 시에도 번역이 유지된다.
    // 형식: 한 줄에 키/EN/CN/FR 을 탭으로. 줄바꿈은 백슬래시 n 으로 접어 한 줄을 지킨다.
    private static string CachePath => System.IO.Path.Combine(Application.persistentDataPath, "loc_cache.tsv");

    private static string Fold(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", " ");

    private static void SaveCache(Dictionary<string, string> en, Dictionary<string, string> cn, Dictionary<string, string> fr)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            var allKeys = new HashSet<string>(en.Keys);
            allKeys.UnionWith(cn.Keys);
            allKeys.UnionWith(fr.Keys);
            foreach (var k in allKeys)
            {
                en.TryGetValue(k, out var e); cn.TryGetValue(k, out var c); fr.TryGetValue(k, out var f);
                sb.Append(Fold(k)).Append('\t').Append(Fold(e)).Append('\t').Append(Fold(c)).Append('\t').Append(Fold(f)).Append('\n');
            }
            System.IO.File.WriteAllText(CachePath, sb.ToString(), System.Text.Encoding.UTF8);
        }
        catch (Exception e) { Debug.LogWarning($"[LocalizationLoader] 번역 캐시 저장 실패(무해): {e.Message}"); }
    }

    /// <summary>직전 실행에서 받아둔 표를 즉시 로드. 시트를 받기 전 화면(로딩 씬)을 위해 존재한다.</summary>
    public static void LoadFromCache()
    {
        try
        {
            if (!System.IO.File.Exists(CachePath)) return;
            var en = new Dictionary<string, string>();
            var cn = new Dictionary<string, string>();
            var fr = new Dictionary<string, string>();
            foreach (var line in System.IO.File.ReadAllLines(CachePath, System.Text.Encoding.UTF8))
            {
                if (string.IsNullOrEmpty(line)) continue;
                var p = line.Split('\t');
                if (p.Length < 4) continue;
                string k = Unfold(p[0]);
                if (string.IsNullOrEmpty(k)) continue;
                if (!string.IsNullOrEmpty(p[1])) en[k] = Unfold(p[1]);
                if (!string.IsNullOrEmpty(p[2])) cn[k] = Unfold(p[2]);
                if (!string.IsNullOrEmpty(p[3])) fr[k] = Unfold(p[3]);
            }
            if (en.Count > 0) Loc.LoadTable(LanguageCode.EN, en);
            if (cn.Count > 0) Loc.LoadTable(LanguageCode.CN, cn);
            if (fr.Count > 0) Loc.LoadTable(LanguageCode.FR, fr);
        }
        catch (Exception e) { Debug.LogWarning($"[LocalizationLoader] 번역 캐시 로드 실패(무해): {e.Message}"); }
    }

    private static string Unfold(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n);
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // 줄바꿈 표기 통일.
    //   ★시트 셀에 줄바꿈을 넣기 번거로워서 백슬래시 n 을 글자 그대로 타이핑한 행이 있다.
    //   코드의 "...\n..." 은 진짜 개행이라 그대로 두면 키가 안 맞아 번역이 통째로 실패하고
    //   (창고 출력 포트 설명이 그랬다), 매칭되더라도 화면에 백슬래시 n 이 글자로 보인다.
    //   시트에 어느 쪽으로 써도 되게 여기서 실제 개행으로 맞춘다.
    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\\n", "\n");
    }
}
