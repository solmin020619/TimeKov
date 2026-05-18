// =====================================================================
// UIStateManager.cs
// UI 상태 관리 인벤토리, 상점, 루팅, 퀘스트,일시정지, 공장, 건축 모드 
// =====================================================================

using UnityEngine;

public class UIStateManager : MonoBehaviour
{
    public static UIStateManager Instance;

    public static bool GameplayInputEnabled { get; private set; } = true;

    public enum UIState
    {
        None,
        Inventory,
        Loot,
        Quest,
        Pause,
        Factory,
        Build
    }

    [Header("UI Panels")]
    public GameObject playerInventoryUI;
    public GameObject warehouseUI;

    [Header("Pause Root & Panels")]
    public GameObject pauseRoot;
    public GameObject pauseMainPanel;
    public GameObject pauseSettingsPanel;

    [Header("Loot UI")]
    public GameObject defaultLootUI;

    [Header("Rules")]
    public bool enableWarehouseInInventory = true;

    [Header("Inventory Buttons (Optional)")]
    public GameObject moveToWarehouseButton;

    [Header("Quest UI")]
    public GameObject questUI;

    [Header("Dim Blocker")]
    public GameObject dimBlocker;

    [Header("Build UI")]
    public GameObject quickSlotUI;
    public GameObject[] hideOnBuildMode;

    private UIState currentState = UIState.None;
    private GameObject currentLootUI = null;
    private bool pauseSettingsOpen = false;
    private int _lastLootToggleFrame = -1;
    private BuildManager buildManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        buildManager = FindAnyObjectByType<BuildManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscape();
            return;
        }

        if (currentState != UIState.None && !IsAnyManagedUIPanelActive())
        {
            currentState = UIState.None;
            pauseSettingsOpen = false;
            ApplyState();
        }

        RefreshCursorState();
    }

    public UIState GetCurrentState() => currentState;
    public bool IsUIBlocking() => currentState != UIState.None;

    public void HandleEscape()
    {
        if (buildManager == null)
            buildManager = FindAnyObjectByType<BuildManager>();

        // 건축 모드 ESC 는 BuildManager 가 단독 처리
        if (buildManager != null && buildManager.IsBuildMode)
            return;

        if (currentState == UIState.None)
        {
            currentState = UIState.Pause;
            pauseSettingsOpen = false;
            ApplyState();
            return;
        }

        if (currentState == UIState.Pause && pauseSettingsOpen)
        {
            ClosePauseSettings();
            return;
        }

        currentState = UIState.None;
        pauseSettingsOpen = false;
        ApplyState();
    }

    public void CloseAllUI()
    {
        currentState = UIState.None;
        pauseSettingsOpen = false;
        ApplyState();
    }

    // ── 공장 설비 UI ──────────────────────────────────────────────
    public void OpenFactoryUI()
    {
        currentState = UIState.Factory;
        ApplyState();
    }

    public void CloseFactoryUI()
    {
        currentState = UIState.None;
        ApplyState();
    }

    public void ToggleInventory()
    {
        if (currentState == UIState.Loot) return;
        if (currentState == UIState.Pause) return;

        currentState = (currentState == UIState.Inventory) ? UIState.None : UIState.Inventory;
        ApplyState();
    }


    public void ToggleLoot(GameObject lootUI)
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Pause) return;

        // 같은 프레임 중복 호출 방지
        if (Time.frameCount == _lastLootToggleFrame) return;
        _lastLootToggleFrame = Time.frameCount;

        if (lootUI != null) currentLootUI = lootUI;
        else if (currentLootUI == null) currentLootUI = defaultLootUI;

        currentState = (currentState == UIState.Loot) ? UIState.None : UIState.Loot;
        ApplyState();
    }

    public void ToggleQuest()
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Loot) return;
        if (currentState == UIState.Pause) return;

        currentState = (currentState == UIState.Quest) ? UIState.None : UIState.Quest;
        ApplyState();
    }

    // Loot UI 루트만 교체 (상태 토글 없이)
    public void SetCurrentLootUI(GameObject lootUI)
    {
        if (lootUI != null) currentLootUI = lootUI;
        else if (currentLootUI == null) currentLootUI = defaultLootUI;

        if (currentState == UIState.Loot)
            ApplyState();
    }

    public void SetState(UIState newState)
    {
        currentState = newState;
        if (currentState != UIState.Pause) pauseSettingsOpen = false;
        ApplyState();
    }

    public void OpenPauseSettings()
    {
        if (currentState != UIState.Pause)
            currentState = UIState.Pause;

        pauseSettingsOpen = true;
        ApplyState();
    }

    public void ClosePauseSettings()
    {
        pauseSettingsOpen = false;

        if (currentState != UIState.Pause)
            currentState = UIState.Pause;

        ApplyState();
    }

    void ApplyState()
    {
        // 전부 끄기
        if (playerInventoryUI) playerInventoryUI.SetActive(false);
        if (warehouseUI) warehouseUI.SetActive(false);
        if (questUI) questUI.SetActive(false);
        if (pauseRoot) pauseRoot.SetActive(false);
        if (pauseMainPanel) pauseMainPanel.SetActive(false);
        if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false);
        if (defaultLootUI) defaultLootUI.SetActive(false);
        if (currentLootUI && currentLootUI != defaultLootUI) currentLootUI.SetActive(false);
        if (quickSlotUI) quickSlotUI.SetActive(false);


        if (dimBlocker != null)
            dimBlocker.SetActive(currentState != UIState.None);

        // 상태별 켜기
        switch (currentState)
        {
            case UIState.Inventory:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(enableWarehouseInInventory);
                break;

                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);
                break;

            case UIState.Quest:
                if (questUI) questUI.SetActive(true);
                break;

            case UIState.Loot:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);
                if (currentLootUI) currentLootUI.SetActive(true);
                else if (defaultLootUI) defaultLootUI.SetActive(true);
                break;

            case UIState.Pause:
                if (pauseRoot) pauseRoot.SetActive(true);
                if (pauseSettingsOpen)
                {
                    if (pauseSettingsPanel) pauseSettingsPanel.SetActive(true);
                    if (pauseMainPanel) pauseMainPanel.SetActive(false);
                }
                else
                {
                    if (pauseMainPanel) pauseMainPanel.SetActive(true);
                    if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false);
                }
                break;

            case UIState.Factory:
                // MachineUI 는 MachineInteraction 이 직접 관리
                break;

            case UIState.Build:
                // 빌드 모드 진입 시 퀵슬롯 UI 표시
                if (quickSlotUI) quickSlotUI.SetActive(true);
                break;

            case UIState.None:
            default:
                break;
        }

        // 창고 이동 버튼 노출 규칙
        if (moveToWarehouseButton != null)
        {
            bool show = (currentState == UIState.Inventory) && enableWarehouseInInventory;
            moveToWarehouseButton.SetActive(show);
        }

        // 커서 + 입력 플래그
        SetGameplayInputEnabled(currentState == UIState.None || currentState == UIState.Build);

        // 빌드 모드 시 숨길 HUD (PlayerStatusUI 등)
        bool isBuild = currentState == UIState.Build;
        if (hideOnBuildMode != null)
        {
            for (int i = 0; i < hideOnBuildMode.Length; i++)
                if (hideOnBuildMode[i] != null)
                    hideOnBuildMode[i].SetActive(!isBuild);
        }

        SetPauseTimeScale(currentState == UIState.Pause);
    }

    private bool IsAnyManagedUIPanelActive()
    {
        if (playerInventoryUI != null && playerInventoryUI.activeInHierarchy) return true;
        if (warehouseUI != null && warehouseUI.activeInHierarchy) return true;
        if (questUI != null && questUI.activeInHierarchy) return true;
        if (pauseRoot != null && pauseRoot.activeInHierarchy) return true;
        if (pauseMainPanel != null && pauseMainPanel.activeInHierarchy) return true;
        if (pauseSettingsPanel != null && pauseSettingsPanel.activeInHierarchy) return true;
        if (currentLootUI != null && currentLootUI.activeInHierarchy) return true;
        if (defaultLootUI != null && defaultLootUI.activeInHierarchy) return true;
        if (currentState == UIState.Factory) return true;
        if (quickSlotUI != null && quickSlotUI.activeInHierarchy) return true;
        return false;
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        GameplayInputEnabled = enabled;
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void RefreshCursorState()
    {
        if (buildManager != null && buildManager.IsTopViewMode)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        bool shouldShowCursor = currentState != UIState.None;

        if (shouldShowCursor)
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (Cursor.visible) Cursor.visible = false;
            if (Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void SetPauseTimeScale(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
    }
}