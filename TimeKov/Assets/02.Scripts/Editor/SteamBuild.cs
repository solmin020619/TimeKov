// SteamBuild.cs
// 스팀 업로드용 빌드를 한 번에 만든다.
// ─────────────────────────────────────────────────────────────────────
// 왜 에디터 메뉴로 두나
//   Build Settings 를 손으로 열어 매번 같은 값을 맞추면 언젠가 하나를 빠뜨린다.
//   특히 Development Build 를 켠 채로 올리는 사고가 잦은데, 그러면 스팀에
//   디버그 빌드가 배포된다. 여기서 항상 끄고 나간다.
//
// 기존 빌드 훅과의 관계
//   BuildPipeline.BuildPlayer 를 쓰므로 IPreprocessBuildWithReport 구현체
//   (BuildSheetBackupEmbedder = 오프라인용 시트 사본 동봉)가 그대로 함께 돈다.
//   즉 이 메뉴로 빌드해도 시트 동봉본은 자동으로 갱신된다.
//
// 출력 위치
//   <레포 루트>/Steam/content/  - upload.bat 이 이 폴더를 통째로 올린다.
//   Assets 바깥이라 유니티가 임포트하지 않고, .gitignore 로 커밋도 막는다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SteamBuild
{
    // Application.dataPath = <레포>/TimeKov/Assets -> 두 단계 위가 레포 루트
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

    private static string ContentDir => Path.Combine(RepoRoot, "Steam", "content");

    private const string ExeName = "TimeKov.exe";

    // ═════════════════════════════════════════════════════════════════
    // 빌드
    // ═════════════════════════════════════════════════════════════════

    [MenuItem("Tools/TIMEKOV/스팀/① 출시 전 점검", priority = 100)]
    public static void Check()
    {
        var issues = Inspect();
        if (issues.Count == 0)
        {
            EditorUtility.DisplayDialog("출시 전 점검", "걸리는 항목이 없습니다.", "확인");
            return;
        }

        // 상세는 콘솔로. 다이얼로그에 다 넣으면 잘려서 "로그를 보라" 는 안내만 남는다.
        Debug.LogWarning("[스팀 점검] " + issues.Count + "건\n\n"
                       + string.Join("\n\n", issues));

        EditorUtility.DisplayDialog(
            "출시 전 점검 - " + issues.Count + "건",
            string.Join("\n", issues.Select(Headline))
            + "\n\n자세한 내용은 콘솔에 있습니다.",
            "확인");
    }

    [MenuItem("Tools/TIMEKOV/스팀/② 빌드 만들기", priority = 101)]
    public static void Build()
    {
        var issues = Inspect();
        if (issues.Count > 0)
        {
            // 점검에 걸려도 막지는 않는다. 테스트 빌드를 뽑고 싶을 때가 있다.
            // 다만 무엇이 걸렸는지 반드시 보고 넘어가게 한다.
            Debug.LogWarning("[스팀 점검] " + issues.Count + "건\n\n"
                           + string.Join("\n\n", issues));
            bool go = EditorUtility.DisplayDialog(
                "점검 " + issues.Count + "건",
                string.Join("\n", issues.Select(Headline))
                + "\n\n자세한 내용은 콘솔에 있습니다.\n그래도 빌드할까요?",
                "빌드", "취소");
            if (!go) return;
        }

        string[] scenes = EnabledScenes();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("빌드 중단",
                "Build Settings 에 활성화된 씬이 없습니다.", "확인");
            return;
        }

        // 이전 결과물이 남아 있으면 지운 파일이 그대로 올라간다.
        // 지우는 대상은 우리가 만든 폴더 하나뿐이고, 지우기 전에 반드시 묻는다.
        if (Directory.Exists(ContentDir))
        {
            bool clear = EditorUtility.DisplayDialog(
                "이전 빌드 삭제",
                "아래 폴더를 비우고 새로 빌드합니다.\n\n" + ContentDir +
                "\n\n비우지 않으면 지난 빌드의 잔여 파일이 함께 업로드됩니다.",
                "비우고 빌드", "취소");
            if (!clear) return;

            try
            {
                Directory.Delete(ContentDir, true);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("삭제 실패",
                    "폴더를 비우지 못했습니다. 탐색기나 다른 프로그램이 열고 있는지 확인하세요.\n\n"
                    + e.Message, "확인");
                return;
            }
        }
        Directory.CreateDirectory(ContentDir);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(ContentDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            // Development / AutoRunPlayer 등을 일절 켜지 않는다 = 출시 빌드
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;

        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[스팀 빌드] 실패: " + s.result + " / 오류 " + s.totalErrors + "건");
            EditorUtility.DisplayDialog("빌드 실패",
                "결과: " + s.result + "\n오류: " + s.totalErrors + "건\n\n콘솔을 확인하세요.", "확인");
            return;
        }

        double mb = s.totalSize / (1024.0 * 1024.0);
        string msg = "버전 " + PlayerSettings.bundleVersion
                   + "\n크기 " + mb.ToString("N0") + " MB"
                   + "\n시간 " + s.totalTime.ToString(@"mm\:ss")
                   + "\n\n" + ContentDir;

        Debug.Log("[스팀 빌드] 성공\n" + msg);
        if (EditorUtility.DisplayDialog("빌드 완료", msg + "\n\n폴더를 열까요?", "열기", "닫기"))
            EditorUtility.RevealInFinder(ContentDir);
    }

    [MenuItem("Tools/TIMEKOV/스팀/빌드 폴더 열기", priority = 120)]
    public static void OpenContent()
    {
        Directory.CreateDirectory(ContentDir);
        EditorUtility.RevealInFinder(ContentDir);
    }

    // ═════════════════════════════════════════════════════════════════
    // 점검
    // ═════════════════════════════════════════════════════════════════

    /// <summary>출시 빌드에서 걸리면 곤란한 것들을 모아 돌려준다.</summary>
    private static List<string> Inspect()
    {
        var list = new List<string>();

        // ★가장 중요. 세이브 경로가 여기서 갈린다.
        //   %USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\Saves
        //   출시 후에 바꾸면 기존 플레이어의 세이브를 전부 못 찾는다.
        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName) ||
            PlayerSettings.companyName == "DefaultCompany")
        {
            list.Add("[치명] Company Name 이 기본값(DefaultCompany)입니다.\n"
                   + "  세이브 경로에 그대로 들어갑니다:\n"
                   + "  AppData/LocalLow/" + PlayerSettings.companyName + "/"
                   + PlayerSettings.productName + "/Saves\n"
                   + "  출시 후에 바꾸면 기존 세이브를 전부 잃습니다. 지금 바꾸세요.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.productName))
            list.Add("[치명] Product Name 이 비어 있습니다.");

        if (PlayerSettings.bundleVersion == "0.1.0")
        {
            list.Add("[확인] Version 이 0.1.0 입니다.\n"
                   + "  출시 버전으로 올렸는지 확인하세요. 스팀 빌드 설명에도 이 값을 씁니다.");
        }

        if (EditorUserBuildSettings.development)
        {
            list.Add("[치명] Development Build 가 켜져 있습니다.\n"
                   + "  이 메뉴로 빌드하면 자동으로 꺼지지만, Build Settings 창에서\n"
                   + "  직접 빌드할 때를 대비해 꺼두는 편이 안전합니다.");
        }

        var scenes = EnabledScenes();
        if (scenes.Length == 0)
            list.Add("[치명] Build Settings 에 활성화된 씬이 없습니다.");
        else
        {
            foreach (string p in scenes)
                if (!File.Exists(Path.Combine(RepoRoot, "TimeKov", p)))
                    list.Add("[치명] 등록된 씬 파일이 없습니다: " + p);
        }

        // 실행 해상도는 Default Is Native Resolution 이 켜져 있으면 모니터를 따라간다.
        // 이 값은 그때 '창 모드로 전환했을 때의 창 크기' 로만 쓰인다.
        // 그 구분을 빼고 경고하면 "게임이 1024x768 로 뜬다" 는 오해를 준다.
        if (PlayerSettings.defaultScreenWidth < 1280)
        {
            list.Add("[확인] 창 모드 기본 크기가 "
                   + PlayerSettings.defaultScreenWidth + "x"
                   + PlayerSettings.defaultScreenHeight + " 입니다.\n"
                   + (PlayerSettings.defaultIsNativeResolution
                       ? "  실행 해상도는 Native Resolution 이 켜져 있어 모니터를 따라갑니다.\n"
                       + "  이 값은 플레이어가 창 모드로 바꿨을 때만 쓰입니다.\n"
                       + "  고해상도 모니터에서 아주 작은 창이 되므로 1600x900 이상을 권합니다."
                       : "  Native Resolution 이 꺼져 있어 이 크기로 실행됩니다. 반드시 올리세요."));
        }

        var backend = PlayerSettings.GetScriptingBackend(
            UnityEditor.Build.NamedBuildTarget.Standalone);
        if (backend == ScriptingImplementation.Mono2x)
        {
            list.Add("[검토] 스크립팅 백엔드가 Mono 입니다.\n"
                   + "  IL2CPP 가 실행 성능이 낫고 코드를 그대로 열어보기 어렵습니다.\n"
                   + "  다만 빌드 시간이 크게 늘고 초기에 오류가 날 수 있어,\n"
                   + "  바꾼다면 출시 직전이 아니라 지금 한 번 시험해 보세요.");
        }

        return list;
    }

    /// <summary>여러 줄짜리 항목에서 첫 줄만 뽑는다.
    /// 다이얼로그는 본문이 길면 통째로 잘라내고 "로그를 보라" 는 안내로 바꿔 버린다.
    /// 그래서 창에는 한 줄씩만 띄우고 상세는 콘솔에 남긴다.</summary>
    private static string Headline(string issue)
    {
        int i = issue.IndexOf('\n');
        return i < 0 ? issue : issue.Substring(0, i);
    }

    private static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();
    }
}
