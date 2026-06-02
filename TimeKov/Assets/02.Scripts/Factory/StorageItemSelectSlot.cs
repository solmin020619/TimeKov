using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// StorageExtractorUI에 붙는 아이템 선택 슬롯.
/// 인벤토리에서 아이템을 드래그해 놓으면 해당 아이템 ID가 추출 대상으로 설정된다.
/// 아이템은 소비하지 않고 ID만 캡처한다.
/// </summary>
public class StorageItemSelectSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image           iconImage;     // 슬롯 아이콘
    [SerializeField] private Image           bgIconImage;   // 왼쪽 패널 배경 아이템 이미지
    [SerializeField] private Image           borderImage;
    [SerializeField] private TextMeshProUGUI countText;   // 왼쪽 수량 텍스트
    [SerializeField] private TextMeshProUGUI labelText;   // hover 시 "아이템 선택"

    [Header("Hover 색상")]
    [SerializeField] private Color normalBorderColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hoverBorderColor  = new Color(0.3f, 0.8f, 1f, 0.8f);

    private StorageExtractor _machine;

    // ── 초기화 / 정리 ───────────────────────────────────────────────────

    public void Setup(StorageExtractor machine)
    {
        if (_machine != null) _machine.OnSelectionChanged -= Refresh;

        _machine = machine;
        _machine.OnSelectionChanged += Refresh;

        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        Refresh();
    }

    public void Cleanup()
    {
        if (_machine != null) _machine.OnSelectionChanged -= Refresh;
        _machine = null;
    }

    private void OnDestroy() => Cleanup();

    // ── 매 프레임 — 창고 수량 갱신 ─────────────────────────────────────

    private void Update()
    {
        if (_machine != null && _machine.SelectedItemId > 0)
            RefreshCount();
    }

    // ── UI 갱신 ─────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (_machine == null) return;

        int itemId = _machine.SelectedItemId;

        if (itemId <= 0)
        {
            if (iconImage   != null) iconImage.enabled   = false;
            if (bgIconImage != null) bgIconImage.enabled = false;
            if (countText   != null) countText.text      = "";
            return;
        }

        var itemData = GameDataUtility.GetItem(itemId);
        Sprite sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;

        if (iconImage != null)
        {
            iconImage.sprite  = sprite;
            iconImage.enabled = sprite != null;
        }

        if (bgIconImage != null)
        {
            bgIconImage.sprite  = sprite;
            bgIconImage.enabled = sprite != null;
        }

        RefreshCount();
    }

    private void RefreshCount()
    {
        if (_machine == null || countText == null) return;
        int itemId = _machine.SelectedItemId;
        if (itemId <= 0) { countText.text = ""; return; }

        var storage = InventoryManager.StorageInstance;
        int count   = storage?.GetTotalItemCount(itemId) ?? 0;
        countText.text = $"창고: {count}개";
    }

    // ── Hover ─────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData e)
    {
        bool isDragging = InventoryDragHandler.Instance != null
                       && InventoryDragHandler.Instance.IsDragging;
        if (!isDragging) return;

        if (borderImage != null) borderImage.color = hoverBorderColor;
        if (labelText   != null) labelText.text    = "아이템 선택";
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
    }

    // ── Drop — 아이템 ID만 캡처, 소비 안 함 ─────────────────────────────

    public void OnDrop(PointerEventData e)
    {
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        if (_machine == null) return;

        var handler = InventoryDragHandler.Instance;
        if (handler == null || !handler.IsDragging) return;

        var draggedSlot = handler.DraggedSlot;
        if (draggedSlot == null || draggedSlot.IsEmpty) return;

        int itemId = draggedSlot.SlotData.itemId;
        _machine.SetTargetItem(itemId);
        // 아이템 소비 없이 ID만 설정 — 인벤토리는 그대로 유지
    }
}
