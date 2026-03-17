using UnityEngine;
using UnityEngine.EventSystems;

public class SlotInputHandler : MonoBehaviour, IPointerClickHandler
{
    [Header("Double Click")]
    public float doubleClickTime = 0.25f;
    public bool allowDoubleClick = true;   

    [Header("Optional Refs")]
    public InventoryManager ownerManager;   // 우클릭 메뉴 표시용 owner
    public InventoryManager invenManager;   // 더블클릭 이동용 inventory
    public ContextMenuManager menu;         // 우클릭 메뉴
    public EquipmentManager equipmentManager; // 장비칸 더블클릭 해제용

    private SlotInfo slot;
    private float lastLeftClickTime;

    private static InventoryManager cachedPlayerOwner;

    private void Awake()
    {
        slot = GetComponent<SlotInfo>();

        ResolveMenu();
        ResolveInventoryManagers();
        ResolveEquipmentManager();
    }

    private void ResolveMenu()
    {
        if (menu == null)
            menu = FindAnyObjectByType<ContextMenuManager>();
    }

    private void ResolveInventoryManagers()
    {
        if (ownerManager == null)
            ownerManager = GetComponentInParent<InventoryManager>();

        if (invenManager == null)
            invenManager = ownerManager;
    }

    private void ResolveEquipmentManager()
    {
        if (equipmentManager == null)
            equipmentManager = FindFirstObjectByType<EquipmentManager>();
    }

    private InventoryManager ResolvePlayerOwnerManager()
    {
        if (ownerManager != null)
            return ownerManager;

        if (cachedPlayerOwner != null)
            return cachedPlayerOwner;

        var all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var m = all[i];
            if (m == null) continue;

            if (m.ownerType == InventoryManager.InventoryOwnerType.Player)
            {
                cachedPlayerOwner = m;
                return m;
            }
        }

        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slot == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            HandleRightClick(eventData);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleLeftClick();
        }
    }

    private void HandleRightClick(PointerEventData eventData)
    {
        ResolveMenu();
        if (menu == null)
            return;

        // 기존 기능 유지: 창고 슬롯은 우클릭 메뉴 안 뜸
        if (slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            menu.Hide();
            return;
        }

        // 빈 슬롯이면 기존처럼 메뉴 안 뜸
        if (slot.slotIndex == 0)
        {
            menu.Hide();
            return;
        }

        // 장비칸은 부모 InventoryManager가 없을 수 있으므로 플레이어 인벤 기준 owner 확보
        InventoryManager resolvedOwner = ResolvePlayerOwnerManager();
        if (resolvedOwner == null)
            return;

        menu.Show(slot, resolvedOwner, eventData.position);
    }

    private void HandleLeftClick()
    {
        float timeSinceLast = Time.time - lastLeftClickTime;

        if (timeSinceLast <= doubleClickTime)
        {
            OnDoubleClick();
            lastLeftClickTime = 0f;
        }
        else
        {
            lastLeftClickTime = Time.time;
        }
    }

    private void OnDoubleClick()
    {
        if (slot == null || slot.slotIndex == 0)
            return;

        // 인벤 / 창고 슬롯 => 더블클릭 이동
        if (slot.ownerType == SlotInfo.SlotOwnerType.Inventory ||
            slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            ResolveInventoryManagers();
            if (invenManager == null)
                return;

            invenManager.MoveItemByDoubleClick(slot);
            return;
        }

        // 장비 슬롯 => 더블클릭 해제
        if (slot.ownerType == SlotInfo.SlotOwnerType.Equip)
        {
            ResolveEquipmentManager();
            if (equipmentManager == null)
                return;

            equipmentManager.UnequipToInventory(slot);
        }
    }
}