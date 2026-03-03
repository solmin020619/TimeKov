using UnityEngine;
using TMPro;

/// <summary>
/// PUBG-style ammo HUD: "탄창 / 예비탄" (ex: 30 / 120)
/// - PlayerWeaponController: GetCurrentAmmo(), GetEquippedItemId(), weaponAmmoMap 사용
/// - InventoryManager: GetTotalItemCount(ammoItemId)로 예비탄 표시
/// </summary>
public class AmmoHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text ammoText; // ex) "30 / 120"

    [Header("Refs")]
    [SerializeField] private PlayerWeaponController weapon;
    [SerializeField] private InventoryManager inventory;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.1f;

    private float _t;

    private void Awake()
    {
        if (weapon == null) weapon = FindFirstObjectByType<PlayerWeaponController>();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryManager>(); // Player 인벤 1개라고 가정
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t < refreshInterval) return;
        _t = 0f;

        Refresh();
    }

    private void Refresh()
    {
        if (ammoText == null || weapon == null) return;

        int inMag = weapon.GetCurrentAmmo();
        int ammoItemId = GetAmmoItemIdFromWeapon(weapon);

        int reserve = 0;
        if (inventory != null && ammoItemId > 0)
            reserve = inventory.GetTotalItemCount(ammoItemId);

        ammoText.text = $"{inMag} / {reserve}";
    }

    /// <summary>
    /// PlayerWeaponController.weaponAmmoMap(weaponItemId -> ammoItemId)를 이용해서
    /// 현재 장착 무기의 탄약 아이템ID를 찾는다.
    /// </summary>
    private int GetAmmoItemIdFromWeapon(PlayerWeaponController w)
    {
        if (w == null) return 0;

        int weaponItemId = w.GetEquippedItemId();
        if (weaponItemId <= 0) return 0;

        var map = w.weaponAmmoMap;
        if (map == null || map.Length == 0) return 0;

        for (int i = 0; i < map.Length; i++)
        {
            if (map[i].weaponItemId == weaponItemId)
                return map[i].ammoItemId;
        }

        return 0;
    }
}