// =====================================================================
// MachineSlotWidget.cs
// ���� ��� ���� ����
// ������ DataStore.GetItem �� �� ��Ű���� ��ü
// =====================================================================

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
        [SerializeField] private Image rarityBorder;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;

        // InventorySlotUI 와 동일한 등급 색상 배열
        private static readonly Color[] GradeColors = new Color[]
        {
            new Color(0.60f, 0.60f, 0.60f, 1f),  // Common   - 회색
            new Color(0.30f, 0.55f, 0.90f, 1f),  // Advanced - 파랑
            new Color(0.20f, 0.75f, 0.40f, 1f),  // Rare     - 초록
            new Color(0.65f, 0.30f, 0.90f, 1f),  // Hero     - 보라
            new Color(0.95f, 0.55f, 0.10f, 1f),  // Legend   - 황금
        };

        private Action _onClick;
        private Action _onDoubleClick;
        private float _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        public void Setup(int itemId, int amount)
        {
            var itemData = GameDataUtility.GetItem(itemId);
            string name = itemData != null ? itemData.itemName : itemId.ToString();

            // 아이콘 로드 (ItemDatabase 경로 통일)
            Sprite sprite = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                sprite = ItemDatabase.GetIcon(itemData.iconKey);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                iconImage.enabled = true;
            }

            // 등급 테두리 색상
            if (rarityBorder != null)
            {
                int gradeIndex = itemData != null ? (int)itemData.itemGrade : 0;
                gradeIndex = Mathf.Clamp(gradeIndex, 0, GradeColors.Length - 1);
                rarityBorder.color = GradeColors[gradeIndex];
            }

            if (itemNameText != null)
                itemNameText.text = name;

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