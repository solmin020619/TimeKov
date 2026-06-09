// ItemTooltipUI.cs
// 툴팁 컴포넌트를 관리하는 스크립트
// 슬롯 호버 시 아이템 정보 표시

using UnityEngine;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    [Header("텍스트 필드")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryText;

    [Header("위치 오프셋")]
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

    private RectTransform _rect;
    private RectTransform _canvasRect;
    private bool _isShowing;
    private InventorySlotUI _currentSlot;

    // 카테고리 이름 한글 테이블 (ItemCategory 열거형 순서와 일치)
    private static readonly string[] CategoryNames = new string[]
    {
        "원재료",       // RawMaterial
        "1차 가공품",   // ProcessedTier1
        "2차 가공품",   // ProcessedTier2
        "전술 소모품",  // TacticalConsumable
        "핵심 강화",    // CoreUpgrade
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

    // 툴팁 표시
    public void Show(InventorySlotUI slot)
    {
        if (slot == null || slot.IsEmpty) return;

        // UI가 닫혀 있으면 표시하지 않음
        // (패널 재오픈 직후 stale OnPointerEnter 오발동 방지 — 기존 같은-픽셀 억제 대체)
        var uic = GameUIController.Instance;
        if (uic != null && !uic.IsUIBlocking()) return;

        // 같은 슬롯에 이미 표시 중이면 위치만 갱신
        // (슬롯 내부 그래픽 경계를 지날 때 enter/exit 가 반복돼도 깜빡임 없음)
        if (_isShowing && slot == _currentSlot)
        {
            UpdatePosition(Input.mousePosition);
            return;
        }
        _currentSlot = slot;

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
        _currentSlot = null;
        gameObject.SetActive(false);
    }

    // 위치 갱신 (마우스 클램프)
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
