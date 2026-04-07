// =====================================================================
// BeltItemVisual.cs
// 벨트 위를 이동하는 아이템 오브젝트에 붙이는 컴포넌트.
// DataManager에서 아이콘/이름을 가져와 표시한다.
// =====================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class BeltItemVisual : MonoBehaviour
    {
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;

        // 항상 카메라를 향하게
        private Camera _cam;

        private void Awake() => _cam = Camera.main;

        private void LateUpdate()
        {
            if (_cam != null)
                transform.forward = _cam.transform.forward;
        }

        public void Setup(int itemId, int amount)
        {
            // DataManager에서 아이템 정보 조회
            var item = DataManager.Instance?.GetItem(itemId);

            if (item != null)
            {
                // 아이콘 로드 (SlotInfo와 동일한 경로 규칙)
                var sprite = Resources.Load<Sprite>("Icon/" + itemId);
                if (iconImage != null)
                {
                    iconImage.sprite  = sprite;
                    iconImage.enabled = sprite != null;
                }

                if (nameText  != null) nameText.text  = item.itemName;
                if (countText != null) countText.text  = amount > 1 ? $"x{amount}" : "";
            }
            else
            {
                // DataManager 없을 때 fallback
                if (nameText  != null) nameText.text  = itemId.ToString();
                if (countText != null) countText.text  = $"x{amount}";
            }

            gameObject.name = $"[Belt] {itemId} x{amount}";
        }
    }
}
