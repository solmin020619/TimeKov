using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotInfo : MonoBehaviour
{
    public int slotIndex;
    public int slotOldIndex;
    public int itemCount;

    public Image iconImage;
    public TextMeshProUGUI slotText;
    public TextMeshProUGUI amountText; // 인벤: 수량 / 상점: 가격 재활용 가능

    public enum SlotOwnerType { Inventory, Equip, Warehouse, Loot, Shop }
    public SlotOwnerType ownerType;

    [Header("Shop Price (Optional)")]
    public TextMeshProUGUI priceText; // 있으면 이걸 가격으로, 없으면(상점 슬롯일 때만) amountText 사용

    private GameObject priceRoot;
    private ShopSlotMarker shopMarker;
    private string defaultSlotText = "";

    private void Awake()
    {
        shopMarker = GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null && shopMarker.itemId != 0);

        if (isShopSlot && ownerType == SlotOwnerType.Inventory)
            ownerType = SlotOwnerType.Shop;

        if (slotText != null)
            defaultSlotText = slotText.text;

        if (priceText != null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        if (priceRoot == this.gameObject)
            priceRoot = null;

        // ✅ 인벤 수량 텍스트는 절대 Awake에서 끄지 않음
        if (!isShopSlot)
        {
            if (priceRoot != null) priceRoot.SetActive(false);
            if (priceText != null) priceText.gameObject.SetActive(false);

            if (amountText != null && !amountText.gameObject.activeSelf)
                amountText.gameObject.SetActive(true);
        }
        else
        {
            // ✅ 상점 슬롯인데 priceText가 비어있으면 amountText를 가격 표시로 재활용
            if (priceText == null) priceText = amountText;
        }
    }

    private void OnEnable() => RefreshShopPriceUI();

    private IEnumerator Start()
    {
        yield return null;
        RefreshShopPriceUI();
    }

    public void SetSlot(int id, int count)
    {
        if (shopMarker == null) shopMarker = GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null && shopMarker.itemId != 0);

        if (isShopSlot && ownerType == SlotOwnerType.Inventory)
            ownerType = SlotOwnerType.Shop;

        int effectiveId = isShopSlot ? shopMarker.itemId : id;

        slotIndex = effectiveId;
        itemCount = count;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (slotIndex == 0 || itemCount <= 0)
        {
            if (slotText != null)
                slotText.text = (ownerType == SlotOwnerType.Equip) ? defaultSlotText : "";

            slotOldIndex = 0;

            if (!isShopSlot && iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            UpdateAmountText();
            RefreshShopPriceUI();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = Resources.Load<Sprite>("Icon/" + slotIndex);
            iconImage.enabled = (iconImage.sprite != null);
        }

        if (slotText != null)
        {
            var item = DataManager.Instance?.GetItem(slotIndex);
            slotText.text = (item != null) ? item.itemName : slotIndex.ToString();
            slotOldIndex = slotIndex;
        }

        UpdateAmountText();
        RefreshShopPriceUI();
    }

    void UpdateAmountText()
    {
        if (amountText == null) return;

        if (shopMarker == null) shopMarker = GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null && shopMarker.itemId != 0);

        // ✅ 상점 슬롯은 amountText를 가격으로 쓰므로 수량 로직이 덮어쓰면 안 됨
        if (isShopSlot) return;

        if (!amountText.gameObject.activeSelf)
            amountText.gameObject.SetActive(true);

        amountText.text = (slotIndex == 0 || itemCount <= 1) ? "" : itemCount.ToString();
    }

    private void RefreshShopPriceUI()
    {
        if (shopMarker == null) shopMarker = GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null && shopMarker.itemId != 0);
        if (!isShopSlot) return;

        if (priceText == null) priceText = amountText;
        if (priceText == null) return;

        if (priceRoot == null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        if (priceRoot == this.gameObject)
            priceRoot = null;

        if (priceRoot != null && !priceRoot.activeSelf) priceRoot.SetActive(true);
        if (!priceText.gameObject.activeSelf) priceText.gameObject.SetActive(true);

        int targetId = shopMarker.itemId;

        int price = 0;
        var item = DataManager.Instance?.GetItem(targetId);
        if (item != null) price = item.saleTime;
        if (price <= 0) price = shopMarker.buyPrice;

        priceText.text = $"{price}s";
    }

    void Update()
    {
        if (slotIndex == 0) return;

        if (slotIndex != slotOldIndex)
        {
            if (slotText == null) { slotOldIndex = slotIndex; return; }
            if (DataManager.Instance == null) { slotText.text = slotIndex.ToString(); slotOldIndex = slotIndex; return; }

            var item = DataManager.Instance.GetItem(slotIndex);
            if (item == null) { slotText.text = slotIndex.ToString(); slotOldIndex = slotIndex; return; }

            slotText.text = item.itemName;
            slotOldIndex = slotIndex;
        }
    }

    public void SlotClick()
    {
        if (slotIndex == 0) { Debug.Log("노아이템"); return; }

        if (DataManager.Instance == null) return;
        var item = DataManager.Instance.GetItem(slotIndex);
        if (item == null) return;

        Debug.Log("아이템 이름 :" + item.itemName);
        Debug.Log("아이템 설명 :" + item.description);
        Debug.Log("아이콘 이미지 파일 이름 :" + item.iconImange);
    }
}