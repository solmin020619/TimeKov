// ContextMenuUI.cs
// ContextMenu ������Ʈ�� ���̴� ��ũ��Ʈ
// ���� ��Ŭ�� �� ���콺 ��ġ�� ��Ÿ���� �˾� �޴�

using UnityEngine;
using UnityEngine.UI;

public class ContextMenuUI : MonoBehaviour
{
    [Header("��ư ����")]
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

    // �޴� ����
    public void Open(InventorySlotUI slot, Vector2 screenPos)
    {
        if (slot == null || slot.IsEmpty) return;

        _currentSlot = slot;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        // ���� �Ҹ�ǰ�� ��� ��ư Ȱ��ȭ
        bool isConsumable = data != null && data.itemCategory == ItemCategory.TacticalConsumable;
        if (useBtn != null) useBtn.interactable = isConsumable;

        // ���� 2 �̻��̸� ���� ����
        if (splitBtn != null) splitBtn.interactable = slot.SlotData.amount > 1;

        gameObject.SetActive(true);
        SetPosition(screenPos);
    }

    // �޴� �ݱ�
    public void Close()
    {
        _currentSlot = null;
        gameObject.SetActive(false);
    }

    // ȭ�� ��� �� ��ġ ����
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
        // itemId를 미리 캡처 — TryConsumeItem이 slot.Clear()를 호출하면
        // slotData.itemId가 -1로 바뀌기 때문에 반드시 소비 전에 저장해야 함
        int itemId = _currentSlot.SlotData.itemId;

        var player = FindAnyObjectByType<Player>();

        // 소비 전 검사: 회복 앰플이 만피면 토스트만 띄우고 소비 안 함(아이템 보존)
        if (!ConsumableEffectApplier.CanApply(itemId.ToString(), player))
        {
            ToastManager.Warning("시간이 이미 가득 찼습니다.");
            Close();
            return;
        }

        // 아이템 소비 시도
        bool consumed = owner != null && owner.TryConsumeItem(itemId, 1);
        if (!consumed) { Close(); return; }

        // 효과 적용
        bool applied = ConsumableEffectApplier.Apply(itemId.ToString(), player);

        // 효과 적용 실패 시 아이템 복구
        if (!applied)
            owner.AddItem(itemId, 1);

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

    // �ܺ� Ŭ�� ���� (InventoryUIController Update ���� ȣ��)
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