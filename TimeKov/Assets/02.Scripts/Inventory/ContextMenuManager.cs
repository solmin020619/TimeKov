using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ContextMenuManager : MonoBehaviour
{
    [Header("UI Root")]
    public GameObject menuRoot;

    [Header("Buttons")]
    public Button btnEquip;
    public Button btnUnequip;
    public Button btnSell;  // ✅ 이 버튼을 '판매/구매'로 재사용
    public Button btnDrop;

    [Header("Refs")]
    public EquipmentManager equipmentManager;
    public ShopManager shopManager;

    private SlotInfo currentSlot;
    private InventoryManager currentOwnerManager;

    void Awake()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        if (btnEquip != null) btnEquip.onClick.AddListener(OnClickEquip);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnClickUnequip);
        if (btnSell != null) btnSell.onClick.AddListener(OnClickSellOrBuy);
        if (btnDrop != null) btnDrop.onClick.AddListener(OnClickDrop);
    }

    void Update()
    {
        if (menuRoot != null && menuRoot.activeSelf)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!IsPointerInsideMenu())
                    Hide();
            }
        }
    }

    bool IsPointerInsideMenu()
    {
        if (menuRoot == null) return false;
        RectTransform rt = menuRoot.GetComponent<RectTransform>();
        if (rt == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
    }

    public void Show(SlotInfo slot, InventoryManager ownerManager, Vector2 screenPos)
    {
        if (slot == null) return;
        if (ownerManager == null) return;
        if (menuRoot == null) return;

        if (slot.slotIndex == 0)
            return;

        currentSlot = slot;
        currentOwnerManager = ownerManager;

        RectTransform rt = menuRoot.GetComponent<RectTransform>();
        if (rt != null)
            rt.position = screenPos;

        RefreshButtons();

        menuRoot.SetActive(true);

        // ✅ 항상 맨 앞으로(최상단) 가져오기
        menuRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        currentSlot = null;
        currentOwnerManager = null;
    }

    void RefreshButtons()
    {
        if (currentSlot == null || currentOwnerManager == null) return;

        bool isEquipSlot = (currentSlot.ownerType == SlotInfo.SlotOwnerType.Equip);
        bool isInventorySlot = (currentSlot.ownerType == SlotInfo.SlotOwnerType.Inventory);

        // 상점 슬롯인지 판별(ShopSlotMarker가 붙어있으면 상점 슬롯)
        ShopSlotMarker shopMarker = currentSlot.GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null);

        // 상점 열림 여부
        bool shopOpen = (shopManager != null && shopManager.IsShopOpen());

        // 장비템 판별
        bool isEquippable = false;
        if (equipmentManager != null)
        {
            isEquippable = (equipmentManager.GetTypeById(currentSlot.slotIndex) != null);
        }

        // ✅ 장착/해제 기존 규칙 유지
        if (btnEquip != null)
            btnEquip.gameObject.SetActive(!isEquipSlot && isInventorySlot && isEquippable);

        if (btnUnequip != null)
            btnUnequip.gameObject.SetActive(isEquipSlot);

        // ✅ 판매/구매 버튼(=btnSell)
        if (btnSell != null)
        {
            // 상점이 열려있을 때만 표시
            if (!shopOpen)
            {
                btnSell.gameObject.SetActive(false);
            }
            else
            {
                // 상점 슬롯이면 "구매", 인벤 슬롯이면 "판매"
                if (isShopSlot)
                {
                    SetButtonText(btnSell, "구매");
                    btnSell.gameObject.SetActive(true);
                }
                else if (isInventorySlot)
                {
                    SetButtonText(btnSell, "판매");
                    btnSell.gameObject.SetActive(true);
                }
                else
                {
                    // 장비칸/창고 등은 판매/구매 숨김(원하면 바꿔도 됨)
                    btnSell.gameObject.SetActive(false);
                }
            }
        }

        // 드롭은 일단 인벤에서만 (원하면 shopOpen 여부 상관없이 켜도 됨)
        if (btnDrop != null)
            btnDrop.gameObject.SetActive(isInventorySlot);
    }

    void SetButtonText(Button b, string text)
    {
        if (b == null) return;

        TextMeshProUGUI tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text uText = b.GetComponentInChildren<Text>();
        if (uText != null)
        {
            uText.text = text;
            return;
        }
    }

    void OnClickEquip()
    {
        if (currentSlot == null) return;
        if (equipmentManager == null) return;

        equipmentManager.EquipOrSwapFromInventorySlot(currentSlot);
        Hide();
    }

    void OnClickUnequip()
    {
        if (currentSlot == null) return;
        if (equipmentManager == null) return;

        equipmentManager.UnequipToInventory(currentSlot);
        Hide();
    }

    void OnClickSellOrBuy()
    {
        if (currentSlot == null) return;
        if (shopManager == null) return;

        ShopSlotMarker shopMarker = currentSlot.GetComponent<ShopSlotMarker>();
        bool isShopSlot = (shopMarker != null);

        if (isShopSlot)
        {
            shopManager.TryBuyFromContext(shopMarker);
        }
        else
        {
            shopManager.TrySellFromContext(currentSlot);
        }

        Hide();
    }

    void OnClickDrop()
    {
        if (currentSlot == null) return;

        Debug.Log($"[DROP] id={currentSlot.slotIndex} count={currentSlot.itemCount}");
        Hide();
    }
}
