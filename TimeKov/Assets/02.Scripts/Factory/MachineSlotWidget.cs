// =====================================================================
// MachineSlotWidget.cs
// MachineUI 내 입력/출력 슬롯 하나를 표현하는 위젯.
// Setup(itemId, amount) 호출 시 DataManager에서 아이콘/이름 자동 조회.
// =====================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineSlotWidget : MonoBehaviour
    {
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button          slotButton;

        public void Setup(int itemId, int amount)
        {
            var item   = DataManager.Instance?.GetItem(itemId);
            var sprite = Resources.Load<Sprite>("Icon/" + itemId);

            if (iconImage != null)
            {
                iconImage.sprite  = sprite;
                iconImage.enabled = sprite != null;
            }

            if (itemNameText != null)
                itemNameText.text = item != null ? item.itemName : itemId.ToString();

            if (amountText != null)
                amountText.text = amount > 1 ? $"x{amount}" : "";
        }

        /// <summary>출력 슬롯은 클릭 시 회수 액션 등록. 입력 슬롯은 null.</summary>
        public void SetClickAction(Action onClick)
        {
            if (slotButton == null) return;
            slotButton.onClick.RemoveAllListeners();

            if (onClick != null)
            {
                slotButton.onClick.AddListener(() => onClick());
                slotButton.interactable = true;
            }
            else
            {
                slotButton.interactable = false;
            }
        }
    }
}
