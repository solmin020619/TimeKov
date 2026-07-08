// =====================================================================
// TransmissionComputerUI.cs
// 기지 전송 컴퓨터 UI (기획서 §11 · §13 · §21)
// 디자인: 사망 UI(DeathOverlay)식 부드러운 톤 —
//   채도 낮은 스틸/웜 오프화이트 팔레트, 반투명 패널 + 얕은 테두리(코너점·강한 아웃라인 X),
//   딥스페이스 배경 + 중앙 소프트 글로우(비네트 느낌) + 별필드.
//   등장 애니메이션(스케일/스태거) + 게이지 카운트업 + 회전 링.
// 폰트: 런타임에 로드된 Pretendard 를 우선 사용(없으면 한글 SDF 폴백).
// 특수 글리프(·→●)는 폰트 정적 아틀라스에 없어 □로 깨지므로 사용하지 않는다(점은 이미지로).
// 프리팹 없이 런타임 코드 빌드. 열닫기 상태는 GameUIController(UIState.Transmission)가 관리.
// =====================================================================

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TransmissionComputerUI : MonoBehaviour
{
    [Tooltip("(선택) UI 폰트. 비우면 런타임 로드된 Pretendard/한글 SDF를 자동 사용.")]
    [SerializeField] private TMP_FontAsset uiFont;

    // ── 팔레트 (사망 UI 톤: 채도 낮게, 웜 오프화이트) ─────────────────
    static readonly Color kBg        = Hex("07080C", 0.985f);
    static readonly Color kGlow      = Hex("7FA3B0", 0.05f);   // 중앙 소프트 글로우
    static readonly Color kPanel     = Hex("12151E", 0.62f);   // 반투명 패널
    static readonly Color kPanelEdge = Hex("8FB0BC", 0.12f);   // 얕은 테두리
    static readonly Color kSteel     = Hex("8FB0BC");          // 절제된 스틸 강조
    static readonly Color kSteelSoft = Hex("7FA3B0");
    static readonly Color kText      = Hex("E4DFD7");          // 웜 오프화이트
    static readonly Color kSub       = Hex("9098A2");
    static readonly Color kDim       = Hex("5C616C");
    static readonly Color kGold      = Hex("C8A45E");          // 뮤트 골드(목표)
    static readonly Color kGreen     = Hex("74B98C");          // 뮤트 그린(달성)
    static readonly Color kTrough    = Hex("0B0E15", 0.9f);
    static readonly Color kRowBg     = Hex("161A25", 0.7f);
    static readonly Color kRowSel    = Hex("8FB0BC", 0.16f);
    static readonly Color kDivider   = Hex("8FB0BC", 0.14f);

    // 지역별 강조색 (채도 낮춤)
    static readonly Color[] kRegionCol =
    {
        Hex("6FA773"), // 자연
        Hex("79A6C4"), // 설원
        Hex("C2A059"), // 사막
        Hex("C0765A"), // 용암
    };

    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static TransmissionComputerUI Instance { get; private set; }
    public static int LastCloseFrame { get; private set; } = -10;

    // ── 런타임 참조 ───────────────────────────────────────────────────
    private TMP_FontAsset _font;
    private GameObject    _panelRoot;
    private CanvasGroup   _rootGroup;
    private RectTransform _centerPanel;
    private RectTransform _ring1, _ring2;

    private CanvasGroup   _grpTop, _grpLeft, _grpRight, _grpBottom;
    private RectTransform _leftPanel, _rightPanel, _bottomGroup;

    private TMP_Text      _rateText;
    private Image         _gaugeFill;
    private TMP_Text      _regionInfoText;
    private RectTransform _kitListRoot;
    private TMP_Text      _projectionText;
    private TMP_Text      _guideText;
    private Button        _transmitBtn;
    private TMP_Text      _transmitBtnText;
    private readonly TMP_Text[] _regionState = new TMP_Text[4];
    private TMP_Text      _nextRewardText;

    private readonly List<GameObject> _kitRows = new();
    private int   _selectedKitId = -1;
    private int   _openedFrame = -1;
    private float _shownRate;
    private Tween _rateTween;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _font = ResolveFont();
        Build();
        _panelRoot.SetActive(false);
    }

    public static TransmissionComputerUI GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("TransmissionComputerUI").AddComponent<TransmissionComputerUI>();
    }

    private void OnEnable()  => TransmissionManager.OnRateChanged += OnRateChanged;
    private void OnDisable() => TransmissionManager.OnRateChanged -= OnRateChanged;

    private void Update()
    {
        if (!IsOpen) return;
        float dt = Time.unscaledDeltaTime;
        if (_ring1 != null) _ring1.Rotate(0, 0,  10f * dt);
        if (_ring2 != null) _ring2.Rotate(0, 0, -15f * dt);
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F))
            Close();
    }

    // ── 공개 API ──────────────────────────────────────────────────────
    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    public void Open()
    {
        _panelRoot.SetActive(true);
        _openedFrame = Time.frameCount;
        _selectedKitId = -1;
        var mgr = TransmissionManager.Instance;
        _shownRate = 0f;
        ApplyShownRate();
        RefreshContent(mgr);
        PlayOpenAnim(mgr);
    }

    public void HidePanel()
    {
        KillTweens();
        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    public void Close()
    {
        LastCloseFrame = Time.frameCount;
        GameUIController.Instance?.CloseTransmissionUI();
        PlayCloseAnim();
    }

    private void OnRateChanged(int newRate)
    {
        if (!IsOpen) return;
        RefreshContent(TransmissionManager.Instance);
        AnimateRate(newRate, 0.5f);
        PulseRate();
    }

    // =====================================================================
    // 애니메이션
    // =====================================================================
    private void PlayOpenAnim(TransmissionManager mgr)
    {
        KillTweens();
        _rootGroup.alpha = 0f;
        _rootGroup.DOFade(1f, 0.25f).SetUpdate(true);
        _centerPanel.localScale = Vector3.one * 0.96f;
        _centerPanel.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);

        AnimateIn(_grpTop,    null,        new Vector2(0,  14f), 0.05f);
        AnimateIn(_grpLeft,   _leftPanel,  new Vector2(-28f, 0), 0.12f);
        AnimateIn(_grpRight,  _rightPanel, new Vector2( 28f, 0), 0.12f);
        AnimateIn(_grpBottom, _bottomGroup,new Vector2(0, -18f), 0.20f);

        AnimateRate(mgr != null ? mgr.TransmissionRate : 0, 0.8f, 0.28f);
        StaggerKitRows();
    }

    private void AnimateIn(CanvasGroup grp, RectTransform rt, Vector2 fromOffset, float delay)
    {
        if (grp != null) { grp.alpha = 0f; grp.DOFade(1f, 0.34f).SetDelay(delay).SetUpdate(true); }
        if (rt != null)
        {
            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + fromOffset;
            rt.DOAnchorPos(target, 0.45f).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    // 키트 행은 VerticalLayoutGroup이 위치를 제어하므로 위치는 건드리지 않고 알파만 스태거(레이아웃과 안 싸우게).
    private void StaggerKitRows()
    {
        for (int i = 0; i < _kitRows.Count; i++)
        {
            var go = _kitRows[i];
            if (go == null) continue;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) continue;
            cg.DOKill();
            cg.alpha = 0f;
            cg.DOFade(1f, 0.28f).SetDelay(0.30f + i * 0.05f).SetUpdate(true);
        }
    }

    private void PlayCloseAnim()
    {
        KillTweens();
        if (_rootGroup == null) { _panelRoot.SetActive(false); return; }
        _rootGroup.DOFade(0f, 0.14f).SetUpdate(true);
        _centerPanel.DOScale(0.97f, 0.14f).SetUpdate(true)
            .OnComplete(() => { if (_panelRoot != null) _panelRoot.SetActive(false); });
    }

    private void AnimateRate(float to, float dur, float delay = 0f)
    {
        _rateTween?.Kill();
        _rateTween = DOTween.To(() => _shownRate, v => { _shownRate = v; ApplyShownRate(); }, to, dur)
            .SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void ApplyShownRate()
    {
        int r = Mathf.RoundToInt(_shownRate);
        if (_rateText != null) _rateText.text = $"{r}<size=40><voffset=0.15em><color=#9098A2>%</color></voffset></size>";
        if (_gaugeFill != null) _gaugeFill.fillAmount = Mathf.Clamp01(_shownRate / TransmissionManager.MaxRate);
    }

    private void PulseRate()
    {
        if (_rateText == null) return;
        _rateText.transform.DOKill();
        _rateText.transform.localScale = Vector3.one;
        _rateText.transform.DOPunchScale(Vector3.one * 0.14f, 0.45f, 6, 0.7f).SetUpdate(true);
    }

    private void KillTweens()
    {
        _rateTween?.Kill();
        if (_rootGroup != null) _rootGroup.DOKill();
        if (_centerPanel != null) _centerPanel.DOKill();
        foreach (var cg in new[] { _grpTop, _grpLeft, _grpRight, _grpBottom })
            if (cg != null) cg.DOKill();
        if (_rateText != null) _rateText.transform.DOKill();
        foreach (var go in _kitRows)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.DOKill(); cg.alpha = 1f; }
        }
    }

    // =====================================================================
    // 데이터 갱신
    // =====================================================================
    private void RefreshContent(TransmissionManager mgr)
    {
        if (mgr == null) return;
        if (_regionInfoText != null)
            _regionInfoText.text = $"현재 구간  <color=#E4DFD7>{RegionName(mgr.CurrentRegion)}</color>" +
                                   $"       일반 상한  <color=#E4DFD7>{mgr.CurrentRegionNormalCap}%</color>" +
                                   $"       목표  <color=#C8A45E>{mgr.CurrentRegionGoal}%</color>";
        RefreshRegionPanel(mgr);
        RebuildKitList(mgr);
        RefreshSelectionUI(mgr);
    }

    private void RefreshRegionPanel(TransmissionManager mgr)
    {
        int rate = mgr.TransmissionRate;
        for (int i = 0; i < 4; i++)
        {
            var st = _regionState[i];
            if (st == null) continue;
            var region = (TransmissionRegion)i;
            int goal = TransmissionManager.RegionGoal(region);
            int start = TransmissionManager.RegionStart(region);
            if (rate >= goal)       { st.text = "달성";    st.color = kGreen; }
            else if (rate >= start) { st.text = "진행 중"; st.color = kSteel; }
            else                    { st.text = "잠김";    st.color = kDim; }
        }

        if (_nextRewardText != null)
        {
            int next = ((rate / 10) + 1) * 10;
            _nextRewardText.text = rate >= 100
                ? "<color=#C8A45E>전송률 100% 달성</color>"
                : $"다음 보상까지  <color=#E4DFD7>{Mathf.Max(0, next - rate)}%</color>";
        }
    }

    private void RebuildKitList(TransmissionManager mgr)
    {
        foreach (var go in _kitRows)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.DOKill();   // 지연 트윈이 파괴된 오브젝트에 접근하지 않도록 먼저 정리
            Destroy(go);
        }
        _kitRows.Clear();
        bool any = false;
        foreach (var kit in mgr.GetOwnedKits())
        {
            any = true;
            AddKitRow(kit, mgr.GetOwnedCount(kit.itemId));
        }
        if (!any) { _selectedKitId = -1; AddEmptyRow("보유한 충전 키트가 없습니다."); }
    }

    private void RefreshSelectionUI(TransmissionManager mgr)
    {
        var kit = _selectedKitId >= 0 ? FindDef(mgr, _selectedKitId) : null;
        foreach (var go in _kitRows)
        {
            if (go == null) continue;
            var img = go.GetComponent<Image>();
            if (img == null) continue;
            bool sel = kit != null && go.name == RowName(kit.itemId);
            img.color = sel ? kRowSel : kRowBg;
        }

        bool can = kit != null && mgr.CanTransmit(kit, out _);
        string guide = kit == null ? "전송할 충전 키트를 선택하세요."
                     : can ? $"전송 준비 완료 — {kit.displayName}"
                     : ReasonOf(mgr, kit);
        if (_guideText != null) { _guideText.text = guide; _guideText.color = (kit != null && !can) ? kGold : kSub; }

        if (_projectionText != null)
        {
            if (kit != null)
            {
                int cur = mgr.TransmissionRate;
                int proj = mgr.GetProjectedRate(kit);
                _projectionText.text = $"전송하면  <color=#8FB0BC>{proj}%</color> 로 상승" +
                                       $"   <size=80%><color=#74B98C>+{Mathf.Max(0, proj - cur)}</color></size>";
            }
            else _projectionText.text = "충전 키트를 선택하면 예상 전송률이 표시됩니다.";
        }

        if (_transmitBtn != null && _transmitBtn.image != null)
            _transmitBtn.image.color = can ? Hex("335A66", 0.95f) : Hex("1A1F29", 0.9f);
        if (_transmitBtnText != null) _transmitBtnText.color = can ? kText : kDim;
    }

    private string ReasonOf(TransmissionManager mgr, TransmissionManager.ChargedKitDef kit)
    {
        mgr.CanTransmit(kit, out string reason);
        return string.IsNullOrEmpty(reason) ? "" : reason;
    }

    private TransmissionManager.ChargedKitDef FindDef(TransmissionManager mgr, int itemId)
    {
        foreach (var k in mgr.KitDefs) if (k != null && k.itemId == itemId) return k;
        return null;
    }

    private void OnClickTransmit()
    {
        var mgr = TransmissionManager.Instance;
        if (mgr == null || _selectedKitId < 0) return;
        mgr.TryTransmit(_selectedKitId);
    }

    private void SelectKit(int itemId)
    {
        _selectedKitId = itemId;
        var mgr = TransmissionManager.Instance;
        if (mgr != null) RefreshSelectionUI(mgr);
    }

    // =====================================================================
    // 빌드
    // =====================================================================
    private void Build()
    {
        var cvGo = new GameObject("Canvas");
        cvGo.transform.SetParent(transform, false);
        var cv = cvGo.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 60;
        var cs = cvGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight  = 0.5f;
        cvGo.AddComponent<GraphicRaycaster>();

        _panelRoot = MakeImage("Root", cvGo.transform, Vector2.zero, Vector2.zero, kBg);
        Stretch((RectTransform)_panelRoot.transform);
        _rootGroup = _panelRoot.AddComponent<CanvasGroup>();

        var grad = MakeImage("SpaceGradient", _panelRoot.transform, Vector2.zero, Vector2.zero, Color.white);
        Stretch((RectTransform)grad.transform);
        var gimg = grad.GetComponent<Image>();
        gimg.sprite = UISpriteFactory.RoundedRectVGrad(Hex32("0C1220"), Hex32("05070C"), 64, 0);
        gimg.raycastTarget = false;

        // 중앙 소프트 글로우 (비네트 느낌 — 가운데만 은은하게 밝게)
        var glow = MakeImage("CenterGlow", _panelRoot.transform, new Vector2(1500, 1100), new Vector2(0, 60), kGlow);
        var glowImg = glow.GetComponent<Image>();
        glowImg.sprite = UISpriteFactory.Disc(128); glowImg.raycastTarget = false;

        BuildStars(_panelRoot.transform);

        var panelGo = MakeImage("Panel", _panelRoot.transform, new Vector2(1600, 900), Vector2.zero, Hex("000000", 0f));
        panelGo.GetComponent<Image>().raycastTarget = false;
        _centerPanel = (RectTransform)panelGo.transform;
        var p = panelGo.transform;

        BuildBackdropRings(p);
        BuildTopBar(p);
        BuildRateFocal(p);
        BuildKitPanel(p);
        BuildRegionPanel(p);
        BuildBottomBar(p);
    }

    private void BuildBackdropRings(Transform p)
    {
        var r1 = MakeImage("BgRing1", p, new Vector2(300, 300), new Vector2(0, 150), Hex("8FB0BC", 0.09f));
        var i1 = r1.GetComponent<Image>(); i1.sprite = UISpriteFactory.Ring(128, 2.5f); i1.raycastTarget = false;
        _ring1 = (RectTransform)r1.transform;
        var r2 = MakeImage("BgRing2", p, new Vector2(232, 232), new Vector2(0, 150), Hex("7FA3B0", 0.07f));
        var i2 = r2.GetComponent<Image>(); i2.sprite = UISpriteFactory.Ring(128, 1.6f); i2.raycastTarget = false;
        _ring2 = (RectTransform)r2.transform;
    }

    private void BuildTopBar(Transform p)
    {
        var top = MakeContainer("TopBar", p, out _grpTop);
        var title = MakeTMP("Title", top, new Vector2(760, 52), new Vector2(0, 400),
            "시간에너지 전송", 36, kText, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; title.characterSpacing = 6f;
        MakeTMP("Sub", top, new Vector2(760, 24), new Vector2(0, 364),
            "기지 전송 컴퓨터", 15, kSteelSoft, TextAlignmentOptions.Center).characterSpacing = 4f;
        SoftDivider(top, 240f, new Vector2(0, 342));

        var closeBtn = MakeButton("CloseButton", top, new Vector2(46, 46), new Vector2(724, 398), "X", 20, Hex("161C26", 0.7f));
        closeBtn.GetComponent<Button>().onClick.AddListener(Close);
    }

    private void BuildRateFocal(Transform p)
    {
        _rateText = MakeTMP("Rate", p, new Vector2(560, 130), new Vector2(0, 150),
            "0<size=40><voffset=0.15em><color=#9098A2>%</color></voffset></size>", 96, kSteel, TextAlignmentOptions.Center);
        _rateText.fontStyle = FontStyles.Bold;
        MakeTMP("RateCap", p, new Vector2(560, 24), new Vector2(0, 88),
            "시간에너지 전송률", 14, kSub, TextAlignmentOptions.Center).characterSpacing = 3f;

        const float gW = 560f;
        var trough = MakeImage("GaugeBG", p, new Vector2(gW, 18f), new Vector2(0, 32f), kTrough);
        var tImg = trough.GetComponent<Image>();
        tImg.sprite = UISpriteFactory.RoundedRect(20, 9); tImg.type = Image.Type.Sliced;

        for (int i = 0; i < 4; i++)
        {
            var seg = new GameObject($"Seg{i}", typeof(RectTransform), typeof(Image));
            seg.transform.SetParent(trough.transform, false);
            var srt = (RectTransform)seg.transform;
            srt.anchorMin = new Vector2(i * 0.25f, 0f); srt.anchorMax = new Vector2((i + 1) * 0.25f, 1f);
            srt.offsetMin = new Vector2(i == 0 ? 3 : 1, 3); srt.offsetMax = new Vector2(i == 3 ? -3 : -1, -3);
            var si = seg.GetComponent<Image>();
            si.color = new Color(kRegionCol[i].r, kRegionCol[i].g, kRegionCol[i].b, 0.12f);
            si.sprite = UISpriteFactory.RoundedRect(12, 5); si.type = Image.Type.Sliced; si.raycastTarget = false;
        }

        var fill = MakeImage("GaugeFill", trough.transform, Vector2.zero, Vector2.zero, kSteel);
        Stretch((RectTransform)fill.transform);
        _gaugeFill = fill.GetComponent<Image>();
        _gaugeFill.sprite = UISpriteFactory.RoundedRect(20, 9);
        _gaugeFill.type = Image.Type.Filled; _gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        _gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left; _gaugeFill.fillAmount = 0f;

        string[] names = { "자연", "설원", "사막", "용암" };
        for (int i = 1; i < 4; i++)
        {
            float fx = (i * 0.25f - 0.5f) * gW;
            MakeImage($"Div{i}", trough.transform, new Vector2(2f, 24f), new Vector2(fx, 0f), Hex("07080C", 0.9f))
                .GetComponent<Image>().raycastTarget = false;
        }
        for (int i = 0; i < 4; i++)
        {
            float fx = ((i + 0.5f) * 0.25f - 0.5f) * gW;
            MakeTMP($"RegLbl{i}", p, new Vector2(120, 20), new Vector2(fx, 6f), names[i], 12, kDim, TextAlignmentOptions.Center)
                .characterSpacing = 2f;
        }

        _regionInfoText = MakeTMP("RegionInfo", p, new Vector2(640, 26), new Vector2(0, -28f),
            "", 15, kSub, TextAlignmentOptions.Center);
    }

    private void BuildKitPanel(Transform p)
    {
        var container = MakeContainer("LeftGroup", p, out _grpLeft);
        var panel = SoftPanel("KitPanel", container, new Vector2(380, 440), new Vector2(-572, -16), "보유 충전 키트");
        _leftPanel = (RectTransform)panel.transform;

        var listGo = new GameObject("KitList", typeof(RectTransform));
        listGo.transform.SetParent(panel.transform, false);
        _kitListRoot = (RectTransform)listGo.transform;
        _kitListRoot.anchorMin = _kitListRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _kitListRoot.pivot = new Vector2(0.5f, 1f);
        _kitListRoot.anchoredPosition = new Vector2(0, 148f);
        _kitListRoot.sizeDelta = new Vector2(344, 320);
        var v = listGo.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f; v.padding = new RectOffset(4, 4, 0, 0);
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
    }

    private void BuildRegionPanel(Transform p)
    {
        var container = MakeContainer("RightGroup", p, out _grpRight);
        var panel = SoftPanel("RegionPanel", container, new Vector2(380, 440), new Vector2(572, -16), "전송 구간과 보상");
        _rightPanel = (RectTransform)panel.transform;

        string[] names = { "자연맵", "설원맵", "사막맵", "용암맵" };
        for (int i = 0; i < 4; i++)
        {
            float y = 118 - i * 50;
            // 지역색 점(이미지) — 글리프 대신
            var dot = MakeImage($"Dot{i}", panel.transform, new Vector2(9, 9), new Vector2(-150, y), kRegionCol[i]);
            var di = dot.GetComponent<Image>(); di.sprite = UISpriteFactory.Disc(32); di.raycastTarget = false;

            MakeTMP($"RName{i}", panel.transform, new Vector2(150, 28), new Vector2(-52, y), names[i], 16, kText, TextAlignmentOptions.Left);
            int goal = TransmissionManager.RegionGoal((TransmissionRegion)i);
            MakeTMP($"RGoal{i}", panel.transform, new Vector2(70, 24), new Vector2(52, y), $"{goal}%", 13, kDim, TextAlignmentOptions.Left);
            _regionState[i] = MakeTMP($"RState{i}", panel.transform, new Vector2(110, 26), new Vector2(112, y), "", 14, kSub, TextAlignmentOptions.Right);
        }

        SoftDivider(panel.transform, 300f, new Vector2(0, -92));
        _nextRewardText = MakeTMP("NextReward", panel.transform, new Vector2(340, 32), new Vector2(0, -128), "", 15, kText, TextAlignmentOptions.Center);
        MakeTMP("RewardHint", panel.transform, new Vector2(340, 40), new Vector2(0, -162),
            "10% 단위 보상, 25 / 50 / 75% 지역 해금", 12, kDim, TextAlignmentOptions.Center);
    }

    private void BuildBottomBar(Transform p)
    {
        var container = MakeContainer("BottomGroup", p, out _grpBottom);
        _bottomGroup = (RectTransform)container;

        var bar = MakeImage("BottomBar", container, new Vector2(900, 116), new Vector2(0, -322), kPanel);
        var bImg = bar.GetComponent<Image>();
        bImg.sprite = UISpriteFactory.RoundedRect(48, 22); bImg.type = Image.Type.Sliced;
        SoftEdge(bar);

        _projectionText = MakeTMP("Projection", bar.transform, new Vector2(860, 34), new Vector2(0, 24),
            "충전 키트를 선택하면 예상 전송률이 표시됩니다.", 20, kText, TextAlignmentOptions.Center);
        _projectionText.fontStyle = FontStyles.Bold;
        _guideText = MakeTMP("Guide", bar.transform, new Vector2(860, 26), new Vector2(0, -22),
            "전송할 충전 키트를 선택하세요.", 14, kSub, TextAlignmentOptions.Center);

        var btn = MakeButton("TransmitButton", container, new Vector2(320, 60), new Vector2(0, -426), "전송", 21, Hex("335A66", 0.95f));
        var bt = btn.GetComponent<Image>();
        bt.sprite = UISpriteFactory.RoundedRect(48, 24); bt.type = Image.Type.Sliced;
        _transmitBtn = btn.GetComponent<Button>();
        _transmitBtn.onClick.AddListener(OnClickTransmit);
        _transmitBtnText = btn.GetComponentInChildren<TextMeshProUGUI>();
        _transmitBtnText.characterSpacing = 4f;
        SoftEdge(btn);
    }

    // ── 키트 행 ───────────────────────────────────────────────────────
    private void AddKitRow(TransmissionManager.ChargedKitDef kit, int count)
    {
        var go = new GameObject(RowName(kit.itemId), typeof(RectTransform));
        go.transform.SetParent(_kitListRoot, false);
        go.AddComponent<LayoutElement>().minHeight = 48f;
        go.AddComponent<CanvasGroup>();   // 스태거 페이드용 — 생성 시 부착(지연 트윈 안전)
        var img = go.AddComponent<Image>();
        img.color = kRowBg; img.sprite = UISpriteFactory.RoundedRect(20, 10); img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };   // EventSystem NaN 투영 스팸 방지
        int id = kit.itemId; btn.onClick.AddListener(() => SelectKit(id));

        var accent = MakeImage("Accent", go.transform, new Vector2(4f, 26f), Vector2.zero, kRegionCol[(int)kit.region]);
        var art = (RectTransform)accent.transform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f); art.pivot = new Vector2(0f, 0.5f);
        art.anchoredPosition = new Vector2(10f, 0f);
        var ai = accent.GetComponent<Image>(); ai.sprite = UISpriteFactory.RoundedRect(8, 3); ai.type = Image.Type.Sliced; ai.raycastTarget = false;

        string tag = kit.isBoss ? "  <size=72%><color=#C99A5A>보스</color></size>" : "";
        var label = MakeTMP("Label", go.transform, Vector2.zero, Vector2.zero, $"{kit.displayName}{tag}", 15, kText, TextAlignmentOptions.MidlineLeft, stretch: true);
        label.rectTransform.offsetMin = new Vector2(26, 0); label.rectTransform.offsetMax = new Vector2(-104, 0);

        var right = MakeTMP("Right", go.transform, Vector2.zero, Vector2.zero, $"<color=#9098A2>x{count}</color>   <color=#8FB0BC>+{kit.ratePercent}%</color>", 15, kSub, TextAlignmentOptions.MidlineRight, stretch: true);
        right.rectTransform.offsetMin = new Vector2(14, 0); right.rectTransform.offsetMax = new Vector2(-14, 0);
        _kitRows.Add(go);
    }

    private void AddEmptyRow(string msg)
    {
        var go = new GameObject("EmptyRow", typeof(RectTransform));
        go.transform.SetParent(_kitListRoot, false);
        go.AddComponent<LayoutElement>().minHeight = 60f;
        go.AddComponent<CanvasGroup>();   // 스태거 페이드용
        MakeTMP("Label", go.transform, Vector2.zero, Vector2.zero, msg, 14, kDim, TextAlignmentOptions.Center, stretch: true);
        _kitRows.Add(go);
    }

    private static string RowName(int itemId) => "KitRow_" + itemId;
    private static string RegionName(TransmissionRegion r) => r switch
    {
        TransmissionRegion.Nature => "자연맵",
        TransmissionRegion.Snow   => "설원맵",
        TransmissionRegion.Desert => "사막맵",
        TransmissionRegion.Lava   => "용암맵",
        _ => "-"
    };

    // =====================================================================
    // 별 배경 + 반짝임
    // =====================================================================
    private void BuildStars(Transform parent)
    {
        var rng = new System.Random(20260708);
        var starsGo = new GameObject("Stars", typeof(RectTransform));
        starsGo.transform.SetParent(parent, false);
        Stretch((RectTransform)starsGo.transform);
        var disc = UISpriteFactory.Disc(16);

        for (int i = 0; i < 150; i++)
        {
            float x = (float)(rng.NextDouble() * 1900.0 - 950.0);
            float y = (float)(rng.NextDouble() * 1060.0 - 530.0);
            float s = 1.1f + (float)rng.NextDouble() * 2.2f;
            float a = 0.10f + (float)rng.NextDouble() * 0.45f;
            Color col = rng.NextDouble() < 0.3 ? new Color(0.66f, 0.78f, 0.84f, a) : new Color(0.92f, 0.90f, 0.86f, a);
            var star = MakeImage($"Star{i}", starsGo.transform, new Vector2(s, s), new Vector2(x, y), col);
            var im = star.GetComponent<Image>(); im.sprite = disc; im.raycastTarget = false;
        }
        for (int i = 0; i < 14; i++)
        {
            float x = (float)(rng.NextDouble() * 1820.0 - 910.0);
            float y = (float)(rng.NextDouble() * 1000.0 - 500.0);
            float mk = 7f + (float)rng.NextDouble() * 6f;
            var glow = MakeImage($"StarGlow{i}", starsGo.transform, new Vector2(mk * 2.4f, mk * 2.4f), new Vector2(x, y), new Color(0.56f, 0.70f, 0.78f, 0.14f));
            glow.GetComponent<Image>().sprite = disc; glow.GetComponent<Image>().raycastTarget = false;
            var bright = MakeImage($"StarBright{i}", starsGo.transform, new Vector2(mk * 0.5f, mk * 0.5f), new Vector2(x, y), new Color(0.86f, 0.90f, 0.92f, 0.9f));
            var bi = bright.GetComponent<Image>(); bi.sprite = disc; bi.raycastTarget = false;
            float dur = 1.4f + (float)rng.NextDouble() * 2.0f;
            bi.DOFade(0.22f, dur).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetDelay((float)rng.NextDouble());
        }
    }

    // =====================================================================
    // UI 헬퍼
    // =====================================================================
    private Transform MakeContainer(string name, Transform parent, out CanvasGroup grp)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        grp = go.AddComponent<CanvasGroup>();
        return go.transform;
    }

    // 부드러운 반투명 패널 (얕은 테두리 + 타이틀 + 소프트 디바이더). 코너점/강한 아웃라인 없음.
    private GameObject SoftPanel(string name, Transform parent, Vector2 size, Vector2 pos, string headerTitle)
    {
        var panel = MakeImage(name, parent, size, pos, kPanel);
        var img = panel.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(64, 30); img.type = Image.Type.Sliced;
        SoftEdge(panel);

        float top = size.y * 0.5f;
        MakeTMP("HeaderTitle", panel.transform, new Vector2(size.x - 40f, 32f), new Vector2(0, top - 34f), headerTitle, 17, kSteel, TextAlignmentOptions.Center)
            .fontStyle = FontStyles.Bold;
        SoftDivider(panel.transform, size.x - 64f, new Vector2(0, top - 60f));
        return panel;
    }

    // 얕은 테두리 + 은은한 외곽 글로우 (딱딱한 라인 대신)
    private void SoftEdge(GameObject panel)
    {
        var glow = MakeImage("Glow", panel.transform, ((RectTransform)panel.transform).sizeDelta + new Vector2(30f, 30f), Vector2.zero, Hex("8FB0BC", 0.05f));
        var gi = glow.GetComponent<Image>(); gi.sprite = UISpriteFactory.RoundedRect(96, 46); gi.type = Image.Type.Sliced; gi.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        var ol = panel.AddComponent<UnityEngine.UI.Outline>();   // 전역 Outline 충돌 방지 한정명
        ol.effectColor = kPanelEdge;
        ol.effectDistance = new Vector2(1f, -1f);
    }

    // 가운데가 밝고 양끝이 페이드되는 느낌의 얕은 구분선 (양쪽 캡으로 소프트하게)
    private void SoftDivider(Transform parent, float width, Vector2 pos)
    {
        var line = MakeImage("Divider", parent, new Vector2(width, 1.5f), pos, kDivider);
        var li = line.GetComponent<Image>(); li.sprite = UISpriteFactory.RoundedRect(8, 1); li.type = Image.Type.Sliced; li.raycastTarget = false;
        var dot = MakeImage("DivDot", parent, new Vector2(5, 5), pos, Hex("8FB0BC", 0.55f));
        var dd = dot.GetComponent<Image>(); dd.sprite = UISpriteFactory.Disc(16); dd.raycastTarget = false;
    }

    private static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private TMP_Text MakeTMP(string name, Transform parent, Vector2 size, Vector2 pos,
                             string text, float fontSize, Color color,
                             TextAlignmentOptions align, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        else { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos; }
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = fontSize; t.color = color;
        t.alignment = align; t.raycastTarget = false; t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private GameObject MakeButton(string name, Transform parent, Vector2 size, Vector2 pos, string label, float fontSize, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = bg; img.sprite = UISpriteFactory.RoundedRect(32, 16); img.type = Image.Type.Sliced;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };   // EventSystem NaN 투영 스팸 방지

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = txtGo.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = label; t.fontSize = fontSize; t.color = kText;
        t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold;
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 런타임 폰트 해석: 이미 로드된 Pretendard 우선(게임 표준·모던), 없으면 한글 SDF 폴백.
    private TMP_FontAsset ResolveFont()
    {
        if (uiFont != null) return uiFont;
        var f = FindLoadedFont("Pretendard-SemiBold");
        if (f == null) f = FindLoadedFont("Pretendard");
        if (f == null) f = FindLoadedFont("남양주");
        if (f == null) f = Resources.Load<TMP_FontAsset>("Font/GabiaMaeumgyeol SDF");
        if (f == null) { var any = FindAnyObjectByType<TextMeshProUGUI>(); if (any != null) f = any.font; }
        if (f == null) f = TMP_Settings.defaultFontAsset;
        return f;
    }

    private static TMP_FontAsset FindLoadedFont(string namePart)
    {
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in all)
            if (f != null && f.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        return null;
    }

    private static Color Hex(string hex, float a = 1f)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) { c.a = a; return c; }
        return Color.white;
    }
    private static Color32 Hex32(string hex) => Hex(hex);
}
