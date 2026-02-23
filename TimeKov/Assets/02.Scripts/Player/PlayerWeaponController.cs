// PlayerWeaponController.cs
using System.Collections;
using UnityEngine;
using KINEMATION.FPSAnimationPack.Scripts.Player;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("KINEMATION (ViewModel)")]
    [Tooltip("SK_Arms_Mono에 붙어있는 FPSPlayer를 넣어(없으면 자동 탐색)")]
    public FPSPlayer fpsPlayer;

    [System.Serializable]
    public struct ItemIdToKinemationIndex
    {
        public int itemId;
        public int weaponIndex; // FPSPlayerSettings.weaponPrefabs index (0-based)
    }

    [Header("ItemId -> Kinemation Weapon Index")]
    public ItemIdToKinemationIndex[] weaponIndexMap;

    [Header("Hit")]
    [Tooltip("Enemy 레이어 포함")]
    public LayerMask hitMask = ~0;

    [Header("Bullet Visual (optional)")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;
    public float bulletLifeTime = 2f;

    [Header("Crosshair")]
    public CrosshairController crosshair;

    [Header("Debug")]
    public bool autoEquipOnStart = false;
    public int autoEquipItemId = 1201;
    public bool debugLogFire = false;

    // ========= Events (for KinemationWeaponDriver) =========
    public System.Action Fired;         // "한 발 성공 발사" 했을 때
    public System.Action ReloadStarted; // "리로드 시작" 했을 때

    // runtime
    private ItemInfo weapon;
    private int equippedItemId;

    private int currentAmmoInMag;
    private bool isReloading;

    // fire
    private float fireCooldown;
    private bool fireHeld;      // 누르고 있는 상태(연사에 사용)
    private bool semiConsume;   // 단발: 눌림 1회 소비용

    // recoil/spread state
    private float recoilAccumYaw;
    private int recoilIndex;
    private float lastFireTime = -999f;

    private PlayerController playerController;
    private Camera cachedCam;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        cachedCam = Camera.main;

        if (fpsPlayer == null)
            fpsPlayer = FindFirstObjectByType<FPSPlayer>();
    }

    private void Start()
    {
        if (autoEquipOnStart)
            StartCoroutine(AutoEquipWhenReady());

        RefreshUI();
    }

    private void Update()
    {
        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        if (crosshair != null)
        {
            crosshair.SetEnabled(weapon != null);
            crosshair.SetRunning(playerController != null && playerController.IsRunning);
        }

        if (weapon == null) return;

        // 연사(자동) - 누르고 있는 동안 계속
        if (!isReloading && fireHeld && IsAutomaticWeapon())
        {
            TryFireNow();
        }

        // 단발 - 눌림 1회만
        if (!isReloading && semiConsume && !IsAutomaticWeapon())
        {
            semiConsume = false;
            TryFireNow();
        }

        // 임시: R키로 리로드
        if (!isReloading)
        {
            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazinesize)
                Reload();
        }
    }

    // =========================
    // Equip (Inventory -> itemId)
    // =========================
    public bool EquipByItemId(int itemId)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogError("[PlayerWeaponController] DataManager.Instance is null.");
            return false;
        }

        ItemInfo item = DataManager.Instance.GetItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[PlayerWeaponController] Item not found. id={itemId}");
            Unequip();
            return false;
        }

        if (!IsWeaponItem(item))
        {
            Debug.LogWarning($"[PlayerWeaponController] Not a weapon item. id={itemId} type={item.itemType}");
            return false;
        }

        equippedItemId = itemId;
        weapon = item;

        Debug.Log($"[DATA] id={weapon.id} name={weapon.itemName} mag={weapon.magazinesize} fireRate={weapon.fireRate} reload={weapon.reloadTime} auto={weapon.isAutomatic}");

        // gameplay reset
        currentAmmoInMag = Mathf.Max(0, weapon.magazinesize);
        isReloading = false;
        fireCooldown = 0f;

        fireHeld = false;
        semiConsume = false;

        recoilAccumYaw = 0f;
        recoilIndex = 0;

        // Kinemation weapon select
        int targetIndex = GetKinemationIndex(itemId);
        if (fpsPlayer != null && targetIndex >= 0)
            fpsPlayer.SetActiveWeaponIndex(targetIndex);
        else
            Debug.LogWarning($"[Equip] skip SetActiveWeaponIndex. fpsPlayer={(fpsPlayer ? "OK" : "NULL")} targetIndex={targetIndex}");

        RefreshUI();
        return true;
    }

    public void Unequip()
    {
        weapon = null;
        equippedItemId = 0;

        currentAmmoInMag = 0;
        isReloading = false;
        fireCooldown = 0f;

        fireHeld = false;
        semiConsume = false;

        recoilAccumYaw = 0f;
        recoilIndex = 0;

        RefreshUI();
    }

    private bool IsWeaponItem(ItemInfo item)
    {
        return item != null && item.id >= 1100 && item.id < 1500;
    }

    private bool IsAutomaticWeapon()
    {
        // ItemInfo.isAutomatic: 1이면 자동(연사), 0이면 단발
        return weapon != null && weapon.isAutomatic == 1;
    }

    private int GetKinemationIndex(int itemId)
    {
        if (weaponIndexMap == null) return -1;
        for (int i = 0; i < weaponIndexMap.Length; i++)
            if (weaponIndexMap[i].itemId == itemId)
                return weaponIndexMap[i].weaponIndex;
        return -1;
    }

    private void RefreshUI()
    {
        if (crosshair != null)
            crosshair.SetEnabled(weapon != null);
    }

    // =========================
    // Input API (Bridge -> Here)
    // =========================
    // 눌림(press/hold 시작)
    public void Fire()
    {
        if (weapon == null) return;

        if (IsAutomaticWeapon())
        {
            fireHeld = true; // Update에서 반복 발사
        }
        else
        {
            semiConsume = true; // Update에서 1회 발사
        }
    }

    // 뗌(release)
    public void FireUp()
    {
        fireHeld = false;
        semiConsume = false;
    }

    public void Reload()
    {
        if (weapon == null) return;
        if (isReloading) return;
        if (currentAmmoInMag >= weapon.magazinesize) return;

        StartCoroutine(ReloadRoutine());
    }

    public void SetADS(bool isAiming) { /* 필요하면 여기 연결 */ }

    // =========================
    // Core Fire/Reload
    // =========================
    private void TryFireNow()
    {
        if (weapon == null) return;
        if (isReloading) return;
        if (fireCooldown > 0f) return;
        if (currentAmmoInMag <= 0) return;

        FireRaycastAndVisual();

        if (crosshair != null) crosshair.OnFire();

        // fireRate = "초당 발사수" (shots per second)
        float sps = Mathf.Max(0.01f, weapon.fireRate);
        fireCooldown = 1f / sps;

        currentAmmoInMag--;

        if (debugLogFire)
            Debug.Log($"[FIRE] id={equippedItemId} ammo={currentAmmoInMag}/{weapon.magazinesize} cooldown={fireCooldown:F3}");

        Fired?.Invoke();
    }

    private IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;
        if (isReloading) yield break;
        if (currentAmmoInMag >= weapon.magazinesize) yield break;

        isReloading = true;
        OnReloadStart();
        ReloadStarted?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, weapon.reloadTime));

        ApplyReload();
        isReloading = false;
    }

    public void OnReloadStart()
    {
        // UI/사운드 훅 자리
    }

    private void ApplyReload()
    {
        if (weapon == null) return;
        currentAmmoInMag = weapon.magazinesize;
    }

    // =========================
    // Raycast + Visual bullet
    // =========================
    private void FireRaycastAndVisual()
    {
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam == null) return;

        Vector3 camOrigin = cachedCam.transform.position;
        Vector3 camDir = cachedCam.transform.forward;

        float range = weapon.effectiveRange;

        Vector3 hitPoint = camOrigin + camDir * range;
        bool hasHitPoint = false;

        if (Physics.Raycast(camOrigin, camDir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hasHitPoint = true;
            hitPoint = hit.point;

            if (crosshair != null) crosshair.OnHitConfirm();

            EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
                enemy.TakeDamage((int)weapon.damage);
        }

        Vector3 origin = GetVisualOrigin(camOrigin);

        Vector3 forward = (hitPoint - origin);
        if (forward.sqrMagnitude < 0.0001f) forward = camDir;
        else forward.Normalize();

        Vector3 recoiledForward = ApplyRecoil(forward);

        int pellets = Mathf.Max(1, weapon.pelletsPerShot);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = GetSpreadDirection(recoiledForward, weapon.spreadAngle);
            Vector3? visualHit = hasHitPoint ? hitPoint : (Vector3?)null;
            SpawnVisualBullet(origin, dir, visualHit);
        }

        lastFireTime = Time.time;
    }

    private Vector3 GetVisualOrigin(Vector3 fallback)
    {
        if (fpsPlayer != null)
        {
            Transform ap = fpsPlayer.GetActiveAimPoint();
            if (ap != null) return ap.position;
        }
        return fallback;
    }

    private Vector3 ApplyRecoil(Vector3 forward)
    {
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        if (Time.time - lastFireTime > weapon.recoilResetTime)
        {
            recoilIndex = 0;
            recoilAccumYaw = 0f;
        }

        float deltaYaw = 0f;

        if (weapon.useRecoilPattern == 1)
        {
            float[] pattern = GetRecoilPatternByItemId(equippedItemId);
            if (pattern != null && pattern.Length > 0)
            {
                deltaYaw += pattern[Mathf.Min(recoilIndex, pattern.Length - 1)];
                recoilIndex++;
            }
        }

        float rand = Mathf.Abs(weapon.randomRecoilAngle);
        if (rand > 0f)
            deltaYaw += Random.Range(-rand, rand);

        recoilAccumYaw += deltaYaw;

        float finalYaw = baseYaw + recoilAccumYaw;
        float rad = finalYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    private Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0.01f) return forward;

        float half = spreadAngle * 0.5f;
        float yawOffset = Random.Range(-half, half);

        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float finalYaw = baseYaw + yawOffset;

        float rad = finalYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    private void SpawnVisualBullet(Vector3 origin, Vector3 dir, Vector3? hitPoint = null)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab);
        var vb = bullet.GetComponent<VisualBullet>();
        if (vb == null)
        {
            Destroy(bullet);
            return;
        }

        float lt = bulletLifeTime;

        if (hitPoint.HasValue)
        {
            float dist = Vector3.Distance(origin, hitPoint.Value);
            float t = dist / Mathf.Max(0.01f, bulletSpeed);
            lt = Mathf.Max(0.02f, t);
        }

        vb.Init(origin, dir, bulletSpeed, lt, hitPoint);
    }

    private float[] GetRecoilPatternByItemId(int itemId)
    {
        switch (itemId)
        {
            case 1101: return new float[] { 0.15f, 0.2f, 0.25f };
            case 1201:
                return new float[] {
                    0.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
                    0.5f, 0.5f, 0.5f, 0.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f,
                    -1.0f, -1.0f, -1.0f, -1.0f, -0.5f, -0.5f, 0.0f, 1.0f, 2.0f, 2.0f
                };
            case 1202: return new float[] { 0.1f, 0.2f, 0.3f };
            case 1301: return new float[] { 0.6f };
            case 1401: return new float[] { 0.15f, 0.15f };
            default: return new float[0];
        }
    }

    private IEnumerator AutoEquipWhenReady()
    {
        if (fpsPlayer == null)
            fpsPlayer = FindFirstObjectByType<FPSPlayer>();

        // 너가 쓰던 fpsPlayer.IsInitialized 쓰고 싶으면 FPSPlayer에 IsInitialized bool을 추가해야 함.
        // 여기서는 안전하게 1프레임만 기다리는 버전으로 둠.
        yield return null;

        EquipByItemId(autoEquipItemId);
    }

    // UI Getters
    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? weapon.magazinesize : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool IsReloading() => isReloading;
    public bool HasWeaponEquipped() => weapon != null;
}