using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DropPickupRow : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image tierBar;
    [SerializeField] private TMP_Text nameText;

    public void Set(int itemId, int count, Color tierColor)
    {
        ItemDataSheetData item = GameDataUtility.GetItem(itemId);

        nameText.text = item != null ? item.GetLocalizedName() : itemId.ToString();
        countText.text = count.ToString();
        tierBar.color = tierColor;

        // 아이콘 — 인벤토리와 동일한 ItemDatabase.GetIcon 사용 (Resources/Items/ + 캐시)
        Sprite icon = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }
}
