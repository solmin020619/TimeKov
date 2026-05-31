using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TIMEKOV.Factory;

/// <summary>
/// 설비 UI의 연료 슬롯.
/// - 인벤토리 → 연료 슬롯 드래그&드랍 : 연료 투입
/// - 연료 슬롯 → 인벤토리 드래그&드랍 : 연료 회수
/// - 더블클릭 : 연료 전량 회수
/// </summary>
[RequireComponent(typeof(Image))]
public class FuelDropSlot : MonoBehaviour,
    IDropHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private Image           borderImage;
    [SerializeField] private TextMeshProUGUI amountText; // 스택 수 (예: "x3")
    [SerializeField] private TextMeshProUGUI timeText;   // 남은 가동 시간 (예: "80초")
    [SerializeField] private TextMeshProUGUI labelText;  // hover 시 "연료 넣기" 표시

    [Header("Hover 색상")]
    [SerializeField] private Color normalBorderColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hoverBorderColor  = new Color(1f, 0.8f, 0.2f, 0.8f);
    [SerializeField] private Color noFuelTextColor   = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color normalTextColor   = new Color(1f, 1f, 1f, 1f);

    // ── 드래그 아웃 static 상태 (InventoryPanelDropZone이 읽어감) ──────
    public static bool             IsFuelDragging { get; private set; }
    public static MachineBase      DragMachine   { get; private set; }
    public static InventoryManager DragInventory { get; private set; }
    public static int              DragFuelCount { get; private set; }
    public static int              DragFuelItemId{ get; private set; }

    private static GameObject _dragVisual;

    // ── 더블클릭 감지 ──────────────────────────────────────────────────
    private float _lastClickTime = -1f;
    private const float DoubleClickThreshold = 0.3f;

    private MachineBase       _machine;
    private InventoryManager  _inventory;
    private Canvas            _canvas;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        GetComponent<Image>().raycastTarget = true;

        // 초기 상태 명시적 초기화
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
    }

    private void OnDisable()
    {
        IsFuelDragging = false;
        if (_dragVisual != null) { Destroy(_dragVisual); _dragVisual = null; }
    }

    // ── 초기화 / 정리 ───────────────────────────────────────────────────

    public void Setup(MachineBase machine, InventoryManager inventory)
    {
        if (_machine != null) _machine.OnFuelChanged -= OnFuelChanged;

        _machine   = machine;
        _inventory = inventory != null ? inventory : InventoryManager.Instance;

        _machine.OnFuelChanged += OnFuelChanged;

        // UI 초기 상태 리셋
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        RefreshIcon();
        RefreshTime();
    }

    public void Cleanup()
    {
        if (_machine != null) _machine.OnFuelChanged -= OnFuelChanged;
        _machine = null;
    }

    private void OnDestroy() => Cleanup();

    // ── 매 프레임 — 남은 시간 갱신 ─────────────────────────────────────

    private void Update()
    {
        if (_machine != null && _machine.Status == MachineStatus.Processing)
            RefreshTime();
    }

    // ── 이벤트 콜백 ────────────────────────────────────────────────────

    private void OnFuelChanged() => RefreshTime();

    // ── UI 갱신 ─────────────────────────────────────────────────────────

    private void RefreshTime()
    {
        if (_machine == null) return;

        float t     = _machine.FuelTimeRemaining;
        int   count = _machine.FuelItemCount;

        if (amountText != null)
            amountText.text = count > 0 ? $"x{count}" : "";

        if (timeText != null)
        {
            if (t > 0f)
            {
                timeText.text  = $"{t:F0}초";
                timeText.color = normalTextColor;
            }
            else
            {
                timeText.text  = "연료 없음";
                timeText.color = noFuelTextColor;
            }
        }

        // 연료 상태 변경 시 아이콘도 갱신
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (iconImage == null) return;

        // 연료가 없으면 아이콘 숨김
        if (_machine == null || _machine.FuelTimeRemaining <= 0f)
        {
            iconImage.enabled = false;
            return;
        }

        var cfg = FuelConfig.Instance;
        if (cfg == null) { iconImage.enabled = false; return; }

        var itemData = GameDataUtility.GetItem(cfg.fuelItemId);
        Sprite sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;

        iconImage.sprite  = sprite;
        iconImage.color   = Color.white;
        iconImage.enabled = sprite != null;
    }

    // ── Hover (인벤토리 → 연료슬롯 방향) ────────────────────────────────

    public void OnPointerEnter(PointerEventData e)
    {
        bool isDragging = InventoryDragHandler.Instance != null
                       && InventoryDragHandler.Instance.IsDragging;
        if (!isDragging) return;

        var cfg = FuelConfig.Instance;
        if (cfg == null) return;

        var draggedSlot = InventoryDragHandler.Instance.DraggedSlot;
        if (draggedSlot == null || draggedSlot.IsEmpty) return;
        if (draggedSlot.SlotData.itemId != cfg.fuelItemId) return;

        if (borderImage != null) borderImage.color = hoverBorderColor;
        if (labelText   != null) labelText.text    = "연료 넣기";
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
    }

    // ── Drop (인벤토리 → 연료슬롯) ───────────────────────────────────────

    public void OnDrop(PointerEventData e)
    {
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";

        if (_machine == null) return;

        var handler = InventoryDragHandler.Instance;
        if (handler == null || !handler.IsDragging) return;

        var draggedSlot = handler.DraggedSlot;
        if (draggedSlot == null || draggedSlot.IsEmpty) return;

        var cfg = FuelConfig.Instance;
        if (cfg == null) { Debug.LogWarning("[FuelDropSlot] FuelConfig 없음."); return; }

        int itemId = draggedSlot.SlotData.itemId;
        if (itemId != cfg.fuelItemId) return;

        int amount = draggedSlot.SlotData.amount;
        var inv    = _inventory != null ? _inventory : InventoryManager.Instance;
        if (inv == null || !inv.TryConsumeItem(itemId, amount)) return;

        inv.ForceRefreshUI();
        _machine.AddFuel(amount);
        RefreshTime();
    }

    // ── 더블클릭 — 전량 회수 ────────────────────────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (_machine == null || _machine.FuelItemCount <= 0) return;

        float now = Time.unscaledTime;
        if (now - _lastClickTime < DoubleClickThreshold)
        {
            _lastClickTime = -1f;
            ReturnFuelToInventory();
        }
        else
        {
            _lastClickTime = now;
        }
    }

    // ── 드래그 아웃 (연료슬롯 → 인벤토리) ──────────────────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (_machine == null || _machine.FuelItemCount <= 0)
        {
            e.pointerDrag = null;
            return;
        }

        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        var cfg = FuelConfig.Instance;
        if (cfg == null) { e.pointerDrag = null; return; }

        IsFuelDragging = true;
        DragMachine    = _machine;
        DragInventory  = _inventory;
        DragFuelCount  = _machine.FuelItemCount;
        DragFuelItemId = cfg.fuelItemId;

        // 드래그 고스트 이미지
        _dragVisual = new GameObject("FuelDragVisual");
        _dragVisual.transform.SetParent(_canvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        var rt  = _dragVisual.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64f, 64f);

        var img = _dragVisual.AddComponent<Image>();
        img.sprite       = iconImage != null ? iconImage.sprite : null;
        img.color        = Color.white;
        img.raycastTarget = false;

        var cg = _dragVisual.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha          = 0.85f;
    }

    public void OnDrag(PointerEventData e)
    {
        // 우클릭으로 드래그 취소 시 고스트 강제 정리
        if (Input.GetMouseButton(1)) { OnEndDrag(e); return; }

        if (_dragVisual == null || _canvas == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            e.position,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out Vector2 localPos))
        {
            _dragVisual.transform.localPosition = localPos;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        IsFuelDragging = false;
        if (_dragVisual != null) { Destroy(_dragVisual); _dragVisual = null; }
    }

    // ── 내부 회수 메서드 ─────────────────────────────────────────────────

    private void ReturnFuelToInventory()
    {
        if (_machine == null) return;

        var cfg = FuelConfig.Instance;
        if (cfg == null) return;

        int count = _machine.TakeFuel();
        if (count <= 0) return;

        var inv = _inventory != null ? _inventory : InventoryManager.Instance;
        inv?.AddItem(cfg.fuelItemId, count);
        inv?.ForceRefreshUI();

        RefreshTime();
    }
}
