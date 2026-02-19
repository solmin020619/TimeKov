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

    // ✅ 슬롯 기본 배경(root) 저장
    private Sprite defaultSlotSprite;

    void Awake()
    {
        // 시작 전 프리팹/씬에서 세팅된 슬롯 배경을 저장해둠
        if (ItemIcon != null)
            defaultSlotSprite = ItemIcon.sprite;
    }

    void Start()
    {
        RefreshUI();
    }

    public void SetData(GameObject inventoryManagerGO, int id, int count)
    {
        invenMana = inventoryManagerGO;
        insertID = id;
        insertItemCount = count;
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (getItemText == null || ItemIcon == null)
        {
            Debug.LogWarning($"[GetItem] UI ref missing on {gameObject.name}");
            return;
        }

        if (insertID == 0)
        {
            // ✅ 문제 1: 빈칸에 글씨 뜨면 안 됨
            getItemText.text = "";

            // ✅ 문제 2: 빈칸이면 아이콘/이미지는 반드시 슬롯 배경으로 복구
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);

            return;
        }

        // 아이템 있는 상태
        if (DataManager.Instance == null)
        {
            Debug.LogWarning($"[GetItem] DataManager.Instance is null (insertID={insertID})");
            getItemText.text = insertID.ToString();

            // 그래도 아이콘 못 찾으면 슬롯배경으로 fallback
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);
            return;
        }

        var item = DataManager.Instance.GetItem(insertID);
        if (item == null)
        {
            Debug.LogWarning($"[GetItem] Item not found (id={insertID})");
            getItemText.text = insertID.ToString();

            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);
            return;
        }

        getItemText.text = item.itemName;

        Sprite spr = Resources.Load<Sprite>("Icon/" + insertID);
        if (spr != null)
        {
            ItemIcon.sprite = spr;
            ItemIcon.enabled = true;
        }
        else
        {
            // 아이콘 파일 없으면 슬롯배경으로
            ItemIcon.sprite = defaultSlotSprite;
            ItemIcon.enabled = (defaultSlotSprite != null);
            Debug.LogWarning($"[GetItem] Missing icon sprite: Resources/Icon/{insertID}");
        }
    }

    public void ItemClick()
    {
        if (insertID == 0) return;

        if (invenMana == null)
        {
            Debug.LogWarning($"[GetItem] invenMana is null on {gameObject.name}");
            return;
        }

        var inv = invenMana.GetComponent<InventoryManager>();
        if (inv == null)
        {
            Debug.LogWarning($"[GetItem] InventoryManager not found on {invenMana.name}");
            return;
        }

        // 인벤에 넣기
        inv.AddItem(insertID, insertItemCount);

        // ✅ 먹었으면 해당 슬롯은 비우고 UI도 즉시 빈칸으로
        insertID = 0;
        insertItemCount = 0;
        RefreshUI();
    }
}
