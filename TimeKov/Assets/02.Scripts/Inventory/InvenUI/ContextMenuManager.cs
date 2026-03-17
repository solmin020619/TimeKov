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
    public Button btnSell;  //  이 버튼을 '판매/구매'로 재사용
    public Button btnDrop;

    [Header("Refs")]
    public EquipmentManager equipmentManager;
    public ShopManager shopManager;

    private SlotInfo currentSlot;
    private InventoryManager currentOwnerManager;
    private ShopSlotMarker currentShopMarker;
    private RectTransform menuRect;

    private TMP_Text btnEquipLabel;
    private TMP_Text btnUnequipLabel;
    private TMP_Text btnSellLabel;
    private TMP_Text btnDropLabel;

    void Awake()
    {
        if (menuRoot != null)
        {
            menuRect = menuRoot.GetComponent<RectTransform>();
            menuRoot.SetActive(false);
        }

        if (equipmentManager == null)
            equipmentManager = FindFirstObjectByType<EquipmentManager>();

        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        CacheButtonLabels();

        if (btnEquip != null) btnEquip.onClick.AddListener(OnClickEquip);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnClickUnequip);
        if (btnSell != null) btnSell.onClick.AddListener(OnClickSellOrBuy);
        if (btnDrop != null) btnDrop.onClick.AddListener(OnClickDrop);
    }

    void Update()
    {
        if (menuRoot == null || !menuRoot.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
            return;

        if (!IsPointerInsideMenu())
            Hide();
    }

    private void CacheButtonLabels()
    {
        btnEquipLabel = GetButtonLabel(btnEquip);
        btnUnequipLabel = GetButtonLabel(btnUnequip);
        btnSellLabel = GetButtonLabel(btnSell);
        btnDropLabel = GetButtonLabel(btnDrop);
    }

    private TMP_Text GetButtonLabel(Button button)
    {
        if (button == null) return null;
        return button.GetComponentInChildren<TMP_Text>(true);
    }

    bool IsPointerInsideMenu()
    {
        if (menuRect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition, null);
    }

    public void Show(SlotInfo slot, InventoryManager ownerManager, Vector2 screenPos)
    {
        if (slot == null) return;
        if (ownerManager == null) return;
        if (menuRoot == null) return;
        if (slot.slotIndex == 0) return;

        currentSlot = slot;
        currentOwnerManager = ownerManager;
        currentShopMarker = slot.GetComponent<ShopSlotMarker>();

        if (menuRect == null)
            menuRect = menuRoot.GetComponent<RectTransform>();

        if (menuRect != null)
            menuRect.position = screenPos;

        RefreshButtons();

        menuRoot.SetActive(true);
        menuRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        currentSlot = null;
        currentOwnerManager = null;
        currentShopMarker = null;
    }

    // 기존 SendMessage 호환용
    public void HideMenu() => Hide();
    public void CloseMenu() => Hide();
    public void Close() => Hide();

    void RefreshButtons()
    {
        if (currentSlot == null || currentOwnerManager == null) return;

        bool isEquipSlot = (currentSlot.ownerType == SlotInfo.SlotOwnerType.Equip);
        bool isInventorySlot = (currentSlot.ownerType == SlotInfo.SlotOwnerType.Inventory);
        bool isShopSlot = (currentShopMarker != null);

        bool shopOpen = (shopManager != null && shopManager.IsShopOpen());

        bool isEquippable = false;
        if (equipmentManager != null)
            isEquippable = (equipmentManager.GetTypeById(currentSlot.slotIndex) != null);

        if (btnEquip != null)
            btnEquip.gameObject.SetActive(!isEquipSlot && isInventorySlot && isEquippable);

        if (btnUnequip != null)
            btnUnequip.gameObject.SetActive(isEquipSlot);

        if (btnSell != null)
        {
            if (!shopOpen)
            {
                btnSell.gameObject.SetActive(false);
            }
            else
            {
                if (isShopSlot)
                {
                    SetButtonText(btnSell, btnSellLabel, "구매");
                    btnSell.gameObject.SetActive(true);
                }
                else if (isInventorySlot)
                {
                    SetButtonText(btnSell, btnSellLabel, "판매");
                    btnSell.gameObject.SetActive(true);
                }
                else
                {
                    btnSell.gameObject.SetActive(false);
                }
            }
        }

        if (btnDrop != null)
            btnDrop.gameObject.SetActive(isInventorySlot);
    }

    void SetButtonText(Button button, TMP_Text cachedLabel, string text)
    {
        if (button == null) return;

        if (cachedLabel != null)
        {
            cachedLabel.text = text;
            return;
        }

        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text uText = button.GetComponentInChildren<Text>(true);
        if (uText != null)
        {
            uText.text = text;
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

        if (currentShopMarker != null)
        {
            shopManager.TryBuyFromContext(currentShopMarker);
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