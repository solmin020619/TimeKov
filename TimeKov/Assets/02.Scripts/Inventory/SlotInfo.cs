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

    // ✅ 기존 참조 유지(다른 스크립트에서 ownerType/SlotOwnerType 쓰는 거 터지면 안 됨)
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

    // ✅ 가격 배경/루트(= Image 오브젝트) 캐시용
    private GameObject priceRoot;
    private ShopSlotMarker shopMarker;

    // ✅ “한 번도 갱신 안 된 상태”만 잡기 위한 플래그
    private bool shopPriceInitialized = false;

    private void Awake()
    {
        shopMarker = GetComponent<ShopSlotMarker>();

        // priceText의 부모(Image)를 priceRoot로 잡기
        if (priceText != null && priceText.transform.parent != null)
            priceRoot = priceText.transform.parent.gameObject;

        // 기본은 무조건 OFF (상점 슬롯에서만 켜짐)
        if (priceRoot != null) priceRoot.SetActive(false);
        if (priceText != null) priceText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // SetActive 토글 방식이면 여기서도 갱신
        RefreshShopPriceUI();
    }

    // ✅ CanvasGroup로 숨기거나, 생성/세팅 순서 꼬여서 OnEnable/SetSlot 안 타는 케이스 대비
    private IEnumerator Start()
    {
        yield return null;
        RefreshShopPriceUI();
    }

    public void SetSlot(int id, int count)
    {
        slotIndex = id;
        itemCount = count;

        if (slotIndex == 0)
        {
            if (slotText != null) slotText.text = "";
            slotOldIndex = 0;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.gameObject.SetActive(false);
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = Resources.Load<Sprite>("Icon/" + slotIndex);
                iconImage.gameObject.SetActive(true);
            }
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

        // ✅ 상점 슬롯이 아니면 무조건 숨김
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

        // ✅ 상점은 marker.itemId 기준으로 가격 가져오기 (slotIndex 안 믿음)
        int targetId = shopMarker.itemId;

        int price = 0;
        var item = DataManager.Instance?.GetItem(targetId);
        if (item != null)
        {
            // 너 ItemInfo 필드명 맞춰서 (현재 너 코드에 saleTime)
            price = item.saleTime;
        }

        // 데이터 없으면 마커 가격으로 fallback (여기 걸리면 그래도 무조건 90s라도 떠야 정상)
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

            // 상점 슬롯(마커 있음)이고 priceText가 연결되어 있으면, 한번 강제 갱신
            if (shopMarker != null && priceText != null)
            {
                bool rootOff = (priceRoot != null && !priceRoot.activeSelf);
                // 네가 수동으로 'd' 넣어둔 상태거나, root가 꺼져있으면 갱신 시도
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
