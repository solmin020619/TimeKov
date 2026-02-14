using UnityEngine;
using UnityEngine.EventSystems;

public class SlotRightClick : MonoBehaviour, IPointerClickHandler
{
    private SlotInfo slot;
    private ContextMenuManager menu;

    // ✅ 슬롯 생성 시 InventoryManager가 주입해주면 베스트
    public InventoryManager ownerManager;

    void Awake()
    {
        slot = GetComponent<SlotInfo>();
        menu = FindAnyObjectByType<ContextMenuManager>();

        if (ownerManager == null)
            ownerManager = GetComponentInParent<InventoryManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (slot == null || menu == null || ownerManager == null)
            return;

        // ✅ [추가] 창고(Warehouse) 슬롯은 우클릭 메뉴 "아예 안 뜨게" (파밍상자처럼)
        if (slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            // 혹시 다른 곳에서 열려있던 메뉴가 남아있으면 닫기 시도 (메서드 없어도 에러 안 남)
            menu.SendMessage("HideMenu", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("CloseMenu", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("Hide", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("Close", SendMessageOptions.DontRequireReceiver);
            return;
        }

        // 나머지는 기존 그대로
        menu.Show(slot, ownerManager, eventData.position);
    }
}
