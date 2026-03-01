using System.Collections.Generic;
using UnityEngine;
using TMPro; // ✅ 인벤토리 5/10 표시용

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

    // ✅ 왼쪽 위 "인벤토리 5/10" 텍스트
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

                // 배그식(B): 빈 슬롯은 시작부터 안 보이게
                newSlotScript.gameObject.SetActive(false);
            }

            DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
            if (dc != null) dc.invenManager = this;

            SlotRightClick rc = newSlot.GetComponent<SlotRightClick>();
            if (rc != null) rc.ownerManager = this;

            SlotData.Add(newSlotScript);
            newSlot.name = $"Slot_{i}";
        }

        ApplyBGSyleVisibility();
    }

    // =========================
    // ✅ 스택 규칙
    // duplicated==1이면 overlapsCount까지, 아니면 1
    // =========================
    private int GetMaxStackSize(int itemId)
    {
        var item = (DataManager.Instance != null) ? DataManager.Instance.GetItem(itemId) : null;
        if (item == null) return 1;

        if (item.duplicated == 1)
            return Mathf.Max(1, item.overlapsCount);

        return 1;
    }

    // target 인벤에 스택 규칙 적용해서 넣고, 남으면 remaining 반환
    private int AddItemWithStacking(InventoryManager target, int itemId, int addCount)
    {
        if (target == null || addCount <= 0) return addCount;

        int maxStack = target.GetMaxStackSize(itemId);

        // 1) 기존 스택 채우기
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

        // 2) 빈 슬롯에 새 스택 생성
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

    // ✅ 외부(상점 판매/이동 등)에서 강제 UI 최신화
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

    // ✅ 드랍/루팅에서 사용할 "안전 Add" (부분 성공/실패 판정 가능)
    // - 기존 AddItem()은 그대로 둠 (절대 수정 X)
    // - 내부 스택 규칙(AddItemWithStacking)을 그대로 사용함
    public int TryAddItemFromLoot(int insertItemID, int count)
    {
        if (count <= 0) return 0;

        // 기존 스택/빈슬롯 채우기 로직 그대로 사용
        int remaining = AddItemWithStacking(this, insertItemID, count);

        // UI 갱신도 기존 방식 그대로
        ApplyBGSyleVisibility();

        return remaining; // 0이면 전부 성공, >0이면 그만큼 못 넣음
    }

    public void SortInventory()
    {
        List<ItemData> tempList = new List<ItemData>();

        foreach (SlotInfo slot in SlotData)
            tempList.Add(new ItemData(slot.slotIndex, slot.itemCount));

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

            // 표시/숨김은 ApplyBGSyleVisibility가 결정
            if (!SlotData[i].gameObject.activeSelf) SlotData[i].gameObject.SetActive(true);
        }

        Debug.Log("인벤토리 정렬 완료!");
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

    // 버튼 Missing 복구용 (기존 유지)
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
            // 줄어든 범위의 아이템 제거
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
                    newSlotScript.gameObject.SetActive(false); // 배그식: 빈칸 숨김
                }

                DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
                if (dc != null) dc.invenManager = this;

                SlotRightClick rc = newSlot.GetComponent<SlotRightClick>();
                if (rc != null) rc.ownerManager = this;

                SlotData.Add(newSlotScript);
                newSlot.name = $"Slot_{i}";
            }
        }

        targetSlotCount = newSlotCount;
        ApplyBGSyleVisibility();
    }

    // =========================
    // ✅ 배그식(B): 빈 슬롯은 안 보이게 + ✅ 인벤 숫자 갱신
    // =========================
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

        // ✅ 여기서 같이 갱신
        RefreshInventoryCountText();
    }

    // ✅ 사용중 슬롯 개수
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

    // ✅ 최대 슬롯 개수(현재 용량)
    private int GetMaxSlotCount()
    {
        return (SlotData != null && SlotData.Count > 0) ? SlotData.Count : targetSlotCount;
    }

    // ✅ 외부에서도 호출 가능하게 public
    public void RefreshInventoryCountText()
    {
        if (ownerType != InventoryOwnerType.Player) return;
        if (inventoryCountText == null) return;

        int used = GetUsedSlotCount();
        int max = GetMaxSlotCount();
        inventoryCountText.text = $"인벤토리 {used}/{max}";
    }

    // =========================================================
    // ✅ Ammo 연동용 유틸 3종 (추가 / 기존 로직 삭제 없음)
    // =========================================================

    // 전체 수량 합
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

    // 부분소비 금지: amount 전부 가능할 때만 소비
    public bool TryConsumeItem(int itemId, int amount)
    {
        if (itemId == 0) return false;
        if (amount <= 0) return true;
        if (SlotData == null) return false;

        // 1) 먼저 충분한지 검사
        int total = GetTotalItemCount(itemId);
        if (total < amount) return false;

        // 2) 소비(여러 슬롯에 걸쳐 차감)
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

        // 3) UI 갱신 (기존 방식 유지)
        ApplyBGSyleVisibility();
        return true;
    }
}