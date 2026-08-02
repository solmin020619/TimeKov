using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [08-02] 튜토리얼 코치마크 오버레이(TutorialOverlay) 실물 생성기.
//   예전엔 런타임이 계층을 통째로 만들고 프레임 스프라이트도 Resources.Load 로 집었다.
//   지금은 여기서 씬에 만들고 스프라이트도 직접 참조로 물린다(Resources 의존 제거).
//   스포트라이트 '위치'는 타깃을 따라가야 하므로 런타임이 매 프레임 계산한다 - 여기선 구성만 만든다.
public static class TutorialOverlayBuilder
{
    private const string RootName = "TutorialOverlay";
    private const string FrameSpritePath = "Assets/Resources/tutorial/1.png";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.88f);
    private static readonly Color BorderColor = new Color(1f, 0.85f, 0.2f, 1f);

    [MenuItem("Tools/TIMEKOV/튜토리얼 오버레이 생성")]
    public static void Build()
    {
        var canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 루트 Canvas 가 없습니다.", "확인");
            return;
        }

        if (Object.FindAnyObjectByType<TutorialOverlay>(FindObjectsInactive.Include) != null)
        {
            if (!EditorUtility.DisplayDialog("확인", "튜토리얼 오버레이가 이미 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소")) return;
            UIBuilderUtil.RemoveExisting<TutorialOverlay>();
        }

        Transform parent = UIBuilderUtil.EnsureGroup(canvas, "Overlays");

        // 루트: 자체 Canvas 로 최상단 정렬(예전 런타임 코드와 동일한 sortingOrder 5000)
        var rootGo = new GameObject(RootName, typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(rootGo, "Build TutorialOverlay");
        Stretch((RectTransform)rootGo.transform, 0f, 0f, 1f, 1f);

        var cv = rootGo.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 5000;   // 거의 최상단(다른 UI 위)
        var raycaster = rootGo.AddComponent<GraphicRaycaster>();

        var root = rootGo.transform;

        // 딤: 타깃 없을 때 전체 / 있을 때 4스트립으로 구멍
        var fullDim = NewImage("FullDim", root, DimColor);
        Stretch(fullDim.rectTransform, 0f, 0f, 1f, 1f);

        var dimTop = NewImage("DimTop", root, DimColor);
        var dimBottom = NewImage("DimBottom", root, DimColor);
        var dimLeft = NewImage("DimLeft", root, DimColor);
        var dimRight = NewImage("DimRight", root, DimColor);

        // 구멍 테두리(프레임 스프라이트 없을 때의 폴백). 딤 위에 그려지도록 이후 생성.
        var borderTop = NewImage("BorderTop", root, BorderColor);
        var borderBottom = NewImage("BorderBottom", root, BorderColor);
        var borderLeft = NewImage("BorderLeft", root, BorderColor);
        var borderRight = NewImage("BorderRight", root, BorderColor);

        // sci-fi 프레임(9-slice). 스프라이트가 붙어 있으면 런타임이 이걸 우선 사용한다.
        var frameImage = NewImage("FocusFrame", root, Color.white);
        var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
        if (frameSprite != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.pixelsPerUnitMultiplier = 3f;   // 9-slice 코너 렌더 크기. 크면 코너가 작아진다.
        }
        else
        {
            Debug.LogWarning("[TutorialOverlayBuilder] 프레임 스프라이트를 못 찾음: " + FrameSpritePath + " -> 4변 단색 테두리로 폴백한다(인스펙터에서 직접 물려도 됨).");
        }
        frameImage.enabled = false;

        // 클릭 캐처(투명, 전체화면 - 뒤 UI 클릭 차단. 진행 판정은 런타임 입력 폴링이 담당)
        var clickCatcher = NewButton("ClickCatcher", root);
        Stretch((RectTransform)clickCatcher.transform, 0f, 0f, 1f, 1f);

        // 배너(배경 박스 + 텍스트)
        var bannerBg = NewImage("BannerBg", root, new Color(0f, 0f, 0f, 0.85f));
        SetAnchors(bannerBg.rectTransform, 0.12f, 0.84f, 0.88f, 0.93f);
        var banner = NewText("BannerText", bannerBg.transform);
        Stretch(banner.rectTransform, 0f, 0f, 1f, 1f);
        banner.alignment = TextAlignmentOptions.Center;
        banner.fontSize = 30f;
        banner.enableAutoSizing = true; banner.fontSizeMin = 16f; banner.fontSizeMax = 32f;
        banner.textWrappingMode = TextWrappingModes.Normal;
        banner.color = Color.white;
        banner.margin = new Vector4(24f, 8f, 24f, 8f);

        // 하단 "아무 곳이나 클릭하여 계속"
        var continueLabel = NewText("ContinueLabel", root);
        SetAnchors(continueLabel.rectTransform, 0.25f, 0.06f, 0.75f, 0.12f);
        continueLabel.alignment = TextAlignmentOptions.Center;
        continueLabel.fontSize = 24f;
        continueLabel.color = BorderColor;
        continueLabel.text = "아무 곳이나 클릭하여 계속";

        // 컴포넌트 + 참조 연결
        var ui = rootGo.AddComponent<TutorialOverlay>();
        var so = new SerializedObject(ui);
        SetRef(so, "canvasComp", cv);
        SetRef(so, "raycaster", raycaster);
        SetRef(so, "fullDim", fullDim);
        SetRef(so, "dimTop", dimTop);
        SetRef(so, "dimBottom", dimBottom);
        SetRef(so, "dimLeft", dimLeft);
        SetRef(so, "dimRight", dimRight);
        SetRef(so, "borderTop", borderTop);
        SetRef(so, "borderBottom", borderBottom);
        SetRef(so, "borderLeft", borderLeft);
        SetRef(so, "borderRight", borderRight);
        SetRef(so, "frameImage", frameImage);
        SetRef(so, "clickCatcher", clickCatcher);
        SetRef(so, "bannerBg", bannerBg.rectTransform);
        SetRef(so, "banner", banner);
        SetRef(so, "continueLabel", continueLabel);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 오브젝트는 항상 활성(런타임 LateUpdate 가 계속 돌아야 설정창 복귀를 감지한다).
        // 평소 안 보이게 하는 건 Canvas/Raycaster 를 끄는 것으로 처리 - 런타임 Awake 가 바로 끈다.
        Selection.activeGameObject = rootGo;
        EditorUtility.DisplayDialog("완료",
            "튜토리얼 오버레이를 만들었습니다.\n\n" +
            "위치: Canvas/Overlays/" + RootName + "\n" +
            "에디터에선 화면이 까맣게 덮여 보입니다(실행하면 코치마크가 뜰 때만 켜짐).\n\n" +
            "Ctrl+S 로 씬을 저장하세요.", "확인");
    }

    // 헬퍼

    private static Image NewImage(string name, Transform parent, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    private static Button NewButton(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);   // 투명하지만 raycast 는 받음
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        return btn;
    }

    private static TMP_Text NewText(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        var any = Object.FindFirstObjectByType<TMP_Text>();
        if (any != null && any != t && any.font != null) t.font = any.font;
        return t;
    }

    private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        => SetAnchors(rt, xMin, yMin, xMax, yMax);

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[TutorialOverlayBuilder] 필드를 못 찾음: " + field);
    }
}
