#if UNITY_EDITOR
// =====================================================================
// PlayerStatPanelBuilder.cs
// 캐릭터 스탯창(C키)에 새 디자인을 한 번에 적용하는 도구.
//   Tools/TIMEKOV/UI/캐릭터 스탯창 새 디자인 적용
//
// [하는 일]
//   스탯창 오브젝트(Character_stat)에 PlayerStatPanelStyle 을 붙인다. 그게 전부다 —
//   배치·색·정리는 전부 실행 중에 그 컴포넌트가 한다.
//
// [왜 프리팹을 직접 고치나]
//   스탯창은 씬이 아니라 Canvas.prefab 안에 있다. 씬에 놓인 인스턴스에 컴포넌트를 붙이면
//   그 씬에만 남는 오버라이드가 되어, 다른 씬에서는 옛 디자인이 그대로 나온다.
//   그래서 프리팹 에셋을 열어 붙이고 저장한다(LoadPrefabContents → SaveAsPrefabAsset).
//
// [찾는 방법]
//   이름이 아니라 PlayerStatHUD 컴포넌트로 찾는다. 스탯창의 정의가 곧 '그 스크립트가
//   붙어 있는 오브젝트'라, 나중에 이름을 바꿔도 이 도구는 계속 동작한다.
//
// 여러 번 돌려도 안전하다 — 이미 붙어 있으면 알려주고 끝낸다.
// =====================================================================


using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerStatPanelBuilder
{
    // 예전 버전이 시간 게이지 옆에 따로 만들어 두던 '초당 감소량' 글자.
    //   지금은 체력 숫자 뒤에 괄호로 붙이므로(PlayerStatHUD) 더 이상 쓰지 않는다.
    //   이미 만들어진 프리팹/씬에서 치우려고 이름만 남겨 둔다.
    const string DrainName = "MaxTimeDrain";

    [MenuItem("Tools/TIMEKOV/UI/캐릭터 스탯창 새 디자인 적용")]
    static void Apply()
    {
        // 1) 프리팹 안에 있는 경우 — 에셋을 직접 고쳐 모든 씬에 반영한다.
        string prefabPath = FindPrefabPath();
        if (!string.IsNullOrEmpty(prefabPath))
        {
            ApplyToPrefab(prefabPath);
            return;
        }

        // 2) 프리팹이 아니라 씬에 직접 놓여 있는 경우.
        var inScene = Object.FindFirstObjectByType<PlayerStatHUD>(FindObjectsInactive.Include);
        if (inScene == null)
        {
            EditorUtility.DisplayDialog("캐릭터 스탯창",
                "스탯창을 찾지 못했습니다.\n\n" +
                "PlayerStatHUD 가 붙은 오브젝트(Character_stat)가 있는 씬을 열거나, " +
                "그 오브젝트가 든 프리팹이 프로젝트에 있는지 확인해 주세요.", "확인");
            return;
        }

        bool added = false;
        if (inScene.GetComponent<PlayerStatPanelStyle>() == null)
        {
            Undo.AddComponent<PlayerStatPanelStyle>(inScene.gameObject);
            added = true;
        }

        // ★이미 붙어 있어도 여기서 끝내면 안 된다. 예전 버전이 남긴 조각을 치워야 한다.
        bool cleaned = RemoveDrainText(inScene, inSceneObject: true)
                     | RemoveRevealEffect(inScene, inSceneObject: true);   // ★단축 || 아님 — 둘 다 실행돼야 한다

        EditorSceneManager.MarkSceneDirty(inScene.gameObject.scene);
        Selection.activeObject = inScene.gameObject;
        Debug.Log($"[스탯창] {inScene.name} — 디자인 {(added ? "적용" : "이미 적용됨")}, " +
                  $"예전 조각 {(cleaned ? "정리함" : "없음")}. 씬을 저장하세요 (Ctrl+S).", inScene);
    }

    // ==================================================================
    /// <summary>스탯창이 든 프리팹의 경로. 없으면 빈 문자열.
    ///
    /// 열려 있는 씬의 인스턴스에서 역추적하는 것을 먼저 시도한다 — 프로젝트 전체를 뒤지는 것보다
    /// 빠르고, '지금 쓰는' 프리팹을 정확히 집는다. 씬이 안 열려 있으면 그때 프로젝트를 뒤진다.</summary>
    static string FindPrefabPath()
    {
        var inScene = Object.FindFirstObjectByType<PlayerStatHUD>(FindObjectsInactive.Include);
        if (inScene != null)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(inScene.gameObject);
            if (root != null)
            {
                string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return "";   // 씬에 직접 놓인 오브젝트 — 프리팹이 아니다
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.GetComponentInChildren<PlayerStatHUD>(true) != null) return path;
        }
        return "";
    }

    static void ApplyToPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogError($"[스탯창] 프리팹을 열지 못했습니다: {path}");
            return;
        }

        try
        {
            var hud = root.GetComponentInChildren<PlayerStatHUD>(true);
            if (hud == null)
            {
                Debug.LogError($"[스탯창] {path} 안에서 PlayerStatHUD 를 찾지 못했습니다.");
                return;
            }

            bool added = false;
            if (hud.GetComponent<PlayerStatPanelStyle>() == null)
            {
                hud.gameObject.AddComponent<PlayerStatPanelStyle>();
                added = true;
            }

            // ★이미 붙어 있어도 여기서 끝내면 안 된다. 예전 버전이 남긴 조각을 치워야 한다.
            bool cleaned = RemoveDrainText(hud, inSceneObject: false)
                         | RemoveRevealEffect(hud, inSceneObject: false);   // ★단축 || 아님 — 둘 다 실행돼야 한다
            if (!added && !cleaned)
            {
                Debug.Log($"[스탯창] 이미 최신 상태입니다 ({path}).");
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);

            Debug.Log($"[스탯창] {hud.name} — 디자인 {(added ? "적용" : "이미 적용됨")}, " +
                      $"예전 조각 {(cleaned ? "정리함" : "없음")}. 프리팹을 저장했습니다.\n{path}\n" +
                      "배치·색·기존 장식 정리는 실행할 때 자동으로 처리됩니다.",
                      AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        finally
        {
            // ★반드시 언로드해야 한다. 예외로 빠져나가도 남으면 에디터에 유령 씬이 쌓인다.
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ==================================================================
    /// <summary>예전 여닫기 연출(StatPanelRevealEffect)을 떼어 낸다. 뗐으면 true.
    ///
    /// 지금은 설정창과 같은 연출(MenuPanelAnim)을 쓴다. 둘이 같이 붙어 있으면
    /// 같은 프레임에 alpha·localScale·anchoredPosition 을 서로 덮어써서 창이 떨린다.
    /// (StatPanelRevealEffect 는 OnEnable 에서 스스로 재생을 시작한다 — 아무도 안 불러도 돈다)</summary>
    static bool RemoveRevealEffect(PlayerStatHUD hud, bool inSceneObject)
    {
        var fx = hud.GetComponent<StatPanelRevealEffect>();
        if (fx == null) return false;

        if (inSceneObject) Undo.DestroyObjectImmediate(fx);
        else Object.DestroyImmediate(fx, true);
        return true;
    }

    // ==================================================================
    /// <summary>예전 버전이 만들어 둔 '초당 감소량' 전용 글자를 치운다. 치웠으면 true.
    ///
    /// 지금은 감소량을 별도 글자가 아니라 체력 숫자 뒤에 괄호로 붙인다(80(-1/s) / 300).
    /// 체력 숫자는 자릿수에 따라 좌우로 움직이는데, 떨어져 있는 글자는 그걸 못 따라가서
    /// 사이가 벌어지기 때문이다.
    ///
    /// 남겨 두면 빈 글자가 계속 따라다니고, PlayerStatPanelStyle 이 '자리를 못 받은 조각'
    /// 으로 보고 매번 경고를 띄운다. 이 메뉴를 한 번 더 돌리면 조용히 사라진다.</summary>
    static bool RemoveDrainText(PlayerStatHUD hud, bool inSceneObject)
    {
        var old = hud.transform.Find(DrainName);
        if (old == null) return false;

        if (inSceneObject) Undo.DestroyObjectImmediate(old.gameObject);
        else Object.DestroyImmediate(old.gameObject, true);
        return true;
    }

    // ==================================================================
    [MenuItem("Tools/TIMEKOV/UI/캐릭터 스탯창 새 디자인 제거")]
    static void Remove()
    {
        string prefabPath = FindPrefabPath();
        if (!string.IsNullOrEmpty(prefabPath))
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var s = root != null ? root.GetComponentInChildren<PlayerStatPanelStyle>(true) : null;
                if (s == null) { Debug.Log("[스탯창] 붙어 있지 않습니다."); return; }
                Object.DestroyImmediate(s, true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[스탯창] 새 디자인을 제거했습니다.\n{prefabPath}");
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            return;
        }

        var inScene = Object.FindFirstObjectByType<PlayerStatPanelStyle>(FindObjectsInactive.Include);
        if (inScene == null) { Debug.Log("[스탯창] 붙어 있지 않습니다."); return; }
        Undo.DestroyObjectImmediate(inScene);
        EditorSceneManager.MarkSceneDirty(inScene.gameObject.scene);
        Debug.Log("[스탯창] 새 디자인을 제거했습니다.");
    }

    // 제거는 '되돌리고 싶을 때'만 쓰는 메뉴라, 붙어 있지 않으면 회색으로 비활성화한다.
    [MenuItem("Tools/TIMEKOV/UI/캐릭터 스탯창 새 디자인 제거", true)]
    static bool RemoveValidate()
    {
        var inScene = Object.FindFirstObjectByType<PlayerStatPanelStyle>(FindObjectsInactive.Include);
        if (inScene != null) return true;

        // 씬에 없으면 프리팹 쪽을 본다(에셋을 여는 건 비싸서 경로 확인까지만).
        return !string.IsNullOrEmpty(FindPrefabPathCheap());
    }

    /// <summary>메뉴 활성화 판정용 — 프로젝트 전체 검색은 하지 않는다.
    /// 검증 함수는 메뉴가 열릴 때마다 불려서, 무거운 검색을 넣으면 에디터가 끊긴다.</summary>
    static string FindPrefabPathCheap()
    {
        var inScene = Object.FindFirstObjectByType<PlayerStatHUD>(FindObjectsInactive.Include);
        if (inScene == null) return "";
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(inScene.gameObject);
        return root == null ? "" : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
    }
}
#endif
