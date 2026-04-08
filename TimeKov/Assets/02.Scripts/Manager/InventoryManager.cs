using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public enum InventoryOwnerType { Player, Warehouse }

    [Header("인벤 타입(가방 영향 여부)")]
    public InventoryOwnerType ownerType = InventoryOwnerType.Player;

    [Header("UI 제어")]
    public GameObject inventoryUI;
    public GameObject DropItemUI;

    [Header("설정")]
    public GameObject slotPrefab;
    public Transform contentTransform;

    [Header("인벤 슬롯")]
    public int baseSlotCount = 10;
    public int targetSlotCount = 30;

    [Header("연결(플레이어 인벤에서만 사용)")]
    public InventoryManager warehouseInventory;

    [Header("연결(창고 인벤에서만 사용)")]
    public InventoryManager playerInventory;

    [Header("UI 텍스트(플레이어 인벤)")]
    [SerializeField] private TMP_Text inventoryCountText;

    private List<SlotInfo> SlotData = new List<SlotInfo>();

    [System.Serializable]
    public class ItemData
    {
        public int id;
        public int count;
        public ItemData(int _id, int _count) { id = _id; count = _count; }
    }

    void Start()
    {
        EnsureDataStoreLoaded();
        CreateSlots();
        ApplyBGSyleVisibility();
    }

    void Update()
    {
        if (ownerType == InventoryOwnerType.Player && Input.GetKeyDown(KeyCode.Tab))
        {
            if (UIStateManager.Instance != null)
                UIStateManager.Instance.ToggleInventory();
        }
    }

    private void EnsureDataStoreLoaded()
    {
        if (!DataStore.IsLoaded)
            DataStore.LoadAll();
    }

    public void ToggleInventory()
    {
        if (inventoryUI != null)
        {
            bool next = !inventoryUI.activeSelf;
            inventoryUI.SetActive(next);

            if (ownerType == InventoryOwnerType.Player &&
                warehouseInventory != null &&
                warehouseInventory.inventoryUI != null)
            {
                warehouseInventory.inventoryUI.SetActive(next);
            }
        }
    }

    public void CreateSlots()
    {
        if (SlotData.Count > 0) return;

        for (int i = 0; i < targetSlotCount; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, contentTransform);
            if (!newSlot.activeSelf) newSlot.SetActive(true);

            SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();
            if (newSlotScript != null)
            {
                newSlotScript.ownerType = (ownerType == InventoryOwnerType.Player)
                    ? SlotInfo.SlotOwnerType.Inventory
                    : SlotInfo.SlotOwnerType.Warehouse;

                newSlotScript.SetSlot(0, 0);
                newSlotScript.gameObject.SetActive(false);
            }

            SlotInputHandler input = newSlot.GetComponent<SlotInputHandler>();
            if (input != null)
            {
                input.invenManager = this;
                input.ownerManager = this;
            }

            SlotData.Add(newSlotScript);
            newSlot.name = $"Slot_{i}";
        }

        ApplyBGSyleVisibility();
    }

    private int GetMaxStackSize(int itemId)
    {
        EnsureDataStoreLoaded();

        ItemRow item = DataStore.GetItem(itemId);
        if (item == null) return 1;

        if (item.stackable == 1)
            return Mathf.Max(1, item.maxStack);

        return 1;
    }

    private int AddItemWithStacking(InventoryManager target, int itemId, int addCount)
    {
        if (target == null || addCount <= 0) return addCount;

        int maxStack = target.GetMaxStackSize(itemId);

        for (int i = 0; i < target.SlotData.Count && addCount > 0; i++)
        {
            var s = target.SlotData[i];
            if (s == null) continue;
            if (s.slotIndex != itemId) continue;

            int space = maxStack - s.itemCount;
            if (space <= 0) continue;

            int put = Mathf.Min(space, addCount);
            s.SetSlot(itemId, s.itemCount + put);
            addCount -= put;
        }

        for (int i = 0; i < target.SlotData.Count && addCount > 0; i++)
        {
            var s = target.SlotData[i];
            if (s == null) continue;
            if (s.slotIndex != 0) continue;

            int put = Mathf.Min(maxStack, addCount);
            s.SetSlot(itemId, put);
            addCount -= put;
        }

        target.ApplyBGSyleVisibility();
        return addCount;
    }

    public void ForceRefreshUI()
    {
        SortInventory();
        ApplyBGSyleVisibility();
    }

    public void AddItem(int insertItemID, int count = 1)
    {
        if (count <= 0) return;

        int remaining = AddItemWithStacking(this, insertItemID, count);

        if (remaining > 0)
            Debug.Log($"인벤토리가 가득 찼습니다! 남은 수량: {remaining}");

        ApplyBGSyleVisibility();
    }

    public int TryAddItemFromLoot(int insertItemID, int count)
    {
        if (count <= 0) return 0;

        int remaining = AddItemWithStacking(this, insertItemID, count);
        ApplyBGSyleVisibility();

        return remaining;
    }

    public void SortInventory()
    {
        Dictionary<int, int> totals = new Dictionary<int, int>();
        for (int i = 0; i < SlotData.Count; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;

            int id = s.slotIndex;
            int c = s.itemCount;

            if (id <= 0 || c <= 0) continue;

            if (totals.ContainsKey(id)) totals[id] += c;
            else totals[id] = c;
        }

        List<int> ids = new List<int>(totals.Keys);
        ids.Sort();

        List<ItemData> packed = new List<ItemData>(SlotData.Count);

        for (int k = 0; k < ids.Count; k++)
        {
            int id = ids[k];
            int remaining = totals[id];

            int maxStack = GetMaxStackSize(id);
            if (maxStack <= 0) maxStack = 1;

            while (remaining > 0)
            {
                int put = Mathf.Min(maxStack, remaining);
                packed.Add(new ItemData(id, put));
                remaining -= put;
            }
        }

        while (packed.Count < SlotData.Count)
            packed.Add(new ItemData(0, 0));

        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i] == null) continue;

            SlotData[i].SetSlot(packed[i].id, packed[i].count);

            if (!SlotData[i].gameObject.activeSelf) SlotData[i].gameObject.SetActive(true);
        }

        Debug.Log("인벤토리 정렬(스택 합치기 포함) 완료!");
        ApplyBGSyleVisibility();
    }

    public void MoveItemByDoubleClick(SlotInfo slot)
    {
        if (slot == null || slot.slotIndex == 0) return;

        if (UIStateManager.Instance != null)
        {
            var state = UIStateManager.Instance.GetCurrentState();
            if (state != UIStateManager.UIState.Inventory) return;
            if (UIStateManager.Instance.enableWarehouseInInventory == false) return;
        }

        int id = slot.slotIndex;
        int count = slot.itemCount;

        if (slot.ownerType == SlotInfo.SlotOwnerType.Inventory)
        {
            if (ownerType != InventoryOwnerType.Player) return;
            if (warehouseInventory == null) return;

            warehouseInventory.CreateSlots();

            bool moved = TryAddItemTo(warehouseInventory, id, count);
            if (moved)
            {
                slot.SetSlot(0, 0);
                ApplyBGSyleVisibility();
            }
            return;
        }

        if (slot.ownerType == SlotInfo.SlotOwnerType.Warehouse)
        {
            if (ownerType != InventoryOwnerType.Warehouse) return;
            if (playerInventory == null) return;

            playerInventory.CreateSlots();

            bool moved = TryAddItemTo(playerInventory, id, count);
            if (moved)
            {
                slot.SetSlot(0, 0);
                ApplyBGSyleVisibility();
            }
            return;
        }
    }

    private bool TryAddItemTo(InventoryManager target, int id, int count)
    {
        if (target == null) return false;

        int remaining = AddItemWithStacking(target, id, count);
        if (remaining > 0)
        {
            Debug.Log($"인벤토리가 가득 찼습니다! 남은 수량: {remaining}");
            target.ApplyBGSyleVisibility();
            return false;
        }

        target.ApplyBGSyleVisibility();
        return true;
    }

    public void MoveAllItemsTo(InventoryManager targetInventory)
    {
        if (UIStateManager.Instance != null)
        {
            var state = UIStateManager.Instance.GetCurrentState();
            if (state != UIStateManager.UIState.Inventory) return;
            if (UIStateManager.Instance.enableWarehouseInInventory == false) return;
        }

        if (targetInventory == null) return;
        targetInventory.CreateSlots();

        for (int i = 0; i < SlotData.Count; i++)
        {
            SlotInfo s = SlotData[i];
            if (s == null) continue;
            if (s.slotIndex == 0) continue;

            int id = s.slotIndex;
            int count = s.itemCount;

            bool moved = TryAddItemTo(targetInventory, id, count);
            if (moved) s.SetSlot(0, 0);
        }

        ApplyBGSyleVisibility();
    }

    // 기존 API 유지:
    // EquipmentManager, 세션 복원 등에서 그대로 호출 가능하게 두고
    // 내부만 DataStore 기반으로 교체
    int GetBagBonus(int bagId)
    {
        if (bagId <= 0)
            return 0;

        EnsureDataStoreLoaded();

        EquipmentRow equip = DataStore.GetEquipment(bagId);
        if (equip == null)
            return 0;

        string equipType = (equip.equipType ?? string.Empty).Trim().ToLowerInvariant();
        if (equipType != "bag" && equipType != "backpack")
            return 0;

        return Mathf.Max(0, equip.addSlotCount);
    }

    public void ApplyBagById(int bagId)
    {
        if (ownerType != InventoryOwnerType.Player) return;

        int newCount = baseSlotCount + GetBagBonus(bagId);
        ResizeInventory(newCount);
    }

    public void ResizeInventory(int newSlotCount)
    {
        if (newSlotCount < 0) newSlotCount = 0;

        if (SlotData == null || SlotData.Count == 0)
        {
            targetSlotCount = newSlotCount;
            return;
        }

        SortInventory();

        int currentCount = SlotData.Count;

        if (newSlotCount < currentCount)
        {
            for (int i = newSlotCount; i < currentCount; i++)
            {
                if (SlotData[i] != null && SlotData[i].slotIndex != 0)
                    SlotData[i].SetSlot(0, 0);
            }

            for (int i = currentCount - 1; i >= newSlotCount; i--)
            {
                if (SlotData[i] != null) Destroy(SlotData[i].gameObject);
                SlotData.RemoveAt(i);
            }
        }
        else if (newSlotCount > currentCount)
        {
            for (int i = currentCount; i < newSlotCount; i++)
            {
                GameObject newSlot = Instantiate(slotPrefab, contentTransform);
                if (!newSlot.activeSelf) newSlot.SetActive(true);

                SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();
                if (newSlotScript != null)
                {
                    newSlotScript.ownerType = (ownerType == InventoryOwnerType.Player)
                        ? SlotInfo.SlotOwnerType.Inventory
                        : SlotInfo.SlotOwnerType.Warehouse;

                    newSlotScript.SetSlot(0, 0);
                    newSlotScript.gameObject.SetActive(false);
                }

                SlotInputHandler input = newSlot.GetComponent<SlotInputHandler>();
                if (input != null)
                {
                    input.invenManager = this;
                    input.ownerManager = this;
                }

                SlotData.Add(newSlotScript);
                newSlot.name = $"Slot_{i}";
            }
        }

        targetSlotCount = newSlotCount;
        ApplyBGSyleVisibility();
    }

    private void ApplyBGSyleVisibility()
    {
        if (SlotData == null) return;

        for (int i = 0; i < SlotData.Count; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;

            bool shouldShow = (s.slotIndex != 0 && s.itemCount > 0);
            if (s.gameObject.activeSelf != shouldShow)
                s.gameObject.SetActive(shouldShow);
        }

        RefreshInventoryCountText();
    }

    private int GetUsedSlotCount()
    {
        if (SlotData == null) return 0;

        int used = 0;
        for (int i = 0; i < SlotData.Count; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;
            if (s.slotIndex != 0 && s.itemCount > 0) used++;
        }
        return used;
    }

    private int GetMaxSlotCount()
    {
        return (SlotData != null && SlotData.Count > 0) ? SlotData.Count : targetSlotCount;
    }

    public void RefreshInventoryCountText()
    {
        if (ownerType != InventoryOwnerType.Player) return;
        if (inventoryCountText == null) return;

        int used = GetUsedSlotCount();
        int max = GetMaxSlotCount();
        inventoryCountText.text = $"인벤토리 {used}/{max}";
    }

    public int GetTotalItemCount(int itemId)
    {
        if (itemId == 0) return 0;
        if (SlotData == null) return 0;

        int total = 0;
        for (int i = 0; i < SlotData.Count; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;
            if (s.slotIndex == itemId && s.itemCount > 0)
                total += s.itemCount;
        }
        return total;
    }

    public bool HasItem(int itemId, int atLeast)
    {
        if (atLeast <= 0) return true;
        return GetTotalItemCount(itemId) >= atLeast;
    }

    public bool TryConsumeItem(int itemId, int amount)
    {
        if (itemId == 0) return false;
        if (amount <= 0) return true;
        if (SlotData == null) return false;

        int total = GetTotalItemCount(itemId);
        if (total < amount) return false;

        int remain = amount;

        for (int i = 0; i < SlotData.Count && remain > 0; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;
            if (s.slotIndex != itemId) continue;
            if (s.itemCount <= 0) continue;

            int take = Mathf.Min(s.itemCount, remain);
            int next = s.itemCount - take;

            if (next <= 0)
                s.SetSlot(0, 0);
            else
                s.SetSlot(itemId, next);

            remain -= take;
        }

        ApplyBGSyleVisibility();
        return true;
    }

    public PlayerSessionData.InventorySnapshot ExportToSessionSnapshot()
    {
        var snap = new PlayerSessionData.InventorySnapshot();
        snap.ownerType = (int)ownerType;

        CreateSlots();

        snap.slotCount = SlotData.Count;
        snap.ids.Clear();
        snap.counts.Clear();

        for (int i = 0; i < SlotData.Count; i++)
        {
            var s = SlotData[i];
            if (s == null)
            {
                snap.ids.Add(0);
                snap.counts.Add(0);
                continue;
            }

            snap.ids.Add(s.slotIndex);
            snap.counts.Add(s.itemCount);
        }

        return snap;
    }

    public void ImportFromSessionSnapshot(PlayerSessionData.InventorySnapshot snap, EquipmentManager equipManager)
    {
        if (snap == null) return;

        CreateSlots();

        if (ownerType == InventoryOwnerType.Player && equipManager != null)
        {
            int bagId = equipManager.GetEquippedBagId();
            ApplyBagById(bagId);
        }

        int desired = Mathf.Max(0, snap.slotCount);
        if (SlotData.Count != desired)
            ResizeInventory(desired);

        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i] == null) continue;
            SlotData[i].SetSlot(0, 0);
        }

        int n = Mathf.Min(SlotData.Count, snap.ids.Count);
        for (int i = 0; i < n; i++)
        {
            var s = SlotData[i];
            if (s == null) continue;

            int id = snap.ids[i];
            int c = (i < snap.counts.Count) ? snap.counts[i] : 0;

            if (id <= 0 || c <= 0)
                s.SetSlot(0, 0);
            else
                s.SetSlot(id, c);
        }

        ForceRefreshUI();
    }
}