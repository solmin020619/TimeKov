using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using TIMEKOV.Factory;

[RequireComponent(typeof(Image))]
public class RecipeDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image borderImage;   // 드래그 hover glow 전용
    [SerializeField] private Image rarityBorder;  // 등급 테두리 색상 전용
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI labelText;

    // InventorySlotUI 와 동일한 등급 색상 배열
    private static readonly Color[] GradeColors = new Color[]
    {
        new Color(0.60f, 0.60f, 0.60f, 1f),  // Common   - 회색
        new Color(0.30f, 0.55f, 0.90f, 1f),  // Advanced - 파랑
        new Color(0.20f, 0.75f, 0.40f, 1f),  // Rare     - 초록
        new Color(0.65f, 0.30f, 0.90f, 1f),  // Hero     - 보라
        new Color(0.95f, 0.55f, 0.10f, 1f),  // Legend   - 황금
    };

    public int RequiredItemId { get; private set; }
    public int RequiredAmount { get; private set; }
    public int CurrentAmount { get; private set; }

    private ProcessingMachine _machine;
    private InventoryManager _inventory;
    private Coroutine _glowRoutine;

    private void Awake()
    {
        GetComponent<Image>().raycastTarget = true;

        if (borderImage != null)
        {
            var le = borderImage.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = borderImage.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    public void Setup(int itemId, int amount, ProcessingMachine machine, InventoryManager inventory = null)
    {
        RequiredItemId = itemId;
        RequiredAmount = amount;
        CurrentAmount = 0;
        _machine = machine;
        _inventory = inventory != null ? inventory : InventoryManager.Instance;

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
                int gradeIndex = Mathf.Clamp((int)itemData.itemGrade, 0, GradeColors.Length - 1);
                rarityBorder.color = GradeColors[gradeIndex];
            }
            else
            {
                rarityBorder.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        PublicRefresh();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        // DraggableSlot → InventoryDragHandler 로 드래그 감지 전환
        bool isDragging = InventoryDragHandler.Instance != null && InventoryDragHandler.Instance.IsDragging;
        if (!isDragging) return;
        if (labelText != null) labelText.text = "재료 넣기";
        StartGlow();
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
    }

    public void OnDrop(PointerEventData e)
    {
        if (labelText != null) labelText.text = "";
        StopGlow();
        SetBorderAlpha(0f);
        if (_machine == null) return;

        // InventoryDragHandler에서 드래그된 InventorySlotUI 슬롯 가져오기
        var handler = InventoryDragHandler.Instance;
        if (handler == null || !handler.IsDragging) return;

        var draggedSlot = handler.DraggedSlot;
        if (draggedSlot == null || draggedSlot.IsEmpty) return;

        int itemId  = draggedSlot.SlotData.itemId;
        int dragAmt = draggedSlot.SlotData.amount;

        if (itemId != RequiredItemId) return;

        int have = _inventory != null ? _inventory.GetTotalItemCount(itemId) : dragAmt;
        if (have <= 0) return;

        int take = Mathf.Min(dragAmt, have);
        if (_inventory != null)
        {
            _inventory.TryConsumeItem(itemId, take);
            _inventory.ForceRefreshUI();
        }
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

    public void PublicRefresh()
    {
        if (_machine == null) return;

        int current = _machine.InputBuffer.GetAmount(RequiredItemId);
        CurrentAmount = current;
        // 아이콘은 Setup()에서 항상 표시되므로 수량 텍스트만 갱신
        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";
    }

    private void RefreshAmount()
    {
        int current = _machine != null
            ? _machine.InputBuffer.GetAmount(RequiredItemId)
            : CurrentAmount;

        if (amountText != null)
            amountText.text = $"{current}/{RequiredAmount}";
    }
}