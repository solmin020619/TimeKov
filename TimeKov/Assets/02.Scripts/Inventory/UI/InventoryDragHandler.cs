// InventoryDragHandler.cs
// Canvas 에 붙는 스크립트. 드래그 상태 보관 + 고스트 이미지 처리.
// 같은/다른 인벤토리 간 이동 처리.

using UnityEngine;
using UnityEngine.UI;

public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    [Header("드래그 고스트 이미지 (DragGhost 연결)")]
    [SerializeField] private Image ghostImage;

    // 현재 드래그 중인 슬롯
    public InventorySlotUI DraggedSlot { get; private set; }
    public bool IsDragging => DraggedSlot != null;

    /// <summary>ALT 분할 드래그 시 이동할 수량. 0 = 전체 스택.</summary>
    public int  DragAmount  { get; private set; }
    public bool IsSplitDrag => DragAmount > 0;

    private RectTransform _ghostRect;
    private RectTransform _canvasRect;

    private void Awake()
    {
        Instance = this;
        _ghostRect = ghostImage != null ? ghostImage.GetComponent<RectTransform>() : null;
        _canvasRect = GetComponent<RectTransform>();

        // 시작 시 고스트 숨김 + 레이캐스트 차단 해제(드래그 중 아래 슬롯/드롭존 감지 방해 안 하게)
        if (ghostImage != null)
        {
            ghostImage.raycastTarget = false;
            ghostImage.gameObject.SetActive(false);
        }
    }

    // 드래그 시작 (InventorySlotUI.OnBeginDrag 에서 호출)
    // amount = 0 : 전체 스택 / amount > 0 : ALT 분할 드래그
    public void BeginDrag(InventorySlotUI slot, int amount = 0)
    {
        if (slot == null || slot.IsEmpty) return;

        DraggedSlot = slot;
        DragAmount  = amount;

        // 고스트 이미지에 아이콘 세팅
        if (ghostImage != null)
        {
            var data = ItemDatabase.GetItem(slot.SlotData.itemId);
            ghostImage.sprite = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            ghostImage.color = Color.white;
            ghostImage.gameObject.SetActive(true);
        }
    }

    // 드래그 중 고스트 위치 갱신 (InventorySlotUI.OnDrag 에서 호출)
    public void UpdateDragPosition(Vector2 screenPos)
    {
        if (!IsDragging || _ghostRect == null || _canvasRect == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out localPos);

        _ghostRect.anchoredPosition = localPos;
    }

    // 드래그 종료 (성공·실패 무관하게 항상 호출)
    public void EndDrag()
    {
        DraggedSlot = null;
        DragAmount  = 0;
        if (ghostImage != null)
            ghostImage.gameObject.SetActive(false);
    }

    // 드랍 수신 처리 (InventorySlotUI.OnDrop 에서 호출)
    public void HandleDrop(InventorySlotUI targetSlot)
    {
        if (!IsDragging || targetSlot == null) { EndDrag(); return; }

        // 같은 슬롯에 드랍하면 취소
        if (DraggedSlot == targetSlot) { EndDrag(); return; }

        var fromSlot    = DraggedSlot.SlotData;
        var fromManager = DraggedSlot.Owner;
        var toSlot      = targetSlot.SlotData;
        var toManager   = targetSlot.Owner;

        if (fromManager == null || toManager == null) { EndDrag(); return; }

        if (IsSplitDrag)
        {
            // ALT 분할 드래그: 지정 수량만 이동
            fromManager.MoveAmountToSlot(fromSlot.slotIndex, DragAmount, toManager, toSlot.slotIndex);
        }
        else if (fromManager == toManager)
        {
            fromManager.SwapSlots(fromSlot.slotIndex, toSlot.slotIndex);
        }
        else
        {
            fromManager.MoveSlotTo(fromSlot.slotIndex, toManager, toSlot.slotIndex);
        }

        EndDrag();
    }
}
