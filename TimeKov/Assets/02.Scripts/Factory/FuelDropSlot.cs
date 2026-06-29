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
    [SerializeField] private Image           fuelGauge;  // 현재 연료 1개분 남은 연소 비율 바(#41)

    [Header("Hover 색상")]
    [SerializeField] private Color normalBorderColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hoverBorderColor  = new Color(1f, 0.8f, 0.2f, 0.8f);
    [SerializeField] private Color noFuelTextColor   = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color normalTextColor   = new Color(1f, 1f, 1f, 1f);

    [Header("빈 슬롯 미리보기")]
    [Tooltip("연료가 없을 때 연료 아이템을 검은색 실루엣으로 미리 표시하는 색상.")]
    [SerializeField] private Color emptySilhouetteColor = new Color(0f, 0f, 0f, 1f);

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
    private bool              _dragHighlighted; // 인벤 드래그 시작으로 강조 중인지

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

        float t    = _machine.FuelTimeRemaining;
        var   cfg  = FuelConfig.Instance;
        float secs = cfg != null ? cfg.secondsPerFuel : 40f;

        // 현재 연소 중인 1개를 제외한 대기 중 아이템 수
        // CeilToInt(t/secs) = 전체 아이템 수 → -1 = 대기 중인 것만
        int queued = t > 0f ? Mathf.Max(0, Mathf.CeilToInt(t / secs) - 1) : 0;

        // 현재 아이템의 남은 연소 시간 (0 ~ secondsPerFuel 범위)
        // t가 정확히 secs의 배수일 때 % 결과가 0이 되므로 secs로 보정
        float currentTime = t % secs;
        if (currentTime < 0.01f && t > 0f) currentTime = secs;

        // 대기 중인 아이템만 개수 표시 (현재 연소 중인 1개는 표시 안 함)
        if (amountText != null)
            amountText.text = queued > 0 ? $"x{queued}" : "";

        if (timeText != null)
        {
            if (t > 0f)
            {
                // 현재 아이템 1개분의 남은 시간만 표시
                timeText.text  = $"{currentTime:F0}초";
                timeText.color = normalTextColor;
                timeText.enabled = true;
            }
            else
            {
                // 연료 없음은 MachineUI.statusText가 "연료 부족"으로 표시 -> 여기선 숨김(겹침 방지)
                timeText.text  = "";
                timeText.enabled = false;
            }
        }

        // 연료 게이지(#41) = 현재 타고 있는 1개의 남은 연소 비율(0~1).
        if (fuelGauge != null)
        {
            fuelGauge.fillAmount = t > 0f ? Mathf.Clamp01(currentTime / secs) : 0f;
            fuelGauge.enabled = t > 0f;
        }

        // 연료 상태 변경 시 아이콘도 갱신
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (iconImage == null) return;

        var cfg = FuelConfig.Instance;

        // 연료 아이템 아이콘 미리 로드 (빈 슬롯 실루엣 표시용으로도 사용)
        Sprite sprite = null;
        if (cfg != null)
        {
            var itemData = GameDataUtility.GetItem(cfg.fuelItemId);
            sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
        }

        // 연료가 없으면 연료 아이템을 검은색 실루엣으로 미리 표시 (연료가 들어가면 원래 아이콘)
        if (cfg == null || _machine == null || _machine.FuelTimeRemaining <= 0f)
        {
            if (sprite != null)
            {
                iconImage.sprite  = sprite;
                iconImage.color   = emptySilhouetteColor;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
            return;
        }

        // 현재 연소 중인 1개를 제외한 대기 아이템 수
        // queued == 0 이면 마지막 1개가 타는 중 → 아이콘 숨김, 시간만 표시
        float t      = _machine.FuelTimeRemaining;
        float secs   = cfg.secondsPerFuel;
        int   queued = Mathf.Max(0, Mathf.CeilToInt(t / secs) - 1);

        if (queued <= 0)
        {
            iconImage.enabled = false;
            return;
        }

        iconImage.sprite  = sprite;
        iconImage.color   = Color.white;
        iconImage.enabled = sprite != null;
    }

    // ── 드래그 시작 강조 (MachineUI가 인벤 드래그 시작 시 호출) ──────────

    /// <summary>이 슬롯이 받는 아이템 ID (연료).</summary>
    public int AcceptedItemId => FuelConfig.Instance != null ? FuelConfig.Instance.fuelItemId : -1;

    /// <summary>"연료 넣기" 프롬프트(호버/드래그 강조)가 떠 있는지 = labelText 비어있지 않음.
    /// MachineUI가 같은 자리의 "연료 부족" 경고와 겹치지 않게 조회한다.</summary>
    public bool IsInsertPromptVisible => labelText != null && !string.IsNullOrEmpty(labelText.text);

    /// <summary>인벤토리에서 연료를 집어든 순간 드랍 대상임을 미리 강조한다.</summary>
    public void SetDragHighlight(bool on)
    {
        _dragHighlighted = on;
        if (borderImage != null) borderImage.color = on ? hoverBorderColor : normalBorderColor;
        if (labelText   != null) labelText.text    = on ? "연료 넣기" : "";
    }

    // ── Hover (인벤토리 → 연료슬롯 방향) ────────────────────────────────

    public void OnPointerEnter(PointerEventData e)
    {
        // 아이템 정보 툴팁 (인벤토리/창고와 동일) — 연료가 들어있을 때만
        var fcfg = FuelConfig.Instance;
        if (fcfg != null && _machine != null && _machine.FuelItemCount > 0)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            ItemTooltipUI.Instance?.Show(fcfg.fuelItemId, _canvas);
        }

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
        ItemTooltipUI.Instance?.Hide();

        // 드래그 강조 중이면 커서가 벗어나도 강조 유지
        if (_dragHighlighted)
        {
            if (borderImage != null) borderImage.color = hoverBorderColor;
            if (labelText   != null) labelText.text    = "연료 넣기";
            return;
        }
        if (borderImage != null) borderImage.color = normalBorderColor;
        if (labelText   != null) labelText.text    = "";
    }

    // ── Drop (인벤토리 → 연료슬롯) ───────────────────────────────────────

    public void OnDrop(PointerEventData e)
    {
        _dragHighlighted = false;
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
        if (itemId != cfg.fuelItemId) { ToastManager.Warning("연료만 넣을 수 있습니다"); return; }

        int amount = draggedSlot.SlotData.amount;
        if (amount <= 0) return;

        // 같은 아이템이 여러 칸에 나뉘어 있어도 실제로 드래그한 칸에서만 차감
        var inv = draggedSlot.Owner != null ? draggedSlot.Owner
                : (_inventory != null ? _inventory : InventoryManager.Instance);
        if (inv == null || !inv.RemoveFromSlot(draggedSlot.SlotData.slotIndex, amount)) return;

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

        // 회수로 슬롯이 비었으므로 호버 툴팁 즉시 숨김 (마우스가 그대로면 Exit가 안 뜸)
        ItemTooltipUI.Instance?.Hide();

        RefreshTime();
    }
}
