// =====================================================================
// LocalTableSource.cs
// 구글 시트를 대신할 로컬 CSV. 두 곳에 나눠 둔다.
//
//   1) 작업용 캐시  : TimeKov/Library/SheetCache/<테이블>.csv
//      Play 를 누를 때마다 갱신된다. gitignore 라 커밋에 안 딸려온다.
//      직행 플레이는 이걸 읽으므로 항상 최신 시트값으로 시작한다.
//
//   2) 레포 사본    : Documentation/sheet_backup/<테이블>.csv
//      시트가 통째로 날아갔을 때의 복구본이자 최후 폴백.
//      ★'시트 > 백업 저장' 메뉴를 눌렀을 때만 갱신된다.
//
// [왜 갈랐나]
//   예전엔 Play 할 때마다 레포 사본까지 덮어썼다. 그러면 남이 시트를 조금만 고쳐도
//   내 작업 트리에 CSV 변경이 계속 생겨서, 커밋할 때마다 관계없는 데이터 변경이 섞였다.
//   갱신은 캐시가 받고, 레포 사본은 사람이 원할 때만 손대게 한다.
//
//   3) 빌드 동봉본 : Assets/Resources/SheetBackup/<테이블>.txt
//      빌드 직전에 BuildSheetBackupEmbedder 가 레포 사본을 복사해 넣는다.
//      인터넷이 없거나 구글이 막혔을 때 게임이 아예 안 켜지는 것을 막는 최후 폴백.
//
// [읽는 순서] 에디터: 캐시 -> 레포 사본 / 빌드: 동봉본. 새로 클론한 직후엔 캐시가 없으니 레포 사본으로 뜬다.
//
// [왜 있나 - 레포 사본]
//   2026-08-02 에 DropTable 시트가 실수로 영구삭제돼 게임이 아예 안 떴다.
//   테이블 하나가 사라지면 GameDataHolder 로드가 통째로 실패하는 구조라,
//   시트 사고 한 번에 팀 전원이 작업을 못 한다.
//
// 어느 경로로 뜨든 원본은 시트다. 폴백은 '시트를 못 받았을 때만' 쓰이고, 받으면 항상 시트가 이긴다.
// =====================================================================

using UnityEngine;

public static class LocalTableSource
{
    // 레포 루트 기준 경로. Application.dataPath = <레포>/TimeKov/Assets 라서 두 단계 올라간다.
    public const string DirRelative = "Documentation/sheet_backup";

#if UNITY_EDITOR
    /// <summary>레포에 커밋되는 복구본 폴더.</summary>
    public static string DirFull =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", DirRelative));

    /// <summary>매 플레이 갱신되는 작업용 캐시 폴더. Library 라서 커밋되지 않는다.</summary>
    public static string CacheDirFull =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Library", "SheetCache"));

    /// <summary>해당 테이블의 CSV 원문. 캐시 우선, 없으면 레포 사본. 둘 다 없으면 null.</summary>
    public static string TryRead(string tableName)
    {
        return ReadFile(System.IO.Path.Combine(CacheDirFull, tableName + ".csv"))
            ?? ReadFile(System.IO.Path.Combine(DirFull, tableName + ".csv"));
    }

    /// <summary>작업용 캐시를 갱신한다(커밋에 안 잡히는 쪽).</summary>
    public static void WriteCache(string tableName, string csv)
        => WriteFile(CacheDirFull, tableName, csv);

    /// <summary>레포 복구본을 갱신한다. ★'시트 > 백업 저장' 에서만 부른다.</summary>
    public static void WriteRepoBackup(string tableName, string csv)
        => WriteFile(DirFull, tableName, csv);

    private static string ReadFile(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            string text = System.IO.File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[로컬 테이블] 읽기 실패 {path}: {e.Message}");
            return null;
        }
    }

    // BOM 없는 UTF-8 - 사람이 붙여넣는 파일이라 편집기 호환이 중요하다.
    private static void WriteFile(string dir, string tableName, string csv)
    {
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, tableName + ".csv"),
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
    ///
    /// 위치는 레포 폴더 그대로 두되 gitignore 되어 있다(시트에 쓰는 쪽이 찾기 쉬운 자리).
    /// </summary>
    public static string PendingPath(string tableName)
        => System.IO.Path.Combine(DirFull, tableName + ".pending.csv");
#else
    /// <summary>
    /// 빌드에 동봉한 사본을 읽는다. 시트를 못 받았을 때만 쓰인다(시트가 되면 항상 시트가 이긴다).
    ///
    /// [왜 필요한가] 예전엔 빌드에서 이게 null 이라 폴백이 아예 없었다. 인터넷이 없거나
    ///   구글이 잠깐 막히면 로딩씬이 "재시도 중"만 반복하며 게임에 못 들어갔다. 시연/심사처럼
    ///   네트워크를 통제 못 하는 자리에서 게임이 안 켜지는 건 감수할 수 없는 실패다.
    ///
    /// 파일은 BuildSheetBackupEmbedder(빌드 직전 훅)가 Resources/SheetBackup 으로 복사한다.
    /// ★확장자가 .txt 인 이유: 유니티는 .csv 를 TextAsset 으로 잡지 않는다.
    /// </summary>
    public static string TryRead(string tableName)
    {
        var asset = Resources.Load<TextAsset>(ResourcePath + tableName);
        return asset != null && !string.IsNullOrWhiteSpace(asset.text) ? asset.text : null;
    }
#endif

    /// <summary>빌드 동봉본의 Resources 경로(확장자 없이). 에디터의 복사 훅과 공유한다.</summary>
    public const string ResourcePath = "SheetBackup/";
}
