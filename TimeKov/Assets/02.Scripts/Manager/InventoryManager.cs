using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


public class InventoryManager : MonoBehaviour
{
    [SerializeField] private GameObject slot;
    [SerializeField] private Transform contentTransform;

    [SerializeField] private int targetSlotCount = 30;

    List<SlotInfo> SlotData = new List<SlotInfo>();
  
    void Start()
    {
        Createslot();
    }

    
    void Update()
    {
        
    }
    public void Createslot()
    {
        
        for(int i = 0; i < targetSlotCount; i++)
        {
            GameObject newslot = Instantiate(slot, contentTransform);

            SlotInfo newSlotScripts = newslot.GetComponent<SlotInfo>();

            newSlotScripts.SetSlotIndex(0);

            SlotData.Add(newSlotScripts);

            

          

            newslot.name = $"slot_{i}";
        }

    }

    public void ChangeIndex(int insertItemID)
    {
        // 리스트의 0번부터 끝(Count)까지 하나씩 검사
        for (int i = 0; i < targetSlotCount; i++)
        {
            //  i번째 칸에 있는 슬롯을 가져옵니다.
            SlotInfo currentSlot = SlotData[i];

            //그 슬롯이 가진 번호(slotIndex)가 우리가 찾는 값 확인
            if (currentSlot.slotIndex == 0)
            {

                currentSlot.SetSlotIndex(insertItemID);

                //Debug.Log($"리스트 {i}번째 칸에서 인덱스를{1101}로 바꿨습니다!");

                break;
            }
        }
    }
}
