// =====================================================================
// TransmissionComputerUI.cs  - 시간에너지 전송 컴퓨터
// 기준 캔버스 1920x1080 절대좌표. 자체 레터박스 래퍼(FitWrap)로 어떤 해상도에서도 안 잘린다.
// 공개 API(Instance/GetOrCreate/Open/HidePanel/Close/LastCloseFrame/IsOpen)는
// 기존 GameUIController / TransmissionComputerTerminal 연동을 위해 유지.
//
// [08-02] 화면 구조를 씬 실물로 전환. 로컬라이징 준비(에디터에 없는 UI엔 컴포넌트를 못 붙인다).
// [08-03] 레이아웃 생성 코드(#if UNITY_EDITOR 영역 619줄) 제거. UI 빌더를 팀 합의로 전부 걷어내면서
//   이 화면도 씬 실물이 원본이 됐다. 다시 구워야 하면 git 이력에서 꺼내 쓴다.
//   런타임 생성물로 남는 것: 배경 장식(격자/링/브래킷 69개), 키트 행/마커(개수가 데이터라 템플릿 복제),
//   물결 텍스처, 무한 트윈.
//
// 주의: 특수 글리프(체크/다이아/삼각 등)는 폰트 아틀라스에 없어 네모로 깨지므로 전부 도형으로 그린다.
//   "?" 만 예외로 폰트 텍스트를 쓴다.
// =====================================================================

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[SingleInstance]
public class TransmissionComputerUI : MonoBehaviour
{
    [Header("폰트 (빌더 입력용 - 실행 시에는 각 글자에 이미 박혀 있다)")]
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
    // 바가 0~100%를 25%씩 4구간으로 나눠 RegionCol 로 칠하므로, 해당 지점의 구간 색을 그대로 돌려준다.
    static Color RegionColorForPct(int pct) => RegionCol[Mathf.Clamp(pct / 25, 0, 3)];

    // ── 싱글톤/공개 API ───────────────────────────────────────────────
    public static TransmissionComputerUI Instance { get; private set; }
    public static int LastCloseFrame { get; private set; } = -10;
    public bool IsOpen => _root != null && _root.activeSelf;
    private static bool _warnedMissing;

    // =====================================================================
    // 씬 참조 (빌더가 채운다)
    // =====================================================================
    [Header("구조")]
    [SerializeField] private GameObject _root;          // 열고 닫는 패널(백드롭 + CanvasGroup)
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private RectTransform _fitWrap;    // 해상도 무관 레터박스 스케일 래퍼
    [SerializeField] private RectTransform _content;    // 1920x1080 레이아웃 루트
    [SerializeField] private RectTransform _decorRoot;  // 격자/크로노링/코너브래킷이 실행 시 담기는 빈 컨테이너
    [SerializeField] private Image _bgRadial;

    [Header("헤더 / 전송률 탱크")]
    [SerializeField] private TMP_Text _subLabel;
    [SerializeField] private TMP_Text _rateBig;
    [SerializeField] private RawImage _rateWave;
    [SerializeField] private TMP_Text _rateRegionLabel;
    [SerializeField] private TMP_Text _goalLabel;

    [Header("진행 바")]
    [SerializeField] private RectTransform _trackRT;
    [SerializeField] private Image _fill;
    [SerializeField] private RectTransform _sweep;
    [SerializeField] private Image _sweepImg;
    [SerializeField] private Image _ghostFill;
    [SerializeField] private RectTransform _node;
    [SerializeField] private Image _nodePulse;
    [SerializeField] private TMP_Text _nodeLabel;
    [SerializeField] private TransmissionMarker _markerTemplate;
    [SerializeField] private TMP_Text[] _legendLabels = new TMP_Text[4];
    [SerializeField] private Image[] _legendDots = new Image[4];
    [SerializeField] private Image[] _legendConns = new Image[4];
    [SerializeField] private RectTransform _legendCurHl;
    [SerializeField] private Image _legendCurHlImg;

    [Header("본문 - 키트 목록")]
    [SerializeField] private RectTransform _kitListRoot;
    [SerializeField] private TransmissionKitRow _kitRowTemplate;
    [SerializeField] private GameObject _kitEmptyLabel;

    [Header("본문 - 전송 제어")]
    [SerializeField] private TMP_Text _selName;
    [SerializeField] private TMP_Text _selMeta;
    [SerializeField] private TMP_Text _previewVal;
    [SerializeField] private Button _sendBtn;
    [SerializeField] private Image _sendBtnImg;
    [SerializeField] private CanvasGroup _sendBtnCg;
    [SerializeField] private Image _sendTri;
    [SerializeField] private Button _closeBtn;
    [SerializeField] private TMP_Text _logText;
    [SerializeField] private Image _logDot;

    [Header("푸터")]
    [SerializeField] private TMP_Text _statusLine;
    [SerializeField] private RectTransform _cursor;
    [SerializeField] private Image _cursorImg;

    [Header("툴팁")]
    [SerializeField] private GameObject _tooltip;
    [SerializeField] private Image _ttBox;
    [SerializeField] private UnityEngine.UI.Outline _ttOutline;   // 풀네임 필수(전역 3D Outline 이 가림)
    [SerializeField] private TMP_Text _ttTitle;
    [SerializeField] private TMP_Text _ttName;
    [SerializeField] private TMP_Text _ttState;
    [SerializeField] private CanvasGroup _ttCg;

    [Header("오버레이")]
    [SerializeField] private Image _scanImg;
    [SerializeField] private Image _vignetteImg;

    [Header("보상 리빌")]
    [SerializeField] private GameObject _reward;
    [SerializeField] private CanvasGroup _rewardCg;
    [SerializeField] private Button _rewardScrimBtn;
    [SerializeField] private RectTransform _rewardCard;
    [SerializeField] private RectTransform _rewardIconTile;
    [SerializeField] private Image _rewardIconBg;
    [SerializeField] private UnityEngine.UI.Outline _rewardIconFrame;
    [SerializeField] private RectTransform _rewardIconHolder;
    [SerializeField] private RectTransform _rewardSweep;
    [SerializeField] private TMP_Text _rewardTitle;
    [SerializeField] private TMP_Text _rewardName;
    [SerializeField] private TMP_Text _rewardDesc;
    [SerializeField] private TMP_Text _rewardHint;
    [SerializeField] private Image[] _rewardTint;     // 리빌 색으로 물들일 장식

    [Header("보상 아이콘 3종 - 보상 종류에 따라 하나만 켠다")]
    [SerializeField] private GameObject _riSingleGo;  // 설비 1개
    [SerializeField] private Image _riSingle;
    [SerializeField] private GameObject _riHalfTL;    // 설비 2개(좌상 반쪽)
    [SerializeField] private Image _riHalfTLMask;
    [SerializeField] private Image _riHalfTLIcon;
    [SerializeField] private GameObject _riHalfBR;    // 설비 2개(우하 반쪽)
    [SerializeField] private Image _riHalfBRMask;
    [SerializeField] private Image _riHalfBRIcon;
    [SerializeField] private GameObject _riGem;       // 비설비 보상 = 보석 엠블럼
    [SerializeField] private Image _riGemCoin;

    [Header("열기 애니 - 순차로 펼쳐질 패널들(순서 = 펼침 순서)")]
    [SerializeField] private CanvasGroup[] _bootPanels;

    // =====================================================================
    // 런타임 상태
    // =====================================================================
    private int _lastFitW, _lastFitH;      // 마지막 fit 계산 시 화면 크기(변경 감지용)
    private float _trackW;
    private int _openedFrame = -1;

    private Texture2D _rateWaveTex; private Color[] _rateWavePx;
    private float _rateLevelShown;
    private const int RWW = 96, RWH = 72;   // 물 텍스처 해상도

    private readonly List<RectTransform> _spinRings = new();
    private readonly List<TransmissionMarker> _markers = new();
    private readonly List<KitRow> _kitRows = new();
    private int _shownRate;               // 현재 게이지에 표시 중인 전송률(애니 from 기준)
    private bool _mgrSubscribed;          // TransmissionManager 이벤트 구독 여부
    private readonly List<Vector2> _bootHome = new();   // 부팅 패널 원위치(슬라이드 인 기준)

    private float _ghostToPct = -1f;      // 고스트 목표 %(활성 시 >=0). Update 가 좌변을 실채움에 맞춰 추적
    private float _splashT;               // 전송 직후 탱크 스플래시 진폭 감쇠(1->0)
    private readonly float[] _legendBx = new float[4]; // 각 구간 경계 x(현재 구간 강조 배치용)
    private float _legendLy;                           // 레전드 도트 y

    private Vector2 _cardHome;            // 리빌 카드 슬라이드 기준 위치
    private Sequence _rewardSeq;
    private struct Reveal { public string title, name, desc; public Color color; public int markerPct; public int order; public bool milestone; }
    private readonly Queue<Reveal> _revealQ = new();
    private bool _revealBusy;
    private bool _revealStartScheduled;   // 같은 rate 변경의 여러 리빌을 모아 한 번에 정렬·재생하기 위한 지연 플래그

    private Model _m;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (UIDuplicateGuard.Report(Instance, this)) { Destroy(gameObject); return; }
        Instance = this;
        _warnedMissing = false;

        if (_root == null || _content == null || _trackRT == null)
        {
            Debug.LogError("[TransmissionUI] 씬 참조가 비어 있다. 메뉴 Tools/TIMEKOV/전송 컴퓨터 UI 생성 을 실행해라.");
            return;
        }

        _m = new Model();
        _m.logs.Add("__UPLINK__");
        EnsureManager();          // 씬에 매니저 없으면 런타임 생성
        SubscribeManager();       // 전송률/보상/해금 이벤트 구독(닫혀있어도 _shownRate 동기화)
        _shownRate = Mathf.RoundToInt(_m.progress);

        ApplyProceduralTextures();
        CaptureLayoutConstants();
        BuildDecor();
        BuildMarkers();
        WireButtons();

        _root.SetActive(false);
    }

    /// <summary>씬에 있는 전송 컴퓨터 UI 를 돌려준다(없으면 에러 1회). 호출부는 null 을 가드할 것.</summary>
    public static TransmissionComputerUI GetOrCreate()
    {
        if (Instance == null && !_warnedMissing)
        {
            Instance = FindAnyObjectByType<TransmissionComputerUI>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                _warnedMissing = true;
                Debug.LogError("[TransmissionUI] 씬 Canvas 에 전송 컴퓨터 UI 가 없다. 메뉴 Tools/TIMEKOV/전송 컴퓨터 UI 생성 을 실행해라.");
            }
        }
        return Instance;
    }

    private void OnEnable()  => Loc.OnLanguageChanged += OnLanguageChanged;
    private void OnDisable() => Loc.OnLanguageChanged -= OnLanguageChanged;
    private void OnLanguageChanged() { if (_root != null && _root.activeSelf) RefreshAll(); }

    private void OnDestroy()
    {
        UnsubscribeManager();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Screen.width != _lastFitW || Screen.height != _lastFitH) FitContentToRoot();   // 창 크기/해상도 바뀌면 재맞춤
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _spinRings.Count; i++)
            if (_spinRings[i] != null) _spinRings[i].Rotate(0, 0, (i % 2 == 0 ? -1f : 1f) * 9f * dt);
        if (_splashT > 0f) _splashT = Mathf.Max(0f, _splashT - dt * 0.8f);   // 전송 스플래시 감쇠(약 1.25s)
        LayoutGhost();   // 고스트 좌변을 실채움에 매 프레임 맞춰 끊김 없이 이어지게
        // 물 표면 - 매 프레임 텍스처 재생성(진짜 물결 애니메이션).
        if (_rateWave != null) RegenWater(Time.unscaledTime);
        if (Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F)) Close();
    }

    public void Open()
    {
        EnsureManager();                      // 다른 씬 등에서 매니저가 없으면 보장
        _root.SetActive(true);
        transform.SetAsLastSibling();         // 같은 그룹 내 형제들 위(맨 앞)로
        FitContentToRoot();                   // 현재 화면 크기에 맞춰 레이아웃 스케일(해상도 무관 안 잘림)
        _openedFrame = Time.frameCount;
        _m.selectedId = null;
        _shownRate = Mathf.RoundToInt(_m.progress);
        RefreshAll();
        SetGauge(_m.progress, false);
        PlayOpenAnim();
        StartAmbientTweens();
        GameSfx.Play(SfxId.PanelTransmissionToggle);   // 시간에너지 전송기 열기음
    }

    public void HidePanel() { KillAll(); if (_root != null) _root.SetActive(false); }

    public void Close()
    {
        LastCloseFrame = Time.frameCount;
        GameSfx.Play(SfxId.PanelTransmissionToggle);   // 닫기음(열/닫 공용 클립)
        GameUIController.Instance?.CloseTransmissionUI();
        if (_cg == null) { if (_root != null) _root.SetActive(false); return; }
        KillAll();
        _cg.DOFade(0f, 0.15f).SetUpdate(true);
        _content.DOScale(0.98f, 0.15f).SetUpdate(true).OnComplete(() => { if (_root != null) _root.SetActive(false); });
    }

    // =====================================================================
    // 실행 시 준비 (씬에 저장할 수 없는 것들)
    // =====================================================================
    // 절차 텍스처는 에셋이 아니라 메모리 생성물이라 씬에 저장되지 않는다.
    // 팩토리(UISpriteFactory) 스프라이트는 RuntimeGeneratedSprite 가 알아서 되살리고,
    // 여기 있는 이 화면 전용 텍스처들만 직접 다시 물린다.
    private void ApplyProceduralTextures()
    {
        if (_bgRadial != null) _bgRadial.sprite = RadialTex();
        if (_fill != null) _fill.sprite = HGrad();
        if (_sweepImg != null) _sweepImg.sprite = SweepTex();
        if (_scanImg != null) _scanImg.sprite = ScanTile();
        if (_vignetteImg != null) _vignetteImg.sprite = VignetteTex();
        if (_sendTri != null) _sendTri.sprite = TriTex();
        if (_riHalfTLMask != null) _riHalfTLMask.sprite = TriangleSprite(true);
        if (_riHalfBRMask != null) _riHalfBRMask.sprite = TriangleSprite(false);

        if (_rateWave != null)
        {
            _rateWaveTex = new Texture2D(RWW, RWH, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _rateWavePx = new Color[RWW * RWH];
            _rateWave.texture = _rateWaveTex;
        }
    }

    // 빌드 시점에 계산해두던 상수들을 실물 배치에서 다시 읽는다.
    private void CaptureLayoutConstants()
    {
        _trackW = _trackRT.rect.width;
        for (int i = 0; i < 4 && _legendDots != null && i < _legendDots.Length; i++)
        {
            if (_legendDots[i] == null) continue;
            var p = _legendDots[i].rectTransform.anchoredPosition;   // 빌드 때 (bx-5, -ly) 로 배치했다
            _legendBx[i] = p.x + 5f;
            _legendLy = -p.y;
        }
        _bootHome.Clear();
        if (_bootPanels != null)
            foreach (var cg in _bootPanels)
                _bootHome.Add(cg != null ? ((RectTransform)cg.transform).anchoredPosition : Vector2.zero);
        if (_rewardCard != null) _cardHome = _rewardCard.anchoredPosition;   // 리빌 카드 슬라이드 기준
    }

    // 배경 장식(격자 56 + 크로노링 5 + 코너브래킷 8). 글자가 없어 씬에 두면 하이어라키만 더러워지므로 코드에 남긴다.
    private void BuildDecor()
    {
        if (_decorRoot == null) return;

        var col = C("4CC9F7", 0.03f);
        const int cell = 56;
        for (int x = 0; x <= 1920; x += cell) Img("gv", _decorRoot, x, 0, 1, 1080, col).raycastTarget = false;
        for (int y = 0; y <= 1080; y += cell) Img("gh", _decorRoot, 0, y, 1920, 1, col).raycastTarget = false;

        // 크로노 링(우상단/좌하단). 점선은 저알파 실선으로 근사, 일부 회전.
        AddRing(-140 + 1920 - 620, -180, 620, 0.12f, 3f, false);
        AddRing(-60 + 1920 - 460, -100, 460, 0.16f, 2f, true);
        AddRing(20 + 1920 - 300, -20, 300, 0.08f, 2f, false);
        AddRing(-200, 1080 - 560 + 260, 560, 0.08f, 3f, false);
        AddRing(-140, 1080 - 440 + 200, 440, 0.10f, 2f, true);

        // 코너 브래킷 4개
        Bracket(26, 26, true, true); Bracket(1920 - 26 - 34, 26, false, true);
        Bracket(26, 1080 - 26 - 34, true, false); Bracket(1920 - 26 - 34, 1080 - 26 - 34, false, false);
    }

    private void AddRing(float x, float y, float d, float a, float th, bool spin)
    {
        var im = Img("ChronoRing", _decorRoot, x, y, d, d, C("4CC9F7", a), UISpriteFactory.Ring((int)Mathf.Min(256, d), th));
        im.raycastTarget = false;
        if (spin) _spinRings.Add((RectTransform)im.transform);
    }

    private void Bracket(float x, float y, bool left, bool top)
    {
        var col = C("4CC9F7", 0.55f);
        Img("brkH", _decorRoot, x, top ? y : y + 32, 34, 2, col).raycastTarget = false;   // 수평 팔
        Img("brkV", _decorRoot, left ? x : x + 32, y, 2, 34, col).raycastTarget = false;  // 수직 팔
    }

    // 보상 지점마다 바를 가로지르는 세로 구분선.
    //   씬에 있던 세로선(TrackLine)은 범례용 4개뿐이라, 보상 지점에는 선이 있다 없다 했다.
    //   마커(동그라미)만으로는 "정확히 어디서 보상이 걸리는지"가 바 위에서 안 읽혀서 전 지점에 넣는다.
    //   ★마커보다 먼저 만들어 뒤에 깔리게 한다(선이 마커 아이콘을 가로지르지 않게).
    private void BuildMilestoneTicks()
    {
        if (_trackRT == null) return;

        float th = _trackRT.rect.height;
        foreach (int p in TransmissionManager.RewardMilestones)
        {
            var go = new GameObject($"Tick{p}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_trackRT, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);   // 마커와 같은 기준(좌하단)
            rt.pivot = new Vector2(0.5f, 0.5f);

            // ★픽셀에 맞춰 스냅한다. 소수 좌표의 얇은 선은 픽셀 경계를 걸쳐 번지고,
            //   지점마다 번지는 정도가 달라 "어떤 건 두껍고 어떤 건 얇은" 것처럼 보인다.
            //   피벗이 가운데라 폭이 홀수면 반픽셀 위치에 놓아야 또렷하다
            //   (1px 선을 정수 좌표에 두면 좌우로 0.5px 씩 걸쳐 오히려 흐려진다).
            float half = (TickWidth % 2f == 0f) ? 0f : 0.5f;
            float x = Mathf.Round(_trackW * p / 100f - half) + half;
            rt.anchoredPosition = new Vector2(x, th / 2f);
            rt.sizeDelta = new Vector2(TickWidth, th);

            var img = go.AddComponent<Image>();
            img.color = C("E2EDF8", 0.30f);   // 채워진 색 위에서도, 빈 바 위에서도 보이는 정도
            img.raycastTarget = false;        // 마커 호버/툴팁을 가리지 않게
        }
    }

    private const float TickWidth = 1f;

    // 마커 - 보상 걸린 마일스톤마다(불균등: 5/15/25/75 등). TransmissionManager 와 공유.
    private void BuildMarkers()
    {
        if (_markerTemplate == null) return;
        BuildMilestoneTicks();

        float th = _trackRT.rect.height;
        foreach (int p in TransmissionManager.RewardMilestones)
        {
            var mk = Instantiate(_markerTemplate, _trackRT);
            mk.name = $"MK{p}";
            mk.gameObject.SetActive(true);
            var rt = (RectTransform)mk.transform;
            rt.anchoredPosition = new Vector2(_trackW * p / 100f, th / 2f);
            mk.Bind(this, p);
            _markers.Add(mk);
        }
        // 진행 노드를 마커보다 위로 - 같은 지점(예: 80%)에서 겹쳐도 노드가 가려지지 않게.
        if (_node != null) _node.SetAsLastSibling();
    }

    // 클릭/호버 리스너는 실행 시에 건다(에디터 영속 리스너를 안 만들어야 씬 diff 가 깔끔하다).
    private void WireButtons()
    {
        if (_sendBtn != null) { _sendBtn.onClick.AddListener(OnSend); AddButtonSfx(_sendBtn); }
        if (_closeBtn != null) { _closeBtn.onClick.AddListener(Close); AddButtonSfx(_closeBtn); }
        if (_rewardScrimBtn != null) _rewardScrimBtn.onClick.AddListener(SkipReveal);
    }

    // 버튼 호버/클릭 사운드 - 씬 세팅 없이 코드로 부착(GameSfx 통합음).
    //   호버: PointerEnter 시 재생하되 잠긴 버튼(interactable=false)은 무음.
    //   클릭: onClick 은 interactable 일 때만 발화하므로 그대로 붙이면 잠금 시 자동 무음.
    private static void AddButtonSfx(Button btn)
    {
        if (btn == null) return;
        var trig = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { if (btn.interactable) GameSfx.Play(SfxId.UIButtonHover); });
        trig.triggers.Add(enter);
        btn.onClick.AddListener(() => GameSfx.Play(SfxId.UIButtonClick));
    }

    // 무한 반복 연출(노드 펄스 / 로그 점 / 커서 깜빡임). 열 때 시작하고 닫을 때 죽인다
    // (빌드 시점에 걸면 닫혀 있는 동안에도 계속 돈다).
    private void StartAmbientTweens()
    {
        if (_nodePulse != null)
        {
            var prt = _nodePulse.rectTransform;
            prt.DOKill(); _nodePulse.DOKill();
            prt.localScale = Vector3.one; SetGraphicAlpha(_nodePulse, 0.5f);
            prt.DOScale(2.3f, 1.2f).SetLoops(-1, LoopType.Restart).SetEase(Ease.OutQuad).SetUpdate(true);
            _nodePulse.DOFade(0f, 1.2f).SetLoops(-1, LoopType.Restart).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        if (_logDot != null)
        {
            _logDot.DOKill(); SetGraphicAlpha(_logDot, 1f);
            _logDot.DOFade(0.1f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }
        if (_cursorImg != null)
        {
            _cursorImg.DOKill(); SetGraphicAlpha(_cursorImg, 1f);
            _cursorImg.DOFade(0f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Linear).SetUpdate(true);
        }
    }

    private static void SetGraphicAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

    // =====================================================================
    // 갱신 / 인터랙션
    // =====================================================================
    private void RefreshAll()
    {
        _m.RebuildKits();     // 인벤토리 -> 키트 목록 최신화
        RebuildKitRows();     // 목록에 맞춰 행 재생성 + 시각 상태 갱신
        RefreshSelection();
        RefreshStatus();
        RefreshLog();
        RefreshMarkers();
    }

    // 보유 키트 목록에 맞춰 행을 재생성한다(개수/종류가 바뀌므로 매 갱신 시 템플릿을 다시 복제).
    private void RebuildKitRows()
    {
        foreach (var r in _kitRows) if (r.ui != null) Destroy(r.ui.gameObject);
        _kitRows.Clear();
        if (_kitListRoot == null || _kitRowTemplate == null) return;

        bool empty = _m.kits.Count == 0;
        if (_kitEmptyLabel != null) _kitEmptyLabel.SetActive(empty);
        if (empty) return;

        for (int i = 0; i < _m.kits.Count; i++)
        {
            var k = _m.kits[i];
            var row = Instantiate(_kitRowTemplate, _kitListRoot);
            row.name = $"kit_{k.id}";
            row.gameObject.SetActive(true);
            row.Bind(this, i, k.name, $"+{k.gain}%", k.isBoss, RegionCol[(int)k.region]);
            _kitRows.Add(new KitRow { kit = k, ui = row });
        }
        RefreshKitRows();
    }

    private void RefreshMarkers()
    {
        foreach (var mk in _markers)
        {
            if (mk == null) continue;
            var st = _m.MarkerState(mk.Pct);
            // 공개된 지점(완료/다음)은 구간 색으로. 잠금은 흐린 회색.
            Color col = st == MState.Locked ? C("E2EDF8", 0.25f) : RegionColorForPct(mk.Pct);
            mk.SetState(ToMarkerState(st), col);
        }
    }

    private static TransmissionMarker.State ToMarkerState(MState st) => st switch
    {
        MState.Done => TransmissionMarker.State.Done,
        MState.Next => TransmissionMarker.State.Next,
        _ => TransmissionMarker.State.Locked,
    };

    private void RefreshKitRows()
    {
        foreach (var r in _kitRows)
        {
            var ui = r.ui; if (ui == null) continue;
            bool usable = _m.Usable(r.kit);
            bool sel = _m.selectedId == r.kit.id;
            // 행 BG 패널 - 지역별 색 틴트(선택 시 더 진하게 + 테두리 강조).
            Color rc = RegionCol[(int)r.kit.region];
            if (ui.background != null) ui.background.color = new Color(rc.r, rc.g, rc.b, sel ? 0.22f : 0.10f);
            if (ui.rowOutline != null) ui.rowOutline.effectColor = sel ? rc : new Color(rc.r, rc.g, rc.b, 0.28f);
            if (ui.accentBar != null) ui.accentBar.color = new Color(rc.r, rc.g, rc.b, sel ? 0.95f : 0f);
            if (ui.nameText != null) ui.nameText.color = usable ? TextBright : C("E8F2FB", 0.35f);
            if (ui.metaText != null) { ui.metaText.text = KitMeta(r.kit); ui.metaText.color = usable ? C("E8F2FB", 0.45f) : C("E8F2FB", 0.35f); }
            if (ui.qtyText != null) ui.qtyText.text = $"x{r.kit.qty}";
            // 불가 행도 호버 툴팁(사유 안내)을 받도록 blocksRaycasts 는 항상 켜두고, 클릭만 interactable 로 차단.
            if (ui.group != null) { ui.group.alpha = usable ? 1f : 0.4f; ui.group.blocksRaycasts = true; ui.group.interactable = usable; }
        }
    }

    private void RefreshSelection()
    {
        var k = _m.Selected();
        _selName.text = k != null ? k.name : Loc.Get("없음");
        _selMeta.text = k != null ? KitMeta(k) : Loc.Get("목록에서 키트를 클릭");
        // 예상 전송률
        string pv; Color pc; bool active;
        if (k == null) { pv = Loc.Get("키트를 선택하세요"); pc = C("E8F2FB", 0.4f); active = false; }
        else if (!_m.Usable(k)) { pv = Loc.Get("전송 불가"); pc = C("E8F2FB", 0.4f); active = false; }
        else { int t = _m.Target(k); int d = t - Mathf.RoundToInt(_m.progress); pv = string.Format(Loc.Get("전송 시 {0}% (+{1})"), t, d); pc = Accent; active = d > 0; }
        _previewVal.text = pv; _previewVal.color = pc;
        _sendBtnImg.color = active ? C("47C4F0") : C("47C4F0", 0.4f);
        _sendBtnCg.alpha = active ? 1f : 0.5f; _sendBtnCg.blocksRaycasts = active; _sendBtnCg.interactable = active;
        UpdateGhostPreview(active ? k : null);
    }

    // ── 게이지 구간 아이콘 ────────────────────────────────────────────────
    // 바가 초록→파랑→주황→빨강으로 차오르는데, 뒤쪽이 빨강이라 "채우면 안 되는 것" 처럼
    // 읽힌다는 피드백이 있었다. 각 구간이 '맵 테마'라는 뜻을 주려고 구간 한가운데에
    // 테마 아이콘(잎/눈결정/태양/불꽃)을 얹는다.
    //   ★미도달 구간은 범례가 아직 ??? 로 가려 두므로 아이콘도 흐린 실루엣으로만 둔다.
    private Image[] _regionIcons;

    private void EnsureRegionIcons()
    {
        if (_regionIcons != null || _trackRT == null) return;

        _regionIcons = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"RegionIcon{i}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_trackRT, false);

            // 구간(25%씩) 한가운데에 배치 — 12.5 / 37.5 / 62.5 / 87.5 %
            float pct = (i * 25f + 12.5f) / 100f;
            rt.anchorMin = rt.anchorMax = new Vector2(pct, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(RegionIconSize, RegionIconSize);

            var img = go.AddComponent<Image>();
            img.sprite = TransmissionRegionIcons.Get(i);
            img.raycastTarget = false;   // 바 위 클릭/툴팁을 가리지 않게
            img.preserveAspect = true;
            _regionIcons[i] = img;
        }
    }

    private const float RegionIconSize = 22f;

    // 도달한 구간은 또렷하게(채워진 바 위라 흰색이 제일 잘 읽힌다), 아직인 구간은 흐린 실루엣.
    // 지금 구간은 살짝 키워서 "여기 진행 중"을 표시한다.
    private void RefreshRegionIcons()
    {
        if (_regionIcons == null) return;

        int rate = _m != null ? Mathf.RoundToInt(_m.progress) : 0;
        int cur  = _m != null ? (int)_m.Cur : -1;

        for (int i = 0; i < 4; i++)
        {
            var img = _regionIcons[i];
            if (img == null) continue;

            bool reached = rate >= i * 25;
            bool isCur   = i == cur && reached;

            // 채워진 구간 위에서는 흰색이 지역색보다 잘 보인다. 미도달은 바 배경 위라 흐리게.
            //   현재 구간은 더 밝게 + 살짝 크게 = "지금 여기" 표시(범례의 굵은 라벨과 짝).
            img.color = reached ? new Color(1f, 1f, 1f, isCur ? 0.95f : 0.75f)
                                : new Color(1f, 1f, 1f, 0.18f);
            img.rectTransform.localScale = Vector3.one * (isCur ? 1.15f : 1f);
        }
    }

    // 각 구간 라벨은 해당 구간에 도달(전송률 >= 구간 시작 %)해야 공개. 그 전엔 ??? + 흐리게.
    private void RefreshLegend()
    {
        EnsureRegionIcons();
        RefreshRegionIcons();
        int rate = _m != null ? Mathf.RoundToInt(_m.progress) : 0;
        int cur = _m != null ? (int)_m.Cur : -1;   // 지금 활성 구간 - 라벨을 굵게 + 뒤에 하이라이트
        for (int i = 0; i < 4; i++)
        {
            if (_legendLabels[i] == null) continue;
            bool reached = rate >= i * 25;
            bool isCur = i == cur && reached;
            if (reached)
            {
                _legendLabels[i].text = $"{Loc.Get(RegionKo[i])} {i * 25}-{(i + 1) * 25}";
                _legendLabels[i].color = new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, isCur ? 1f : 0.88f);
                _legendLabels[i].fontStyle = isCur ? FontStyles.Bold : FontStyles.Normal;
                if (_legendDots[i] != null)
                {
                    _legendDots[i].color = RegionCol[i];
                    _legendDots[i].rectTransform.localScale = Vector3.one * (isCur ? 1.35f : 1f);
                }
                if (_legendConns[i] != null) _legendConns[i].color = new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.7f);
            }
            else
            {
                _legendLabels[i].text = "???";
                _legendLabels[i].color = C("E2EDF8", 0.28f);
                _legendLabels[i].fontStyle = FontStyles.Normal;
                if (_legendDots[i] != null) { _legendDots[i].color = C("E2EDF8", 0.2f); _legendDots[i].rectTransform.localScale = Vector3.one; }
                if (_legendConns[i] != null) _legendConns[i].color = C("E2EDF8", 0.15f);
            }
        }
        // 현재 구간 하이라이트 바 - 도트~라벨을 감싸도록 배치.
        if (_legendCurHl != null)
        {
            bool show = cur >= 0 && rate >= cur * 25;
            _legendCurHl.gameObject.SetActive(show);
            if (show)
            {
                var rc = RegionCol[cur];
                if (_legendCurHlImg != null) _legendCurHlImg.color = new Color(rc.r, rc.g, rc.b, 0.12f);
                float labW = _legendLabels[cur] != null ? _legendLabels[cur].GetPreferredValues().x : 70f;
                _legendCurHl.anchoredPosition = new Vector2(_legendBx[cur] - 11, -(_legendLy - 6));
                _legendCurHl.sizeDelta = new Vector2(labW + 44, 22);
            }
        }
    }

    // 바 위 고스트 미리보기 - 현재 채움 오른쪽에 "예상 상승 구간"을 반투명으로 겹쳐 은은하게 점멸.
    // 좌변은 매 프레임(Update)의 실채움 위치에 맞춰 따라가므로, 게이지가 차오르는 동안에도
    // 실채움 -> 고스트가 끊김 없이 이어진다(빈 칸 없음).
    private void UpdateGhostPreview(Kit k)
    {
        if (_ghostFill == null) return;
        float to = k != null ? Mathf.Clamp(_m.Target(k), 0f, 100f) : -1f;
        if (k == null || to <= Mathf.Clamp(_m.progress, 0f, 100f))
        {
            _ghostToPct = -1f;
            _ghostFill.DOKill();
            _ghostFill.gameObject.SetActive(false);
            return;
        }
        _ghostToPct = to;
        Color rc = RegionCol[(int)k.region];
        _ghostFill.gameObject.SetActive(true);
        _ghostFill.DOKill();
        _ghostFill.color = new Color(rc.r, rc.g, rc.b, 0.14f);
        _ghostFill.DOFade(0.34f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        LayoutGhost();
    }

    // 고스트 좌변을 현재 보이는 실채움 위치에, 우변을 목표에 맞춘다(실채움이 애니 중이면 그 값을 따라감).
    private void LayoutGhost()
    {
        if (_ghostFill == null || _ghostToPct < 0f || !_ghostFill.gameObject.activeSelf) return;
        float live = _fill != null ? _fill.fillAmount * 100f : Mathf.Clamp(_m.progress, 0f, 100f);
        float to = _ghostToPct;
        if (to <= live) { _ghostFill.rectTransform.sizeDelta = new Vector2(0, _ghostFill.rectTransform.sizeDelta.y); return; }
        float x = _trackW * live / 100f;
        float w = _trackW * (to - live) / 100f;
        var rt = _ghostFill.rectTransform;
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
    }

    private void RefreshStatus()
    {
        _statusLine.text = _m.StatusText();
        if (_subLabel != null) _subLabel.text = Loc.Get("기지 전송 컴퓨터 현재 구간") + " " + Loc.Get(RegionKo[(int)_m.Cur]);
        SetRateTankColor();
        PositionCursor();
        RefreshLegend();
    }

    private void RefreshLog()
    {
        var sb = new System.Text.StringBuilder();
        int start = Mathf.Max(0, _m.logs.Count - 4);
        for (int i = start; i < _m.logs.Count; i++)
        {
            string entry = _m.logs[i] == "__UPLINK__" ? Loc.Get("UPLINK 연결됨") : _m.logs[i];
            sb.AppendLine("> " + entry);
        }
        _logText.text = sb.ToString().TrimEnd();
    }

    // 상태 텍스트 실제 폭을 재서 커서를 그 끝 바로 옆에 배치.
    private void PositionCursor()
    {
        if (_cursor == null || _statusLine == null) return;
        float w = _statusLine.GetPreferredValues(_statusLine.text).x;
        _cursor.anchoredPosition = new Vector2(88 + w + 10, _cursor.anchoredPosition.y);
    }

    /// <summary>키트 행 클릭(TransmissionKitRow 가 부른다).</summary>
    public void OnKitRowClicked(int index)
    {
        if (index < 0 || index >= _kitRows.Count) return;
        var k = _kitRows[index].kit;
        if (!_m.Usable(k)) return;
        _m.selectedId = _m.selectedId == k.id ? null : k.id;
        RefreshKitRows(); RefreshSelection();
        // 선택된 행에 클릭 피드백 - 살짝 팝(scale punch) + 악센트 바 슬라이드 인.
        foreach (var r in _kitRows)
        {
            if (r.ui == null || r.kit.id != _m.selectedId) continue;
            var rt = (RectTransform)r.ui.transform;
            rt.DOKill(); rt.localScale = Vector3.one;
            rt.DOPunchScale(new Vector3(0.015f, 0.06f, 0f), 0.28f, 8, 0.7f).SetUpdate(true);
            if (r.ui.accentBar != null)
            {
                var art = r.ui.accentBar.rectTransform;
                art.DOKill(); art.localScale = new Vector3(1f, 0.2f, 1f);
                art.DOScaleY(1f, 0.24f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            break;
        }
    }

    private void OnSend()
    {
        var k = _m.Selected(); if (k == null || !_m.Usable(k)) return;
        string kn = k.name; int from = Mathf.RoundToInt(_m.progress);
        // 매니저가 키트 소모 + 전송률 상승 + 토스트 + 이벤트 처리.
        // 전송률 상승은 OnRateChanged -> HandleRateChanged 에서 게이지/목록/마커를 갱신한다.
        if (!_m.Send(k)) return;
        // 전송 버튼 클릭 피드백 - 살짝 눌리는 팝.
        if (_sendBtnImg != null)
        {
            var brt = _sendBtnImg.rectTransform; brt.DOKill(); brt.localScale = Vector3.one;
            brt.DOPunchScale(new Vector3(0.03f, 0.06f, 0f), 0.3f, 9, 0.8f).SetUpdate(true);
        }
        _m.logs.Add(string.Format(Loc.Get("{0} x1 전송 / {1}% -> {2}%"), kn, from, Mathf.RoundToInt(_m.progress)));
        RefreshLog();
    }

    // ── TransmissionManager 이벤트 ────────────────────────────────────
    // 씬에 매니저가 없으면(다른 씬에서 열릴 때 등) 런타임 생성 - 기본 키트 정의 사용.
    private static void EnsureManager()
    {
        if (TransmissionManager.Instance != null) return;
        // 어느 씬에도 매니저를 두지 않는 게 현재 구성이라 이 경로가 정상이다.
        // 경고로 알릴 이유가 없어 뺐다(열 때마다 떴다).
        new GameObject("TransmissionManager").AddComponent<TransmissionManager>();
    }

    private void SubscribeManager()
    {
        if (_mgrSubscribed) return;
        TransmissionManager.OnRateChanged    += HandleRateChanged;
        TransmissionManager.OnRewardMilestone += HandleMilestone;
        _mgrSubscribed = true;
    }
    private void UnsubscribeManager()
    {
        if (!_mgrSubscribed) return;
        TransmissionManager.OnRateChanged    -= HandleRateChanged;
        TransmissionManager.OnRewardMilestone -= HandleMilestone;
        _mgrSubscribed = false;
    }

    // 전송률 변화(전송 성공 / F3 등 외부 변경 공통 경로) - 게이지 애니 + 전체 갱신.
    private void HandleRateChanged(int newRate)
    {
        if (!IsOpen) { _shownRate = newRate; return; }   // 닫혀있으면 값만 저장(재열 때 반영)
        int oldRate = _shownRate;
        SetGauge(newRate, true, _shownRate);
        _shownRate = newRate;
        _m.RebuildKits(); RebuildKitRows(); RefreshSelection(); RefreshStatus();
        // 전송으로 상승하면 탱크에 한 번 스플래시가 튄다(Update 에서 감쇠).
        if (newRate > oldRate) _splashT = 1f;
        // 게이지가 도착할 즈음(약 0.9s) 마커/레전드 갱신 + 이번에 새로 넘긴 지점 강조.
        DOVirtual.DelayedCall(0.9f, () =>
        {
            RefreshMarkers();
            // 이번 상승으로 새로 넘긴 마일스톤 마커를 펄스.
            foreach (int pct in TransmissionManager.RewardMilestones)
                if (pct > oldRate && pct <= newRate) FlashMarker(pct, RegionColorForPct(pct));
            // 새로 진입한 구간(25/50/75)의 레전드 도트를 펄스.
            for (int b = ((oldRate / 25) + 1) * 25; b <= newRate && b <= 100; b += 25)
                PulseLegendDot(b / 25);
        }).SetUpdate(true);
    }

    // 레전드 도트를 한 번 튕겨(강조) 새 구간 진입을 알린다.
    private void PulseLegendDot(int i)
    {
        if (i < 0 || i >= _legendDots.Length || _legendDots[i] == null) return;
        var rt = _legendDots[i].rectTransform;
        rt.DOKill();
        rt.DOPunchScale(Vector3.one * 0.9f, 0.5f, 8, 0.6f).SetUpdate(true);
    }

    private void HandleMilestone(int pct)
    {
        _m.logs.Add(string.Format(Loc.Get("{0}% 구간 보상 획득"), pct)); if (IsOpen) RefreshLog();
        if (!IsOpen) return;   // 닫혀있으면 연출 스킵(스테일 큐 방지)
        _revealQ.Enqueue(new Reveal
        {
            title = Loc.Get("구간 달성") + $" {pct}%", name = pct >= 100 ? _m.RewardName(pct) : _m.RewardName(pct) + " " + Loc.Get("획득!"),
            desc = _m.RewardDesc(pct), color = RegionColorForPct(pct), markerPct = pct, order = pct, milestone = true
        });
        ScheduleRevealStart();
    }

    // ── 리워드 리빌 재생 ──────────────────────────────────────────────
    // 한 번의 rate 변경에서 보상·해금 이벤트가 연달아 발생하므로(매니저가 보상 먼저, 해금 나중에 통지),
    // 즉시 재생하지 않고 다음 틱까지 미뤄 전부 큐에 모은 뒤 정렬해서 재생한다.
    private void ScheduleRevealStart()
    {
        if (_revealStartScheduled) return;
        _revealStartScheduled = true;
        DOVirtual.DelayedCall(0f, () => { _revealStartScheduled = false; TryPlayNextReveal(); }, false).SetUpdate(true);
    }

    private void TryPlayNextReveal()
    {
        if (_revealBusy || !IsOpen || _revealQ.Count == 0) return;
        SortRevealQueue();
        _revealBusy = true;
        PlayReveal(_revealQ.Dequeue());
    }

    // 낮은 지점부터, 같은 지점이면 '구간 개방'(markerPct<0)이 보상보다 먼저 나오도록 정렬.
    private void SortRevealQueue()
    {
        if (_revealQ.Count < 2) return;
        var list = new List<Reveal>(_revealQ);
        list.Sort((a, b) =>
        {
            if (a.order != b.order) return a.order.CompareTo(b.order);
            int ka = a.markerPct < 0 ? 0 : 1, kb = b.markerPct < 0 ? 0 : 1;
            return ka.CompareTo(kb);
        });
        _revealQ.Clear();
        foreach (var rv in list) _revealQ.Enqueue(rv);
    }

    private void PlayReveal(Reveal r)
    {
        const float PW = 780;   // 카드 폭(스윕 종료 x 계산용) - 빌드 값과 일치
        Color col = r.color;

        // 내용/색 세팅
        _rewardTitle.text = r.title; _rewardTitle.color = col;
        _rewardName.text = r.name; _rewardDesc.text = r.desc;
        _rewardIconBg.color = new Color(col.r, col.g, col.b, 0.14f);
        _rewardIconFrame.effectColor = col;
        if (_rewardTint != null) foreach (var im in _rewardTint) if (im != null) im.color = col;

        // 아이콘: 설비 보상=탑뷰와 동일한 설비 이미지(2개면 대각선 반반), 그 외=보석 엠블럼.
        SetRewardIcon(r.markerPct, col);

        // 초기 상태로 리셋
        _reward.SetActive(true); _reward.transform.SetAsLastSibling();
        KillRewardTweens();
        _rewardCg.alpha = 0f; _rewardCg.blocksRaycasts = false;
        _rewardCard.anchoredPosition = _cardHome - new Vector2(0, 46);   // 아래에서 위로 슬라이드
        _rewardCard.localScale = Vector3.one;
        _rewardIconTile.localScale = Vector3.one * 0.55f;
        _rewardSweep.anchoredPosition = new Vector2(-110, 0);            // 좌측 밖에서 대기(TL: 부호 그대로)
        SetAlpha(_rewardTitle, 0); SetAlpha(_rewardName, 0); SetAlpha(_rewardDesc, 0); SetAlpha(_rewardHint, 0);

        _rewardSeq?.Kill();
        _rewardSeq = DOTween.Sequence().SetUpdate(true);
        _rewardSeq.AppendInterval(0.7f);   // 게이지가 해당 지점까지 오르는 동안 대기
        _rewardSeq.AppendCallback(() =>
        {
            _rewardCg.blocksRaycasts = true;
            // 퍼센트 보상(설비 획득) 카드가 뜨는 순간 획득음.
            if (r.milestone) GameSfx.Play(SfxId.FacilityUnlockReveal);
            if (r.markerPct >= 0) FlashMarker(r.markerPct, col);
            // 카드: 아래->위 슬라이드
            _rewardCard.DOAnchorPos(_cardHome, 0.42f).SetEase(Ease.OutCubic).SetUpdate(true);
            // 아이콘 타일: 팝
            _rewardIconTile.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).SetDelay(0.12f);
            // 스윕 하이라이트: 좌->우 한 번 통과(카드 마스크로 양끝 클리핑)
            _rewardSweep.DOAnchorPos(new Vector2(PW + 20, 0), 0.7f).SetEase(Ease.OutSine).SetUpdate(true).SetDelay(0.1f);
            // 텍스트: 순차 페이드(원래 알파로 복원)
            FadeIn(_rewardTitle, 1f, 0.24f, 0.16f);
            FadeIn(_rewardName, 1f, 0.3f, 0.22f);
            FadeIn(_rewardDesc, 0.66f, 0.3f, 0.32f);
            FadeIn(_rewardHint, 0.42f, 0.3f, 0.5f);
        });
        _rewardSeq.Append(_rewardCg.DOFade(1f, 0.28f));
        // 자동 닫힘 없음 - 플레이어가 클릭(SkipReveal->CloseReveal)할 때까지 유지.
    }

    // 리빌 카드 아이콘 - 설비 보상이면 탑뷰와 동일한 설비 이미지(2개면 대각선 반반),
    //   비설비 보상(창고포트 증설·엔진·보급꾸러미 등)은 설비 사진과 다른 느낌으로 보석 엠블럼.
    // 세 형태를 씬에 다 만들어두고 여기서 켜고 끈다.
    private void SetRewardIcon(int pct, Color col)
    {
        var ids = TransmissionManager.Instance != null ? TransmissionManager.Instance.GetRewardFacilityIds(pct) : null;
        var db  = FacilityIconDatabase.Instance;

        bool tl = false, br = false, single = false;
        if (ids != null && db != null)
        {
            if (ids.Count >= 2)
            {
                // 2개 - "/" 대각선 기준 좌상 / 우하로 반반. 마스크 사이 간격이 구분선 역할.
                var a = db.GetIcon(ids[0]); var b = db.GetIcon(ids[1]);
                if (a != null && _riHalfTLIcon != null) { _riHalfTLIcon.sprite = a; tl = true; }
                if (b != null && _riHalfBRIcon != null) { _riHalfBRIcon.sprite = b; br = true; }
            }
            else if (ids.Count == 1)
            {
                var sp = db.GetIcon(ids[0]);
                if (sp != null && _riSingle != null) { _riSingle.sprite = sp; single = true; }
            }
        }
        bool gem = !tl && !br && !single;   // 설비 이미지가 없으면(예: 저장고) 보석 엠블럼으로 폴백

        if (_riHalfTL != null) _riHalfTL.SetActive(tl);
        if (_riHalfBR != null) _riHalfBR.SetActive(br);
        if (_riSingleGo != null) _riSingleGo.SetActive(single);
        if (_riGem != null) _riGem.SetActive(gem);
        if (gem && _riGemCoin != null) _riGemCoin.color = col;
    }

    // 등장 트윈 일괄 정리(재생 시작 전·닫을 때).
    private void KillRewardTweens()
    {
        if (_rewardCard != null) _rewardCard.DOKill();
        if (_rewardIconTile != null) _rewardIconTile.DOKill();
        if (_rewardSweep != null) _rewardSweep.DOKill();
        if (_rewardTitle != null) DOTween.Kill(_rewardTitle);
        if (_rewardName != null) DOTween.Kill(_rewardName);
        if (_rewardDesc != null) DOTween.Kill(_rewardDesc);
        if (_rewardHint != null) DOTween.Kill(_rewardHint);
    }

    private static void SetAlpha(TMP_Text t, float a) { if (t != null) { var c = t.color; c.a = a; t.color = c; } }
    // DOTween TMP 모듈 없이 텍스트 알파를 트윈(코어 DOTween.To). SetTarget 으로 DOKill 대상 지정.
    private Tweener FadeText(TMP_Text t, float to, float dur, float delay)
    {
        return DOTween.To(() => t.color.a, a => { var c = t.color; c.a = a; t.color = c; }, to, dur)
            .SetTarget(t).SetUpdate(true).SetDelay(delay);
    }
    private void FadeIn(TMP_Text t, float targetA, float dur, float delay) { if (t != null) FadeText(t, targetA, dur, delay); }

    private void FlashMarker(int pct, Color col)
    {
        foreach (var mk in _markers)
        {
            if (mk == null || mk.Pct != pct) continue;
            var rt = (RectTransform)mk.transform;
            rt.DOKill(); rt.localScale = Vector3.one;
            rt.DOScale(1.55f, 0.22f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad).SetUpdate(true);
            break;
        }
    }

    private void SkipReveal()
    {
        // 스크림 blocksRaycasts 는 등장 이후에만 true 라, 그 전엔 이 콜백이 오지 않는다.
        if (!_revealBusy) return;
        CloseReveal();
    }

    private void CloseReveal()
    {
        _rewardSeq?.Kill();                     // 등장/이전 닫힘 시퀀스 정리(OnComplete 미발생)
        KillRewardTweens();
        if (_rewardCg != null) _rewardCg.blocksRaycasts = false;   // 닫는 동안 추가 클릭 차단
        _rewardSeq = DOTween.Sequence().SetUpdate(true);
        _rewardSeq.Append(_rewardCg.DOFade(0f, 0.22f));
        _rewardSeq.Join(_rewardCard.DOAnchorPos(_cardHome - new Vector2(0, 24), 0.22f).SetEase(Ease.InSine));   // 아래로 내려가며 사라짐
        _rewardSeq.OnComplete(() =>
        {
            if (_reward != null) _reward.SetActive(false);
            _revealBusy = false;
            TryPlayNextReveal();               // 큐에 남은 다음 보상 재생
        });
    }

    // ── 게이지 이동 ───────────────────────────────────────────────────
    private void SetGauge(float to, bool animated, float from = -1)
    {
        if (from < 0) from = to;
        float tw = _trackW;
        if (!animated)
        {
            _fill.fillAmount = to / 100f;
            _node.anchoredPosition = new Vector2(tw * to / 100f, 0);
            _nodeLabel.text = Mathf.RoundToInt(to) + "%"; _rateBig.text = RateText(to);
            _rateLevelShown = to; SetRateTankLevel(to);
            RunSweep();
            return;
        }
        _fill.DOKill(); _node.DOKill();
        _fill.DOFillAmount(to / 100f, 0.9f).SetEase(Ease.OutQuint).SetUpdate(true);
        _node.DOAnchorPosX(tw * to / 100f, 0.9f).SetEase(Ease.OutQuint).SetUpdate(true);
        DOTween.To(() => from, v => { _nodeLabel.text = Mathf.RoundToInt(v) + "%"; _rateBig.text = RateText(v); _rateLevelShown = v; }, to, 0.9f)
            .SetEase(Ease.OutQuint).SetUpdate(true);
        RunSweep();
    }

    // 큰 숫자 + 살짝 작은 % (리치텍스트). 예: 50<size=45%>%</size>
    private static string RateText(float v) => $"{Mathf.RoundToInt(v)}<size=45%>%</size>";

    // 탱크 수위(전송률 높이) 즉시 반영(개장 시). 이후엔 Update 가 매 프레임 갱신.
    private void SetRateTankLevel(float rate) { if (_rateWave != null) RegenWater(Time.unscaledTime); }

    // 탱크 색(현재 구간 색)과 구간 라벨 갱신 - 구간이 바뀔 때만 필요. (텍스처는 알파만, 색은 RawImage.color)
    private void SetRateTankColor()
    {
        if (_rateWave == null) return;
        if (Mathf.RoundToInt(_rateLevelShown) >= 100)
            _rateWave.color = C("9AA6B0", 1f);                    // 가득 참 - 회색
        else
        {
            Color rc = RegionCol[(int)_m.Cur];
            _rateWave.color = new Color(rc.r, rc.g, rc.b, 1f);
        }
        if (_rateRegionLabel != null) _rateRegionLabel.text = Loc.Get(RegionKo[(int)_m.Cur]) + " " + Loc.Get("구간");
        if (_goalLabel != null)
        {
            int goal = TransmissionManager.Instance != null ? TransmissionManager.Instance.CurrentRegionGoal : 0;
            _goalLabel.text = string.Format(Loc.Get("목표 {0}%"), goal);
        }
    }

    // 물 텍스처 재생성 - 열(x)마다 여러 진행파를 합쳐 수면 행(s)을 구하고, 그 아래를 채운다.
    // 텍스처는 알파만(흰색), 색은 RawImage.color(구간 색). 표면 근처는 알파를 높여 밝은 수면.
    private void RegenWater(float t)
    {
        if (_rateWaveTex == null || _rateWavePx == null) return;
        // 100% 가득 차면 물결 없이 균일하게 꽉 채우고 색을 회색으로 (물 애니메이션 정지).
        if (Mathf.RoundToInt(_rateLevelShown) >= 100)
        {
            _rateWave.color = C("9AA6B0", 1f);
            for (int i = 0; i < _rateWavePx.Length; i++) _rateWavePx[i] = new Color(1f, 1f, 1f, 0.9f);
            _rateWaveTex.SetPixels(_rateWavePx); _rateWaveTex.Apply(false);
            return;
        }
        float level = Mathf.Clamp01(_rateLevelShown / 100f);
        float slosh = 1.2f * Mathf.Sin(t * 1.2f);
        // 전송 직후엔 진폭을 키우고 빠른 파를 더해 튀는 느낌(_splashT: 1->0 감쇠).
        float sp = _splashT;
        float ampBoost = 1f + sp * 2.2f;
        for (int x = 0; x < RWW; x++)
        {
            // 균일한 진행파(같은 파장 2주기)로 규칙적인 물결 + 전체 찰랑임(slosh).
            float wave = 3.2f * ampBoost * Mathf.Sin(2f * Mathf.PI * (x / (RWW * 0.5f)) - t * 1.3f);
            float splash = sp * 4.5f * Mathf.Sin(2f * Mathf.PI * (x / (RWW * 0.28f)) - t * 5f);
            float s = level * RWH + wave + splash + slosh;   // 수면 행
            for (int y = 0; y < RWH; y++)
            {
                float d = s - y;   // 수면 아래 깊이(>0) / 위(<0)
                float a;
                if (d >= 0) { float hi = Mathf.Clamp01(1f - d / 3f); a = Mathf.Lerp(0.5f, 0.85f, hi); }
                else a = Mathf.Clamp01(1f + d) * 0.5f;   // 위쪽 1px 소프트
                _rateWavePx[y * RWW + x] = new Color(1f, 1f, 1f, a);
            }
        }
        _rateWaveTex.SetPixels(_rateWavePx); _rateWaveTex.Apply(false);
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

        // 2) 각 패널이 순서대로 "로드되듯" 아래에서 살짝 올라오며 페이드 인.
        if (_bootPanels == null) return;
        for (int i = 0; i < _bootPanels.Length; i++)
        {
            var cg = _bootPanels[i]; if (cg == null) continue;
            var rt = (RectTransform)cg.transform;
            rt.DOKill(); cg.DOKill();
            rt.localScale = Vector3.one;                          // 접힘/확대 없음
            Vector2 home = i < _bootHome.Count ? _bootHome[i] : rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(0, -22f);    // 22px 아래에서 시작(TL: 아래=음의 y)
            cg.alpha = 0f;
            float delay = 0.12f + i * 0.13f;                      // 순차 스태거(하나씩 로드되는 느낌)
            cg.DOFade(1f, 0.3f).SetEase(Ease.OutSine).SetUpdate(true).SetDelay(delay);
            rt.DOAnchorPos(home, 0.42f).SetEase(Ease.OutCubic).SetUpdate(true).SetDelay(delay);
        }
    }

    private void KillAll()
    {
        if (_cg != null) _cg.DOKill(); if (_content != null) _content.DOKill();
        if (_fill != null) _fill.DOKill(); if (_node != null) _node.DOKill();
        if (_sweep != null) _sweep.DOKill();
        if (_nodePulse != null) { _nodePulse.rectTransform.DOKill(); _nodePulse.DOKill(); }
        if (_logDot != null) _logDot.DOKill();
        if (_cursorImg != null) _cursorImg.DOKill();
        if (_ghostFill != null) { _ghostFill.DOKill(); _ghostFill.gameObject.SetActive(false); }
        _ghostToPct = -1f;
        _splashT = 0f;
        _rewardSeq?.Kill(); _revealQ.Clear(); _revealBusy = false; _revealStartScheduled = false;
        KillRewardTweens();
        if (_reward != null) _reward.SetActive(false);
    }

    // 1920x1080 설계 레이아웃을 루트 캔버스 실제 크기에 맞춰 통째로 축소한다.
    // 호스트 캔버스의 CanvasScaler 설정이 우리 가정과 달라도 UI가 화면 밖으로 잘리지 않게 하는 안전장치.
    // min 비율 + 1 클램프라, 스케일러가 정상이면 (캔버스==1920x1080) s=1 로 완전히 동일하고,
    // 화면이 좁을 때만 축소한다(레터박스, 여백은 백드롭이 가림).
    //
    // 크기를 부모(그룹)가 아니라 루트 캔버스에서 직접 가져오는 이유:
    //   Canvas 아래 그룹(Panels/Overlays 등)이 전체화면 stretch 가 아니라 100x100 센터인 경우가 있어서,
    //   부모에 stretch 로 붙이면 화면 전체가 손톱만하게 줄어든다(예전에 CastGauge 가 이걸로 어긋났었다).
    private void FitContentToRoot()
    {
        if (_fitWrap == null || _root == null) return;
        var rootRT = _root.transform as RectTransform;
        if (rootRT == null) return;
        var cv = GetComponentInParent<Canvas>();
        if (cv != null && cv.rootCanvas != null && cv.rootCanvas.transform is RectTransform cvRT)
            rootRT.sizeDelta = cvRT.rect.size;
        Vector2 sz = rootRT.rect.size;
        if (sz.x <= 1f || sz.y <= 1f) return;   // 레이아웃이 아직 안 잡힘(0 크기) - 다음 기회에
        float s = Mathf.Min(Mathf.Min(sz.x / 1920f, sz.y / 1080f), 1f);
        _fitWrap.localScale = new Vector3(s, s, 1f);
        _lastFitW = Screen.width; _lastFitH = Screen.height;
    }

    // ── 툴팁 (마커 / 키트 행 호버 콜백) ───────────────────────────────
    public void ShowTooltip(int pct, RectTransform marker)
    {
        var st = _m.MarkerState(pct);
        Color col = st == MState.Done ? Success : st == MState.Next ? Accent : C("E2EDF8", 0.35f);
        // 보상 이름은 이미 획득(Done)했거나 바로 다음 구간(Next)일 때만 공개. 그 이후(Locked)는 ??? 로 가림.
        string name = st == MState.Locked ? "???" : _m.RewardName(pct);
        ShowTooltipCommon(col, $"{pct}" + Loc.Get("% 지점 보상"), name, _m.TooltipStatus(pct, st), marker);
    }

    /// <summary>키트 행 호버(TransmissionKitRow 가 부른다) - 사용 가능하면 상승치를, 불가면 사유를.</summary>
    public void ShowKitTooltip(int index, RectTransform anchor)
    {
        if (index < 0 || index >= _kitRows.Count) return;
        var k = _kitRows[index].kit;
        if (k == null) return;
        bool usable = _m.Usable(k);
        Color col = usable ? Accent : Danger;
        string state = usable ? string.Format(Loc.Get("전송 시 +{0}% 상승"), k.gain) : _m.UnusableReason(k);
        ShowTooltipCommon(col, k.isBoss ? Loc.Get("보스 충전키트") : Loc.Get("일반 충전키트"), k.name, state, anchor);
    }

    // 공통 툴팁 표시 - 색/제목/이름/상태 세팅 후 앵커 위로 떠오르며 페이드 인.
    private void ShowTooltipCommon(Color col, string title, string name, string state, RectTransform anchor)
    {
        if (_tooltip == null) return;
        if (_ttBox != null) _ttBox.color = C("101A2D", 0.98f);
        if (_ttOutline != null) _ttOutline.effectColor = col;
        _ttTitle.color = col; _ttTitle.text = title;
        _ttName.text = name;
        _ttState.color = col; _ttState.text = state;
        var rt = (RectTransform)_tooltip.transform;
        Vector2 target = ContentPointFromMarker(anchor);
        rt.DOKill(); _ttCg.DOKill();
        _tooltip.transform.SetAsLastSibling();
        _tooltip.SetActive(true);
        // 아래에서 살짝 올라오며 페이드 인 (timeScale 0 에서도 동작하도록 SetUpdate(true))
        rt.anchoredPosition = target + new Vector2(0, -12f);
        _ttCg.alpha = 0f;
        rt.DOAnchorPos(target, 0.22f).SetEase(Ease.OutCubic).SetUpdate(true);
        _ttCg.DOFade(1f, 0.18f).SetUpdate(true);
    }

    public void HideTooltip()
    {
        if (_tooltip == null) return;
        ((RectTransform)_tooltip.transform).DOKill(); if (_ttCg != null) _ttCg.DOKill();
        _tooltip.SetActive(false);
    }

    private Vector2 ContentPointFromMarker(RectTransform marker)
    {
        // content(top-left pivot) 기준 마커 중심 위 46px
        Vector3 w = marker.TransformPoint(Vector3.zero);
        Vector3 l = _content.InverseTransformPoint(w);
        return new Vector2(l.x, l.y + 46);
    }

    // =====================================================================
    // 모델
    // =====================================================================
    private enum MState { Done, Next, Locked }

    // UI 표시용 키트 뷰모델 - 실제 정의(ChargedKitDef)와 인벤토리 보유수를 담는다.
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

    // 행 <-> 키트 짝. 화면 조각은 전부 TransmissionKitRow 가 들고 있다.
    private class KitRow { public Kit kit; public TransmissionKitRow ui; }

    // TransmissionManager(로직·인벤토리·저장)를 감싸는 어댑터. UI는 이 Model만 본다.
    private class Model
    {
        private static TransmissionManager Mgr => TransmissionManager.Instance;

        public string selectedId;
        public readonly List<Kit> kits = new();
        public readonly List<string> logs = new();

        public float progress => Mgr != null ? Mgr.TransmissionRate : 0f;
        public TransmissionRegion Cur => Mgr != null ? Mgr.CurrentRegion : TransmissionRegion.Nature;

        // 인벤토리에 실제 보유한 키트로 목록 재구성. 이름은 ItemData 시트에서 가져온다.
        public void RebuildKits()
        {
            kits.Clear();
            if (Mgr != null)
                foreach (var d in Mgr.GetOwnedKits())
                    kits.Add(new Kit
                    {
                        def = d, id = d.itemId.ToString(), name = Mgr.GetKitName(d),
                        region = d.region, isBoss = d.isBoss, gain = d.ratePercent,
                        qty = Mgr.GetOwnedCount(d.itemId)
                    });
            if (selectedId != null && Selected() == null) selectedId = null;  // 선택 키트가 소진됐으면 해제
        }

        public Kit Selected() { foreach (var k in kits) if (k.id == selectedId) return k; return null; }

        // 사용 가능 여부·예상 전송률·전송 실행은 전부 매니저에 위임(구간/상한/보스 규칙은 매니저가 판정).
        public bool Usable(Kit k)
        {
            if (Mgr == null || k == null) return false;
            return Mgr.CanTransmit(k.def, out _);
        }
        public int Target(Kit k) => (Mgr != null && k != null) ? Mgr.GetProjectedRate(k.def) : Mathf.RoundToInt(progress);

        // 사용 불가 사유(툴팁용) - Usable() 판정과 같은 순서로 첫 번째 걸리는 이유를 문장으로.
        public string UnusableReason(Kit k)
        {
            if (Mgr == null || k == null) return Loc.Get("사용 불가");
            if (progress >= TransmissionManager.MaxRate) return Loc.Get("전송률 100% · 더 올릴 수 없음");
            if (k.region != Cur) return string.Format(Loc.Get("다른 지역 키트 · 현재 {0} 구간에서 사용 불가"), Loc.Get(RegionKo[(int)Cur]));
            if (!k.isBoss && progress >= Mgr.CurrentRegionNormalCap) return string.Format(Loc.Get("일반 상한 {0}% 도달 · 보스 키트 필요"), Mgr.CurrentRegionNormalCap);
            if (k.qty <= 0) return Loc.Get("보유 수량 없음");
            if (Target(k) <= Mathf.RoundToInt(progress)) return Loc.Get("전송률 상승 없음");
            return Loc.Get("사용 불가");
        }
        public bool Send(Kit k)
        {
            if (Mgr == null || k == null) return false;
            return Mgr.TryTransmit(k.def.itemId);
        }

        public MState MarkerState(int pct)
        {
            if (progress >= pct) return MState.Done;
            // 다음 = 현재 전송률을 초과하는 첫 보상 마일스톤. 마일스톤 간격이 5/10/25로 불균등이라
            // 10% 격자로 계산하면 5% 지점(15, 25, 75 등)을 건너뛴다. 공유 소스에서 직접 찾는다.
            int next = int.MaxValue;
            foreach (int m in TransmissionManager.RewardMilestones)
                if (m > progress && m < next) next = m;
            return pct == next ? MState.Next : MState.Locked;
        }

        // ── 마일스톤 보상 문구 ─────────────────────────────────────────────
        // ★설비 이름을 손으로 적지 마라. 매니저의 해금 목록(GetRewardFacilityIds)과 FacilityData 시트에서
        //   그때그때 만든다. 예전엔 여기에 % -> 이름 표를 손으로 유지했는데, 마일스톤을 재배치하자마자
        //   전부 어긋나서 "10% 에 용해로"(실제로는 5% 에 이미 받음) 처럼 없는 보상을 예고했다.
        //   표를 다시 두면 마일스톤을 옮길 때마다 또 어긋난다. 설비 외 보상만 아래 표로 유지한다
        //   (매니저 GrantMilestoneRewards 의 switch / ShipPartMilestones 와 짝).

        // 이 마일스톤이 해금하는 설비 이름. 없으면 null.
        private static string FacilityRewardNames(int pct)
        {
            var ids = Mgr != null ? Mgr.GetRewardFacilityIds(pct) : null;
            if (ids == null || ids.Count == 0) return null;
            var names = new List<string>(ids.Count);
            foreach (int id in ids)
                if (GameDataHolder.I != null && GameDataHolder.I.FacilityData.TryGet(id.ToString(), out var d)
                    && !string.IsNullOrEmpty(d.facilityName))
                    names.Add(Loc.Get(d.facilityName));
            return names.Count > 0 ? string.Join(" / ", names) : null;
        }

        // 설비별 한 줄 부연(있는 것만). 해금 문장 뒤에 붙는다. 설비 id 기준이라 % 를 옮겨도 따라온다.
        private static string FacilityHint(int facilityId) => facilityId switch
        {
            3 => Loc.Get("이제 충전 키트를 직접 제작할 수 있습니다."),   // 첫 전송 보상 = 부트스트랩이라 설명이 필요
            _ => null,
        };

        // 창고 출력 포트 상한 증가분 - 매니저 램프(WarehousePortLimitAt)에서 직전 마일스톤과의 차로 뽑는다.
        //   램프는 15% 부터 거의 매 마일스톤 오르는데 예전 표는 70/80/90 만 적어뒀고 그 수치마저 틀렸다.
        private static int PortLimitGain(int pct)
        {
            int prev = 0;
            foreach (int m in TransmissionManager.RewardMilestones) { if (m >= pct) break; prev = m; }
            return TransmissionManager.WarehousePortLimitAt(pct) - TransmissionManager.WarehousePortLimitAt(prev);
        }

        private static string ExtraRewardName(int pct) => pct switch
        {
            20 => Loc.Get("귀환석 Lv.1"), 25 => Loc.Get("선체 보강재"),
            40 => Loc.Get("귀환석 Lv.2"), 50 => Loc.Get("동력 안정기"),
            70 => Loc.Get("귀환석 Lv.3"), 75 => Loc.Get("우주선 엔진"),
            90 => Loc.Get("앰플 꾸러미 / 코어 키트 V"),
            _  => null,
        };

        private static string ExtraRewardDesc(int pct) => pct switch
        {
            20 => Loc.Get("귀환석 Lv.1(쿨타임 15분)을 획득했습니다."),
            25 => Loc.Get("우주선 선체 보강재를 확보했습니다."),
            40 => Loc.Get("귀환석 Lv.2(쿨타임 10분)로 강화되었습니다."),
            50 => Loc.Get("우주선 동력 안정기를 확보했습니다."),
            70 => Loc.Get("귀환석 Lv.3(쿨타임 5분)로 강화되었습니다."),
            75 => Loc.Get("우주선 엔진을 확보했습니다."),
            90 => Loc.Get("앰플 꾸러미(대량)와 코어 키트 V x5를 받았습니다."),
            _  => null,
        };

        private static void AddPart(List<string> list, string s) { if (!string.IsNullOrEmpty(s)) list.Add(s); }

        public string RewardName(int pct)
        {
            if (pct >= TransmissionManager.MaxRate) return Loc.Get("전송 완료");
            var parts = new List<string>(3);
            AddPart(parts, FacilityRewardNames(pct));
            AddPart(parts, ExtraRewardName(pct));
            // 창고 출력 포트 상한은 15% 부터 거의 매 구간 올라서, 이름에 늘 붙이면 리빌 카드 제목이 넘친다.
            //   다른 보상이 하나도 없는 구간(60/80)에서만 대표 이름으로 쓴다. 상세는 RewardDesc 가 항상 알려준다.
            int port = PortLimitGain(pct);
            if (parts.Count == 0 && port > 0) parts.Add(string.Format(Loc.Get("창고 출력 포트 상한 +{0}"), port));
            return parts.Count > 0 ? string.Join(" / ", parts) : Loc.Get("전송 완료");
        }

        public string RewardDesc(int pct)
        {
            if (pct >= TransmissionManager.MaxRate) return Loc.Get("시간에너지 전송 100% — 탈출(엔딩) 조건을 달성했습니다!");
            var parts = new List<string>(4);
            string fac = FacilityRewardNames(pct);
            if (!string.IsNullOrEmpty(fac))
            {
                // 느낌표 버전은 FacilityUnlockManager 가 이미 쓰고 시트에도 번역돼 있다. 새 키를 만들지 말고 재사용한다.
                parts.Add(string.Format(Loc.Get("{0} 설비를 해금했습니다!"), fac));
                var ids = Mgr != null ? Mgr.GetRewardFacilityIds(pct) : null;
                if (ids != null) foreach (int id in ids) AddPart(parts, FacilityHint(id));
            }
            AddPart(parts, ExtraRewardDesc(pct));
            int port = PortLimitGain(pct);
            if (port > 0) parts.Add(string.Format(Loc.Get("창고 출력 포트 건설 수가 {0} 늘어났습니다."), port));
            return parts.Count > 0 ? string.Join(" ", parts) : Loc.Get("전송 보상을 획득했습니다.");
        }

        public string TooltipStatus(int pct, MState st)
        {
            if (st == MState.Done) return Loc.Get("획득 완료");
            if (st == MState.Next)
            {
                bool boundary = pct % 25 == 0; // 25/50/75/100
                string grade = boundary ? Loc.Get("보스") : Loc.Get("일반");
                return string.Format(Loc.Get("필요: {0} {1} 충전키트"), Loc.Get(RegionKo[(int)Cur]), grade);
            }
            return Loc.Get("??? (도달 시 공개)");
        }

        public string StatusText()
        {
            if (Mgr == null) return Loc.Get("전송 시스템 대기 중");
            int p = Mgr.TransmissionRate;
            if (p >= TransmissionManager.MaxRate) return Loc.Get("전송률 100% 달성 — 엔딩 조건 충족");
            return string.Format(Loc.Get("현재 구간 {0} / 일반 상한 {1}% / 목표 {2}%"), Loc.Get(RegionKo[(int)Cur]), Mgr.CurrentRegionNormalCap, Mgr.CurrentRegionGoal);
        }
    }

    private string KitMeta(Kit k)
    {
        string g = k.isBoss ? Loc.Get("보스") : Loc.Get("일반");
        string gradeHex = k.isBoss ? "F2C14E" : "8FB6C9";           // 보스=골드 / 일반=차분한 블루그레이
        string regionHex = ColorUtility.ToHtmlStringRGB(RegionCol[(int)k.region]);
        string meta = $"<color=#{regionHex}>{Loc.Get(RegionKo[(int)k.region])} {Loc.Get("지역")}</color> / <color=#{gradeHex}>{g} {Loc.Get("등급")}</color>";
        if (k.region != _m.Cur) meta += $"  <color=#F27059>{Loc.Get("다른 지역 / 이 구간 사용 불가")}</color>";
        else if (k.qty <= 0) meta += $"  <color=#F27059>{Loc.Get("수량 없음")}</color>";
        return meta;
    }

    // =====================================================================
    // 공용 헬퍼 (배경 장식 생성에도 쓰이므로 런타임에 남는다)
    // =====================================================================
    private GameObject NewGO(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return g; }
    private RectTransform NewRT(string n, GameObject p) { var g = NewGO(n, p.transform); return g.GetComponent<RectTransform>(); }

    // 좌상단 원점 배치(y 는 아래로 증가). 이 화면의 모든 절대좌표가 이 기준이다.
    private RectTransform TL(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
    }

    private Image Img(string n, Transform p, float x, float y, float w, float h, Color col, Sprite spr = null)
    {
        var go = NewGO(n, p); TL(go, x, y, w, h);
        var im = go.AddComponent<Image>(); im.color = col; if (spr != null) { im.sprite = spr; im.type = Image.Type.Sliced; }
        return im;
    }
    private Image Img(string n, GameObject p, float x, float y, float w, float h, Color col, Sprite spr = null) => Img(n, p.transform, x, y, w, h, col, spr);

    private static Color C(string hex, float a = 1f)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) { c.a = a; return c; }
        return Color.white;
    }

    // ── 이 화면 전용 절차 텍스처 (UISpriteFactory 에 없는 모양) ───────
    private static Sprite _hgrad, _radial, _scan, _tri, _sweep2, _vig;

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

    // 반반 대각 마스크("/" 대각선 기준 좌상 / 우하). 두 절반 사이 간격으로 구분선 효과. 캐시.
    private static Sprite _triTL, _triBR;
    private static Sprite TriangleSprite(bool topLeft)
    {
        if (topLeft && _triTL != null) return _triTL;
        if (!topLeft && _triBR != null) return _triBR;
        const int S = 64, gap = 2;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        var on = new Color32(255, 255, 255, 255); var off = new Color32(255, 255, 255, 0);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int d = y - x;   // >0 = 좌상(/ 대각 기준), <0 = 우하
                bool onHalf = topLeft ? (d > gap) : (d < -gap);
                px[y * S + x] = onHalf ? on : off;
            }
        tex.SetPixels32(px); tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        if (topLeft) _triTL = sp; else _triBR = sp;
        return sp;
    }

}
