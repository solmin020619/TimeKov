using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LootContainer : MonoBehaviour
{
    [Header("Drop Key")]
    [Tooltip("dropTable.sourceId와 정확히 맞춰야 함. 예: LC_AMMO, LC_LOOT, LC_TOOL, LC_WEAPON, LC_BAG")]
    public string containerId = "LC_AMMO";

    [Header("Drop Source")]
    [Tooltip("네 dropTable 기준 컨테이너는 chest")]
    public string sourceType = "chest";

    [Header("UI")]
    public GameObject lootPanelRoot;
    public GetItem[] lootSlots;
    public GameObject playerInventoryManagerGO;

    [Header("BG식(빈 슬롯 숨김/자동 정렬)")]
    public bool enableBGStyleLoot = true;
    public float bgSyncInterval = 0.1f;

    [Header("세션 규칙")]
    [Tooltip("같은 씬/레이드 안에서는 한 번 굴린 루팅 결과를 유지")]
    public bool keepRolledLootUntilSceneChanges = true;

    private float _nextBgSyncTime = 0f;
    private int _lastSnapshotHash = 0;
    private bool _rolled = false;

    private float _nextToggleAllowedTime = 0f;

    public static LootContainer ActiveContainer { get; private set; }

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

    private readonly List<LootStack> _cachedLoot = new List<LootStack>(16);

    private static string s_lastSceneName = "";
    private static int s_sceneSession = 0;
    private int _seenSceneSession = -1;

    private void OnDisable()
    {
        if (ActiveContainer == this)
            ActiveContainer = null;
    }

    public void NotifyLootChanged()
    {
        if (!enableBGStyleLoot) return;
        if (lootPanelRoot == null || !lootPanelRoot.activeInHierarchy) return;
        if (ActiveContainer != this) return;

        _lastSnapshotHash = 0;
        CompactAndRefreshIfNeeded(force: true);
        CacheFromSlots();
    }

    public void Open()
    {
        EnsureDataStoreLoaded();
        UpdateSceneSession();
        SyncSceneSessionAndResetIfNeeded();

        if (Time.unscaledTime < _nextToggleAllowedTime) return;
        _nextToggleAllowedTime = Time.unscaledTime + 0.15f;

        var ui = UIStateManager.Instance;

        if (ui != null)
        {
            if (ui.GetCurrentState() == UIStateManager.UIState.Loot)
            {
                if (ActiveContainer == this)
                {
                    CacheFromSlots();
                    ui.ToggleLoot(lootPanelRoot);
                    ActiveContainer = null;
                    return;
                }

                ActiveContainer = this;
                ui.SetCurrentLootUI(lootPanelRoot);
            }
            else
            {
                ActiveContainer = this;
                ui.ToggleLoot(lootPanelRoot);
            }

            if (ui.GetCurrentState() != UIStateManager.UIState.Loot)
                return;
        }
        else
        {
            if (lootPanelRoot != null)
                lootPanelRoot.SetActive(true);

            ActiveContainer = this;
        }

        List<DropRow> dropRows = GetDropRowsForContainer();
        if (dropRows == null || dropRows.Count == 0)
        {
            Debug.LogWarning($"[LootContainer] drop rows not found. sourceType={sourceType}, sourceId={containerId}", gameObject);
            return;
        }

        if (!_rolled)
        {
            RollAndFill(dropRows);
            _rolled = true;
            CacheFromSlots();
        }
        else
        {
            FillSlotsFromCache();
        }
    }

    private List<DropRow> GetDropRowsForContainer()
    {
        List<DropRow> best = null;
        int bestDropId = int.MaxValue;

        foreach (var kv in DataStore.DropRowsByDropId)
        {
            List<DropRow> rows = kv.Value;
            if (rows == null || rows.Count == 0) continue;

            DropRow head = rows[0];

            if (!StringEquals(head.sourceType, sourceType)) continue;
            if (!StringEquals(head.sourceId, containerId)) continue;

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

    private void RollAndFill(List<DropRow> dropRows)
    {
        ClearSlots();

        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null)
        {
            Debug.LogWarning("[LootContainer] playerInventoryManagerGO가 null", gameObject);
            return;
        }

        int pickCount = GetPickCount(dropRows);
        pickCount = Mathf.Clamp(pickCount, 0, lootSlots.Length);

        List<DropRow> pool = new List<DropRow>(dropRows);

        for (int i = 0; i < pickCount; i++)
        {
            if (pool.Count == 0) break;

            int pickedIndex = PickWeightedIndex(pool);
            if (pickedIndex < 0) break;

            DropRow picked = pool[pickedIndex];

            ItemRow item = DataStore.GetItem(picked.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[LootContainer] ItemRow 없음. itemId={picked.itemId}");
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
        if (ActiveContainer != this) return;

        if (Time.unscaledTime < _nextBgSyncTime) return;
        _nextBgSyncTime = Time.unscaledTime + Mathf.Max(0.02f, bgSyncInterval);

        CompactAndRefreshIfNeeded(force: false);
        CacheFromSlots();
    }

    private int GetMaxStackSize(int itemId)
    {
        ItemRow item = DataStore.GetItem(itemId);
        if (item == null) return 1;

        if (item.stackable == 1)
            return Mathf.Max(1, item.maxStack);

        return 1;
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

    private void CacheFromSlots()
    {
        if (lootSlots == null) return;

        _cachedLoot.Clear();

        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            if (id != 0 && count > 0)
                _cachedLoot.Add(new LootStack(id, count));
        }
    }

    private void FillSlotsFromCache()
    {
        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] == null) continue;

            if (i < _cachedLoot.Count)
                lootSlots[i].SetData(playerInventoryManagerGO, _cachedLoot[i].itemId, _cachedLoot[i].count);
            else
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            _lastSnapshotHash = 0;
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }

    private static void UpdateSceneSession()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != s_lastSceneName)
        {
            s_lastSceneName = sceneName;
            s_sceneSession++;
        }
    }

    private void SyncSceneSessionAndResetIfNeeded()
    {
        if (!keepRolledLootUntilSceneChanges)
        {
            _rolled = false;
            _cachedLoot.Clear();
            _lastSnapshotHash = 0;
            return;
        }

        if (_seenSceneSession == s_sceneSession) return;

        _seenSceneSession = s_sceneSession;
        _rolled = false;
        _cachedLoot.Clear();
        _lastSnapshotHash = 0;

        if (ActiveContainer == this)
            ActiveContainer = null;
    }

    private bool StringEquals(string a, string b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}