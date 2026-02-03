using UnityEngine;
using UnityEngine.UI;

public class ContextMenuManager : MonoBehaviour
{
    [Header("UI Root")]
    public GameObject menuRoot;

    [Header("Buttons")]
    public Button btnEquip;
    public Button btnUnequip;
    public Button btnSell;
    public Button btnDrop;

    [Header("Refs")]
    public EquipmentManager equipmentManager;

    private SlotInfo currentSlot;
    private InventoryManager currentOwnerManager;

    void Awake()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);

        if (btnEquip != null) btnEquip.onClick.AddListener(OnClickEquip);
        if (btnUnequip != null) btnUnequip.onClick.AddListener(OnClickUnequip);
        if (btnSell != null) btnSell.onClick.AddListener(OnClickSell);
        if (btnDrop != null) btnDrop.onClick.AddListener(OnClickDrop);
    }

    void Update()
    {
        // ✅ 메뉴가 켜져있고, 메뉴 바깥을 클릭하면 닫기
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

        // ✅ 창고에서도 메뉴는 뜨게 한다 (요구사항)
        // 아이템 없는 칸이면 메뉴 안 띄움(원하면 띄우게 변경 가능)
        if (slot.slotIndex == 0)
            return;

        currentSlot = slot;
        currentOwnerManager = ownerManager;

        // 메뉴 위치 = 마우스 위치
        RectTransform rt = menuRoot.GetComponent<RectTransform>();
        if (rt != null)
            rt.position = screenPos;

        RefreshButtons();
        menuRoot.SetActive(true);
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

        bool isWarehouse = (currentOwnerManager.ownerType == InventoryManager.InventoryOwnerType.Warehouse);
        bool isEquipSlot = (currentSlot.ownerType == SlotInfo.SlotOwnerType.Equip);

        // 장비템 판별(EquipmentManager 기준)
        bool isEquippable = false;
        if (equipmentManager != null)
        {
            isEquippable = (equipmentManager.GetTypeById(currentSlot.slotIndex) != null);
        }

        // ✅ 표시 규칙
        // - 창고에서는 장착/해제 숨김
        // - 인벤에서 장비템이면 장착 표시
        // - 장비칸이면 해제 표시
        if (btnEquip != null)
            btnEquip.gameObject.SetActive(!isWarehouse && !isEquipSlot && currentSlot.ownerType == SlotInfo.SlotOwnerType.Inventory && isEquippable);

        if (btnUnequip != null)
            btnUnequip.gameObject.SetActive(!isWarehouse && isEquipSlot);

        // 판매/버리기: 일단 둘 다 뜨게(원하면 창고에서는 숨기게 바꿔줄게)
        if (btnSell != null) btnSell.gameObject.SetActive(true);
        if (btnDrop != null) btnDrop.gameObject.SetActive(true);
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

    void OnClickSell()
    {
        if (currentSlot == null) return;
        Debug.Log($"[SELL] id={currentSlot.slotIndex} count={currentSlot.itemCount} owner={currentOwnerManager.ownerType}");
        Hide();
    }

    void OnClickDrop()
    {
        if (currentSlot == null) return;
        Debug.Log($"[DROP] id={currentSlot.slotIndex} count={currentSlot.itemCount} owner={currentOwnerManager.ownerType}");
        Hide();
    }
}
