using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GetItem : MonoBehaviour
{
    public GameObject invenMana;
    public TextMeshProUGUI getItemText;
    public Image ItemIcon;

    public int insertID;
    public int insertItemCount;

    void Start()
    {
        getItemText.text = DataManager.Instance.GetItem(insertID).itemName;
        ItemIcon.sprite = Resources.Load<Sprite>("Icon/" + insertID);
    }
    public void ItemClick()
    {
        if (insertID != 0)
        {
            invenMana.GetComponent<InventoryManager>().AddItem(insertID, insertItemCount);
            insertID = 0;
            getItemText.text = "노 아이템";
            ItemIcon.sprite = Resources.Load<Sprite>("RPG_inventory_icons/f");
        }

    }
}