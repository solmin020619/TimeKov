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

        menu.Show(slot, ownerManager, eventData.position);
    }
}
