using System.Collections.Generic;
using UnityEngine;

public class MonsterLoot : MonoBehaviour
{
    [Header("Drop Key")]
    public string monsterType = "MeleeBot_Ghoul";

    [Header("Drop Source")]
    public string sourceType = "monster";

    [Header("Drop Tier")]
    public int dropTier = 0;

    [Header("UI")]
    public GameObject lootPanelRoot;
    public Transform lootContentTransform;
    public GameObject lootSlotPrefab;
    public GameObject playerInventoryManagerGO;

    private bool _rolled = false;

    private readonly List<LootStack> _rolledLoot = new List<LootStack>(32);
    private readonly List<GetItem> _spawnedLootSlots = new List<GetItem>(32);

    [System.Serializable]
    private struct LootStack
    {
        public int itemId;
        public int count;

        public LootStack(int id, int c)
        {
            itemId = id;
            count = c;
        }
    }

    public void Open()
    {
        EnsureDataStoreLoaded();

        if (lootPanelRoot == null)
        {
            Debug.LogError("[MonsterLoot] lootPanelRoot 없음");
            return;
        }

        if (UIStateManager.Instance != null)
        {
            RightPanelController rpc = Object.FindFirstObjectByType<RightPanelController>();
            if (rpc != null)
                rpc.ShowPanel(RightPanelController.PanelType.Loot);

            UIStateManager.Instance.ToggleLoot(lootPanelRoot);
        }
        else
        {
            lootPanelRoot.SetActive(true);
        }

        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() != UIStateManager.UIState.Loot)
            return;

        if (!_rolled)
        {
            List<DropRow> rows = GetDropRowsForMonster();
            if (rows == null || rows.Count == 0)
            {
                Debug.LogWarning($"[MonsterLoot] drop rows empty: {monsterType}");
                return;
            }

            RollAndFill(rows);
            _rolled = true;
        }
        else
        {
            FillSlotsFromCache();
        }
    }

    private List<DropRow> GetDropRowsForMonster()
    {
        List<DropRow> best = null;

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
            {
                
                if (dropTier != 0)
                    continue;
            }

         
            return new List<DropRow>(rows);
        }

        Debug.LogWarning($"[MonsterLoot] drop rows not found: {sourceType}, {monsterType}, tier:{dropTier}");
        return null;
    }

    private void RollAndFill(List<DropRow> rows)
    {
        ClearSpawnedSlots();
        _rolledLoot.Clear();

        int pickCount = Mathf.Max(1, rows[0].pickCount);
        List<DropRow> pool = new List<DropRow>(rows);

        for (int i = 0; i < pickCount; i++)
        {
            if (pool.Count == 0) break;

            int index = PickWeightedIndex(pool);
            if (index < 0) break;

            DropRow picked = pool[index];

            int min = Mathf.Max(1, picked.minCount);
            int max = Mathf.Max(min, picked.maxCount);
            int count = Random.Range(min, max + 1);

            _rolledLoot.Add(new LootStack(picked.itemId, count));
            pool.RemoveAt(index);
        }

        BuildSlots();
    }

    private void BuildSlots()
    {
        ClearSpawnedSlots();

        for (int i = 0; i < _rolledLoot.Count; i++)
        {
            GameObject go = Instantiate(lootSlotPrefab, lootContentTransform);
            GetItem slot = go.GetComponent<GetItem>();

            if (slot == null)
            {
                Debug.LogError("GetItem 없음");
                Destroy(go);
                continue;
            }

            slot.SetData(playerInventoryManagerGO,
                _rolledLoot[i].itemId,
                _rolledLoot[i].count);

            _spawnedLootSlots.Add(slot);
        }
    }

    private void FillSlotsFromCache()
    {
        BuildSlots();
    }

    private void ClearSpawnedSlots()
    {
        foreach (var s in _spawnedLootSlots)
        {
            if (s != null)
                Destroy(s.gameObject);
        }

        _spawnedLootSlots.Clear();
    }

    private int PickWeightedIndex(List<DropRow> entries)
    {
        float sum = 0f;
        foreach (var e in entries)
            sum += Mathf.Max(0f, e.dropWeight);

        if (sum <= 0f) return -1;

        float r = Random.value * sum;
        float acc = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            acc += Mathf.Max(0f, entries[i].dropWeight);
            if (r <= acc) return i;
        }

        return entries.Count - 1;
    }

    private void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }
}