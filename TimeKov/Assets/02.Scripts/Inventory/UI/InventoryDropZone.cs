// InventoryDropZone.cs
// 인벤 그리드(ScrollRect) GO에 붙는 드롭존. 양방향.
//   depositToStorage=true  : 가방 아이템을 창고로 입고
//   depositToStorage=false : 창고 아이템을 가방으로 회수
// 동작:
//  - 호환 아이템을 집으면(드래그 시작) 이 영역 전체 강조(regionHighlight) 켜짐.
//  - 포인터가 이 영역에 들어오면 매칭(겹칠) 슬롯 강조 + "드래그하여 ...에 넣기" 힌트.
//  - 드롭하면 대상 인벤으로 이동(자동 스택/끝에 추가).
// ScrollRect와 같은 GO라 드래그(IDragHandler)와 휠 스크롤(IScrollHandler) 독립 -> 드래그 중에도 스크롤 됨.

using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(InventoryGridUI))]
public class InventoryDropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private bool depositToStorage = true;
    [SerializeField] private GameObject regionHighlight;   // 집으면 켜지는 영역 강조 프레임
    [SerializeField] private GameObject dragHint;          // "드래그하여 ...에 넣기" 안내

    private InventoryGridUI _grid;
    private RectTransform _rect;
    private Canvas _canvas;
    private bool _region, _hint;

    private InventoryManager Source => depositToStorage ? InventoryManager.Instance : InventoryManager.StorageInstance;
    private InventoryManager Target => depositToStorage ? InventoryManager.StorageInstance : InventoryManager.Instance;

    private void Awake()
    {
        _grid = GetComponent<InventoryGridUI>();
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        SetRegion(false);
        SetHint(false);
    }

    private void Update()
    {
        var dh = InventoryDragHandler.Instance;
        var src = Source;
        bool compat = dh != null && dh.IsDragging && dh.DraggedSlot != null && !dh.DraggedSlot.IsEmpty
                      && src != null && dh.DraggedSlot.Owner == src;

        if (!compat)
        {
            SetRegion(false);
            SetHint(false);
            // 이 그리드 소속 아이템을 드래그 중(같은 인벤 안 슬롯 이동)이면 그리드 Update가 강조를 담당 -> 지우지 않는다.
            var tgt = Target;
            bool withinThisGrid = dh != null && dh.IsDragging && dh.DraggedSlot != null
                                  && tgt != null && dh.DraggedSlot.Owner == tgt;
            if (!withinThisGrid) _grid?.ClearDropHighlight();
            return;
        }

        SetRegion(true);   // 집자마자 영역 강조
        SetHint(true);     // 집자마자 "드래그하여 ...에 넣기" 힌트도 바로 표시 (포인터 진입 안 기다림)

        if (IsPointerOver())
        {
            // 가방=커서 밑 칸 강조(드롭 위치와 일치), 창고=매칭 칸/새 칸 미리보기
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            _grid?.HighlightDropTarget(dh.DraggedSlot.SlotData.itemId, Input.mousePosition, cam);
        }
        else
        {
            _grid?.ClearDropHighlight();
        }
    }

    private bool IsPointerOver()
    {
        if (_rect == null) return false;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, cam);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dh = InventoryDragHandler.Instance;
        SetRegion(false);
        SetHint(false);
        _grid?.ClearDropHighlight();

        if (dh == null || !dh.IsDragging) { dh?.EndDrag(); return; }

        var dragged = dh.DraggedSlot;
        var src = Source;
        var tgt = Target;
        if (dragged == null || dragged.IsEmpty || src == null || tgt == null || dragged.Owner != src) { dh.EndDrag(); return; }

        var fromSlot = dragged.SlotData;
        if (fromSlot == null) { dh.EndDrag(); return; }

        int movedItemId = fromSlot.itemId;   // 이동 후 칸이 비므로 미리 캡처

        if (dh.IsSplitDrag)
        {
            int t = tgt.FindTargetSlotIndexForItem(fromSlot.itemId);
            if (t >= 0) src.MoveAmountToSlot(fromSlot.slotIndex, dh.DragAmount, tgt, t);
        }
        else
        {
            src.MoveSlot(fromSlot.slotIndex, tgt);   // AddItem 경유 = 자동 스택/끝에 추가
        }

        // 가방 -> 창고 입고면, 옮긴 아이템 카테고리 탭으로 창고 자동 전환(현재 탭이 숨기는 경우만)
        if (depositToStorage)
            InventoryUIController.Instance?.OnDepositedToWarehouse(movedItemId);

        dh.EndDrag();
    }

    private void SetRegion(bool on)
    {
        if (_region == on) return;
        _region = on;
        if (regionHighlight != null) regionHighlight.SetActive(on);
    }

    private void SetHint(bool on)
    {
        if (_hint == on) return;
        _hint = on;
        if (dragHint != null) dragHint.SetActive(on);
    }

    private void OnDisable()
    {
        SetRegion(false);
        SetHint(false);
        _grid?.ClearDropHighlight();
    }
}
