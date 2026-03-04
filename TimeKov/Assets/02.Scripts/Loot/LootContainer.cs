using System.Collections.Generic;
using UnityEngine;

public class LootContainer : MonoBehaviour
{
    public string containerId = "LC_001";

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

    // ✅ 입력 디바운스
    private float _nextToggleAllowedTime = 0f;

    // ✅ [추가] 현재 Loot UI에 "바인딩되어 있는" 컨테이너 (덕코프처럼 Loot UI는 하나, 컨테이너만 스위칭)
    public static LootContainer ActiveContainer { get; private set; }

    [System.Serializable]
    private struct LootStack
    {
        public int itemId;
        public int count;
        public LootStack(int id, int c) { itemId = id; count = c; }
    }
    private readonly List<LootStack> _cachedLoot = new List<LootStack>(16);

    private int _seenRaidSession = -1;

    private void OnDisable()
    {
        if (ActiveContainer == this) ActiveContainer = null;
    }

    // ✅ GetItem(슬롯)에서 루팅/변경이 발생했을 때 즉시 동기화하고 싶을 때 호출
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
        SyncRaidSessionAndResetIfNeeded();

        if (Time.unscaledTime < _nextToggleAllowedTime) return;
        _nextToggleAllowedTime = Time.unscaledTime + 0.15f;

        var ui = UIStateManager.Instance;

        // ✅ [핵심] Loot UI는 하나를 유지하고, 컨테이너만 스위칭
        if (ui != null)
        {
            if (ui.GetCurrentState() == UIStateManager.UIState.Loot)
            {
                if (ActiveContainer == this)
                {
                    // 같은 상자에서 다시 F -> 닫기
                    CacheFromSlots();
                    ui.ToggleLoot(lootPanelRoot);
                    ActiveContainer = null;
                    return;
                }

                // 다른 상자에서 F -> Loot UI 유지 + 현재 컨테이너 교체
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
            if (lootPanelRoot != null) lootPanelRoot.SetActive(true);
            ActiveContainer = this;
        }

        var dm = LootDataManager.Instance;
        if (dm == null) return;

        if (!dm.TryGetContainer(containerId, out var cdef))
        {
            Debug.LogWarning($"[LootContainer] ContainerDef 못 찾음. containerId={containerId}", gameObject);
            return;
        }

        if (cdef.reroll == 1)
        {
            _rolled = false;
            _cachedLoot.Clear();
        }

        if (!_rolled)
        {
            RollAndFill(cdef);
            _rolled = true;
            CacheFromSlots();
        }
        else
        {
            FillSlotsFromCache();
        }
    }

    private void RollAndFill(LootDataManager.ContainerDef cdef)
    {
        var dm = LootDataManager.Instance;
        if (!dm.TryGetLootTable(cdef.lootTableId, out var tdef))
        {
            Debug.LogWarning($"[LootContainer] LootTableDef 못 찾음. lootTableId={cdef.lootTableId}", gameObject);
            return;
        }

        ClearSlots();

        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null)
        {
            Debug.LogWarning("[LootContainer] playerInventoryManagerGO가 null (Inspector 연결 필요)", gameObject);
            return;
        }

        int rollCount = Random.Range(tdef.minRoll, tdef.maxRoll + 1);
        rollCount = Mathf.Clamp(rollCount, 0, lootSlots.Length);

        var pool = new List<LootDataManager.LootEntry>(tdef.entries);

        for (int i = 0; i < rollCount; i++)
        {
            if (pool.Count == 0) break;

            int pickedIndex = PickWeightedIndex(pool);
            if (pickedIndex < 0) break;

            var picked = pool[pickedIndex];
            int count = Random.Range(picked.minCount, picked.maxCount + 1);

            lootSlots[i].SetData(playerInventoryManagerGO, picked.itemId, count);

            if (tdef.allowDuplicate == 0)
                pool.RemoveAt(pickedIndex);
        }

        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true); // ✅ 여기서 스택 머지까지 같이 됨
        }
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

    // =========================
    // ✅ 스택 규칙 (InventoryManager와 동일 룰)
    // duplicated==1이면 overlapsCount, 아니면 1
    // =========================
    private int GetMaxStackSize(int itemId)
    {
        var item = (DataManager.Instance != null) ? DataManager.Instance.GetItem(itemId) : null;
        if (item == null) return 1;

        if (item.duplicated == 1)
            return Mathf.Max(1, item.overlapsCount);

        return 1;
    }

    private void CompactAndRefreshIfNeeded(bool force)
    {
        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        int snapshotHash = ComputeSnapshotHash();
        if (!force && snapshotHash == _lastSnapshotHash) return;
        _lastSnapshotHash = snapshotHash;

        // 1) 현재 슬롯에서 아이템 읽기 (등장 순서 유지)
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

        // 2) order 순서대로 "최대 스택까지" 쪼개서 packed 만들기
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

        // 3) 슬롯에 반영 (남는 칸은 0,0)
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

    private int PickWeightedIndex(List<LootDataManager.LootEntry> entries)
    {
        float sum = 0f;
        for (int i = 0; i < entries.Count; i++)
            sum += Mathf.Max(0f, entries[i].probability);

        if (sum <= 0f) return -1;

        float r = Random.value * sum;
        float acc = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            acc += Mathf.Max(0f, entries[i].probability);
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
            CompactAndRefreshIfNeeded(force: true); // ✅ 여기서 스택 머지까지 같이 됨
        }
    }

    private void SyncRaidSessionAndResetIfNeeded()
    {
        int cur = LootDataManager.CurrentRaidSession;
        if (_seenRaidSession == cur) return;

        _seenRaidSession = cur;
        _rolled = false;
        _cachedLoot.Clear();
        _lastSnapshotHash = 0;

        if (ActiveContainer == this) ActiveContainer = null;
    }
}