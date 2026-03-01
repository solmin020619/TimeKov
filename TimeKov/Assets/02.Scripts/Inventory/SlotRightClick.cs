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

        if (slot == null || menu == null)
            return;

        // ✅ 창고 슬롯은 우클릭 메뉴 안 뜨게 유지
        if (slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            menu.SendMessage("HideMenu", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("CloseMenu", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("Hide", SendMessageOptions.DontRequireReceiver);
            menu.SendMessage("Close", SendMessageOptions.DontRequireReceiver);
            return;
        }

        // ✅ 장비칸(Equip)은 InventoryManager가 부모에 없을 수 있음 -> 플레이어 인벤 매니저 찾아서 주입
        InventoryManager resolvedOwner = ownerManager;
        if (resolvedOwner == null)
        {
            var all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
            foreach (var m in all)
            {
                if (m.ownerType == InventoryManager.InventoryOwnerType.Player)
                {
                    resolvedOwner = m;
                    break;
                }
            }
        }

        if (resolvedOwner == null)
            return;

        menu.Show(slot, resolvedOwner, eventData.position);
    }
}