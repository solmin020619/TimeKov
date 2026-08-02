using JeffGrawAssets.FlexibleUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [08-02] 상호작용 프롬프트 패널 생성기(PromptPanelUI 파생 공용).
//   예전엔 런타임 스크립트가 250줄짜리 레이아웃을 실행 중에 만들어서 에디터엔 아무것도 없었다.
//   지금은 여기서 씬에 실물로 만들고, 런타임 스크립트는 참조만 쓴다.
//
//   스프라이트는 RuntimeRoundedSprite 가 실행 시 채운다(UISpriteFactory 생성물은 저장이 안 되므로).
public static class PromptPanelBuilder
{
    // 팔레트(런타임 스크립트에서 옮겨옴. 색은 직렬화되므로 여기서 넣고 끝)
    private static readonly Color BgTint    = new Color(0.08f, 0.08f, 0.08f, 0.88f);
    private static readonly Color Border    = new Color(1.00f, 1.00f, 1.00f, 0.13f);
    private static readonly Color HdrLine   = new Color(1.00f, 1.00f, 1.00f, 0.11f);
    private static readonly Color Divider   = new Color(1.00f, 1.00f, 1.00f, 0.07f);
    private static readonly Color HdrBg     = new Color(0.00f, 0.00f, 0.00f, 0.22f);
    private static readonly Color TextCol   = new Color(0.88f, 0.90f, 0.90f, 1.00f);
    private static readonly Color SubText   = new Color(0.54f, 0.55f, 0.55f, 1.00f);
    private static readonly Color BtnHov    = new Color(1.00f, 1.00f, 1.00f, 0.07f);
    private static readonly Color BtnPrs    = new Color(0.00f, 0.00f, 0.00f, 0.15f);
    private static readonly Color BarBg     = new Color(0.05f, 0.05f, 0.05f, 1.00f);
    private static readonly Color BarFill   = new Color(0.78f, 0.80f, 0.80f, 1.00f);
    private static readonly Color TitleCol  = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    private static readonly Color KeyBorder = new Color(0.62f, 0.62f, 0.62f, 0.70f);
    private static readonly Color KeyBg     = new Color(0.24f, 0.24f, 0.24f, 1.00f);
    private static readonly Color KeyText   = new Color(0.96f, 0.96f, 0.96f, 1.00f);

    /// <summary>프롬프트 패널을 Canvas/Overlays 아래에 만들고 T 컴포넌트에 참조를 연결한다.</summary>
    /// <param name="rootName">하이어라키에 표시될 오브젝트 이름</param>
    /// <param name="progressLabel">진행 섹션 좌측 문구("여는 중"/"해금 중")</param>
    public static void BuildPrompt<T>(string rootName, string progressLabel) where T : PromptPanelUI
    {
        // 루트 캔버스로만 찾는다(중첩 Canvas 를 잡으면 UI 안에 UI 가 파묻힌다 - 실제로 겪은 사고).
        var canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 루트 Canvas 가 없습니다.", "확인");
            return;
        }

        // 이전 결과물이 엉뚱한 부모 아래 있어도 확실히 정리되도록 씬 전체에서 제거한다.
        if (Object.FindAnyObjectByType<T>(FindObjectsInactive.Include) != null)
        {
            if (!EditorUtility.DisplayDialog("확인", rootName + " 가 이미 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소")) return;
            UIBuilderUtil.RemoveExisting<T>();
        }

        Transform parent = UIBuilderUtil.EnsureGroup(canvas, "Overlays");

        // 루트: 자체 Canvas 로 정렬 격리(예전 런타임 코드와 동일한 sortingOrder 50)
        var rootGo = new GameObject(rootName, typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(rootGo, "Build " + rootName);
        Stretch((RectTransform)rootGo.transform);

        var cv = rootGo.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 50;
        rootGo.AddComponent<GraphicRaycaster>();

        // 패널(표시/숨김 대상)
        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(rootGo.transform, false);
        var panel = (RectTransform)panelGo.transform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 130f);
        panel.sizeDelta = new Vector2(300f, 0f);

        var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 8);
        vlg.spacing = 0f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        panelGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 절대 레이어(테두리 / 블러 / 배경틴트)
        AbsImg(panelGo, Border, 64, 4, 0, 0, 0, 0);
        var blur = AbsBlur(panelGo);
        AbsImg(panelGo, BgTint, 64, 3, 1, 1, 1, 1);

        var title = BuildHeader(panelGo);
        HLine(panelGo, HdrLine, 1f);

        var (progressSection, progressFill, timerText) = BuildProgressGroup(panelGo, progressLabel);
        progressSection.SetActive(false);

        var (btn1, key1, lbl1) = MakeButton(panelGo, "PrimaryBtn");
        var (btn2, key2, lbl2) = MakeButton(panelGo, "SecondaryBtn");

        // 컴포넌트 + 참조 연결(필드는 PromptPanelUI 베이스에 있다)
        var ui = rootGo.AddComponent<T>();
        var so = new SerializedObject(ui);
        SetRef(so, "panel", panelGo);
        SetRef(so, "titleText", title);
        SetRef(so, "progressSection", progressSection);
        SetRef(so, "progressFill", progressFill);
        SetRef(so, "timerText", timerText);
        SetRef(so, "primaryBtn", btn1);
        SetRef(so, "primaryKey", key1);
        SetRef(so, "primaryLabel", lbl1);
        SetRef(so, "secondaryBtn", btn2);
        SetRef(so, "secondaryKey", key2);
        SetRef(so, "secondaryLabel", lbl2);
        SetRef(so, "blur", blur);
        so.ApplyModifiedPropertiesWithoutUndo();

        panelGo.SetActive(false);   // 평소 숨김(루트는 켜둔 채 Panel 만 토글)

        Selection.activeGameObject = rootGo;
        EditorUtility.DisplayDialog("완료",
            rootName + " 를 만들었습니다.\n\n" +
            "위치: Canvas/Overlays/" + rootName + "\n" +
            "스프라이트는 실행 시 코드가 넣습니다(에디터에선 각져 보이는 게 정상).\n\n" +
            "Ctrl+S 로 씬을 저장하세요.", "확인");
    }

    // 구성 요소

    private static TMP_Text BuildHeader(GameObject parent)
    {
        var go = new GameObject("Header", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<LayoutElement>().minHeight = 36f;
        var bg = go.AddComponent<Image>();
        bg.color = HdrBg;
        bg.raycastTarget = false;

        var txtGo = new GameObject("Title", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var t = txtGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(t);
        t.fontSize = 14f;
        t.fontStyle = FontStyles.Bold;
        t.color = TitleCol;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false;
        var rt = (RectTransform)txtGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(14f, 0f); rt.offsetMax = new Vector2(-14f, 0f);
        return t;
    }

    private static (GameObject section, Image fill, TMP_Text timer) BuildProgressGroup(GameObject parent, string label)
    {
        var wrapper = new GameObject("ProgressGroup", typeof(RectTransform));
        wrapper.transform.SetParent(parent.transform, false);
        var wvlg = wrapper.AddComponent<VerticalLayoutGroup>();
        wvlg.spacing = 0f;
        wvlg.childControlWidth = true; wvlg.childControlHeight = true;
        wvlg.childForceExpandWidth = true; wvlg.childForceExpandHeight = false;

        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(wrapper.transform, false);
        var ivlg = inner.AddComponent<VerticalLayoutGroup>();
        ivlg.padding = new RectOffset(12, 12, 7, 7);
        ivlg.spacing = 5f;
        ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false;
        inner.AddComponent<LayoutElement>();

        var row = new GameObject("TimerRow", typeof(RectTransform));
        row.transform.SetParent(inner.transform, false);
        var rhlg = row.AddComponent<HorizontalLayoutGroup>();
        rhlg.childControlWidth = true; rhlg.childControlHeight = true;
        rhlg.childForceExpandWidth = true; rhlg.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().minHeight = 20f;

        AddTMP(row, "Lbl", label, 12f, SubText, TextAlignmentOptions.MidlineLeft);
        var timer = AddTMP(row, "Timer", "", 12f, TitleCol, TextAlignmentOptions.MidlineRight);
        timer.fontStyle = FontStyles.Bold;

        var barGo = new GameObject("BarBG", typeof(RectTransform));
        barGo.transform.SetParent(inner.transform, false);
        barGo.AddComponent<LayoutElement>().minHeight = 4f;
        var barImg = barGo.AddComponent<Image>();
        barImg.color = BarBg;
        barImg.type = Image.Type.Sliced;
        barImg.raycastTarget = false;
        Rounded(barGo, 16, 2);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(barGo.transform, false);
        var fill = fillGo.AddComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.color = BarFill;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        Rounded(fillGo, 16, 2);
        Stretch((RectTransform)fillGo.transform);

        HLine(wrapper, Divider, 1f);
        return (wrapper, fill, timer);
    }

    private static (Button btn, TMP_Text keyTmp, TMP_Text labelTmp) MakeButton(GameObject parent, string name)
    {
        const float BadgeSize = 24f, BadgeLeft = 12f, BadgeGap = 9f, LabelRight = 12f;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f; le.preferredHeight = 40f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.type = Image.Type.Sliced;
        Rounded(go, 16, 1);

        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = new Color(0f, 0f, 0f, 0f);
        cb.highlightedColor = BtnHov;
        cb.pressedColor = BtnPrs;
        cb.selectedColor = new Color(0f, 0f, 0f, 0f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.targetGraphic = img;

        var sepGo = new GameObject("Sep", typeof(RectTransform));
        sepGo.transform.SetParent(go.transform, false);
        sepGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var sepImg = sepGo.AddComponent<Image>();
        sepImg.color = Divider; sepImg.raycastTarget = false;
        var sepRT = (RectTransform)sepGo.transform;
        sepRT.anchorMin = new Vector2(0f, 1f); sepRT.anchorMax = new Vector2(1f, 1f);
        sepRT.pivot = new Vector2(0.5f, 1f);
        sepRT.anchoredPosition = Vector2.zero; sepRT.sizeDelta = new Vector2(0f, 1f);

        var badgeGo = new GameObject("KeyBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(go.transform, false);
        badgeGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var badgeBase = badgeGo.AddComponent<Image>();
        badgeBase.color = Color.clear; badgeBase.raycastTarget = false;
        var badgeRT = (RectTransform)badgeGo.transform;
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0f, 0.5f);
        badgeRT.pivot = new Vector2(0f, 0.5f);
        badgeRT.anchoredPosition = new Vector2(BadgeLeft, 0f);
        badgeRT.sizeDelta = new Vector2(BadgeSize, BadgeSize);

        AbsImg(badgeGo, KeyBorder, 32, 5, 0, 0, 0, 0);
        AbsImg(badgeGo, KeyBg, 32, 4, 1, 1, 1, 1);

        var keyTxtGo = new GameObject("Key", typeof(RectTransform));
        keyTxtGo.transform.SetParent(badgeGo.transform, false);
        var keyTmp = keyTxtGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(keyTmp);
        keyTmp.fontSize = 13f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.color = KeyText;
        keyTmp.alignment = TextAlignmentOptions.Center;
        keyTmp.raycastTarget = false;
        Stretch((RectTransform)keyTxtGo.transform);

        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(go.transform, false);
        lblGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(lbl);
        lbl.fontSize = 13f;
        lbl.color = TextCol;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.richText = true;
        lbl.raycastTarget = false;
        var lblRT = (RectTransform)lblGo.transform;
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(BadgeLeft + BadgeSize + BadgeGap, 0f);
        lblRT.offsetMax = new Vector2(-LabelRight, 0f);

        return (btn, keyTmp, lbl);
    }

    // 헬퍼

    private static BlurredImage AbsBlur(GameObject parent)
    {
        var go = new GameObject("_Blur", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var blur = go.AddComponent<BlurredImage>();
        blur.type = Image.Type.Sliced;
        blur.color = Color.white;
        blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        Rounded(go, 64, 4);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(1f, 1f); rt.offsetMax = new Vector2(-1f, -1f);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
        return blur;
    }

    private static void AbsImg(GameObject parent, Color color, int texSize, int radius,
                               float l, float b, float r, float t)
    {
        var go = new GameObject("_Abs", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        Rounded(go, texSize, radius);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    private static void HLine(GameObject parent, Color color, float h)
    {
        var go = new GameObject("HLine", typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<LayoutElement>().minHeight = h;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
    }

    private static TMP_Text AddTMP(GameObject parent, string name, string text,
                                   float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(t);
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = align; t.raycastTarget = false;
        return t;
    }

    // UISpriteFactory 스프라이트는 저장이 안 되므로 실행 시 채우도록 표시만 해둔다.
    private static void Rounded(GameObject go, int texSize, int radius)
    {
        var rs = go.GetComponent<RuntimeRoundedSprite>();
        if (rs == null) rs = go.AddComponent<RuntimeRoundedSprite>();
        rs.shape = RuntimeRoundedSprite.Shape.RoundedRect;
        rs.texSize = texSize;
        rs.radius = radius;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void ApplyFont(TMP_Text t)
    {
        var any = Object.FindFirstObjectByType<TMP_Text>();
        if (any != null && any != t && any.font != null) t.font = any.font;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[PromptPanelBuilder] 필드를 못 찾음: " + field);
    }
}
