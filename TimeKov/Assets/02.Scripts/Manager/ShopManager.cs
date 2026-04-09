using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("UI")]
    public GameObject shopPanel;
    public Transform shopGridContent;
    public Button closeButton;

    [Header("Prefabs")]
    public GameObject slotPrefab;

    [Header("Refs")]
    public InventoryManager playerInventory;

    [Header("Test Money (임시)")]
    public int playerMoney = 999999;

    [Header("Shop Options")]
    [Tooltip("tradeValue가 0 이하인 sellOnly 아이템은 상점 목록에서 제외")]
    public bool excludeZeroPriceItems = true;

    [Tooltip("정렬 기준: itemId 오름차순")]
    public bool sortByItemId = true;

    private void Awake()
    {
        Instance = this;

        if (!DataStore.IsLoaded)
            DataStore.LoadAll();

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseShop);

        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    public bool IsShopOpen()
    {
        return shopPanel != null && shopPanel.activeSelf;
    }

    public void OpenShop()
    {
        if (shopPanel == null || shopGridContent == null || slotPrefab == null)
        {
            Debug.LogWarning("ShopManager 연결 부족 (shopPanel / shopGridContent / slotPrefab 확인)");
            return;
        }

        if (!DataStore.IsLoaded)
            DataStore.LoadAll();

        shopPanel.SetActive(true);
        shopGridContent.gameObject.SetActive(true);

        BuildShopSlots();
    }

    public void CloseShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    private void BuildShopSlots()
    {
        for (int i = shopGridContent.childCount - 1; i >= 0; i--)
        {
            Destroy(shopGridContent.GetChild(i).gameObject);
        }

        List<FactoryItemRow> shopItems = GetSellOnlyItems();

        for (int i = 0; i < shopItems.Count; i++)
        {
            FactoryItemRow factoryRow = shopItems[i];
            if (factoryRow == null) continue;

            ItemRow itemRow = DataStore.GetItem(factoryRow.itemId);
            if (itemRow == null) continue;

            GameObject slotObj = Instantiate(slotPrefab, shopGridContent);

            ShopSlotMarker marker = slotObj.GetComponent<ShopSlotMarker>();
            if (marker == null)
                marker = slotObj.AddComponent<ShopSlotMarker>();

            marker.itemId = factoryRow.itemId;
            marker.buyPrice = Mathf.Max(0, factoryRow.tradeValue);
            marker.stock = -1;

            SlotInfo slotInfo = slotObj.GetComponent<SlotInfo>();
            if (slotInfo != null)
            {
                slotInfo.ownerType = SlotInfo.SlotOwnerType.Shop;
                slotInfo.SetSlot(factoryRow.itemId, 1);
            }

            SlotInputHandler input = slotObj.GetComponent<SlotInputHandler>();
            if (input != null)
            {
                input.ownerManager = playerInventory;
                input.invenManager = playerInventory;
                input.allowDoubleClick = false;
            }

            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
            }

            slotObj.name = $"ShopSlot_{factoryRow.itemId}";
        }
    }

    private List<FactoryItemRow> GetSellOnlyItems()
    {
        IEnumerable<FactoryItemRow> query = DataStore.FactoryItemById.Values
            .Where(x => x != null && x.factoryUsage == "sellOnly");

        if (excludeZeroPriceItems)
            query = query.Where(x => x.tradeValue > 0);

        if (sortByItemId)
            query = query.OrderBy(x => x.itemId);

        return query.ToList();
    }

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

        if (invSlot.ownerType != SlotInfo.SlotOwnerType.Inventory)
            return;

        int id = invSlot.slotIndex;
        int count = invSlot.itemCount;
        if (id == 0 || count <= 0) return;

        ItemRow item = DataStore.GetItem(id);
        if (item == null)
        {
            Debug.LogWarning($"판매 실패: ItemRow 없음 ({id})");
            return;
        }

        int sellPrice = Mathf.Max(0, item.sellValue);

        playerMoney += sellPrice;

        int newCount = count - 1;
        if (newCount <= 0)
            invSlot.SetSlot(0, 0);
        else
            invSlot.SetSlot(id, newCount);

        if (playerInventory != null)
            playerInventory.ForceRefreshUI();

        Debug.Log($"[판매] {id} / +{sellPrice} / 남은 돈: {playerMoney}");
    }
}