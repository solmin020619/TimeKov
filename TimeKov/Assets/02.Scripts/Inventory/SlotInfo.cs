using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotInfo : MonoBehaviour
{


    public int slotIndex; // 아이템 ID
    public int slotOldIndex;
    public int itemCount; // [추가] 아이템 개수


    public Image iconImage; // 연결된 아이콘 이미지
    public TextMeshProUGUI slotText;
    public TextMeshProUGUI amountText; // [추가] 개수 표시 텍스트


    public void SetSlot(int id, int count)
    {
        slotIndex = id;
        itemCount = count;

        if (slotIndex != 0)
        {
            GetComponent<Image>().sprite = Resources.Load<Sprite>("Icon/" + slotIndex);
            //Debug.Log("fadfasdfasdfasdf___" + DataManager.Instance.GetItem(slotIndex).iconImange);
        }

        UpdateAmountText();
    }

    void UpdateAmountText()
    {
        if (amountText != null)
        {
            // 아이템이 없거나(0), 1개일 때는 숫자 숨김
            if (slotIndex == 0 || itemCount <= 1)
            {
                amountText.text = "";
            }
            else
            {
                amountText.text = itemCount.ToString(); // 2개 이상일 때만 숫자 표시
            }
        }
    }

    void Update()
    {
        // 현재 값이 과거 값과 달라졌는지 매 프레임 체크
        if (slotIndex != slotOldIndex)
        {
            Debug.Log("slotIndex가 변경되었습니다!");
            slotText.text = DataManager.Instance.GetItem(slotIndex).itemName;
            // 변경된 후에는 과거 값을 현재 값으로 업데이트
            slotOldIndex = slotIndex;
        }
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
}