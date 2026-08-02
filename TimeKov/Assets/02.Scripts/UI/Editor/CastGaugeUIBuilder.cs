using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [08-02] 시전 게이지(CastGaugeUI) 실물 생성기.
//   예전엔 CastGaugeUI 가 실행 중에 자기 계층을 통째로 만들었다(에디터에 아무것도 없음).
//   이제 이 빌더가 Canvas 아래에 실물로 만들고, 런타임 스크립트는 참조만 쓴다.
//
//   스프라이트는 일부러 안 넣는다: UISpriteFactory 생성물은 에셋이 아니라 프리팹/씬에 구우면
//   유니티 재시작 때 사라진다. 그래서 CastGaugeUI.Awake 가 매 실행 다시 넣는다.
public static class CastGaugeUIBuilder
{
    private const string RootName = "CastGauge";
    private const float W = 340f, PillH = 42f, BarH = 8f, Gap = 6f;

    [MenuItem("Tools/TIMEKOV/시전 게이지 UI 생성")]
    public static void Build()
    {
        // 루트 캔버스로만 찾는다(중첩 Canvas 를 잡으면 UI 안에 UI 가 파묻힌다 - 실제로 겪은 사고).
        var canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 루트 Canvas 가 없습니다.", "확인");
            return;
        }

        // 이전 결과물이 엉뚱한 부모 아래 있어도 확실히 정리되도록 씬 전체에서 제거한다.
        if (Object.FindAnyObjectByType<CastGaugeUI>(FindObjectsInactive.Include) != null)
        {
            if (!EditorUtility.DisplayDialog("확인", "CastGauge 가 이미 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소")) return;
            UIBuilderUtil.RemoveExisting<CastGaugeUI>();
        }

        Transform parent = UIBuilderUtil.EnsureGroup(canvas, "Overlays");

        // 루트: 자체 Canvas 로 정렬 격리(HUD 위, 워프 검은 페이드 아래). 예전 런타임 코드와 동일한 sortingOrder.
        var root = NewChild(RootName, parent, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(W, PillH + Gap + BarH));
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build CastGauge");

        var subCanvas = root.gameObject.AddComponent<Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = 8000;
        root.gameObject.AddComponent<GraphicRaycaster>();

        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;   // 평소 숨김(오브젝트는 켜둔 채 알파로만 숨긴다)

        // 알약 배경
        var pill = NewImage("Pill", root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(W, PillH));

        // 아이콘(좌) / 라벨 / 남은시간(우)
        var icon = NewImage("Icon", pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(26f, 26f));
        icon.preserveAspect = true;

        var label = NewText("Label", pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(W - 120f, PillH), 20f);
        label.alignment = TextAlignmentOptions.Left;
        label.fontStyle = FontStyles.Bold;

        var count = NewText("Count", pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(70f, PillH), 20f);
        count.alignment = TextAlignmentOptions.Right;
        count.fontStyle = FontStyles.Bold;

        // 진행 바(알약 아래) + 채워지는 게이지
        var track = NewImage("Track", root, new Vector2(0.5f, 1f), new Vector2(0f, -(PillH + Gap)), new Vector2(W, BarH));
        var fill = NewImage("Fill", track.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, BarH));

        // 컴포넌트 + 참조 연결
        var ui = root.gameObject.AddComponent<CastGaugeUI>();
        var so = new SerializedObject(ui);
        SetRef(so, "group", group);
        SetRef(so, "pill", pill);
        SetRef(so, "icon", icon);
        SetRef(so, "label", label);
        SetRef(so, "count", count);
        SetRef(so, "track", track);
        SetRef(so, "fill", fill);
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root.gameObject;
        EditorUtility.DisplayDialog("완료",
            "시전 게이지(CastGauge)를 만들었습니다.\n\n" +
            "위치: Canvas/Overlays/CastGauge\n" +
            "스프라이트/색은 실행 시 코드가 넣습니다(빈 사각형으로 보이는 게 정상).\n\n" +
            "Ctrl+S 로 씬을 저장하세요.", "확인");
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[CastGaugeUIBuilder] 필드를 못 찾음: {field}");
    }

    private static RectTransform NewChild(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = ap;
        rt.pivot = ap;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size)
    {
        var rt = NewChild(name, parent, ap, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewChild(name, parent, ap, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        var font = HudFont();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // 씬에서 이미 쓰는 폰트를 그대로 따라간다(HUD 폰트 통일).
    private static TMP_FontAsset HudFont()
    {
        var any = Object.FindFirstObjectByType<TMP_Text>();
        return any != null ? any.font : null;
    }
}
