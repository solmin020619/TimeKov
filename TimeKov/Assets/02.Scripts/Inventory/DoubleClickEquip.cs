using UnityEngine;
using UnityEngine.EventSystems;

public class DoubleClickEquip : MonoBehaviour, IPointerClickHandler
{
    public float doubleClickTime = 0.25f;
    private float lastClickTime;

    private SlotInfo slot;

    // 슬롯 생성 시 InventoryManager에서 주입
    public InventoryManager invenManager;

    private void Awake()
    {
        slot = GetComponent<SlotInfo>();

        if (invenManager == null)
            invenManager = GetComponentInParent<InventoryManager>();
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
        if (invenManager == null) return;

        // 더블클릭 = 이동 전용 (인벤/창고만)
        if (slot.ownerType == SlotInfo.SlotOwnerType.Inventory ||
            slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            invenManager.MoveItemByDoubleClick(slot);
        }
    }
}
