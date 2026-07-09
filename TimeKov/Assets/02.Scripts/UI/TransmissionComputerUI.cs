// =====================================================================
// TransmissionComputerUI.cs  ─ 시안 2a 완전 구현 (TimeKov 시간에너지 전송 컴퓨터)
// 클로드 디자인 스펙(시안 2a)을 Unity 런타임 코드로 구현. 프리팹 없음.
// 기준 캔버스 1920×1080, Scale With Screen Size, Match 0.5.
// 공개 API(Instance/GetOrCreate/Open/HidePanel/Close/LastCloseFrame/IsOpen)는
// 기존 GameUIController·TransmissionComputerTerminal 연동을 위해 유지.
//
// 주의(스펙 F-3): 특수 글리프(✓ ? ▤ ◆ ★ ▶ ●)는 폰트 아틀라스에 없어 □로 깨지므로
//   전부 도형/스프라이트로 그린다(텍스트로 안 씀). "?"만 예외로 폰트 텍스트 사용.
// 근사 처리: 점선 링→저알파 실선, 4색 게이지→절차 생성 가로 그라데이션 텍스처.
// 데이터는 스펙 D의 시안 데모 모델(진행 42%, 설원 키트) — 실 인벤토리 연동은 후속.
// =====================================================================

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TransmissionComputerUI : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset krFont;    // 선택: 비우면 Pretendard 자동
    [SerializeField] private TMP_FontAsset monoFont;  // 선택: 비우면 Rajdhani/폴백

    // ── A. 팔레트 ─────────────────────────────────────────────────────
    static readonly Color Accent      = C("4CC9F7");
    static readonly Color AccentBright = C("EAF7FF");
    static readonly Color AccentSoft   = C("9FDCF9");
    static readonly Color AccentSoft2  = C("8FD5EE");
    static readonly Color TextBright   = C("F1F7FD");
    static readonly Color TextMain     = C("E8F2FB");
    static readonly Color Success      = C("5FDD9D");
    static readonly Color Danger       = C("F27059");
    static readonly Color[] RegionCol = { C("43B06C"), C("5BC7E8"), C("D9A44A"), C("E0593A") };
    static readonly string[] RegionKo = { "자연", "설원", "사막", "용암" };

    // ── 싱글톤/공개 API ───────────────────────────────────────────────
    public static TransmissionComputerUI Instance { get; private set; }
    public static int LastCloseFrame { get; private set; } = -10;
    public bool IsOpen => _root != null && _root.activeSelf;

    // ── 런타임 참조 ───────────────────────────────────────────────────
    private TMP_FontAsset _kr, _mono;
    private GameObject _root;
    private CanvasGroup _cg;
    private RectTransform _content;
    private float _trackW;
    private int _openedFrame = -1;

    private TMP_Text _rateBig, _subLabel, _statusLine, _previewVal, _selName, _selMeta;
    private Image _fill; private RectTransform _node, _nodeLabelRT; private TMP_Text _nodeLabel;
    private RectTransform _sweep, _sweepMaskRT;
    private Button _sendBtn; private Image _sendBtnImg; private CanvasGroup _sendBtnCg;
    private readonly List<RectTransform> _spinRings = new();
    private readonly List<GameObject> _markers = new();
    private readonly List<KitRow> _kitRows = new();
    private GameObject _tooltip; private TMP_Text _ttTitle, _ttName, _ttState; private Image _ttBox;
    private TMP_Text _logText;

    private Model _m;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _kr = ResolveFont(krFont, "Pretendard-SemiBold", "Pretendard", "남양주", "GabiaMaeumgyeol");
        _mono = ResolveFont(monoFont, "JetBrains", "Rajdhani-SemiBold", "Rajdhani", null) ?? _kr;
        Debug.Log($"[TransmissionUI] 폰트 → 한글: {(_kr != null ? _kr.name : "null")} / 영숫자: {(_mono != null ? _mono.name : "null")}  (krFont지정={(krFont != null ? krFont.name : "none")}, monoFont지정={(monoFont != null ? monoFont.name : "none")})");
        _m = new Model();
        Build();
        _root.SetActive(false);
    }

    public static TransmissionComputerUI GetOrCreate()
        => Instance != null ? Instance : new GameObject("TransmissionComputerUI").AddComponent<TransmissionComputerUI>();

    private void Update()
    {
        if (!IsOpen) return;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _spinRings.Count; i++)
            if (_spinRings[i] != null) _spinRings[i].Rotate(0, 0, (i % 2 == 0 ? -1f : 1f) * 9f * dt);
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F)) Close();
    }

    public void Open()
    {
        _root.SetActive(true);
        _openedFrame = Time.frameCount;
        _m.selectedId = null;
        RefreshAll();
        SetGauge(_m.progress, false);
        PlayOpenAnim();
    }

    public void HidePanel() { KillAll(); if (_root != null) _root.SetActive(false); }

    public void Close()
    {
        LastCloseFrame = Time.frameCount;
        GameUIController.Instance?.CloseTransmissionUI();
        if (_cg == null) { _root.SetActive(false); return; }
        KillAll();
        _cg.DOFade(0f, 0.15f).SetUpdate(true);
        _content.DOScale(0.98f, 0.15f).SetUpdate(true).OnComplete(() => { if (_root != null) _root.SetActive(false); });
    }

    // =====================================================================
    // 빌드
    // =====================================================================
    private void Build()
    {
        var cvGo = new GameObject("Canvas");
        cvGo.transform.SetParent(transform, false);
        var cv = cvGo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 60;
        var cs = cvGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
        cvGo.AddComponent<GraphicRaycaster>();

        _root = NewGO("Root", cvGo.transform); Stretch(_root);
        _root.AddComponent<Image>().color = new Color(0, 0, 0, 0); // raycast blocker (invisible; bg drawn below)
        _cg = _root.AddComponent<CanvasGroup>();

        _content = TL(NewGO("Content", _root.transform), 0, 0, 1920, 1080);

        BuildBackground();
        BuildHeader();
        BuildProgress();
        BuildBody();
        BuildFooter();
        BuildTooltip();
        BuildOverlay();
    }

    // ── C-0 배경 ──────────────────────────────────────────────────────
    private void BuildBackground()
    {
        var bg = Img("BG", _content, 0, 0, 1920, 1080, Color.white, RadialTex());
        bg.raycastTarget = false;

        BuildGrid();

        // 크로노 링(우상단/좌하단). 점선은 저알파 실선으로 근사, 일부 회전(E-3).
        AddRing(-140 + 1920 - 620, -180, 620, 0.12f, 3f, false);
        AddRing(-60 + 1920 - 460, -100, 460, 0.16f, 2f, true);
        AddRing(20 + 1920 - 300, -20, 300, 0.08f, 2f, false);
        AddRing(-200, 1080 - 560 + 260, 560, 0.08f, 3f, false);
        AddRing(-140, 1080 - 440 + 200, 440, 0.10f, 2f, true);

        // 코너 브래킷 4개
        Bracket(26, 26, true, true); Bracket(1920 - 26 - 34, 26, false, true);
        Bracket(26, 1080 - 26 - 34, true, false); Bracket(1920 - 26 - 34, 1080 - 26 - 34, false, false);
    }

    // 규칙적 정사각형 그리드 — 정확히 cell 간격으로 실제 라인을 그린다(타일 텍스처의 스케일 왜곡 방지).
    private void BuildGrid()
    {
        var col = C("4CC9F7", 0.03f);
        const int cell = 56;
        for (int x = 0; x <= 1920; x += cell) Img("gv", _content, x, 0, 1, 1080, col).raycastTarget = false;
        for (int y = 0; y <= 1080; y += cell) Img("gh", _content, 0, y, 1920, 1, col).raycastTarget = false;
    }

    private void AddRing(float x, float y, float d, float a, float th, bool spin)
    {
        var im = Img("ChronoRing", _content, x, y, d, d, C("4CC9F7", a), UISpriteFactory.Ring((int)Mathf.Min(256, d), th));
        im.raycastTarget = false;
        if (spin) _spinRings.Add((RectTransform)im.transform);
    }

    private void Bracket(float x, float y, bool left, bool top)
    {
        var col = C("4CC9F7", 0.55f);
        // 수평 팔
        Img("brkH", _content, x, top ? y : y + 32, 34, 2, col).raycastTarget = false;
        // 수직 팔
        Img("brkV", _content, left ? x : x + 32, y, 2, 34, col).raycastTarget = false;
    }

    // ── C-1 헤더 ──────────────────────────────────────────────────────
    private void BuildHeader()
    {
        // 좌측 그룹
        Img("sysDot", _content, 88, 62, 8, 8, Accent, UISpriteFactory.RoundedRect(16, 4)).raycastTarget = false;
        Txt("sys", _content, 108, 58, 600, 18, "TIMEKOV // TRANSFER TERMINAL", _mono, 14, C("4CC9F7", 0.75f), TextAlignmentOptions.Left, 3);
        Txt("title", _content, 86, 84, 900, 62, "시간에너지 전송", _kr, 48, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _subLabel = Txt("sub", _content, 88, 156, 900, 26, "기지 전송 컴퓨터     현재 구간 설원", _kr, 19, C("E8F2FB", 0.55f), TextAlignmentOptions.Left);

        // 우측 전송률 카드
        float cw = 250, cx = 1832 - cw, cy = 40;
        var card = Img("rateCard", _content, cx, cy, cw, 182, C("111A2C", 0.4f), UISpriteFactory.RoundedRect(48, 16));
        Outline(card.gameObject, C("4CC9F7", 0.25f));
        // 장식 링
        var rr = Img("rcRingSpin", card.transform, cw - 70 - 70, -70, 210, 210, C("4CC9F7", 0.16f), UISpriteFactory.Ring(210, 2f));
        rr.raycastTarget = false; _spinRings.Add((RectTransform)rr.transform);
        Img("rcRing2", card.transform, cw - 40 - 110, -40, 150, 150, C("4CC9F7", 0.10f), UISpriteFactory.Ring(150, 2f)).raycastTarget = false;

        Txt("rateLbl", card.transform, 18, 18, cw - 36, 16, "현재 전송률", _mono, 12, C("E8F2FB", 0.5f), TextAlignmentOptions.Right, 3);
        _rateBig = Txt("rateBig", card.transform, 18, 40, cw - 36 - 44, 100, "42", _mono, 96, Accent, TextAlignmentOptions.Right, 0, FontStyles.Bold);
        Txt("ratePct", card.transform, cw - 46, 74, 30, 44, "%", _mono, 40, C("4CC9F7", 0.6f), TextAlignmentOptions.Left, 0, FontStyles.Bold);
        // 하단 행
        Txt("rcBottom", card.transform, 18, 150, cw - 36 - 74, 20, "설원 구간", _mono, 12, C("E8F2FB", 0.5f), TextAlignmentOptions.Right);
        var goalChip = Img("goalChip", card.transform, cw - 18 - 66, 148, 66, 22, C("5BC7E8", 0f), UISpriteFactory.RoundedRect(40, 11));
        Outline(goalChip.gameObject, C("5BC7E8", 0.4f));
        Txt("goalTxt", goalChip.transform, 0, 0, 66, 22, "목표 50%", _mono, 12, AccentSoft2, TextAlignmentOptions.Center);
    }

    // ── C-2 진행 바 패널 ──────────────────────────────────────────────
    private void BuildProgress()
    {
        var panel = Panel("progressPanel", 88, 264, 1744, 240);
        // 헤더 행
        Txt("pHdr", panel.transform, 44, 20, 300, 18, "TRANSFER PROGRESS", _mono, 13, Accent, TextAlignmentOptions.Left, 3);
        Img("pLine", panel.transform, 240, 28, 1744 - 44 - 240 - 120, 1, C("4CC9F7", 0.25f)).raycastTarget = false;
        Txt("p100", panel.transform, 1744 - 44 - 110, 20, 110, 18, "0 - 100%", _mono, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Right);

        // 바 트랙
        float tx = 44, ty = 66, tw = 1744 - 88, th = 54; _trackW = tw;
        var track = TL(NewGO("track", panel.transform), tx, ty, tw, th);
        // 구간 배경 4등분
        for (int i = 0; i < 4; i++)
        {
            var seg = Img($"seg{i}", track.gameObject, i * tw / 4f, 0, tw / 4f, th,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.15f),
                UISpriteFactory.RoundedRect(24, i == 0 || i == 3 ? 12 : 2));
            seg.raycastTarget = false;
            if (i < 3) Img($"segDiv{i}", track.gameObject, (i + 1) * tw / 4f - 1, 0, 1, th, C("E8F2FB", 0.10f)).raycastTarget = false;
        }
        // 눈금 오버레이
        var ticks = Img("ticks", track.gameObject, 0, 0, tw, th, new Color(1, 1, 1, 0.7f), TickTile((int)(tw / 40f)));
        ticks.type = Image.Type.Tiled; ticks.color = C("E8F2FB", 0.07f); ticks.raycastTarget = false;

        // 채움 (마스크 + 그라데이션 이미지, fillAmount 로 클리핑)
        var fillGo = TL(NewGO("fill", track), 0, 0, tw, th);
        fillGo.gameObject.AddComponent<RectMask2D>();
        _fill = Img2(NewGO("fillImg", fillGo), HGrad());
        Stretch(_fill.gameObject); _fill.type = Image.Type.Filled; _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left; _fill.fillAmount = 0.42f; _fill.raycastTarget = false;
        // 스윕 하이라이트(E-1) — fill 마스크 내부에서 이동
        _sweepMaskRT = fillGo;
        _sweep = TL(NewGO("sweep", fillGo), 0, 0, 70, th);
        var sImg = _sweep.gameObject.AddComponent<Image>(); sImg.sprite = SweepTex(); sImg.color = Color.white; sImg.raycastTarget = false;

        // 진행 노드
        _node = NewRT("node", track.gameObject);
        _node.anchorMin = _node.anchorMax = new Vector2(0, 0.5f); _node.pivot = new Vector2(0.5f, 0.5f);
        _node.sizeDelta = new Vector2(20, th + 24); _node.anchoredPosition = new Vector2(tw * 0.42f, 0);
        var line = Img("nLine", _node.gameObject, 0, 0, 3, th + 24, AccentBright, UISpriteFactory.RoundedRect(8, 1));
        CenterIn(line, _node); line.raycastTarget = false;
        var dia = Img("nDia", _node.gameObject, 0, 0, 16, 16, Accent, UISpriteFactory.RoundedRect(16, 4));
        dia.rectTransform.anchorMin = dia.rectTransform.anchorMax = new Vector2(0.5f, 1f); dia.rectTransform.pivot = new Vector2(0.5f, 1f);
        dia.rectTransform.anchoredPosition = new Vector2(0, 9); dia.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); dia.raycastTarget = false;
        var pulse = Img("nPulse", dia.transform, 0, 0, 16, 16, C("4CC9F7", 0.5f), UISpriteFactory.RoundedRect(16, 4)); CenterIn(pulse, dia.rectTransform); pulse.raycastTarget = false;
        pulse.rectTransform.DOScale(2.0f, 1.0f).SetLoops(-1, LoopType.Restart).SetEase(Ease.OutQuad).SetUpdate(true);
        pulse.DOFade(0f, 1.0f).SetLoops(-1, LoopType.Restart).SetEase(Ease.OutQuad).SetUpdate(true);
        _nodeLabelRT = TL(NewGO("nLabelWrap", _node), 0, 0, 60, 24);
        _nodeLabelRT.anchorMin = _nodeLabelRT.anchorMax = new Vector2(0.5f, 0); _nodeLabelRT.pivot = new Vector2(0.5f, 1f);
        _nodeLabelRT.anchoredPosition = new Vector2(0, -16);
        var lblBg = _nodeLabelRT.gameObject.AddComponent<Image>(); lblBg.sprite = UISpriteFactory.RoundedRect(16, 8); lblBg.type = Image.Type.Sliced; lblBg.color = C("4CC9F7", 0.16f);
        Outline(_nodeLabelRT.gameObject, C("4CC9F7", 0.5f));
        _nodeLabel = Txt("nLbl", _nodeLabelRT.gameObject, 0, 0, 60, 24, "42%", _mono, 14, AccentBright, TextAlignmentOptions.Center, 0, FontStyles.Bold);
        Stretch(_nodeLabel.gameObject);

        // 마커 10개
        for (int p = 10; p <= 100; p += 10) BuildMarker(track.gameObject, p, tw, th);

        // 레전드
        float ly = ty + th + 46;
        for (int i = 0; i < 4; i++)
        {
            float lx = tx + i * tw / 4f + 4;
            Img($"lgDot{i}", panel.transform, lx, ly + 3, 8, 8, RegionCol[i], UISpriteFactory.Disc(16)).raycastTarget = false;
            int lo = i * 25, hi = (i + 1) * 25;
            Txt($"lg{i}", panel.transform, lx + 16, ly, 160, 18, $"{RegionKo[i]} {lo}-{hi}", _mono, 13,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.88f), TextAlignmentOptions.Left);
        }
    }

    private void BuildMarker(GameObject track, int pct, float tw, float th)
    {
        var mk = NewRT($"marker{pct}", track);
        mk.anchorMin = mk.anchorMax = new Vector2(0, 0.5f); mk.pivot = new Vector2(0.5f, 0.5f);
        mk.sizeDelta = new Vector2(34, 34); mk.anchoredPosition = new Vector2(tw * pct / 100f, th / 2f);

        var chip = Img("chip", mk.gameObject, 0, 0, 34, 34, C("0F1A2D"), UISpriteFactory.RoundedRect(34, 17));
        CenterIn(chip, mk);
        Outline(chip.gameObject, Accent);
        // 스템
        var stem = Img("stem", mk.gameObject, 0, 0, 1, 14, C("4CC9F7", 0.5f));
        stem.rectTransform.anchorMin = stem.rectTransform.anchorMax = new Vector2(0.5f, 0f); stem.rectTransform.pivot = new Vector2(0.5f, 1f);
        stem.rectTransform.anchoredPosition = new Vector2(0, 1); stem.raycastTarget = false;
        // 인터랙션(호버)
        var trg = mk.gameObject.AddComponent<Image>(); trg.color = new Color(0, 0, 0, 0); trg.raycastTarget = true;
        var hov = mk.gameObject.AddComponent<MarkerHover>(); hov.Init(this, pct, mk);

        _markers.Add(mk.gameObject);
        mk.gameObject.name = $"MK{pct}";  // 상태 갱신에서 찾기 쉽게
    }

    // ── C-4 본문 ──────────────────────────────────────────────────────
    private void BuildBody()
    {
        float by = 534, bh = 466, gap = 30;
        float leftW = (1744 - gap) * 1.5f / 2.5f, rightW = (1744 - gap) - leftW;

        // 좌: 보유 충전 키트
        var lp = Panel("kitPanel", 88, by, leftW, bh);
        PanelHeader(lp, leftW, "보유 충전 키트");
        var list = TL(NewGO("kitList", lp.transform), 16, 74, leftW - 32, bh - 90);
        var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 8; vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.UpperCenter;
        foreach (var k in _m.kits) _kitRows.Add(BuildKitRow(list.gameObject, k));

        // 우: 전송 제어
        var rp = Panel("ctrlPanel", 88 + leftW + gap, by, rightW, bh);
        PanelHeader(rp, rightW, "전송 제어");
        float ix = 24, iw = rightW - 48, iy = 74;
        var selCard = Card(rp.transform, ix, iy, iw, 84, C("E8F2FB", 0.04f), C("E8F2FB", 0.08f));
        Txt("selLbl", selCard.transform, 18, 14, iw - 36, 16, "선택된 키트", _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left, 1);
        _selName = Txt("selName", selCard.transform, 18, 32, iw - 36, 28, "없음", _kr, 22, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _selMeta = Txt("selMeta", selCard.transform, 18, 60, iw - 36, 20, "목록에서 키트를 클릭", _kr, 14, C("E8F2FB", 0.5f), TextAlignmentOptions.Left);

        iy += 84 + 18;
        var pvCard = Card(rp.transform, ix, iy, iw, 84, C("4CC9F7", 0.06f), C("4CC9F7", 0.2f));
        Txt("pvLbl", pvCard.transform, 18, 14, iw - 36, 16, "예상 전송률", _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left, 1);
        _previewVal = Txt("pvVal", pvCard.transform, 18, 36, iw - 36, 36, "키트를 선택하세요", _mono, 30, C("E8F2FB", 0.4f), TextAlignmentOptions.Left, 0, FontStyles.Bold);

        // 버튼 행 + TX LOG (패널 로컬 좌표: 아래에서부터 로그→버튼)
        float logY = bh - 16 - 100;
        float btnY = logY - 14 - 62;
        float sendW = iw - 130 - 14;
        var send = Img("sendBtn", rp.transform, ix, btnY, sendW, 62, C("47C4F0"), UISpriteFactory.RoundedRect(48, 12));
        _sendBtnImg = send; _sendBtn = send.gameObject.AddComponent<Button>(); _sendBtn.targetGraphic = send;
        _sendBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        _sendBtn.onClick.AddListener(OnSend); _sendBtnCg = send.gameObject.AddComponent<CanvasGroup>();
        Outline(send.gameObject, C("FFFFFF", 0.35f));
        var tri = Img("sendTri", send.gameObject, 0, 0, 15, 16, C("06202E"), TriTex());
        tri.rectTransform.anchorMin = tri.rectTransform.anchorMax = tri.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tri.rectTransform.anchoredPosition = new Vector2(-34, 0); tri.raycastTarget = false;
        Txt("sendTxt", send.gameObject, 0, 0, sendW, 62, "전송", _mono, 22, C("06202E"), TextAlignmentOptions.Center, 0, FontStyles.Bold)
            .rectTransform.anchoredPosition += new Vector2(12, 0);

        var close = Img("closeBtn", rp.transform, ix + sendW + 14, btnY, 130, 62, new Color(0, 0, 0, 0), UISpriteFactory.RoundedRect(48, 12));
        var cb = close.gameObject.AddComponent<Button>(); cb.targetGraphic = close; cb.navigation = new Navigation { mode = Navigation.Mode.None };
        cb.onClick.AddListener(Close); Outline(close.gameObject, C("E2EDF8", 0.25f));
        Txt("closeTxt", close.gameObject, 0, 0, 130, 62, "닫기 ESC", _mono, 18, C("E8F2FB", 0.6f), TextAlignmentOptions.Center);

        // TX LOG
        var logBox = Img("txLog", rp.transform, ix, logY, iw, 100, C("070C17", 0.7f), UISpriteFactory.RoundedRect(48, 12));
        Outline(logBox.gameObject, C("4CC9F7", 0.15f));
        var lgDot = Img("logDot", logBox.transform, 15, 13, 6, 6, Accent, UISpriteFactory.Disc(12));
        lgDot.DOFade(0.1f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        Txt("logHdr", logBox.transform, 28, 11, iw - 40, 14, "TX LOG", _mono, 11, C("4CC9F7", 0.6f), TextAlignmentOptions.Left, 2);
        _logText = Txt("logLines", logBox.transform, 15, 34, iw - 30, 60, "", _mono, 13, C("E8F2FB", 0.6f), TextAlignmentOptions.TopLeft);
    }

    private KitRow BuildKitRow(GameObject parent, Kit k)
    {
        var go = NewGO($"kit_{k.id}", parent.transform);
        go.AddComponent<LayoutElement>().minHeight = 66;
        var cg = go.AddComponent<CanvasGroup>();   // 생성 시 부착(런타임 GetComponent??Add 함정 회피)
        var bg = go.AddComponent<Image>(); bg.sprite = UISpriteFactory.RoundedRect(24, 12); bg.type = Image.Type.Sliced; bg.color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg; btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => OnKitClick(k));
        var outline = go.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(0, 0, 0, 0); outline.effectDistance = new Vector2(1, -1);

        // 아이콘 웰
        var well = Img("well", go, 20, 14, 38, 38, C("E8F2FB", 0.05f), UISpriteFactory.RoundedRect(24, 10));
        Outline(well.gameObject, C("E8F2FB", 0.1f));
        Img("wellSq", well.transform, 0, 0, 11, 11, RegionCol[(int)k.region], UISpriteFactory.RoundedRect(12, 3)).rectTransform.anchoredPosition = new Vector2(13, -13);
        // 이름/메타
        var name = Txt("kName", go, 74, 12, 400, 26, k.name, _kr, 20, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        var meta = Txt("kMeta", go, 74, 40, 500, 18, KitMeta(k), _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left);
        // 수량/상승률 (우측)
        var qty = Txt("kQty", go, 0, 0, 60, 30, $"x{k.qty}", _mono, 18, C("E8F2FB", 0.7f), TextAlignmentOptions.Center);
        qty.rectTransform.anchorMin = qty.rectTransform.anchorMax = new Vector2(1, 0.5f); qty.rectTransform.pivot = new Vector2(1, 0.5f); qty.rectTransform.anchoredPosition = new Vector2(-96, 0);
        var gain = Txt("kGain", go, 0, 0, 74, 30, $"+{k.gain}%", _mono, 18, Accent, TextAlignmentOptions.Right, 0, FontStyles.Bold);
        gain.rectTransform.anchorMin = gain.rectTransform.anchorMax = new Vector2(1, 0.5f); gain.rectTransform.pivot = new Vector2(1, 0.5f); gain.rectTransform.anchoredPosition = new Vector2(-14, 0);

        return new KitRow { kit = k, go = go, bg = bg, outline = outline, cg = cg, name = name, meta = meta, qty = qty, gain = gain, well = well };
    }

    private void BuildFooter()
    {
        _statusLine = Txt("status", _content, 88, 1010, 1200, 22, "", _mono, 14, C("E8F2FB", 0.5f), TextAlignmentOptions.Left);
        var cursor = Img("cursor", _content, 88 + 470, 1010, 9, 16, Accent, UISpriteFactory.RoundedRect(8, 2));
        cursor.DOFade(0f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Linear).SetUpdate(true);
    }

    private void KeyHint(float x, float y, float w, string t)
    {
        var h = Img("hint", _content, x, y, w, 26, new Color(0, 0, 0, 0), UISpriteFactory.RoundedRect(16, 7));
        Outline(h.gameObject, C("E2EDF8", 0.2f));
        Txt("hintTxt", h.transform, 0, 0, w, 26, t, _mono, 13, C("E8F2FB", 0.6f), TextAlignmentOptions.Center);
    }

    private void BuildTooltip()
    {
        _tooltip = NewGO("tooltip", _content.transform);
        var rt = _tooltip.GetComponent<RectTransform>() ?? _tooltip.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(264, 96);
        _ttBox = _tooltip.AddComponent<Image>(); _ttBox.sprite = UISpriteFactory.RoundedRect(48, 12); _ttBox.type = Image.Type.Sliced; _ttBox.color = C("101A2D", 0.98f);
        _ttBox.raycastTarget = false; Outline(_tooltip, Accent);
        _ttTitle = Txt("ttT", _tooltip, 17, 14, 230, 16, "", _mono, 12, Accent, TextAlignmentOptions.Left, 2);
        _ttName = Txt("ttN", _tooltip, 17, 33, 230, 24, "", _kr, 17, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _ttState = Txt("ttS", _tooltip, 17, 62, 230, 20, "", _kr, 13, C("E8F2FB", 0.6f), TextAlignmentOptions.Left);
        _tooltip.SetActive(false);
    }

    private void BuildOverlay()
    {
        var scan = Img("scan", _content, 0, 0, 1920, 1080, C("78C8FF", 0.03f), ScanTile());
        scan.type = Image.Type.Tiled; scan.raycastTarget = false;
        var vig = Img("vignette", _content, 0, 0, 1920, 1080, Color.white, VignetteTex());
        vig.raycastTarget = false;
    }

    // =====================================================================
    // 갱신 / 인터랙션
    // =====================================================================
    private void RefreshAll()
    {
        RefreshMarkers();
        RefreshKitRows();
        RefreshSelection();
        RefreshStatus();
        RefreshLog();
    }

    private void RefreshMarkers()
    {
        foreach (var mk in _markers)
        {
            if (mk == null) continue;
            int pct = int.Parse(mk.name.Substring(2));
            var st = _m.MarkerState(pct);
            Color col = st == MState.Done ? Success : st == MState.Next ? Accent : C("E2EDF8", 0.25f);
            var chip = mk.transform.Find("chip")?.GetComponent<Image>();
            if (chip != null)
            {
                var ol = chip.GetComponent<UnityEngine.UI.Outline>(); if (ol != null) ol.effectColor = col;
            }
            var stem = mk.transform.Find("stem")?.GetComponent<Image>(); if (stem != null) stem.color = new Color(col.r, col.g, col.b, 0.5f);
            // 아이콘: done=체크 / next=설계도 / locked="?"
            var iconHolder = mk.transform.Find("iconHolder");
            if (iconHolder != null) Destroy(iconHolder.gameObject);
            var ih = NewRT("iconHolder", mk); CenterIn2(ih, (RectTransform)mk.transform); ih.sizeDelta = new Vector2(18, 18);
            if (st == MState.Done) BuildCheck(ih.gameObject, col);
            else if (st == MState.Next) BuildDoc(ih.gameObject, col);
            else Txt("q", ih.gameObject, 0, 0, 18, 18, "?", _mono, 14, col, TextAlignmentOptions.Center);
        }
    }

    private void RefreshKitRows()
    {
        foreach (var r in _kitRows)
        {
            bool usable = _m.Usable(r.kit);
            bool sel = _m.selectedId == r.kit.id;
            r.bg.color = sel ? C("4CC9F7", 0.10f) : new Color(0, 0, 0, 0);
            r.outline.effectColor = sel ? Accent : new Color(0, 0, 0, 0);
            r.name.color = usable ? TextBright : C("E8F2FB", 0.35f);
            r.meta.text = KitMeta(r.kit); r.meta.color = usable ? C("E8F2FB", 0.45f) : C("E8F2FB", 0.35f);
            r.qty.text = $"x{r.kit.qty}";
            r.cg.alpha = usable ? 1f : 0.4f; r.cg.blocksRaycasts = usable; r.cg.interactable = usable;
        }
    }

    private void RefreshSelection()
    {
        var k = _m.Selected();
        _selName.text = k != null ? k.name : "없음";
        _selMeta.text = k != null ? KitMeta(k) : "목록에서 키트를 클릭";
        // 예상 전송률
        string pv; Color pc; bool active;
        if (k == null) { pv = "키트를 선택하세요"; pc = C("E8F2FB", 0.4f); active = false; }
        else if (!_m.Usable(k)) { pv = "전송 불가"; pc = C("E8F2FB", 0.4f); active = false; }
        else { int t = _m.Target(k); int d = t - Mathf.RoundToInt(_m.progress); pv = $"전송하면 {t}% (+{d})"; pc = Accent; active = d > 0; }
        _previewVal.text = pv; _previewVal.color = pc;
        _sendBtnImg.color = active ? C("47C4F0") : C("47C4F0", 0.4f);
        _sendBtnCg.alpha = active ? 1f : 0.5f; _sendBtnCg.blocksRaycasts = active; _sendBtnCg.interactable = active;
    }

    private void RefreshStatus() => _statusLine.text = _m.StatusText();
    private void RefreshLog()
    {
        var sb = new System.Text.StringBuilder();
        int start = Mathf.Max(0, _m.logs.Count - 4);
        for (int i = start; i < _m.logs.Count; i++) sb.AppendLine("> " + _m.logs[i]);
        _logText.text = sb.ToString().TrimEnd();
    }

    private void OnKitClick(Kit k)
    {
        if (!_m.Usable(k)) return;
        _m.selectedId = _m.selectedId == k.id ? null : k.id;
        RefreshKitRows(); RefreshSelection();
    }

    private void OnSend()
    {
        var k = _m.Selected(); if (k == null || !_m.Usable(k)) return;
        float from = _m.progress;
        bool cross = _m.Send(k, out int to, out bool broke);
        if (!cross) return;
        SetGauge(to, true, from);
        RefreshKitRows(); RefreshSelection(); RefreshStatus(); RefreshLog();
        DOVirtual.DelayedCall(0.9f, RefreshMarkers).SetUpdate(true);
    }

    // ── E-6 게이지 이동 ───────────────────────────────────────────────
    private void SetGauge(float to, bool animated, float from = -1)
    {
        if (from < 0) from = to;
        float tw = _trackW;
        if (!animated)
        {
            _fill.fillAmount = to / 100f;
            _node.anchoredPosition = new Vector2(tw * to / 100f, 0);
            _nodeLabel.text = Mathf.RoundToInt(to) + "%"; _rateBig.text = Mathf.RoundToInt(to).ToString();
            RunSweep();
            return;
        }
        _fill.DOKill(); _node.DOKill();
        _fill.DOFillAmount(to / 100f, 0.9f).SetEase(Ease.OutQuint).SetUpdate(true);
        _node.DOAnchorPosX(tw * to / 100f, 0.9f).SetEase(Ease.OutQuint).SetUpdate(true);
        DOTween.To(() => from, v => { _nodeLabel.text = Mathf.RoundToInt(v) + "%"; _rateBig.text = Mathf.RoundToInt(v).ToString(); }, to, 0.9f)
            .SetEase(Ease.OutQuint).SetUpdate(true);
        RunSweep();
    }

    private void RunSweep()
    {
        if (_sweep == null) return;
        _sweep.DOKill();
        _sweep.anchoredPosition = new Vector2(-90, 0);
        _sweep.DOAnchorPosX(_trackW + 90, 3.4f).SetLoops(-1, LoopType.Restart).SetEase(Ease.InOutSine).SetUpdate(true);
    }

    // ── 애니메이션 라이프사이클 ───────────────────────────────────────
    private void PlayOpenAnim()
    {
        _cg.DOKill(); _content.DOKill();
        _cg.alpha = 0f; _cg.DOFade(1f, 0.18f).SetUpdate(true);
        _content.localScale = Vector3.one * 0.98f; _content.DOScale(1f, 0.22f).SetEase(Ease.OutSine).SetUpdate(true);
    }
    private void KillAll() { if (_cg != null) _cg.DOKill(); if (_content != null) _content.DOKill(); if (_fill != null) _fill.DOKill(); if (_node != null) _node.DOKill(); }

    // ── 툴팁 (마커 호버 콜백) ─────────────────────────────────────────
    public void ShowTooltip(int pct, RectTransform marker)
    {
        var st = _m.MarkerState(pct);
        Color col = st == MState.Done ? Success : st == MState.Next ? Accent : C("E2EDF8", 0.35f);
        _ttBox.color = C("101A2D", 0.98f); var ol = _tooltip.GetComponent<UnityEngine.UI.Outline>(); if (ol != null) ol.effectColor = col;
        _ttTitle.color = col; _ttTitle.text = $"{pct}% 지점 보상";
        _ttName.text = _m.RewardName(pct);
        _ttState.color = col; _ttState.text = _m.TooltipStatus(pct, st);
        var rt = (RectTransform)_tooltip.transform;
        rt.anchoredPosition = ContentPointFromMarker(marker);
        _tooltip.transform.SetAsLastSibling();
        _tooltip.SetActive(true);
    }
    public void HideTooltip() { if (_tooltip != null) _tooltip.SetActive(false); }

    private Vector2 ContentPointFromMarker(RectTransform marker)
    {
        // content(top-left pivot) 기준 마커 중심 위 46px
        Vector3 w = marker.TransformPoint(Vector3.zero);
        Vector3 l = _content.InverseTransformPoint(w);
        return new Vector2(l.x, l.y + 46);
    }

    // =====================================================================
    // 모델 (스펙 D)
    // =====================================================================
    private enum MState { Done, Next, Locked }
    private enum Grade { Normal, Elite, Boss }
    private class Kit { public string id, name; public TransmissionRegion region; public Grade grade; public int gain, qty; }

    private class Model
    {
        public float progress = 42;
        public string selectedId;
        public readonly List<Kit> kits = new()
        {
            new Kit{ id="snN", name="설원 일반 충전키트", region=TransmissionRegion.Snow, grade=Grade.Normal, gain=3, qty=4 },
            new Kit{ id="snE", name="설원 정예 충전키트", region=TransmissionRegion.Snow, grade=Grade.Elite,  gain=5, qty=2 },
            new Kit{ id="snB", name="설원 보스 충전키트", region=TransmissionRegion.Snow, grade=Grade.Boss,   gain=8, qty=1 },
            new Kit{ id="naN", name="자연 일반 충전키트", region=TransmissionRegion.Nature, grade=Grade.Normal, gain=3, qty=2 },
        };
        public readonly List<string> logs = new() { "UPLINK 연결됨 / 구간: 설원" };

        public TransmissionRegion Cur => progress >= 100 ? TransmissionRegion.Lava : (TransmissionRegion)Mathf.Clamp((int)(progress / 25), 0, 3);
        public int Cap => ((int)Cur + 1) * 25;

        public Kit Selected() { foreach (var k in kits) if (k.id == selectedId) return k; return null; }

        public bool Usable(Kit k) => k.qty > 0 && k.region == Cur && progress < Cap;
        public int Target(Kit k) => Mathf.Min(Mathf.RoundToInt(progress) + k.gain, Cap);

        public bool Send(Kit k, out int to, out bool broke)
        {
            to = Target(k); broke = false;
            if (to <= progress || !Usable(k)) return false;
            int delta = to - Mathf.RoundToInt(progress);
            progress = to; k.qty -= 1;
            logs.Add($"{k.name} x1 전송 / +{delta}% -> {to}%");
            if (to == Cap) { broke = true; logs.Add($"{to}% 보상 획득 / {NextRegionKo()} 구간 해금"); }
            selectedId = null;
            return true;
        }
        string NextRegionKo() { int n = Mathf.Clamp((int)Cur + 1, 0, 3); return RegionKo[n]; }

        public MState MarkerState(int pct)
        {
            if (progress >= pct) return MState.Done;
            int next = ((int)(progress / 10) + 1) * 10;   // progress 초과 첫 10% 지점
            return pct == next ? MState.Next : MState.Locked;
        }

        public string RewardName(int pct) => pct switch
        {
            10 => "설비 설계도 I", 20 => "설비 설계도 II", 30 => "설비 설계도 III",
            40 => "설비 설계도 IV", 50 => "설비 설계도 V", 60 => "설비 설계도 VI",
            70 => "영구 귀환석", 80 => "우주선 부품 A", 90 => "우주선 부품 B",
            _ => "최종 보상 - 엔딩"
        };

        public string TooltipStatus(int pct, MState st)
        {
            if (st == MState.Done) return "획득 완료";
            if (st == MState.Next)
            {
                bool boundary = pct % 25 == 0; // 25/50/75/100
                string grade = boundary ? "보스" : "일반";
                int n = boundary ? 1 : 2;
                return $"필요: {RegionKo[(int)Cur]} {grade} 충전키트 x{n}";
            }
            return "??? (도달 시 공개)";
        }

        public string StatusText()
        {
            int cap = Cap; int p = Mathf.RoundToInt(progress);
            if (p < cap) return $"현재 구간 {RegionKo[(int)Cur]} / 일반 상한 {cap}% / 목표 {cap}%";
            return $"{cap}% 도달 / 다음 구간 진행";
        }
    }

    private string KitMeta(Kit k)
    {
        string g = k.grade == Grade.Normal ? "일반" : k.grade == Grade.Elite ? "정예" : "보스";
        string meta = $"{RegionKo[(int)k.region]} 지역 / {g} 등급";
        if (k.region != _m.Cur) meta += "  <color=#F27059>다른 지역 / 이 구간 사용 불가</color>";
        else if (k.qty <= 0) meta += "  <color=#F27059>수량 없음</color>";
        return meta;
    }

    private class KitRow { public Kit kit; public GameObject go; public Image bg, well; public UnityEngine.UI.Outline outline; public CanvasGroup cg; public TMP_Text name, meta, qty, gain; }

    // 마커 호버 이벤트 컴포넌트
    private class MarkerHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        TransmissionComputerUI _ui; int _pct; RectTransform _rt;
        public void Init(TransmissionComputerUI ui, int pct, RectTransform rt) { _ui = ui; _pct = pct; _rt = rt; }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => _ui.ShowTooltip(_pct, _rt);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => _ui.HideTooltip();
    }

    // =====================================================================
    // 헬퍼 (레이아웃/그래픽)
    // =====================================================================
    private GameObject NewGO(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return g; }
    private RectTransform NewRT(string n, GameObject p) { var g = NewGO(n, p.transform); return g.GetComponent<RectTransform>(); }

    private RectTransform TL(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
    }
    private void Stretch(GameObject go) { var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private void CenterIn(Image img, RectTransform parent) { var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }
    private void CenterIn2(RectTransform rt, RectTransform parent) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }

    private Image Img(string n, Transform p, float x, float y, float w, float h, Color col, Sprite spr = null)
    {
        var go = NewGO(n, p); TL(go, x, y, w, h);
        var im = go.AddComponent<Image>(); im.color = col; if (spr != null) { im.sprite = spr; im.type = Image.Type.Sliced; }
        return im;
    }
    private Image Img(string n, GameObject p, float x, float y, float w, float h, Color col, Sprite spr = null) => Img(n, p.transform, x, y, w, h, col, spr);
    private Image Img2(GameObject go, Sprite spr) { var im = go.AddComponent<Image>(); im.sprite = spr; im.color = Color.white; return im; }

    private TMP_Text Txt(string n, Transform p, float x, float y, float w, float h, string t, TMP_FontAsset f, float size, Color col, TextAlignmentOptions al, float spacing = 0, FontStyles style = FontStyles.Normal)
    {
        var go = NewGO(n, p); TL(go, x, y, w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (f != null) tmp.font = f;
        tmp.text = t; tmp.fontSize = size; tmp.color = col; tmp.alignment = al; tmp.characterSpacing = spacing; tmp.fontStyle = style;
        tmp.raycastTarget = false; tmp.richText = true; tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
    private TMP_Text Txt(string n, GameObject p, float x, float y, float w, float h, string t, TMP_FontAsset f, float size, Color col, TextAlignmentOptions al, float spacing = 0, FontStyles style = FontStyles.Normal)
        => Txt(n, p.transform, x, y, w, h, t, f, size, col, al, spacing, style);

    private RectTransform Panel(string n, float x, float y, float w, float h)
    {
        var rt = TL(NewGO(n, _content.transform), x, y, w, h);
        var shadow = Img("pShadow", rt.gameObject, -2, 10, w + 4, h + 8, C("000000", 0.35f), UISpriteFactory.RoundedRect(48, 18)); shadow.raycastTarget = false;
        var body = Img("pBody", rt.gameObject, 0, 0, w, h, C("111A2C", 0.95f), UISpriteFactory.RoundedRect(48, 16));
        Outline(body.gameObject, C("4CC9F7", 0.22f));
        return rt;
    }
    private void PanelHeader(RectTransform panel, float w, string title)
    {
        var hb = Img("hdrBar", panel.gameObject, 0, 0, w, 54, C("4CC9F7", 0.06f), UISpriteFactory.RoundedRect(48, 16));
        Img("hdrLine", panel.gameObject, 0, 53, w, 1, C("4CC9F7", 0.18f)).raycastTarget = false;
        Img("hdrTick", panel.gameObject, 22, 18, 4, 18, Accent, UISpriteFactory.RoundedRect(8, 2)).raycastTarget = false;
        Txt("hdrTxt", panel.gameObject, 40, 18, 300, 18, title, _mono, 15, AccentSoft, TextAlignmentOptions.Left, 2, FontStyles.Bold);
    }
    private RectTransform Card(Transform p, float x, float y, float w, float h, Color bg, Color border)
    {
        var rt = TL(NewGO("card", p), x, y, w, h);
        var im = rt.gameObject.AddComponent<Image>(); im.sprite = UISpriteFactory.RoundedRect(48, 12); im.type = Image.Type.Sliced; im.color = bg;
        Outline(rt.gameObject, border);
        return rt;
    }
    private void Outline(GameObject go, Color col) { var o = go.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = col; o.effectDistance = new Vector2(1, -1); }

    // 체크 표시 (두 막대)
    private void BuildCheck(GameObject parent, Color col)
    {
        var a = Img("ck1", parent, 0, 0, 6, 2.4f, col, UISpriteFactory.RoundedRect(8, 1));
        a.rectTransform.anchorMin = a.rectTransform.anchorMax = a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        a.rectTransform.anchoredPosition = new Vector2(-3.5f, -2.5f); a.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); a.raycastTarget = false;
        var b = Img("ck2", parent, 0, 0, 11, 2.4f, col, UISpriteFactory.RoundedRect(8, 1));
        b.rectTransform.anchorMin = b.rectTransform.anchorMax = b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        b.rectTransform.anchoredPosition = new Vector2(2.5f, 1f); b.rectTransform.localRotation = Quaternion.Euler(0, 0, -45); b.raycastTarget = false;
    }
    // 설계도(문서) 아이콘 = 3줄 막대
    private void BuildDoc(GameObject parent, Color col)
    {
        for (int i = 0; i < 3; i++)
            Img($"dl{i}", parent, 0, 0, 12, 1.8f, col, UISpriteFactory.RoundedRect(8, 1))
                .rectTransform.anchoredPosition = new Vector2(0, 4 - i * 4);
        foreach (Transform t in parent.transform) { var rt = (RectTransform)t; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); }
    }

    // ── 폰트 ──────────────────────────────────────────────────────────
    private TMP_FontAsset ResolveFont(TMP_FontAsset given, params string[] names)
    {
        if (given != null) return given;
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var f = FindLoaded(n); if (f != null) return f;
            var r = Resources.Load<TMP_FontAsset>("Font/" + n); if (r != null) return r;
        }
        var any = FindAnyObjectByType<TextMeshProUGUI>(); if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }
    private static TMP_FontAsset FindLoaded(string namePart)
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0) return f;
        return null;
    }

    // ── 절차 텍스처 ───────────────────────────────────────────────────
    private static Sprite _hgrad, _radial, _grid, _scan, _tick, _tri, _sweep2, _vig;
    private static Sprite HGrad()
    {
        if (_hgrad != null) return _hgrad;
        int w = 256; var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] stops = { C("43B06C"), C("5BC7E8"), C("D9A44A"), C("E0593A") };
        var px = new Color[w * 4];
        for (int x = 0; x < w; x++)
        {
            float f = x / (float)(w - 1) * 3f; int i = Mathf.Clamp((int)f, 0, 2); Color c = Color.Lerp(stops[i], stops[i + 1], f - i);
            for (int y = 0; y < 4; y++) px[y * w + x] = c;
        }
        tex.SetPixels(px); tex.Apply(); _hgrad = Sprite.Create(tex, new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f), 100f); return _hgrad;
    }
    private static Sprite RadialTex()
    {
        if (_radial != null) return _radial;
        int s = 256; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color c0 = C("101C33"), c1 = C("0A1120"), c2 = C("070C17");
        Vector2 ctr = new Vector2(0.30f, 0.90f) * s;
        float maxd = s * 1.2f; var px = new Color[s * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), ctr) / maxd);
            px[y * s + x] = d < 0.55f ? Color.Lerp(c0, c1, d / 0.55f) : Color.Lerp(c1, c2, (d - 0.55f) / 0.45f);
        }
        tex.SetPixels(px); tex.Apply(); _radial = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); return _radial;
    }
    private static Sprite GridTile()
    {
        if (_grid != null) return _grid;
        int s = 56; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) px[y * s + x] = (x == 0 || y == 0) ? Color.white : new Color(1, 1, 1, 0);
        tex.SetPixels(px); tex.Apply(); _grid = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); return _grid;
    }
    private static Sprite ScanTile()
    {
        if (_scan != null) return _scan;
        int s = 4; var tex = new Texture2D(4, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color[4 * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < 4; x++) px[y * 4 + x] = (y == 0) ? Color.white : new Color(1, 1, 1, 0);
        tex.SetPixels(px); tex.Apply(); _scan = Sprite.Create(tex, new Rect(0, 0, 4, s), new Vector2(0.5f, 0.5f), 100f); return _scan;
    }
    private static Sprite TickTile(int cellW)
    {
        if (_tick != null) return _tick; int w = 40, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = (x == 0) ? Color.white : new Color(1, 1, 1, 0);
        tex.SetPixels(px); tex.Apply(); _tick = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f); return _tick;
    }
    private static Sprite TriTex()
    {
        if (_tri != null) return _tri; int s = 32; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        { float ty = Mathf.Abs(y - s / 2f) / (s / 2f); px[y * s + x] = (x / (float)s <= 1f - ty) ? Color.white : new Color(1, 1, 1, 0); }
        tex.SetPixels(px); tex.Apply(); _tri = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); return _tri;
    }
    private static Sprite SweepTex()
    {
        if (_sweep2 != null) return _sweep2; int w = 64; var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * 4];
        for (int x = 0; x < w; x++) { float f = x / (float)(w - 1); float a = Mathf.Sin(f * Mathf.PI) * 0.35f; for (int y = 0; y < 4; y++) px[y * w + x] = new Color(1, 1, 1, a); }
        tex.SetPixels(px); tex.Apply(); _sweep2 = Sprite.Create(tex, new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f), 100f); return _sweep2;
    }
    private static Sprite VignetteTex()
    {
        if (_vig != null) return _vig; int s = 128; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s]; Vector2 c = new Vector2(s / 2f, s / 2f);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        { float d = Vector2.Distance(new Vector2(x, y), c) / (s / 2f); float a = Mathf.Clamp01((d - 0.6f) / 0.4f) * 0.4f; px[y * s + x] = new Color(0, 0, 0, a); }
        tex.SetPixels(px); tex.Apply(); _vig = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); return _vig;
    }

    private static Color C(string hex, float a = 1f)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) { c.a = a; return c; }
        return Color.white;
    }
}
