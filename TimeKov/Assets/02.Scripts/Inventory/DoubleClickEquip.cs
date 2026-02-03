using UnityEngine;
using UnityEngine.EventSystems;
using static SlotInfo;

public class DoubleClickEquip : MonoBehaviour, IPointerClickHandler
{
    public float doubleClickTime = 0.25f;
    private float lastClickTime;

    private SlotInfo slot;

    // ✅ [변경] InventoryManager에서 슬롯 생성 시 주입할 수 있게 public
    public InventoryManager invenManager;

    void Awake()
    {
        slot = GetComponent<SlotInfo>();

        // ✅ 기존 방식은 유지하되(규칙: 다른 부분 내맘대로 안 바꿈),
        // 이 구조에선 부모 체인에 InventoryManager가 없을 수 있으니,
        // 주입이 안 된 경우에만 fallback으로 찾는다.
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

    void OnDoubleClick()
    {
        if (slot == null || slot.slotIndex == 0)
            return;

        if (invenManager == null)
            return;

        // ✅ 더블클릭 = 이동 전용
        if (slot.ownerType == SlotOwnerType.Inventory || slot.ownerType == SlotOwnerType.Warehouse)
        {
            invenManager.MoveItemByDoubleClick(slot);
            return;
        }
    }
}
