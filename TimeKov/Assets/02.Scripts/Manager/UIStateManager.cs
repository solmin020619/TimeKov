// UIStateManager.cs (원본 유지 + Loot 상태 추가 + 레이드에서 인벤만 열리게 옵션)
using UnityEngine;

public class UIStateManager : MonoBehaviour
{
    public static UIStateManager Instance;

    public enum UIState
    {
        None,
        Inventory,   // I키: 인벤(옵션에 따라 창고 포함/미포함)
        Shop,        // F키: 상점
        Loot         // F키: 파밍 상자(루팅패널 + 인벤 같이)
    }

    [Header("UI Panels")]
    public GameObject playerInventoryUI;
    public GameObject warehouseUI;
    public GameObject shopUI;

    [Header("Loot UI (루팅패널은 상자마다 다를 수 있어서 런타임 주입도 지원)")]
    public GameObject defaultLootUI; // (선택) 공통 루팅 UI가 있으면 연결, 없으면 비워도 됨

    [Header("Rules")]
    [Tooltip("I키 Inventory 상태에서 창고 UI도 같이 켤지(기지=true / 레이드=false)")]
    public bool enableWarehouseInInventory = true;

    [Header("Managers (optional but recommended)")]
    public ShopManager shopManager;

    private UIState currentState = UIState.None;

    // 현재 열려있는 루팅 UI(상자마다 다르면 여기로 주입)
    private GameObject currentLootUI = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public UIState GetCurrentState()
    {
        return currentState;
    }

    public bool IsUIBlocking()
    {
        return currentState != UIState.None;
    }

    // =========================
    // ✅ I키 전용 토글
    // - Shop/Loot 상태에서 I 눌러도 아무 일 안 함
    // - None <-> Inventory 만 토글
    // =========================
    public void ToggleInventory()
    {
        if (currentState == UIState.Shop) return;
        if (currentState == UIState.Loot) return;

        currentState = (currentState == UIState.Inventory) ? UIState.None : UIState.Inventory;
        ApplyState();
    }

    // =========================
    // ✅ F키(상점 상호작용) 전용 토글
    // - Inventory/Loot 상태에서 F 눌러도 아무 일 안 함
    // - None <-> Shop 만 토글
    // =========================
    public void ToggleShop()
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Loot) return;

        currentState = (currentState == UIState.Shop) ? UIState.None : UIState.Shop;
        ApplyState();
    }

    // =========================
    // ✅ F키(파밍 상자) 전용 토글
    // - Inventory/Shop 상태에서 Loot로 못 넘어가게 막음(원하면 풀어도 됨)
    // - None <-> Loot 만 토글
    // =========================
    public void ToggleLoot(GameObject lootUI)
    {
        if (currentState == UIState.Inventory) return;
        if (currentState == UIState.Shop) return;

        // LootUI 주입
        if (lootUI != null) currentLootUI = lootUI;
        else if (currentLootUI == null) currentLootUI = defaultLootUI;

        currentState = (currentState == UIState.Loot) ? UIState.None : UIState.Loot;
        ApplyState();
    }

    // (혹시 기존 코드가 SetState를 직접 쓰고 있으면 깨질 수 있어서 남겨둠)
    public void SetState(UIState newState)
    {
        currentState = newState;
        ApplyState();
    }

    void ApplyState()
    {
        // 1) 우선 모두 끔
        if (playerInventoryUI) playerInventoryUI.SetActive(false);
        if (warehouseUI) warehouseUI.SetActive(false);
        if (shopUI) shopUI.SetActive(false);

        if (defaultLootUI) defaultLootUI.SetActive(false);
        if (currentLootUI && currentLootUI != defaultLootUI) currentLootUI.SetActive(false);

        // ShopManager 정리
        if (shopManager != null && currentState != UIState.Shop)
            shopManager.CloseShop();

        // 2) 상태별로 켬
        switch (currentState)
        {
            case UIState.Inventory:
                // ✅ 레이드에서는 왼쪽 인벤만 켜고 싶다 -> enableWarehouseInInventory = false
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(enableWarehouseInInventory);
                break;

            case UIState.Shop:
                // ✅ 상점에서는 인벤 같이 보이게(기존 유지)
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);

                if (shopManager != null) shopManager.OpenShop();
                else if (shopUI) shopUI.SetActive(true);
                break;

            case UIState.Loot:
                // ✅ 덕코프처럼: 루팅패널 + 인벤 같이 ON, 창고/상점 OFF
                if (playerInventoryUI) playerInventoryUI.SetActive(true);
                if (warehouseUI) warehouseUI.SetActive(false);
                if (shopUI) shopUI.SetActive(false);

                if (currentLootUI) currentLootUI.SetActive(true);
                else if (defaultLootUI) defaultLootUI.SetActive(true);
                break;

            case UIState.None:
            default:
                break;
        }

        // 커서 처리
        SetGameplayInputEnabled(currentState == UIState.None);
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        Cursor.visible = !enabled;
        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
