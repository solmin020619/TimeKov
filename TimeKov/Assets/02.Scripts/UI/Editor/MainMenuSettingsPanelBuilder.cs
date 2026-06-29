// =====================================================================
// MainMenuSettingsPanelBuilder.cs  (Editor Only)
// Tools/UI/Build MainMenu Settings Panel
// World.unity의 실제 설정 패널(Canvas/Panels/SettingsPanel)을 그대로 복제해
// MainMenu_Cinematic 캔버스 밑에 붙인다. 그래픽/오디오/조작 UI를 두 번 만들지 않고
// World와 동일한 진본 하나(GlobalSettingsManager + SettingsData)를 그대로 재사용하기 위함.
// World.unity는 복제 출처로만 열고 저장하지 않으므로 원본에는 영향이 없다.
// =====================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MainMenuSettingsPanelBuilder
{
    private const string MenuPath = "Tools/UI/Build MainMenu Settings Panel";
    private const string CanvasName = "MainMenu_Cinematic";
    private const string WorldScenePath = "Assets/01.Scenes/01.BuildScene/World.unity";

    [MenuItem(MenuPath)]
    static void BuildMenuItem()
    {
        var canvasGO = GameObject.Find(CanvasName);
        if (canvasGO == null)
        {
            Debug.LogError($"[MainMenuSettingsPanelBuilder] '{CanvasName}' 캔버스를 찾을 수 없습니다. MainMenu 씬에서 실행하세요.");
            return;
        }
        BuildSettingsPanel(canvasGO);
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    public static GlobalSettingsManager BuildSettingsPanel(GameObject canvasGO)
    {
        Undo.SetCurrentGroupName("Build MainMenu Settings Panel");
        int undoGroup = Undo.GetCurrentGroup();

        var existing = canvasGO.transform.Find("SettingsPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var worldScene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Additive);

        GameObject sourcePanel = null;
        foreach (var root in worldScene.GetRootGameObjects())
        {
            if (root.name != "Canvas") continue;
            var found = root.transform.Find("Panels/SettingsPanel");
            if (found != null) sourcePanel = found.gameObject;
            break;
        }

        if (sourcePanel == null)
        {
            Debug.LogError("[MainMenuSettingsPanelBuilder] World.unity에서 'Canvas/Panels/SettingsPanel'을 찾을 수 없습니다.");
            EditorSceneManager.CloseScene(worldScene, true);
            return null;
        }

        var clone = Object.Instantiate(sourcePanel);
        clone.name = "SettingsPanel";
        // 부모를 MainMenu 쪽 캔버스로 바로 옮겨 World 씬에서 완전히 분리시킨다 — 그래야
        // 아래 CloseScene이 원본엔 손대지 않은 채(저장 없이) World를 안전하게 닫을 수 있다.
        clone.transform.SetParent(canvasGO.transform, false);
        Undo.RegisterCreatedObjectUndo(clone, "Clone SettingsPanel");

        EditorSceneManager.CloseScene(worldScene, true);

        var mgr = clone.GetComponent<GlobalSettingsManager>();
        if (mgr == null)
            Debug.LogWarning("[MainMenuSettingsPanelBuilder] 복제된 패널에 GlobalSettingsManager 컴포넌트가 없습니다.");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[MainMenuSettingsPanelBuilder] World.unity의 설정 패널을 복제해 MainMenu에 붙였습니다.");
        return mgr;
    }
}
#endif
