// =====================================================================
// GameUIController.cs
// UI 상태 통합 관리
// - ESC → 설정창 직접 열기
// - UI 열려있는 동안 다른 UI 열기 차단
// - 커서 / 게임플레이 입력 플래그 관리
// =====================================================================

using UnityEngine;

public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    /// <summary>게임플레이 입력이 활성화된 상태인지 (UI 미오픈 = true)</summary>
    public static bool GameplayInputEnabled { get; private set; } = true;

    public enum UIState
    {
        None,
        Settings,   // ESC로 열리는 설정·일시정지 통합창
        Factory,    // 설비 UI (MachineInteraction이 직접 관리)
        Build,      // 건설 모드 (BuildManager가 직접 관리)
        Quest       // 퀘스트 수락/조회 팝업 (J키)
    }

    [Header("Settings Panel")]
    [Tooltip("ESC로 열리는 설정창 루트 패널")]
    public GameObject settingsPanel;

    [Header("Build Mode UI")]
    [Tooltip("건설 모드에서 표시되는 퀵슬롯 UI")]
    public GameObject quickSlotUI;

    [Header("Quest")]
    [Tooltip("J키로 여닫는 퀘스트 수락/조회 팝업 패널")]
    public GameObject questPanel;

    [Tooltip("항상 화면에 표시되는 퀘스트 HUD — CanvasGroup 필수 (SetActive 대신 alpha로 숨겨 QuestPanelUI 구독 유지)")]
    public GameObject questHud;

    [Header("Player HUD")]
    [Tooltip("HP·스태미나 등 플레이어 상태 HUD — 다른 UI가 열리면 숨겨짐")]
    public GameObject playerHud;

    private UIState _currentState = UIState.None;
    private BuildManager _buildManager;
    private CanvasGroup _questHudGroup;

    // ── 초기화 ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _buildManager = FindAnyObjectByType<BuildManager>();

        if (questHud != null)
            _questHudGroup = questHud.GetComponent<CanvasGroup>();
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();

        if (Input.GetKeyDown(KeyCode.J))
            ToggleQuest();

        RefreshCursorState();
    }

    // ── 공개 쿼리 ────────────────────────────────────────────────────

    public UIState GetCurrentState() => _currentState;

    /// <summary>어떤 UI든 열려있으면 true — 카메라 줌, 다른 UI 열기 차단에 사용</summary>
    public bool IsUIBlocking() => _currentState != UIState.None;

    // ── ESC 처리 ─────────────────────────────────────────────────────

    public void HandleEscape()
    {
        if (_buildManager == null)
            _buildManager = FindAnyObjectByType<BuildManager>();

        // 건설 모드 ESC는 BuildManager가 처리
        if (_buildManager != null && _buildManager.IsBuildMode)
            return;

        if (_currentState == UIState.None)
        {
            OpenSettings();
            return;
        }

        CloseAll();
    }

    // ── 전체 닫기 ────────────────────────────────────────────────────

    public void CloseAll()
    {
        _currentState = UIState.None;
        ApplyState();
    }

    /// <summary>이전 API 호환 — CloseAll()과 동일</summary>
    public void CloseAllUI() => CloseAll();

    // ── 설정창 ───────────────────────────────────────────────────────

    public void OpenSettings()
    {
        if (_currentState != UIState.None && _currentState != UIState.Settings) return;
        _currentState = UIState.Settings;
        ApplyState();
    }

    public void CloseSettings()
    {
        if (_currentState != UIState.Settings) return;
        _currentState = UIState.None;
        ApplyState();
    }

    // ── 설비 UI ──────────────────────────────────────────────────────

    public void OpenFactoryUI()
    {
        if (_currentState != UIState.None) return;
        _currentState = UIState.Factory;
        ApplyState();
    }

    public void CloseFactoryUI()
    {
        if (_currentState != UIState.Factory) return;
        _currentState = UIState.None;
        ApplyState();
    }

    // ── 퀘스트 팝업 ──────────────────────────────────────────────────

    public void ToggleQuest()
    {
        if (_currentState == UIState.Quest) { CloseAll(); return; }
        if (_currentState != UIState.None) return;
        _currentState = UIState.Quest;
        ApplyState();
    }

    // ── 상태 직접 설정 (BuildManager 연동용) ─────────────────────────

    public void SetState(UIState newState)
    {
        _currentState = newState;
        ApplyState();
    }

    // ── 호환 래퍼 (구 Pause 시스템) ──────────────────────────────────

    /// <summary>구 PauseMenuManager 호환용</summary>
    public void OpenPauseSettings()  => OpenSettings();
    /// <summary>구 PauseMenuManager 호환용</summary>
    public void ClosePauseSettings() => CloseSettings();

    // ── 내부 상태 적용 ───────────────────────────────────────────────

    protected virtual void ApplyState()
    {
        // 설정창
        if (settingsPanel != null)
            settingsPanel.SetActive(_currentState == UIState.Settings);

        // 퀵슬롯 UI — 건설 모드에서만 표시
        if (quickSlotUI != null)
            quickSlotUI.SetActive(_currentState == UIState.Build);

        // 퀘스트 팝업 — Quest 상태에서만 표시
        if (questPanel != null)
            questPanel.SetActive(_currentState == UIState.Quest);

        // 퀘스트 HUD — CanvasGroup으로 숨김 (SetActive 금지: QuestPanelUI 구독 해제 방지)
        if (_questHudGroup != null)
        {
            bool showQuest = _currentState == UIState.None;
            _questHudGroup.alpha = showQuest ? 1f : 0f;
            _questHudGroup.interactable = showQuest;
            _questHudGroup.blocksRaycasts = showQuest;
        }

        // 플레이어 HUD — 다른 UI가 열리면 숨김
        if (playerHud != null)
            playerHud.SetActive(_currentState == UIState.None);

        // 커서 + 입력 플래그
        // Build / Quest 모드는 커서가 필요하지만 게임 시간은 유지
        bool gameplay = _currentState == UIState.None || _currentState == UIState.Build;
        SetGameplayInputEnabled(gameplay);

        // 설정창이 열릴 때만 시간 정지
        Time.timeScale = (_currentState == UIState.Settings) ? 0f : 1f;
    }

    // ── 커서 / 입력 관리 ─────────────────────────────────────────────

    private void SetGameplayInputEnabled(bool enabled)
    {
        GameplayInputEnabled = enabled;
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void RefreshCursorState()
    {
        // 탑뷰(건설) 모드는 항상 커서 표시 — TopViewPanCamera 우선
        if (_buildManager != null && _buildManager.IsTopViewMode)
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            return;
        }

        bool showCursor = _currentState != UIState.None;
        if (Cursor.visible != showCursor)
            Cursor.visible = showCursor;

        CursorLockMode target = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        if (Cursor.lockState != target)
            Cursor.lockState = target;
    }
}
