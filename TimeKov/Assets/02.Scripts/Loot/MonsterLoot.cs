using System.Collections.Generic;
using UnityEngine;

public class MonsterLoot : MonoBehaviour
{
    [Header("Drop Key")]
    [Tooltip("dropTable.sourceId와 정확히 맞춰야 함. 예: MeleeBot_Ghoul")]
    public string monsterType = "MeleeBot_Ghoul";

    [Header("Drop Source")]
    [Tooltip("네 dropTable 기준 몬스터는 monster")]
    public string sourceType = "monster";

    [Header("Drop Tier")]
    [Tooltip("현재 네 dropTable 기준 몬스터 드랍은 0 고정")]
    public int dropTier = 0;

    [Header("UI")]
    public GameObject lootPanelRoot;
    public GetItem[] lootSlots;
    public GameObject playerInventoryManagerGO;

    [Header("BG식(빈 슬롯 숨김/자동 정렬)")]
    public bool enableBGStyleLoot = true;
    public float bgSyncInterval = 0.1f;

    private float _nextBgSyncTime = 0f;
    private int _lastSnapshotHash = 0;
    private bool _rolled = false;

    public void Open()
    {
        EnsureDataStoreLoaded();
        EnsureRefs();

        if (lootPanelRoot == null)
        {
            Debug.LogError("[MonsterLoot] lootPanelRoot 못 찾음");
            return;
        }

        if (UIStateManager.Instance != null)
            UIStateManager.Instance.ToggleLoot(lootPanelRoot);
        else
            lootPanelRoot.SetActive(true);

        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() != UIStateManager.UIState.Loot)
            return;

        if (lootSlots == null || lootSlots.Length == 0)
        {
            Debug.LogError("[MonsterLoot] lootSlots 비어있음");
            return;
        }

        if (playerInventoryManagerGO == null)
        {
            Debug.LogError("[MonsterLoot] playerInventoryManagerGO 못 찾음");
            return;
        }

        if (!_rolled)
        {
            List<DropRow> rows = GetDropRowsForMonster();
            if (rows == null || rows.Count == 0)
            {
                Debug.LogWarning($"[MonsterLoot] drop rows empty. sourceType={sourceType}, sourceId={monsterType}, dropTier={dropTier}");
                return;
            }

            RollAndFill(rows);
            _rolled = true;

            Debug.Log($"[MonsterLoot] Loot rolled. sourceId={monsterType}, dropTier={dropTier}");
        }
        else
        {
            Debug.Log($"[MonsterLoot] Re-open fixed loot 유지 ({monsterType})");
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private List<DropRow> GetDropRowsForMonster()
    {
        List<DropRow> best = null;
        int bestDropId = int.MaxValue;

        foreach (var kv in DataStore.DropRowsByDropId)
        {
            List<DropRow> rows = kv.Value;
            if (rows == null || rows.Count == 0) continue;

            DropRow head = rows[0];

            if (!StringEquals(head.sourceType, sourceType)) continue;
            if (!StringEquals(head.sourceId, monsterType)) continue;
            if (head.dropTier != dropTier) continue;

            if (kv.Key < bestDropId)
            {
                bestDropId = kv.Key;
                best = rows;
            }
        }

        if (best == null)
            return null;

        return new List<DropRow>(best);
    }

    private void RollAndFill(List<DropRow> rows)
    {
        EnsureRefs();
        ClearSlots();

        int pickCount = GetPickCount(rows);
        pickCount = Mathf.Clamp(pickCount, 0, lootSlots.Length);

        List<DropRow> pool = new List<DropRow>(rows);

        for (int i = 0; i < pickCount; i++)
        {
            if (pool.Count == 0) break;
            if (lootSlots[i] == null) continue;

            int pickedIndex = PickWeightedIndex(pool);
            if (pickedIndex < 0) break;

            DropRow picked = pool[pickedIndex];

            ItemRow item = DataStore.GetItem(picked.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[MonsterLoot] ItemRow 없음. itemId={picked.itemId}");
                pool.RemoveAt(pickedIndex);
                i--;
                continue;
            }

            int min = Mathf.Max(1, picked.minCount);
            int max = Mathf.Max(min, picked.maxCount);
            int count = Random.Range(min, max + 1);

            lootSlots[i].SetData(playerInventoryManagerGO, picked.itemId, count);

            // 현재 구조는 중복 없이 pickCount번 선택
            pool.RemoveAt(pickedIndex);
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private int GetPickCount(List<DropRow> rows)
    {
        if (rows == null || rows.Count == 0) return 0;
        return Mathf.Max(1, rows[0].pickCount);
    }

    private void EnsureRefs()
    {
        if (lootPanelRoot == null)
            lootPanelRoot = FindSceneObjectEvenIfInactive("Drop");

        if (playerInventoryManagerGO == null)
        {
            InventoryManager picked = null;
            var all = Resources.FindObjectsOfTypeAll<InventoryManager>();

            for (int i = 0; i < all.Length; i++)
            {
                var inv = all[i];
                if (inv == null) continue;
                if (!inv.gameObject.scene.IsValid()) continue;
                if (inv.GetType() != typeof(InventoryManager)) continue;

                if (inv.gameObject.name == "InventoryManager")
                {
                    picked = inv;
                    break;
                }

                if (picked == null)
                    picked = inv;
            }

            if (picked != null)
                playerInventoryManagerGO = picked.gameObject;
        }

        bool needRebuildSlots = (lootSlots == null || lootSlots.Length == 0);
        if (!needRebuildSlots)
        {
            for (int i = 0; i < lootSlots.Length; i++)
            {
                if (lootSlots[i] == null)
                {
                    needRebuildSlots = true;
                    break;
                }
            }
        }

        if (needRebuildSlots && lootPanelRoot != null)
            lootSlots = lootPanelRoot.GetComponentsInChildren<GetItem>(true);
    }

    private void ClearSlots()
    {
        if (lootSlots == null) return;

        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] != null && playerInventoryManagerGO != null)
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            _lastSnapshotHash = 0;
        }
    }

    private void Update()
    {
        if (!enableBGStyleLoot) return;
        if (lootPanelRoot == null) return;
        if (!lootPanelRoot.activeInHierarchy) return;
        if (Time.unscaledTime < _nextBgSyncTime) return;

        _nextBgSyncTime = Time.unscaledTime + Mathf.Max(0.02f, bgSyncInterval);
        CompactAndRefreshIfNeeded(force: false);
    }

    private void CompactAndRefreshIfNeeded(bool force)
    {
        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        int snapshotHash = ComputeSnapshotHash();
        if (!force && snapshotHash == _lastSnapshotHash) return;
        _lastSnapshotHash = snapshotHash;

        var order = new List<int>(lootSlots.Length);
        var totals = new Dictionary<int, int>(lootSlots.Length);

        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            if (id == 0 || count <= 0) continue;

            if (!totals.ContainsKey(id))
            {
                totals[id] = count;
                order.Add(id);
            }
            else
            {
                totals[id] += count;
            }
        }

        var packed = new List<(int id, int count)>(lootSlots.Length);

        for (int k = 0; k < order.Count; k++)
        {
            int id = order[k];
            int remaining = totals[id];

            int maxStack = GetMaxStackSize(id);
            if (maxStack <= 0) maxStack = 1;

            while (remaining > 0)
            {
                int put = Mathf.Min(maxStack, remaining);
                packed.Add((id, put));
                remaining -= put;
            }
        }

        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] == null) continue;

            if (i < packed.Count)
                lootSlots[i].SetData(playerInventoryManagerGO, packed[i].id, packed[i].count);
            else
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }

        ApplyBGStyleVisibility();
    }

    private int GetMaxStackSize(int itemId)
    {
        ItemRow item = DataStore.GetItem(itemId);
        if (item == null) return 1;

        if (item.stackable == 1)
            return Mathf.Max(1, item.maxStack);

        return 1;
    }

    private void ApplyBGStyleVisibility()
    {
        if (lootSlots == null) return;

        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            bool shouldShow = (id != 0 && count > 0);
            if (slot.gameObject.activeSelf != shouldShow)
                slot.gameObject.SetActive(shouldShow);
        }
    }

    private int ComputeSnapshotHash()
    {
        unchecked
        {
            int hash = 17;
            if (lootSlots == null) return hash;

            for (int i = 0; i < lootSlots.Length; i++)
            {
                var slot = lootSlots[i];
                if (slot == null) continue;

                int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
                int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

                hash = hash * 31 + id;
                hash = hash * 31 + count;
            }

            return hash;
        }
    }

    private int ReadIntField(object obj, params string[] fieldNames)
    {
        if (obj == null) return 0;
        var type = obj.GetType();

        for (int i = 0; i < fieldNames.Length; i++)
        {
            var f = type.GetField(fieldNames[i]);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(obj);

            var p = type.GetProperty(fieldNames[i]);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
                return (int)p.GetValue(obj, null);
        }

        return 0;
    }

    private GameObject FindSceneObjectEvenIfInactive(string targetName)
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.name == targetName) return t.gameObject;
        }
        return null;
    }

    private int PickWeightedIndex(List<DropRow> entries)
    {
        float sum = 0f;
        for (int i = 0; i < entries.Count; i++)
            sum += Mathf.Max(0f, entries[i].dropWeight);

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

    private bool StringEquals(string a, string b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}