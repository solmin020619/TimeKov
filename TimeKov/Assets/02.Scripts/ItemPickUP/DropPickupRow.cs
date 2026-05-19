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

        nameText.text = item != null ? item.itemName : itemId.ToString();
        countText.text = count.ToString();
        tierBar.color = tierColor;

        // 아이콘 — ItemData 의 iconKey 로 Resources/Icon 에서 로드
        Sprite icon = null;
        if (item != null && !string.IsNullOrEmpty(item.iconKey))
            icon = Resources.Load<Sprite>("Icon/" + item.iconKey);
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }
}
