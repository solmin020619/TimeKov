using UnityEngine;

public class SlotInfo : MonoBehaviour
{
    [SerializeField] public int slotIndex;

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    public void SlotClick()
    {
        if (slotIndex == 0)
        {
            Debug.Log("노아이템");
        }
        else
        {
            Debug.Log("아이템 이름 이름 :" + DataManager.Instance.GetItem(slotIndex).itemName);
            Debug.Log("아이템 이름 설명 :" + DataManager.Instance.GetItem(slotIndex).description);
            Debug.Log("아이콘 이미지 파일 이름 :" + DataManager.Instance.GetItem(slotIndex).iconImange);
        }

    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
