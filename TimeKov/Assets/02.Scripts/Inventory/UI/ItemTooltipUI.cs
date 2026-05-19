// ItemTooltipUI.cs
// Tooltip 오브젝트에 붙이는 스크립트
// 슬롯 호버 시 아이템 정보 표시

using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    [Header("텍스트 참조")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;

    [Header("위치 오프셋")]
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

    private RectTransform _rect;
    private RectTransform _canvasRect;
    private bool _isShowing;

    // 카테고리 이름 한국어 테이블 (ItemCategory 순서와 일치)
    private static readonly string[] CategoryNames = new string[]
    {
        "원초 재료",    // RawMaterial
        "1차 가공품",   // ProcessedTier1
        "2차 가공품",   // ProcessedTier2
        "전술 소모품",  // TacticalConsumable
        "코어 강화",    // CoreUpgrade
        "특수"          // Special
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
        if (_isShowing)
            UpdatePosition(Input.mousePosition);
    }

    // 툴팁 표시
    public void Show(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        var data = ItemDatabase.GetItem(slot.SlotData.itemId);

        if (itemNameText != null)
            itemNameText.text = data != null ? data.itemName : "알 수 없는 아이템";

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

    // 툴팁 숨김
    public void Hide()
    {
        _isShowing = false;
        gameObject.SetActive(false);
    }

    // 위치 갱신 (경계 클램핑)
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