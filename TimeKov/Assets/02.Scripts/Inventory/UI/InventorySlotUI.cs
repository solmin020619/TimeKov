// InventorySlotUI.cs
// 슬롯 프리팹에 붙이는 스크립트
// 클릭, 우클릭, 호버, 드래그앤드롭 이벤트 처리

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("시각 요소")]
    [SerializeField] private Image bgImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject newBadge;

    [Header("색상 설정")]
    [SerializeField] private Color normalColor = new Color(0.18f, 0.22f, 0.30f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.30f, 0.55f, 0.80f, 1f);
    [SerializeField] private Color emptyBorderColor = new Color(0.3f, 0.3f, 0.3f, 0f);
    [SerializeField] private Color dragColor = new Color(1f, 1f, 1f, 0.4f);

    // 등급별 테두리 색상 (Common / Advanced / Rare / Hero / Legend 순서)
    private static readonly Color[] GradeColors = new Color[]
    {
        new Color(0.60f, 0.60f, 0.60f, 1f),  // Common   - 회색
        new Color(0.30f, 0.55f, 0.90f, 1f),  // Advanced - 파랑
        new Color(0.20f, 0.75f, 0.40f, 1f),  // Rare     - 초록
        new Color(0.65f, 0.30f, 0.90f, 1f),  // Hero     - 보라
        new Color(0.95f, 0.55f, 0.10f, 1f),  // Legend   - 주황
    };

    private InventorySlot _slot;
    private InventoryManager _owner;
    private bool _isSelected;

    // 전역 이벤트 (InventoryUIController 에서 구독)
    public static event Action<InventorySlotUI> OnAnySlotClicked;
    public static event Action<InventorySlotUI> OnAnySlotDoubleClicked;
    public static event Action<InventorySlotUI> OnAnySlotRightClicked;
    public static event Action<InventorySlotUI> OnAnySlotHoverEnter;
    public static event Action<InventorySlotUI> OnAnySlotHoverExit;

    public InventorySlot SlotData => _slot;
    public InventoryManager Owner => _owner;
    public bool IsEmpty => _slot == null || _slot.IsEmpty;

    // 슬롯 데이터 바인딩 및 시각 갱신
    public void Refresh(InventorySlot slot, InventoryManager owner)
    {
        _slot = slot;
        _owner = owner;

        if (slot == null || slot.IsEmpty)
        {
            SetEmpty();
            // 빈 슬롯이 되면 선택 상태도 해제
            _isSelected = false;
            if (bgImage != null) bgImage.color = normalColor;
            return;
        }

        var data = ItemDatabase.GetItem(slot.itemId);

        // 아이콘 설정
        if (itemIcon != null)
        {
            itemIcon.enabled = true;
            Sprite icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            itemIcon.sprite = icon;
            itemIcon.color = icon != null ? Color.white : new Color(1, 1, 1, 0.3f);
        }

        // 등급 테두리 색상
        if (rarityBorder != null)
        {
            int gradeIndex = data != null ? (int)data.itemGrade : 0;
            gradeIndex = Mathf.Clamp(gradeIndex, 0, GradeColors.Length - 1);
            rarityBorder.color = GradeColors[gradeIndex];
        }

        // 수량 텍스트 (1개면 숨김)
        if (countText != null)
        {
            countText.gameObject.SetActive(slot.amount > 1);
            countText.text = slot.amount.ToString();
        }

        // NEW 뱃지
        if (newBadge != null)
            newBadge.SetActive(slot.isNew);
    }

    // 빈 슬롯 시각 초기화
    private void SetEmpty()
    {
        if (itemIcon != null) itemIcon.enabled = false;
        if (rarityBorder != null) rarityBorder.color = emptyBorderColor;
        if (countText != null) countText.gameObject.SetActive(false);
        if (newBadge != null) newBadge.SetActive(false);
    }

    // 선택 상태 토글
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (bgImage != null)
            bgImage.color = selected ? selectedColor : normalColor;
    }

    // 클릭 이벤트 (좌클릭 단클릭 / 더블클릭 / 우클릭 분기)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsEmpty) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount >= 2)
            {
                // 더블클릭: 다른 인벤토리로 빠른 이동
                OnAnySlotDoubleClicked?.Invoke(this);
            }
            else
            {
                // 단일 클릭: 슬롯 선택
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

    // 호버 진입 (툴팁 표시)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsEmpty)
            OnAnySlotHoverEnter?.Invoke(this);
    }

    // 호버 이탈 (툴팁 숨김)
    public void OnPointerExit(PointerEventData eventData)
    {
        OnAnySlotHoverExit?.Invoke(this);
    }

    // 드래그 시작
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;
        InventoryDragHandler.Instance?.BeginDrag(this);
    }

    // 드래그 중 고스트 위치 갱신
    public void OnDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.UpdateDragPosition(eventData.position);
    }

    // 드래그 종료
    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.EndDrag();
    }

    // 다른 슬롯에서 드롭 받기
    public void OnDrop(PointerEventData eventData)
    {
        InventoryDragHandler.Instance?.HandleDrop(this);
    }
}