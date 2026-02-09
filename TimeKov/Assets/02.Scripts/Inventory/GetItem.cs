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
        RefreshUI();
    }

    // ✅ 상자가 “데이터 주입”할 때 쓰는 함수
    public void SetData(GameObject inventoryManagerGO, int id, int count)
    {
        invenMana = inventoryManagerGO;
        insertID = id;
        insertItemCount = count;
        RefreshUI();
    }

    // ✅ 화면 갱신 (NullReference 방지)
    public void RefreshUI()
    {
        // UI 레퍼런스가 비어있으면 여기서 터졌을 가능성이 제일 큼
        if (getItemText == null || ItemIcon == null)
        {
            Debug.LogWarning($"[GetItem] UI ref missing on {gameObject.name} (getItemText or ItemIcon null)");
            return;
        }

        if (insertID != 0)
        {
            if (DataManager.Instance == null)
            {
                Debug.LogWarning($"[GetItem] DataManager.Instance is null (insertID={insertID})");
                // 그래도 최소한 텍스트는 표시
                getItemText.text = insertID.ToString();
                ItemIcon.sprite = null;
                return;
            }

            var item = DataManager.Instance.GetItem(insertID);
            if (item == null)
            {
                Debug.LogWarning($"[GetItem] Item not found in DataManager (id={insertID})");
                getItemText.text = insertID.ToString();
                ItemIcon.sprite = null;
                return;
            }

            getItemText.text = item.itemName;

            // 스프라이트가 없으면 null이어도 터지진 않게
            ItemIcon.sprite = Resources.Load<Sprite>("Icon/" + insertID);
        }
        else
        {
            getItemText.text = "노 아이템";
            ItemIcon.sprite = Resources.Load<Sprite>("RPG_inventory_icons/f");
        }
    }

    public void ItemClick()
    {
        if (insertID != 0)
        {
            if (invenMana == null)
            {
                Debug.LogWarning($"[GetItem] invenMana is null on {gameObject.name}");
                return;
            }

            var inv = invenMana.GetComponent<InventoryManager>();
            if (inv == null)
            {
                Debug.LogWarning($"[GetItem] InventoryManager component not found on invenMana ({invenMana.name})");
                return;
            }

            inv.AddItem(insertID, insertItemCount);

            insertID = 0;
            insertItemCount = 0;
            RefreshUI();
        }
    }
}
