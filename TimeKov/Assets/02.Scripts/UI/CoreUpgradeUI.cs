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

    // ── 레벨 표시 ─────────────────────────────────────────────────────
    [Header("레벨")]
    [SerializeField] private TextMeshProUGUI levelText;

    // ── 코어 비주얼 ───────────────────────────────────────────────────
    [Header("코어 비주얼")]
    [SerializeField] private Image coreImage;

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
    [SerializeField] private int   perfectBonusPct = 45;
    [SerializeField] private int   goodBonusPct    = 22;
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
    private Image _flashOverlay;   // 런타임 생성 전체화면 플래시

    // 부가 스탯(부활/흡수) 행 — 런타임 생성(빌더 재실행 불필요, 현재 패널 위에 얹음)
    private bool _extraStatsBuilt;
    private TextMeshProUGUI _curRespawnText, _curLifestealText, _nextRespawnText, _nextLifestealText;

    // ── 내부 ──────────────────────────────────────────────────────────
    private enum CatchPhase { Idle, Spin, Judge, Result }
    private CatchPhase _phase = CatchPhase.Idle;
    private float _spinElapsed;
    private Coroutine _spinCo;
    private Coroutine _seqCo;
    private Coroutine _fadeCo;
    private Coroutine _feedbackRoutine;
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
    }

    private void OnDisable()
    {
        CoreUpgradeManager.OnUpgradeResult -= OnUpgradeResult;
        CoreUpgradeManager.OnLevelChanged  -= OnLevelChanged;
        DataBoot.OnDataLoaded -= OnDataReady;
    }

    private void OnDataReady() => RefreshData();

    private void Update()
    {
        if (!IsPanelOpen()) return;

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
        _openedFrame = Time.frameCount;
        ResetCatchVisual();
        Refresh();
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

        if (levelText != null) levelText.text = $"Lv.{mgr.CurrentCoreLevel} / 10";

        if (upgradeInfoGroup != null) upgradeInfoGroup.SetActive(!isMax);
        if (maxLevelGroup    != null) maxLevelGroup.SetActive(isMax);

        if (cur != null) SetText(currentTimeText, $"{cur.maxTime}s");

        // 부가 스탯(부활/흡수) — 현재 레벨 값
        BuildExtraStatRows();
        int lv = mgr.CurrentCoreLevel;
        if (_curRespawnText != null)
        {
            _curRespawnText.text   = $"부활 체력   {Pct(mgr.GetRespawnHpPercentAt(lv))}";
            _curLifestealText.text = $"흡수율   {Pct(mgr.GetLifestealPercentAt(lv))}";
        }

        if (isMax) return;

        if (next != null && cur != null)
        {
            SetText(nextTimeText, $"{next.maxTime}s");
            SetDelta(deltaTimeText, next.maxTime - cur.maxTime, "s");

            if (_nextRespawnText != null)
            {
                _nextRespawnText.text   = $"부활 체력   {Pct(mgr.GetRespawnHpPercentAt(lv + 1))}";
                _nextLifestealText.text = $"흡수율   {Pct(mgr.GetLifestealPercentAt(lv + 1))}";
            }
        }
        RefreshKitPanel(mgr, next);
    }

    private void RefreshButton()
    {
        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        bool canUpgrade = mgr.CanUpgrade();
        if (upgradeButton != null)
        {
            upgradeButton.interactable = true;
            if (upgradeButton.image != null)
            {
                Color grey = _btnNormalColor * 0.5f; grey.a = _btnNormalColor.a;
                upgradeButton.image.color = canUpgrade ? _btnNormalColor : grey;
            }
        }
        if (upgradeButtonText != null) upgradeButtonText.text = "강화";
    }

    private void RefreshKitPanel(CoreUpgradeManager mgr, CoreLevelDataSheetData next)
    {
        if (next == null) return;

        string kitIdStr = (string)next.requiredKitItemId;
        bool noKit = string.IsNullOrEmpty(kitIdStr) || kitIdStr == "-" || next.requiredAmount <= 0;

        if (noKit)
        {
            SetText(kitNameText,     "재료 불필요");
            SetText(kitCountText,    "");
            SetText(kitShortageText, "");
            SetText(successRateText, "100%");
            if (gaugeFill != null) gaugeFill.fillAmount = 1f;
            if (kitIconImage != null) kitIconImage.gameObject.SetActive(false);
            return;
        }

        string kitName = GetKitName(next);
        SetText(kitNameText, $"필요: {kitName}");

        int owned = 0;
        if (int.TryParse(kitIdStr, out int kitItemId))
            owned = mgr.GetTotalKitCount(kitItemId);

        int required = next.requiredAmount;
        if (kitCountText != null)
        {
            kitCountText.text  = $"보유:  {owned} / {required}";
            kitCountText.color = owned >= required ? Color.white : shortageColor;
        }

        if (kitShortageText != null)
        {
            int shortage = required - owned;
            if (shortage > 0)
            {
                kitShortageText.text  = $"← {shortage}개 부족";
                kitShortageText.color = shortageColor;
                kitShortageText.gameObject.SetActive(true);
            }
            else kitShortageText.gameObject.SetActive(false);
        }

        if (successRateText != null)
        {
            int ratePct = Mathf.RoundToInt(next.successRate * 100f);
            successRateText.text  = $"성공 확률:  {ratePct}%";
            successRateText.color = ratePct >= 50 ? rateNormalColor : rateLowColor;
        }

        if (gaugeFill != null) gaugeFill.fillAmount = Mathf.Clamp01(next.successRate);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────

    private void OnClickUpgrade()
    {
        if (_phase == CatchPhase.Spin) { StopSpin(false); return; }   // 정지!
        if (_phase != CatchPhase.Idle) return;                        // judge/result 중 무시

        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        if (!mgr.CanUpgrade())
        {
            var blockedNext = mgr.GetNextLevelData();
            if (blockedNext != null && IsKitShort(mgr, blockedNext))
                ToastManager.Warning("강화 키트가 부족합니다");
            else
                ToastManager.Warning("지금은 강화할 수 없습니다");
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
        ShowFeedback(success ? "강화 성공!" : "강화 실패", success ? deltaColor : shortageColor);
        PlayResultEffect(success);   // 플래시 + 펀치 + 성공 버스트 / 실패 흔들림

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
        Color cc = coreImage != null ? coreImage.color : Color.white;
        while (e < crossfadeDur)
        {
            e += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(e / crossfadeDur);
            float clockA = toClock ? k : 1f - k;
            if (coreImage != null) { cc.a = 1f - clockA; coreImage.color = cc; }
            if (clockGroup != null) clockGroup.alpha = clockA;
            yield return null;
        }

        if (coreImage != null) { cc.a = toClock ? 0f : 1f; coreImage.color = cc; }
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
        if (upgradeButtonText != null) upgradeButtonText.text = stop ? "정지!" : "강화";
        if (upgradeButton != null && upgradeButton.image != null)
            upgradeButton.image.color = stop ? stopButtonColor : _btnNormalColor;
    }

    private void SetButtonBusyMode()
    {
        if (upgradeButtonText != null) upgradeButtonText.text = "강화 중...";
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

        if (clockGroup != null) { clockGroup.alpha = 0f; clockGroup.gameObject.SetActive(false); }
        if (coreImage != null) { var c = coreImage.color; c.a = 1f; coreImage.color = c; }
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
            successRateText.text  = $"성공 확률:  {effPct}%  (+{bonusPct})";
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

        if (success) PlaySuccessBurst();
        else         PlayFailShake();
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

    // 성공: 코어 자리에 링 + 디스크 버스트
    private void PlaySuccessBurst()
    {
        if (coreImage == null) return;
        var parent = coreImage.rectTransform.parent as RectTransform;
        if (parent == null) return;
        Vector2 pos = coreImage.rectTransform.anchoredPosition;

        var ring = MakeFxImage("ResultRing", parent, pos, 120f, UISpriteFactory.Ring(96, 5f));
        ring.color = perfectColor;
        ring.rectTransform.localScale = Vector3.one * 0.6f;
        ring.rectTransform.DOScale(2.6f, 0.6f).SetUpdate(true).SetEase(Ease.OutCubic);
        ring.DOFade(0f, 0.6f).SetUpdate(true).OnComplete(() => { if (ring != null) Destroy(ring.gameObject); });

        var disc = MakeFxImage("ResultDisc", parent, pos, 110f, UISpriteFactory.Disc(96));
        disc.color = new Color(goodColor.r, goodColor.g, goodColor.b, 0.7f);
        disc.rectTransform.localScale = Vector3.one * 0.5f;
        disc.rectTransform.DOScale(1.8f, 0.5f).SetUpdate(true).SetEase(Ease.OutCubic);
        disc.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(() => { if (disc != null) Destroy(disc.gameObject); });
    }

    // 실패: 시계(코어) 영역 흔들기
    private void PlayFailShake()
    {
        RectTransform t = clockGroup != null ? clockGroup.transform as RectTransform
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

    // 현재/강화후 카드에 부활·흡수 두 줄을 런타임 생성 (빌더 재실행 없이 현재 패널 위에 얹음)
    private void BuildExtraStatRows()
    {
        if (_extraStatsBuilt) return;
        if (currentTimeText == null || nextTimeText == null) return;

        var leftCard  = currentTimeText.transform.parent as RectTransform;
        var rightCard = nextTimeText.transform.parent as RectTransform;
        if (leftCard == null || rightCard == null) return;

        _extraStatsBuilt = true;

        EnlargeCard(leftCard);
        EnlargeCard(rightCard);

        Color sub = new Color(0.68f, 0.75f, 0.82f);   // 현재(보조)
        Color hi  = new Color(0.68f, 0.89f, 1f);      // 강화 후(강조)

        _curRespawnText    = MakeStatRow(leftCard,  -92f,  sub);
        _curLifestealText  = MakeStatRow(leftCard,  -120f, sub);
        _nextRespawnText   = MakeStatRow(rightCard, -92f,  hi);
        _nextLifestealText = MakeStatRow(rightCard, -120f, hi);
    }

    private static void EnlargeCard(RectTransform card)
    {
        var s = card.sizeDelta;
        if (s.y < 280f) card.sizeDelta = new Vector2(s.x, 290f);
    }

    private TextMeshProUGUI MakeStatRow(RectTransform parent, float y, Color color)
    {
        var go = new GameObject("StatRow", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(250f, 26f);
        rt.anchoredPosition = new Vector2(0f, y);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (currentTimeText.font != null) tmp.font = currentTimeText.font;
        tmp.fontSize  = 18;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void SetText(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }

    private void SetDelta(TextMeshProUGUI tmp, int delta, string suffix)
    {
        if (tmp == null) return;
        tmp.text  = delta > 0 ? $"+{delta}{suffix} ↑" : $"{delta}{suffix}";
        tmp.color = deltaColor;
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
        return itemData != null ? itemData.itemName : kitIdStr;
    }
}
