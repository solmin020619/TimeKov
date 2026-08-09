// =====================================================================
// SheetBackupMenu.cs (Editor Only)
// 시트/백업 저장 (시트가 날아갔을 때 복구용)
//
// 구글 시트를 CSV 로 받아 Documentation/sheet_backup/ 에 저장한다.
// 실제 받는 일은 SheetCache 가 한다(Play 누를 때 도는 것과 같은 코드).
//
// 왜 필요한가: 2026-08-02 에 DropTable 시트가 실수로 영구삭제돼 게임이 아예 안 떴다.
//   시트가 유일한 원본이라 구글 고객센터 복구에 매달려야 했다.
//   CSV 는 다 합쳐야 수십 KB 라 레포에 넣어두면 다음엔 붙여넣기로 5분이면 끝난다.
//
// 저장 위치가 Assets 밖인 이유: Assets 안에 두면 유니티가 TextAsset 으로 임포트해서
//   .meta 가 생기고 커밋마다 딸려 다닌다. 백업은 사람이 읽고 붙여넣는 용도라 밖이 낫다.
//
// ★평소엔 이 메뉴를 누를 일이 거의 없다. Play 를 누를 때마다 자동으로 갱신된다.
//   시트를 손으로 고친 뒤 플레이는 안 하고 사본만 커밋하고 싶을 때 쓴다.
// =====================================================================

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SheetBackupMenu
{
    [MenuItem("시트/백업 저장 (시트가 날아갔을 때 복구용)")]
    public static void BackupAll()
    {
        var res = SheetCache.RefreshAll("시트 백업 저장");

        var msg = new StringBuilder();
        msg.AppendLine($"저장 위치: {LocalTableSource.DirRelative}");
        msg.AppendLine();
        msg.AppendLine($"갱신 {res.Ok.Count}개");

        if (res.UsedPending.Count > 0)
        {
            msg.AppendLine();
            msg.AppendLine($"게시본 반영 대기중 {res.UsedPending.Count}개 (방금 쓴 값을 유지했다)");
            foreach (var s in res.UsedPending) msg.AppendLine("  " + s);
        }

        if (res.Failed.Count > 0)
        {
            msg.AppendLine();
            msg.AppendLine($"실패 {res.Failed.Count}개 (기존 사본 유지 - 게시 해제 여부 확인)");
            foreach (var s in res.Failed) msg.AppendLine("  " + s);
        }

        if (res.Failed.Count > 0) Debug.LogWarning("[시트 백업] 일부 실패\n" + msg);
        else Debug.Log("[시트 백업] 완료\n" + msg);

        EditorUtility.DisplayDialog(res.Failed.Count > 0 ? "시트 백업 - 일부 실패" : "시트 백업 완료",
            msg.ToString() + "\n변경분을 커밋해 두면 시트가 날아가도 복구할 수 있다.", "확인");
    }
}
