using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotInfo : MonoBehaviour
{
    public int slotIndex; // 아이템 ID
    public int slotOldIndex;
    public int itemCount; // 아이템 개수

    public Image iconImage; // 아이템 아이콘 (자식 Icon)
    public TextMeshProUGUI slotText;
    public TextMeshProUGUI amountText; // 개수 표시 텍스트

    public enum SlotOwnerType
    {
        Inventory,
        Equip,
        Warehouse,
        Loot
    }

    public SlotOwnerType ownerType;



    public void SetSlot(int id, int count)
    {
        slotIndex = id;
        itemCount = count;

        var img = GetComponent<Image>(); // 슬롯 BG (이제 고정)

        if (slotIndex == 0)
        {
            // ❌ 슬롯 BG는 더 이상 건드리지 않음
            // if (img != null) img.sprite = null;

            if (slotText != null) slotText.text = "";
            slotOldIndex = 0;

            // ✅ 아이템 없으면 아이콘 OFF
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.gameObject.SetActive(false);
            }
        }
        else
        {
            // ❌ 슬롯 BG는 더 이상 아이템으로 바꾸지 않음
            // if (img != null) img.sprite = Resources.Load<Sprite>("Icon/" + slotIndex);

            // ✅ 아이템 있으면 아이콘 ON
            if (iconImage != null)
            {
                iconImage.sprite = Resources.Load<Sprite>("Icon/" + slotIndex);
                iconImage.gameObject.SetActive(true);
            }
        }

        UpdateAmountText();
    }

    void UpdateAmountText()
    {
        if (amountText != null)
        {
            if (slotIndex == 0 || itemCount <= 1)
                amountText.text = "";
            else
                amountText.text = itemCount.ToString();
        }
    }

    void Update()
    {
        if (slotIndex == 0) return;

        if (slotIndex != slotOldIndex)
        {
            Debug.Log("slotIndex가 변경되었습니다!");
            slotText.text = DataManager.Instance.GetItem(slotIndex).itemName;
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
            Debug.Log("아이템 이름 :" + DataManager.Instance.GetItem(slotIndex).itemName);
            Debug.Log("아이템 설명 :" + DataManager.Instance.GetItem(slotIndex).description);
            Debug.Log("아이콘 이미지 파일 이름 :" + DataManager.Instance.GetItem(slotIndex).iconImange);
        }
    }
}