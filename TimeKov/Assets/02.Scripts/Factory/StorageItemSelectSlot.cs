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
public class StorageItemSelectSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image           iconImage;     // 슬롯 아이콘
    [SerializeField] private Image           bgIconImage;   // 왼쪽 패널 배경 아이템 이미지
    [SerializeField] private Image           borderImage;
    [SerializeField] private TextMeshProUGUI countText;   // 왼쪽 수량 텍스트
    [SerializeField] private TextMeshProUGUI labelText;   // hover 시 "아이템 선택" (드롭 대상 안내)
    [SerializeField] private TextMeshProUGUI nameText;    // 선택된 아이템 이름
    [SerializeField] private GameObject       removeOverlay;     // hover/드래그 시 아이콘 위에 뜨는 해제 안내 막
    [SerializeField] private TextMeshProUGUI  removeOverlayText; // 오버레이 안내 문구

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
            if (nameText    != null) nameText.text       = "";
            return;
        }

        var itemData = GameDataUtility.GetItem(itemId);
        Sprite sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;

        if (nameText != null)
            nameText.text = itemData != null ? itemData.itemName : "";

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

        if (isDragging)
        {
            // 인벤 아이템을 드래그해 올 때 = 드롭 대상 안내 (해제 오버레이는 숨김)
            ShowRemoveOverlay(false, null);
            if (borderImage != null) borderImage.color = hoverBorderColor;
            if (labelText   != null) labelText.text    = "아이템 선택";
        }
        else if (_machine != null && _machine.SelectedItemId > 0)
        {
            // 이미 선택돼 있으면 = 드래그로 빼서 해제 가능 안내 (아이콘 위 오버레이)
            if (borderImage != null) borderImage.color = hoverBorderColor;
            ShowRemoveOverlay(true, "드래그로 빼기");
        }
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_removeDragActive) return;   // 빼는 중엔 hover 리셋 무시
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
        ShowRemoveOverlay(false, null);
    }

    // 해제 안내 오버레이 표시/숨김
    private void ShowRemoveOverlay(bool show, string msg)
    {
        if (removeOverlayText != null && show && msg != null) removeOverlayText.text = msg;
        if (removeOverlay != null) removeOverlay.SetActive(show);
    }

    // ── 드래그로 추출 품목 빼기 (슬롯 밖에서 놓으면 해제) ────────────────
    private GameObject _dragGhost;
    private bool       _removeDragActive;

    public void OnBeginDrag(PointerEventData e)
    {
        if (_machine == null || _machine.SelectedItemId <= 0) return;
        if (iconImage == null || iconImage.sprite == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 커서를 따라다니는 드래그 고스트 생성 (최상위 캔버스)
        _dragGhost = new GameObject("RemoveDragGhost", typeof(RectTransform));
        _dragGhost.transform.SetParent(canvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();
        var gimg = _dragGhost.AddComponent<Image>();
        gimg.sprite        = iconImage.sprite;
        gimg.raycastTarget = false;
        gimg.preserveAspect = true;
        ((RectTransform)_dragGhost.transform).sizeDelta = new Vector2(72, 72);

        _removeDragActive = true;

        // 빼는 중 안내 (아이콘 위 오버레이 + 테두리 강조)
        if (borderImage != null) borderImage.color = hoverBorderColor;
        ShowRemoveOverlay(true, "밖에 놓으면 해제");

        UpdateGhostPosition(e);
    }

    public void OnDrag(PointerEventData e) => UpdateGhostPosition(e);

    private void UpdateGhostPosition(PointerEventData e)
    {
        if (_dragGhost == null) return;
        var canvas = GetComponentInParent<Canvas>();
        var canvasRt = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRt == null) return;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, e.position, cam, out var lp))
            ((RectTransform)_dragGhost.transform).anchoredPosition = lp;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_dragGhost != null) { Destroy(_dragGhost); _dragGhost = null; }
        if (iconImage   != null) iconImage.color = Color.white;
        if (labelText   != null) labelText.text  = "";
        if (borderImage != null) borderImage.color = normalBorderColor;
        ShowRemoveOverlay(false, null);

        if (!_removeDragActive) return;
        _removeDragActive = false;

        if (_machine == null || _machine.SelectedItemId <= 0) return;

        // 슬롯 영역 밖에서 놓으면 해제, 안이면 원위치
        var rt     = transform as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        bool insideSlot = rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, e.position, cam);

        if (!insideSlot)
        {
            _machine.SetTargetItem(-1);   // 선택 해제 → 추출 중지
            UISoundManager.Instance?.PlayItemDrop();
        }
        else
        {
            Refresh();   // 아이콘 복구
        }
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

        // 인벤 드롭과 동일한 드롭 사운드 (선택 슬롯은 InventorySlotUI가 아니라 자동 재생이 안 됨)
        UISoundManager.Instance?.PlayItemDrop();
    }
}
