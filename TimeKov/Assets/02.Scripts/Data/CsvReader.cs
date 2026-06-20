// =====================================================================
// CsvReader.cs
// CSV 텍스트 파싱 + 구글 시트 웹 게시 URL 에서 다운로드
// 에디터(동기)와 런타임(코루틴 비동기) 두 가지 방식을 지원한다
// =====================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class CsvReader
{
    // CSV 텍스트를 CsvTable 로 파싱
    // 쉼표 구분, 따옴표 내부의 쉼표/개행은 하나의 셀로 처리한다
    public static CsvTable Parse(string csvText)
    {
        var table = new CsvTable();
        var lines = SplitLines(csvText);

        if (lines.Count == 0)
            return table;

        // 첫 행은 헤더
        var headerCells = ParseLine(lines[0]);
        foreach (var h in headerCells)
            table.Headers.Add(h.Trim());

        // 나머지 행은 데이터
        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;
            var cells = ParseLine(line);
            table.Rows.Add(new CsvRow(cells.ToArray()));
        }

        return table;
    }

    // 구글 시트에서 동기 다운로드 (에디터 전용)
    // EditorUtility 로 진행률을 표시하면서 사용한다
#if UNITY_EDITOR
    public static CsvTable DownloadSync(string url)
    {
        using var request = UnityWebRequest.Get(url);
        request.SendWebRequest();

        // 완료 대기 (에디터 전용 — 런타임에서 사용 금지)
        while (!request.isDone) { }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[CsvReader] 다운로드 실패: {url}\n{request.error}");
            return null;
        }
        return Parse(request.downloadHandler.text);
    }
#endif

    // 여러 구글 시트를 "동시에" 다운로드한다 (직렬 대비 핵심 속도개선).
    // 원리: SendWebRequest() 를 yield 하지 않고 핸들만 모아 한꺼번에 띄운 뒤,
    //       매 프레임 yield return null 로 양보하며 isDone 된 것만 수거한다.
    //       스레드가 아니라 코루틴 위 async I/O 라 Unity API 접근이 안전하다.
    //       총시간 = (직렬) 왕복지연 합 -> (병렬) 가장 느린 한 개 수준.
    // maxConcurrent: 동시에 떠 있는 요청 수 상한 (구글 throttle/모바일 소켓 한계 대비).
    // onComplete: (성공 시트 name->CsvTable, 실패한 시트 name 목록) 을 돌려준다.
    public static IEnumerator DownloadAllAsync(
        IReadOnlyList<(string name, string url)> sources,
        int maxConcurrent,
        Action<Dictionary<string, CsvTable>, List<string>> onComplete,
        Action<float> onProgress = null)
    {
        const int MaxRetry = 1;       // 실패 시 재시도 횟수 (일시적 네트워크 끊김 대비)
        const int TimeoutSec = 20;    // 요청 1개당 타임아웃(초). 매달린 요청이 전체를 막지 않게.
                                      // 작은 CSV엔 넉넉. 너무 짧으면 느린 회선서 정상요청을 거짓실패시킴.

        var results = new Dictionary<string, CsvTable>();
        var failed = new List<string>();

        // 아직 시작 안 한 작업 (name, url, 지금까지 시도횟수)
        var pending = new Queue<(string name, string url, int tries)>();
        foreach (var s in sources) pending.Enqueue((s.name, s.url, 0));

        // 진행 중인 요청 (동시에 떠 있는 것)
        var active = new List<(string name, string url, int tries, UnityWebRequest req)>();

        int total = Mathf.Max(sources.Count, 1);
        int finished = 0;             // 성공 or 최종실패로 끝난 시트 수
        int cap = Mathf.Max(1, maxConcurrent);

        while (finished < sources.Count)
        {
            // 1) 빈 슬롯만큼 새 요청 시작 (= 동시성)
            while (active.Count < cap && pending.Count > 0)
            {
                var job = pending.Dequeue();
                var req = UnityWebRequest.Get(job.url);
                req.timeout = TimeoutSec;
                req.SendWebRequest();                 // yield 안 함 -> 즉시 다음 요청
                active.Add((job.name, job.url, job.tries, req));
            }

            // 2) 끝난 요청 수거 (뒤에서부터 지워야 인덱스 안 꼬임)
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                if (!a.req.isDone) continue;

                if (a.req.result == UnityWebRequest.Result.Success)
                {
                    results[a.name] = Parse(a.req.downloadHandler.text);
                    finished++;
                }
                else if (a.tries < MaxRetry)
                {
                    // 실패 -> 재시도 큐로 (다음 빈 슬롯에 다시 뜸)
                    Debug.LogWarning($"[CsvReader] 다운로드 재시도({a.tries + 1}/{MaxRetry}): {a.name}\n{a.req.error}");
                    pending.Enqueue((a.name, a.url, a.tries + 1));
                }
                else
                {
                    // 재시도 소진 -> 실패 확정
                    Debug.LogError($"[CsvReader] 다운로드 실패: {a.name}\n{a.req.error}");
                    failed.Add(a.name);
                    finished++;
                }

                a.req.Dispose();
                active.RemoveAt(i);
            }

            onProgress?.Invoke((float)finished / total);

            if (finished < sources.Count)
                yield return null;                    // 다음 프레임에 다시
        }

        onComplete?.Invoke(results, failed);
    }

    // CSV 행 파싱 — 따옴표 감싸기(RFC 4180) 처리
    private static List<string> ParseLine(string line)
    {
        var result = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    // 따옴표 이스케이프 ("") 처리
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(c);
            }
        }
        result.Add(cell.ToString());
        return result;
    }

    // 개행 분리 (CRLF / LF 모두 처리)
    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var line = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                lines.Add(line.ToString());
                line.Clear();
            }
            else if (c == '\n')
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            else
            {
                line.Append(c);
            }
        }
        if (line.Length > 0)
            lines.Add(line.ToString());

        return lines;
    }
}