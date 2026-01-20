using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("UI 제어")]
    public GameObject inventoryUI; //인벤 활성비활성 테스트용
    public GameObject DropItemUI; //드랍 활성 비활성 테스트용

    [Header("설정")]
    public GameObject slotPrefab;       // 복사할 슬롯 프리팹
    public Transform contentTransform;  // 슬롯이 들어갈 부모 (Content)

    [Header("인벤 슬롯")]
    public int targetSlotCount = 30;          // 생성할 슬롯 개수

    private List<SlotInfo> SlotData = new List<SlotInfo>();

    [System.Serializable]
    public class ItemData
    {
        public int id;
        public int count;
        public ItemData(int _id, int _count) { id = _id; count = _count; } //생성자 초기화
    }
    void Start()
    {
        CreateSlots();

        /*
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
        }
        */

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
            // activeSelf: 현재 활성화 상태 (true/false)
            // !activeSelf: 반대 값 (true -> false, false -> true)
            inventoryUI.SetActive(!inventoryUI.activeSelf);
        }
    }

    // 외부에서도 호출할 수 있도록 public으로 만듦
    public void CreateSlots()
    {
        for (int i = 0; i < targetSlotCount; i++)
        {
            // 프리팹 생성 부모를 Content
            GameObject newSlot = Instantiate(slotPrefab, contentTransform);
            // 생성된 오브젝트에서 Slot 스크립트 가져오기
            SlotInfo newSlotScript = newSlot.GetComponent<SlotInfo>();
            // 인덱스 번호 부여하기
            newSlotScript.SetSlot(0, 0);
            // 관리 리스트에 추가 
            SlotData.Add(newSlotScript);
            // 슬롯의 이름
            newSlot.name = $"Slot_{i}";
        }
    }

    public void AddItem(int insertItemID, int count = 1)
    {
        // 이미 같은 아이템이 있는지 검사 (중복 쌓기)
        for (int i = 0; i < SlotData.Count; i++)
        {
            // 빈 슬롯(0)이 아니고, ID가 같다면
            if (SlotData[i].slotIndex != 0 && SlotData[i].slotIndex == insertItemID)
            {
                int currentCount = SlotData[i].itemCount; // 기존 개수에 더하기
                SlotData[i].SetSlot(insertItemID, currentCount + count);

                Debug.Log($"슬롯 {i}번: {insertItemID}번 아이템 개수 증가 -> {currentCount + count}개");
                return;
            }
        }

        // 중복 아이템이 없다면 빈 슬롯(ID가 0인 곳) 찾기
        for (int i = 0; i < SlotData.Count; i++)
        {
            if (SlotData[i].slotIndex == 0) // 빈 슬롯 발견
            {
                // 새 아이템 등록 (ID와 개수 설정)
                SlotData[i].SetSlot(insertItemID, count);

                Debug.Log($"슬롯 {i}번: 빈 칸에 {insertItemID}번 아이템 {count}개 신규 등록");
                return;
            }
        }

        Debug.Log("인벤토리가 가득 찼습니다!");
    }


    public void SortInventory()
    {
        // 데이터를 가져와서 임시 리스트에 담기
        List<ItemData> tempList = new List<ItemData>();

        foreach (SlotInfo slot in SlotData)
        {
            tempList.Add(new ItemData(slot.slotIndex, slot.itemCount));
        }

        // 리스트 정렬
        tempList.Sort((a, b) =>
        {
            // 둘 다 0(빈칸)이면 순서 안 바꿈
            if (a.id == 0 && b.id == 0) return 0;

            // A가 0이면(빈칸이면) 뒤로 보냄
            if (a.id == 0) return 1;

            //  B가 0이면(빈칸이면) A를 앞으로 보냄
            if (b.id == 0) return -1;

            // 둘 다 아이템이 있으면 ID 기준 오름차순
            return a.id.CompareTo(b.id);
        });

        // 슬롯정리
        for (int i = 0; i < SlotData.Count; i++)
        {
            SlotData[i].SetSlot(tempList[i].id, tempList[i].count); // 슬롯 갱신
        }

        Debug.Log("인벤토리 정렬 완료!");
    }

}