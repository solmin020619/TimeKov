// =====================================================================
// CoreUpgradeUI.cs
// 코어 강화 패널 UI
// CoreUpgradeTerminal.cs → GameUIController.OpenCoreUpgradeUI() → Open()
// =====================================================================

using System.Collections;
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
    [SerializeField] private TextMeshProUGUI levelText;         // "Lv.3 / 10"

    // ── 코어 비주얼 (플레이스홀더 — 나중에 이미지/스프라이트 교체) ──
    [Header("코어 비주얼 (Image 플레이스홀더)")]
    [SerializeField] private Image coreImage;

    // ── 현재 스탯 패널 ────────────────────────────────────────────────
    [Header("현재 스탯")]
    [SerializeField] private TextMeshProUGUI currentTimeText;
    [SerializeField] private TextMeshProUGUI currentStaminaText;
    [SerializeField] private TextMeshProUGUI currentAtkText;
    [SerializeField] private TextMeshProUGUI currentDefText;

    // ── 강화 후 스탯 패널 ─────────────────────────────────────────────
    [Header("강화 후 스탯")]
    [SerializeField] private TextMeshProUGUI nextTimeText;
    [SerializeField] private TextMeshProUGUI nextStaminaText;
    [SerializeField] private TextMeshProUGUI nextAtkText;
    [SerializeField] private TextMeshProUGUI nextDefText;

    // ── 증가량 텍스트 (+60s ↑) ────────────────────────────────────────
    [Header("증가량 (+N ↑)")]
    [SerializeField] private TextMeshProUGUI deltaTimeText;
    [SerializeField] private TextMeshProUGUI deltaStaminaText;
    [SerializeField] private TextMeshProUGUI deltaAtkText;
    [SerializeField] private TextMeshProUGUI deltaDefText;

    // ── 재료 패널 ─────────────────────────────────────────────────────
    [Header("재료 패널")]
    [SerializeField] private Image kitIconImage;                // 키트 아이콘 (나중에 교체)
    [SerializeField] private TextMeshProUGUI kitNameText;       // "내장 코어 보강 키트 II"
    [SerializeField] private TextMeshProUGUI kitCountText;      // "1 / 2"
    [SerializeField] private TextMeshProUGUI kitShortageText;   // "← 1개 부족"  (충족 시 숨김)
    [SerializeField] private TextMeshProUGUI successRateText;   // "85%"

    // ── 강화 버튼 ─────────────────────────────────────────────────────
    [Header("버튼")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private Button closeButton;

    // ── 최대 단계 / 강화 정보 그룹 ────────────────────────────────────
    [Header("그룹 (최대 단계 분기)")]
    [SerializeField] private GameObject upgradeInfoGroup;   // 일반 강화 정보 (MAX면 숨김)
    [SerializeField] private GameObject maxLevelGroup;      // MAX 표시 (MAX일 때만 보임)

    // ── 피드백 텍스트 (성공/실패 메시지) ─────────────────────────────
    [Header("피드백")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private float feedbackDuration = 2.5f;

    // ── 색상 설정 ─────────────────────────────────────────────────────
    [Header("색상")]
    [SerializeField] private Color deltaColor     = new Color(0.2f, 0.9f, 0.4f);   // 증가량 초록
    [SerializeField] private Color shortageColor  = new Color(1f,   0.3f, 0.3f);   // 부족 빨강
    [SerializeField] private Color rateNormalColor = new Color(0.4f, 0.8f, 1f);    // 성공률 하늘색
    [SerializeField] private Color rateLowColor   = new Color(1f,   0.6f, 0.2f);   // 50% 미만 주황

    // ── 내부 ──────────────────────────────────────────────────────────
    private Coroutine _feedbackRoutine;

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        upgradeButton?.onClick.AddListener(OnClickUpgrade);
        closeButton?.onClick.AddListener(OnClickClose);

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        panelRoot?.SetActive(false);
    }

    private void OnEnable()
    {
        CoreUpgradeManager.OnUpgradeResult += OnUpgradeResult;
        CoreUpgradeManager.OnLevelChanged  += OnLevelChanged;
    }

    private void OnDisable()
    {
        CoreUpgradeManager.OnUpgradeResult -= OnUpgradeResult;
        CoreUpgradeManager.OnLevelChanged  -= OnLevelChanged;
    }

    private void Update()
    {
        if (!IsPanelOpen()) return;

        // F키 토글 닫기 (IsBlocked 상태에서도 직접 감지)
        if (Input.GetKeyDown(KeyCode.F))
            Close();
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    public void Open()
    {
        panelRoot?.SetActive(true);
        Refresh();
    }

    /// <summary>GameUIController.CloseAll() 에서 순환 호출 없이 패널만 숨길 때 사용</summary>
    public void HidePanel()
    {
        panelRoot?.SetActive(false);
    }

    public void Close()
    {
        panelRoot?.SetActive(false);
        GameUIController.Instance?.CloseCoreUpgradeUI();
    }

    // ── 이벤트 콜백 ───────────────────────────────────────────────────

    private void OnUpgradeResult(bool success)
    {
        Refresh();

        if (success)
            ShowFeedback("강화 성공!", deltaColor);
        else
        {
            var nextData = CoreUpgradeManager.Instance?.GetNextLevelData();
            string kitName = GetKitName(nextData);
            int amount = nextData != null ? nextData.requiredAmount : 0;
            ShowFeedback($"강화 실패.  {kitName} {amount}개가 소모되었습니다.", shortageColor);
        }
    }

    private void OnLevelChanged(int newLevel)
    {
        Refresh();
    }

    // ── UI 갱신 ───────────────────────────────────────────────────────

    private void Refresh()
    {
        var mgr = CoreUpgradeManager.Instance;
        if (mgr == null) return;

        var cur  = mgr.GetCurrentLevelData();
        var next = mgr.GetNextLevelData();
        bool isMax = next == null;

        // 레벨 텍스트
        if (levelText != null)
            levelText.text = $"Lv.{mgr.CurrentCoreLevel} / 10";

        // 최대 단계 분기
        if (upgradeInfoGroup != null) upgradeInfoGroup.SetActive(!isMax);
        if (maxLevelGroup    != null) maxLevelGroup.SetActive(isMax);

        // 현재 스탯
        if (cur != null)
        {
            SetText(currentTimeText,    $"Time : {cur.maxTime}s");
            SetText(currentStaminaText, $"Stamina : {cur.stamina}");
            SetText(currentAtkText,     $"ATK : {cur.atk}");
            SetText(currentDefText,     $"DEF : {cur.def}");
        }

        if (isMax)
        {
            upgradeButton?.gameObject.SetActive(false);
            return;
        }

        // 강화 후 스탯 + 증가량
        if (next != null && cur != null)
        {
            SetText(nextTimeText,    $"Time : {next.maxTime}s");
            SetText(nextStaminaText, $"Stamina : {next.stamina}");
            SetText(nextAtkText,     $"ATK : {next.atk}");
            SetText(nextDefText,     $"DEF : {next.def}");

            SetDelta(deltaTimeText,    next.maxTime - cur.maxTime,    "s");
            SetDelta(deltaStaminaText, next.stamina - cur.stamina,    "");
            SetDelta(deltaAtkText,     next.atk     - cur.atk,        "");
            SetDelta(deltaDefText,     next.def      - cur.def,       "");
        }

        // 재료 패널
        RefreshKitPanel(mgr, next);

        // 강화 버튼 활성화
        bool canUpgrade = mgr.CanUpgrade();
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
        if (upgradeButtonText != null)
            upgradeButtonText.text = "강화";
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
            if (kitIconImage != null) kitIconImage.gameObject.SetActive(false);
            return;
        }

        // 키트 이름
        string kitName = GetKitName(next);
        SetText(kitNameText, $"필요: {kitName}");

        // 보유 수량
        int owned = 0;
        if (int.TryParse(kitIdStr, out int kitItemId))
            owned = mgr.GetTotalKitCount(kitItemId);

        int required = next.requiredAmount;
        if (kitCountText != null)
        {
            kitCountText.text = $"보유:  {owned} / {required}";
            kitCountText.color = owned >= required ? Color.white : shortageColor;
        }

        // 부족 표시
        if (kitShortageText != null)
        {
            int shortage = required - owned;
            if (shortage > 0)
            {
                kitShortageText.text  = $"← {shortage}개 부족";
                kitShortageText.color = shortageColor;
                kitShortageText.gameObject.SetActive(true);
            }
            else
            {
                kitShortageText.gameObject.SetActive(false);
            }
        }

        // 성공 확률
        if (successRateText != null)
        {
            int ratePct = Mathf.RoundToInt(next.successRate * 100f);
            successRateText.text  = $"성공 확률:  {ratePct}%";
            successRateText.color = ratePct >= 50 ? rateNormalColor : rateLowColor;
        }
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────

    private void OnClickUpgrade()
    {
        CoreUpgradeManager.Instance?.TryUpgrade();
    }

    private void OnClickClose()
    {
        Close();
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
        yield return new WaitForSeconds(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
        _feedbackRoutine = null;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private bool IsPanelOpen() => panelRoot != null && panelRoot.activeSelf;

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
