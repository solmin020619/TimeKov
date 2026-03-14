// PlayerWeaponController.cs
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

    //  Ammo Link (추가)
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

    // muzzle cache
    private Transform cachedMuzzle;
    private readonly Dictionary<int, Transform> muzzleByItemId = new Dictionary<int, Transform>();

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

        RefreshUI();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        // UI 열려있으면 발사/연사/단발 입력 상태 끊고 아무것도 안 함
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

        // 달리는 중이면 발사 상태를 강제로 끊고(홀드/단발 포함) 이번 프레임 발사 로직 스킵
        if (blockFireWhileRunning && playerController != null && playerController.IsRunning)
        {
            fireHeld = false;
            semiConsume = false;
            return;
        }

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
            int cap = GetMagazineCapacity();
            if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < cap)
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
        // 탄창 최대(cap)는 탄약 overlapsCount 기반 (매핑 없으면 기존 magazinesize로 fallback)
        int cap = GetMagazineCapacity();
        currentAmmoInMag = Mathf.Max(0, cap); // 기존 동작(장착 시 꽉 찬 상태) 유지
        isReloading = false;
        fireCooldown = 0f;

        fireHeld = false;
        semiConsume = false;

        recoilAccumYaw = 0f;
        recoilIndex = 0;

        // Kinemation weapon select
        int targetIndex = GetKinemationIndex(itemId);
        if (fpsPlayer != null && targetIndex >= 0)
        {
            fpsPlayer.SetActiveWeaponIndex(targetIndex);

            // 무기 바뀌면 muzzle 재탐색(활성 무기 프리팹 기준)
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

    // Ammo Link Helpers
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

        // 탄약 매핑이 있으면 overlapsCount 기반
        int ammoId = GetAmmoItemIdForWeapon(equippedItemId);
        if (ammoId != 0 && DataManager.Instance != null)
        {
            ItemInfo ammo = DataManager.Instance.GetItem(ammoId);
            if (ammo != null)
            {
                // 요구사항: 탄창 최대 = 탄약 아이템 overlapsCount
                int cap = Mathf.Max(1, ammo.overlapsCount);
                return cap;
            }
        }

        // 매핑/데이터 없으면 기존 magazinesize로 fallback (안전)
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

        // 부분소비 금지 정책은 InventoryManager.TryConsumeItem이 보장
        return playerInventory.TryConsumeItem(ammoId, amount);
    }
    private bool CanFireNow()
    {
        if (weapon == null) return false;
        if (Time.timeScale == 0f) return false;

        // UI 열려있으면 발사 불가
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

        // UI 열려있으면 발사 입력 자체 무시 + 상태 끊기
        if (!UIStateManager.GameplayInputEnabled)
        {
            fireHeld = false;
            semiConsume = false;
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // 달리는 중 발사 입력 무시
        if (!CanFireNow()) return;

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

        // UI 열려있으면 장전 시작 불가
        if (!UIStateManager.GameplayInputEnabled) return;

        int cap = GetMagazineCapacity();
        if (currentAmmoInMag >= cap) return;

        // 인벤 탄약 0이면 장전 불가
        if (GetInventoryAmmoCount() <= 0) return;

        StartCoroutine(ReloadRoutine());
    }

    public void SetADS(bool isAiming) { /* 필요하면 여기 연결 */ }

    // =========================
    // Core Fire/Reload
    // =========================
    private void TryFireNow()
    {
        // 여기도 방어(연사 루프/외부 호출 대비)
        if (!CanFireNow()) return;

        if (fireCooldown > 0f) return;

        // 탄창 0이면 발사 불가 (기존 동작 유지)
        if (currentAmmoInMag <= 0) return;

        FireRaycastAndVisual();

        if (crosshair != null) crosshair.OnFire();

        // fireRate = "초당 발사수" (shots per second)
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

        // 리로드 도중 UI가 열리면 아예 진행하지 않게(시작 시점 방어)
        if (!UIStateManager.GameplayInputEnabled) yield break;

        int cap = GetMagazineCapacity();
        if (currentAmmoInMag >= cap) yield break;

        // 인벤 탄약 0이면 리로드 시작 자체를 막음
        if (GetInventoryAmmoCount() <= 0) yield break;

        isReloading = true;
        OnReloadStart();
        ReloadStarted?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, weapon.reloadTime));

        // 리로드 진행 중 UI가 열렸으면 "적용 없이 취소"
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
        // UI/사운드 훅 자리
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

        // 장전 시 인벤 탄약 소비 -> 탄창 채움
        // (부분소비 금지 정책: load는 available 이하로 잡았기 때문에 항상 전량 소비 가능)
        if (TryConsumeInventoryAmmo(load))
        {
            currentAmmoInMag += load;
            currentAmmoInMag = Mathf.Clamp(currentAmmoInMag, 0, cap);
        }
    }

    // =========================
    // Raycast + Visual bullet
    // =========================
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
        // 1) muzzle(무기 총구) 우선
        if (cachedMuzzle != null)
            return cachedMuzzle.position;

        // 2) aimPoint (최후의 뷰모델 기준)
        if (fpsPlayer != null)
        {
            Transform ap = fpsPlayer.GetActiveAimPoint();
            if (ap != null) return ap.position;
        }

        // 3) 카메라 fallback
        return fallback;
    }

    // =========================
    // Auto-find muzzle per weapon
    // =========================
    private IEnumerator ResolveMuzzleNextFrame(int itemId)
    {
        // SetActiveWeaponIndex 후 계층/활성화 반영을 위해 1프레임 대기
        yield return null;

        // itemId 캐시가 있으면 재사용
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

        // 1) "현재 활성 무기 루트" 찾기
        Transform activeWeaponRoot = FindActiveWeaponRootUnder(fpsPlayer.transform);

        // 2) 활성 무기 하위에서 muzzle 찾기 (이름 키워드 기반)
        Transform muzzle = null;
        if (activeWeaponRoot != null)
            muzzle = FindMuzzleByKeywords(activeWeaponRoot);

        // 3) 그래도 못 찾으면: fpsPlayer 전체에서 한번 더(최후)
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

        // 1) active인 노드들 중, "무기 같아 보이는" 이름 + muzzle 존재면 우선
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

        // 2) 그냥 active인 노드들 중 muzzle을 가진 노드의 상위를 반환
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

            // 포함 키워드 점수
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

            // 제외(잘못 잡기 쉬운 것들)
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

        // 1프레임 대기(초기화 안정)
        yield return null;

        EquipByItemId(autoEquipItemId);
    }

    // UI Getters
    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? GetMagazineCapacity() : 0; // ✅ 탄약 기반 cap
    public int GetEquippedItemId() => equippedItemId;
    public bool IsReloading() => isReloading;
    public bool HasWeaponEquipped() => weapon != null;

    // =========================================================
    //  Session Export / Import (씬 이동 시 무기/탄창 유지)
    //  기존 기능 삭제/변경 없이 "추가"만
    // =========================================================

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
            // 무기 없던 상태
            Unequip();
            return;
        }

        // 기존 장착 로직 사용
        EquipByItemId(s.equippedItemId);

        // 장착 로직에서 탄창을 "꽉 채우는" 기존 동작이 있으므로
        // 여기서 다시 저장값으로 덮어씀 (기존 로직은 건드리지 않음)
        int cap = GetMagazineCapacity();
        int clamped = Mathf.Clamp(s.currentAmmoInMag, 0, Mathf.Max(0, cap));

        // private 변수라서 함수가 없으면 직접 접근이 불가하지만,
        // 이 스크립트 내부이므로 필드에 접근 가능
        currentAmmoInMag = clamped;
    }

}