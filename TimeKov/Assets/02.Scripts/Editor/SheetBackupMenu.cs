// =====================================================================
// SheetBackupMenu.cs (Editor Only)
// Tools/TIMEKOV/시트 백업
//
// 구글 시트 8종을 CSV 로 받아 Documentation/sheet_backup/ 에 저장한다.
//
// 왜 필요한가: 2026-08-02 에 DropTable 시트가 실수로 영구삭제돼 게임이 아예 안 떴다.
//   시트가 유일한 원본이라 구글 고객센터 복구에 매달려야 했다.
//   CSV 는 다 합쳐야 수십 KB 라 레포에 넣어두면 다음엔 붙여넣기로 5분이면 끝난다.
//
// 저장 위치가 Assets 밖인 이유: Assets 안에 두면 유니티가 TextAsset 으로 임포트해서
//   .meta 가 생기고 커밋마다 딸려 다닌다. 백업은 사람이 읽고 붙여넣는 용도라 밖이 낫다.
//
// 밸런싱 시트를 고친 날엔 이 메뉴 한 번 눌러서 갱신하고 같이 커밋할 것.
// =====================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class SheetBackupMenu
{
    // Assets 의 부모(프로젝트 루트) 기준. 레포 루트에 Documentation 폴더가 이미 있다.
    private const string BackupDir = "Documentation/sheet_backup";

    [MenuItem("Tools/TIMEKOV/시트 백업")]
    public static void BackupAll()
    {
        var schemas = AllTableSchemas.GetAll();
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", BackupDir));
        Directory.CreateDirectory(dir);

        var ok = new List<string>();
        var fail = new List<string>();

        try
        {
            for (int i = 0; i < schemas.Count; i++)
            {
                var s = schemas[i];
                EditorUtility.DisplayProgressBar("시트 백업", $"{s.TableName} 받는 중...", (float)i / schemas.Count);

                string csv = Download(s.GoogleSheetUrl, out string err);
                if (csv == null)
                {
                    fail.Add($"{s.TableName} - {err}");
                    continue;
                }
                // 내려받은 게 CSV 가 아니라 오류 HTML 이면 덮어쓰지 않는다(멀쩡한 백업을 날리지 않게).
                if (csv.TrimStart().StartsWith("<"))
                {
                    fail.Add($"{s.TableName} - CSV 가 아닌 응답(게시 해제 의심)");
                    continue;
                }

                File.WriteAllText(Path.Combine(dir, s.TableName + ".csv"), csv, new UTF8Encoding(false));
                int rows = csv.Split('\n').Length - 1;   // 헤더 제외 근사치
                ok.Add($"{s.TableName} ({rows}행)");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var msg = new StringBuilder();
        msg.AppendLine($"저장 위치: {BackupDir}");
        msg.AppendLine();
        msg.AppendLine($"성공 {ok.Count}개");
        foreach (var s in ok) msg.AppendLine("  " + s);
        if (fail.Count > 0)
        {
            msg.AppendLine();
            msg.AppendLine($"실패 {fail.Count}개");
            foreach (var s in fail) msg.AppendLine("  " + s);
        }

        if (fail.Count > 0) Debug.LogWarning("[시트 백업] 일부 실패\n" + msg);
        else Debug.Log("[시트 백업] 완료\n" + msg);

        EditorUtility.DisplayDialog(fail.Count > 0 ? "시트 백업 - 일부 실패" : "시트 백업 완료",
            msg.ToString() + "\n변경분을 커밋해 두면 시트가 날아가도 복구할 수 있다.", "확인");
    }

    // 에디터라 블로킹해도 된다. 리다이렉트(구글 게시 URL 은 307 로 넘어간다) 따라간다.
    private static string Download(string url, out string error)
    {
        error = null;
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 30;
            var op = req.SendWebRequest();
            while (!op.isDone) { }
            if (req.result != UnityWebRequest.Result.Success)
            {
                error = $"{(int)req.responseCode} {req.error}";
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
