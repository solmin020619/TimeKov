// =====================================================================
// BeltItemVisual.cs
// �����̾� ��Ʈ �� ������ �ð�ȭ
// ������ DataStore.GetItem �� GameDataUtility.GetItem ���� ��ü
// =====================================================================

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
            // ī�޶� ���� ������ (�ʿ� �� �ּ� ����)
            // if (_cam != null)
            //     transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position, Vector3.up);
        }

        public void Setup(int itemId, int amount)
        {
            var itemData = GameDataUtility.GetItem(itemId);
            Sprite sprite = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.iconKey))
                sprite = Resources.Load<Sprite>("Icon/" + itemData.iconKey);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }

            if (nameText != null) nameText.text = itemData?.GetLocalizedName() ?? itemId.ToString();
            if (countText != null) countText.text = amount > 1 ? $"x{amount}" : "";

            gameObject.name = $"[Belt] {itemId} x{amount}";
        }
    }
}