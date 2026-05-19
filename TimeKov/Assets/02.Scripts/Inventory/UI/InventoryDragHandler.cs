// InventoryDragHandler.cs
// Canvas 에 붙이는 스크립트
// 드래그 중인 슬롯 정보 관리 및 고스트 이미지 처리
// 같은 인벤토리 내 이동, 창고와 가방 간 이동 모두 처리

using UnityEngine;
using UnityEngine.UI;

public class InventoryDragHandler : MonoBehaviour
{
    public static InventoryDragHandler Instance { get; private set; }

    [Header("드래그 고스트 이미지 (DragGhost 오브젝트 연결)")]
    [SerializeField] private Image ghostImage;

    // 현재 드래그 중인 슬롯
    public InventorySlotUI DraggedSlot { get; private set; }
    public bool IsDragging => DraggedSlot != null;

    private RectTransform _ghostRect;
    private RectTransform _canvasRect;

    private void Awake()
    {
        Instance = this;
        _ghostRect = ghostImage != null ? ghostImage.GetComponent<RectTransform>() : null;
        _canvasRect = GetComponent<RectTransform>();

        // 시작 시 고스트 숨기기
        if (ghostImage != null)
            ghostImage.gameObject.SetActive(false);
    }

    // 드래그 시작 (InventorySlotUI.OnBeginDrag 에서 호출)
    public void BeginDrag(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        DraggedSlot = slot;

        // 고스트 이미지에 아이콘 설정
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

    // 드래그 종료 (드롭 성공 여부와 무관하게 항상 호출)
    public void EndDrag()
    {
        DraggedSlot = null;
        if (ghostImage != null)
            ghostImage.gameObject.SetActive(false);
    }

    // 슬롯에 드롭 처리 (InventorySlotUI.OnDrop 에서 호출)
    public void HandleDrop(InventorySlotUI targetSlot)
    {
        if (!IsDragging || targetSlot == null) { EndDrag(); return; }

        // 같은 슬롯에 드롭하면 취소
        if (DraggedSlot == targetSlot) { EndDrag(); return; }

        var fromSlot = DraggedSlot.SlotData;
        var fromManager = DraggedSlot.Owner;
        var toSlot = targetSlot.SlotData;
        var toManager = targetSlot.Owner;

        if (fromManager == null || toManager == null) { EndDrag(); return; }

        if (fromManager == toManager)
        {
            // 같은 인벤토리 내 슬롯 교환
            fromManager.SwapSlots(fromSlot.slotIndex, toSlot.slotIndex);
        }
        else
        {
            // 다른 인벤토리 간 슬롯 이동 (교환)
            fromManager.MoveSlotTo(fromSlot.slotIndex, toManager, toSlot.slotIndex);
        }

        EndDrag();
    }
}