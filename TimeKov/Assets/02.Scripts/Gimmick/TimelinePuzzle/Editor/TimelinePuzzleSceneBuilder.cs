#if UNITY_EDITOR
// =====================================================================
// TimelinePuzzleSceneBuilder.cs
// 시간선 복원 퍼즐을 씬에 앉히는 도구 두 개.
//   Tools/TIMEKOV/기믹/시간선 퍼즐 1) 패널 생성·연결
//   Tools/TIMEKOV/기믹/시간선 퍼즐 2) 선택 오브젝트를 퍼즐 장치로
//
// [패널은 왜 씬에 만드나]
//   글자는 반드시 씬 오브젝트여야 한다 — 팀원이 씬을 훑어 번역 문구를 모으기 때문에,
//   실행 중에 만든 라벨은 그 수집에서 통째로 빠진다.
//   그래서 '글자 2개(제목·카운터)'만 씬에 굽고, 나머지 도형은 실행 중에 만든다
//   (TimelinePuzzleUI.Build 참고). 씬 오브젝트가 4개뿐이라 diff 도 거의 없다.
//
// [기존 캔버스에 얹는다]
//   캔버스를 새로 만들지 않는다. 하나 더 늘면 정렬 순서를 따로 관리해야 하고, 팀원이
//   계층에서 UI 를 찾을 때 엉뚱한 데를 뒤지게 된다. 씬의 화면 UI 캔버스 안에서
//   CoreUpgradePanel·ShipRepairPanel 과 같은 층(맨 뒤 형제 = 가장 위)에 둔다.
//
// 여러 번 돌려도 안전하다 — 같은 이름이 이미 있으면 그것을 재사용한다.
// =====================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TimelinePuzzleSceneBuilder
{
    const string PanelName = "TimelinePuzzlePanel";

    [MenuItem("Tools/TIMEKOV/기믹/시간선 퍼즐 1) 패널 생성·연결")]
    static void BuildPanel()
    {
        Undo.SetCurrentGroupName("시간선 퍼즐 패널");
        int group = Undo.GetCurrentGroup();

        var existing = Object.FindFirstObjectByType<TimelinePuzzleUI>(FindObjectsInactive.Include);
        var panel = existing != null ? existing.gameObject : null;

        if (panel == null)
        {
            var host = FindHostCanvas();
            if (host == null)
            {
                EditorUtility.DisplayDialog("시간선 퍼즐",
                    "화면 UI 캔버스(ScreenSpaceOverlay)를 찾지 못했습니다.\n" +
                    "넣고 싶은 캔버스를 선택한 뒤 다시 실행해 주세요.", "확인");
                return;
            }

            panel = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer),
                                   typeof(Image), typeof(CanvasGroup), typeof(TimelinePuzzleUI));
            Undo.RegisterCreatedObjectUndo(panel, PanelName);
            panel.transform.SetParent(host.transform, false);
            Stretch((RectTransform)panel.transform);

            // 형제 중 맨 뒤 = 가장 위에 그려진다. CoreUpgradePanel·ShipRepairPanel 처럼
            // 상호작용으로 열리는 패널들과 같은 층에 둔다(정렬 순서를 따로 둘 필요가 없다).
            panel.transform.SetAsLastSibling();
        }

        var ui = panel.GetComponent<TimelinePuzzleUI>();
        var rt = (RectTransform)panel.transform;
        var font = FindFont();

        // 글자 2개 — 이 둘만 씬에 있다.
        var title = MakeText(rt, "Title", font, "시간선 복원", 21f,
                             new Color(0.941f, 0.965f, 1f), localize: true);
        // 카운터는 코드가 매번 숫자를 써넣는다 → LocalizedLabel 을 붙이면 서로 덮어쓴다.
        var counter = MakeText(rt, "Counter", font, "1 / 25", 20f,
                               new Color(0.435f, 0.659f, 0.878f), localize: false);

        var so = new SerializedObject(ui);
        Set(so, "titleLabel", title);
        Set(so, "counterLabel", counter);
        so.ApplyModifiedProperties();

        panel.SetActive(false);   // 상호작용할 때만 열린다

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeObject = panel;
        Debug.Log("[시간선 퍼즐] 패널을 만들고 연결했습니다. 씬을 저장하세요 (Ctrl+S).", panel);
    }

    [MenuItem("Tools/TIMEKOV/기믹/시간선 퍼즐 2) 선택 오브젝트를 퍼즐 장치로")]
    static void MakeConsole()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("시간선 퍼즐", "장치로 만들 오브젝트를 먼저 선택하세요.", "확인");
            return;
        }

        var console = go.GetComponent<TimelinePuzzleConsole>();
        if (console == null) console = Undo.AddComponent<TimelinePuzzleConsole>(go);

        var panel = Object.FindFirstObjectByType<TimelinePuzzleUI>(FindObjectsInactive.Include);
        if (panel != null)
        {
            var so = new SerializedObject(console);
            Set(so, "panel", panel);
            so.ApplyModifiedProperties();
        }

        // 외곽선 모드 — 다른 오브젝트/지형에 가려진 부분은 그리지 않는다(OutlineVisible).
        //   이게 없으면 InteractOutline 이 기본값 OutlineAll 로 그려서, 땅에 박힌 부분과
        //   앞을 가리는 물체 위로까지 선이 다 비친다.
        if (go.GetComponent<InteractOutlineStyle>() == null)
        {
            var style = Undo.AddComponent<InteractOutlineStyle>(go);
            style.mode = Outline.Mode.OutlineVisible;
            style.showOutline = true;
        }

        CheckInteractable(go);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeObject = go;
        Debug.Log($"[시간선 퍼즐] {go.name} 을(를) 퍼즐 장치로 만들었습니다. " +
                  "인스펙터에서 시작·도착 칸과 열릴 대상(targets)을 지정하세요.", go);
    }

    /// <summary>상호작용이 실제로 감지될 조건을 확인하고, 레이어는 자동으로 맞춘다.
    ///
    /// PlayerInteractComponent 는 이렇게 찾는다:
    ///   Physics.OverlapSphere(플레이어, InteractRadius, InteractLayer, QueryTriggerInteraction.Collide)
    ///   → hit.TryGetComponent&lt;IInteractable&gt;()
    /// 여기서 조용히 실패하는 함정이 둘이다.
    ///   ① 콜라이더가 '같은 GameObject' 에 없으면 안 잡힌다(자식에 두면 감지 실패)
    ///   ② 레이어가 InteractLayer 마스크에 없으면 아예 후보에 안 들어온다
    /// 트리거 여부는 상관없다 — QueryTriggerInteraction.Collide 라 둘 다 잡힌다.</summary>
    static void CheckInteractable(GameObject go)
    {
        if (go.GetComponent<Collider>() == null)
            Debug.LogWarning($"[{go.name}] 이 오브젝트 '자신'에 Collider 가 없습니다. " +
                             "자식에 있는 콜라이더로는 감지되지 않으니 같은 오브젝트에 붙여 주세요. " +
                             "(트리거든 아니든 무관)", go);

        var pic = Object.FindFirstObjectByType<PlayerInteractComponent>(FindObjectsInactive.Include);
        if (pic == null) return;   // 플레이어가 없는 씬 — 확인할 수 없다

        int mask = pic.InteractLayer.value;
        if ((mask & (1 << go.layer)) != 0) return;   // 이미 잡히는 레이어다

        // 마스크에 들어 있는 레이어 중 첫 번째로 옮겨 준다(보통 Interactable).
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName)) continue;

            Undo.RecordObject(go, "레이어 변경");
            go.layer = i;
            Debug.Log($"[{go.name}] 레이어를 '{layerName}' 로 바꿨습니다. " +
                      $"플레이어의 InteractLayer 가 그 레이어만 검사하기 때문입니다.", go);
            return;
        }
        Debug.LogWarning($"[{go.name}] 플레이어의 InteractLayer 에 쓸 수 있는 레이어가 없습니다.", go);
    }

    // ==================================================================
    /// <summary>이미 씬에 있는 화면 UI 캔버스를 찾는다. ★새로 만들지 않는다 —
    /// 캔버스가 하나 더 늘면 정렬 순서를 따로 관리해야 하고, 팀원이 계층에서 UI 를 찾을 때도
    /// 엉뚱한 데를 뒤지게 된다. 기존 캔버스 안에 다른 패널들과 같은 층으로 둔다.
    ///
    /// 고르는 순서:
    ///   1) 선택한 오브젝트가 캔버스 안에 있으면 그 캔버스 (배치를 직접 지정하고 싶을 때)
    ///   2) 다른 패널(CoreUpgradePanel 등)을 이미 품고 있는 캔버스
    ///   3) 그래도 없으면 아무 ScreenSpaceOverlay 캔버스</summary>
    static Canvas FindHostCanvas()
    {
        if (Selection.activeGameObject != null)
        {
            var picked = Selection.activeGameObject.GetComponentInParent<Canvas>(true);
            if (picked != null && picked.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return picked.rootCanvas;
        }

        var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // 기존 패널이 얹혀 있는 캔버스를 우선한다 — 거기가 이 게임의 'UI 사는 곳'이다.
        foreach (var name in new[] { "CoreUpgradePanel", "ShipRepairPanel", "Panels", "HUD" })
            foreach (var c in all)
            {
                if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.transform.Find(name) != null) return c.rootCanvas;
            }

        foreach (var c in all)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c.rootCanvas;

        return null;
    }

    static TMP_FontAsset FindFont()
    {
        // 주변 UI 와 같은 폰트를 쓴다. 따로 지정하게 만들면 빼먹었을 때 이 패널만 폰트가 튄다.
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.font != null) return t.font;
        return null;
    }

    /// <param name="localize">코드가 .text 를 덮어쓰는 라벨에는 붙이면 안 된다 — 서로 덮어써서 둘 다 깨진다.</param>
    static TMP_Text MakeText(RectTransform parent, string name, TMP_FontAsset font, string content,
                             float size, Color color, bool localize)
    {
        var found = parent.Find(name);
        GameObject go;
        if (found != null) go = found.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
        }

        var tmp = go.GetComponent<TMP_Text>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        var loc = go.GetComponent<LocalizedLabel>();
        if (localize) LocalizedLabelEditorUtil.Attach(go, content);
        else if (loc != null) Undo.DestroyObjectImmediate(loc);

        return tmp;
    }

    static void Set(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[시간선 퍼즐] 필드를 못 찾음: {field}");
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(.5f, .5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
#endif
