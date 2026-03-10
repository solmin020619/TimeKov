using UnityEngine;
using UnityEngine.EventSystems;

public class DoubleClickEquip : MonoBehaviour, IPointerClickHandler
{
    public float doubleClickTime = 0.25f;
    private float lastClickTime;

    private SlotInfo slot;

    // 슬롯 생성 시 InventoryManager에서 주입
    public InventoryManager invenManager;

    // ================================
    // [추가] 장비칸 더블클릭 해제용 EquipmentManager 참조
    // ================================
    private EquipmentManager equipmentManager;

    private void Awake()
    {
        slot = GetComponent<SlotInfo>();

        if (invenManager == null)
            invenManager = GetComponentInParent<InventoryManager>();

        // ================================
        // [추가] EquipmentManager 자동 탐색
        // ================================
        if (equipmentManager == null)
            equipmentManager = FindFirstObjectByType<EquipmentManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        float timeSinceLast = Time.time - lastClickTime;

        if (timeSinceLast <= doubleClickTime)
        {
            OnDoubleClick();
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = Time.time;
        }
    }

    private void OnDoubleClick()
    {
        if (slot == null || slot.slotIndex == 0) return;

        // 더블클릭 = 이동 전용 (인벤/창고만)
        if (slot.ownerType == SlotInfo.SlotOwnerType.Inventory ||
            slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            if (invenManager == null) return;
            invenManager.MoveItemByDoubleClick(slot);
            return;
        }

        // ================================
        // [추가] 장비칸 더블클릭 = 장비 해제
        // 무기칸이면 EquipmentManager 내부에서 실제 무기/nogun 상태까지 같이 갱신됨
        // ================================
        if (slot.ownerType == SlotInfo.SlotOwnerType.Equip)
        {
            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();

            if (equipmentManager == null) return;

            equipmentManager.UnequipToInventory(slot);
        }
    }
}