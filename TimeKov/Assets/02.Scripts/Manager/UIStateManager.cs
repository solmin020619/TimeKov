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
        Pause
    }

    [Header("UI Panels")]
    public GameObject playerInventoryUI;
    public GameObject warehouseUI;
    public GameObject shopUI;

    [Header("Pause Root & Panels")]
    public GameObject pauseRoot;          // ✅ PauseSystem (부모)
    public GameObject pauseMainPanel;     // ✅ Pause_V1Blue (실제 Pause 패널)
    public GameObject pauseSettingsPanel; // ✅ SettingsSystem (설정 패널 루트)

    [Header("Loot UI")]
    public GameObject defaultLootUI;

    [Header("Rules")]
    public bool enableWarehouseInInventory = true;

    [Header("Inventory Buttons (Optional)")]
    public GameObject moveToWarehouseButton;

    [Header("Managers (optional but recommended)")]
    public ShopManager shopManager;

    [Header("Dim Blocker (Optional)")]
    public DimBlockerManager dimBlockerManager;

    private UIState currentState = UIState.None;

    private GameObject currentLootUI = null;

    // ✅ Pause 안에서 Settings가 열려있는지 플래그
    private bool pauseSettingsOpen = false;

    // ✅ 추가: 같은 프레임에 Loot 토글이 두 번 들어오면 "켜졌다가 바로 꺼짐" 방지
    private int _lastLootToggleFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dimBlockerManager == null)
            dimBlockerManager = FindAnyObjectByType<DimBlockerManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscape();
        }

        // ✅ 외부에서 패널을 직접 껐다/켰어도 상태가 꼬이지 않게 싱크
        if (currentState != UIState.None && !IsAnyManagedUIPanelActive())
        {
            currentState = UIState.None;
            pauseSettingsOpen = false;
            ApplyState();
        }
    }

    public UIState GetCurrentState() => currentState;
    public bool IsUIBlocking() => currentState != UIState.None;

    // ✅ ESC 규칙
    // 1) 아무 UI도 없으면 -> Pause 켜기
    // 2) Pause 상태에서 Settings가 열려있으면 -> Settings 먼저 닫기
    // 3) 그 외 UI가 열려있으면 -> 닫기(None)
    public void HandleEscape()
    {
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

        // ✅ 추가: 같은 프레임에 두 번 호출되면 2번째는 무시 (켜졌다가 바로 꺼짐 방지)
        if (Time.frameCount == _lastLootToggleFrame) return;
        _lastLootToggleFrame = Time.frameCount;

        if (lootUI != null) currentLootUI = lootUI;
        else if (currentLootUI == null) currentLootUI = defaultLootUI;

        currentState = (currentState == UIState.Loot) ? UIState.None : UIState.Loot;
        ApplyState();
    }

    public void SetState(UIState newState)
    {
        currentState = newState;

        // Pause가 아니면 Settings 플래그 해제
        if (currentState != UIState.Pause) pauseSettingsOpen = false;

        ApplyState();
    }

    // ✅ Pause의 Settings 열기 (버튼에서 이거 호출)
    public void OpenPauseSettings()
    {
        // Pause가 아니면 Pause로 강제 진입
        if (currentState != UIState.Pause)
            currentState = UIState.Pause;

        pauseSettingsOpen = true;
        ApplyState();
    }

    // ✅ Pause의 Settings 닫기 (ESC에서 우선 닫기)
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

        // Pause 쪽은 Root/Panel/Settings 따로 관리
        if (pauseRoot) pauseRoot.SetActive(false);
        if (pauseMainPanel) pauseMainPanel.SetActive(false);
        if (pauseSettingsPanel) pauseSettingsPanel.SetActive(false);

        if (defaultLootUI) defaultLootUI.SetActive(false);
        if (currentLootUI && currentLootUI != defaultLootUI) currentLootUI.SetActive(false);

        // ShopManager 정리
        if (shopManager != null && currentState != UIState.Shop)
            shopManager.CloseShop();

        // 2) 상태별로 켬
        switch (currentState)
        {
            case UIState.Inventory:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(enableWarehouseInInventory);
                break;

            case UIState.Shop:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);

                if (shopManager != null) shopManager.OpenShop();
                else if (shopUI) shopUI.SetActive(true);
                break;

            case UIState.Loot:
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);
                if (shopUI) shopUI.SetActive(false);

                if (currentLootUI) currentLootUI.SetActive(true);
                else if (defaultLootUI) defaultLootUI.SetActive(true);
                break;

            case UIState.Pause:
                // ✅ Root는 항상 켬
                if (pauseRoot) pauseRoot.SetActive(true);

                // ✅ Settings 열려있으면 Settings만 / 아니면 메인 Pause 패널
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

        // 커서 + 입력 플래그 처리
        SetGameplayInputEnabled(currentState == UIState.None);

        // 딤블로커
        if (dimBlockerManager == null)
            dimBlockerManager = FindAnyObjectByType<DimBlockerManager>();

        if (dimBlockerManager != null)
            dimBlockerManager.SetDim(currentState != UIState.None);
    }

    // ✅ “관리 대상 UI가 실제로 하나라도 켜져있는가” 체크
    private bool IsAnyManagedUIPanelActive()
    {
        if (playerInventoryUI != null && playerInventoryUI.activeInHierarchy) return true;
        if (warehouseUI != null && warehouseUI.activeInHierarchy) return true;
        if (shopUI != null && shopUI.activeInHierarchy) return true;

        if (pauseRoot != null && pauseRoot.activeInHierarchy) return true;
        if (pauseMainPanel != null && pauseMainPanel.activeInHierarchy) return true;
        if (pauseSettingsPanel != null && pauseSettingsPanel.activeInHierarchy) return true;

        if (currentLootUI != null && currentLootUI.activeInHierarchy) return true;
        if (defaultLootUI != null && defaultLootUI.activeInHierarchy) return true;

        return false;
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        GameplayInputEnabled = enabled;

        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }
}