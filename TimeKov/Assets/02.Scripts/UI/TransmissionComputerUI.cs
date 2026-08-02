// =====================================================================
// TransmissionComputerUI.cs  - 시간에너지 전송 컴퓨터
// 기준 캔버스 1920x1080 절대좌표. 자체 레터박스 래퍼(FitWrap)로 어떤 해상도에서도 안 잘린다.
// 공개 API(Instance/GetOrCreate/Open/HidePanel/Close/LastCloseFrame/IsOpen)는
// 기존 GameUIController / TransmissionComputerTerminal 연동을 위해 유지.
//
// [08-02] 화면 구조를 씬 실물로 전환. 로컬라이징 준비(에디터에 없는 UI엔 컴포넌트를 못 붙인다).
//   - 레이아웃 생성 코드는 #if UNITY_EDITOR 로 남겨 '메뉴에서 그 자리서 실행' 한다.
//     절대좌표가 수백 개라 손으로 옮겨 적으면 오타 하나가 조용한 레이아웃 붕괴가 된다.
//   - 실행 시 남는 생성물: 배경 장식(격자/링/브래킷 69개, 글자가 없어 씬에 두면 하이어라키만 더러워짐),
//     키트 행/마커(개수가 데이터라 템플릿 복제), 물결 텍스처, 무한 트윈.
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _warnedMissing = false;

        if (_root == null || _content == null || _trackRT == null)
        {
            Debug.LogError("[TransmissionUI] 씬 참조가 비어 있다. 메뉴 Tools/TIMEKOV/전송 컴퓨터 UI 생성 을 실행해라.");
            return;
        }

        _m = new Model();
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

    // 마커 - 보상 걸린 마일스톤마다(불균등: 5/15/25/75 등). TransmissionManager 와 공유.
    private void BuildMarkers()
    {
        if (_markerTemplate == null) return;
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
        _selName.text = k != null ? k.name : "없음";
        _selMeta.text = k != null ? KitMeta(k) : "목록에서 키트를 클릭";
        // 예상 전송률
        string pv; Color pc; bool active;
        if (k == null) { pv = "키트를 선택하세요"; pc = C("E8F2FB", 0.4f); active = false; }
        else if (!_m.Usable(k)) { pv = "전송 불가"; pc = C("E8F2FB", 0.4f); active = false; }
        else { int t = _m.Target(k); int d = t - Mathf.RoundToInt(_m.progress); pv = $"전송 시 {t}% (+{d})"; pc = Accent; active = d > 0; }
        _previewVal.text = pv; _previewVal.color = pc;
        _sendBtnImg.color = active ? C("47C4F0") : C("47C4F0", 0.4f);
        _sendBtnCg.alpha = active ? 1f : 0.5f; _sendBtnCg.blocksRaycasts = active; _sendBtnCg.interactable = active;
        UpdateGhostPreview(active ? k : null);
    }

    // 각 구간 라벨은 해당 구간에 도달(전송률 >= 구간 시작 %)해야 공개. 그 전엔 ??? + 흐리게.
    private void RefreshLegend()
    {
        int rate = _m != null ? Mathf.RoundToInt(_m.progress) : 0;
        int cur = _m != null ? (int)_m.Cur : -1;   // 지금 활성 구간 - 라벨을 굵게 + 뒤에 하이라이트
        for (int i = 0; i < 4; i++)
        {
            if (_legendLabels[i] == null) continue;
            bool reached = rate >= i * 25;
            bool isCur = i == cur && reached;
            if (reached)
            {
                _legendLabels[i].text = $"{RegionKo[i]} {i * 25}-{(i + 1) * 25}";
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
        if (_subLabel != null) _subLabel.text = $"기지 전송 컴퓨터     현재 구간 {RegionKo[(int)_m.Cur]}";
        SetRateTankColor();
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
        _m.logs.Add($"{kn} x1 전송 / {from}% -> {Mathf.RoundToInt(_m.progress)}%");
        RefreshLog();
    }

    // ── TransmissionManager 이벤트 ────────────────────────────────────
    // 씬에 매니저가 없으면(다른 씬에서 열릴 때 등) 런타임 생성 - 기본 키트 정의 사용.
    private static void EnsureManager()
    {
        if (TransmissionManager.Instance != null) return;
        new GameObject("TransmissionManager").AddComponent<TransmissionManager>();
        Debug.LogWarning("[TransmissionUI] 씬에 TransmissionManager가 없어 런타임 생성했습니다.");
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
        _m.logs.Add($"{pct}% 구간 보상 획득"); if (IsOpen) RefreshLog();
        if (!IsOpen) return;   // 닫혀있으면 연출 스킵(스테일 큐 방지)
        _revealQ.Enqueue(new Reveal
        {
            title = $"구간 달성 {pct}%", name = pct >= 100 ? _m.RewardName(pct) : $"{_m.RewardName(pct)} 획득!",
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
        if (_rateRegionLabel != null) _rateRegionLabel.text = $"{RegionKo[(int)_m.Cur]} 구간";
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
        ShowTooltipCommon(col, $"{pct}% 지점 보상", name, _m.TooltipStatus(pct, st), marker);
    }

    /// <summary>키트 행 호버(TransmissionKitRow 가 부른다) - 사용 가능하면 상승치를, 불가면 사유를.</summary>
    public void ShowKitTooltip(int index, RectTransform anchor)
    {
        if (index < 0 || index >= _kitRows.Count) return;
        var k = _kitRows[index].kit;
        if (k == null) return;
        bool usable = _m.Usable(k);
        Color col = usable ? Accent : Danger;
        string state = usable ? $"전송 시 +{k.gain}% 상승" : _m.UnusableReason(k);
        ShowTooltipCommon(col, k.isBoss ? "보스 충전키트" : "일반 충전키트", k.name, state, anchor);
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
        public readonly List<string> logs = new() { "UPLINK 연결됨" };

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
            if (Mgr == null || k == null) return "사용 불가";
            if (progress >= TransmissionManager.MaxRate) return "전송률 100% · 더 올릴 수 없음";
            if (k.region != Cur) return $"다른 지역 키트 · 현재 {RegionKo[(int)Cur]} 구간에서 사용 불가";
            if (!k.isBoss && progress >= Mgr.CurrentRegionNormalCap) return $"일반 상한 {Mgr.CurrentRegionNormalCap}% 도달 · 보스 키트 필요";
            if (k.qty <= 0) return "보유 수량 없음";
            if (Target(k) <= Mathf.RoundToInt(progress)) return "전송률 상승 없음";
            return "사용 불가";
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

        // 실제 지급 보상(TransmissionManager.GrantMilestoneRewards)과 일치해야 함(2026-07-24 확정표).
        public string RewardName(int pct) => pct switch
        {
            5  => "시간에너지 합성기", 10 => "용해로", 15 => "창고 출력 포트",
            20 => "저장고 / 귀환석 Lv.1", 25 => "선체 보강재", 30 => "코어 합성기",
            40 => "귀환석 Lv.2", 50 => "동력 안정기", 60 => "생체 분리기 / 에너지 변환기",
            70 => "창고포트 상한 +1 / 귀환석 Lv.3", 75 => "우주선 엔진", 80 => "창고포트 상한 +3",
            90 => "창고포트 상한 +2 / 앰플 꾸러미 / 코어 키트 V",
            _ => "전송 완료"
        };

        public string RewardDesc(int pct) => pct switch
        {
            5  => "시간에너지 합성기 설비를 해금했습니다. 이제 충전 키트를 직접 제작할 수 있습니다.",
            10 => "용해로 설비를 해금했습니다.",
            15 => "창고 출력 포트 설비를 해금했습니다.",
            20 => "저장고 해금 + 귀환석 Lv.1(쿨타임 15분)을 획득했습니다.",
            25 => "우주선 선체 보강재를 확보했습니다.",
            30 => "코어 합성기 설비를 해금했습니다.",
            40 => "귀환석 Lv.2(쿨타임 10분)로 강화되었습니다.",
            50 => "우주선 동력 안정기를 확보했습니다.",
            60 => "생체 분리기, 에너지 변환기 설비를 해금했습니다.",
            70 => "창고 출력 포트 건설 수 +1 + 귀환석 Lv.3(쿨타임 5분)로 강화되었습니다.",
            75 => "우주선 엔진을 확보했습니다.",
            80 => "창고 출력 포트 건설 수가 3 늘어났습니다.",
            90 => "창고 출력 포트 건설 수 +2 + 앰플 꾸러미(대량) + 코어 키트 V x5를 받았습니다.",
            _ => "시간에너지 전송 100% - 탈출(엔딩) 조건을 달성했습니다!"
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
            if (p >= TransmissionManager.MaxRate) return "전송률 100% 달성 - 엔딩 조건 충족";
            return $"현재 구간 {RegionKo[(int)Cur]} / 일반 상한 {Mgr.CurrentRegionNormalCap}% / 목표 {Mgr.CurrentRegionGoal}%";
        }
    }

    private string KitMeta(Kit k)
    {
        string g = k.isBoss ? "보스" : "일반";
        string gradeHex = k.isBoss ? "F2C14E" : "8FB6C9";           // 보스=골드 / 일반=차분한 블루그레이
        string regionHex = ColorUtility.ToHtmlStringRGB(RegionCol[(int)k.region]);
        string meta = $"<color=#{regionHex}>{RegionKo[(int)k.region]} 지역</color> / <color=#{gradeHex}>{g} 등급</color>";
        if (k.region != _m.Cur) meta += "  <color=#F27059>다른 지역 / 이 구간 사용 불가</color>";
        else if (k.qty <= 0) meta += "  <color=#F27059>수량 없음</color>";
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

#if UNITY_EDITOR
    // =====================================================================
    // 에디터 전용 - 화면 실물 생성
    //
    // 절대좌표가 수백 개라 이 코드를 빌더로 "옮겨 적으면" 오타 하나가 조용한 레이아웃 붕괴가 된다.
    // 그래서 원래 생성 코드를 여기 그대로 두고, 에디터 메뉴가 이 오브젝트 위에서 실행시킨다.
    // 그러면 _rateBig = Txt(...) 같은 대입이 그대로 직렬화된다(참조를 손으로 연결할 필요가 없다).
    // 빌드에는 안 실린다.
    // =====================================================================
    private TMP_FontAsset _kr, _mono;
    private readonly List<CanvasGroup> _bootBuild = new();

    public void EditorBuild()
    {
        _kr = ResolveFont(krFont, "Pretendard-SemiBold", "Pretendard", "남양주", "GabiaMaeumgyeol");
        _mono = ResolveFont(monoFont, "JetBrains", "Rajdhani-SemiBold", "Rajdhani", null) ?? _kr;
        _bootBuild.Clear();
        Build();
        _bootPanels = _bootBuild.ToArray();
    }

    private void Build()
    {
        // 이 컴포넌트가 붙은 오브젝트(이미 Canvas 아래)가 부모다.
        // 크기는 부모 그룹이 아니라 루트 캔버스를 따라간다(FitContentToRoot 가 실행 시 다시 맞춘다).
        _root = NewGO("Panel", transform);
        var rootRT = (RectTransform)_root.transform;
        rootRT.anchorMin = rootRT.anchorMax = rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero; rootRT.sizeDelta = new Vector2(1920, 1080);
        // 불투명 백드롭. raycast 차단 + 화면비가 16:9가 아닐 때 생기는 레터박스 여백을
        // 뒤 게임화면 대신 어둡게 가린다(모달이라 어차피 게임은 정지).
        _root.AddComponent<Image>().color = C("040810", 1f);
        _cg = _root.AddComponent<CanvasGroup>();

        // 해상도/호스트 캔버스 스케일러와 무관하게 1920x1080 레이아웃 전체가 항상 화면 안에 들어오도록,
        // 루트 실제 크기에 맞춰 통째로 축소(레터박스)하는 래퍼. _content.localScale 은 열기/닫기 애니가
        // 쓰므로 건드리지 않고 이 래퍼 스케일만 조절한다.
        _fitWrap = NewRT("FitWrap", _root);
        _fitWrap.anchorMin = _fitWrap.anchorMax = _fitWrap.pivot = new Vector2(0.5f, 0.5f);
        _fitWrap.anchoredPosition = Vector2.zero; _fitWrap.sizeDelta = new Vector2(1920, 1080);

        _content = TL(NewGO("Content", _fitWrap), 0, 0, 1920, 1080);

        BuildBackground();
        BuildHeader();
        BuildProgress();
        BuildBody();
        BuildFooter();
        BuildTooltip();
        BuildOverlay();
        BuildRewardOverlay();

        // 전체화면 불투명 백드롭이라 켜둔 채로는 씬의 다른 UI 를 편집할 수 없다.
        // 레이아웃을 눈으로 보려면 하이어라키에서 Panel 을 잠깐 켜면 된다.
        _root.SetActive(false);
    }

    // ── 배경 ──────────────────────────────────────────────────────────
    private void BuildBackground()
    {
        _bgRadial = Img("BG", _content, 0, 0, 1920, 1080, Color.white, RadialTex());
        _bgRadial.raycastTarget = false;
        // 격자/크로노링/코너브래킷 69개는 실행 시 여기에 담는다(글자가 없어 씬에 두면 하이어라키만 더러워짐).
        _decorRoot = TL(NewGO("Decor", _content), 0, 0, 1920, 1080);
    }

    // ── 헤더 ──────────────────────────────────────────────────────────
    private void BuildHeader()
    {
        // 좌측 그룹
        Img("sysDot", _content, 88, 62, 8, 8, Accent, UISpriteFactory.RoundedRect(16, 4)).raycastTarget = false;
        Txt("sys", _content, 108, 58, 600, 18, "TIMEKOV // TRANSFER TERMINAL", _mono, 14, C("4CC9F7", 0.75f), TextAlignmentOptions.Left, 3);
        Txt("title", _content, 86, 84, 900, 62, "시간에너지 전송", _kr, 48, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _subLabel = Txt("sub", _content, 88, 156, 900, 26, "기지 전송 컴퓨터     현재 구간 설원", _kr, 19, C("E8F2FB", 0.55f), TextAlignmentOptions.Left);

        // 우측 전송률 카드 = 액체 탱크(전송률만큼 구간 색이 차오르고 표면이 물결친다)
        float cw = 250, cx = 1832 - cw, cy = 40, ch = 182;
        var card = Img("rateCard", _content, cx, cy, cw, ch, C("0E1728", 0.94f), UISpriteFactory.RoundedRect(48, 16));
        Outline(card.gameObject, C("4CC9F7", 0.25f));
        RegisterBootPanel(card.rectTransform);   // 순차 오픈 애니 첫 번째 대상
        // 라운드 마스크 - 탱크가 카드 모서리 안쪽에서만 차오르게 클리핑
        var mask = card.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;

        // 물 - RawImage + 매 프레임 재생성 텍스처. 표면을 여러 진행파의 합으로 실제 계산.
        var waveGo = NewGO("rateWave", card.transform); Stretch(waveGo);
        _rateWave = waveGo.AddComponent<RawImage>();
        _rateWave.color = C("5BC7E8", 1f); _rateWave.raycastTarget = false;

        Txt("rateLbl", card.transform, 18, 16, cw - 36, 16, "현재 전송률", _mono, 12, C("E8F2FB", 0.5f), TextAlignmentOptions.Left, 3);
        _rateBig = Txt("rateBig", card.transform, 0, 50, cw, 92, "42%", _mono, 74, TextBright, TextAlignmentOptions.Center, 0, FontStyles.Bold);
        // 하단 행: 구간 이름(좌) / 목표 칩(우)
        _rateRegionLabel = Txt("rcBottom", card.transform, 18, ch - 30, cw - 36 - 74, 20, "설원 구간", _mono, 12, C("E8F2FB", 0.6f), TextAlignmentOptions.Left);
        var goalChip = Img("goalChip", card.transform, cw - 18 - 66, ch - 32, 66, 22, C("5BC7E8", 0f), UISpriteFactory.RoundedRect(40, 11));
        Outline(goalChip.gameObject, C("5BC7E8", 0.4f));
        Txt("goalTxt", goalChip.transform, 0, 0, 66, 22, "목표 50%", _mono, 12, AccentSoft2, TextAlignmentOptions.Center);
    }

    // ── 진행 바 패널 ──────────────────────────────────────────────────
    private void BuildProgress()
    {
        var panel = Panel("progressPanel", 88, 264, 1744, 240);
        // 헤더 행
        Txt("pHdr", panel.transform, 44, 20, 300, 18, "TRANSFER PROGRESS", _mono, 13, Accent, TextAlignmentOptions.Left, 3);
        Img("pLine", panel.transform, 240, 28, 1744 - 44 - 240 - 120, 1, C("4CC9F7", 0.25f)).raycastTarget = false;
        Txt("p100", panel.transform, 1744 - 44 - 110, 20, 110, 18, "0 - 100%", _mono, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Right);

        // 바 트랙
        float tx = 44, ty = 66, tw = 1744 - 88, th = 54;
        var track = TL(NewGO("track", panel.transform), tx, ty, tw, th);
        _trackRT = track;
        // 바 본체 - 바깥 4모서리만 라운드(마스크). 안쪽 구간/눈금/채움은 전부 각지게.
        var body = TL(NewGO("barBody", track), 0, 0, tw, th);
        var bodyImg = body.gameObject.AddComponent<Image>();
        bodyImg.sprite = UISpriteFactory.RoundedRect(48, 12); bodyImg.type = Image.Type.Sliced; bodyImg.raycastTarget = false;
        var bodyMask = body.gameObject.AddComponent<Mask>(); bodyMask.showMaskGraphic = false;

        // 구간 배경 4등분 (전부 각진 사각 - 바깥 라운드는 body 마스크가 처리)
        for (int i = 0; i < 4; i++)
        {
            var seg = Img($"seg{i}", body.gameObject, i * tw / 4f, 0, tw / 4f, th,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.15f));
            seg.raycastTarget = false;
        }
        // 채움 (마스크 + 그라데이션 이미지, fillAmount 로 클리핑). body 마스크 안이라 왼쪽 끝도 라운드로 잘림.
        var fillGo = TL(NewGO("fill", body), 0, 0, tw, th);
        fillGo.gameObject.AddComponent<RectMask2D>();
        _fill = Img2(NewGO("fillImg", fillGo), HGrad());
        Stretch(_fill.gameObject); _fill.type = Image.Type.Filled; _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left; _fill.fillAmount = 0f; _fill.raycastTarget = false;   // Open 시 SetGauge 로 채움
        // 스윕 하이라이트 - fill 마스크 내부에서 이동
        _sweep = TL(NewGO("sweep", fillGo), 0, 0, 70, th);
        _sweepImg = _sweep.gameObject.AddComponent<Image>(); _sweepImg.sprite = SweepTex(); _sweepImg.color = Color.white; _sweepImg.raycastTarget = false;

        // 고스트 미리보기 - 선택한 키트로 "이만큼 오른다"를 현재 채움 오른쪽에 반투명 구간으로 겹쳐 보여준다.
        _ghostFill = Img("ghostFill", body.gameObject, 0, 0, 0, th, new Color(1, 1, 1, 0));
        _ghostFill.raycastTarget = false; _ghostFill.gameObject.SetActive(false);

        // 세로 눈금선 - 채움 '위'에 그려 채워진(밝은) 구간에서도 경계가 확실히 보이게 한다.
        // 밝은 채움엔 어두운 심(0.42)으로, 어두운 미채움엔 밝은 하이라이트(0.28)로 - 양쪽 대비를 겹쳐 어디서나 보이게.
        for (int p = 10; p <= 90; p += 10)
        {
            float lx = tw * p / 100f;
            Img($"tickD{p}", body.gameObject, lx - 1f, 0, 2, th, C("05101C", 0.42f)).raycastTarget = false;
            Img($"tickL{p}", body.gameObject, lx, 0, 1, th, C("EAF7FF", 0.28f)).raycastTarget = false;
        }

        // 진행 노드 - 바 한가운데에 들어가는 원형 노브(슬라이더 손잡이) 스타일.
        _node = NewRT("node", track.gameObject);
        _node.anchorMin = _node.anchorMax = new Vector2(0, 0.5f); _node.pivot = new Vector2(0.5f, 0.5f);
        _node.sizeDelta = new Vector2(34, th); _node.anchoredPosition = new Vector2(0f, 0);   // Open 시 SetGauge 로 실제 위치 이동
        // 노브: 글로우 -> 어두운 원판 -> 밝은 링 -> 코어. 연결선 없이 노브만으로 위치 표시.
        var glow = Img("nGlow", _node.gameObject, 0, 0, 30, 30, C("4CC9F7", 0.22f), UISpriteFactory.Disc(48)); CenterIn(glow, _node); glow.raycastTarget = false;
        var knob = Img("nKnob", _node.gameObject, 0, 0, 20, 20, C("0A1420", 0.98f), UISpriteFactory.Disc(48)); CenterIn(knob, _node); knob.raycastTarget = false;
        var ring = Img("nRing", _node.gameObject, 0, 0, 20, 20, AccentBright, UISpriteFactory.Ring(48, 3f)); CenterIn(ring, _node); ring.raycastTarget = false;
        var core = Img("nCore", _node.gameObject, 0, 0, 8, 8, Accent, UISpriteFactory.Disc(24)); CenterIn(core, _node); core.raycastTarget = false;
        // 펄스(노브 주위 확장 링) - 무한 트윈은 실행 시 StartAmbientTweens 가 건다.
        _nodePulse = Img("nPulse", _node.gameObject, 0, 0, 20, 20, C("4CC9F7", 0.5f), UISpriteFactory.Ring(48, 3f)); CenterIn(_nodePulse, _node); _nodePulse.raycastTarget = false;
        // 라벨(바 아래 작은 태그)
        var lblWrap = TL(NewGO("nLabelWrap", _node), 0, 0, 60, 24);
        lblWrap.anchorMin = lblWrap.anchorMax = new Vector2(0.5f, 0); lblWrap.pivot = new Vector2(0.5f, 1f);
        lblWrap.anchoredPosition = new Vector2(0, -10);
        var lblBg = lblWrap.gameObject.AddComponent<Image>(); lblBg.sprite = UISpriteFactory.RoundedRect(16, 8); lblBg.type = Image.Type.Sliced; lblBg.color = C("4CC9F7", 0.16f);
        Outline(lblWrap.gameObject, C("4CC9F7", 0.5f));
        _nodeLabel = Txt("nLbl", lblWrap.gameObject, 0, 0, 60, 24, "42%", _mono, 14, AccentBright, TextAlignmentOptions.Center, 0, FontStyles.Bold);
        Stretch(_nodeLabel.gameObject);

        // 마커 템플릿 - 실제 마커는 실행 시 마일스톤 수만큼 복제된다.
        _markerTemplate = BuildMarkerTemplate(track.gameObject, th);
        _node.SetAsLastSibling();

        // 레전드 - 각 구간이 열리는 % 지점에서 바 하단으로부터 세로 연결선이 내려와 도트+라벨로 이어진다.
        // 라벨/도트/연결선은 진행 도달 여부에 따라 RefreshLegend()에서 공개/??? 처리.
        float ly = ty + th + 40;              // 레전드 도트 y
        // 현재 구간 강조 하이라이트 - 도트+라벨 뒤에 깔리는 둥근 바. 위치/표시는 RefreshLegend 에서.
        _legendCurHlImg = Img("lgCurHl", panel.transform, 0, ly - 6, 150, 22, C("4CC9F7", 0.10f), UISpriteFactory.RoundedRect(20, 8));
        _legendCurHl = _legendCurHlImg.rectTransform;
        _legendCurHl.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++)
        {
            // 구간 i가 열리는 지점(i*25%)의 바 x. (경계 그대로 - 위치 이동 없음)
            float bx = tx + i * tw / 4f;
            // 연결선 상단 y. 자연(0%)은 둥근 좌측 모서리라 바 하단에 붙이면 곡선 아래로 떠 보이므로
            // 상단만 모서리 안쪽까지 더 끌어올려(길이만 늘려) 바에 닿게 한다. 나머지는 바 하단에 딱 붙임.
            float connTop = (i == 0) ? ty + th - 14f : ty + th;
            // 자연은 바 좌측 끝이라 bx-1로 두면 왼쪽으로 삐져나온다 -> 연결선만 안쪽으로 붙임(도트·라벨은 그대로).
            float connX = (i == 0) ? bx : bx - 1f;
            _legendConns[i] = Img($"lgConn{i}", panel.transform, connX, connTop, 2, ly + 5 - connTop, RegionCol[i]);
            _legendConns[i].raycastTarget = false;
            // 연결선 끝 도트(구간 경계 x 중앙)
            _legendDots[i] = Img($"lgDot{i}", panel.transform, bx - 5, ly, 10, 10, RegionCol[i], UISpriteFactory.Disc(20));
            _legendDots[i].raycastTarget = false;
            // 라벨(도트 오른쪽)
            _legendLabels[i] = Txt($"lg{i}", panel.transform, bx + 12, ly - 2, 160, 18, "???", _mono, 13,
                new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, 0.88f), TextAlignmentOptions.Left);
        }

        // 바(track: 노드·노드라벨 포함)를 범례 연결선보다 위로 - 파란 % 라벨이 초록 연결선에 가리지 않게.
        track.SetAsLastSibling();
    }

    // 마커 템플릿 - 칩 + 아이콘 3종(완료/다음/잠금). 실행 시 상태에 따라 하나만 켠다.
    private TransmissionMarker BuildMarkerTemplate(GameObject track, float th)
    {
        var mk = NewRT("markerTemplate", track);
        mk.anchorMin = mk.anchorMax = new Vector2(0, 0.5f); mk.pivot = new Vector2(0.5f, 0.5f);
        mk.sizeDelta = new Vector2(34, 34); mk.anchoredPosition = new Vector2(0, th / 2f);

        var chip = Img("chip", mk.gameObject, 0, 0, 34, 34, C("0F1A2D"), UISpriteFactory.RoundedRect(34, 17));
        CenterIn(chip, mk);
        var chipOl = Outline(chip.gameObject, Accent);
        // 호버 판정용 투명 이미지(마커 자체에 붙인다)
        var trg = mk.gameObject.AddComponent<Image>(); trg.color = new Color(0, 0, 0, 0); trg.raycastTarget = true;

        var holder = NewRT("iconHolder", mk.gameObject); CenterIn2(holder); holder.sizeDelta = new Vector2(18, 18);

        var done = NewRT("iconDone", holder.gameObject); CenterIn2(done); done.sizeDelta = new Vector2(18, 18);
        var doneCoin = MarkerIcon("doneCoin", done.gameObject, 16, 16, Success, UISpriteFactory.Disc(32));         // 채운 코인
        MarkerIcon("doneRim", done.gameObject, 16, 16, C("0A1420", 0.9f), UISpriteFactory.Ring(48, 2f));           // 어두운 테두리 림
        MarkerIcon("doneCore", done.gameObject, 5, 5, C("0A1420", 0.9f), UISpriteFactory.Disc(16));                // 중앙 각인

        var next = NewRT("iconNext", holder.gameObject); CenterIn2(next); next.sizeDelta = new Vector2(18, 18);
        var tgtRing = MarkerIcon("tgtRing", next.gameObject, 16, 16, Accent, UISpriteFactory.Ring(48, 3f));
        var tgtDot = MarkerIcon("tgtDot", next.gameObject, 5, 5, Accent, UISpriteFactory.Disc(16));

        var locked = NewRT("iconLocked", holder.gameObject); CenterIn2(locked); locked.sizeDelta = new Vector2(18, 18);
        var q = Txt("q", locked.gameObject, 0, 0, 18, 18, "?", _mono, 14, C("E2EDF8", 0.25f), TextAlignmentOptions.Center);

        var comp = mk.gameObject.AddComponent<TransmissionMarker>();
        comp.chipOutline = chipOl;
        comp.iconDone = done.gameObject; comp.iconNext = next.gameObject; comp.iconLocked = locked.gameObject;
        comp.doneCoin = doneCoin; comp.nextRing = tgtRing; comp.nextDot = tgtDot; comp.lockedMark = q;
        mk.gameObject.SetActive(false);
        return comp;
    }

    // ── 본문 ──────────────────────────────────────────────────────────
    private void BuildBody()
    {
        float by = 534, bh = 466, gap = 30;
        float leftW = (1744 - gap) * 1.5f / 2.5f, rightW = (1744 - gap) - leftW;

        // 좌: 보유 충전 키트
        var lp = Panel("kitPanel", 88, by, leftW, bh);
        PanelHeader(lp, leftW, "보유 충전 키트");
        // 열 헤더 - 각 행의 수량 / 상승률 컬럼이 무엇인지 안내. 행 컬럼 x와 맞춰 우측 정렬.
        float rowRight = leftW - 16f;
        Txt("colQty", lp.gameObject, rowRight - 156f, 22, 60, 15, "수량", _mono, 12, C("E8F2FB", 0.45f), TextAlignmentOptions.Center, 1);
        Txt("colGain", lp.gameObject, rowRight - 14f - 130f, 22, 130, 15, "예상 상승률", _mono, 12, C("E8F2FB", 0.45f), TextAlignmentOptions.Right, 1);
        // 컬럼 구분선(이름|수량|상승률) - 짧고 둥근 은은한 세로 바. 행 구분선과 같은 x.
        Img("colDivH1", lp.gameObject, rowRight - 165f, 14, 2, 28, C("9EC7D9", 0.16f), UISpriteFactory.RoundedRect(4, 1)).raycastTarget = false;
        Img("colDivH2", lp.gameObject, rowRight - 93f, 14, 2, 28, C("9EC7D9", 0.16f), UISpriteFactory.RoundedRect(4, 1)).raycastTarget = false;
        // 스크롤 뷰(표준 3단): ScrollRect 루트 -> 뷰포트(마스크+레이캐스트) -> 콘텐츠(세로 레이아웃+크기 자동).
        var scrollGo = TL(NewGO("kitScroll", lp.transform), 16, 74, leftW - 32, bh - 90);
        var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;

        // 뷰포트 - 부모를 꽉 채우고 RectMask2D로 클립. 투명 Image를 둬서 빈 영역도 드래그/휠 레이캐스트가 잡히게.
        var viewport = NewRT("kitViewport", scrollGo.gameObject); Stretch(viewport.gameObject);
        var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;

        // 콘텐츠 - 가로는 뷰포트 폭에 맞추고(스트레치), 세로는 ContentSizeFitter가 행 합계로 자동. 상단 정렬.
        var content = NewRT("kitContent", viewport.gameObject);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
        scroll.content = content;

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>(); vlg.spacing = 8; vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.UpperCenter;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 콘텐츠 높이를 행 합계로 자동 -> 넘치면 스크롤
        _kitListRoot = content;
        _kitRowTemplate = BuildKitRowTemplate(content);
        _kitEmptyLabel = BuildKitEmptyLabel(content);

        // 우: 전송 제어
        var rp = Panel("ctrlPanel", 88 + leftW + gap, by, rightW, bh);
        PanelHeader(rp, rightW, "전송 제어");
        float ix = 24, iw = rightW - 48, iy = 74;
        var selCard = Card(rp.transform, ix, iy, iw, 84, C("E8F2FB", 0.04f), C("E8F2FB", 0.08f));
        Txt("selLbl", selCard.transform, 18, 14, iw - 36, 16, "선택된 키트", _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left, 1);
        _selName = Txt("selName", selCard.transform, 18, 32, iw - 36, 28, "없음", _kr, 22, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _selMeta = Txt("selMeta", selCard.transform, 18, 60, iw - 36, 20, "목록에서 키트를 클릭", _kr, 14, C("E8F2FB", 0.5f), TextAlignmentOptions.Left);

        iy += 84 + 12;   // 카드 간 세로 간격 12로 통일
        var pvCard = Card(rp.transform, ix, iy, iw, 84, C("4CC9F7", 0.06f), C("4CC9F7", 0.2f));
        Txt("pvLbl", pvCard.transform, 18, 14, iw - 36, 16, "예상 전송률", _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left, 1);
        _previewVal = Txt("pvVal", pvCard.transform, 18, 36, iw - 36, 36, "키트를 선택하세요", _kr, 30, C("E8F2FB", 0.4f), TextAlignmentOptions.Left, 0, FontStyles.Normal);

        // 버튼 행 + 로그 (패널 로컬 좌표: 아래에서부터 로그->버튼)
        float logH = 110;
        float logY = bh - 16 - logH;
        float btnY = logY - 12 - 62;   // 버튼<->로그 간격도 12로 통일
        float sendW = iw - 130 - 14;
        var send = Img("sendBtn", rp.transform, ix, btnY, sendW, 62, C("47C4F0"), UISpriteFactory.RoundedRect(48, 12));
        _sendBtnImg = send; _sendBtn = send.gameObject.AddComponent<Button>(); _sendBtn.targetGraphic = send;
        _sendBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        _sendBtnCg = send.gameObject.AddComponent<CanvasGroup>();
        Outline(send.gameObject, C("FFFFFF", 0.35f));
        _sendTri = Img("sendTri", send.gameObject, 0, 0, 15, 16, C("06202E"), TriTex());
        _sendTri.rectTransform.anchorMin = _sendTri.rectTransform.anchorMax = _sendTri.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _sendTri.rectTransform.anchoredPosition = new Vector2(-34, 0); _sendTri.raycastTarget = false;
        Txt("sendTxt", send.gameObject, 0, 0, sendW, 62, "전송", _mono, 22, C("06202E"), TextAlignmentOptions.Center, 0, FontStyles.Bold)
            .rectTransform.anchoredPosition += new Vector2(12, 0);

        var close = Img("closeBtn", rp.transform, ix + sendW + 14, btnY, 130, 62, C("E8F2FB", 0.07f), UISpriteFactory.RoundedRect(48, 12));
        _closeBtn = close.gameObject.AddComponent<Button>(); _closeBtn.targetGraphic = close; _closeBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        Outline(close.gameObject, C("E2EDF8", 0.25f));
        Txt("closeTxt", close.gameObject, 0, 0, 130, 62, "닫기 ESC", _mono, 18, C("E8F2FB", 0.6f), TextAlignmentOptions.Center);

        // 이벤트 로그 (헤더 아래 로그 라인 - 위/아래 여백 균등)
        var logBox = Img("txLog", rp.transform, ix, logY, iw, logH, C("070C17", 0.7f), UISpriteFactory.RoundedRect(48, 12));
        Outline(logBox.gameObject, C("4CC9F7", 0.15f));
        _logDot = Img("logDot", logBox.transform, 15, 13, 6, 6, Accent, UISpriteFactory.Disc(12));
        Txt("logHdr", logBox.transform, 28, 11, iw - 40, 14, "SYSTEM LOG", _mono, 11, C("4CC9F7", 0.6f), TextAlignmentOptions.Left, 2);
        _logText = Txt("logLines", logBox.transform, 15, 34, iw - 30, logH - 34 - 8, "", _mono, 13, C("E8F2FB", 0.6f), TextAlignmentOptions.TopLeft);
    }

    // 키트 행 템플릿 - 실행 시 보유 키트 수만큼 복제된다.
    private TransmissionKitRow BuildKitRowTemplate(RectTransform parent)
    {
        var go = NewGO("kitRowTemplate", parent);
        go.AddComponent<LayoutElement>().minHeight = 66;
        var cg = go.AddComponent<CanvasGroup>();
        var bg = go.AddComponent<Image>(); bg.sprite = UISpriteFactory.RoundedRect(24, 12); bg.type = Image.Type.Sliced; bg.color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg; btn.navigation = new Navigation { mode = Navigation.Mode.None };
        var outline = go.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(0, 0, 0, 0); outline.effectDistance = new Vector2(1, -1);

        // 지역 색은 실행 시 Bind 가 넣는다(템플릿은 자연 구간 색으로 보이기만).
        var rc = RegionCol[0];
        // 선택 강조용 좌측 악센트 바(지역 색). 평소 투명, 선택 시 색이 차오르며 살짝 슬라이드 인.
        var accent = Img("kAccent", go, 6, 12, 4, 42, new Color(rc.r, rc.g, rc.b, 0f), UISpriteFactory.RoundedRect(4, 2));
        accent.raycastTarget = false;
        // 아이콘 웰 - 지역 색 틴트(보스는 더 진하게) + 등급 글리프(일반=사각 / 보스=마름모+링)
        var well = Img("well", go, 20, 14, 38, 38, new Color(rc.r, rc.g, rc.b, 0.09f), UISpriteFactory.RoundedRect(24, 10));
        var wellOl = Outline(well.gameObject, new Color(rc.r, rc.g, rc.b, 0.3f));

        var gNormal = NewRT("glyphNormal", well.gameObject); Stretch(gNormal.gameObject);
        var gSq = MarkerIcon("kgSq", gNormal.gameObject, 13, 13, new Color(rc.r, rc.g, rc.b, 0.9f), UISpriteFactory.RoundedRect(12, 3));
        var gBoss = NewRT("glyphBoss", well.gameObject); Stretch(gBoss.gameObject);
        var gRing = MarkerIcon("kgRing", gBoss.gameObject, 22, 22, new Color(rc.r, rc.g, rc.b, 0.55f), UISpriteFactory.Ring(48, 2.5f));
        var gGem = MarkerIcon("kgGem", gBoss.gameObject, 12, 12, rc, UISpriteFactory.RoundedRect(8, 3), 45f);
        MarkerIcon("kgHl", gBoss.gameObject, 3.5f, 3.5f, C("FFFFFF", 0.8f), UISpriteFactory.Disc(16), 0f, new Vector2(-1.5f, 1.5f));
        gBoss.gameObject.SetActive(false);

        // 이름/메타
        var name = Txt("kName", go, 74, 12, 400, 26, "충전 키트", _kr, 20, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        var meta = Txt("kMeta", go, 74, 40, 500, 18, "", _kr, 13, C("E8F2FB", 0.45f), TextAlignmentOptions.Left);
        // 수량/상승률 (우측)
        var qty = Txt("kQty", go, 0, 0, 60, 30, "x0", _mono, 18, C("E8F2FB", 0.7f), TextAlignmentOptions.Center);
        qty.rectTransform.anchorMin = qty.rectTransform.anchorMax = new Vector2(1, 0.5f); qty.rectTransform.pivot = new Vector2(1, 0.5f); qty.rectTransform.anchoredPosition = new Vector2(-96, 0);
        var gain = Txt("kGain", go, 0, 0, 74, 30, "+0%", _mono, 18, Accent, TextAlignmentOptions.Right, 0, FontStyles.Bold);
        gain.rectTransform.anchorMin = gain.rectTransform.anchorMax = new Vector2(1, 0.5f); gain.rectTransform.pivot = new Vector2(1, 0.5f); gain.rectTransform.anchoredPosition = new Vector2(-14, 0);
        // 컬럼 구분선(이름|수량|상승률) - 헤더와 동일 x, 짧고 둥근 은은한 세로 바
        ColDivider(go, -164f); ColDivider(go, -92f);

        var row = go.AddComponent<TransmissionKitRow>();
        row.background = bg; row.rowOutline = outline; row.group = cg; row.button = btn; row.accentBar = accent;
        row.well = well; row.wellOutline = wellOl;
        row.glyphNormal = gNormal.gameObject; row.glyphSquare = gSq;
        row.glyphBoss = gBoss.gameObject; row.glyphRing = gRing; row.glyphGem = gGem;
        row.nameText = name; row.metaText = meta; row.qtyText = qty; row.gainText = gain;
        go.SetActive(false);
        return row;
    }

    // 보유 키트 0개일 때 안내 - 빈 슬롯 글리프 + 텍스트. 레이아웃 자식이라 LayoutElement 로 높이 확보.
    private GameObject BuildKitEmptyLabel(RectTransform parent)
    {
        var go = NewGO("kitEmpty", parent);
        go.AddComponent<LayoutElement>().minHeight = 128;

        var box = Img("emptyBox", go, 0, 0, 48, 48, C("4CC9F7", 0.05f), UISpriteFactory.RoundedRect(28, 10));
        var brt = box.rectTransform; brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0, -16); box.raycastTarget = false;
        Outline(box.gameObject, C("4CC9F7", 0.18f));
        var bar = Img("emptyBar", box.gameObject, 0, 0, 20, 3, C("9FDCF9", 0.45f), UISpriteFactory.RoundedRect(6, 1));
        bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = bar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero; bar.raycastTarget = false;

        var t = Txt("emptyTxt", go, 0, 0, 320, 24, "보유한 충전키트가 없습니다.", _kr, 15, C("E8F2FB", 0.4f), TextAlignmentOptions.Center);
        var tt = t.rectTransform; tt.anchorMin = tt.anchorMax = tt.pivot = new Vector2(0.5f, 1f);
        tt.anchoredPosition = new Vector2(0, -80); tt.sizeDelta = new Vector2(320, 24);

        go.SetActive(false);
        return go;
    }

    private void BuildFooter()
    {
        _statusLine = Txt("status", _content, 88, 1010, 1200, 22, "", _mono, 14, C("E8F2FB", 0.5f), TextAlignmentOptions.Left);
        // 커서 높이 18, 상태 텍스트(y=1010, h=22, 세로 중앙) 중심(=1021)에 맞춰 y=1012 배치.
        _cursorImg = Img("cursor", _content, 88 + 470, 1012, 9, 18, Accent, UISpriteFactory.RoundedRect(8, 2));
        _cursorImg.raycastTarget = false; _cursor = _cursorImg.rectTransform;   // 상태 텍스트 끝으로 따라오도록 참조 저장
    }

    private void BuildTooltip()
    {
        _tooltip = NewGO("tooltip", _content.transform);
        var rt = _tooltip.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(264, 96);
        _ttBox = _tooltip.AddComponent<Image>(); _ttBox.sprite = UISpriteFactory.RoundedRect(48, 12); _ttBox.type = Image.Type.Sliced; _ttBox.color = C("101A2D", 0.98f);
        _ttBox.raycastTarget = false; _ttOutline = Outline(_tooltip, Accent);
        _ttTitle = Txt("ttT", _tooltip, 17, 14, 230, 16, "", _mono, 12, Accent, TextAlignmentOptions.Left, 2);
        _ttName = Txt("ttN", _tooltip, 17, 33, 230, 24, "", _kr, 17, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _ttState = Txt("ttS", _tooltip, 17, 62, 230, 20, "", _kr, 13, C("E8F2FB", 0.6f), TextAlignmentOptions.Left);
        _ttCg = _tooltip.AddComponent<CanvasGroup>(); _ttCg.blocksRaycasts = false;
        _tooltip.SetActive(false);
    }

    private void BuildOverlay()
    {
        _scanImg = Img("scan", _content, 0, 0, 1920, 1080, C("78C8FF", 0.03f), ScanTile());
        _scanImg.type = Image.Type.Tiled; _scanImg.raycastTarget = false;
        _vignetteImg = Img("vignette", _content, 0, 0, 1920, 1080, Color.white, VignetteTex());
        _vignetteImg.raycastTarget = false;
    }

    // ── 리워드 리빌 오버레이(지점 도달 연출용, 평소 비활성) ────────────
    private void BuildRewardOverlay()
    {
        _reward = TL(NewGO("RewardReveal", _content.transform), 0, 0, 1920, 1080).gameObject;
        _rewardCg = _reward.AddComponent<CanvasGroup>();

        // 스크림 - 배경을 어둡게 + 클릭 시 스킵
        var scrim = Img("rwScrim", _reward, 0, 0, 1920, 1080, C("040810", 0.62f));
        scrim.raycastTarget = true;
        _rewardScrimBtn = scrim.gameObject.AddComponent<Button>();
        _rewardScrimBtn.transition = Selectable.Transition.None; _rewardScrimBtn.navigation = new Navigation { mode = Navigation.Mode.None };

        // 가로형 매니페스트 카드(화면 중앙, 슬라이드 인). RectMask2D 로 스윕 하이라이트를 카드 안에 가둔다.
        const float PW = 780, PH = 176, X0 = 190;
        _rewardCard = NewRT("rwCard", _reward);
        _rewardCard.anchorMin = _rewardCard.anchorMax = _rewardCard.pivot = new Vector2(0.5f, 0.5f);
        _rewardCard.sizeDelta = new Vector2(PW, PH); _rewardCard.anchoredPosition = Vector2.zero;

        var bg = _rewardCard.gameObject.AddComponent<Image>();
        bg.sprite = UISpriteFactory.RoundedRectVGrad(C("14243A"), C("0A1421"), 64, 18); bg.type = Image.Type.Sliced;
        bg.color = new Color(1, 1, 1, 0.99f); bg.raycastTarget = false;
        var cardOutline = _rewardCard.gameObject.AddComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = new Color(1, 1, 1, 0.06f); cardOutline.effectDistance = new Vector2(1.2f, -1.2f);
        _rewardCard.gameObject.AddComponent<RectMask2D>();   // 스윕/자식 클리핑

        // 좌측 악센트 바(구간 색) - 라운드 코너 안쪽으로 살짝 들여 각진 nub 방지
        var accentBar = Img("rwAccent", _rewardCard.gameObject, 14, 22, 6, PH - 44, Success, UISpriteFactory.RoundedRect(6, 3));
        _rewardTint = new[] { accentBar };

        // 아이콘 타일(구간 색 배경 + 프레임 + 타입별 아이콘)
        float tS = 116, tX = 30, tY = (PH - tS) / 2f;
        _rewardIconTile = TL(NewGO("rwTile", _rewardCard), tX, tY, tS, tS);
        _rewardIconBg = _rewardIconTile.gameObject.AddComponent<Image>();
        _rewardIconBg.sprite = UISpriteFactory.RoundedRect(40, 18); _rewardIconBg.type = Image.Type.Sliced; _rewardIconBg.color = C("5FDD9D", 0.14f); _rewardIconBg.raycastTarget = false;
        _rewardIconFrame = _rewardIconTile.gameObject.AddComponent<UnityEngine.UI.Outline>();
        _rewardIconFrame.effectColor = Success; _rewardIconFrame.effectDistance = new Vector2(1.4f, -1.4f);
        _rewardIconHolder = NewRT("rwIcon", _rewardIconTile.gameObject);
        _rewardIconHolder.anchorMin = _rewardIconHolder.anchorMax = _rewardIconHolder.pivot = new Vector2(0.5f, 0.5f);
        _rewardIconHolder.sizeDelta = new Vector2(28, 28); _rewardIconHolder.anchoredPosition = Vector2.zero;
        _rewardIconHolder.localScale = Vector3.one * 2.6f;
        BuildRewardIconVariants(_rewardIconHolder);

        // 세로 구분선
        Img("rwDiv", _rewardCard.gameObject, X0 - 24, 34, 1, PH - 68, C("E8F2FB", 0.10f)).raycastTarget = false;

        // 텍스트 블록(우측)
        _rewardTitle = Txt("rwTitle", _rewardCard.gameObject, X0, 34, PW - X0 - 40, 20, "", _mono, 13, Success, TextAlignmentOptions.Left, 3, FontStyles.Bold);
        _rewardName  = Txt("rwName", _rewardCard.gameObject, X0, 58, PW - X0 - 40, 46, "", _kr, 32, TextBright, TextAlignmentOptions.Left, 0, FontStyles.Bold);
        _rewardDesc  = Txt("rwDesc", _rewardCard.gameObject, X0, 112, PW - X0 - 40, 42, "", _kr, 15, C("E8F2FB", 0.66f), TextAlignmentOptions.TopLeft);
        _rewardDesc.textWrappingMode = TextWrappingModes.Normal;

        // 클릭 힌트(우하단). 특수 글리프는 폰트 아틀라스에 없어 깨지므로 순수 텍스트만.
        _rewardHint = Txt("rwHint", _rewardCard.gameObject, PW - 210, PH - 30, 190, 18, "클릭하여 계속", _mono, 12, C("E8F2FB", 0.42f), TextAlignmentOptions.Right, 2);

        // 스윕 하이라이트(등장 시 좌->우로 한 번 지나감). 카드 마스크로 양끝 클리핑.
        _rewardSweep = TL(NewGO("rwSweep", _rewardCard), 0, 0, 90, PH);
        var sw = _rewardSweep.gameObject.AddComponent<Image>(); sw.color = new Color(1, 1, 1, 0.06f); sw.raycastTarget = false;

        _reward.SetActive(false);
    }

    // 보상 아이콘 3형태(설비1 / 설비2 대각반반 / 보석)를 다 만들어둔다. 실행 시 하나만 켠다.
    private void BuildRewardIconVariants(RectTransform holder)
    {
        // 설비 1개 - 원본 비율 유지
        _riSingle = Img("facSingle", holder.gameObject, 0, 0, 28, 28, Color.white);
        CenterIn(_riSingle, holder); _riSingle.preserveAspect = true; _riSingle.raycastTarget = false;
        _riSingle.type = Image.Type.Simple;
        _riSingleGo = _riSingle.gameObject;
        _riSingleGo.SetActive(false);

        _riHalfTLIcon = BuildRewardHalf(holder, true, out _riHalfTL, out _riHalfTLMask);
        _riHalfBRIcon = BuildRewardHalf(holder, false, out _riHalfBR, out _riHalfBRMask);

        // 비설비 보상 - 보석 엠블럼(구간 색 코인 + 어두운 림 + 중앙 각인)
        var gem = NewRT("gem", holder.gameObject); CenterIn2(gem); gem.sizeDelta = new Vector2(28, 28);
        _riGemCoin = MarkerIcon("gemCoin", gem.gameObject, 16, 16, Success, UISpriteFactory.Disc(32));
        MarkerIcon("gemRim", gem.gameObject, 16, 16, C("0A1420", 0.9f), UISpriteFactory.Ring(48, 2f));
        MarkerIcon("gemCore", gem.gameObject, 5, 5, C("0A1420", 0.9f), UISpriteFactory.Disc(16));
        _riGem = gem.gameObject;
        _riGem.SetActive(false);
    }

    // 설비 2개 대각 분할: 설비 이미지를 삼각형 절반으로 마스킹하고, 내용이 그 삼각형 코너로 오도록 배치.
    private Image BuildRewardHalf(RectTransform holder, bool topLeft, out GameObject root, out Image maskImg)
    {
        var maskGO = NewRT(topLeft ? "halfTL" : "halfBR", holder.gameObject);
        maskGO.anchorMin = maskGO.anchorMax = maskGO.pivot = new Vector2(0.5f, 0.5f);
        maskGO.sizeDelta = new Vector2(28, 28); maskGO.anchoredPosition = Vector2.zero;
        maskImg = maskGO.gameObject.AddComponent<Image>(); maskImg.raycastTarget = false;
        maskGO.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var icon = Img("facIcon", maskGO.gameObject, 0, 0, 17, 17, Color.white);   // 각 반쪽에 또렷이 들어갈 크기
        var rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = topLeft ? new Vector2(-6f, 6f) : new Vector2(6f, -6f);   // 각 삼각형 반쪽 중앙으로
        icon.preserveAspect = true; icon.raycastTarget = false; icon.type = Image.Type.Simple;

        root = maskGO.gameObject;
        root.SetActive(false);
        return icon;
    }

    // ── 에디터 전용 레이아웃 헬퍼 ─────────────────────────────────────
    private void Stretch(GameObject go) { var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private void CenterIn(Image img, RectTransform parent) { var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }
    private void CenterIn2(RectTransform rt) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }

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
        if (rt.GetComponent<CanvasGroup>() == null) _bootBuild.Add(rt.gameObject.AddComponent<CanvasGroup>());
    }

    private void PanelHeader(RectTransform panel, float w, string title)
    {
        var hb = Img("hdrBar", panel.gameObject, 0, 0, w, 54, C("4CC9F7", 0.06f), UISpriteFactory.RoundedRect(48, 16));
        Img("hdrLine", panel.gameObject, 0, 53, w, 1, C("4CC9F7", 0.18f)).raycastTarget = false;
        Img("hdrTick", panel.gameObject, 22, 18, 4, 18, Accent, UISpriteFactory.RoundedRect(8, 2)).raycastTarget = false;
        Txt("hdrTxt", panel.gameObject, 40, 18, 300, 18, title, _mono, 15, AccentSoft, TextAlignmentOptions.Left, 2, FontStyles.Normal);
    }

    private RectTransform Card(Transform p, float x, float y, float w, float h, Color bg, Color border)
    {
        var rt = TL(NewGO("card", p), x, y, w, h);
        var im = rt.gameObject.AddComponent<Image>(); im.sprite = UISpriteFactory.RoundedRect(48, 12); im.type = Image.Type.Sliced; im.color = bg;
        Outline(rt.gameObject, border);
        return rt;
    }

    // 풀네임 필수: 18.외부에셋 의 3D Outline(전역 네임스페이스)이 UnityEngine.UI.Outline 을 가린다.
    private UnityEngine.UI.Outline Outline(GameObject go, Color col)
    {
        var o = go.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = col; o.effectDistance = new Vector2(1, -1); return o;
    }

    // 행 컬럼 구분선 - 우측 끝 기준 xFromRight 위치에 세로 중앙 정렬된 짧고 둥근 바.
    private void ColDivider(GameObject row, float xFromRight)
    {
        var im = Img("colDiv", row, 0, 0, 2, 34, C("9EC7D9", 0.16f), UISpriteFactory.RoundedRect(4, 1));
        var rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xFromRight, 0); im.raycastTarget = false;
    }

    // 중심 정렬 이미지 헬퍼(아이콘 홀더 자식용)
    private Image MarkerIcon(string n, GameObject parent, float w, float h, Color col, Sprite spr, float rotZ = 0f, Vector2 off = default)
    {
        var im = Img(n, parent, 0, 0, w, h, col, spr); var rt = im.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = off; if (rotZ != 0f) rt.localRotation = Quaternion.Euler(0, 0, rotZ);
        im.raycastTarget = false; return im;
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
#endif
}
