// =====================================================================
// InventoryPanelDropZone.cs
// 설비 UI - 인벤토리 패널 드롭존
//
// 인벤토리 패널(슬롯 배경 패널) 오브젝트에 붙여두면
// MachineSlotWidget(output 슬롯)에서 드래그한 아이템을
// 패널 어디에 드랍해도 자동으로 인벤토리에 이동시켜 줍니다.
// 엔드필드와 동일한 "패널에 드롭 → 자동 인벤토리 추가" 방식.
// =====================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TIMEKOV.Factory;

public class InventoryPanelDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("드롭 가능 강조 색상 (선택 사항)")]
    [SerializeField] private Image highlightImage;
    [SerializeField] private Color normalColor    = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.8f, 1f, 0.15f);

    // MachineUI 가 세팅해주는 콜백 (아이템 ID, 수량을 받아 처리)
    private Action<int, int> _onDropCallback;

    private void Awake()
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;
    }

    /// <summary>
    /// MachineUI 에서 호출. output 아이템을 인벤토리로 이동하는 액션을 등록합니다.
    /// </summary>
    public void SetDropCallback(Action<int, int> callback)
    {
        _onDropCallback = callback;
    }

    // ── 드래그 올라왔을 때 패널 강조 ────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!MachineSlotWidget.IsOutputDragging && !RecipeDropSlot.IsRecipeDragging && !FuelDropSlot.IsFuelDragging) return;
        if (highlightImage != null)
            highlightImage.color = highlightColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;
    }

    // ── 드롭 처리 ────────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;

        if (MachineSlotWidget.IsOutputDragging)
            AcceptOutputDrop();
        else if (RecipeDropSlot.IsRecipeDragging)
            AcceptRecipeDrop();
        else if (FuelDropSlot.IsFuelDragging)
            AcceptFuelDrop();
    }

    /// <summary>
    /// output 슬롯 드래그 → 인벤토리 이동.
    /// InventorySlotUI.OnDrop에서도 호출됩니다.
    /// </summary>
    public void AcceptOutputDrop()
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;

        if (!MachineSlotWidget.IsOutputDragging) return;

        int itemId = MachineSlotWidget.DragItemId;
        int amount = MachineSlotWidget.DragAmount;

        if (itemId <= 0 || amount <= 0) return;

        _onDropCallback?.Invoke(itemId, amount);
    }

    /// <summary>
    /// 연료 슬롯 드래그 → 인벤토리로 반환.
    /// </summary>
    public void AcceptFuelDrop()
    {
        if (highlightImage != null) highlightImage.color = normalColor;
        if (!FuelDropSlot.IsFuelDragging) return;

        var machine = FuelDropSlot.DragMachine;
        var inv     = FuelDropSlot.DragInventory != null
                        ? FuelDropSlot.DragInventory
                        : InventoryManager.Instance;
        int itemId  = FuelDropSlot.DragFuelItemId;

        if (machine == null || itemId <= 0) return;

        int count = machine.TakeFuel();
        if (count <= 0) return;

        // 못 들어간 만큼은 설비 연료로 되돌림 - 가방 가득 시 증발 방지.
        int leftover = inv != null ? inv.AddItem(itemId, count) : count;
        if (leftover > 0)
        {
            machine.AddFuel(leftover);
            ToastManager.Warning("가방이 가득 찼습니다");
        }
        inv?.ForceRefreshUI();
        machine.PublicNotifyBufferChanged();
    }

    /// <summary>
    /// recipe(input) 슬롯 드래그 → 인벤토리로 반환.
    /// InventorySlotUI.OnDrop에서도 호출됩니다.
    /// </summary>
    public void AcceptRecipeDrop()
    {
        if (highlightImage != null)
            highlightImage.color = normalColor;

        if (!RecipeDropSlot.IsRecipeDragging) return;

        int itemId          = RecipeDropSlot.DragItemId;
        var machine         = RecipeDropSlot.DragMachine;
        var inv             = RecipeDropSlot.DragInventory != null
                                ? RecipeDropSlot.DragInventory
                                : InventoryManager.Instance;

        if (machine == null || itemId <= 0) return;

        // 현재 버퍼에 실제로 들어있는 양을 다시 확인 (드래그 시작 후 변했을 수도 있음)
        int amount = machine.InputBuffer.GetAmount(itemId);
        if (amount <= 0) return;

        // 먼저 넣어보고 들어간 만큼만 차감 - 가방 가득 시 초과분 증발 방지(남는 건 설비 유지).
        int leftover = inv != null ? inv.AddItem(itemId, amount) : amount;
        int taken = amount - leftover;
        if (taken > 0) machine.InputBuffer.Consume(itemId, taken);
        if (leftover > 0) ToastManager.Warning("가방이 가득 찼습니다");
        inv?.ForceRefreshUI();
        machine.PublicNotifyBufferChanged();
    }
}
