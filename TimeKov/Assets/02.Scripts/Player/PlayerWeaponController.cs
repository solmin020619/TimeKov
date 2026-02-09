using System.Collections;
using UnityEngine;
using KINEMATION.FPSAnimationPack.Scripts.Player;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("KINEMATION (ViewModel)")]
    [Tooltip("SK_Arms_Mono에 붙어있는 FPSPlayer를 넣어")]
    public FPSPlayer fpsPlayer;

    [System.Serializable]
    public struct ItemIdToKinemationIndex
    {
        public int itemId;
        public int weaponIndex; // FPSPlayerSettings.weaponPrefabs index
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

    // runtime
    private ItemInfo weapon;
    private int equippedItemId;

    private int currentAmmoInMag;
    private bool isReloading;
    private float fireCooldown;

    // recoil/spread state
    private float recoilAccumYaw;
    private int recoilIndex;
    private float lastFireTime = -999f;

    // “입력 눌림 → 애니 이벤트 때 발사” 동기화용
    private bool fireRequested;

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
            EquipByItemId(autoEquipItemId);

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

        // 임시: R키로 리로드 (나중에 에셋 입력으로 옮겨도 됨)
        if (!isReloading)
        {
            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazinesize)
                StartCoroutine(ReloadRoutine());
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

        // gameplay reset
        currentAmmoInMag = Mathf.Max(0, weapon.magazinesize);
        isReloading = false;
        fireCooldown = 0f;
        recoilAccumYaw = 0f;
        recoilIndex = 0;
        fireRequested = false;

        // A안: 에셋 무기 선택만
        int targetIndex = GetKinemationIndex(itemId);
        if (fpsPlayer != null && targetIndex >= 0)
            fpsPlayer.SetActiveWeaponIndex(targetIndex);

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
        recoilAccumYaw = 0f;
        recoilIndex = 0;
        fireRequested = false;

        RefreshUI();
    }

    private bool IsWeaponItem(ItemInfo item)
    {
        return item != null && item.id >= 1100 && item.id < 1500;
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
    // Input hook (Asset -> call these)
    // =========================

    /// <summary>
    /// 에셋 입력(좌클릭 Down)에서 호출.
    /// true면 “발사 가능”이므로, 이때만 fpsPlayer.FirePressed()를 호출하면 됨.
    /// </summary>
    public bool RequestFire()
    {
        if (weapon == null) return false;
        if (isReloading) return false;
        if (fireCooldown > 0f) return false;
        if (currentAmmoInMag <= 0) return false;

        fireRequested = true;

        if (debugLogFire)
            Debug.Log($"[PWC] RequestFire OK ammo={currentAmmoInMag}");

        return true;
    }

    /// <summary>
    /// 에셋 입력(좌클릭 Up)에서 호출.
    /// </summary>
    public void ReleaseFire()
    {
        // 자동화기 연사 끊는 용도
        fireRequested = false;
    }

    /// <summary>
    /// (브릿지 통해) Fire 애니 클립 Animation Event에서 호출됨.
    /// Function name: OnFireGameplay
    /// </summary>
    public void OnFireGameplay()
    {
        if (weapon == null) return;
        if (isReloading) return;

        if (!fireRequested) return;   // “입력 없이 애니만” 방지
        fireRequested = false;

        TryFireNow();
    }

    private void TryFireNow()
    {
        if (fireCooldown > 0f) return;
        if (currentAmmoInMag <= 0) return;

        FireRaycastAndVisual();

        if (crosshair != null) crosshair.OnFire();

        fireCooldown = 1f / Mathf.Max(0.01f, weapon.fireRate);
        currentAmmoInMag--;
    }

    // =========================
    // Reload (time-based, 임시)
    // =========================
    private IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;
        if (isReloading) yield break;
        if (currentAmmoInMag >= weapon.magazinesize) yield break;

        isReloading = true;
        OnReloadStart();

        yield return new WaitForSeconds(weapon.reloadTime);

        ApplyReload();
        isReloading = false;
    }

    public void OnReloadStart()
    {
        // UI/사운드 훅 자리
    }

    public void OnReloadApply()
    {
        ApplyReload();
    }

    private void ApplyReload()
    {
        if (weapon == null) return;
        currentAmmoInMag = weapon.magazinesize;
    }

    // =========================
    // Fire (Raycast + Visual)
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

    // UI Getters
    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? weapon.magazinesize : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool IsReloading() => isReloading;
    public bool HasWeaponEquipped() => weapon != null;
}
