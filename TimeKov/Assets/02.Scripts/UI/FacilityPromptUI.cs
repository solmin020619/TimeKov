using JeffGrawAssets.FlexibleUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FacilityPromptUI : MonoBehaviour
{
    // ── 팔레트 ────────────────────────────────────────────────────────
    static readonly Color kBgTint    = new Color(0.08f, 0.08f, 0.08f, 0.88f);
    static readonly Color kBorder    = new Color(1.00f, 1.00f, 1.00f, 0.13f);
    static readonly Color kHdrLine   = new Color(1.00f, 1.00f, 1.00f, 0.11f);
    static readonly Color kDivider   = new Color(1.00f, 1.00f, 1.00f, 0.07f);
    static readonly Color kHdrBg     = new Color(0.00f, 0.00f, 0.00f, 0.22f);
    static readonly Color kText      = new Color(0.88f, 0.90f, 0.90f, 1.00f);
    static readonly Color kSubText   = new Color(0.54f, 0.55f, 0.55f, 1.00f);
    static readonly Color kBtnHov    = new Color(1.00f, 1.00f, 1.00f, 0.07f);
    static readonly Color kBtnPrs    = new Color(0.00f, 0.00f, 0.00f, 0.15f);
    static readonly Color kBarBg     = new Color(0.05f, 0.05f, 0.05f, 1.00f);
    static readonly Color kBarFill   = new Color(0.78f, 0.80f, 0.80f, 1.00f);
    static readonly Color kTitle     = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    static readonly Color kKeyBorder = new Color(0.62f, 0.62f, 0.62f, 0.70f);
    static readonly Color kKeyBg     = new Color(0.24f, 0.24f, 0.24f, 1.00f);
    static readonly Color kKeyText   = new Color(0.96f, 0.96f, 0.96f, 1.00f);

    // ── 필드 ──────────────────────────────────────────────────────────
    public static FacilityPromptUI Instance { get; private set; }

    private FacilityUnlockPickup _owner;
    private RectTransform        _panel;
    private TMP_Text             _titleText;
    private GameObject           _progressSection;
    private Image                _progressFill;
    private TMP_Text             _timerText;
    private Button               _primaryBtn;
    private TMP_Text             _primaryKey;
    private TMP_Text             _primaryLabel;
    private Button               _secondaryBtn;
    private TMP_Text             _secondaryKey;
    private TMP_Text             _secondaryLabel;
    private BlurredImage         _blur;

    private System.Action _onPrimary;
    private System.Action _onSecondary;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        _progressSection?.SetActive(false);
        _panel?.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (_blur != null && _blur.Common.cameraReference == null)
            _blur.Common.cameraReference = Camera.main;
    }

    public static FacilityPromptUI GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("FacilityPromptUI").AddComponent<FacilityPromptUI>();
    }

    // ── 공개 API ──────────────────────────────────────────────────────
    public void ShowIdle(FacilityUnlockPickup owner, float instantCost,
                         System.Action onF, System.Action onG)
    {
        _owner = owner;
        EnsureBlurCamera();
        _titleText.text = "잠긴  설비";
        _progressSection.SetActive(false);
        SetPrimary("F", "해금 시작", onF);
        SetSecondary("G", instantCost > 0
            ? $"즉시 해금   <color=#E05050>HP –{Mathf.CeilToInt(instantCost)}</color>"
            : null, onG);
        _panel.gameObject.SetActive(true);
    }

    public void ShowOpening(FacilityUnlockPickup owner, float progress, float secsLeft,
                            float instantCost, System.Action onG)
    {
        _owner = owner;
        EnsureBlurCamera();
        _titleText.text = "해금  중";
        _progressSection.SetActive(true);
        _progressFill.fillAmount = progress;
        _timerText.text = $"{Mathf.CeilToInt(secsLeft)}초";
        SetPrimary(null, null, null);
        SetSecondary("G", $"즉시 해금   <color=#E05050>HP –{Mathf.CeilToInt(instantCost)}</color>", onG);
        _panel.gameObject.SetActive(true);
    }

    public void ShowReady(FacilityUnlockPickup owner, System.Action onF)
    {
        _owner = owner;
        EnsureBlurCamera();
        _titleText.text = "해금  완료";
        _progressSection.SetActive(false);
        SetPrimary("F", "설비 해금", onF);
        SetSecondary(null, null, null);
        _panel.gameObject.SetActive(true);
    }

    public void HideIfOwner(FacilityUnlockPickup pickup)
    {
        if (_owner == pickup) Hide();
    }

    public void Hide()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
        _owner = null;
    }

    // ── 내부 ──────────────────────────────────────────────────────────
    private void SetPrimary(string keyChar, string actionText, System.Action action)
    {
        _onPrimary = action;
        bool show = !string.IsNullOrEmpty(actionText);
        if (_primaryBtn != null) _primaryBtn.gameObject.SetActive(show);
        if (show)
        {
            if (_primaryKey != null)   _primaryKey.text   = keyChar ?? "";
            if (_primaryLabel != null) _primaryLabel.text = actionText;
        }
    }

    private void SetSecondary(string keyChar, string actionText, System.Action action)
    {
        _onSecondary = action;
        bool show = !string.IsNullOrEmpty(actionText);
        if (_secondaryBtn != null) _secondaryBtn.gameObject.SetActive(show);
        if (show)
        {
            if (_secondaryKey != null)   _secondaryKey.text   = keyChar ?? "";
            if (_secondaryLabel != null) _secondaryLabel.text = actionText;
        }
    }

    private void EnsureBlurCamera()
    {
        if (_blur != null && _blur.Common.cameraReference == null)
            _blur.Common.cameraReference = Camera.main;
    }

    // ── 빌드 ──────────────────────────────────────────────────────────
    private void Build()
    {
        // Canvas
        var cvGo = new GameObject("Canvas");
        cvGo.transform.SetParent(transform, false);
        var cv = cvGo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 50;
        var cs = cvGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cvGo.AddComponent<GraphicRaycaster>();

        // 패널 루트
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(cvGo.transform, false);
        _panel = panelGo.AddComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0f);
        _panel.anchorMax        = new Vector2(0.5f, 0f);
        _panel.pivot            = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = new Vector2(0f, 130f);
        _panel.sizeDelta        = new Vector2(300f, 0f);

        var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(0, 0, 0, 8);
        vlg.spacing               = 0f;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        panelGo.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        // Absolute 레이어
        AbsImg(panelGo, kBorder, UISpriteFactory.RoundedRect(64, 4), 0, 0, 0, 0);
        AbsBlur(panelGo);
        AbsImg(panelGo, kBgTint, UISpriteFactory.RoundedRect(64, 3), 1, 1, 1, 1);

        // 레이아웃 자식
        BuildHeader(panelGo);
        HLine(panelGo, kHdrLine, 1f);
        _progressSection = BuildProgressGroup(panelGo);
        _progressSection.SetActive(false);

        (_primaryBtn,   _primaryKey,   _primaryLabel)   = MakeButton(panelGo);
        _primaryBtn.onClick.AddListener(() => _onPrimary?.Invoke());
        (_secondaryBtn, _secondaryKey, _secondaryLabel) = MakeButton(panelGo);
        _secondaryBtn.onClick.AddListener(() => _onSecondary?.Invoke());
    }

    private void AbsBlur(GameObject parent)
    {
        var go = new GameObject("_Blur");
        go.transform.SetParent(parent.transform, false);
        _blur = go.AddComponent<BlurredImage>();
        _blur.sprite = UISpriteFactory.RoundedRect(64, 4);
        _blur.type   = Image.Type.Sliced;
        _blur.color  = Color.white;
        _blur.raycastTarget = false;
        _blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        _blur.Common.featureNumber      = 0;
        _blur.Common.unrankedLayer      = 1;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(1f, 1f); rt.offsetMax = new Vector2(-1f, -1f);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    private void AbsImg(GameObject parent, Color color, Sprite spr,
                        float l, float b, float r, float t)
    {
        var go = new GameObject("_Abs");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color; img.sprite = spr;
        img.type = Image.Type.Sliced; img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        go.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    private void BuildHeader(GameObject parent)
    {
        var go = new GameObject("Header");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<LayoutElement>().minHeight = 36f;
        var bg = go.AddComponent<Image>();
        bg.color = kHdrBg; bg.raycastTarget = false;

        var txtGo = new GameObject("Title");
        txtGo.transform.SetParent(go.transform, false);
        _titleText = txtGo.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize  = 14f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color     = kTitle;
        _titleText.alignment = TextAlignmentOptions.MidlineLeft;
        _titleText.raycastTarget = false;
        var rt = (RectTransform)txtGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(14f, 0f); rt.offsetMax = new Vector2(-14f, 0f);
    }

    private void HLine(GameObject parent, Color color, float h)
    {
        var go = new GameObject("HLine");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<LayoutElement>().minHeight = h;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
    }

    private GameObject BuildProgressGroup(GameObject parent)
    {
        var wrapper = new GameObject("ProgressGroup");
        wrapper.transform.SetParent(parent.transform, false);
        var wvlg = wrapper.AddComponent<VerticalLayoutGroup>();
        wvlg.spacing = 0f;
        wvlg.childControlWidth = true; wvlg.childControlHeight = true;
        wvlg.childForceExpandWidth = true; wvlg.childForceExpandHeight = false;

        var inner = new GameObject("Inner");
        inner.transform.SetParent(wrapper.transform, false);
        var ivlg = inner.AddComponent<VerticalLayoutGroup>();
        ivlg.padding = new RectOffset(12, 12, 7, 7);
        ivlg.spacing = 5f;
        ivlg.childControlWidth = true; ivlg.childControlHeight = true;
        ivlg.childForceExpandWidth = true; ivlg.childForceExpandHeight = false;
        inner.AddComponent<LayoutElement>();

        // 타이머 행
        var row = new GameObject("TimerRow");
        row.transform.SetParent(inner.transform, false);
        var rhlg = row.AddComponent<HorizontalLayoutGroup>();
        rhlg.childControlWidth = true; rhlg.childControlHeight = true;
        rhlg.childForceExpandWidth = true; rhlg.childForceExpandHeight = false;
        row.AddComponent<LayoutElement>().minHeight = 20f;

        AddTMP(row, "Lbl", "해금 중", 12f, kSubText, TextAlignmentOptions.MidlineLeft);
        _timerText = AddTMP(row, "Timer", "", 12f, kTitle, TextAlignmentOptions.MidlineRight);
        _timerText.fontStyle = FontStyles.Bold;

        // 진행 바
        var barGo = new GameObject("BarBG");
        barGo.transform.SetParent(inner.transform, false);
        barGo.AddComponent<LayoutElement>().minHeight = 4f;
        var barImg = barGo.AddComponent<Image>();
        barImg.color = kBarBg;
        barImg.sprite = UISpriteFactory.RoundedRect(16, 2);
        barImg.type = Image.Type.Sliced;
        barImg.raycastTarget = false;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(barGo.transform, false);
        _progressFill = fillGo.AddComponent<Image>();
        _progressFill.sprite     = UISpriteFactory.RoundedRect(16, 2);
        _progressFill.type       = Image.Type.Filled;
        _progressFill.fillMethod = Image.FillMethod.Horizontal;
        _progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _progressFill.color      = kBarFill;
        _progressFill.fillAmount = 0f;
        _progressFill.raycastTarget = false;
        var fillRT = (RectTransform)fillGo.transform;
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;

        HLine(wrapper, kDivider, 1f);
        return wrapper;
    }

    private (Button btn, TMP_Text keyTmp, TMP_Text labelTmp) MakeButton(GameObject parent)
    {
        const float kBadgeSize  = 24f;
        const float kBadgeLeft  = 12f;
        const float kBadgeGap   =  9f;
        const float kLabelRight = 12f;

        var go = new GameObject("Btn");
        go.transform.SetParent(parent.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f; le.preferredHeight = 40f;

        var img = go.AddComponent<Image>();
        img.color  = new Color(0f, 0f, 0f, 0f);
        img.sprite = UISpriteFactory.RoundedRect(16, 1);
        img.type   = Image.Type.Sliced;

        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = new Color(0f, 0f, 0f, 0f);
        cb.highlightedColor = kBtnHov;
        cb.pressedColor     = kBtnPrs;
        cb.selectedColor    = new Color(0f, 0f, 0f, 0f);
        cb.fadeDuration     = 0.08f;
        btn.colors = cb; btn.targetGraphic = img;

        var sepGo = new GameObject("Sep");
        sepGo.transform.SetParent(go.transform, false);
        sepGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var sepImg = sepGo.AddComponent<Image>();
        sepImg.color = kDivider; sepImg.raycastTarget = false;
        var sepRT = (RectTransform)sepGo.transform;
        sepRT.anchorMin = new Vector2(0f, 1f); sepRT.anchorMax = new Vector2(1f, 1f);
        sepRT.pivot = new Vector2(0.5f, 1f);
        sepRT.anchoredPosition = Vector2.zero; sepRT.sizeDelta = new Vector2(0f, 1f);

        var badgeGo = new GameObject("KeyBadge");
        badgeGo.transform.SetParent(go.transform, false);
        badgeGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var badgeBase = badgeGo.AddComponent<Image>();
        badgeBase.color = Color.clear; badgeBase.raycastTarget = false;
        var badgeRT = (RectTransform)badgeGo.transform;
        badgeRT.anchorMin        = new Vector2(0f, 0.5f);
        badgeRT.anchorMax        = new Vector2(0f, 0.5f);
        badgeRT.pivot            = new Vector2(0f, 0.5f);
        badgeRT.anchoredPosition = new Vector2(kBadgeLeft, 0f);
        badgeRT.sizeDelta        = new Vector2(kBadgeSize, kBadgeSize);

        AbsImg(badgeGo, kKeyBorder, UISpriteFactory.RoundedRect(32, 5), 0, 0, 0, 0);
        AbsImg(badgeGo, kKeyBg,     UISpriteFactory.RoundedRect(32, 4), 1, 1, 1, 1);

        var keyTxtGo = new GameObject("Key");
        keyTxtGo.transform.SetParent(badgeGo.transform, false);
        var keyTmp = keyTxtGo.AddComponent<TextMeshProUGUI>();
        keyTmp.fontSize  = 13f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.color     = kKeyText;
        keyTmp.alignment = TextAlignmentOptions.Center;
        keyTmp.raycastTarget = false;
        var keyRT = (RectTransform)keyTxtGo.transform;
        keyRT.anchorMin = Vector2.zero; keyRT.anchorMax = Vector2.one;
        keyRT.offsetMin = Vector2.zero; keyRT.offsetMax = Vector2.zero;

        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        lblGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.fontSize           = 13f;
        lbl.color              = kText;
        lbl.alignment          = TextAlignmentOptions.MidlineLeft;
        lbl.enableWordWrapping = false;
        lbl.richText           = true;
        lbl.raycastTarget      = false;
        var lblRT = (RectTransform)lblGo.transform;
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(kBadgeLeft + kBadgeSize + kBadgeGap, 0f);
        lblRT.offsetMax = new Vector2(-kLabelRight, 0f);

        return (btn, keyTmp, lbl);
    }

    private TMP_Text AddTMP(GameObject parent, string name, string text,
                             float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = align; t.raycastTarget = false;
        return t;
    }
}
