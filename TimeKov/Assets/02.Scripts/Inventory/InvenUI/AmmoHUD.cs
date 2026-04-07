using UnityEngine;
using TMPro;

/// <summary>
/// PUBG-style ammo HUD
/// - 현재는 장착된 탄창 탄 수만 표시
/// - reserve 계산은 유지하되, ammoItemId는 DataStore.GetWeapon(itemId).ammoItemId 사용
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
        EnsureDataStoreLoaded();
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
            inventory = FindFirstObjectByType<InventoryManager>();
    }

    private void EnsureDataStoreLoaded()
    {
        if (DataStore.IsLoaded) return;
        DataStore.LoadAll();
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
        int ammoItemId = GetAmmoItemIdFromWeaponItemId(weaponItemId);

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

        // 현재 표시 규칙: 탄창 내 탄 수만 표시
        ammoText.text = $"{inMag}";

        _lastInMag = inMag;
        _lastReserve = reserve;
        _lastWeaponItemId = weaponItemId;
        _hasInitializedText = true;
    }

    /// <summary>
    /// DataStore의 WeaponRow에서 ammoItemId를 가져온다.
    /// </summary>
    private int GetAmmoItemIdFromWeaponItemId(int weaponItemId)
    {
        if (weaponItemId <= 0)
            return 0;

        EnsureDataStoreLoaded();

        WeaponRow weaponRow = DataStore.GetWeapon(weaponItemId);
        if (weaponRow == null)
            return 0;

        return weaponRow.ammoItemId;
    }
}