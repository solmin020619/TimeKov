using System.Collections.Generic;
using UnityEngine;

public class LootContainer : MonoBehaviour
{
    public string containerId = "LC_001";

    [Header("UI")]
    public GameObject lootPanelRoot;
    public GetItem[] lootSlots;
    public GameObject playerInventoryManagerGO;

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
    }

    private void ClearSlots()
    {
        if (lootSlots == null) return;
        for (int i = 0; i < lootSlots.Length; i++)
        {
            if (lootSlots[i] != null && playerInventoryManagerGO != null)
                lootSlots[i].SetData(playerInventoryManagerGO, 0, 0);
        }
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
