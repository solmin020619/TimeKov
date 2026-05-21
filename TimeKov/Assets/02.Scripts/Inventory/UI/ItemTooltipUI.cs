// ItemTooltipUI.cs
// Tooltip ������Ʈ�� ���̴� ��ũ��Ʈ
// ���� ȣ�� �� ������ ���� ǥ��

using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    [Header("�ؽ�Ʈ ����")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;

    [Header("��ġ ������")]
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

    private RectTransform _rect;
    private RectTransform _canvasRect;
    private bool _isShowing;

    // Hide() 시점의 마우스 위치 — 같은 자리에서 Show()가 오면 억제
    // (UI 재오픈 시 SetActive(true)로 인해 OnPointerEnter가 재발동되는 Unity 특성 대응)
    private Vector3 _mousePositionAtHide = new Vector3(-9999f, -9999f, 0f);

    // ī�װ��� �̸� �ѱ��� ���̺� (ItemCategory ������ ��ġ)
    private static readonly string[] CategoryNames = new string[]
    {
        "���� ���",    // RawMaterial
        "1�� ����ǰ",   // ProcessedTier1
        "2�� ����ǰ",   // ProcessedTier2
        "���� �Ҹ�ǰ",  // TacticalConsumable
        "�ھ� ��ȭ",    // CoreUpgrade
        "Ư��"          // Special
    };

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvasRect = canvas.GetComponent<RectTransform>();

        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_isShowing) return;

        // UI가 모두 닫혔는데 툴팁이 남아있으면 자동 숨김
        // (SetActive(false)로 UI가 꺼질 때 OnPointerExit가 발동되지 않는 Unity 특성 대응)
        var uic = GameUIController.Instance;
        if (uic != null && !uic.IsUIBlocking())
        {
            Hide();
            return;
        }

        UpdatePosition(Input.mousePosition);
    }

    // ���� ǥ��
    public void Show(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        // Hide() 이후 마우스가 움직이지 않은 상태면 억제
        // (패널 재오픈 직후 OnPointerEnter 오발동 방지)
        if (Input.mousePosition == _mousePositionAtHide) return;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        if (itemNameText != null)
            itemNameText.text = data != null ? data.itemName : "�� �� ���� ������";

        if (categoryText != null)
        {
            if (data != null)
            {
                int catIndex = (int)data.itemCategory;
                catIndex = Mathf.Clamp(catIndex, 0, CategoryNames.Length - 1);
                categoryText.text = CategoryNames[catIndex];
            }
            else
            {
                categoryText.text = "";
            }
        }

        _isShowing = true;
        gameObject.SetActive(true);
        UpdatePosition(Input.mousePosition);
    }

    // ���� ����
    public void Hide()
    {
        _isShowing = false;
        gameObject.SetActive(false);
        // 이 위치에서 Show()가 오면 억제 (재오픈 시 오발동 방지)
        _mousePositionAtHide = Input.mousePosition;
    }

    // ��ġ ���� (��� Ŭ����)
    private void UpdatePosition(Vector2 screenPos)
    {
        if (_rect == null || _canvasRect == null) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out localPos);

        localPos += offset;

        float halfW = _rect.sizeDelta.x * 0.5f;
        float halfH = _rect.sizeDelta.y * 0.5f;
        float maxX = _canvasRect.rect.width * 0.5f - halfW;
        float maxY = _canvasRect.rect.height * 0.5f - halfH;

        localPos.x = Mathf.Clamp(localPos.x, -maxX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, -maxY, maxY);

        _rect.anchoredPosition = localPos;
    }
}