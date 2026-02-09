// InventoryManager.cs (기존 유지 + ✅ MoveAllItemsTo 복구만 추가)
using System.Collections.Generic;
using UnityEngine;

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
        CreateSlots();
    }

    void Update()
    {
        if (ownerType == InventoryOwnerType.Player && Input.GetKeyDown(KeyCode.I))
        {
            if (UIStateManager.Instance != null)
            {
                UIStateManager.Instance.ToggleInventory();
            }
        }
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
            SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();

            if (newSlotScript != null)
            {
                newSlotScript.ownerType = (ownerType == InventoryOwnerType.Player)
                    ? SlotInfo.SlotOwnerType.Inventory
                    : SlotInfo.SlotOwnerType.Warehouse;

                newSlotScript.SetSlot(0, 0);
            }

            DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
            if (dc != null)
            {
                dc.invenManager = this;
            }

            SlotRightClick rc = newSlot.GetComponent<SlotRightClick>();
            if (rc != null)
            {
                rc.ownerManager = this;
            }

            SlotData.Add(newSlotScript);
            newSlot.name = $"Slot_{i}";
        }
    }

    public void AddItem(int insertItemID, int count = 1)
    {
        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i].slotIndex != 0 && SlotData[i].slotIndex == insertItemID)
            {
                int currentCount = SlotData[i].itemCount;
                SlotData[i].SetSlot(insertItemID, currentCount + count);
                Debug.Log($"슬롯 {i}번: {insertItemID}번 아이템 개수 증가 -> {currentCount + count}개");
                return;
            }
        }

        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i].slotIndex == 0)
            {
                SlotData[i].SetSlot(insertItemID, count);
                Debug.Log($"슬롯 {i}번: 빈 칸에 {insertItemID}번 아이템 {count}개 신규 등록");
                return;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다!");
    }

    public void SortInventory()
    {
        List<ItemData> tempList = new List<ItemData>();

        foreach (SlotInfo slot in SlotData)
        {
            tempList.Add(new ItemData(slot.slotIndex, slot.itemCount));
        }

        tempList.Sort((a, b) =>
        {
            if (a.id == 0 && b.id == 0) return 0;
            if (a.id == 0) return 1;
            if (b.id == 0) return -1;
            return a.id.CompareTo(b.id);
        });

        for (int i = 0; i < SlotData.Count; i++)
        {
            SlotData[i].SetSlot(tempList[i].id, tempList[i].count);
        }

        Debug.Log("인벤토리 정렬 완료!");
    }

    public void MoveItemByDoubleClick(SlotInfo slot)
    {
        if (slot == null || slot.slotIndex == 0) return;

        if (UIStateManager.Instance != null)
        {
            var state = UIStateManager.Instance.GetCurrentState();
            if (state != UIStateManager.UIState.Inventory)
                return;

            if (UIStateManager.Instance.enableWarehouseInInventory == false)
                return;
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
            }
            return;
        }
    }

    private bool TryAddItemTo(InventoryManager target, int id, int count)
    {
        if (target == null) return false;

        for (int i = 0; i < target.SlotData.Count; i++)
        {
            if (target.SlotData[i] != null &&
                target.SlotData[i].slotIndex != 0 &&
                target.SlotData[i].slotIndex == id)
            {
                int currentCount = target.SlotData[i].itemCount;
                target.SlotData[i].SetSlot(id, currentCount + count);
                return true;
            }
        }

        for (int i = 0; i < target.SlotData.Count; i++)
        {
            if (target.SlotData[i] != null && target.SlotData[i].slotIndex == 0)
            {
                target.SlotData[i].SetSlot(id, count);
                return true;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다!");
        return false;
    }

    // ✅✅✅ [복구] UI 버튼에서 Missing 뜨던 함수 (InventoryManager.MoveAllItemsTo)
    // 버튼 OnClick에 "InventoryManager.MoveAllItemsTo(InventoryManager target)"로 다시 연결하면 됨.
    public void MoveAllItemsTo(InventoryManager targetInventory)
    {
        // 기지(Inventory 상태 + 창고 켜짐)에서만 동작
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
            if (moved)
            {
                s.SetSlot(0, 0);
            }
        }
    }

    int GetBagBonus(int bagId)
    {
        switch (bagId)
        {
            case 4101: return 32;
            case 4102: return 24;
            case 4103: return 16;
            default: return 0;
        }
    }

    public void ApplyBagById(int bagId)
    {
        if (ownerType != InventoryOwnerType.Player)
            return;

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
                if (SlotData[i] != null)
                    Destroy(SlotData[i].gameObject);

                SlotData.RemoveAt(i);
            }
        }
        else if (newSlotCount > currentCount)
        {
            for (int i = currentCount; i < newSlotCount; i++)
            {
                GameObject newSlot = Instantiate(slotPrefab, contentTransform);
                SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();

                if (newSlotScript != null)
                {
                    newSlotScript.ownerType = (ownerType == InventoryOwnerType.Player)
                        ? SlotInfo.SlotOwnerType.Inventory
                        : SlotInfo.SlotOwnerType.Warehouse;

                    newSlotScript.SetSlot(0, 0);
                }

                DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
                if (dc != null)
                {
                    dc.invenManager = this;
                }

                SlotRightClick rc = newSlot.GetComponent<SlotRightClick>();
                if (rc != null)
                {
                    rc.ownerManager = this;
                }

                SlotData.Add(newSlotScript);
                newSlot.name = $"Slot_{i}";
            }
        }

        targetSlotCount = newSlotCount;
    }
}
