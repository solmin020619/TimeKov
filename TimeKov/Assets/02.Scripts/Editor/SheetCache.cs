// =====================================================================
// Editor/SheetCache.cs
// 구글 시트를 받아 로컬 사본(Documentation/sheet_backup)을 갱신하는 한 곳.
//
// 이 사본은 세 가지로 쓰인다.
//   1) 직행 플레이의 즉시 부팅 소스 (DirectPlayBoot)
//   2) 시트 다운로드 실패 시 폴백 (LocalTableSource)
//   3) 시트가 통째로 날아갔을 때의 복구본 (2026-08-02 DropTable 삭제 사고)
//
// [게시 CSV 의 캐시 지연 - .pending.csv 가 있는 이유]
//   구글 게시 CSV 는 CDN 이라 시트를 고쳐도 몇 분간 옛값이 섞여 나온다.
//   실측으로 5분 넘게 옛값/새값이 번갈아 나왔다. 그동안 테스트하면 "안 바뀌었네" 하게 된다.
//   그래서 시트에 값을 쓴 쪽이 같은 내용을 <테이블>.pending.csv 로 남겨두면,
//   여기서는 게시본 대신 그걸 쓴다. 게시본이 따라잡으면(내용 일치) 파일은 스스로 지워진다.
//   = 시트를 고친 직후에도 지연 없이 그 값으로 테스트할 수 있다.
// =====================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class SheetCache
{
    /// <summary>요청 1개당 타임아웃(초). 플레이 직전에 도는 경로라 짧게 잡는다.</summary>
    private const int TimeoutSec = 8;

    /// <summary>전체 대기 상한(초). 회선이 죽어도 에디터가 오래 멈추지 않게.</summary>
    private const float DeadlineSec = 12f;

    public struct Result
    {
        public List<string> Ok;         // 갱신된 테이블
        public List<string> Failed;     // 못 받은 테이블 (기존 사본 유지)
        public List<string> UsedPending; // 게시본이 아직 옛값이라 로컬 최신값을 쓴 테이블
    }

    /// <summary>전체 시트를 병렬로 받아 사본을 갱신한다. progressTitle 이 null 이면 진행바를 띄우지 않는다.</summary>
    public static Result RefreshAll(string progressTitle)
    {
        var res = new Result
        {
            Ok = new List<string>(),
            Failed = new List<string>(),
            UsedPending = new List<string>(),
        };

        var sources = new List<(string name, string url)>();
        foreach (var s in AllTableSchemas.GetAll()) sources.Add((s.TableName, s.GoogleSheetUrl));
        // 번역표는 SheetSchema 가 없어서(자유 텍스트 표) 따로 붙인다.
        sources.Add((LocalizationLoader.TableName, LocalizationLoader.URL));

        var reqs = new List<(string name, UnityWebRequest req)>(sources.Count);
        foreach (var s in sources)
        {
            var r = UnityWebRequest.Get(s.url);
            r.timeout = TimeoutSec;
            r.SendWebRequest();          // yield 안 함 -> 전부 동시에 뜬다
            reqs.Add((s.name, r));
        }

        try
        {
            // 전부 끝날 때까지 대기. 에디터가 그동안 멈추지만 어차피 플레이 시작 전이고,
            // 게임에 들어가서 기다리던 시간이 여기로 옮겨온 것뿐이다(총 시간 동일).
            double start = EditorApplication.timeSinceStartup;
            while (true)
            {
                int done = 0;
                foreach (var x in reqs) if (x.req.isDone) done++;
                if (done >= reqs.Count) break;
                if (EditorApplication.timeSinceStartup - start > DeadlineSec) break;

                if (progressTitle != null)
                    EditorUtility.DisplayProgressBar(progressTitle, $"구글 시트 {done}/{reqs.Count}", (float)done / reqs.Count);

                System.Threading.Thread.Sleep(16);   // 통짜 스핀은 코어 하나를 태운다
            }

            foreach (var x in reqs)
            {
                string csv = ReadBody(x.req);
                string pending = ReadPending(x.name);

                if (pending != null)
                {
                    if (csv != null && SameContent(csv, pending))
                    {
                        // 게시본이 따라잡았다 -> 표식 제거
                        TryDelete(LocalTableSource.PendingPath(x.name));
                    }
                    else
                    {
                        // 아직 옛값을 주고 있다 -> 로컬 최신값이 이긴다
                        LocalTableSource.Write(x.name, pending);
                        res.UsedPending.Add(x.name);
                        continue;
                    }
                }

                if (csv == null) { res.Failed.Add(x.name); continue; }   // 기존 사본은 그대로 둔다

                LocalTableSource.Write(x.name, csv);
                res.Ok.Add(x.name);
            }
        }
        finally
        {
            foreach (var x in reqs) x.req.Dispose();
            if (progressTitle != null) EditorUtility.ClearProgressBar();
        }

        return res;
    }

    // 성공 + CSV 로 보이는 응답만 통과. 게시가 해제되면 오류 HTML 이 오는데
    // 그걸 사본에 덮어쓰면 멀쩡한 복구본을 잃는다.
    private static string ReadBody(UnityWebRequest req)
    {
        if (!req.isDone || req.result != UnityWebRequest.Result.Success) return null;
        string text = req.downloadHandler.text;
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.TrimStart().StartsWith("<")) return null;
        return text;
    }

    private static string ReadPending(string tableName)
    {
        try
        {
            string p = LocalTableSource.PendingPath(tableName);
            if (!File.Exists(p)) return null;
            string t = File.ReadAllText(p);
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
        catch { return null; }
    }

    // 줄바꿈/끝 공백 차이로 "다르다"고 잘못 판단하지 않게 맞춰서 비교한다.
    private static bool SameContent(string a, string b)
        => Norm(a) == Norm(b);

    private static string Norm(string s)
        => s.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
