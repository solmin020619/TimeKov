using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotInfo : MonoBehaviour
{
    public int slotIndex; // 아이템 ID
    public int slotOldIndex;
    public int itemCount; // 아이템 개수

    public Image iconImage; // 아이템 아이콘 (자식 Icon)
    public TextMeshProUGUI slotText;
    public TextMeshProUGUI amountText; // 개수 표시 텍스트

    public enum SlotOwnerType
    {
        Inventory,
        Equip,
        Warehouse,
        Loot
    }
    public SlotOwnerType ownerType;

    [Header("Shop Price (Optional)")]
    public TextMeshProUGUI priceText; // 프리팹의 Image 아래 Text(TMP) 연결

    private GameObject priceRoot;
    private ShopSlotMarker shopMarker;

    private bool shopPriceInitialized = false;

    // 장비칸 기본 문구(총기칸/방탄모칸 등) 복구용
    private string defaultSlotText = "";

    // ✅ (추가) 아이템 없을 때 보여줄 "기본 슬롯 배경 아이콘" 스프라이트 캐시
    private Sprite defaultIconSprite = null;
    private bool defaultIconSpriteCaptured = false;

    private void Awake()
    {
        shopMarker = GetComponent<ShopSlotMarker>();

        // 초기 슬롯 텍스트 저장
        if (slotText != null)
            defaultSlotText = slotText.text;

        // ✅ (추가) Icon에 원래 들어있던 기본 스프라이트(= 슬롯 배경) 저장
        if (iconImage != null)
        {
            defaultIconSprite = iconImage.sprite; // 인스펙터에 박혀있는 슬롯 배경 이미지
            defaultIconSpriteCaptured = true;
        }

        if (priceText != null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        // 기본은 무조건 OFF (상점 슬롯에서만 켜짐)
        if (priceRoot != null) priceRoot.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
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

    public void SetSlot(int id, int count)
    {
        if (shopMarker == null) shopMarker = GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null);

        int effectiveId = isShopSlot ? shopMarker.itemId : id;

        slotIndex = effectiveId;
        itemCount = count;

        // (선택) 배그 리스트 느낌: 빈 슬롯은 숨김 (장비칸/상점칸 제외)
        if (!isShopSlot && ownerType != SlotOwnerType.Equip)
            gameObject.SetActive(slotIndex != 0);
        else
            if (!gameObject.activeSelf) gameObject.SetActive(true);

        // 빈 슬롯
        if (slotIndex == 0)
        {
            if (slotText != null)
                slotText.text = (ownerType == SlotOwnerType.Equip) ? defaultSlotText : "";

            slotOldIndex = 0;

            // 아이템 아이콘 비우기(=투명/끄기) + 배경은 IconBG가 들고있게 해야 함
            if (!isShopSlot && iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            UpdateAmountText();
            RefreshShopPriceUI();
            return;
        }

        // 아이템 있을 때: 아이콘
        if (iconImage != null)
        {
            iconImage.sprite = Resources.Load<Sprite>("Icon/" + slotIndex);
            iconImage.enabled = true;
        }

        // ✅ 아이템 있을 때: 이름 즉시 세팅 (Update 기다리지 않게)
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
        amountText.text = (slotIndex == 0 || itemCount <= 1) ? "" : itemCount.ToString();
    }

    private void RefreshShopPriceUI()
    {
        if (priceText == null) return;

        if (priceRoot == null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        if (shopMarker == null)
            shopMarker = GetComponent<ShopSlotMarker>();

        // 상점 슬롯이 아니면 무조건 숨김
        if (shopMarker == null)
        {
            if (priceRoot != null && priceRoot.activeSelf) priceRoot.SetActive(false);
            if (priceText.gameObject.activeSelf) priceText.gameObject.SetActive(false);
            shopPriceInitialized = false;
            return;
        }

        // ✅ 상점 슬롯이면 표시 (배경+텍스트)
        if (priceRoot != null && !priceRoot.activeSelf) priceRoot.SetActive(true);
        if (!priceText.gameObject.activeSelf) priceText.gameObject.SetActive(true);

        int targetId = shopMarker.itemId;

        int price = 0;
        var item = DataManager.Instance?.GetItem(targetId);
        if (item != null)
        {
            price = item.saleTime; // 너 프로젝트 기준
        }

        if (price <= 0) price = shopMarker.buyPrice;

        priceText.text = $"{price}s";
        shopPriceInitialized = true;
    }

    void Update()
    {
        // ✅ 상점 슬롯인데 SetSlot/OnEnable이 안 타서 가격이 한 번도 안 바뀌는 경우 대비
        if (!shopPriceInitialized)
        {
            if (shopMarker == null) shopMarker = GetComponent<ShopSlotMarker>();

            if (shopMarker != null && priceText != null)
            {
                bool rootOff = (priceRoot != null && !priceRoot.activeSelf);
                bool looksUninitialized = rootOff || priceText.text == "d" || string.IsNullOrEmpty(priceText.text);

                if (looksUninitialized)
                    RefreshShopPriceUI();
            }
        }

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
