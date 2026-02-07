using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Weapon Equip Visual")]
    [Tooltip("무기 프리팹이 붙을 소켓(1인칭 ViewModel이면 ik_hand_gun 같은 곳)")]
    public Transform weaponSocket; // 이제 ViewModelRoot가 아니라 FPSPlayer의 ik_hand_gun을 넣는다

    [Tooltip("무기 프리팹 5개를 itemId 순서에 맞게 넣지 않아도 됨. 아래 itemId->index 매핑을 따라감.")]
    public GameObject[] weaponPrefabs;
    // index 0: 1101 SR
    // index 1: 1201 AK
    // index 2: 1202 MP7
    // index 3: 1301 Shotgun
    // index 4: 1401 Pistol

    [Header("Fire Point")]
    [Tooltip("레이캐스트가 맞출 대상 레이어(Enemy 등)")]
    public LayerMask hitMask;

    // Bullet Visual (눈에 보이는 탄)
    [Header("Bullet Visual")]
    [Tooltip("눈에 보이는 탄 프리팹(없으면 생성 안 함)")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;
    public float bulletLifeTime = 2f;

    private float recoilAccumYaw = 0f;      // 누적된 Yaw(좌우) 반동 값
    public float recoilRecoverSpeed = 0f;   // 0이면 복구없음(리셋만) 10~30이면 서서히 복구

    public System.Action<float> onReloadStart;  // duration 전달
    public System.Action onReloadEnd;

    // 장착된 무기 오브젝트/총구
    private GameObject equippedWeaponGO; // 현재 소켓에 붙어있는 무기 오브젝트
    private Transform muzzle;            // 무기 프리팹 내부의 총구 트랜스폼
    private const string MUZZLE_NAME = "Muzzle"; // 무기 프리팹 내부 총구 오브젝트 이름

    // 인벤 호환 핵심: 장착 상태는 itemId + ItemInfo 캐시
    private int equippedItemId = 0;     // 0 = 맨손/미장착
    private ItemInfo weapon = null;

    // 기존 기능 유지용 런타임 상태 변수들
    private int currentAmmoInMag = 0;
    private bool isReloading = false;
    private float fireCooldown = 0f;
    private int recoilIndex = 0;
    private float lastFireTime = -999f;

    private PlayerController playerController;
    public CrosshairController crosshair;

    [Header("Debug")]
    [Tooltip("테스트용: 시작 시 자동으로 AK(1201) 장착")]
    public bool autoEquipOnStart = false;

    private Camera cachedCam;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        cachedCam = Camera.main;
    }

    private void Start()
    {
        // 자동 장착은 필요할 때만 켜기(기본 OFF 권장)
        if (autoEquipOnStart)
            EquipByItemId(1201);
    }

    private void Update()
    {
        if (crosshair != null)
        {
            crosshair.SetEnabled(weapon != null);
            crosshair.SetRunning(playerController != null && playerController.IsRunning);
        }

        // 무기 없으면 아무것도 하지않음
        if (weapon == null) return;

        // 쿨타임 감소
        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        // 재장전 중이면 발사 입력 무시
        if (isReloading) return;

        // 수동 재장전
        if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazinesize)
            StartCoroutine(ReloadRoutine());

        // 자동/단발 로직 유지 (ItemInfo.isAutomatic: 1이면 자동)
        bool fireInput = weapon.isAutomatic == 1
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (fireInput)
            TryFire();
    }

    //  인벤/장비 UI에서 itemId만 넘기면 장착되는 함수
    public bool EquipByItemId(int itemId)
    {
        Debug.Log($"[Weapon] EquipByItemId called: {itemId}");

        if (itemId <= 0)
        {
            Unequip();
            return false;
        }

        // [중요] 아이템 조회는 DataManager 단일 루트
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[Weapon] DataManager is null (not initialized?)");
            return false;
        }

        ItemInfo item = DataManager.Instance.GetItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[Weapon] Item not found. itemId={itemId}");
            Unequip();
            return false;
        }

        // 무기 아이템인지 체크 (가능하면 ItemInfo의 itemType 기반으로 바꿔라)
        if (!IsWeaponItem(item))
        {
            Debug.LogWarning($"[Weapon] Item is not weapon. itemId={itemId}, name={item.itemName}");
            return false;
        }

        equippedItemId = itemId;
        weapon = item;

        // 장착 시 상태 리셋 (데모/테스트 기준: 탄창 가득)
        currentAmmoInMag = Mathf.Max(0, weapon.magazinesize);
        recoilIndex = 0;
        fireCooldown = 0f;
        isReloading = false;
        recoilAccumYaw = 0f;

        // 비주얼 장착 + muzzle 찾기
        AttachWeaponVisual(itemId);

        Debug.Log($"[Weapon] Equipped: {weapon.itemName} (ID:{itemId})");
        return true;
    }

    public void Unequip()
    {
        equippedItemId = 0;
        weapon = null;

        currentAmmoInMag = 0;
        recoilIndex = 0;
        fireCooldown = 0f;
        isReloading = false;
        recoilAccumYaw = 0f;

        DetachWeaponVisual();
        Debug.Log("[Weapon] Unequipped");
    }

    private void TryFire()
    {
        if (weapon == null) return;
        if (fireCooldown > 0f) return;

        if (currentAmmoInMag <= 0)
        {
            Debug.Log("탄창 비었음 → 재장전 필요");
            return;
        }

        if (muzzle == null)
        {
            Debug.LogWarning("총구없어서 발사 불가");
            return;
        }

        Fire();

        if (crosshair != null) crosshair.OnFire();

        fireCooldown = 1f / Mathf.Max(0.01f, weapon.fireRate);
        currentAmmoInMag--;
    }

    // 판정(Raycast) = 카메라 위치 + 카메라 forward
    // 연출(Visual Bullet) = muzzle에서 hit.point 방향으로
    void Fire()
    {
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam == null)
        {
            Debug.LogWarning("[Weapon] Camera.main not found.");
            return;
        }

        Vector3 camOrigin = cachedCam.transform.position;
        Vector3 camDir = cachedCam.transform.forward;

        // 1) 먼저 카메라 기준으로 히트 판정
        Vector3 hitPoint = camOrigin + camDir * weapon.effectiveRange;
        bool hasHitPoint = false;

        if (Physics.Raycast(camOrigin, camDir, out RaycastHit hit, weapon.effectiveRange, hitMask))
        {
            hasHitPoint = true;
            hitPoint = hit.point;

            Debug.DrawLine(camOrigin, hit.point, Color.red, 0.2f);

            if (crosshair != null) crosshair.OnHitConfirm();

            EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
                enemy.TakeDamage(weapon.damage);
        }
        else
        {
            Debug.DrawRay(camOrigin, camDir * weapon.effectiveRange, Color.yellow, 0.2f);
        }

        // 2) muzzle에서 hitPoint로 발사 방향 생성 (여기서부터 반동/스프레드 적용)
        Vector3 origin = muzzle.position;
        Vector3 forward = (hitPoint - origin);

        // 너무 가까우면(카메라가 총구 뒤에 있거나) 안전 처리
        if (forward.sqrMagnitude < 0.0001f)
            forward = cachedCam.transform.forward;
        else
            forward.Normalize();

        Vector3 recoiledForward = ApplyRecoil(forward);

        int pellets = Mathf.Max(1, weapon.pelletsPerShot);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = GetSpreadDirection(recoiledForward, weapon.spreadAngle);

            // 연출 탄: 실제 hitPoint로 날려야 “맞은 지점에서 사라짐”이 성립
            Vector3? visualHit = hasHitPoint ? hitPoint : (Vector3?)null;

            SpawnVisualBullet(origin, dir, visualHit);
        }

        lastFireTime = Time.time;
    }

    private Vector3 ApplyRecoil(Vector3 forward)
    {
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        if (Time.time - lastFireTime > weapon.recoilResetTime)
        {
            recoilIndex = 0;
            recoilAccumYaw = 0f;
        }
        else
        {
            if (recoilRecoverSpeed > 0f)
                recoilAccumYaw = Mathf.MoveTowards(recoilAccumYaw, 0f, recoilRecoverSpeed * Time.deltaTime);
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

        float randomRange = Mathf.Abs((float)weapon.randomRecoilAngle);
        if (randomRange > 0f)
            deltaYaw += Random.Range(-randomRange, randomRange);

        recoilAccumYaw += deltaYaw;

        float finalYaw = baseYaw + recoilAccumYaw;
        float rad = finalYaw * Mathf.Deg2Rad;

        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;

        isReloading = true;
        onReloadStart?.Invoke(weapon.reloadTime);

        recoilIndex = 0;
        recoilAccumYaw = 0f;

        Debug.Log("재장전 시작");
        yield return new WaitForSeconds(weapon.reloadTime);

        currentAmmoInMag = weapon.magazinesize;
        isReloading = false;

        Debug.Log("재장전 완료");
        onReloadEnd?.Invoke();
    }

    // 수평(XZ) 스프레드 (현재 데모 기준 유지)
    Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0.01f)
            return forward;

        float half = spreadAngle * 0.5f;
        float yawOffset = Random.Range(-half, half);

        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float finalYaw = baseYaw + yawOffset;

        float rad = finalYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    void SpawnVisualBullet(Vector3 origin, Vector3 dir, Vector3? hitPoint = null)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab);
        var vb = bullet.GetComponent<VisualBullet>();

        if (vb == null)
        {
            Debug.LogWarning("[Bullet] VisualBullet component missing on bulletPrefab.");
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

    private Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void AttachWeaponVisual(int itemId)
    {
        if (weaponSocket == null)
        {
            Debug.LogWarning("[Weapon] weaponSocket is null. (Assign ik_hand_gun)");
            return;
        }

        ClearWeaponSocketChildren();
        DetachWeaponVisual();

        GameObject prefab = GetWeaponPrefab(itemId);
        if (prefab == null)
        {
            Debug.LogWarning($"[Weapon] No prefab mapped for itemId={itemId}");
            muzzle = null;
            return;
        }

        equippedWeaponGO = Instantiate(prefab, weaponSocket);
        equippedWeaponGO.transform.localPosition = Vector3.zero;
        equippedWeaponGO.transform.localRotation = Quaternion.identity;
        equippedWeaponGO.transform.localScale = Vector3.one;

        muzzle = FindChildRecursive(equippedWeaponGO.transform, MUZZLE_NAME);

        if (muzzle == null)
            Debug.LogWarning("[Weapon] Muzzle not found. 프리팹 안에 이름이 정확히 'Muzzle'인 오브젝트가 있어야 함.");
    }

    private void DetachWeaponVisual()
    {
        if (equippedWeaponGO != null)
            Destroy(equippedWeaponGO);

        equippedWeaponGO = null;
        muzzle = null;
    }

    private GameObject GetWeaponPrefab(int itemId)
    {
        if (weaponPrefabs == null || weaponPrefabs.Length < 5)
            return null;

        switch (itemId)
        {
            case 1101: return weaponPrefabs[0]; // SR
            case 1201: return weaponPrefabs[1]; // AK
            case 1202: return weaponPrefabs[2]; // MP7
            case 1301: return weaponPrefabs[3]; // Shotgun
            case 1401: return weaponPrefabs[4]; // Pistol
            default: return null;
        }
    }

    private float[] GetRecoilPatternByItemId(int itemId)
    {
        switch (itemId)
        {
            case 1101: return new float[] { 0.15f, 0.2f, 0.25f };
            case 1201:
                return new float[] { 0.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
                                            0.5f, 0.5f, 0.5f, 0.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f,
                                            -1.0f, -1.0f, -1.0f, -1.0f, -0.5f, -0.5f, 0.0f, 1.0f, 2.0f, 2.0f };
            case 1202: return new float[] { 0.1f, 0.2f, 0.3f };
            case 1301: return new float[] { 0.6f };
            case 1401: return new float[] { 0.15f, 0.15f };
            default: return new float[0];
        }
    }

    private bool IsWeaponItem(ItemInfo item)
    {
        if (item == null) return false;
        return item.id >= 1100 && item.id < 1500;
    }

    private void ClearWeaponSocketChildren()
    {
        if (weaponSocket == null) return;
        for (int i = weaponSocket.childCount - 1; i >= 0; i--)
            Destroy(weaponSocket.GetChild(i).gameObject);
    }


    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? weapon.magazinesize : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool HasWeaponEquipped() => weapon != null;
}
