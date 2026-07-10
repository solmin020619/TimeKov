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
    private readonly TMP_Text[] _legendLabels = new TMP_Text[4];   // 구간 레전드 라벨(도달 시 공개)
    private readonly Image[] _legendDots = new Image[4];
    private readonly List<KitRow> _kitRows = new();
    private GameObject _kitListRoot;      // 키트 행이 들어가는 컨테이너(동적 재구성 대상)
    private GameObject _kitEmptyLabel;    // 보유 키트 0개일 때 안내 라벨
    private int _shownRate;               // 현재 게이지에 표시 중인 전송률(애니 from 기준)
    private bool _mgrSubscribed;          // TransmissionManager 이벤트 구독 여부
    private GameObject _tooltip; private TMP_Text _ttTitle, _ttName, _ttState; private Image _ttBox;
    private TMP_Text _logText;
    private RectTransform _cursor;   // 상태 텍스트 끝을 따라가는 깜빡이 커서
    private readonly List<CanvasGroup> _bootPanels = new();   // 열릴 때 순차로 펼쳐질 패널들

    // ── 리워드 리빌(지점 도달 연출) ──────────────────────────────────
    private GameObject _reward; private CanvasGroup _rewardCg; private RectTransform _rewardCard;
    private TMP_Text _rewardTitle, _rewardName, _rewardDesc; private Image _rewardBurst, _rewardBar, _rewardEmblem;
    private UnityEngine.UI.Outline _rewardOutline; private readonly List<RectTransform> _rewardRings = new();
    private Sequence _rewardSeq;
    private struct Reveal { public string title, name, desc; public Color color; public int markerPct; }
    private readonly Queue<Reveal> _revealQ = new();
    private bool _revealBusy;

    private Model _m;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _kr = ResolveFont(krFont, "Pretendard-SemiBold", "Pretendard", "남양주", "GabiaMaeumgyeol");
        _mono = ResolveFont(monoFont, "JetBrains", "Rajdhani-SemiBold", "Rajdhani", null) ?? _kr;
        Debug.Log($"[TransmissionUI] 폰트 → 한글: {(_kr != null ? _kr.name : "null")} / 영숫자: {(_mono != null ? _mono.name : "null")}  (krFont지정={(krFont != null ? krFont.name : "none")}, monoFont지정={(monoFont != null ? monoFont.name : "none")})");
        _m = new Model();
        EnsureManager();          // 씬에 매니저 없으면 런타임 생성
        SubscribeManager();       // 전송률/보상/해금 이벤트 구독(닫혀있어도 _shownRate 동기화)
        _shownRate = Mathf.RoundToInt(_m.progress);
        Build();
        _root.SetActive(false);
    }

    public static TransmissionComputerUI GetOrCreate()
        => Instance != null ? Instance : new GameObject("TransmissionComputerUI").AddComponent<TransmissionComputerUI>();

    private void OnDestroy()
    {
        UnsubscribeManager();
        if (Instance == this) Instance = null;
        // _root 는 메인 캔버스 아래에 붙어 있어 이 컴포넌트와 수명이 분리될 수 있으므로 함께 정리(고아 방지).
        if (_root != null) Destroy(_root);
    }

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
        EnsureManager();                      // 다른 씬 등에서 매니저가 없으면 보장
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();   // 메인 캔버스 내 형제들 위(맨 앞)로
        _openedFrame = Time.frameCount;
        _m.selectedId = null;
        _shownRate = Mathf.RoundToInt(_m.progress);
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
        // 기존 메인 Canvas 안에 들어가도록 그 아래에 붙인다(코어강화 UI 등과 동일 관례).
        // 절대좌표는 1920×1080 기준 — 메인 캔버스도 동일 기준이라 그대로 맞는다.
        Transform uiParent = ResolveUIParent();
        _root = NewGO("TransmissionComputerUI_Root", uiParent); Stretch(_root);
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
        BuildRewardOverlay();
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
        RegisterBootPanel(card.rectTransform);   // 순차 오픈 애니 첫 번째 대상
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
        // 바 본체 — 바깥 4모서리만 라운드(마스크). 안쪽 구간/눈금/채움은 전부 각지게.
        // (구간마다 RoundedRect 를 쓰면 용암 세그먼트 안쪽 모서리까지 둥글어져 부자연스러웠음 → 마스크로 통일)
        var body = TL(NewGO("barBody", track), 0, 0, tw, th);
        var bodyImg = body.gameObject.AddComponent<Image>();
        bodyImg.sprite = UISpriteFactory.RoundedRect(48, 12); bodyImg.type = Image.Type.Sliced; bodyImg.raycastTarget = false;
        var bodyMask = body.gameObject.AddComponent<Mask>(); bodyMask.showMaskGraphic = false;

        // 구간 배경 4등분 (전부 각진 사각 — 바깥 라운드는 body 마스크가 처리)
        for (int i = 0; i < 4; i++)
        {
            var seg = Img($"seg{i}", body.gameObject, i * tw / 4f, 0, tw / 4f, th,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.15f));
            seg.raycastTarget = false;
        }
        // 세로 눈금선 — 정확히 10% 지점마다(마커 위치와 1:1 일치). 10~90% 내부 라인.
        for (int p = 10; p <= 90; p += 10)
            Img($"tick{p}", body.gameObject, tw * p / 100f - 0.5f, 0, 1, th, C("E8F2FB", 0.10f)).raycastTarget = false;

        // 채움 (마스크 + 그라데이션 이미지, fillAmount 로 클리핑). body 마스크 안이라 왼쪽 끝도 라운드로 잘림.
        var fillGo = TL(NewGO("fill", body), 0, 0, tw, th);
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
        // pivot 을 다이아 중심(0.5,0.5)으로 — top-center 로 두면 45° 회전 시 시각 중심이 오른쪽으로 밀려 선과 어긋난다.
        dia.rectTransform.anchorMin = dia.rectTransform.anchorMax = new Vector2(0.5f, 1f); dia.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dia.rectTransform.anchoredPosition = new Vector2(0, 2); dia.rectTransform.localRotation = Quaternion.Euler(0, 0, 45); dia.raycastTarget = false;
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

        // 레전드 — 라벨/도트는 진행 도달 여부에 따라 RefreshLegend()에서 공개/??? 처리.
        float ly = ty + th + 46;
        for (int i = 0; i < 4; i++)
        {
            float lx = tx + i * tw / 4f + 4;
            _legendDots[i] = Img($"lgDot{i}", panel.transform, lx, ly + 3, 8, 8, RegionCol[i], UISpriteFactory.Disc(16));
            _legendDots[i].raycastTarget = false;
            _legendLabels[i] = Txt($"lg{i}", panel.transform, lx + 16, ly, 160, 18, "???", _mono, 13,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.88f), TextAlignmentOptions.Left);
        }
    }

    // 각 구간 라벨은 해당 구간에 도달(전송률 ≥ 구간 시작 %)해야 공개. 그 전엔 ??? + 흐리게.
    private void RefreshLegend()
    {
        int rate = _m != null ? Mathf.RoundToInt(_m.progress) : 0;
        for (int i = 0; i < 4; i++)
        {
            if (_legendLabels[i] == null) continue;
            bool reached = rate >= i * 25;
            if (reached)
            {
                _legendLabels[i].text = $"{RegionKo[i]} {i * 25}-{(i + 1) * 25}";
                _legendLabels[i].color = new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.88f);
                if (_legendDots[i] != null) _legendDots[i].color = RegionCol[i];
            }
            else
            {
                _legendLabels[i].text = "???";
                _legendLabels[i].color = C("E2EDF8", 0.28f);
                if (_legendDots[i] != null) _legendDots[i].color = C("E2EDF8", 0.2f);
            }
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
        _kitListRoot = list.gameObject;   // 실제 키트 행은 Open()의 RebuildKitRows()에서 인벤토리 기준으로 채움

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

        var close = Img("closeBtn", rp.transform, ix + sendW + 14, btnY, 130, 62, C("E8F2FB", 0.07f), UISpriteFactory.RoundedRect(48, 12));
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
        // 커서 높이 18, 상태 텍스트(y=1010, h=22, 세로 중앙) 중심(=1021)에 맞춰 y=1012 배치.
        var cursor = Img("cursor", _content, 88 + 470, 1012, 9, 18, Accent, UISpriteFactory.RoundedRect(8, 2));
        cursor.raycastTarget = false; _cursor = cursor.rectTransform;   // 상태 텍스트 끝으로 따라오도록 참조 저장
        cursor.DOFade(0f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Linear).SetUpdate(true);
    }

    // 상태 텍스트 실제 폭을 재서 커서를 그 끝 바로 옆에 배치.
    private void PositionCursor()
    {
        if (_cursor == null || _statusLine == null) return;
        float w = _statusLine.GetPreferredValues(_statusLine.text).x;
        _cursor.anchoredPosition = new Vector2(88 + w + 10, _cursor.anchoredPosition.y);
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

    // ── 리워드 리빌 오버레이(지점 도달 연출용, 평소 비활성) ────────────
    private void BuildRewardOverlay()
    {
        _reward = TL(NewGO("RewardReveal", _content.transform), 0, 0, 1920, 1080).gameObject;
        _rewardCg = _reward.AddComponent<CanvasGroup>();

        // 스크림 — 배경을 어둡게 + 클릭 시 스킵
        var scrim = Img("rwScrim", _reward, 0, 0, 1920, 1080, C("040810", 0.62f));
        scrim.raycastTarget = true;
        var sbtn = scrim.gameObject.AddComponent<Button>();
        sbtn.transition = Selectable.Transition.None; sbtn.navigation = new Navigation { mode = Navigation.Mode.None };
        sbtn.onClick.AddListener(SkipReveal);

        // 중앙 빛폭발 + 확장 링(카드 뒤)
        _rewardBurst = CenteredImg("rwBurst", _reward.transform, 0, 0, 360, 360, new Color(0, 0, 0, 0), UISpriteFactory.Disc(256));
        for (int i = 0; i < 2; i++)
        {
            var ring = CenteredImg($"rwRing{i}", _reward.transform, 0, 0, 120, 120, new Color(0, 0, 0, 0), UISpriteFactory.Ring(256, 3f));
            _rewardRings.Add(ring.rectTransform);
        }

        // 카드(중앙, 640×300 → TL 640,390). pivot 을 중앙으로 바꿔 스케일이 중앙에서 퍼지게 한다
        // (자식들은 좌상단 앵커라 위치 영향 없음). anchoredPosition 은 중앙점(960,-540)으로 보정.
        var card = TL(NewGO("rwCard", _reward.transform), 640, 390, 640, 300); _rewardCard = card;
        card.pivot = new Vector2(0.5f, 0.5f); card.anchoredPosition = new Vector2(960, -540);
        var bg = card.gameObject.AddComponent<Image>(); bg.sprite = UISpriteFactory.RoundedRect(48, 18); bg.type = Image.Type.Sliced; bg.color = C("0B1524", 0.98f); bg.raycastTarget = false;
        _rewardOutline = card.gameObject.AddComponent<UnityEngine.UI.Outline>(); _rewardOutline.effectColor = Success; _rewardOutline.effectDistance = new Vector2(1.5f, -1.5f);
        _rewardBar = Img("rwBar", card.gameObject, 0, 0, 640, 4, Success, UISpriteFactory.RoundedRect(8, 2)); _rewardBar.raycastTarget = false;

        // 엠블럼(중심 글로우 + 링)
        Img("rwEmGlow", card.gameObject, 288, 52, 64, 64, C("FFFFFF", 0.10f), UISpriteFactory.Disc(96)).raycastTarget = false;
        _rewardEmblem = Img("rwEmRing", card.gameObject, 288, 52, 64, 64, Success, UISpriteFactory.Ring(96, 4f)); _rewardEmblem.raycastTarget = false;

        _rewardTitle = Txt("rwTitle", card.gameObject, 20, 24, 600, 20, "", _mono, 14, Success, TextAlignmentOptions.Center, 3, FontStyles.Bold);
        _rewardName  = Txt("rwName", card.gameObject, 24, 130, 592, 42, "", _kr, 30, TextBright, TextAlignmentOptions.Center, 0, FontStyles.Bold);
        _rewardDesc  = Txt("rwDesc", card.gameObject, 30, 182, 580, 46, "", _kr, 16, C("E8F2FB", 0.7f), TextAlignmentOptions.Top);
        _rewardDesc.textWrappingMode = TextWrappingModes.Normal;
        Txt("rwHint", card.gameObject, 0, 264, 640, 18, "클릭하여 계속", _mono, 12, C("E8F2FB", 0.4f), TextAlignmentOptions.Center);

        _reward.SetActive(false);
    }

    // 부모 중심 기준으로 배치되는 이미지(원형 이펙트용).
    private Image CenteredImg(string n, Transform p, float ox, float oy, float w, float h, Color col, Sprite spr)
    {
        var go = NewGO(n, p); var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(ox, oy);
        var im = go.AddComponent<Image>(); im.color = col; if (spr != null) im.sprite = spr;
        im.raycastTarget = false; return im;
    }

    // =====================================================================
    // 갱신 / 인터랙션
    // =====================================================================
    private void RefreshAll()
    {
        _m.RebuildKits();     // 인벤토리 → 키트 목록 최신화
        RebuildKitRows();     // 목록에 맞춰 행 재생성 + 시각 상태 갱신
        RefreshSelection();
        RefreshStatus();
        RefreshLog();
        RefreshMarkers();
    }

    // 보유 키트 목록에 맞춰 행을 재생성한다(개수/종류가 바뀌므로 매 갱신 시 다시 만든다).
    private void RebuildKitRows()
    {
        foreach (var r in _kitRows) if (r != null && r.go != null) Destroy(r.go);
        _kitRows.Clear();
        if (_kitEmptyLabel != null) { Destroy(_kitEmptyLabel); _kitEmptyLabel = null; }
        if (_kitListRoot == null) return;

        if (_m.kits.Count == 0)
        {
            // 빈 상태 안내(레이아웃 그룹 자식이므로 LayoutElement 로 높이 확보).
            var go = NewGO("kitEmpty", _kitListRoot.transform);
            go.AddComponent<LayoutElement>().minHeight = 60;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_kr != null) t.font = _kr;
            t.text = "보유한 충전키트가 없습니다.\n공장에서 제작해 가져오세요.";
            t.fontSize = 15; t.color = C("E8F2FB", 0.4f); t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
            _kitEmptyLabel = go;
            return;
        }
        foreach (var k in _m.kits) _kitRows.Add(BuildKitRow(_kitListRoot, k));
        RefreshKitRows();
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

    private void RefreshStatus()
    {
        _statusLine.text = _m.StatusText();
        if (_subLabel != null) _subLabel.text = $"기지 전송 컴퓨터     현재 구간 {RegionKo[(int)_m.Cur]}";
        PositionCursor();
        RefreshLegend();
    }
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
        string kn = k.name; int from = Mathf.RoundToInt(_m.progress);
        // 매니저가 키트 소모 + 전송률 상승 + 토스트 + 이벤트 처리.
        // 전송률 상승은 OnRateChanged → HandleRateChanged 에서 게이지/목록/마커를 갱신한다.
        if (!_m.Send(k)) return;
        _m.logs.Add($"{kn} x1 전송 / {from}% -> {Mathf.RoundToInt(_m.progress)}%");
        RefreshLog();
    }

    // ── TransmissionManager 이벤트 ────────────────────────────────────
    // 씬에 매니저가 없으면(다른 씬에서 열릴 때 등) 런타임 생성 — 기본 키트 정의 사용.
    private static void EnsureManager()
    {
        if (TransmissionManager.Instance != null) return;
        new GameObject("TransmissionManager").AddComponent<TransmissionManager>();
        Debug.LogWarning("[TransmissionUI] 씬에 TransmissionManager가 없어 런타임 생성했습니다(기본 키트 정의 사용).");
    }

    private void SubscribeManager()
    {
        if (_mgrSubscribed) return;
        TransmissionManager.OnRateChanged    += HandleRateChanged;
        TransmissionManager.OnRewardMilestone += HandleMilestone;
        TransmissionManager.OnRegionUnlocked += HandleRegionUnlocked;
        _mgrSubscribed = true;
    }
    private void UnsubscribeManager()
    {
        if (!_mgrSubscribed) return;
        TransmissionManager.OnRateChanged    -= HandleRateChanged;
        TransmissionManager.OnRewardMilestone -= HandleMilestone;
        TransmissionManager.OnRegionUnlocked -= HandleRegionUnlocked;
        _mgrSubscribed = false;
    }

    // 전송률 변화(전송 성공 / F3 등 외부 변경 공통 경로) — 게이지 애니 + 전체 갱신.
    private void HandleRateChanged(int newRate)
    {
        if (!IsOpen) { _shownRate = newRate; return; }   // 닫혀있으면 값만 저장(재열 때 반영)
        SetGauge(newRate, true, _shownRate);
        _shownRate = newRate;
        _m.RebuildKits(); RebuildKitRows(); RefreshSelection(); RefreshStatus();
        DOVirtual.DelayedCall(0.9f, RefreshMarkers).SetUpdate(true);
    }
    private void HandleMilestone(int pct)
    {
        _m.logs.Add($"{pct}% 구간 보상 획득"); if (IsOpen) RefreshLog();
        if (!IsOpen) return;   // 닫혀있으면 연출 스킵(스테일 큐 방지)
        _revealQ.Enqueue(new Reveal
        {
            title = $"구간 달성 · {pct}%", name = $"{_m.RewardName(pct)} 획득!",
            desc = _m.RewardDesc(pct), color = Success, markerPct = pct
        });
        TryPlayNextReveal();
    }
    private void HandleRegionUnlocked(TransmissionRegion r)
    {
        _m.logs.Add($"{RegionKo[(int)r]} 구간 해금"); if (IsOpen) RefreshLog();
        if (!IsOpen) return;
        _revealQ.Enqueue(new Reveal
        {
            title = "구간 해금", name = $"{RegionKo[(int)r]} 구간 개방!",
            desc = "새로운 지역으로 시간에너지 전송을 이어갈 수 있습니다.", color = Accent, markerPct = -1
        });
        TryPlayNextReveal();
    }

    // ── 리워드 리빌 재생 ──────────────────────────────────────────────
    private void TryPlayNextReveal()
    {
        if (_revealBusy || !IsOpen || _revealQ.Count == 0) return;
        _revealBusy = true;
        PlayReveal(_revealQ.Dequeue());
    }

    private void PlayReveal(Reveal r)
    {
        // 내용/색 세팅
        _rewardTitle.text = r.title; _rewardTitle.color = r.color;
        _rewardName.text = r.name; _rewardDesc.text = r.desc;
        _rewardBar.color = r.color; _rewardOutline.effectColor = r.color; _rewardEmblem.color = r.color;

        _reward.SetActive(true); _reward.transform.SetAsLastSibling();
        _rewardCg.alpha = 0f; _rewardCg.blocksRaycasts = false;
        _rewardCard.localScale = Vector3.one * 0.85f;

        _rewardSeq?.Kill();
        _rewardSeq = DOTween.Sequence().SetUpdate(true);
        _rewardSeq.AppendInterval(0.7f);   // 게이지가 해당 지점까지 오르는 동안 대기
        _rewardSeq.AppendCallback(() =>
        {
            _rewardCg.blocksRaycasts = true;
            if (r.markerPct >= 0) FlashMarker(r.markerPct, r.color);
            PlayBurst(r.color);
        });
        _rewardSeq.Append(_rewardCg.DOFade(1f, 0.25f));
        _rewardSeq.Join(_rewardCard.DOScale(1f, 0.42f).SetEase(Ease.OutBack));
        // 자동 닫힘 없음 — 플레이어가 클릭(SkipReveal→CloseReveal)할 때까지 유지.
    }

    private void PlayBurst(Color col)
    {
        _rewardBurst.color = new Color(col.r, col.g, col.b, 0.55f);
        _rewardBurst.rectTransform.localScale = Vector3.one * 0.3f;
        _rewardBurst.rectTransform.DOScale(2.4f, 0.7f).SetEase(Ease.OutQuad).SetUpdate(true);
        _rewardBurst.DOFade(0f, 0.7f).SetUpdate(true);
        for (int i = 0; i < _rewardRings.Count; i++)
        {
            var ring = _rewardRings[i]; var im = ring.GetComponent<Image>();
            ring.localScale = Vector3.one * 0.5f; im.color = new Color(col.r, col.g, col.b, 0.6f);
            ring.DOScale(3.2f + i, 0.9f).SetEase(Ease.OutQuad).SetUpdate(true).SetDelay(i * 0.12f);
            im.DOFade(0f, 0.9f).SetUpdate(true).SetDelay(i * 0.12f);
        }
    }

    private void FlashMarker(int pct, Color col)
    {
        foreach (var mk in _markers)
        {
            if (mk == null || mk.name != $"MK{pct}") continue;
            var rt = (RectTransform)mk.transform;
            rt.DOKill(); rt.localScale = Vector3.one;
            rt.DOScale(1.55f, 0.22f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad).SetUpdate(true);
            break;
        }
    }

    private void SkipReveal()
    {
        // 스크림 blocksRaycasts 는 등장(빛폭발) 이후에만 true 라, 그 전엔 이 콜백이 오지 않는다.
        if (!_revealBusy) return;
        CloseReveal();
    }

    private void CloseReveal()
    {
        _rewardSeq?.Kill();                     // 등장/이전 닫힘 시퀀스 정리(OnComplete 미발생)
        if (_rewardCg != null) _rewardCg.blocksRaycasts = false;   // 닫는 동안 추가 클릭 차단
        _rewardSeq = DOTween.Sequence().SetUpdate(true);
        _rewardSeq.Append(_rewardCg.DOFade(0f, 0.25f));
        _rewardSeq.Join(_rewardCard.DOScale(0.9f, 0.25f).SetEase(Ease.InSine));
        _rewardSeq.OnComplete(() =>
        {
            if (_reward != null) _reward.SetActive(false);
            _revealBusy = false;
            TryPlayNextReveal();               // 큐에 남은 다음 보상 재생
        });
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
        // 1) 창(프레임/배경)이 먼저 빠르게 켜진다.
        _cg.alpha = 0f; _cg.DOFade(1f, 0.12f).SetUpdate(true);
        _content.localScale = Vector3.one;

        // 2) 각 패널이 순서대로 "로드되듯" 서서히 드러난다(스케일 없이 페이드만 — 정적이고 컴퓨터스럽게).
        for (int i = 0; i < _bootPanels.Count; i++)
        {
            var cg = _bootPanels[i]; if (cg == null) continue;
            var rt = (RectTransform)cg.transform;
            rt.DOKill(); cg.DOKill();
            rt.localScale = Vector3.one;                          // 접힘/확대 없음
            cg.alpha = 0f;
            float delay = 0.12f + i * 0.13f;                      // 순차 스태거(하나씩 로드되는 느낌)
            cg.DOFade(1f, 0.3f).SetEase(Ease.OutSine).SetUpdate(true).SetDelay(delay);
        }
    }
    private void KillAll()
    {
        if (_cg != null) _cg.DOKill(); if (_content != null) _content.DOKill();
        if (_fill != null) _fill.DOKill(); if (_node != null) _node.DOKill();
        _rewardSeq?.Kill(); _revealQ.Clear(); _revealBusy = false;
        if (_reward != null) _reward.SetActive(false);
    }

    // ── 툴팁 (마커 호버 콜백) ─────────────────────────────────────────
    public void ShowTooltip(int pct, RectTransform marker)
    {
        var st = _m.MarkerState(pct);
        Color col = st == MState.Done ? Success : st == MState.Next ? Accent : C("E2EDF8", 0.35f);
        _ttBox.color = C("101A2D", 0.98f); var ol = _tooltip.GetComponent<UnityEngine.UI.Outline>(); if (ol != null) ol.effectColor = col;
        _ttTitle.color = col; _ttTitle.text = $"{pct}% 지점 보상";
        // 보상 이름은 이미 획득(Done)했거나 바로 다음 구간(Next)일 때만 공개. 그 이후(Locked)는 ??? 로 가림.
        _ttName.text = st == MState.Locked ? "???" : _m.RewardName(pct);
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
    // UI 표시용 키트 뷰모델 — 실제 정의(ChargedKitDef)와 인벤토리 보유수를 담는다.
    private class Kit
    {
        public TransmissionManager.ChargedKitDef def;  // 매니저 호출용 원본
        public string id;        // 선택 키(itemId 문자열)
        public string name;
        public TransmissionRegion region;
        public bool isBoss;
        public int gain;         // 1개당 상승 전송률(%)
        public int qty;          // 인벤토리 실보유 수(RebuildKits 때 갱신)
    }

    // TransmissionManager(로직·인벤토리·저장)를 감싸는 어댑터. UI는 이 Model만 본다.
    private class Model
    {
        private static TransmissionManager Mgr => TransmissionManager.Instance;

        public string selectedId;
        public readonly List<Kit> kits = new();
        public readonly List<string> logs = new() { "UPLINK 연결됨" };

        public float progress => Mgr != null ? Mgr.TransmissionRate : 0f;
        public TransmissionRegion Cur => Mgr != null ? Mgr.CurrentRegion : TransmissionRegion.Nature;

        // 인벤토리에 실제 보유한 키트로 목록 재구성.
        public void RebuildKits()
        {
            kits.Clear();
            if (Mgr != null)
            {
                foreach (var d in Mgr.GetOwnedKits())
                    kits.Add(new Kit
                    {
                        def = d, id = d.itemId.ToString(), name = d.displayName,
                        region = d.region, isBoss = d.isBoss, gain = d.ratePercent,
                        qty = Mgr.GetOwnedCount(d.itemId)
                    });
            }
            if (selectedId != null && Selected() == null) selectedId = null;  // 선택 키트가 소진됐으면 해제
        }

        public Kit Selected() { foreach (var k in kits) if (k.id == selectedId) return k; return null; }

        // 사용 가능 여부·예상 전송률·전송 실행은 전부 매니저에 위임(구간/상한/보스 규칙은 매니저가 판정).
        public bool Usable(Kit k) => Mgr != null && k != null && Mgr.CanTransmit(k.def, out _);
        public int Target(Kit k) => (Mgr != null && k != null) ? Mgr.GetProjectedRate(k.def) : Mathf.RoundToInt(progress);
        public bool Send(Kit k) => Mgr != null && k != null && Mgr.TryTransmit(k.def.itemId);

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

        // 보상 설명(연출용). TODO: 실제 설비 이름/효과는 보상 데이터 확정 후 교체.
        public string RewardDesc(int pct) => pct switch
        {
            10 or 20 or 30 or 40 or 50 or 60 => "새로운 설비 설계도를 사용할 수 있습니다.",
            70 => "언제든 기지로 즉시 귀환할 수 있습니다.",
            80 or 90 => "우주선 복원에 필요한 핵심 부품을 확보했습니다.",
            _ => "시간에너지 전송 100% — 탈출(엔딩) 조건을 달성했습니다!"
        };

        public string TooltipStatus(int pct, MState st)
        {
            if (st == MState.Done) return "획득 완료";
            if (st == MState.Next)
            {
                bool boundary = pct % 25 == 0; // 25/50/75/100
                string grade = boundary ? "보스" : "일반";
                return $"필요: {RegionKo[(int)Cur]} {grade} 충전키트";
            }
            return "??? (도달 시 공개)";
        }

        public string StatusText()
        {
            if (Mgr == null) return "전송 시스템 대기 중";
            int p = Mgr.TransmissionRate;
            if (p >= TransmissionManager.MaxRate) return "전송률 100% 달성 — 엔딩 조건 충족";
            return $"현재 구간 {RegionKo[(int)Cur]} / 일반 상한 {Mgr.CurrentRegionNormalCap}% / 목표 {Mgr.CurrentRegionGoal}%";
        }
    }

    private string KitMeta(Kit k)
    {
        string g = k.isBoss ? "보스" : "일반";
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
    // 기존 메인 Canvas(스크린 오버레이 루트, 최상위 sortingOrder)를 찾아 그 아래에 넣는다.
    // 못 찾으면(부팅 순서 등) 자체 캔버스로 폴백.
    private Transform ResolveUIParent()
    {
        var main = FindMainCanvas();
        if (main != null)
        {
            // 레이아웃이 1920×1080 절대좌표라 스케일러가 없으면(=ScaleFactor 1) 다른 해상도에서
            // UI가 원본 픽셀로 렌더돼 늘어남/뿌옇게(겹쳐 보임) 나온다. 스케일러가 없을 때만 추가한다.
            // 실게임 HUD(Canvas.prefab)는 이미 ScaleWithScreenSize 1920×1080 스케일러가 있어 이 분기를 그냥 통과.
            EnsureScaler(main.gameObject);
            return main.transform;
        }

        var cvGo = new GameObject("TransmissionCanvas(Fallback)");
        cvGo.transform.SetParent(transform, false);
        var cv = cvGo.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 100;
        EnsureScaler(cvGo);
        cvGo.AddComponent<GraphicRaycaster>();
        Debug.LogWarning("[TransmissionUI] 메인 Canvas를 못 찾아 자체 캔버스로 표시합니다.");
        return cvGo.transform;
    }

    // 캔버스에 CanvasScaler가 없으면 1920×1080 ScaleWithScreenSize 로 추가(있으면 그대로 둠 — 호스트 설정 존중).
    private static void EnsureScaler(GameObject canvasGo)
    {
        if (canvasGo.GetComponent<CanvasScaler>() != null) return;
        var cs = canvasGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        Debug.LogWarning($"[TransmissionUI] 호스트 캔버스 '{canvasGo.name}'에 CanvasScaler가 없어 1920×1080 스케일러를 추가했습니다.");
    }

    private static Canvas FindMainCanvas()
    {
        Canvas best = null; int bestOrder = int.MinValue;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null || !c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
            if (c.sortingOrder > bestOrder) { best = c; bestOrder = c.sortingOrder; }
        }
        return best;
    }

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
        RegisterBootPanel(rt);   // 열림 시 순차 펼침 대상
        return rt;
    }

    // 패널에 CanvasGroup 을 붙여 순차 오픈 애니 대상으로 등록(등록 순서 = 펼쳐지는 순서).
    private void RegisterBootPanel(RectTransform rt)
    {
        if (rt.GetComponent<CanvasGroup>() == null) _bootPanels.Add(rt.gameObject.AddComponent<CanvasGroup>());
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
    private static Sprite _hgrad, _radial, _grid, _scan, _tri, _sweep2, _vig;
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
