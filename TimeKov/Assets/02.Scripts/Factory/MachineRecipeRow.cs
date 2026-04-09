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

            BuildIconRow(inputIconParent,  recipe.inputs);
            BuildIconRow(outputIconParent, recipe.outputs);
        }

        private void BuildIconRow(Transform parent, FactorySlot[] slots)
        {
            if (parent == null || iconSlotPrefab == null || slots == null) return;

            foreach (Transform child in parent) Destroy(child.gameObject);

            foreach (var slot in slots)
            {
                var go   = Instantiate(iconSlotPrefab, parent);
                var icon = go.GetComponentInChildren<Image>();
                var text = go.GetComponentInChildren<TextMeshProUGUI>();

                var row    = DataStore.GetItem(slot.itemId);
                var sprite = row != null ? Resources.Load<Sprite>("Icon/" + row.iconKey) : null;

                if (icon != null)
                {
                    icon.sprite  = sprite;
                    icon.enabled = sprite != null;
                }

                if (text != null)
                {
                    string nm = row?.itemName ?? slot.itemId.ToString();
                    text.text  = slot.amount > 1 ? $"{nm}\nx{slot.amount}" : nm;
                }
            }
        }
    }
}
