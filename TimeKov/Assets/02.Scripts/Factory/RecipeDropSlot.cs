using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using TIMEKOV.Factory;

[RequireComponent(typeof(Image))]
public class RecipeDropSlot : MonoBehaviour,
    IDropHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;   // 드래그 hover glow 전용
    [SerializeField] private Image rarityBorder;  // 등급 테두리 색상 전용
    [SerializeField] private Image gradeAurora;   // 등급 오로라(하단 그라데이션, 인벤 슬롯과 동일). 등급색 틴트.
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI labelText;

    // 등급 색상은 공용 GradeVisual 로 이동(중앙화).

    public int RequiredItemId { get; private set; }
    public int RequiredAmount { get; private set; }
    public int CurrentAmount { get; private set; }

    private ProcessingMachine _machine;
    private InventoryManager _inventory;
    private Coroutine _glowRoutine;
    private bool _dragHighlighted; // 인벤 드래그 시작으로 강조 중인지
    /// <summary>이 슬롯이 속한 레시피 인덱스. 재료를 넣을 때 SetLockedRecipe에 사용.</summary>
    private int _recipeIndex = -1;

    // 더블클릭 감지
    private float _lastClickTime = -1f;
    private const float DoubleClickThreshold = 0.3f;

    // ── 드래그 아웃 static 상태 ─────────────────────────────
    // input 슬롯에서 인벤토리로 드래그할 때 InventoryPanelDropZone이 읽어감
    public static bool IsRecipeDragging { get; private set; }
    public static int DragItemId { get; private set; }
    public static int DragAmount { get; private set; }
    public static ProcessingMachine DragMachine { get; private set; }
    public static InventoryManager DragInventory { get; private set; }

    private static GameObject _dragVisual;
    private Canvas _canvas;

    private void Awake()
    {
        GetComponent<Image>().raycastTarget = true;
        _canvas = GetComponentInParent<Canvas>();

        // 첫 활성화 시 기본 상태 숨김 — "재료 넣기" 텍스트·흰 박스 깜빡임 방지
        if (labelText != null) labelText.text = "";
        SetBorderAlpha(0f);

        if (borderImage != null)
        {
            var le = borderImage.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = borderImage.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    private void OnEnable()
    {
        // 활성화될 때마다 기본 상태 초기화
        // (패널 닫기 전 OnPointerEnter로 "재료 넣기" 텍스트가 남아있는 경우도 처리)
        _dragHighlighted = false;
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
    }

    private void OnDisable()
    {
        IsRecipeDragging = false;
        if (_dragVisual != null)
        {
            Destroy(_dragVisual);
            _dragVisual = null;
        }
    }

    public void Setup(int itemId, int amount, ProcessingMachine machine, InventoryManager inventory = null, int recipeIndex = -1)
    {
        var body0 = GetComponent<Image>(); if (body0 != null) body0.raycastTarget = true;   // 빈 포트로 쓰였다 재사용 시 상호작용 복구
        RequiredItemId = itemId;
        RequiredAmount = amount;
        CurrentAmount = 0;
        _machine = machine;
        _inventory = inventory != null ? inventory : InventoryManager.Instance;
        _recipeIndex = recipeIndex;

        if (labelText != null) labelText.text = "";
        SetBorderAlpha(0f);

        // 필요 아이템 아이콘과 등급 테두리를 항상 표시
        var itemData = GameDataUtility.GetItem(itemId);
        if (iconImage != null)
        {
            Sprite sprite = itemData != null ? ItemDatabase.GetIcon(itemData.iconKey) : null;
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            iconImage.enabled = true;
        }
        if (rarityBorder != null)
        {
            if (itemData != null)
            {
                rarityBorder.color = GradeVisual.GetColor((int)itemData.itemGrade);
            }
            else
            {
                rarityBorder.color = new Color(0f, 0f, 0f, 0f);
            }
        }
        // 등급 오로라(하단 글로우) - 인벤 슬롯과 동일하게 등급색 틴트.
        if (gradeAurora != null)
        {
            if (itemData != null)
            {
                Color gc = GradeVisual.GetColor((int)itemData.itemGrade);
                gradeAurora.color = new Color(gc.r, gc.g, gc.b, gc.a * 0.6f);
            }
            else
            {
                gradeAurora.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        PublicRefresh();
    }

    /// <summary>빈 입력 포트(레시피 재료 없음) = 아이콘/수량 없이 포트 자리만. 드롭 비활성(벨트 연결구 표시용).
    /// 설비 입력 포트 수(inputSlotCount) > 레시피 재료수 일 때 남는 칸에 쓰인다.</summary>
    public void SetupEmptyPort()
    {
        RequiredItemId = 0; RequiredAmount = 0; CurrentAmount = 0;
        if (labelText != null) labelText.text = "";
        if (amountText != null) amountText.text = "";
        if (iconImage != null) iconImage.enabled = false;
        if (rarityBorder != null) rarityBorder.color = new Color(0f, 0f, 0f, 0f);
        if (gradeAurora != null) gradeAurora.color = new Color(0f, 0f, 0f, 0f);
        SetBorderAlpha(0f);
        var body = GetComponent<Image>(); if (body != null) body.raycastTarget = false;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        // 아이템 정보 툴팁 (인벤토리/창고와 동일) — 요구 재료 기준
        if (RequiredItemId > 0)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            ItemTooltipUI.Instance?.Show(RequiredItemId, _canvas);
        }

        // 활성 아닌 레시피 미리보기 슬롯이면 "재료 넣기" 유도 안 함(투입 막혀있어 착오 방지). 툴팁은 위에서 표시함.
        if (IsSuppressed()) return;

        // 인벤토리 → 재료 슬롯 드래그일 때만 "재료 넣기" 표시
        bool isDragging = InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging;
        if (!isDragging) return;
        if (labelText != null) labelText.text = Loc.Get("재료 넣기");
        StartGlow();
    }

    public void OnPointerExit(PointerEventData e)
    {
        ItemTooltipUI.Instance?.Hide();

        // 드래그 강조 중이면 커서가 벗어나도 강조 유지
        if (_dragHighlighted) return;
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
    }

    /// <summary>인벤토리에서 해당 재료를 집어든 순간 드랍 대상임을 미리 강조한다.</summary>
    public void SetDragHighlight(bool on)
    {
        if (on && IsSuppressed()) on = false;   // 활성 아닌 레시피 슬롯은 드롭 대상 강조 안 함(투입 막힘)
        _dragHighlighted = on;
        if (on)
        {
            if (labelText != null) labelText.text = Loc.Get("재료 넣기");
            StartGlow();
        }
        else
        {
            if (labelText != null) labelText.text = "";
            StopGlow();
            SetBorderAlpha(0f);
        }
    }

    // ── 드래그 아웃 (재료 슬롯 → 인벤토리) ─────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (_machine == null) return;

        // 활성 아닌 레시피 미리보기에선 버퍼 조작 불가(그 재료는 활성 레시피 것 - 착오로 빼가는 것 방지)
        if (IsSuppressed()) { e.pointerDrag = null; return; }

        int buffered = _machine.InputBuffer.GetAmount(RequiredItemId);
        if (buffered <= 0)
        {
            // 버퍼에 아이템 없으면 드래그 취소
            e.pointerDrag = null;
            return;
        }

        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        IsRecipeDragging = true;
        DragItemId  = RequiredItemId;
        DragAmount  = buffered;
        DragMachine = _machine;
        DragInventory = _inventory;

        // 드래그 고스트 생성
        _dragVisual = new GameObject("RecipeDragVisual");
        _dragVisual.transform.SetParent(_canvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        var rt = _dragVisual.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64f, 64f);

        var img = _dragVisual.AddComponent<Image>();
        img.sprite = iconImage != null ? iconImage.sprite : null;
        img.color  = Color.white;
        img.raycastTarget = false;

        var cg = _dragVisual.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha = 0.85f;
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
        IsRecipeDragging = false;
        if (_dragVisual != null)
        {
            Destroy(_dragVisual);
            _dragVisual = null;
        }
    }

    // ── 더블클릭 (재료 슬롯 → 인벤토리) ────────────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (_machine == null) return;
        if (IsSuppressed()) return;   // 활성 아닌 레시피 = 더블클릭 회수 불가(활성 레시피 재료라 착오 방지)

        float now = Time.unscaledTime;
        if (now - _lastClickTime < DoubleClickThreshold)
        {
            // 더블클릭 확정 — InputBuffer에서 해당 재료를 인벤토리로 이동
            _lastClickTime = -1f;

            int buffered = _machine.InputBuffer.GetAmount(RequiredItemId);
            if (buffered <= 0) return;

            // 먼저 넣어보고 들어간 만큼만 차감 - 가방 가득 시 초과분 증발 방지(남는 건 설비 유지).
            var recInv = _inventory != null ? _inventory : InventoryManager.Instance;
            int leftover = recInv != null ? recInv.AddItem(RequiredItemId, buffered) : buffered;
            int taken = buffered - leftover;
            if (taken > 0) _machine.InputBuffer.Consume(RequiredItemId, taken);
            if (leftover > 0) ToastManager.Warning(Loc.Get("가방이 가득 찼습니다"));

            _machine.PublicNotifyBufferChanged();
        }
        else
        {
            _lastClickTime = now;
        }
    }

    public void OnDrop(PointerEventData e)
    {
        _dragHighlighted = false;
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
        if (_machine == null) return;

        // InventoryDragHandler에서 드래그된 InventorySlotUI 슬롯 가져오기
        var handler = InventoryDragHandler.Instance;
        if (handler == null || !handler.IsDragging) return;

        var draggedSlot = handler.DraggedSlot;
        if (draggedSlot == null || draggedSlot.IsEmpty) return;

        // 라이브 시각 칸 대신 박제한 출발 슬롯 사용(컴팩트 표시에서 드래그 중 재렌더로 엉뚱한 재료 차감 방지).
        if (!handler.SourceStillValid()) return;
        int itemId    = handler.SrcItemId;
        int srcIndex  = handler.SrcSlotIndex;
        var sourceInv = handler.SrcManager;
        int have      = sourceInv.GetSlot(srcIndex).amount;
        int dragAmt   = handler.IsSplitDrag ? Mathf.Min(handler.DragAmount, have) : have;   // ALT 분할 드래그 = 든 수량만

        // 이미 다른 레시피에 재료가 들어있거나 가공 중이면 그 레시피만 재료를 받는다(설비당 한 레시피).
        //   공통재료 섞임/"B가 준비된 듯" 착시로 인한 오작동 방지 - 바꾸려면 재료 회수 먼저.
        if (IsSuppressed())
        {
            ToastManager.Warning(Loc.Get("이미 다른 레시피에 재료가 들어있습니다. 재료를 회수한 뒤 바꿔주세요."));
            return;
        }

        if (itemId != RequiredItemId) { ToastManager.Warning(Loc.Get("요구하는 재료와 다릅니다")); return; }
        if (dragAmt <= 0) return;

        // 같은 아이템이 여러 칸에 나뉘어 있어도 실제로 드래그한 칸에서만 차감
        int take = dragAmt;
        if (!sourceInv.RemoveFromSlot(srcIndex, take)) return;
        sourceInv.ForceRefreshUI();

        // 재료를 실제로 넣을 때 이 슬롯의 레시피로 생산 레시피를 고정
        if (_recipeIndex >= 0)
            _machine.SetLockedRecipe(_recipeIndex);

        _machine.Receive(itemId, take);

        CurrentAmount += take;
        RefreshAmount();
        // OnEndDrag → InventoryDragHandler.EndDrag() 가 자동 호출되므로 별도 호출 불필요
    }

    private void StartGlow()
    {
        StopGlow();
        _glowRoutine = StartCoroutine(GlowRoutine());
    }

    private void StopGlow()
    {
        if (_glowRoutine != null)
        {
            StopCoroutine(_glowRoutine);
            _glowRoutine = null;
        }
    }

    private IEnumerator GlowRoutine()
    {
        while (true)
        {
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime * 3f; SetBorderAlpha(Mathf.Lerp(0f, 1f, t)); yield return null; }
            t = 0f;
            while (t < 1f) { t += Time.deltaTime * 3f; SetBorderAlpha(Mathf.Lerp(1f, 0f, t)); yield return null; }
        }
    }

    private void SetBorderAlpha(float alpha)
    {
        if (borderImage == null) return;
        var c = borderImage.color;
        c.a = alpha;
        borderImage.color = c;
    }

    // 다른 레시피에 커밋된 상태(재료 있음/가공중)에서 이 슬롯이 그 레시피가 아니면 = 미리보기(비활성).
    //   공유 InputBuffer 수량을 표시/조작하지 않아 "A 재료로 B가 준비된 듯" 착시와 오작동을 막는다.
    private bool IsSuppressed()
        => _machine != null && _recipeIndex >= 0 && _machine.IsCommitted && _recipeIndex != _machine.EffectiveRecipeIndex;

    public void PublicRefresh()
    {
        if (_machine == null) return;

        int current = IsSuppressed() ? 0 : _machine.InputBuffer.GetAmount(RequiredItemId);
        CurrentAmount = current;
        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";
        ApplyLoadedVisual(current > 0);
    }

    private void RefreshAmount()
    {
        int current = IsSuppressed() ? 0
            : (_machine != null ? _machine.InputBuffer.GetAmount(RequiredItemId) : CurrentAmount);

        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";
        ApplyLoadedVisual(current > 0);
    }

    // 재료 로드 여부로 구분: 안 들어옴 = 유령 아이콘 + 발광 끔(뭘 넣는지 힌트만), 들어옴 = 선명 + 등급 테두리/오로라.
    private void ApplyLoadedVisual(bool loaded)
    {
        if (RequiredItemId <= 0) return;   // 빈 포트는 SetupEmptyPort 가 처리
        var itemData = GameDataUtility.GetItem(RequiredItemId);
        Color gc = itemData != null ? GradeVisual.GetColor((int)itemData.itemGrade) : Color.white;

        if (iconImage != null && iconImage.sprite != null)
            iconImage.color = loaded ? Color.white : new Color(1f, 1f, 1f, 0.26f);
        if (rarityBorder != null)
            rarityBorder.color = (loaded && itemData != null) ? gc : new Color(0f, 0f, 0f, 0f);
        if (gradeAurora != null)
            gradeAurora.color = (loaded && itemData != null)
                ? new Color(gc.r, gc.g, gc.b, gc.a * 0.6f)
                : new Color(0f, 0f, 0f, 0f);
    }
}