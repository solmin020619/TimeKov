// InventorySlotUI.cs
// ���� �����տ� ���̴� ��ũ��Ʈ
// Ŭ��, ��Ŭ��, ȣ��, �巡�׾ص�� �̺�Ʈ ó��

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("�ð� ���")]
    [SerializeField] private Image bgImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject newBadge;

    [Header("���� ����")]
    [SerializeField] private Color normalColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.55f, 0.80f, 1f);
    [SerializeField] private Color emptyBorderColor = new Color(0.3f, 0.3f, 0.3f, 0f);
    [SerializeField] private Color dragColor = new Color(1f, 1f, 1f, 0.4f);
    // 호버 시 슬롯에 "불 들어오는" 강조색 (선택색보다 밝게)
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.62f, 0.95f, 0.5f);

    // ��޺� �׵θ� ���� (Common / Advanced / Rare / Hero / Legend ����)
    private static readonly Color[] GradeColors = new Color[]
    {
        new Color(0.60f, 0.60f, 0.60f, 0f),    // Common   - 투명
        new Color(0.30f, 0.55f, 0.90f, 0.5f),  // Advanced - 파랑
        new Color(0.20f, 0.75f, 0.40f, 0.5f),  // Rare     - 초록
        new Color(0.65f, 0.30f, 0.90f, 0.5f),  // Hero     - 보라
        new Color(0.95f, 0.55f, 0.10f, 0.5f),  // Legend   - 황금
    };

    private InventorySlot _slot;
    private InventoryManager _owner;
    private bool _isSelected;
    private bool _isHovered;

    // ���� �̺�Ʈ (InventoryUIController ���� ����)
    public static event Action<InventorySlotUI> OnAnySlotClicked;
    public static event Action<InventorySlotUI> OnAnySlotDoubleClicked;
    public static event Action<InventorySlotUI> OnAnySlotRightClicked;
    public static event Action<InventorySlotUI> OnAnySlotHoverEnter;
    public static event Action<InventorySlotUI> OnAnySlotHoverExit;
    public static event Action<InventorySlotUI> OnAnySlotDragBegin;  // 드래그 시작
    public static event Action<InventorySlotUI> OnAnySlotDragEnd;    // 드래그 종료
    public static event Action<InventorySlotUI> OnAnySlotDropped;    // 드랍 수신

    public InventorySlot SlotData => _slot;
    public InventoryManager Owner => _owner;
    public bool IsEmpty => _slot == null || _slot.IsEmpty;

    private void OnDisable()
    {
        // 비활성화 시 Unity가 OnPointerExit를 호출하지 않으므로 호버 상태를 직접 리셋한다.
        // (안 하면 패널을 닫았다 다시 열 때 마우스가 없던 칸이 강조된 채로 남는다)
        _isHovered = false;
    }

    // ���� ������ ���ε� �� �ð� ����
    public void Refresh(InventorySlot slot, InventoryManager owner)
    {
        _slot = slot;
        _owner = owner;

        if (slot == null || slot.IsEmpty)
        {
            SetEmpty();
            // �� ������ �Ǹ� ���� ���µ� ����
            _isSelected = false;
            UpdateBgVisual();
            return;
        }

        // 호버/선택 상태를 반영해 배경색 적용 (리프레시 중에도 강조 유지)
        UpdateBgVisual();

        var data = ItemDatabase.GetItem(slot.itemId);

        // ������ ����
        if (itemIcon != null)
        {
            itemIcon.enabled = true;
            Sprite icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            itemIcon.sprite = icon;
            itemIcon.color = icon != null ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        // ��� �׵θ� ����
        if (rarityBorder != null)
        {
            int gradeIndex = data != null ? (int)data.itemGrade : 0;
            gradeIndex = Mathf.Clamp(gradeIndex, 0, GradeColors.Length - 1);
            rarityBorder.color = GradeColors[gradeIndex];
        }

        // ���� �ؽ�Ʈ (1���� ����)
        if (countText != null)
        {
            countText.gameObject.SetActive(slot.amount > 1);
            countText.text = slot.amount.ToString();
        }

        // NEW ����
        if (newBadge != null)
            newBadge.SetActive(slot.isNew);
    }

    // �� ���� �ð� �ʱ�ȭ
    private void SetEmpty()
    {
        if (itemIcon != null) itemIcon.enabled = false;
        if (rarityBorder != null) rarityBorder.color = emptyBorderColor;
        if (countText != null) countText.gameObject.SetActive(false);
        if (newBadge != null) newBadge.SetActive(false);
    }

    // ���� ���� ���
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateBgVisual();
    }

    // 선택 > 호버 > 기본 우선순위로 배경색 결정
    private void UpdateBgVisual()
    {
        if (bgImage == null) return;
        bgImage.color = _isSelected ? selectedColor
                      : (_isHovered ? hoverColor : normalColor);
    }

    // Ŭ�� �̺�Ʈ (��Ŭ�� ��Ŭ�� / ����Ŭ�� / ��Ŭ�� �б�)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsEmpty) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount >= 2)
            {
                // ����Ŭ��: �ٸ� �κ��丮�� ���� �̵�
                OnAnySlotDoubleClicked?.Invoke(this);
            }
            else
            {
                // ���� Ŭ��: ���� ����
                OnAnySlotClicked?.Invoke(this);
                if (_slot != null && _slot.isNew)
                {
                    _owner?.ClearNewFlag(_slot.slotIndex);
                    if (newBadge != null) newBadge.SetActive(false);
                }
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnAnySlotRightClicked?.Invoke(this);
        }
    }

    // ȣ�� ���� (���� ǥ�� + ���� ����)
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateBgVisual();
        if (!IsEmpty)
            OnAnySlotHoverEnter?.Invoke(this);
    }

    // ȣ�� ��Ż (���� ���� + ���� ����)
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateBgVisual();
        OnAnySlotHoverExit?.Invoke(this);
    }

    // 드래그 시작
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        // ALT + 좌클릭 드래그 → 절반 수량만 분할해서 들기
        // (1개짜리는 ALT 눌러도 그냥 1개 드래그)
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (altHeld && eventData.button == PointerEventData.InputButton.Left
                    && _slot != null && _slot.amount >= 2)
        {
            int half = _slot.amount / 2;
            OnAnySlotDragBegin?.Invoke(this);
            InventoryDragHandler.Instance?.BeginDrag(this, half);
            return;
        }

        OnAnySlotDragBegin?.Invoke(this);
        InventoryDragHandler.Instance?.BeginDrag(this);
    }

    // �巡�� �� ����Ʈ ��ġ ����
    public void OnDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.UpdateDragPosition(eventData.position);
    }

    // �巡�� ����
    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.EndDrag();
        OnAnySlotDragEnd?.Invoke(this);
    }

    // 다른 슬롯에서 드랍 받기
    public void OnDrop(PointerEventData eventData)
    {
        // output 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (TIMEKOV.Factory.MachineSlotWidget.IsOutputDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptOutputDrop();
            return;
        }

        // recipe(input) 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (RecipeDropSlot.IsRecipeDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptRecipeDrop();
            return;
        }

        // 연료 슬롯 드래그 → 부모 계층의 InventoryPanelDropZone에 위임
        if (FuelDropSlot.IsFuelDragging)
        {
            GetComponentInParent<InventoryPanelDropZone>()?.AcceptFuelDrop();
            return;
        }

        OnAnySlotDropped?.Invoke(this);
        InventoryDragHandler.Instance?.HandleDrop(this);
    }
}