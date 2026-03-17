using System.Collections;
using System.Collections.Generic;
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

    private static readonly Dictionary<int, Sprite> _iconCache = new Dictionary<int, Sprite>();

    private void Awake()
    {
        CacheRefs();
        ApplyOwnerTypeForShopSlot();

        if (slotText != null)
            defaultSlotText = slotText.text;

        ResolvePriceRoot();

        bool isShopSlot = IsShopSlot();

        //  인벤 수량 텍스트는 절대 Awake에서 끄지 않음
        if (!isShopSlot)
        {
            if (priceRoot != null) priceRoot.SetActive(false);
            if (priceText != null) priceText.gameObject.SetActive(false);

            if (amountText != null && !amountText.gameObject.activeSelf)
                amountText.gameObject.SetActive(true);
        }
        else
        {
            //  상점 슬롯인데 priceText가 비어있으면 amountText를 가격 표시로 재활용
            if (priceText == null) priceText = amountText;
        }
    }

    private void OnEnable()
    {
        RefreshShopPriceUI();
    }

    private IEnumerator Start()
    {
        yield return null;
        RefreshShopPriceUI();
    }

    private void CacheRefs()
    {
        if (shopMarker == null)
            shopMarker = GetComponent<ShopSlotMarker>();
    }

    private void ApplyOwnerTypeForShopSlot()
    {
        if (IsShopSlot() && ownerType == SlotOwnerType.Inventory)
            ownerType = SlotOwnerType.Shop;
    }

    private bool IsShopSlot()
    {
        return shopMarker != null && shopMarker.itemId != 0;
    }

    private void ResolvePriceRoot()
    {
        if (priceText != null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        if (priceRoot == gameObject)
            priceRoot = null;
    }

    public void SetSlot(int id, int count)
    {
        CacheRefs();
        ApplyOwnerTypeForShopSlot();

        bool isShopSlot = IsShopSlot();
        int effectiveId = isShopSlot ? shopMarker.itemId : id;

        slotIndex = effectiveId;
        itemCount = count;
        slotOldIndex = slotIndex;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (slotIndex == 0 || itemCount <= 0)
        {
            ApplyEmptyState(isShopSlot);
            UpdateAmountText();
            RefreshShopPriceUI();
            return;
        }

        ApplyFilledState();
        UpdateAmountText();
        RefreshShopPriceUI();
    }

    private void ApplyEmptyState(bool isShopSlot)
    {
        if (slotText != null)
            slotText.text = (ownerType == SlotOwnerType.Equip) ? defaultSlotText : "";

        slotOldIndex = 0;

        if (!isShopSlot && iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    private void ApplyFilledState()
    {
        ApplyIcon(slotIndex);
        ApplySlotText(slotIndex);
    }

    private void ApplyIcon(int id)
    {
        if (iconImage == null)
            return;

        Sprite sprite = GetCachedIcon(id);
        iconImage.sprite = sprite;
        iconImage.enabled = (sprite != null);
    }

    private void ApplySlotText(int id)
    {
        if (slotText == null)
            return;

        var item = DataManager.Instance?.GetItem(id);
        slotText.text = (item != null) ? item.itemName : id.ToString();
    }

    private Sprite GetCachedIcon(int id)
    {
        if (id <= 0)
            return null;

        if (_iconCache.TryGetValue(id, out Sprite cached))
            return cached;

        Sprite loaded = Resources.Load<Sprite>("Icon/" + id);
        _iconCache[id] = loaded;
        return loaded;
    }

    void UpdateAmountText()
    {
        if (amountText == null) return;

        if (IsShopSlot()) return;

        if (!amountText.gameObject.activeSelf)
            amountText.gameObject.SetActive(true);

        amountText.text = (slotIndex == 0 || itemCount <= 1) ? "" : itemCount.ToString();
    }

    private void RefreshShopPriceUI()
    {
        CacheRefs();

        if (!IsShopSlot()) return;

        if (priceText == null)
            priceText = amountText;

        if (priceText == null)
            return;

        if (priceRoot == null)
            ResolvePriceRoot();

        if (priceRoot != null && !priceRoot.activeSelf)
            priceRoot.SetActive(true);

        if (!priceText.gameObject.activeSelf)
            priceText.gameObject.SetActive(true);

        int targetId = shopMarker.itemId;

        int price = 0;
        var item = DataManager.Instance?.GetItem(targetId);
        if (item != null) price = item.saleTime;
        if (price <= 0) price = shopMarker.buyPrice;

        priceText.text = $"{price}s";
    }

    public void SlotClick()
    {
        if (slotIndex == 0)
        {
            Debug.Log("노아이템");
            return;
        }

        if (DataManager.Instance == null) return;

        var item = DataManager.Instance.GetItem(slotIndex);
        if (item == null) return;

        Debug.Log("아이템 이름 :" + item.itemName);
        Debug.Log("아이템 설명 :" + item.description);
        Debug.Log("아이콘 이미지 파일 이름 :" + item.iconImange);
    }
}