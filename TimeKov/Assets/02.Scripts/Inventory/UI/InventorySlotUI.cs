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
    [SerializeField] private GameObject countChip;   // 우상단 수량 칩 배경 (수량 2 이상일 때만 표시)
    [SerializeField] private GameObject iconBacking; // 아이콘 뒤 어두운 backing (아이템 있을 때만 표시)

    [Header("���� ����")]
    [SerializeField] private Color normalColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    [SerializeField] private Color emptyBorderColor = new Color(0.3f, 0.3f, 0.3f, 0f);
    [SerializeField] private Color dragColor = new Color(1f, 1f, 1f, 0.4f);
    // 호버 시 슬롯에 "불 들어오는" 강조색 (선택색보다 밝게)
    [SerializeField] private Color hoverColor = new Color(0.45f, 0.62f, 0.95f, 0.5f);
    [SerializeField] private Color normalBorderColor = new Color(0.627f, 0.745f, 0.855f, 0.34f);  // 평소 테두리(크롬)
    [SerializeField] private Color hoverBorderColor  = new Color(0.37f, 0.77f, 1f, 0.9f);          // 호버 테두리(시안)

    // ��޺� �׵θ� ���� (Common / Advanced / Rare / Hero / Legend ����)
    private static readonly Color[] GradeColors = new Color[]
    {
        new Color(0.60f, 0.60f, 0.60f, 0f),    // Common   - 없음
        new Color(0.31f, 0.61f, 0.88f, 1f),    // Advanced - 파랑 4f9be0
        new Color(0.20f, 0.75f, 0.42f, 1f),    // Rare     - 초록 34c06a
        new Color(0.65f, 0.31f, 0.88f, 1f),    // Hero     - 보라 a64fe0
        new Color(1.00f, 0.69f, 0.13f, 1f),    // Legend   - 골드 ffb020
    };

    private InventorySlot _slot;
    private InventoryManager _owner;
    private bool _isHovered;
    private UnityEngine.UI.Outline _outline;   // 호버 시 테두리 시안 전환용 (루트 Outline)

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
            UpdateBgVisual();
            return;
        }

        // 호버/선택 상태를 반영해 배경색 적용 (리프레시 중에도 강조 유지)
        UpdateBgVisual();

        if (iconBacking != null) iconBacking.SetActive(true);   // 아이템 있을 때만 backing

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
        if (countChip != null) countChip.SetActive(slot.amount > 1);

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
        if (countChip != null) countChip.SetActive(false);
        if (iconBacking != null) iconBacking.SetActive(false);
        if (newBadge != null) newBadge.SetActive(false);
    }

    // ���� ���� ���
    // 호버일 때만 배경 강조 (클릭 선택 강조는 제거함 - 호버 전용)
    private void UpdateBgVisual()
    {
        if (bgImage != null)
            bgImage.color = _isHovered ? hoverColor : normalColor;

        // 호버 시 테두리 시안 (글로우 느낌). 루트 Outline 색 전환.
        if (_outline == null) _outline = GetComponent<UnityEngine.UI.Outline>();
        if (_outline != null)
            _outline.effectColor = _isHovered ? hoverBorderColor : normalBorderColor;
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
        if (IsEmpty) return;   // 빈 칸은 호버 강조/툴팁 없음 (빈 칸 선택돼 보이던 버그)
        _isHovered = true;
        UpdateBgVisual();
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