// =====================================================================
// ChestItemRow.cs
// ChestOpenUI 안에서 아이템 한 줄을 표시하는 UI 컴포넌트
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestItemRow : MonoBehaviour
{
    [SerializeField] private Image            iconImage;
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private TextMeshProUGUI  countText;
    [SerializeField] private Image            gradeBar;   // 등급 색 표시 (선택)

    public void Set(int itemId, int count)
    {
        var data = GameDataUtility.GetItem(itemId);

        // 아이콘 — ItemDatabase.GetIcon 사용 (캐시 + 올바른 경로 Resources/Items/)
        if (iconImage != null)
        {
            Sprite icon = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        // 이름
        if (nameText != null)
            nameText.text = data?.itemName ?? itemId.ToString();

        // 수량
        if (countText != null)
            countText.text = $"x{count}";

        // 등급 바 색상
        if (gradeBar != null && data != null)
        {
            gradeBar.color = GetGradeColor((int)data.itemGrade);
        }
    }

    private Color GetGradeColor(int grade)
    {
        return grade switch
        {
            0 => new Color(0.7f, 0.7f, 0.7f),   // Common  (회색)
            1 => new Color(0.3f, 0.8f, 0.3f),   // Uncommon (초록)
            2 => new Color(0.3f, 0.5f, 1.0f),   // Rare     (파랑)
            3 => new Color(0.7f, 0.3f, 1.0f),   // Epic     (보라)
            4 => new Color(1.0f, 0.6f, 0.1f),   // Hero     (주황)
            5 => new Color(1.0f, 0.8f, 0.0f),   // Legend   (금색)
            _ => Color.white
        };
    }
}
