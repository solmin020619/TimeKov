using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [System.Serializable]
    public struct WeaponIdToAmmoId
    {
        public int weaponItemId;
        public int ammoItemId;
    }

    [Header("Ammo Link (WeaponItemId -> AmmoItemId)")]
    [Tooltip("총 아이템ID와 탄약 아이템ID를 매핑해줘야 overlapsCount(탄창 최대)를 탄약 기준으로 계산함")]
    public WeaponIdToAmmoId[] weaponAmmoMap;

    [Header("Inventory Reference (Ammo Source)")]
    [Tooltip("플레이어 인벤토리(탄약을 소비할 InventoryManager). 비어있으면 씬에서 자동 탐색 시도")]
    public InventoryManager playerInventory;

    [Header("Hit")]
    [Tooltip("Enemy 레이어 포함")]
    public LayerMask hitMask = ~0;

    [Header("Bullet Visual (optional)")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;
    public float bulletLifeTime = 2f;

    [Header("Bullet Pool")]
    public int bulletPoolSize = 30;

    [Header("Visual Origin (Auto Muzzle)")]
    [Tooltip("무기 교체될 때마다 '현재 활성 무기'에서 muzzle/firePoint를 자동 탐색해서 비주얼 탄 시작점으로 사용")]
    public bool autoFindMuzzleOnEquip = true;

    [Tooltip("총구 탐색 키워드(이름에 포함되면 총구로 간주). 기본값이면 대부분 커버됨.")]
    public string[] muzzleNameKeywords = new string[] { "muzzle", "firepoint", "fire_point", "barrel", "tip" };

    [Header("Crosshair")]
    public CrosshairController crosshair;

    [Header("Fire Rule")]
    [Tooltip("달리는 동안 발사 금지")]
    public bool blockFireWhileRunning = true;

    [Header("Debug")]
    public bool autoEquipOnStart = false;
    public int autoEquipItemId = 1201;
    public bool debugLogFire = false;

    // ========= Events (for KinemationWeaponDriver) =========
    public System.Action Fired;
    public System.Action ReloadStarted;

    // runtime
    private ItemInfo weapon;
    private int equippedItemId;

    private int currentAmmoInMag;
    private bool isReloading;

    // fire
    private float fireCooldown;
    private bool fireHeld;
    private bool semiConsume;

    // recoil/spread state
    private float recoilAccumYaw;
    private int recoilIndex;
    private float lastFireTime = -999f;

    private PlayerController playerController;
    private Camera cachedCam;

    // muzzle cache
    private Transform cachedMuzzle;
    private readonly Dictionary<int, Transform> muzzleByItemId = new Dictionary<int, Transform>();

    // bullet pool
    private readonly Queue<VisualBullet> bulletPool = new Queue<VisualBullet>();

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();

        cachedCam = GetComponentInChildren<Camera>();
        if (cachedCam == null) cachedCam = Camera.main;

        if (fpsPlayer == null)
            fpsPlayer = FindFirstObjectByType<FPSPlayer>();

        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<InventoryManager>();

        Debug.Log($"[Weapon] {gameObject.name}이 사용하는 카메라: {(cachedCam != null ? cachedCam.name : "NULL")}");
    }

    private void Start()
    {
        if (autoEquipOnStart)
            StartCoroutine(AutoEquipWhenReady());

        InitBulletPool();
        RefreshUI();
    }

    private void InitBulletPool()
    {
        if (bulletPrefab == null) return;

        for (int i = 0; i < bulletPoolSize; i++)
        {
            GameObject go = Instantiate(bulletPrefab);
            go.SetActive(false);

            VisualBullet vb = go.GetComponent<VisualBullet>();
            if (vb == null)
            {
                Debug.LogError("[PlayerWeaponController] bulletPrefab에 VisualBullet이 없음");
                Destroy(go);
                continue;
            }

            vb.SetOwner(this);
            bulletPool.Enqueue(vb);
        }
    }

    private VisualBullet GetPooledBullet()
    {
        while (bulletPool.Count > 0)
        {
            VisualBullet vb = bulletPool.Dequeue();
            if (vb != null) return vb;
        }

        if (bulletPrefab != null)
        {
            GameObject go = Instantiate(bulletPrefab);
            go.SetActive(false);

            VisualBullet vb = go.GetComponent<VisualBullet>();
            if (vb != null)
            {
                vb.SetOwner(this);
                return vb;
            }

            Destroy(go);
        }

        return null;
    }

    public void ReturnBullet(VisualBullet bullet)
    {
        if (bullet == null) return;

        bullet.gameObject.SetActive(false);
        bulletPool.Enqueue(bullet);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        if (!UIStateManager.GameplayInputEnabled)
        {
            fireHeld = false;
            semiConsume = false;
            return;
        }

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        if (crosshair != null)
        {
            crosshair.SetEnabled(weapon != null);
            crosshair.SetRunning(playerController != null && playerController.IsRunning);
        }

        if (weapon == null) return;

        if (blockFireWhileRunning && playerController != null && playerController.IsRunning)
        {
            fireHeld = false;
            semiConsume = false;
            return;
        }

        if (!isReloading && fireHeld && IsAutomaticWeapon())
        {
            TryFireNow();
        }

        if (!isReloading && semiConsume && !IsAutomaticWeapon())
        {
            semiConsume = false;
            TryFireNow();
        }

        if (!isReloading)
        {
            int cap = GetMagazineCapacity();
            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < cap)
                Reload();
        }
    }

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

        int cap = GetMagazineCapacity();
        currentAmmoInMag = Mathf.Max(0, cap);
        isReloading = false;
        fireCooldown = 0f;

        fireHeld = false;
        semiConsume = false;

        recoilAccumYaw = 0f;
        recoilIndex = 0;

        int targetIndex = GetKinemationIndex(itemId);
        if (fpsPlayer != null && targetIndex >= 0)
        {
            fpsPlayer.SetActiveWeaponIndex(targetIndex);

            if (autoFindMuzzleOnEquip)
                StartCoroutine(ResolveMuzzleNextFrame(itemId));
        }
        else
        {
            Debug.LogWarning($"[Equip] skip SetActiveWeaponIndex. fpsPlayer={(fpsPlayer ? "OK" : "NULL")} targetIndex={targetIndex}");
            cachedMuzzle = null;
        }

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

        cachedMuzzle = null;

        RefreshUI();
    }

    private bool IsWeaponItem(ItemInfo item)
    {
        return item != null && item.id >= 1100 && item.id < 1500;
    }

    private bool IsAutomaticWeapon()
    {
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

    private int GetAmmoItemIdForWeapon(int weaponItemId)
    {
        if (weaponAmmoMap == null) return 0;
        for (int i = 0; i < weaponAmmoMap.Length; i++)
        {
            if (weaponAmmoMap[i].weaponItemId == weaponItemId)
                return weaponAmmoMap[i].ammoItemId;
        }
        return 0;
    }

    private int GetMagazineCapacity()
    {
        if (weapon == null) return 0;

        int ammoId = GetAmmoItemIdForWeapon(equippedItemId);
        if (ammoId != 0 && DataManager.Instance != null)
        {
            ItemInfo ammo = DataManager.Instance.GetItem(ammoId);
            if (ammo != null)
            {
                int cap = Mathf.Max(1, ammo.overlapsCount);
                return cap;
            }
        }

        return Mathf.Max(0, weapon.magazinesize);
    }

    private int GetInventoryAmmoCount()
    {
        int ammoId = GetAmmoItemIdForWeapon(equippedItemId);
        if (ammoId == 0) return 0;
        if (playerInventory == null) return 0;

        return playerInventory.GetTotalItemCount(ammoId);
    }

    private bool TryConsumeInventoryAmmo(int amount)
    {
        if (amount <= 0) return false;
        int ammoId = GetAmmoItemIdForWeapon(equippedItemId);
        if (ammoId == 0) return false;
        if (playerInventory == null) return false;

        return playerInventory.TryConsumeItem(ammoId, amount);
    }

    private bool CanFireNow()
    {
        if (weapon == null) return false;
        if (Time.timeScale == 0f) return false;
        if (!UIStateManager.GameplayInputEnabled) return false;
        if (isReloading) return false;

        if (blockFireWhileRunning && playerController != null && playerController.IsRunning)
            return false;

        return true;
    }

    public void Fire()
    {
        if (weapon == null) return;
        if (Time.timeScale == 0f) return;

        if (!UIStateManager.GameplayInputEnabled)
        {
            fireHeld = false;
            semiConsume = false;
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!CanFireNow()) return;

        if (IsAutomaticWeapon()) fireHeld = true;
        else semiConsume = true;
    }

    public void FireUp()
    {
        fireHeld = false;
        semiConsume = false;
    }

    public void Reload()
    {
        if (weapon == null) return;
        if (isReloading) return;
        if (!UIStateManager.GameplayInputEnabled) return;

        int cap = GetMagazineCapacity();
        if (currentAmmoInMag >= cap) return;
        if (GetInventoryAmmoCount() <= 0) return;

        StartCoroutine(ReloadRoutine());
    }

    public void SetADS(bool isAiming) { }

    private void TryFireNow()
    {
        if (!CanFireNow()) return;
        if (fireCooldown > 0f) return;
        if (currentAmmoInMag <= 0) return;

        FireRaycastAndVisual();

        if (crosshair != null) crosshair.OnFire();

        float sps = Mathf.Max(0.01f, weapon.fireRate);
        fireCooldown = 1f / sps;

        currentAmmoInMag--;

        if (debugLogFire)
            Debug.Log($"[FIRE] id={equippedItemId} ammo={currentAmmoInMag}/{GetMagazineCapacity()} cooldown={fireCooldown:F3}");

        Fired?.Invoke();
    }

    private IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;
        if (isReloading) yield break;
        if (!UIStateManager.GameplayInputEnabled) yield break;

        int cap = GetMagazineCapacity();
        if (currentAmmoInMag >= cap) yield break;
        if (GetInventoryAmmoCount() <= 0) yield break;

        isReloading = true;
        OnReloadStart();
        ReloadStarted?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, weapon.reloadTime));

        if (!UIStateManager.GameplayInputEnabled)
        {
            isReloading = false;
            yield break;
        }

        ApplyReload();
        isReloading = false;
    }

    public void OnReloadStart()
    {
    }

    private void ApplyReload()
    {
        if (weapon == null) return;

        int cap = GetMagazineCapacity();
        if (cap <= 0) return;

        int need = cap - currentAmmoInMag;
        if (need <= 0) return;

        int available = GetInventoryAmmoCount();
        if (available <= 0) return;

        int load = Mathf.Min(need, available);

        if (TryConsumeInventoryAmmo(load))
        {
            currentAmmoInMag += load;
            currentAmmoInMag = Mathf.Clamp(currentAmmoInMag, 0, cap);
        }
    }

    private void FireRaycastAndVisual()
    {
        if (cachedCam == null || !cachedCam.gameObject.activeInHierarchy)
            cachedCam = GetComponentInChildren<Camera>() ?? Camera.main;

        if (cachedCam == null) return;

        Vector3 camOrigin = cachedCam.transform.position;
        Vector3 camDir = cachedCam.transform.forward;

        float range = weapon.effectiveRange;

        Vector3 rayStart = camOrigin + camDir * 0.5f;
        Vector3 hitPoint = camOrigin + camDir * range;
        bool hasHitPoint = false;

        if (Physics.Raycast(rayStart, camDir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            hasHitPoint = true;
            hitPoint = hit.point;

            if (debugLogFire) Debug.Log($"[Hit] 맞은 물체: {hit.collider.name} / 위치: {hit.point}");

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
        if (cachedMuzzle != null)
            return cachedMuzzle.position;

        if (fpsPlayer != null)
        {
            Transform ap = fpsPlayer.GetActiveAimPoint();
            if (ap != null) return ap.position;
        }

        return fallback;
    }

    private IEnumerator ResolveMuzzleNextFrame(int itemId)
    {
        yield return null;

        if (muzzleByItemId.TryGetValue(itemId, out var cached) && cached != null)
        {
            cachedMuzzle = cached;
            yield break;
        }

        if (fpsPlayer == null)
        {
            cachedMuzzle = null;
            yield break;
        }

        Transform activeWeaponRoot = FindActiveWeaponRootUnder(fpsPlayer.transform);

        Transform muzzle = null;
        if (activeWeaponRoot != null)
            muzzle = FindMuzzleByKeywords(activeWeaponRoot);

        if (muzzle == null)
            muzzle = FindMuzzleByKeywords(fpsPlayer.transform);

        cachedMuzzle = muzzle;

        if (cachedMuzzle != null)
            muzzleByItemId[itemId] = cachedMuzzle;
    }

    private Transform FindActiveWeaponRootUnder(Transform root)
    {
        if (root == null) return null;

        var all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!t.gameObject.activeInHierarchy) continue;

            string n = t.name.ToLowerInvariant();
            bool looksWeapon = n.Contains("weapon") || n.Contains("gun") || n.Contains("rifle") || n.Contains("pistol");

            if (!looksWeapon) continue;

            if (FindMuzzleByKeywords(t) != null)
                return t;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (!t.gameObject.activeInHierarchy) continue;

            var mz = FindMuzzleByKeywords(t);
            if (mz != null)
                return t;
        }

        return null;
    }

    private Transform FindMuzzleByKeywords(Transform root)
    {
        if (root == null) return null;

        var all = root.GetComponentsInChildren<Transform>(true);

        Transform best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t.gameObject.activeInHierarchy) continue;

            string n = t.name.ToLowerInvariant();
            int score = 0;

            if (muzzleNameKeywords != null)
            {
                for (int k = 0; k < muzzleNameKeywords.Length; k++)
                {
                    string key = muzzleNameKeywords[k];
                    if (string.IsNullOrEmpty(key)) continue;
                    if (n.Contains(key.ToLowerInvariant()))
                    {
                        score += (key.ToLowerInvariant() == "muzzle") ? 200 : 80;
                    }
                }
            }

            if (n.Contains("hand")) score -= 80;
            if (n.Contains("camera")) score -= 80;
            if (n.Contains("aim")) score -= 40;

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        if (bestScore < 80) return null;
        return best;
    }

    private Vector3 ApplyRecoil(Vector3 forward)
    {
        if (cachedCam == null) cachedCam = Camera.main;

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
                if (recoilIndex < pattern.Length)
                {
                    deltaYaw = pattern[recoilIndex];
                    recoilIndex++;
                }
            }
        }

        float rand = Mathf.Abs(weapon.randomRecoilAngle);
        if (rand > 0f) deltaYaw += Random.Range(-rand, rand);

        recoilAccumYaw += deltaYaw;

        Quaternion horizRecoil = Quaternion.AngleAxis(recoilAccumYaw, cachedCam.transform.up);
        return (horizRecoil * forward).normalized;
    }

    private Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0.01f) return forward;

        float randomYaw = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
        float randomPitch = Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);

        Quaternion baseRot = Quaternion.FromToRotation(Vector3.forward, forward);
        Quaternion spreadRot = Quaternion.Euler(randomPitch, randomYaw, 0f);

        return (baseRot * spreadRot * Vector3.forward).normalized;
    }

    private void SpawnVisualBullet(Vector3 origin, Vector3 dir, Vector3? hitPoint = null)
    {
        if (bulletPrefab == null) return;

        VisualBullet vb = GetPooledBullet();
        if (vb == null) return;

        float lt = bulletLifeTime;

        if (hitPoint.HasValue)
        {
            float dist = Vector3.Distance(origin, hitPoint.Value);
            float t = dist / Mathf.Max(0.01f, bulletSpeed);
            lt = Mathf.Max(0.02f, t);
        }

        vb.gameObject.SetActive(true);
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

        yield return null;
        EquipByItemId(autoEquipItemId);
    }

    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? GetMagazineCapacity() : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool IsReloading() => isReloading;
    public bool HasWeaponEquipped() => weapon != null;

    public PlayerSessionData.WeaponSnapshot ExportToSessionSnapshot()
    {
        var s = new PlayerSessionData.WeaponSnapshot();
        s.equippedItemId = GetEquippedItemId();
        s.currentAmmoInMag = GetCurrentAmmo();
        return s;
    }

    public void ImportFromSessionSnapshot(PlayerSessionData.WeaponSnapshot s)
    {
        if (s == null) return;

        if (s.equippedItemId <= 0)
        {
            Unequip();
            return;
        }

        EquipByItemId(s.equippedItemId);

        int cap = GetMagazineCapacity();
        int clamped = Mathf.Clamp(s.currentAmmoInMag, 0, Mathf.Max(0, cap));
        currentAmmoInMag = clamped;
    }
}