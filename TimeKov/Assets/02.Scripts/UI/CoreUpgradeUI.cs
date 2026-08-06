// =====================================================================
// CoreUpgradeUI.cs
// 코어 강화 패널 UI — 코어→시계 인라인 전환 타이밍 미니게임 통합
// 강화 시작 → 중앙 코어가 시계로 전환 → 정지 판정(PERFECT/GOOD/MISS) → 코어 복귀
// (클로드디자인 프로토타입 app.jsx 동작 이식)
// =====================================================================

using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoreUpgradeUI : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────────────
    public static CoreUpgradeUI Instance { get; private set; }

    // ── 루트 ──────────────────────────────────────────────────────────
    [Header("루트 패널")]
    [SerializeField] private GameObject panelRoot;

    // ── 타이틀 ────────────────────────────────────────────────────────
    [Header("타이틀")]
    [SerializeField] private TextMeshProUGUI titleText;

    // ── 레벨 표시 ─────────────────────────────────────────────────────
    [Header("레벨")]
    [SerializeField] private TextMeshProUGUI levelText;

    // ── 코어 비주얼 ───────────────────────────────────────────────────
    [Header("코어 비주얼")]
    [SerializeField] private Image coreImage;              // 중앙 구체. 위치/연출 기준.
    [SerializeField] private CanvasGroup coreVisualGroup;  // 후광+링+구체 묶음(시계 전환 시 통째 페이드)
    [SerializeField] private RectTransform coreRingA;      // 회전 링 A(가로 기운 고리)
    [SerializeField] private RectTransform coreRingB;      // 회전 링 B(세로 기운 고리, 반대 회전)
    [SerializeField] private float ringASpeedDeg = 22f;    // 링 A 회전 속도(도/초)
    [SerializeField] private float ringBSpeedDeg = -31f;   // 링 B 회전 속도(반대 방향)
    [SerializeField] private Image[] constellationNodes;   // 별자리 10노드(점등 = 강화 단계).
    [SerializeField] private TextMeshProUGUI[] effectRows; // 효과 리스트(최대시간/부활/대쉬/흡수). 미해금=???.
    [SerializeField] private Image[] kitStockIcons;        // 좌측 보유 키트 아이콘 5(I~V)
    [SerializeField] private TextMeshProUGUI[] kitStockTexts; // 좌측 보유 키트 이름+개수 5
    [SerializeField] private Image kitNeedIcon;            // 하단 필요 키트 실제 아이콘

    // ── 현재 / 강화 후 스탯 ───────────────────────────────────────────
    [Header("현재 스탯")]
    [SerializeField] private TextMeshProUGUI currentTimeText;
    [Header("강화 후 스탯")]
    [SerializeField] private TextMeshProUGUI nextTimeText;
    [SerializeField] private TextMeshProUGUI deltaTimeText;

    // ── 재료 패널 ─────────────────────────────────────────────────────
    [Header("재료 패널")]
    [SerializeField] private Image kitIconImage;
    [SerializeField] private TextMeshProUGUI kitNameText;
    [SerializeField] private TextMeshProUGUI kitCountText;
    [SerializeField] private TextMeshProUGUI kitShortageText;
    [SerializeField] private TextMeshProUGUI successRateText;
    [SerializeField] private Image gaugeFill;

    // ── 버튼 ──────────────────────────────────────────────────────────
    [Header("버튼")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private Button closeButton;

    // ── 그룹 (최대 단계 분기) ─────────────────────────────────────────
    [Header("그룹 (최대 단계 분기)")]
    [SerializeField] private GameObject upgradeInfoGroup;
    [SerializeField] private GameObject maxLevelGroup;

    // ── 인라인 시계 (코어 자리에서 전환) ──────────────────────────────
    [Header("인라인 시계")]
    [SerializeField] private CanvasGroup     clockGroup;       // 시계 묶음 (alpha 0 시작)
    [SerializeField] private Image           successZoneImage; // 성공존 아크 (초록, Filled Radial360)
    [SerializeField] private Image           perfectZoneImage; // 퍼펙트존 아크 (골드)
    [SerializeField] private RectTransform   clockNeedle;      // 회전 바늘
    [SerializeField] private TextMeshProUGUI judgeChipText;    // PERFECT/GOOD/MISS +N%
    [SerializeField] private GameObject      spinHint;         // "멈춰서 성공존에 맞추세요!"

    // ── 타임 캐치 수치 (난이도) ───────────────────────────────────────
    [Header("타임 캐치 수치")]
    [SerializeField] private float spinPeriod     = 1.6f;   // 바늘 1회전 (초) — 처음엔 느긋하게
    [SerializeField] private float zoneHalfDeg    = 32f;    // 성공존 반각 (넉넉히)
    [SerializeField] private float perfectHalfDeg = 11f;    // 퍼펙트존 반각
    [SerializeField] private int   perfectBonusPct = 20;   // (이전 45 — 너무 쉬워서 하향)
    [SerializeField] private int   goodBonusPct    = 8;     // (이전 22)
    [SerializeField] private float spinTimeout     = 6f;    // 미정지 자동 미스
    [SerializeField] private float crossfadeDur    = 0.35f;
    [SerializeField] private float judgeHold       = 1.1f;  // 판정 칩 유지
    [SerializeField] private float resultHold      = 1.6f;  // 결과 표시 유지

    // ── 피드백 ────────────────────────────────────────────────────────
    [Header("피드백")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private float feedbackDuration = 2.5f;

    // ── 색상 ──────────────────────────────────────────────────────────
    [Header("색상")]
    [SerializeField] private Color deltaColor      = new Color(0.2f, 0.9f, 0.4f);
    [SerializeField] private Color shortageColor   = new Color(1f,   0.3f, 0.3f);
    [SerializeField] private Color rateNormalColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color rateLowColor    = new Color(1f,   0.6f, 0.2f);
    [SerializeField] private Color stopButtonColor = new Color(1f,   0.70f, 0.23f); // 정지! 골드
    [SerializeField] private Color perfectColor    = new Color(1f,   0.84f, 0.42f);
    [SerializeField] private Color goodColor       = new Color(0.27f,0.88f, 0.54f);
    [SerializeField] private Color missColor       = new Color(1f,   0.38f, 0.41f);

    // ── 결과 연출 ─────────────────────────────────────────────────────
    [Header("결과 연출")]
    [SerializeField] private Color successFlashColor = new Color(0.27f, 0.88f, 0.54f, 0.35f);
    [SerializeField] private Color failFlashColor    = new Color(1f,    0.38f, 0.41f, 0.35f);
    [SerializeField] private Sprite[] crackleFrames;        // CoreCrackle 16프레임(번개, 보조)
    [SerializeField] private Sprite[] successBurstFrames;   // 강화 성공 폭발/광채 16프레임(결과연출 메인)
    [SerializeField] private Sprite[] failBurstFrames;      // 강화 실패 균열/파편 16프레임(결과연출 메인)
    private Image _flashOverlay;   // 런타임 생성 전체화면 플래시

    // 별자리 별점 색 (글로우 = 뒤 후광 / 코어점 = 앞 점). 점등 = 시안, 다음 목표 = 골드, 미점등 = 어두움.
    private static readonly Color GlowOff       = new Color(0.14f, 0.22f, 0.36f, 0.28f);
    private static readonly Color GlowOn        = new Color(0.37f, 0.77f, 1f,    0.60f);
    private static readonly Color GlowTarget    = new Color(1f,    0.84f, 0.42f, 0.55f);
    private static readonly Color CoreDotOff    = new Color(0.27f, 0.35f, 0.49f, 1f);
    private static readonly Color CoreDotOn     = new Color(0.78f, 0.93f, 1f,    1f);
    private static readonly Color CoreDotTarget = new Color(1f,    0.88f, 0.55f, 1f);
    private static readonly Color LineOn        = new Color(0.37f,  0.77f,  1f,     0.85f);
    private static readonly Color LineOff       = new Color(0.165f, 0.235f, 0.353f, 0.30f);

    private RectTransform[] _lines;   // 연결선(코어->노드0, 노드i-1->노드i) 런타임 생성
    private bool _linesBuilt;

    // ── 내부 ──────────────────────────────────────────────────────────
    private enum CatchPhase { Idle, Spin, Judge, Result }
    private CatchPhase _phase = CatchPhase.Idle;
    private float _spinElapsed;
    private Coroutine _spinCo;
    private Coroutine _seqCo;
    private Coroutine _fadeCo;
    private AudioSource _tickLoop;   // 바늘 회전(Spin) 중 째깍째깍 루프. 클립은 GameSfxConfig(SfxId.CoreUpgradeTick).
    private Coroutine _feedbackRoutine;
    private Coroutine _introCo;   // 패널 열릴 때 별자리 순차 등장 연출
    private CanvasGroup _starsGroup;   // 배경 별 페이드용 (런타임에 Stars에 부착)
    private Color _btnNormalColor = Color.white;

    // F 열기/닫기 충돌 방지용 프레임 가드.
    // 연 프레임엔 F-닫기 무시, 닫은 프레임엔 터미널이 재오픈 무시.
    private int _openedFrame = -1;
    public static int LastCloseFrame { get; private set; } = -10;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        upgradeButton?.onClick.AddListener(OnClickUpgrade);
        closeButton?.onClick.AddListener(OnClickClose);

        if (upgradeButton != null && upgradeButton.image != null)
            _btnNormalColor = upgradeButton.image.color;

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (clockGroup != null) { clockGroup.alpha = 0f; clockGroup.gameObject.SetActive(false); }
        if (spinHint != null) spinHint.SetActive(false);
        if (judgeChipText != null) judgeChipText.gameObject.SetActive(false);

        panelRoot?.SetActive(false);
    }

    private void OnEnable()
    {
        CoreUpgradeManager.OnUpgradeResult += OnUpgradeResult;
        CoreUpgradeManager.OnLevelChanged  += OnLevelChanged;
        DataBoot.OnDataLoaded += OnDataReady;
        Loc.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        CoreUpgradeManager.OnUpgradeResult -= OnUpgradeResult;
        CoreUpgradeManager.OnLevelChanged  -= OnLevelChanged;
        DataBoot.OnDataLoaded -= OnDataReady;
        Loc.OnLanguageChanged -= Refresh;
    }

    private void OnDataReady() => RefreshData();

    private void Update()
    {
        if (!IsPanelOpen()) return;

        SpinCoreRings();

        // 스핀 중 Space로도 정지
        if (_phase == CatchPhase.Spin && Input.GetKeyDown(KeyCode.Space))
        {
            StopSpin(false);
            return;
        }

        // F키 닫기 — 미니게임 진행 중에는 막음. 연 프레임엔 무시(같은 F 입력으로 바로 닫히는 깜빡임 방지).
        if (_phase == CatchPhase.Idle && Time.frameCount != _openedFrame && Input.GetKeyDown(KeyCode.F))
            Close();
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    public void Open()
    {
        panelRoot?.SetActive(true);
        UISoundManager.Instance?.PlayPanelOpen();   // 코어 강화 UI 열기음(기지 업그레이드 UI와 동일 패널음)
        _openedFrame = Time.frameCount;
        EnsureUpgradeLayout();            // 강화 버튼 위치/배경/발광 코드로 보장(메뉴 재실행 불필요)
        ResetCatchVisual();
        Refresh();
        PlayIntroSequence();              // 별자리 순차 등장(시각 연출, 강화는 즉시 가능)
        GameEvents.RaiseCoreUIOpened();   // 튜토리얼 'F로 단말 열기' 감지용
    }

    // 튜토리얼 objective(CoreOpenObjective)에서 현재 열림 상태 확인용
    public bool IsOpen => IsPanelOpen();

    public void HidePanel()
    {
        panelRoot?.SetActive(false);
    }

    public void Close()
    {
        if (_phase != CatchPhase.Idle) return;   // 미니게임 진행 중 닫기 방지
        LastCloseFrame = Time.frameCount;        // 같은 프레임에 터미널 F가 다시 여는 것 방지
        panelRoot?.SetActive(false);
        UISoundManager.Instance?.PlayPanelClose();   // 코어 강화 UI 닫기음(닫기 버튼/외부 닫기 공통)
        GameUIController.Instance?.CloseCoreUpgradeUI();
    }

    // ── 이벤트 콜백 ───────────────────────────────────────────────────

    // 결과 메시지/연출은 시퀀스(JudgeThenResult)가 담당 — 여기선 데이터만 갱신
    private void OnUpgradeResult(bool success) => RefreshData();
    private void OnLevelChanged(int newLevel)  => RefreshData();

    // ── UI 갱신 ───────────────────────────────────────────────────────

    private void Refresh()
    {
        RefreshData();
        if (_phase == CatchPhase.Idle) RefreshButton();
    }

    private void RefreshData()
    {
        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        var cur  = mgr.GetCurrentLevelData();
        var next = mgr.GetNextLevelData();
        bool isMax = (next == null) && DataBoot.IsLoaded;
        int lv = mgr.CurrentCoreLevel;

        if (titleText != null) titleText.text = $"{Loc.Get("코어")} <color=#5FC4FF>{Loc.Get("강화")}</color>";
        if (levelText != null) levelText.text = $"Lv.{lv} / 10";
        if (upgradeInfoGroup != null) upgradeInfoGroup.SetActive(!isMax);
        if (maxLevelGroup    != null) maxLevelGroup.SetActive(isMax);

        RefreshConstellation(lv);
        RefreshEffectList(mgr, cur, next, isMax, lv);
        RefreshKitStock(mgr);

        if (!isMax) RefreshKitPanel(mgr, next);
    }

    // ── 코어 비주얼 (구체 + 회전 링 + 페이드) ─────────────────────────

    // 두 링을 서로 다른 속도/방향으로 Z회전 = 자이로스코프 느낌. 정지(timeScale 0)에서도 돌게 unscaled.
    private void SpinCoreRings()
    {
        float dt = Time.unscaledDeltaTime;
        if (coreRingA != null) coreRingA.Rotate(0f, 0f, ringASpeedDeg * dt);
        if (coreRingB != null) coreRingB.Rotate(0f, 0f, ringBSpeedDeg * dt);
    }

    // 코어 비주얼(구체+링+후광) 통째 알파. CanvasGroup이 있으면 그걸로, 없으면 구체 색 폴백.
    private void SetCoreVisualAlpha(float a)
    {
        if (coreVisualGroup != null) { coreVisualGroup.alpha = a; return; }
        if (coreImage != null) { var c = coreImage.color; c.a = a; coreImage.color = c; }
    }

    // 코어의 panel 좌표(연출 생성용). 코어 비주얼은 CanvasGroup으로 묶여 시계 전환 시 투명해지므로,
    // 버스트/링 연출은 그 그룹 '밖'(panel)에 panel 좌표로 만들어야 시계 모드에서도 보인다.
    private bool GetCorePanelPos(out RectTransform parent, out Vector2 pos)
    {
        parent = null; pos = Vector2.zero;
        if (coreImage == null) return false;
        var coreRt   = coreImage.rectTransform;
        var visualRt = coreRt.parent as RectTransform;          // CoreVisual(묶음)
        parent = (visualRt != null ? visualRt.parent : coreRt.parent) as RectTransform;   // panel
        if (parent == null) return false;
        pos = visualRt != null ? visualRt.anchoredPosition : coreRt.anchoredPosition;
        return true;
    }

    // ── 오프닝 연출 (별자리 순차 등장) ────────────────────────────────

    // 패널 열릴 때 코어 팝 -> 노드 시계방향 순차 팝 + 연결선 따라 등장.
    // 순수 시각 연출이라 강화 버튼/패널은 즉시 활성(연출 중에도 바로 강화 가능).
    private void PlayIntroSequence()
    {
        if (!isActiveAndEnabled) return;
        if (_introCo != null) StopCoroutine(_introCo);
        _introCo = StartCoroutine(IntroRoutine());
    }

    // 배경 별 묶음 CanvasGroup (없으면 런타임에 Stars에 부착)
    private CanvasGroup EnsureStarsGroup()
    {
        if (_starsGroup != null) return _starsGroup;
        if (panelRoot == null) return null;
        var stars = panelRoot.transform.Find("Stars");
        if (stars == null) return null;
        _starsGroup = stars.GetComponent<CanvasGroup>();
        if (_starsGroup == null) _starsGroup = stars.gameObject.AddComponent<CanvasGroup>();
        return _starsGroup;
    }

    // 사이드 패널 = SetRef된 자식(효과행/키트텍스트)의 부모로 역참조 (빌더 수정 불필요)
    private RectTransform GetEffectPanel()
        => (effectRows != null && effectRows.Length > 0 && effectRows[0] != null)
            ? effectRows[0].transform.parent as RectTransform : null;

    private RectTransform GetKitStockPanel()
        => (kitStockTexts != null && kitStockTexts.Length > 0 && kitStockTexts[0] != null)
            ? kitStockTexts[0].transform.parent as RectTransform : null;

    // 패널 즉시 숨김 (오프닝 시작 시)
    private void HidePanelInstant(RectTransform panel)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
        cg.DOKill(); panel.DOKill();
        cg.alpha = 0f;
        panel.localScale = Vector3.one * 0.9f;
    }

    // 패널 등장 (페이드 + 살짝 스케일)
    private void RevealPanel(RectTransform panel)
    {
        if (panel == null) return;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.gameObject.AddComponent<CanvasGroup>();
        cg.DOFade(1f, 0.25f).SetUpdate(true);
        panel.DOScale(1f, 0.3f).SetUpdate(true).SetEase(Ease.OutBack);
    }

    private void RestorePanelInstant(RectTransform panel)
    {
        if (panel == null) return;
        panel.DOKill();
        panel.localScale = Vector3.one;
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) { cg.DOKill(); cg.alpha = 1f; }
    }

    private IEnumerator IntroRoutine()
    {
        var starsCg = EnsureStarsGroup();
        var ksPanel = GetKitStockPanel();
        var fxPanel = GetEffectPanel();
        Transform coreTr = coreVisualGroup != null ? coreVisualGroup.transform
                         : coreImage != null ? coreImage.transform : null;

        // === 0) 전부 숨긴 채 시작 ===
        if (starsCg != null) { starsCg.DOKill(); starsCg.alpha = 0f; }
        HidePanelInstant(ksPanel);
        HidePanelInstant(fxPanel);
        if (coreTr != null) { coreTr.DOKill(); coreTr.localScale = Vector3.zero; }
        if (constellationNodes != null)
            foreach (var nd in constellationNodes)
                if (nd != null) { nd.transform.DOKill(); nd.transform.localScale = Vector3.zero; }
        Color[] saved = null;
        if (_lines != null)
        {
            saved = new Color[_lines.Length];
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) continue;
                var im = _lines[i].GetComponent<Image>();
                if (im == null) continue;
                im.DOKill();
                saved[i] = im.color;
                var c = im.color; c.a = 0f; im.color = c;
            }
        }
        yield return new WaitForSecondsRealtime(0.2f);

        // === 1) 좌우 패널 동시 등장 ===
        RevealPanel(ksPanel);
        RevealPanel(fxPanel);
        yield return new WaitForSecondsRealtime(0.4f);

        // === 2) 별 배경 차오름 ===
        if (starsCg != null) starsCg.DOFade(1f, 0.45f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.6f);

        // === 3) 코어 중앙 팝 ===
        if (coreTr != null) coreTr.DOScale(1f, 0.3f).SetUpdate(true).SetEase(Ease.OutBack);
        yield return new WaitForSecondsRealtime(0.5f);

        // === 4) 노드 시계방향 순차 점등 + 연결선 따라 그려짐 (파바방) ===
        int n = constellationNodes != null ? constellationNodes.Length : 0;
        for (int i = 0; i < n; i++)
        {
            var node = constellationNodes[i];
            if (node != null)
                node.transform.DOScale(1f, 0.2f).SetUpdate(true).SetEase(Ease.OutBack);
            if (_lines != null && i < _lines.Length && _lines[i] != null && saved != null)
            {
                var im = _lines[i].GetComponent<Image>();
                if (im != null) im.DOColor(saved[i], 0.15f).SetUpdate(true);
            }
            yield return new WaitForSecondsRealtime(0.07f);
        }

        // === 5) 닫는 선(폐곡선) 마지막에 ===
        if (_lines != null && n < _lines.Length && _lines[n] != null && saved != null)
        {
            var im = _lines[n].GetComponent<Image>();
            if (im != null) im.DOColor(saved[n], 0.2f).SetUpdate(true);
        }

        _introCo = null;
    }

    // 오프닝 중 강화 클릭 등으로 즉시 마무리 (노드/코어/선 바로 복원)
    private void SkipIntro()
    {
        if (_introCo == null) return;
        StopCoroutine(_introCo); _introCo = null;

        // 별/패널 즉시 복원
        var starsCg = EnsureStarsGroup();
        if (starsCg != null) { starsCg.DOKill(); starsCg.alpha = 1f; }
        RestorePanelInstant(GetKitStockPanel());
        RestorePanelInstant(GetEffectPanel());

        Transform coreTr = coreVisualGroup != null ? coreVisualGroup.transform
                         : coreImage != null ? coreImage.transform : null;
        if (coreTr != null) { coreTr.DOKill(); coreTr.localScale = Vector3.one; }

        if (constellationNodes != null)
            foreach (var nd in constellationNodes)
                if (nd != null) { nd.transform.DOKill(); nd.transform.localScale = Vector3.one; }

        if (_lines != null)
            foreach (var ln in _lines)
                if (ln != null) { var im = ln.GetComponent<Image>(); if (im != null) im.DOKill(); }

        // 선/노드 색 재설정 (점등/미점등 상태로 복원)
        var mgr = CoreUpgradeManager.Instance;
        RefreshConstellation(mgr != null ? mgr.CurrentCoreLevel : 0);
    }

    // ── 별자리 (노드 점등 + 연결선) ───────────────────────────────────

    // i<level=달성(점등), i==level=다음 목표(골드 테두리), i>level=미점등.
    private void RefreshConstellation(int level)
    {
        BuildConstellationLines();

        if (constellationNodes != null)
        {
            for (int i = 0; i < constellationNodes.Length; i++)
            {
                var glow = constellationNodes[i];
                if (glow == null) continue;
                bool on     = i < level;
                bool target = i == level;
                glow.color = on ? GlowOn : (target ? GlowTarget : GlowOff);

                var core = glow.transform.Find("Core");
                if (core != null)
                {
                    var cimg = core.GetComponent<Image>();
                    if (cimg != null) cimg.color = on ? CoreDotOn : (target ? CoreDotTarget : CoreDotOff);
                    // 점등/목표 별점은 살짝 크게 = 빛나는 느낌
                    core.localScale = (on || target) ? Vector3.one * 1.35f : Vector3.one;
                }
            }
        }

        if (_lines != null)
        {
            int n = constellationNodes != null ? constellationNodes.Length : 0;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) continue;
                var img = _lines[i].GetComponent<Image>();
                if (img == null) continue;
                // 일반 선(i<n)은 i<level, 닫는 선(i==n)은 만렙(level>=n)일 때만 점등
                bool on = (i < n) ? (i < level) : (level >= n);
                img.color = on ? LineOn : LineOff;
            }
        }
    }

    // 코어->노드0, 노드(i-1)->노드i 를 잇는 연결선 런타임 생성 (1회). 코어/노드 뒤, 배경 앞.
    private void BuildConstellationLines()
    {
        if (_linesBuilt) return;
        if (constellationNodes == null || constellationNodes.Length == 0) return;
        if (coreImage == null) return;
        // 코어는 CoreVisual(묶음) 안에 있으므로 panel = 코어.parent.parent, 코어중심 = CoreVisual 위치.
        var coreRt   = coreImage.rectTransform;
        var visualRt = coreRt.parent as RectTransform;                 // CoreVisual
        var parent   = (visualRt != null ? visualRt.parent : coreRt.parent) as RectTransform;   // panel
        if (parent == null) return;
        _linesBuilt = true;

        // 선 전용 컨테이너 (코어 비주얼보다 뒤 = 더 작은 sibling index)
        var container = new GameObject("ConstellationLines", typeof(RectTransform));
        var crt = (RectTransform)container.transform;
        crt.SetParent(parent, false);
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta        = Vector2.zero;
        int coreSibling = visualRt != null ? visualRt.GetSiblingIndex() : coreRt.GetSiblingIndex();
        crt.SetSiblingIndex(Mathf.Max(0, coreSibling));

        int n = constellationNodes.Length;
        _lines = new RectTransform[n + 1];   // [n] = 마지막->처음 닫는 선(별자리 폐곡선, 만렙 때만 점등)
        Vector2 corePos = visualRt != null ? visualRt.anchoredPosition : coreRt.anchoredPosition;

        for (int i = 0; i < n; i++)
        {
            if (constellationNodes[i] == null) continue;
            Vector2 a = (i == 0 || constellationNodes[i - 1] == null) ? corePos : constellationNodes[i - 1].rectTransform.anchoredPosition;
            Vector2 b = constellationNodes[i].rectTransform.anchoredPosition;
            _lines[i] = MakeLine(crt, a, b);
        }

        // 닫는 선: 마지막 노드 -> 첫 노드 (10단계 완성 시 별자리가 닫힘)
        if (constellationNodes[n - 1] != null && constellationNodes[0] != null)
            _lines[n] = MakeLine(crt, constellationNodes[n - 1].rectTransform.anchoredPosition,
                                       constellationNodes[0].rectTransform.anchoredPosition);
    }

    private RectTransform MakeLine(RectTransform parent, Vector2 a, Vector2 b)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = b - a;
        float len = dir.magnitude;
        rt.sizeDelta        = new Vector2(len, 3f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        var img = go.GetComponent<Image>();
        img.color         = LineOff;
        img.raycastTarget = false;
        return rt;
    }

    // ── 효과 리스트 (현재 효과 + 다음 강화 미리보기, 미해금=???) ────────
    private void RefreshEffectList(CoreUpgradeManager mgr, CoreLevelDataSheetData cur, CoreLevelDataSheetData next, bool isMax, int lv)
    {
        if (effectRows == null || effectRows.Length < 4) return;
        int dashLv = PlayerDashComponent.DashUnlockCoreLevel;
        int lifeLv = mgr.LifestealUnlockLevel;

        // 0) 최대 시간(체력)
        if (cur != null)
        {
            string d = (!isMax && next != null && next.maxTime > cur.maxTime) ? PreviewTag($"+{next.maxTime - cur.maxTime}s") : "";
            SetEffectRow(effectRows[0], Loc.Get("최대 시간"), $"{cur.maxTime}s", d);
        }

        // 1) 부활 체력
        {
            float c = mgr.GetRespawnHpPercentAt(lv);
            string d = "";
            if (!isMax)
            {
                int dd = Mathf.RoundToInt((mgr.GetRespawnHpPercentAt(lv + 1) - c) * 100f);
                if (dd > 0) d = PreviewTag($"+{dd}%");
            }
            SetEffectRow(effectRows[1], Loc.Get("부활 체력"), Pct(c), d);
        }

        // 2) 우클릭 대쉬 (해금형)
        {
            bool have = lv >= dashLv;
            bool soon = !isMax && !have && (lv + 1 >= dashLv);
            if (have) SetEffectRow(effectRows[2], Loc.Get("우클릭 대쉬"), Loc.Get("사용 가능"), "");
            else      SetEffectRowLocked(effectRows[2], Loc.Get("우클릭 대쉬"), dashLv, soon);
        }

        // 3) 흡수율 (해금형)
        {
            bool have = mgr.IsLifestealUnlockedAt(lv);
            bool soon = !isMax && !have && mgr.IsLifestealUnlockedAt(lv + 1);
            if (have)
            {
                float c = mgr.GetLifestealPercentAt(lv);
                string d = "";
                if (!isMax && mgr.IsLifestealUnlockedAt(lv + 1))
                {
                    float dd = (mgr.GetLifestealPercentAt(lv + 1) - c) * 100f;
                    if (dd > 0.001f) d = PreviewTag($"+{dd:0.#}%");
                }
                SetEffectRow(effectRows[3], Loc.Get("흡수율"), PctF(c), d);
            }
            else SetEffectRowLocked(effectRows[3], Loc.Get("흡수율"), lifeLv, soon);
        }
    }

    private void SetEffectRow(TextMeshProUGUI row, string label, string val, string deltaTag)
    {
        if (row == null) return;
        row.gameObject.SetActive(true);
        row.text = $"<color=#8FA6BC>{label}</color>    <color=#EAF3FB>{val}</color>{deltaTag}";
    }

    private void SetEffectRowLocked(TextMeshProUGUI row, string label, int unlockLv, bool soon)
    {
        if (row == null) return;
        row.gameObject.SetActive(true);
        if (soon)
            row.text = $"<color=#FFD66B>{label}</color>    <color=#FFD66B>" + Loc.Get("다음 강화 해금!") + "</color>";
        else
            row.text = $"<color=#54657C>{label}</color>    <color=#54657C>??? ({unlockLv}" + Loc.Get("단계 해금") + ")</color>";
    }

    // 미리보기 태그(다음 강화 시 변화량) = 골드, 작게
    private static string PreviewTag(string t) => $"   <size=82%><color=#FFD66B>{t}</color></size>";

    // 코어 키트 ID (I~V). 좌측 보유량 패널용. CoreLevelData의 requiredKitItemId와 일치.
    private static readonly int[] KitItemIds = { 6101, 6102, 6103, 6104, 6105 };

    // 좌측 보유 코어 키트 패널 갱신 (가방 + 창고 합산)
    private void RefreshKitStock(CoreUpgradeManager mgr)
    {
        if (kitStockIcons == null && kitStockTexts == null) return;
        for (int i = 0; i < KitItemIds.Length; i++)
        {
            int id = KitItemIds[i];
            var item = GameDataUtility.GetItem(id);
            int count = mgr.GetTotalKitCount(id);

            if (kitStockIcons != null && i < kitStockIcons.Length && kitStockIcons[i] != null)
            {
                kitStockIcons[i].sprite  = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
                kitStockIcons[i].enabled = kitStockIcons[i].sprite != null;
            }
            if (kitStockTexts != null && i < kitStockTexts.Length && kitStockTexts[i] != null)
            {
                string nm = item != null ? item.GetLocalizedName() : Loc.Get("키트") + " " + (i + 1);
                kitStockTexts[i].text  = $"{nm}   <color=#EAF3FB>x{count}</color>";
                kitStockTexts[i].color = count > 0 ? new Color(0.78f, 0.86f, 0.94f) : new Color(0.42f, 0.5f, 0.6f);
            }
        }
    }

    // 강화 성공으로 새 노드가 켜질 때 버스트 링 + 노드 펀치
    private void PlayNodeLightUp(int index)
    {
        if (constellationNodes == null || index < 0 || index >= constellationNodes.Length) return;
        var node = constellationNodes[index];
        if (node == null) return;
        var parent = node.rectTransform.parent as RectTransform;
        if (parent == null) return;

        Vector2 pos = node.rectTransform.anchoredPosition;
        var ring = MakeFxImage("NodeBurst", parent, pos, 78f, UISpriteFactory.Ring(64, 4f));
        ring.color = CoreDotOn;
        ring.rectTransform.localScale = Vector3.one * 0.6f;
        ring.rectTransform.DOScale(2.3f, 0.55f).SetUpdate(true).SetEase(Ease.OutCubic);
        ring.DOFade(0f, 0.55f).SetUpdate(true).OnComplete(() => { if (ring != null) Destroy(ring.gameObject); });

        node.transform.DOKill();
        node.transform.localScale = Vector3.one;
        node.transform.DOPunchScale(Vector3.one * 0.4f, 0.5f, 6, 0.7f).SetUpdate(true);
    }

    // 10단계 완성: 중앙 큰 버스트 + 모든 연결선 골드 플래시
    private void PlayConstellationComplete()
    {
        if (!GetCorePanelPos(out var parent, out var c)) return;

        var ring = MakeFxImage("CompleteRing", parent, c, 200f, UISpriteFactory.Ring(128, 6f));
        ring.color = CoreDotTarget;
        ring.rectTransform.localScale = Vector3.one * 0.4f;
        ring.rectTransform.DOScale(3.2f, 0.9f).SetUpdate(true).SetEase(Ease.OutCubic);
        ring.DOFade(0f, 0.9f).SetUpdate(true).OnComplete(() => { if (ring != null) Destroy(ring.gameObject); });

        if (_lines != null)
            foreach (var ln in _lines)
            {
                if (ln == null) continue;
                var img = ln.GetComponent<Image>();
                if (img == null) continue;
                img.DOKill();
                img.color = CoreDotTarget;
                img.DOColor(LineOn, 0.8f).SetUpdate(true);
            }
    }

    // 강화 버튼 상태색 (가능 = 밝은 시안 강조 / 불가 = 어두운 회청 + 흐린 글자)
    private static readonly Color BtnReadyColor    = new Color(0.20f, 0.66f, 0.95f, 1f);
    private static readonly Color BtnLockedColor   = new Color(0.14f, 0.19f, 0.27f, 0.90f);
    private static readonly Color BtnTextReady     = Color.white;
    private static readonly Color BtnTextLocked    = new Color(0.52f, 0.60f, 0.70f, 1f);
    private static readonly Color BtnGlowReady     = new Color(0.45f, 0.88f, 1f,   0.95f);
    private static readonly Color BtnGlowLocked    = new Color(0.28f, 0.34f, 0.42f, 0.45f);
    private UnityEngine.UI.Outline _btnGlow;

    // 강화 버튼을 메뉴 재실행 없이도 또렷하게: 위치 위로 + 어두운 강철 스프라이트를 둥근 단색으로 교체 + 발광 테두리.
    private void EnsureUpgradeLayout()
    {
        if (upgradeButton == null) return;

        // 위치 (하단바 + 버튼 함께 위로, 노드와 28px 띄움)
        var infoGroup = upgradeButton.transform.parent;
        var bar = infoGroup != null ? infoGroup.Find("BottomBar") as RectTransform : null;
        if (bar != null) bar.anchoredPosition = new Vector2(0f, -320f);
        var btnRt = upgradeButton.GetComponent<RectTransform>();
        if (btnRt != null) { btnRt.anchoredPosition = new Vector2(0f, -434f); btnRt.sizeDelta = new Vector2(380f, 66f); }

        // 배경 = 둥근 단색(어두운 강철 스프라이트 대체 -> 색이 또렷하게 살아남)
        if (upgradeButton.image != null)
        {
            upgradeButton.image.sprite = UISpriteFactory.RoundedRect(96, 20);
            upgradeButton.image.type   = Image.Type.Simple;
        }

        // 발광 테두리 (활성 시 시안빛)
        _btnGlow = upgradeButton.GetComponent<UnityEngine.UI.Outline>();
        if (_btnGlow == null) _btnGlow = upgradeButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        _btnGlow.effectDistance = new Vector2(2.5f, -2.5f);
    }

    private void RefreshButton()
    {
        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        bool canUpgrade = mgr.CanUpgrade();
        if (upgradeButton != null)
        {
            upgradeButton.interactable = true;   // 클릭은 항상 받되(부족 토스트 안내), 색으로 가능여부 표시
            if (upgradeButton.image != null)
                upgradeButton.image.color = canUpgrade ? BtnReadyColor : BtnLockedColor;
        }
        if (_btnGlow != null)
            _btnGlow.effectColor = canUpgrade ? BtnGlowReady : BtnGlowLocked;
        if (upgradeButtonText != null)
        {
            upgradeButtonText.text  = canUpgrade ? Loc.Get("강화") : Loc.Get("키트 부족");
            upgradeButtonText.color = canUpgrade ? BtnTextReady : BtnTextLocked;
        }
    }

    private void RefreshKitPanel(CoreUpgradeManager mgr, CoreLevelDataSheetData next)
    {
        if (next == null) return;

        string kitIdStr = (string)next.requiredKitItemId;
        bool noKit = string.IsNullOrEmpty(kitIdStr) || kitIdStr == "-" || next.requiredAmount <= 0;

        if (noKit)
        {
            SetText(kitNameText,     Loc.Get("재료 불필요"));
            SetText(kitCountText,    "");
            SetText(kitShortageText, "");
            SetText(successRateText, "100%");
            if (gaugeFill != null) gaugeFill.fillAmount = 1f;
            if (kitIconImage != null) kitIconImage.gameObject.SetActive(false);
            return;
        }

        // 키트 필요 레벨: noKit에서 껐을 수 있는 슬롯/아이콘을 다시 켜 복구
        if (kitIconImage != null) kitIconImage.gameObject.SetActive(true);

        string kitName = GetKitName(next);
        SetText(kitNameText, Loc.Get("필요:") + " " + kitName);

        int owned = 0;
        if (int.TryParse(kitIdStr, out int kitItemId))
        {
            owned = mgr.GetTotalKitCount(kitItemId);
            if (kitNeedIcon != null)
            {
                var kitItem = GameDataUtility.GetItem(kitItemId);
                kitNeedIcon.sprite  = kitItem != null ? ItemDatabase.GetIcon(kitItem.iconKey) : null;
                kitNeedIcon.enabled = kitNeedIcon.sprite != null;
            }
        }

        int required = next.requiredAmount;
        if (kitCountText != null)
        {
            kitCountText.text  = Loc.Get("보유:") + "  " + owned + " / " + required;
            kitCountText.color = owned >= required ? Color.white : shortageColor;
        }

        if (kitShortageText != null)
        {
            int shortage = required - owned;
            if (shortage > 0)
            {
                kitShortageText.text  = string.Format(Loc.Get("← {0}개 부족"), shortage);
                kitShortageText.color = shortageColor;
                kitShortageText.gameObject.SetActive(true);
            }
            else kitShortageText.gameObject.SetActive(false);
        }

        if (successRateText != null)
        {
            int ratePct = Mathf.RoundToInt(next.successRate * 100f);
            successRateText.text  = Loc.Get("성공 확률:") + "  " + ratePct + "%";
            successRateText.color = ratePct >= 50 ? rateNormalColor : rateLowColor;
        }

        if (gaugeFill != null) gaugeFill.fillAmount = Mathf.Clamp01(next.successRate);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────

    private void OnClickUpgrade()
    {
        if (_phase != CatchPhase.Idle && _phase != CatchPhase.Spin) return;   // judge/result 중이면 무음·무시
        UISoundManager.Instance?.PlayButtonClick();   // 강화 버튼 클릭음(시작/정지 공통)
        if (_phase == CatchPhase.Spin) { StopSpin(false); return; }   // 정지!
        if (_phase != CatchPhase.Idle) return;                        // judge/result 중 무시

        SkipIntro();   // 오프닝 연출 중이면 즉시 마무리(강화엔 지장 없음)

        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        if (!mgr.CanUpgrade())
        {
            var blockedNext = mgr.GetNextLevelData();
            if (blockedNext != null && IsKitShort(mgr, blockedNext))
                ToastManager.Warning(Loc.Get("강화 키트가 부족합니다"));
            else
                ToastManager.Warning(Loc.Get("지금은 강화할 수 없습니다"));
            return;
        }

        StartSpin();
    }

    private void OnClickClose() => Close();

    // ── 미니게임 ──────────────────────────────────────────────────────

    private void StartSpin()
    {
        _phase = CatchPhase.Spin;
        SetupZones();

        if (judgeChipText != null) judgeChipText.gameObject.SetActive(false);
        if (spinHint != null) spinHint.SetActive(true);
        SetButtonStopMode(true);

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(CrossfadeCoreToClock(true));

        _spinElapsed = 0f;
        if (_spinCo != null) StopCoroutine(_spinCo);
        _spinCo = StartCoroutine(SpinRoutine());

        StartTickLoop();   // 바늘 도는 동안 째깍째깍
    }

    // 째깍 루프 — 클립은 GameSfxConfig(SfxId.CoreUpgradeTick)에서, 재생은 이 로컬 2D 소스에서.
    private void StartTickLoop()
    {
        if (!GameSfx.TryGet(SfxId.CoreUpgradeTick, out var clip, out var vol) || clip == null) return;
        if (_tickLoop == null)
        {
            var go = new GameObject("[CoreUpgradeTick]");
            go.transform.SetParent(transform, false);
            _tickLoop = go.AddComponent<AudioSource>();
            _tickLoop.spatialBlend = 0f;
            _tickLoop.loop = true;
            _tickLoop.playOnAwake = false;
        }
        _tickLoop.clip = clip;
        _tickLoop.volume = vol * GlobalSettingsManager.CurrentSFXVolume;
        _tickLoop.Play();
    }

    private void StopTickLoop()
    {
        if (_tickLoop != null && _tickLoop.isPlaying) _tickLoop.Stop();
    }

    private IEnumerator SpinRoutine()
    {
        while (true)
        {
            _spinElapsed += Time.unscaledDeltaTime;
            float ang = ((_spinElapsed / spinPeriod) * 360f) % 360f;
            if (clockNeedle != null) clockNeedle.localRotation = Quaternion.Euler(0f, 0f, -ang);
            if (_spinElapsed >= spinTimeout) { StopSpin(true); yield break; }
            yield return null;
        }
    }

    private void StopSpin(bool auto)
    {
        if (_phase != CatchPhase.Spin) return;
        if (_spinCo != null) { StopCoroutine(_spinCo); _spinCo = null; }
        StopTickLoop();   // 바늘 멈춤 → 째깍 정지
        _phase = CatchPhase.Judge;
        if (spinHint != null) spinHint.SetActive(false);

        // 상단(12시, 0deg) 기준 최소 각도 거리
        float ang = ((_spinElapsed / spinPeriod) * 360f) % 360f;
        float a = ang; if (a > 180f) a -= 360f; if (a < -180f) a += 360f;
        float dist = Mathf.Abs(a);

        int bonus; string tier; Color chipColor;
        if (auto || dist > zoneHalfDeg)        { tier = "MISS";    bonus = 0;               chipColor = missColor; }
        else if (dist <= perfectHalfDeg)       { tier = "PERFECT"; bonus = perfectBonusPct; chipColor = perfectColor; }
        else
        {
            float k = 1f - (dist - perfectHalfDeg) / (zoneHalfDeg - perfectHalfDeg);
            tier = "GOOD"; bonus = Mathf.RoundToInt(goodBonusPct * (0.4f + 0.6f * k)); chipColor = goodColor;
        }

        ShowJudgeChip(tier, bonus, chipColor);
        SetButtonBusyMode();
        AnimateGaugeToEffective(bonus);   // 미니게임 보너스만큼 게이지 차오름

        if (_seqCo != null) StopCoroutine(_seqCo);
        _seqCo = StartCoroutine(JudgeThenResult(bonus));
    }

    private IEnumerator JudgeThenResult(int bonusPct)
    {
        yield return new WaitForSecondsRealtime(judgeHold);

        var mgr = CoreUpgradeManager.Instance;
        bool success = (mgr != null) && mgr.TryUpgrade(bonusPct / 100f);

        _phase = CatchPhase.Result;
        ShowFeedback(success ? Loc.Get("강화 성공!") : Loc.Get("강화 실패"), success ? deltaColor : shortageColor);
        PlayResultEffect(success);   // 플래시 + 펀치 + 성공 버스트 / 실패 흔들림

        // 성공 시 새로 켜진 노드 점등 연출 (+ 10단계면 완성 연출). 노드 색은 RefreshData(OnLevelChanged)가 이미 갱신.
        if (success)
        {
            int newLv = mgr != null ? mgr.CurrentCoreLevel : 0;
            PlayNodeLightUp(newLv - 1);
            if (mgr != null && newLv >= mgr.MaxLevel) PlayConstellationComplete();
        }

        yield return new WaitForSecondsRealtime(resultHold);

        if (judgeChipText != null) judgeChipText.gameObject.SetActive(false);
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(CrossfadeCoreToClock(false));

        _phase = CatchPhase.Idle;
        Refresh();
    }

    private IEnumerator CrossfadeCoreToClock(bool toClock)
    {
        if (toClock && clockGroup != null) clockGroup.gameObject.SetActive(true);

        float e = 0f;
        while (e < crossfadeDur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / crossfadeDur);
            float clockA = toClock ? k : 1f - k;
            SetCoreVisualAlpha(1f - clockA);
            if (clockGroup != null) clockGroup.alpha = clockA;
            yield return null;
        }

        SetCoreVisualAlpha(toClock ? 0f : 1f);
        if (clockGroup != null)
        {
            clockGroup.alpha = toClock ? 1f : 0f;
            if (!toClock) clockGroup.gameObject.SetActive(false);
        }
        _fadeCo = null;
    }

    private void SetupZones()
    {
        if (successZoneImage != null)
        {
            successZoneImage.fillAmount = Mathf.Clamp01((zoneHalfDeg * 2f) / 360f);
            successZoneImage.transform.localRotation = Quaternion.Euler(0f, 0f, zoneHalfDeg);
        }
        if (perfectZoneImage != null)
        {
            perfectZoneImage.fillAmount = Mathf.Clamp01((perfectHalfDeg * 2f) / 360f);
            perfectZoneImage.transform.localRotation = Quaternion.Euler(0f, 0f, perfectHalfDeg);
        }
    }

    private void ShowJudgeChip(string tier, int bonus, Color color)
    {
        if (judgeChipText == null) return;
        judgeChipText.gameObject.SetActive(true);
        judgeChipText.text  = bonus > 0 ? $"{tier}  +{bonus}%" : tier;
        judgeChipText.color = color;
    }

    private void SetButtonStopMode(bool stop)
    {
        if (upgradeButtonText != null) upgradeButtonText.text = stop ? Loc.Get("정지!") : Loc.Get("강화");
        if (upgradeButton != null && upgradeButton.image != null)
            upgradeButton.image.color = stop ? stopButtonColor : _btnNormalColor;
    }

    private void SetButtonBusyMode()
    {
        if (upgradeButtonText != null) upgradeButtonText.text = Loc.Get("강화 중...");
        if (upgradeButton != null && upgradeButton.image != null)
        {
            Color grey = _btnNormalColor * 0.5f; grey.a = _btnNormalColor.a;
            upgradeButton.image.color = grey;
        }
    }

    private void ResetCatchVisual()
    {
        _phase = CatchPhase.Idle;
        if (_spinCo != null) { StopCoroutine(_spinCo); _spinCo = null; }
        if (_seqCo  != null) { StopCoroutine(_seqCo);  _seqCo  = null; }
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        StopTickLoop();   // 창 닫힘/리셋 중이면 째깍도 정지

        if (clockGroup != null) { clockGroup.alpha = 0f; clockGroup.gameObject.SetActive(false); }
        SetCoreVisualAlpha(1f);
        if (spinHint != null) spinHint.SetActive(false);
        if (judgeChipText != null) judgeChipText.gameObject.SetActive(false);
        if (clockNeedle != null) clockNeedle.localRotation = Quaternion.identity;

        // 결과 연출 잔여 정리
        if (gaugeFill != null) gaugeFill.DOKill();
        if (feedbackText != null) { feedbackText.transform.DOKill(); feedbackText.transform.localScale = Vector3.one; }
        if (_flashOverlay != null) { _flashOverlay.DOKill(); _flashOverlay.gameObject.SetActive(false); }
    }

    // ── 결과 연출 ─────────────────────────────────────────────────────

    // 미니게임 정지 보너스만큼 게이지를 기본확률 → 보정확률로 차오르게 (성공확률 상승 시각화)
    private void AnimateGaugeToEffective(int bonusPct)
    {
        if (gaugeFill == null) return;
        var mgr  = CoreUpgradeManager.Instance;
        var next = mgr != null ? mgr.GetNextLevelData() : null;
        if (next == null) return;

        float baseRate = Mathf.Clamp01(next.successRate);
        float eff      = Mathf.Clamp01(baseRate + bonusPct / 100f);

        gaugeFill.DOKill();
        gaugeFill.fillAmount = baseRate;
        gaugeFill.DOFillAmount(eff, 0.4f).SetUpdate(true).SetEase(Ease.OutQuad);

        if (successRateText != null && bonusPct > 0)
        {
            int effPct = Mathf.RoundToInt(eff * 100f);
            successRateText.text  = Loc.Get("성공 확률:") + "  " + effPct + "%  (+" + bonusPct + ")";
            successRateText.color = rateNormalColor;
        }
    }

    private void PlayResultEffect(bool success)
    {
        // 전체화면 플래시 (초록=성공 / 빨강=실패)
        EnsureFlashOverlay();
        if (_flashOverlay != null)
        {
            _flashOverlay.DOKill();
            _flashOverlay.color = success ? successFlashColor : failFlashColor;
            _flashOverlay.gameObject.SetActive(true);
            _flashOverlay.DOFade(0f, 0.45f).SetUpdate(true)
                .OnComplete(() => { if (_flashOverlay != null) _flashOverlay.gameObject.SetActive(false); });
        }

        // 결과 텍스트 펀치
        if (feedbackText != null)
        {
            feedbackText.transform.DOKill();
            feedbackText.transform.localScale = Vector3.one;
            feedbackText.transform.DOPunchScale(Vector3.one * 0.35f, 0.5f, 6, 0.7f).SetUpdate(true);
        }

        if (success)
        {
            PlayFrameBurst(successBurstFrames, Color.white, 460f, 0.04f);   // 디자인 성공 폭발/광채
        }
        else
        {
            PlayFrameBurst(failBurstFrames, Color.white, 420f, 0.045f);     // 디자인 실패 균열/파편
            PlayFailShake();
        }
    }

    // 프레임 시퀀스(폭발/광채 등)를 코어 자리에 한 번 재생. tint=Color.white면 PNG 원색 그대로.
    private void PlayFrameBurst(Sprite[] frames, Color tint, float size, float frameDur)
    {
        if (frames == null || frames.Length == 0) return;
        if (!GetCorePanelPos(out var parent, out var pos)) return;
        StartCoroutine(FrameBurstRoutine(frames, parent, pos, tint, size, frameDur));
    }

    private IEnumerator FrameBurstRoutine(Sprite[] frames, RectTransform parent, Vector2 pos, Color tint, float size, float frameDur)
    {
        var img = MakeFxImage("FrameBurst", parent, pos, size, frames[0]);
        img.color = tint;
        for (int i = 0; i < frames.Length; i++)
        {
            if (img == null) yield break;
            if (frames[i] != null) img.sprite = frames[i];
            yield return new WaitForSecondsRealtime(frameDur);
        }
        if (img != null) Destroy(img.gameObject);
    }

    private void EnsureFlashOverlay()
    {
        if (_flashOverlay != null || panelRoot == null) return;
        var go = new GameObject("ResultFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(panelRoot.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        _flashOverlay = go.GetComponent<Image>();
        _flashOverlay.raycastTarget = false;
        _flashOverlay.color = new Color(0f, 0f, 0f, 0f);
        go.SetActive(false);
    }

    // 실패: 시계(코어) 영역 흔들기
    private void PlayFailShake()
    {
        RectTransform t = clockGroup != null ? clockGroup.transform as RectTransform
                        : coreVisualGroup != null ? coreVisualGroup.transform as RectTransform
                        : coreImage != null ? coreImage.rectTransform : null;
        if (t == null) return;
        t.DOShakeAnchorPos(0.4f, new Vector2(16f, 7f), 18, 90, false, true).SetUpdate(true);
    }

    private Image MakeFxImage(string name, RectTransform parent, Vector2 anchoredPos, float size, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        rt.SetAsLastSibling();
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // ── 피드백 ────────────────────────────────────────────────────────

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = StartCoroutine(FeedbackRoutine(message, color));
    }

    private IEnumerator FeedbackRoutine(string message, Color color)
    {
        feedbackText.text  = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
        _feedbackRoutine = null;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private bool IsPanelOpen() => panelRoot != null && panelRoot.activeSelf;

    private static string Pct(float f) => $"{Mathf.RoundToInt(f * 100f)}%";
    private static string PctF(float f) => $"{f * 100f:0.#}%";   // 소수 1자리(흡수 0.5% 단위 등)

    private void SetText(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }

    private bool IsKitShort(CoreUpgradeManager mgr, CoreLevelDataSheetData next)
    {
        string kitIdStr = (string)next.requiredKitItemId;
        if (string.IsNullOrEmpty(kitIdStr) || kitIdStr == "-" || next.requiredAmount <= 0) return false;
        if (!int.TryParse(kitIdStr, out int kitItemId)) return false;
        return mgr.GetTotalKitCount(kitItemId) < next.requiredAmount;
    }

    private string GetKitName(CoreLevelDataSheetData data)
    {
        if (data == null) return "";
        string kitIdStr = (string)data.requiredKitItemId;
        if (string.IsNullOrEmpty(kitIdStr) || kitIdStr == "-") return "";
        if (!int.TryParse(kitIdStr, out int kitItemId)) return kitIdStr;
        var itemData = GameDataUtility.GetItem(kitItemId);
        return itemData != null ? itemData.GetLocalizedName() : kitIdStr;
    }
}
