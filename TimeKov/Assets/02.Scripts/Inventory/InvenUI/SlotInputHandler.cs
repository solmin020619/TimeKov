using UnityEngine;
using UnityEngine.EventSystems;

public class SlotInputHandler : MonoBehaviour, IPointerClickHandler
{
    [Header("Double Click")]
    public float doubleClickTime = 0.25f;
    public bool allowDoubleClick = true;

    [Header("Optional Refs")]
    public InventoryManager ownerManager;
    public InventoryManager invenManager;
    public ContextMenuManager menu;
    public EquipmentManager equipmentManager;

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

        if (slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            menu.Hide();
            return;
        }

        if (slot.slotIndex == 0)
        {
            menu.Hide();
            return;
        }

        InventoryManager resolvedOwner = ResolvePlayerOwnerManager();
        if (resolvedOwner == null)
            return;

        menu.Show(slot, resolvedOwner, eventData.position);
    }

    private void HandleLeftClick()
    {
        if (!allowDoubleClick)
        {
            lastLeftClickTime = Time.time;
            return;
        }

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
        if (!allowDoubleClick)
            return;

        if (slot == null || slot.slotIndex == 0)
            return;

        if (slot.ownerType == SlotInfo.SlotOwnerType.Inventory ||
            slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            ResolveInventoryManagers();
            if (invenManager == null)
                return;

            invenManager.MoveItemByDoubleClick(slot);
            return;
        }

        if (slot.ownerType == SlotInfo.SlotOwnerType.Equip)
        {
            ResolveEquipmentManager();
            if (equipmentManager == null)
                return;

            equipmentManager.UnequipToInventory(slot);
        }
    }
}