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

        Sprite icon = Resources.Load<Sprite>("Icon/" + itemId);
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }
}
