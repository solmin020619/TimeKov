// ContextMenuUI.cs
// ContextMenu 오브젝트에 붙이는 스크립트
// 슬롯 우클릭 시 마우스 위치에 나타나는 팝업 메뉴

using UnityEngine;
using UnityEngine.UI;

public class ContextMenuUI : MonoBehaviour
{
    [Header("버튼 참조")]
    [SerializeField] private Button useBtn;
    [SerializeField] private Button splitBtn;
    [SerializeField] private Button trashBtn;

    private InventorySlotUI _currentSlot;
    private RectTransform _rect;
    private RectTransform _canvasRect;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvasRect = canvas.GetComponent<RectTransform>();

        gameObject.SetActive(false);
    }

    private void Start()
    {
        if (useBtn != null) useBtn.onClick.AddListener(OnClickUse);
        if (splitBtn != null) splitBtn.onClick.AddListener(OnClickSplit);
        if (trashBtn != null) trashBtn.onClick.AddListener(OnClickTrash);
    }

    // 메뉴 열기
    public void Open(InventorySlotUI slot, Vector2 screenPos)
    {
        if (slot == null || slot.IsEmpty) return;

        _currentSlot = slot;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        // 전술 소모품만 사용 버튼 활성화
        bool isConsumable = data != null && data.itemCategory == ItemCategory.TacticalConsumable;
        if (useBtn != null) useBtn.interactable = isConsumable;

        // 수량 2 이상이면 분할 가능
        if (splitBtn != null) splitBtn.interactable = slot.SlotData.amount > 1;

        gameObject.SetActive(true);
        SetPosition(screenPos);
    }

    // 메뉴 닫기
    public void Close()
    {
        _currentSlot = null;
        gameObject.SetActive(false);
    }

    // 화면 경계 내 위치 설정
    private void SetPosition(Vector2 screenPos)
    {
        if (_rect == null || _canvasRect == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out localPos);

        float halfW = _rect.sizeDelta.x * 0.5f;
        float halfH = _rect.sizeDelta.y * 0.5f;
        float maxX = _canvasRect.rect.width * 0.5f - halfW;
        float maxY = _canvasRect.rect.height * 0.5f - halfH;

        localPos.x = Mathf.Clamp(localPos.x, -maxX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, -maxY, maxY);

        _rect.anchoredPosition = localPos;
    }

    private void OnClickUse()
    {
        if (_currentSlot == null) return;

        var owner = _currentSlot.Owner;
        var slotData = _currentSlot.SlotData;

        // 수량 먼저 차감
        bool consumed = owner != null && owner.TryConsumeItem(slotData.itemId, 1);
        if (!consumed) { Close(); return; }

        // 효과 적용
        var player = FindAnyObjectByType<Player>();
        bool applied = ConsumableEffectApplier.Apply(slotData.itemId.ToString(), player);

        // 효과 적용 실패 시 수량 복구
        if (!applied)
            owner.AddItem(slotData.itemId, 1);

        Close();
    }

    private void OnClickSplit()
    {
        if (_currentSlot == null) return;
        InventoryUIController.Instance?.OpenSplitPopup(_currentSlot);
        Close();
    }

    private void OnClickTrash()
    {
        if (_currentSlot == null) return;
        InventoryUIController.Instance?.OpenTrashConfirm(_currentSlot);
        Close();
    }

    // 외부 클릭 감지 (InventoryUIController Update 에서 호출)
    public void TryCloseOnOutsideClick()
    {
        if (!IsOpen) return;

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                _rect, Input.mousePosition, null))
            {
                Close();
            }
        }
    }
}