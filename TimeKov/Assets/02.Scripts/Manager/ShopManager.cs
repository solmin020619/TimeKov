using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("UI")]
    public GameObject shopPanel;
    public Transform shopGridContent; // GridLayoutGroup 붙은 오브젝트
    public Button closeButton;

    [Header("Data")]
    public ShopCatalogSO catalog;

    [Header("Prefabs")]
    public GameObject slotPrefab; // 너가 쓰는 Slot 프리팹 그대로 재사용

    [Header("Refs")]
    public InventoryManager playerInventory; // 구매하면 여기로 들어감

    [Header("Test Money (임시)")]
    public int playerMoney = 999999; // 일단 테스트용. 나중에 너 돈 시스템으로 교체

    void Awake()
    {
        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseShop);

        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    void Update()
    {

    }

    public bool IsShopOpen()
    {
        return shopPanel != null && shopPanel.activeSelf;
    }

    public void OpenShop()
    {
        if (shopPanel == null || shopGridContent == null || catalog == null || slotPrefab == null)
        {
            Debug.LogWarning("ShopManager 연결이 부족함 (shopPanel/shopGridContent/catalog/slotPrefab 확인)");
            return;
        }

        shopPanel.SetActive(true);
        // ShopGrid(컨텐츠)가 비활성화된 상태면 슬롯을 만들어도 화면에 안 보임 → 상점 열 때 강제로 켜줌
        shopGridContent.gameObject.SetActive(true);

        BuildShopSlots();
    }

    public void CloseShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    void BuildShopSlots()
    {
        for (int i = shopGridContent.childCount - 1; i >= 0; i--)
        {
            Destroy(shopGridContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < catalog.entries.Count; i++)
        {
            ShopEntry entry = catalog.entries[i];
            if (entry == null) continue;

            GameObject slotObj = Instantiate(slotPrefab, shopGridContent);

            // 1) SlotInfo로 아이콘 표시
            SlotInfo slotInfo = slotObj.GetComponent<SlotInfo>();
            if (slotInfo != null)
            {
                slotInfo.SetSlot(entry.itemId, 1);
            }

            // 2) 상점 슬롯 표식(구매 가격/재고)
            ShopSlotMarker marker = slotObj.GetComponent<ShopSlotMarker>();
            if (marker == null)
                marker = slotObj.AddComponent<ShopSlotMarker>();

            marker.itemId = entry.itemId;
            marker.buyPrice = entry.buyPrice;
            marker.stock = entry.stock;

            // 3) 상점 슬롯에서는 더블클릭 이동은 막음(구매는 우클릭 메뉴로)
            DoubleClickEquip dc = slotObj.GetComponent<DoubleClickEquip>();
            if (dc != null) dc.enabled = false;

            // ✅ 4) 우클릭 메뉴는 "켜야" 구매가 가능함
            SlotRightClick rc = slotObj.GetComponent<SlotRightClick>();
            if (rc != null)
            {
                rc.enabled = true;

                // 메뉴 로직이 ownerManager를 요구하니까, 플레이어 인벤을 넣어준다
                // (shop 슬롯은 InventoryManager 소속이 아니기 때문에 주입이 필요)
                rc.ownerManager = playerInventory;
            }

            // ✅ 5) 테스트용 '클릭 구매'는 이제 필요 없음 (우클릭 구매로 갈 거니까)
            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
            }

            slotObj.name = $"ShopSlot_{entry.itemId}";
        }
    }

    // =========================
    // 우클릭 메뉴에서 호출할 함수들
    // =========================

    public void TryBuyFromContext(ShopSlotMarker marker)
    {
        if (marker == null) return;
        if (!IsShopOpen())
        {
            Debug.Log("상점이 열려있을 때만 구매 가능");
            return;
        }

        if (marker.stock == 0)
        {
            Debug.Log("재고 없음");
            return;
        }

        if (playerMoney < marker.buyPrice)
        {
            Debug.Log("돈 부족");
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogWarning("playerInventory 연결 안됨");
            return;
        }

        playerMoney -= marker.buyPrice;
        playerInventory.AddItem(marker.itemId, 1);

        if (marker.stock > 0)
            marker.stock--;

        Debug.Log($"[구매] {marker.itemId} / -{marker.buyPrice} / 남은 돈: {playerMoney} / 남은 재고: {marker.stock}");
    }

    public void TrySellFromContext(SlotInfo invSlot)
    {
        if (invSlot == null) return;
        if (!IsShopOpen())
        {
            Debug.Log("상점이 열려있을 때만 판매 가능");
            return;
        }

        // 판매는 플레이어 인벤 슬롯에서만(창고/상점 슬롯은 제외)
        if (invSlot.ownerType != SlotInfo.SlotOwnerType.Inventory)
            return;

        int id = invSlot.slotIndex;
        int count = invSlot.itemCount;
        if (id == 0 || count <= 0) return;

        // 판매가(임시): ItemDataBase의 SaleTime 그대로 사용 (너 DB 구조 기준)
        int sellPrice = 0;
        if (DataManager.Instance != null)
        {
            var item = DataManager.Instance.GetItem(id);
            if (item != null)
                sellPrice = item.saleTime;
        }

        if (sellPrice < 0) sellPrice = 0;

        // 1개 판매(원하면 전량 판매로 바꿀 수 있음)
        playerMoney += sellPrice;

        int newCount = count - 1;
        if (newCount <= 0)
            invSlot.SetSlot(0, 0);
        else
            invSlot.SetSlot(id, newCount);

        // ✅✅✅ [추가] 판매 후 인벤 UI 갱신(정렬/빈칸숨김/인벤 5/10 텍스트 갱신)
        if (playerInventory != null)
        {
            playerInventory.ForceRefreshUI();
        }

        Debug.Log($"[판매] {id} / +{sellPrice} / 남은 돈: {playerMoney}");
    }
}