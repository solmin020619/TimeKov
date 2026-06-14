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
        // 공용 GradeVisual 사용 (예전 switch는 6단계 + Uncommon/Epic 라벨로 enum과 어긋난 버그였음)
        return GradeVisual.GetColor(grade);
    }
}
