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

    // 구글 시트에서 비동기 다운로드 (런타임 코루틴)
    public static IEnumerator DownloadAsync(string url, Action<CsvTable> onComplete)
    {
        using var request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[CsvReader] 다운로드 실패: {url}\n{request.error}");
            onComplete?.Invoke(null);
            yield break;
        }
        onComplete?.Invoke(Parse(request.downloadHandler.text));
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