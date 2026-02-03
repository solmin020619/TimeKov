using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // ✅ 이 인벤이 플레이어용인지(가방 영향 받는지) 구분
    public enum InventoryOwnerType { Player, Warehouse }

    [Header("인벤 타입(가방 영향 여부)")]
    public InventoryOwnerType ownerType = InventoryOwnerType.Player;

    [Header("UI 제어")]
    public GameObject inventoryUI; // 인벤 활성/비활성 테스트용
    public GameObject DropItemUI;  // 드랍 활성/비활성 테스트용

    [Header("설정")]
    public GameObject slotPrefab;       // 복사할 슬롯 프리팹
    public Transform contentTransform;  // 슬롯이 들어갈 부모 (Content)

    [Header("인벤 슬롯")]
    public int baseSlotCount = 10;      // 가방 없을 때 기본 슬롯 수
    public int targetSlotCount = 30;    // 생성/유지할 슬롯 개수(현재 상태)

    [Header("연결(플레이어 인벤에서만 사용)")]
    public InventoryManager warehouseInventory; // 플레이어 인벤 -> 창고 이동용

    [Header("연결(창고 인벤에서만 사용)")]
    public InventoryManager playerInventory; // 창고 인벤 -> 플레이어 인벤 이동용

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
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(!inventoryUI.activeSelf);
        }
    }

    // 슬롯 생성
    public void CreateSlots()
    {
        // 중복 생성 방지
        if (SlotData.Count > 0) return;

        for (int i = 0; i < targetSlotCount; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, contentTransform);
            SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();

            // ✅ 핵심: 슬롯의 "소유자 타입"을 인벤 종류에 맞춰 자동 세팅
            if (newSlotScript != null)
            {
                newSlotScript.ownerType = (ownerType == InventoryOwnerType.Player)
                    ? SlotInfo.SlotOwnerType.Inventory
                    : SlotInfo.SlotOwnerType.Warehouse;

                newSlotScript.SetSlot(0, 0);
            }

            // ✅ [추가] 슬롯의 DoubleClickEquip이 InventoryManager를 못 찾는 구조라서 여기서 직접 주입
            DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
            if (dc != null)
            {
                dc.invenManager = this;
            }

            // ✅ [추가] 우클릭 메뉴용 SlotRightClick도 InventoryManager를 못 찾는 구조라서 여기서 직접 주입
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
        // 이미 같은 아이템이 있는지 검사 (중복 쌓기)
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

        // 빈 슬롯 찾기
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

    // =========================
    // 가방 슬롯 가변 로직
    // =========================

    int GetBagBonus(int bagId)
    {
        switch (bagId)
        {
            case 4101: return 32; // Lv.3
            case 4102: return 24; // Lv.2
            case 4103: return 16; // Lv.1
            default: return 0;    // 없음
        }
    }

    public void ApplyBagById(int bagId)
    {
        // ✅ 핵심: 창고는 가방 영향 절대 X
        if (ownerType != InventoryOwnerType.Player)
            return;

        int newCount = baseSlotCount + GetBagBonus(bagId);
        ResizeInventory(newCount);
    }

    // 슬롯 수 변경 (줄어들면 뒤쪽 칸부터 삭제)
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
            // 뒤쪽 아이템 삭제
            for (int i = newSlotCount; i < currentCount; i++)
            {
                if (SlotData[i] != null && SlotData[i].slotIndex != 0)
                    SlotData[i].SetSlot(0, 0);
            }

            // 뒤쪽 슬롯 오브젝트 삭제
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

                // ✅ [추가] 새로 늘어난 슬롯에도 manager 주입
                DoubleClickEquip dc = newSlot.GetComponent<DoubleClickEquip>();
                if (dc != null)
                {
                    dc.invenManager = this;
                }

                // ✅ [추가] 새로 늘어난 슬롯에도 우클릭 메뉴용 SlotRightClick manager 주입
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
        Debug.Log($"인벤 슬롯 수 변경 완료: {currentCount} -> {newSlotCount}");


    }

    // =========================
    // 인벤 -> 창고 전체 이동
    // =========================

    public void MoveAllToWarehouseButton()
    {
        if (warehouseInventory == null)
        {
            Debug.LogWarning("warehouseInventory가 연결 안됨!");
            return;
        }

        MoveAllItemsTo(warehouseInventory);
    }

    public void MoveAllItemsTo(InventoryManager target)
    {
        if (target == null) return;

        for (int i = 0; i < SlotData.Count; i++)
        {
            int id = SlotData[i].slotIndex;
            int count = SlotData[i].itemCount;
            if (id == 0) continue;

            target.AddItem(id, count);
            SlotData[i].SetSlot(0, 0);
        }

        SortInventory();
        target.SortInventory();

        Debug.Log("인벤 -> 창고 전체 이동 완료");
    }

    // =========================
    // 더블클릭 아이템 이동(슬롯 1개)
    // =========================

    public bool MoveItemByDoubleClick(SlotInfo fromSlot)
    {
        if (fromSlot == null) return false;
        if (fromSlot.slotIndex == 0) return false;

        InventoryManager target = null;

        // ✅ 플레이어 인벤 -> 창고
        if (ownerType == InventoryOwnerType.Player)
        {
            target = warehouseInventory;
        }
        // ✅ 창고 -> 플레이어 인벤
        else if (ownerType == InventoryOwnerType.Warehouse)
        {
            target = (playerInventory != null) ? playerInventory : FindPlayerInventoryFallback();
        }

        if (target == null)
            return false;

        int id = fromSlot.slotIndex;
        int count = fromSlot.itemCount;

        bool added = target.TryAddItem(id, count);
        if (!added)
            return false;

        fromSlot.SetSlot(0, 0);



        return true;
    }

    // AddItem()과 동일한 동작을 하되, 성공/실패를 반환(이동 로직용)
    bool TryAddItem(int insertItemID, int count = 1)
    {
        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i].slotIndex != 0 && SlotData[i].slotIndex == insertItemID)
            {
                int currentCount = SlotData[i].itemCount;
                SlotData[i].SetSlot(insertItemID, currentCount + count);
                Debug.Log($"슬롯 {i}번: {insertItemID}번 아이템 개수 증가 -> {currentCount + count}개");
                return true;
            }
        }

        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i].slotIndex == 0)
            {
                SlotData[i].SetSlot(insertItemID, count);
                Debug.Log($"슬롯 {i}번: 빈 칸에 {insertItemID}번 아이템 {count}개 신규 등록");
                return true;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다!");
        return false;
    }

    InventoryManager FindPlayerInventoryFallback()
    {
        InventoryManager[] all = FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].ownerType == InventoryOwnerType.Player)
                return all[i];
        }
        return null;
    }
}
