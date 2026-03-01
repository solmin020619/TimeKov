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
    [Tooltip("인벤토리처럼: 아이템 있는 슬롯만 보이게 + 아이템 빠지면 아래 아이템이 위로 당겨짐")]
    public bool enableBGStyleLoot = true;

    [Tooltip("루팅창이 열려있는 동안 슬롯 상태를 주기적으로 감지해서 자동 정렬/숨김 처리")]
    public float bgSyncInterval = 0.1f;

    private float _nextBgSyncTime = 0f;
    private int _lastSnapshotHash = 0;

    private bool _rolled = false;

    public void Open()
    {
        if (UIStateManager.Instance != null)
            UIStateManager.Instance.ToggleLoot(lootPanelRoot);
        else if (lootPanelRoot != null)
            lootPanelRoot.SetActive(true);

        if (UIStateManager.Instance != null &&
            UIStateManager.Instance.GetCurrentState() != UIStateManager.UIState.Loot)
            return;

        var dm = LootDataManager.Instance;
        if (dm == null) return;

        if (!dm.TryGetContainer(containerId, out var cdef)) return;

        if (cdef.reroll == 1) _rolled = false;

        if (!_rolled)
        {
            RollAndFill(cdef);
            _rolled = true; // ✅ 예외 안 터지면 여기 찍힘 -> 이후 재오픈해도 고정
        }
        else
        {
            Debug.Log($"[LootContainer] Re-open (fixed loot 유지) containerId={containerId}");
        }

        // ✅ BG식(빈칸 숨김/정렬) 반영
        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
        }
    }

    private void RollAndFill(LootDataManager.ContainerDef cdef)
    {
        var dm = LootDataManager.Instance;
        if (!dm.TryGetLootTable(cdef.lootTableId, out var tdef)) return;

        ClearSlots();

        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        int rollCount = Random.Range(tdef.minRoll, tdef.maxRoll + 1);
        rollCount = Mathf.Clamp(rollCount, 0, lootSlots.Length);

        Debug.Log($"[LootContainer] Roll containerId={containerId}, table={cdef.lootTableId}, rollCount={rollCount}, slots={lootSlots.Length}");

        var pool = new List<LootDataManager.LootEntry>(tdef.entries);

        for (int i = 0; i < rollCount; i++)
        {
            if (pool.Count == 0) break;

            int pickedIndex = PickWeightedIndex(pool);
            if (pickedIndex < 0) break;

            var picked = pool[pickedIndex];
            int count = Random.Range(picked.minCount, picked.maxCount + 1);

            Debug.Log($"[LootContainer]  -> slot[{i}] itemId={picked.itemId}, count={count}");

            lootSlots[i].SetData(playerInventoryManagerGO, picked.itemId, count);

            if (tdef.allowDuplicate == 0)
                pool.RemoveAt(pickedIndex);
        }

        // ✅ BG식 반영(초기 롤 단계)
        if (enableBGStyleLoot)
        {
            ApplyBGStyleVisibility();
            CompactAndRefreshIfNeeded(force: true);
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
        if (Time.unscaledTime < _nextBgSyncTime) return;

        _nextBgSyncTime = Time.unscaledTime + Mathf.Max(0.02f, bgSyncInterval);
        CompactAndRefreshIfNeeded(force: false);
    }

    // =========================
    // ✅ BG식(빈칸 숨김 + 아이템 당김 정렬)
    // GetItem 스크립트는 건드리지 않고, 현재 슬롯 상태를 읽어서 재배치함
    // =========================
    private void CompactAndRefreshIfNeeded(bool force)
    {
        if (lootSlots == null || lootSlots.Length == 0) return;
        if (playerInventoryManagerGO == null) return;

        int snapshotHash = ComputeSnapshotHash();
        if (!force && snapshotHash == _lastSnapshotHash) return;
        _lastSnapshotHash = snapshotHash;

        // 1) 현재 슬롯에서 아이템만 뽑기
        var items = new List<(int id, int count)>(lootSlots.Length);
        for (int i = 0; i < lootSlots.Length; i++)
        {
            var slot = lootSlots[i];
            if (slot == null) continue;

            int id = ReadIntField(slot, "InsertID", "insertID", "itemId", "slotIndex");
            int count = ReadIntField(slot, "InsertItemCount", "insertItemCount", "count", "itemCount");

            if (id != 0 && count > 0)
                items.Add((id, count));
        }

        // 2) 위에서부터 다시 채우기 (정렬 = 빈칸 제거)
        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] == null) continue;
            if (i < items.Count)
                lootSlots[i].SetData(playerInventoryManagerGO, items[i].id, items[i].count);
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
}