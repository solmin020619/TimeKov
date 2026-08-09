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

    /// <summary>사본을 갱신한다. BOM 없는 UTF-8 - 붙여넣기용이라 편집기 호환이 중요하다.</summary>
    public static void Write(string tableName, string csv)
    {
        try
        {
            System.IO.Directory.CreateDirectory(DirFull);
            System.IO.File.WriteAllText(System.IO.Path.Combine(DirFull, tableName + ".csv"),
                                        csv, new System.Text.UTF8Encoding(false));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[로컬 테이블] {tableName}.csv 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 시트에 방금 쓴 값인데 게시본이 아직 반영 못한 것. 있으면 이게 이긴다.
    ///
    /// [왜 필요한가] 구글 게시 CSV 는 CDN 캐시라 값을 고쳐도 몇 분간 옛값이 섞여 나온다
    /// (실측: 5분 넘게 옛값/새값이 번갈아 나왔다). 그동안 테스트하면 안 바뀐 줄 알게 된다.
    /// 시트에 쓰는 쪽이 같은 내용을 여기에 남겨두면 그 지연을 통째로 건너뛴다.
    /// 게시본이 따라잡으면(내용 일치) 이 파일은 스스로 사라진다.
    /// </summary>
    public static string PendingPath(string tableName)
        => System.IO.Path.Combine(DirFull, tableName + ".pending.csv");
#else
    // 빌드에는 로컬 경로가 없다. 항상 시트에서 받는다.
    public static string TryRead(string tableName) => null;
#endif
}
