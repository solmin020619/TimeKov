using System.Collections.Generic;
using UnityEngine;

public class LootContainer : MonoBehaviour
{
    [Header("Drop Info")]
    public int dropKey;
    public string sourceType; // ex) chest, monster

    [Header("UI")]
    [SerializeField] private GameObject lootPanelRoot;
    [SerializeField] private Transform lootContentTransform;
    [SerializeField] private GameObject lootSlotPrefab;

    [Header("Refs")]
    [SerializeField] private InventoryManager playerInventoryManager;

    private List<GameObject> _spawnedSlots = new List<GameObject>();
    private List<LootData> _rolledLoot = new List<LootData>();

    private bool _isOpened = false;

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

    public void Open()
    {
        if (_isOpened)
        {
            Close();
            return;
        }

        RollLoot();
        BuildSlotsFromRolledLoot();

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.ToggleLoot(lootPanelRoot);
        }
        else
        {
            lootPanelRoot.SetActive(true);
        }

        _isOpened = true;
    }

    public void Close()
    {
        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.None);
        }
        else
        {
            lootPanelRoot.SetActive(false);
        }

        _isOpened = false;
    }

    private void RollLoot()
    {
        _rolledLoot.Clear();

        var dropRows = DataStore.GetDropRows(dropKey);

        if (dropRows == null || dropRows.Count == 0)
        {
            Debug.LogWarning($"[LootContainer] drop rows not found. sourceType={sourceType}, sourceId={dropKey}");
            return;
        }

        foreach (var row in dropRows)
        {
            int count = Random.Range(row.minCount, row.maxCount + 1);
            _rolledLoot.Add(new LootData(row.itemId, count));
        }
    }

   
    private void BuildSlotsFromRolledLoot()
    {
        ClearSlots();

        foreach (var data in _rolledLoot)
        {
            GameObject slotGO = Instantiate(lootSlotPrefab, lootContentTransform);

            var slotInfo = slotGO.GetComponent<SlotInfo>();
            if (slotInfo == null)
            {
                Debug.LogError("[LootContainer] SlotInfo 없음");
                continue;
            }

           
            slotInfo.ownerType = SlotInfo.SlotOwnerType.Loot;

           
            slotInfo.SetSlot(data.itemId, data.count);

            _spawnedSlots.Add(slotGO);
        }
    }

  
    private void ClearSlots()
    {
        foreach (var slot in _spawnedSlots)
        {
            if (slot != null)
                Destroy(slot);
        }

        _spawnedSlots.Clear();
    }

   
    public void NotifyLootChanged()
    {
        BuildSlotsFromRolledLoot();
    }
}