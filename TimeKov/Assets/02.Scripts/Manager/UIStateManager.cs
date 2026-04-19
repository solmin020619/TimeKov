using UnityEngine;

public class UIStateManager : MonoBehaviour
{
    public static UIStateManager Instance;

    public static bool GameplayInputEnabled { get; private set; } = true;

    public enum UIState
    {
        None,
        Inventory,
        Shop,
        Loot,
        Quest,
        Pause,
        Factory,  // 공장 설비 UI
        Build     // 건축 모드 (QuickSlot UI)
    }

    [Header("UI Panels")]
    public GameObject playerInventoryUI;
    public GameObject warehouseUI;
    public GameObject shopUI;

    [Header("Pause Root & Panels")]
    public GameObject pauseRoot;          //  PauseSystem (부모)
    public GameObject pauseMainPanel;     //  Pause_V1Blue (실제 Pause 패널)
    public GameObject pauseSettingsPanel; //  SettingsSystem (설정 패널 루트)

    [Header("Loot UI")]
    public GameObject defaultLootUI;

    [Header("Rules")]
    public bool enableWarehouseInInventory = true;

    [Header("Inventory Buttons (Optional)")]
    public GameObject moveToWarehouseButton;

    [Header("Quest UI")]
    public GameObject questUI;

    [Header("Managers (optional but recommended)")]
    public ShopManager shopManager;

    [Header("Dim Blocker")]
    public GameObject dimBlocker;

    [Header("Build UI")]
    public GameObject quickSlotUI;
    public GameObject[] hideOnBuildMode; // 빌드 모드 진입 시 숨길 UI (PlayerStatusUI 등)

    [Header("Base Scene Settings")]
    public string[] baseSceneNames = { "Base" };



    private UIState currentState = UIState.None;

    private GameObject currentLootUI = null;

    //  Pause 안에서 Settings가 열려있는지 플래그
    private bool pauseSettingsOpen = false;

    //  추가: 같은 프레임에 Loot 토글이 두 번 들어오면 "켜졌다가 바로 꺼짐" 방지
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
        buildManager = FindObjectOfType<BuildManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscape();
            return; //  이거 추가
        }

        if (currentState != UIState.None && !IsAnyManagedUIPanelActive())
        {
            currentState = UIState.None;
            pauseSettingsOpen = false;
            ApplyState();
        }

        //  추가: 다른 스크립트가 커서를 다시 숨기거나 잠궈도
        // UI가 열려있는 동안에는 계속 UI 조작 가능하게 커서 상태를 강제 유지
        RefreshCursorState();
    }

    public UIState GetCurrentState() => currentState;
    public bool IsUIBlocking() => currentState != UIState.None;

    //  ESC 규칙
    // 1) 아무 UI도 없으면 -> Pause 켜기
    // 2) Pause 상태에서 Settings가 열려있으면 -> Settings 먼저 닫기
    // 3) 그 외 UI가 열려있으면 -> 닫기(None)
    public void HandleEscape()
    {
        if (buildManager == null)
            buildManager = FindObjectOfType<BuildManager>();

        // 건축 모드 ESC는 BuildManager가 단독 처리
        if (buildManager != null && buildManager.IsBuildMode)
            return;

        ItemTooltipUI.Instance?.Hide();
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

    // ── 공장 설비 UI 전용 ────────────────────────────────────────
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
        if (currentState == UIState.Shop) return;
        if (currentState == UIState.Loot) return;
        if (currentState == UIState.Pause) return;

        currentState = (currentState == UIState.Inventory) ? UIState.None : UIState.Inventory;
        ApplyState();
    }

    public void ToggleShop()
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Loot) return;
        if (currentState == UIState.Pause) return;

        currentState = (currentState == UIState.Shop) ? UIState.None : UIState.Shop;
        ApplyState();
    }

    public void ToggleLoot(GameObject lootUI)
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Shop) return;
        if (currentState == UIState.Pause) return;

        //  추가: 같은 프레임에 두 번 호출되면 2번째는 무시 (켜졌다가 바로 꺼짐 방지)
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
        if (currentState == UIState.Shop) return;
        if (currentState == UIState.Loot) return;
        if (currentState == UIState.Pause) return;

        currentState = (currentState == UIState.Quest) ? UIState.None : UIState.Quest;
        ApplyState();
    }

    //  [추가] Loot UI가 이미 열린 상태에서, 다른 LootContainer가 다른 루트로 바인딩해야 할 때 사용
    // ToggleLoot처럼 상태를 토글하지 않고, 현재 Loot UI 루트만 교체합니다.
    public void SetCurrentLootUI(GameObject lootUI)
    {
        if (lootUI != null) currentLootUI = lootUI;
        else if (currentLootUI == null) currentLootUI = defaultLootUI;

        // Loot 상태라면 즉시 반영
        if (currentState == UIState.Loot)
            ApplyState();
    }

    public void SetState(UIState newState)
    {
        currentState = newState;

        // Pause가 아니면 Settings 플래그 해제
        if (currentState != UIState.Pause) pauseSettingsOpen = false;

        ApplyState();
    }

    //  Pause의 Settings 열기 (버튼에서 이거 호출)
    public void OpenPauseSettings()
    {
        // Pause가 아니면 Pause로 강제 진입
        if (currentState != UIState.Pause)
            currentState = UIState.Pause;

        pauseSettingsOpen = true;
        ApplyState();
    }

    //  Pause의 Settings 닫기 (ESC에서 우선 닫기)
    public void ClosePauseSettings()
    {
        pauseSettingsOpen = false;

        // Pause 상태는 유지하고 메인 패널로 복귀
        if (currentState != UIState.Pause)
            currentState = UIState.Pause;

        ApplyState();
    }

    void ApplyState()
    {
        // 1) 우선 모두 끔
        if (playerInventoryUI) playerInventoryUI.SetActive(false);
        if (warehouseUI) warehouseUI.SetActive(false);
        if (shopUI) shopUI.SetActive(false);
        if (questUI) questUI.SetActive(false);

        // Pause 쪽은 Root/Panel/Settings 따로 관리
        if (pauseRoot) pauseRoot.SetActive(false);
        if (pauseMainPanel) pauseMainPanel.SetActive(false);
        if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false);

        if (defaultLootUI) defaultLootUI.SetActive(false);
        if (currentLootUI && currentLootUI != defaultLootUI) currentLootUI.SetActive(false);
        if (quickSlotUI) quickSlotUI.SetActive(false);

        // ShopManager 정리
        if (shopManager != null && currentState != UIState.Shop)
            shopManager.CloseShop();

        if (dimBlocker != null)
            dimBlocker.SetActive(currentState != UIState.None);

        // 2) 상태별로 켬
        switch (currentState)
        {
            case UIState.Inventory:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                bool isBase = IsBaseScene();
                if (warehouseUI)
                    warehouseUI.SetActive(enableWarehouseInInventory && isBase);
                break;

            case UIState.Shop:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);

                if (shopManager != null) shopManager.OpenShop();
                else if (shopUI) shopUI.SetActive(true);
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
                //  Root는 항상 켬
                if (pauseRoot) pauseRoot.SetActive(true);

                //  Settings 열려있으면 Settings만 / 아니면 메인 Pause 패널
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
                // MachineUI는 MachineInteraction이 직접 관리
                // 여기서는 커서/입력 제어만 담당
                break;

            case UIState.Build:
                if (quickSlotUI) quickSlotUI.SetActive(true);
                break;

            case UIState.None:
            default:
                break;




        }

        // 창고 이동 버튼 노출 규칙
        if (moveToWarehouseButton != null)
        {
            bool show = (currentState == UIState.Inventory) && enableWarehouseInInventory && IsBaseScene();
            moveToWarehouseButton.SetActive(show);
        }

        // 커서 + 입력 플래그 처리
        SetGameplayInputEnabled(currentState == UIState.None || currentState == UIState.Build);

        // 빌드 모드일 때 숨길 UI 처리 (PlayerStatusUI 등 상시 표시 HUD)
        bool isBuild = currentState == UIState.Build;
        if (hideOnBuildMode != null)
        {
            for (int i = 0; i < hideOnBuildMode.Length; i++)
                if (hideOnBuildMode[i] != null)
                    hideOnBuildMode[i].SetActive(!isBuild);
        }

        SetPauseTimeScale(currentState == UIState.Pause);


    }

    //  “관리 대상 UI가 실제로 하나라도 켜져있는가” 체크
    private bool IsAnyManagedUIPanelActive()
    {
        if (playerInventoryUI != null && playerInventoryUI.activeInHierarchy) return true;
        if (warehouseUI != null && warehouseUI.activeInHierarchy) return true;
        if (shopUI != null && shopUI.activeInHierarchy) return true;
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

        // 커서 처리
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }

    //  추가: UI 상태에 맞는 커서 상태를 매 프레임 보정
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

    //  Pause 상태일 때만 게임 일시정지
    private void SetPauseTimeScale(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
    }

    private bool IsBaseScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        for (int i = 0; i < baseSceneNames.Length; i++)
        {
            if (sceneName == baseSceneNames[i])
                return true;
        }

        return false;
    }
}