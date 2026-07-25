using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 일회용 편의 메뉴. 도감 몬스터 설정(CodexPreviewConfig)에 05.Prefabs/Enemy/Enemy_* 8종을
// 자동으로 채운다(직접 드래그 대신). 이미 들어있는 프리팹은 보존(프레이밍 튜닝 안 날림).
// 메뉴: Tools > TIMEKOV > Codex > 몬스터 설정 자동 채우기
public static class CodexConfigPopulator
{
    // [MenuItem("Tools/TIMEKOV/Codex/몬스터 설정 자동 채우기")]   // 메뉴 정리: 숨김(필요시 주석 해제)
    public static void Populate()
    {
        // 1) 설정 에셋 찾기(없으면 Resources/Codex 에 생성)
        CodexPreviewConfig cfg;
        string cfgPath;
        var found = AssetDatabase.FindAssets("t:CodexPreviewConfig");
        if (found.Length > 0)
        {
            cfgPath = AssetDatabase.GUIDToAssetPath(found[0]);
            cfg = AssetDatabase.LoadAssetAtPath<CodexPreviewConfig>(cfgPath);
        }
        else
        {
            cfgPath = ResourcesCodexFolder() + "/CodexPreviewConfig.asset";
            cfg = ScriptableObject.CreateInstance<CodexPreviewConfig>();
            AssetDatabase.CreateAsset(cfg, cfgPath);
        }

        // 2) Enemy_ 프리팹 수집(이름순)
        var prefabs = AssetDatabase.FindAssets("t:GameObject Enemy_")
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.name.StartsWith("Enemy_"))
            .OrderBy(p => p.name)
            .ToList();

        // 3) 이미 있는 프리팹은 건너뛰고 추가(튜닝 보존)
        int added = 0;
        foreach (var p in prefabs)
        {
            if (cfg.monsters.Any(m => m != null && m.prefab == p)) continue;
            cfg.monsters.Add(new CodexPreviewConfig.MonsterEntry
            {
                displayName = p.name.Replace("Enemy_", ""),
                prefab = p,
                state = CodexPreviewConfig.RevealState.Revealed
            });
            added++;
        }

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
        Debug.Log($"[Codex] 설정 채움 완료: 신규 {added}개, 총 {cfg.monsters.Count}개. 경로 {cfgPath}");
    }

    // 기존 영상 튜토(TutorialVideoObjective)의 페이지를 도감 튜토 설정으로 복붙.
    // 영상 추가/삭제 따라가게 매번 새로 채운다(튜토는 별도 튜닝 없음).
    // [MenuItem("Tools/TIMEKOV/Codex/튜토리얼 영상 설정 자동 채우기")]   // 메뉴 정리: 숨김(필요시 주석 해제)
    public static void PopulateTutorial()
    {
        CodexTutorialConfig cfg;
        string cfgPath;
        var found = AssetDatabase.FindAssets("t:CodexTutorialConfig");
        if (found.Length > 0)
        {
            cfgPath = AssetDatabase.GUIDToAssetPath(found[0]);
            cfg = AssetDatabase.LoadAssetAtPath<CodexTutorialConfig>(cfgPath);
        }
        else
        {
            cfgPath = ResourcesCodexFolder() + "/CodexTutorialConfig.asset";
            cfg = ScriptableObject.CreateInstance<CodexTutorialConfig>();
            AssetDatabase.CreateAsset(cfg, cfgPath);
        }

        cfg.pages.Clear();
        var objs = AssetDatabase.FindAssets("t:TutorialVideoObjective")
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<TutorialVideoObjective>(p))
            .Where(o => o != null && o.pages != null);
        foreach (var o in objs)
            foreach (var p in o.pages)
                if (p != null) cfg.pages.Add(p);   // clip/title/body 그대로 복붙

        // 발견 팝업(DiscoveryCueSet)의 페이지도 도감 재시청 대상에 포함.
        // 이벤트로 뜨는 설명(설비 소개/귀환석/전송 등)도 놓쳤거나 다시 보고 싶을 때 도감서 열람.
        var cueSets = AssetDatabase.FindAssets("t:DiscoveryCueSet")
            .Select(g => AssetDatabase.LoadAssetAtPath<DiscoveryCueSet>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null && s.cues != null);
        foreach (var s in cueSets)
            foreach (var c in s.cues)
                if (c != null && c.pages != null)
                    foreach (var p in c.pages)
                        if (p != null) cfg.pages.Add(p);

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
        Debug.Log($"[Codex] 튜토 설정 채움 완료: 영상 페이지 {cfg.pages.Count}개. 경로 {cfgPath}");
    }

    // 기존 Resources/Codex 폴더를 찾고, 없으면 Assets/Resources/Codex 생성.
    static string ResourcesCodexFolder()
    {
        var match = Directory
            .GetDirectories(Application.dataPath, "Codex", SearchOption.AllDirectories)
            .Select(d => d.Replace('\\', '/'))
            .FirstOrDefault(d => d.Contains("/Resources/Codex"));
        if (match != null)
            return "Assets" + match.Substring(Application.dataPath.Length);

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Codex"))
            AssetDatabase.CreateFolder("Assets/Resources", "Codex");
        return "Assets/Resources/Codex";
    }
}
