// =====================================================================
// MachineRecipeRow.cs
// MachineUI 레시피 영역에 조합식 한 줄을 표시하는 위젯.
// 재료 아이콘들 → [화살표] → 결과물 아이콘들 형태로 표시.
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

            BuildIconRow(inputIconParent,  recipe.inputs);
            BuildIconRow(outputIconParent, recipe.outputs);
        }

        private void BuildIconRow(Transform parent, FactorySlot[] slots)
        {
            if (parent == null || iconSlotPrefab == null || slots == null) return;

            // 기존 자식 제거
            foreach (Transform child in parent)
                Destroy(child.gameObject);

            foreach (var slot in slots)
            {
                var go   = Instantiate(iconSlotPrefab, parent);
                var icon = go.GetComponentInChildren<Image>();
                var text = go.GetComponentInChildren<TextMeshProUGUI>();

                var sprite = Resources.Load<Sprite>("Icon/" + slot.itemId);
                if (icon != null)
                {
                    icon.sprite  = sprite;
                    icon.enabled = sprite != null;
                }

                if (text != null)
                {
                    var item  = DataManager.Instance?.GetItem(slot.itemId);
                    string nm = item != null ? item.itemName : slot.itemId.ToString();
                    text.text = slot.amount > 1 ? $"{nm}\nx{slot.amount}" : nm;
                }
            }
        }
    }
}
