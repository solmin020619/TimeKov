// =====================================================================
// LocalTableSource.cs
// 구글 시트 다운로드가 실패했을 때 쓰는 로컬 CSV 사본.
//
// [왜 있나]
//   2026-08-02 에 DropTable 시트가 실수로 영구삭제돼 게임이 아예 안 떴다.
//   테이블 하나가 사라지면 GameDataHolder 로드가 통째로 실패하는 구조라,
//   시트 사고 한 번에 팀 전원이 작업을 못 한다.
//   레포에 사본이 있으면 최소한 게임은 뜬다(원본이 시트라는 사실은 그대로다).
//
// [읽는 위치] Documentation/sheet_backup/<테이블이름>.csv
//   '시트 백업' 메뉴(SheetBackupMenu)가 채우는 그 폴더를 그대로 쓴다.
//   Assets 밖이라 유니티가 임포트하지 않고 .meta 도 안 생긴다.
//
// [평상시에는 아무 일도 하지 않는다]
//   다운로드가 성공하면 이 파일은 읽히지 않는다. 폴백 전용이다.
//   대체가 일어나면 콘솔에 경고를 남긴다 - 옛 데이터로 조용히 도는 게 제일 위험하다.
//
// 에디터 전용: 빌드에는 Documentation 폴더가 없다. 정식 빌드는 항상 시트가 원본.
// =====================================================================

using UnityEngine;

public static class LocalTableSource
{
    // 레포 루트 기준 경로. Application.dataPath = <레포>/TimeKov/Assets 라서 두 단계 올라간다.
    public const string DirRelative = "Documentation/sheet_backup";

#if UNITY_EDITOR
    public static string DirFull =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", DirRelative));

    /// <summary>해당 테이블의 로컬 CSV 원문. 없으면 null.</summary>
    public static string TryRead(string tableName)
    {
        try
        {
            string path = System.IO.Path.Combine(DirFull, tableName + ".csv");
            if (!System.IO.File.Exists(path)) return null;
            string text = System.IO.File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[로컬 테이블] {tableName}.csv 읽기 실패: {e.Message}");
            return null;
        }
    }
#else
    // 빌드에는 로컬 경로가 없다. 항상 시트에서 받는다.
    public static string TryRead(string tableName) => null;
#endif
}
