// =====================================================================
// BuildSheetBackupEmbedder.cs
// 빌드 직전에 레포 사본(Documentation/sheet_backup/*.csv)을
// Assets/Resources/SheetBackup/*.txt 로 복사해 빌드에 동봉한다.
//
// [왜 있나]
//   빌드에는 레포 폴더가 없어서 시트를 못 받으면 폴백이 아예 없었다. 인터넷이 없거나
//   구글이 잠깐 막히면 로딩씬이 "재시도 중"만 반복하며 게임에 못 들어간다.
//   시연/심사처럼 네트워크를 통제할 수 없는 자리에서 게임이 안 켜지는 건 감수할 수 없다.
//
// [원본은 여전히 시트다]
//   동봉본은 '못 받았을 때만' 쓰인다. 받으면 항상 시트가 이긴다(GameDataHolder 의 폴백 순서).
//
// [사람이 할 일]
//   밸런스를 확정한 뒤 '시트/백업 저장' 을 한 번 누르는 것뿐. 그러면 레포 사본이 최신이 되고,
//   빌드할 때 이 훅이 알아서 복사한다. 안 눌러도 온라인은 멀쩡하고, 오프라인일 때만 옛 수치로 뜬다.
//   그래서 사본이 낡았으면 아래에서 경고하고, 아예 없으면 빌드를 막는다.
//
// ★확장자를 .txt 로 바꾸는 이유: 유니티는 .csv 를 TextAsset 으로 임포트하지 않는다.
//   Resources.Load<TextAsset> 이 null 을 돌려주고, 폴백이 조용히 죽는다.
// =====================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildSheetBackupEmbedder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string TargetDir = "Assets/Resources/SheetBackup";

    // 이보다 오래된 사본이면 경고. 막지는 않는다 - 온라인이면 아무 영향이 없어서
    // 빌드를 세울 만한 사유는 아니다. 다만 모르고 지나가면 안 되니 로그로 남긴다.
    private const int StaleDays = 14;

    public void OnPreprocessBuild(BuildReport report) => Embed();

    [MenuItem("시트/빌드 동봉본 지금 갱신")]
    public static void EmbedFromMenu()
    {
        int n = Embed();
        EditorUtility.DisplayDialog("빌드 동봉본",
            $"{n}개 테이블을 {TargetDir} 에 넣었다.\n\n" +
            "빌드할 때 자동으로도 실행되니 평소엔 누를 필요 없다.", "확인");
    }

    /// <summary>레포 사본을 Resources 로 복사한다. 반환 = 복사한 테이블 수.</summary>
    private static int Embed()
    {
        // ★AllTableSchemas 만으로는 부족하다. Localization 은 스키마 목록에 없고
        //   LocalizationLoader 가 따로 받는다 - 여기서 빠뜨리면 오프라인 빌드에 번역이 통째로 없다.
        var names = new List<string>();
        foreach (var s in AllTableSchemas.GetAll()) names.Add(s.TableName);
        if (!names.Contains(LocalizationLoader.TableName)) names.Add(LocalizationLoader.TableName);

        string srcDir = LocalTableSource.DirFull;

        if (!Directory.Exists(srcDir))
            throw new BuildFailedException(
                $"[빌드 동봉] 레포 사본 폴더가 없다: {srcDir}\n" +
                "'시트/백업 저장' 을 한 번 눌러 사본을 만들어라.");

        Directory.CreateDirectory(TargetDir);

        var missing = new List<string>();
        var stale   = new List<string>();
        int copied  = 0;

        foreach (var name in names)
        {
            string src = Path.Combine(srcDir, name + ".csv");
            if (!File.Exists(src)) { missing.Add(name); continue; }

            string text = File.ReadAllText(src);
            if (string.IsNullOrWhiteSpace(text)) { missing.Add(name); continue; }

            // BOM 없는 UTF-8 로 통일 - CsvReader 가 그대로 파싱한다.
            File.WriteAllText(Path.Combine(TargetDir, name + ".txt"),
                              text, new System.Text.UTF8Encoding(false));
            copied++;

            if ((System.DateTime.Now - File.GetLastWriteTime(src)).TotalDays > StaleDays)
                stale.Add(name);
        }

        // ★한 장이라도 빠지면 빌드를 세운다. 절반만 동봉하면 오프라인에서 '일부 테이블만 빈' 상태로
        //   뜨는데, 그건 아예 안 켜지는 것보다 진단하기 어렵다(GameDataHolder 도 같은 이유로 전부 아니면 실패).
        if (missing.Count > 0)
            throw new BuildFailedException(
                $"[빌드 동봉] 레포 사본에 없는 테이블: {string.Join(", ", missing)}\n" +
                "'시트/백업 저장' 을 눌러 사본을 채운 뒤 다시 빌드해라.");

        // 낡은 사본 - 온라인이면 무해하므로 막지 않고 알리기만 한다.
        if (stale.Count > 0)
            Debug.LogWarning(
                $"[빌드 동봉] 사본이 {StaleDays}일 이상 낡았다: {string.Join(", ", stale)}\n" +
                "오프라인으로 켜면 이 수치로 뜬다. 최신 밸런스를 담으려면 '시트/백업 저장' 후 다시 빌드해라.");

        AssetDatabase.Refresh();
        Debug.Log($"[빌드 동봉] 테이블 {copied}개를 {TargetDir} 에 넣었다 (오프라인 폴백).");
        return copied;
    }
}
