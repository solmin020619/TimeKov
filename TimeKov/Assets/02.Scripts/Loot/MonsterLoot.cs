using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine;

public class MonsterLoot : MonoBehaviour
{
    [Header("Drop Info")]
    public string monsterType;
    public string sourceType = "monster";
    public int dropTier = 0;

    [Header("UI")]
    [SerializeField] private GameObject lootSlotPrefab;

    private RectTransform _content;
    private GameObject _lootPanel;

    private List<GameObject> _spawnedSlots = new List<GameObject>();
    private List<LootData> _rolledLoot = new List<LootData>();


    public class LootData
    {
        public int itemId;
        public int count;

        public LootData(int id, int cnt)
        {
            itemId = id;
            count = cnt;
        }
    }

    void Awake()
    {
        FindUI();
    }

    void FindUI()
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();

        var inventory = all.FirstOrDefault(t => t.name == "Inventory");
        if (inventory == null)
        {
            Debug.LogError("Inventory 없음");
            return;
        }

        var panel = inventory.Find("RightPanel/LootPanel");
        if (panel == null)
        {
            Debug.LogError("LootPanel 못찾음");
            return;
        }

        _lootPanel = panel.gameObject;

        var content = panel.Find("Scroll View/Viewport/Content");
        if (content == null)
        {
            Debug.LogError("Content 못찾음");
            return;
        }

        _content = content.GetComponent<RectTransform>();
    }

    public void Open()
    {
        if (_content == null || _lootPanel == null)
        {
            FindUI();
            if (_content == null) return;
        }

       
        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() == UIStateManager.UIState.Loot)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.None);
            return;
        }

        EnsureDataStoreLoaded();

        ClearSlots();
        _rolledLoot.Clear();

        RollLoot();
        BuildSlots();

        if (UIStateManager.Instance != null)
            UIStateManager.Instance.ToggleLoot(_lootPanel);
        else
            _lootPanel.SetActive(true);
    }

    public void Close()
    {
        if (UIStateManager.Instance != null)
            UIStateManager.Instance.SetState(UIStateManager.UIState.None);
        else if (_lootPanel != null)
            _lootPanel.SetActive(false);

        ClearSlots();
    }

    void BuildSlots()
    {
        foreach (var data in _rolledLoot)
        {
            GameObject slot = Instantiate(lootSlotPrefab, _content);

            var slotInfo = slot.GetComponent<SlotInfo>();
            if (slotInfo != null)
            {
                slotInfo.ownerType = SlotInfo.SlotOwnerType.Loot;
                slotInfo.SetSlot(data.itemId, data.count);
            }

            _spawnedSlots.Add(slot);
        }
    }

    void ClearSlots()
    {
        if (_content == null) return;

        for (int i = _content.childCount - 1; i >= 0; i--)
        {
            Destroy(_content.GetChild(i).gameObject);
        }

        _spawnedSlots.Clear();
    }

    void RollLoot()
    {
        var rows = GetMatchedDropRows();

        if (rows == null || rows.Count == 0)
            return;

        int pickCount = Mathf.Max(1, rows[0].pickCount);
        List<DropRow> pool = new List<DropRow>(rows);

        for (int i = 0; i < pickCount; i++)
        {
            if (pool.Count == 0) break;

            int index = Random.Range(0, pool.Count);
            var picked = pool[index];

            int count = Random.Range(picked.minCount, picked.maxCount + 1);
            _rolledLoot.Add(new LootData(picked.itemId, count));

            pool.RemoveAt(index);
        }
    }

    List<DropRow> GetMatchedDropRows()
    {
        foreach (var kv in DataStore.DropRowsByDropId)
        {
            var rows = kv.Value;
            if (rows == null || rows.Count == 0) continue;

            var head = rows[0];

            if (!head.sourceType.Equals(sourceType, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (!head.sourceId.Equals(monsterType, System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (head.dropTier != dropTier)
                continue;

            return new List<DropRow>(rows);
        }

        return null;
    }

    void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }
}