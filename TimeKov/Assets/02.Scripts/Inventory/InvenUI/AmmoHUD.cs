using UnityEngine;
using TMPro;

/// <summary>
/// PUBG-style ammo HUD
/// - 현재는 장착된 탄창 탄 수만 표시
/// - PlayerWeaponController: GetCurrentAmmo(), GetEquippedItemId(), weaponAmmoMap 사용
/// - InventoryManager 참조 및 기존 함수 구조는 유지
/// </summary>
public class AmmoHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text ammoText; // ex) "30"

    [Header("Refs")]
    [SerializeField] private PlayerWeaponController weapon;
    [SerializeField] private InventoryManager inventory;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.1f;

    private float _t;
    private int _lastInMag = int.MinValue;
    private int _lastReserve = int.MinValue;
    private int _lastWeaponItemId = int.MinValue;
    private bool _hasInitializedText = false;

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        _t = refreshInterval;
        Refresh(force: true);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        if (_t < refreshInterval) return;
        _t = 0f;

        Refresh(force: false);
    }

    private void ResolveRefs()
    {
        if (weapon == null)
            weapon = FindFirstObjectByType<PlayerWeaponController>();

        if (inventory == null)
            inventory = FindFirstObjectByType<InventoryManager>(); // Player 인벤 1개라고 가정
    }

    private void Refresh(bool force)
    {
        if (ammoText == null)
            return;

        if (weapon == null || inventory == null)
            ResolveRefs();

        if (weapon == null)
            return;

        int inMag = weapon.GetCurrentAmmo();
        int weaponItemId = weapon.GetEquippedItemId();
        int ammoItemId = GetAmmoItemIdFromWeapon(weapon);

        int reserve = 0;
        if (inventory != null && ammoItemId > 0)
            reserve = inventory.GetTotalItemCount(ammoItemId);

        bool changed =
            !_hasInitializedText ||
            force ||
            inMag != _lastInMag ||
            reserve != _lastReserve ||
            weaponItemId != _lastWeaponItemId;

        if (!changed)
            return;

        // 기존 reserve 계산 구조는 유지하고, 표시만 현재 장착 탄 수만 나오게 변경
        ammoText.text = $"{inMag}";

        _lastInMag = inMag;
        _lastReserve = reserve;
        _lastWeaponItemId = weaponItemId;
        _hasInitializedText = true;
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