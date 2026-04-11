using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineSlotWidget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;

        private Action _onClick;
        private Action _onDoubleClick;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        public void Setup(int itemId, int amount)
        {
            var row = DataStore.GetItem(itemId);
            var sprite = Resources.Load<Sprite>("Icon/" + itemId);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }

            if (itemNameText != null)
                itemNameText.text = row?.itemName ?? itemId.ToString();

            if (amountText != null)
                amountText.text = amount > 1 ? $"x{amount}" : "";
        }

        public void SetClickAction(Action a) => _onClick = a;
        public void SetDoubleClickAction(Action a) => _onDoubleClick = a;

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DoubleClickThreshold)
            {
                _onDoubleClick?.Invoke();
                _lastClickTime = 0f;
            }
            else
            {
                _onClick?.Invoke();
                _lastClickTime = now;
            }
        }
    }
}