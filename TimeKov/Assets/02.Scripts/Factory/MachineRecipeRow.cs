// =====================================================================
// MachineRecipeRow.cs
// 설비 레시피 행 UI
// 구버전 DataStore.GetItem 을 새 스키마로 교체
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class MachineRecipeRow : MonoBehaviour
    {
        [Header("재료 아이콘 부모 (가로 Layout Group)")]
        public Transform inputIconParent;

        [Header("결과물 아이콘 부모 (가로 Layout Group)")]
        public Transform outputIconParent;

        [Header("소요 시간 텍스트")]
        public TextMeshProUGUI timeText;

        [Header("아이콘 슬롯 프리팹 (Image + TMP 포함)")]
        public GameObject iconSlotPrefab;

        public void Setup(FactoryRecipe recipe)
        {
            if (timeText != null)
                timeText.text = $"{recipe.processingTime}s";

            BuildIconRow(inputIconParent, recipe.inputs);
            BuildIconRow(outputIconParent, recipe.outputs);
        }

        private void BuildIconRow(Transform parent, FactorySlot[] slots)
        {
            if (parent == null || iconSlotPrefab == null || slots == null) return;

            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var slot in slots)
            {
                var go = Instantiate(iconSlotPrefab, parent);
                var icon = go.GetComponentInChildren<Image>();
                var text = go.GetComponentInChildren<TextMeshProUGUI>();

                // 구버전: DataStore.GetItem(slot.itemId) → 신버전: GameDataHolder.I.ItemData.TryGet
                string itemName = slot.itemId.ToString();
                if (GameDataHolder.I.ItemData.TryGet(slot.itemId.ToString(), out var itemData))
                    itemName = itemData.itemName;

                var sprite = Resources.Load<Sprite>("Icon/" + slot.itemId);

                if (icon != null)
                {
                    icon.sprite = sprite;
                    icon.enabled = sprite != null;
                }

                if (text != null)
                    text.text = slot.amount > 1 ? $"{itemName}\nx{slot.amount}" : itemName;
            }
        }
    }
}