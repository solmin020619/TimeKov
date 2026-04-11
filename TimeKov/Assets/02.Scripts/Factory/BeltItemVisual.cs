using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TIMEKOV.Factory
{
    public class BeltItemVisual : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;

        private Camera _cam;
        private void Awake() => _cam = Camera.main;

        private void LateUpdate()
        {
            if (_cam != null) transform.forward = _cam.transform.forward;
        }

        public void Setup(int itemId, int amount)
        {
            var row = DataStore.GetItem(itemId);
            var sprite = Resources.Load<Sprite>("Icon/" + itemId);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }

            if (nameText != null) nameText.text = row?.itemName ?? itemId.ToString();
            if (countText != null) countText.text = amount > 1 ? $"x{amount}" : "";

            gameObject.name = $"[Belt] {itemId} x{amount}";
        }
    }
}