// =====================================================================
// MachineSlotWidget.cs
// 단일 클릭과 더블클릭을 분리 처리하는 슬롯 위젯.
// 클릭   → 1개 투입
// 더블클릭 → 전체 투입 or 회수
// =====================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineSlotWidget : MonoBehaviour,
        IPointerClickHandler
    {
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI amountText;

        private Action _onClick;
        private Action _onDoubleClick;
        private float  _lastClickTime;
        private const float DoubleClickThreshold = 0.3f;

        // ── 세팅 ────────────────────────────────────────────────────

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

        public void SetClickAction(Action a)       => _onClick       = a;
        public void SetDoubleClickAction(Action a) => _onDoubleClick = a;

        // ── 클릭 감지 ───────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            float now = Time.unscaledTime;

            if (now - _lastClickTime < DoubleClickThreshold)
            {
                // 더블클릭
                _onDoubleClick?.Invoke();
                _lastClickTime = 0f; // 연속 트리거 방지
            }
            else
            {
                // 단일클릭 (더블클릭 판정 후가 아닐 때)
                _onClick?.Invoke();
                _lastClickTime = now;
            }
        }
    }
}
