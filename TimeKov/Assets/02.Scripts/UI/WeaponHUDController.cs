using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponHUDController : MonoBehaviour
{
    [Header("Bind Targets (optional)")]
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("UI")]
    [SerializeField] private GameObject weaponPanel;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Icon (Temporary)")]
    [Tooltip("아트 없을 때 임시 아이콘. 비워도 됨.")]
    [SerializeField] private Sprite fallbackIcon;

    private int lastItemId = -1;

    private void Start()
    {
        if (weaponPanel == null) weaponPanel = gameObject;

        if (weaponController == null)
            weaponController = FindAnyObjectByType<PlayerWeaponController>();

        RefreshAll(true);
    }

    private void Update()
    {
        if (weaponController == null)
        {
            weaponController = FindAnyObjectByType<PlayerWeaponController>();
            return;
        }

        bool has = weaponController.HasWeaponEquipped();
        if (weaponPanel.activeSelf != has)
            weaponPanel.SetActive(has);

        if (!has) return;

        // 장착 무기 바뀌면 이름/아이콘 갱신
        int itemId = weaponController.GetEquippedItemId();
        if (itemId != lastItemId)
        {
            lastItemId = itemId;
            RefreshWeaponInfo(itemId);
        }

        // 탄 수는 실시간 갱신
        int cur = weaponController.GetCurrentAmmo();
        int max = weaponController.GetMagazineSize();
        if (ammoText != null)
            ammoText.text = $"{cur} / {max}";
    }

    private void RefreshAll(bool force)
    {
        if (weaponController == null)
        {
            if (weaponPanel != null) weaponPanel.SetActive(false);
            return;
        }

        bool has = weaponController.HasWeaponEquipped();
        if (weaponPanel != null) weaponPanel.SetActive(has);

        if (!has) return;

        int itemId = weaponController.GetEquippedItemId();
        if (force || itemId != lastItemId)
        {
            lastItemId = itemId;
            RefreshWeaponInfo(itemId);
        }
    }

    private void RefreshWeaponInfo(int itemId)
    {
        // 이름: DataManager 있으면 아이템 DB에서 가져오고, 없으면 ID 표시
        if (weaponNameText != null)
        {
            string name = $"Weapon {itemId}";
            if (DataManager.Instance != null)
            {
                var info = DataManager.Instance.GetItem(itemId);
                if (info != null) name = info.itemName;
            }
            weaponNameText.text = name;
        }

        // 아이콘: 임시로 Resources/Icon/{itemId} 시도 → 실패하면 fallback
        if (weaponIcon != null)
        {
            Sprite sp = Resources.Load<Sprite>($"Icon/{itemId}");
            if (sp == null) sp = fallbackIcon;
            weaponIcon.sprite = sp;
            weaponIcon.enabled = (weaponIcon.sprite != null);
        }
    }
}
