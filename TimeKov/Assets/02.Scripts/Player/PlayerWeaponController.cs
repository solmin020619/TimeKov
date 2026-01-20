using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Weapon Equip Visual")]
    [Tooltip("무기 프리팹이 붙을 손/무기 소켓(오른손 본 등)")]
    public Transform weaponSocket; // 오른손 본/weapon socket

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

    // 장착된 무기 오브젝트/총구
    private GameObject equippedWeaponGO; // 현재 손에 붙어있는 무기 오브젝트
    private Transform muzzle;            // 무기 프리팹 내부의 총구 트랜스폼
    private const string MUZZLE_NAME = "Muzzle"; // 무기 프리팹 내부 총구 오브젝트 이름(Find 용)

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
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        EquipByItemId(1201); // 테스트용 로그
    }

    private void Update()
    {
        if(crosshair != null)
        {
            crosshair.SetEnabled(weapon != null);
        }

        if (crosshair != null)
            crosshair.SetRunning(playerController != null && playerController.IsRunning);


        // 무기 없으면 아무것도 하지않음
        if (weapon == null) return;

        // 쿨타임 감소
        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        // 재장전 중이면 발사 입력 무시
        if (isReloading) return;

        // 수동 재장전
        if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazinesize)
        {
            StartCoroutine(ReloadRoutine());
        }

        // 자동/단발 로직 유지 (WeaponData.isAutomatic → ItemInfo.IsAutomatic)
        bool fireInput = weapon.IsAutomatic == 1
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (fireInput)
            TryFire();
    }

    //  인벤/장비 UI에서 itemId만 넘기면 장착되는 함수
    //  itemDatabase에서 ItemInfo 조회
    //  내부 weapon 변수에 캐싱
    //  탄창/리코일/쿨타임 상태 리셋
    //  무기 프리팹을 손에 붙이고 muzzle 찾기
    public bool EquipByItemId(int itemId)
    {
        Debug.Log($"[Weapon] EquipByItemId called: {itemId}");  // 테스트 용 로그

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
        // 안전 체크 (외부 호출 대비)
        if (weapon == null) return;

        // 발사 쿨타임 체크
        if (fireCooldown > 0f) return;

        // 탄창 체크
        if (currentAmmoInMag <= 0)
        {
            Debug.Log("탄창 비었음 → 재장전 필요");
            return;
        }

        // 총구 체크
        if (muzzle == null)
        {
            Debug.LogWarning("총구없어서 발사 불가");
            return;
        }

        // 실제 발사
        Fire();

        if(crosshair != null) crosshair.OnFire();

        // 발사 간격 설정 fireRate = 초당 발사 수 → 간격 = 1 / fireRate
        fireCooldown = 1f / Mathf.Max(0.01f, weapon.FireRate);

        // 탄 소모
        currentAmmoInMag--;
    }

    void Fire()
    {
        // 총구 위치
        Vector3 origin = muzzle.position;

        // 플레이어가 바라보는 방향
        Vector3 forward = transform.forward;

        // 마우스가 바라보는 바닥 지점를 기준으로 발사 방향을 만든다.
        if(TryGetAimPoint(out Vector3 aimPoint))
        {
            forward = aimPoint - origin;
            forward.y = 0f;

            if(forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;
            else
                forward.Normalize();
        }

        // 무기 데이터 기반으로 각도 패턴 + 랜덤 반동 적용
        Vector3 recoiledForward = ApplyRecoil(forward);

        // 그 위에 spreadAngle로 탄 퍼짐(샷건/정확도) 추가
        int pellets = Mathf.Max(1, weapon.PelletsPerShot);

        for (int i = 0; i < pellets; i++)
        {
            // 퍼짐(Spread)을 적용한 실제 발사 방향 계산
            Vector3 dir = GetSpreadDirection(recoiledForward, weapon.SpreadAngle);

            Vector3? hitPoint = null;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, weapon.EffectiveRange, hitMask))
            {
                // 맞았을떄 디버그 라인
                Debug.DrawLine(origin, hit.point, Color.red, 0.2f);

                hitPoint = hit.point;

                if (crosshair != null) crosshair.OnHitConfirm();


                // 맞은 대상이 EnemyHealth를 가지고 있으면 데미지 적용
                EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    // TODO: 여기 나중에 bulletTier, 방어력 등 공식 추가
                    enemy.TakeDamage(weapon.Damage);
                }
            }
            else
            {
                // 아무것도 안맞으면 사거리까지 노란색 디버그 레이
                Debug.DrawRay(origin, dir * weapon.EffectiveRange, Color.yellow, 0.2f);
            }

            // 눈에 보이는 탄 모델은 따로 앞으로 날림
            SpawnVisualBullet(origin, dir, hitPoint);
        }

        // 마지막 발사 시간 기록 -> 다음 발사에서 패턴 리셋 여부 체크에 사용
        lastFireTime = Time.time;

        // TODO: 여기서 총구 이펙트 / 사운드 / 반동 호출
    }

    private Vector3 ApplyRecoil(Vector3 forward)
    {
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        // 일정 시간 이상 안 쐈으면 패턴 인덱스 리셋
        if (Time.time - lastFireTime > weapon.RecoilResetTime)
        {
            recoilIndex = 0;
            recoilAccumYaw = 0f;
        }
        else
        {
            // 쉬는 동안 서서히 복구 (0이면 안 움직임)
            if(recoilRecoverSpeed > 0f)
            {
                recoilAccumYaw = Mathf.MoveTowards(recoilAccumYaw,0f,recoilRecoverSpeed * Time.deltaTime);
            }
        }
        float deltaYaw = 0f;

        // 반동 패턴 사용 여부는 CSV의 UseRecoilPattern으로 제어
        if (weapon.UseRecoilPattern == 1)
        {
            float[] pattern = GetRecoilPatternByItemId(equippedItemId);

            if (pattern != null && pattern.Length > 0)
            {
                // 패턴 길이 안에서는 정상 적용
                deltaYaw += pattern[recoilIndex];
                recoilIndex++;
            }
        }

        float randomRange = Mathf.Abs((float)weapon.RandomRecoilAngle);

        if(randomRange > 0f)
        {
            deltaYaw += Random.Range(-randomRange, randomRange);
        }

        // 누적
        recoilAccumYaw += deltaYaw;

        float finalYaw = baseYaw + recoilAccumYaw;
        float rad = finalYaw * Mathf.Deg2Rad;

        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;

        isReloading = true;

        // 재장전 시작 시 반동 초기화
        recoilIndex = 0;
        recoilAccumYaw = 0f;

        Debug.Log("재장전 시작");

        yield return new WaitForSeconds(weapon.ReloadTime);


        currentAmmoInMag = weapon.magazinesize;
        isReloading = false;

        Debug.Log("재장전 완료");
    }

    // 수평(XZ) 스프레드 (쿼터뷰용)
    Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0.01f)
            return forward;

        // forward의 yaw 기준으로 ±(spreadAngle/2) 안에서 랜덤
        float half = spreadAngle * 0.5f;
        float yawOffset = Random.Range(-half, half);

        // forward의 yaw 계산
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float finalYaw = baseYaw + yawOffset;

        float rad = finalYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    bool TryGetAimPoint(out Vector3 aimPoint)
    {
        Camera cam = Camera.main;

        if(cam == null)
        {
            aimPoint = Vector3.zero;
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Y축 고정 플레이이므로 플레이어가 서있는 눈높이를 바닥으로 본다
        float groundY = transform.position.y;
        Plane ground = new Plane(Vector3.up,new Vector3(0f, groundY, 0f));

        if(ground.Raycast(ray,out float enter))
        {
            aimPoint = ray.GetPoint(enter);
            return true;
        }

        aimPoint = Vector3.zero;
        return false;
    }

    void SpawnVisualBullet(Vector3 origin, Vector3 dir,Vector3? hitPoint = null)
    {
        if (bulletPrefab == null) return;

        //Debug.Log($"[Bullet] Spawn at {origin}, dir={dir}, prefab={(bulletPrefab ? bulletPrefab.name : "NULL")}"); 테스트 용 로그

        GameObject bullet = Instantiate(bulletPrefab);
        var vb = bullet.GetComponent<VisualBullet>();

        if(vb == null)
        {
            Debug.LogWarning("[Bullet] VisualBullet component missing on bulletPrefab.");
            Destroy(bullet);
            return;
        }

        // 맞았을 때 너무 짧으면 안 보일 수 있으니 최소 lifeTime 보정
        float lt = bulletLifeTime;

        if (hitPoint.HasValue)
        {
            float dist = Vector3.Distance(origin, hitPoint.Value);
            float t = dist / Mathf.Max(0.01f, bulletSpeed);
            lt = Mathf.Max(0.02f, t); // 최소 1~2프레임은 보이게
        }

        vb.Init(origin, dir, bulletSpeed, lt, hitPoint);
    }

    private Transform FindChildRecursive(Transform parent,string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == name)
                return child;

            Transform Found = FindChildRecursive(child, name);
            if (Found != null)
                return Found;
        }
        return null;
    }

    private void AttachWeaponVisual(int itemId)
    {
        if (weaponSocket == null) return;

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
        {
            Debug.LogWarning("[Weapon] Muzzle not found. 프리팹 안에 이름이 정확히 'Muzzle'인 오브젝트가 있어야 함.");
        }
    }

    private void DetachWeaponVisual()
    {
        if (equippedWeaponGO != null)
            Destroy(equippedWeaponGO);

        equippedWeaponGO = null;
        muzzle = null;
    }

    // itemId → 무기 프리팹 매핑 (5개)
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

    // WeaponData의 recoilPattern[]을 itemId 기반으로 대체.
    // 총기 추가 없고 5개 고정이므로 하드코딩이 가장 안전
    // 패턴 모양은 여기서만 관리 (유지보수 포인트 1곳)
    private float[] GetRecoilPatternByItemId(int itemId)
    {
        switch (itemId)
        {
            // SR
            case 1101: return new float[] { 0.15f, 0.2f, 0.25f };
            
            // AK
            case 1201: return new float[] { 0.0f,  // 1: 0.0 - 0.0
                                            0.5f,  // 2: 0.5 - 0.0
                                            0.5f,  // 3: 1.0 - 0.5
                                            0.5f,  // 4: 1.5 - 1.0
                                            0.5f,  // 5: 2.0 - 1.5
                                            0.5f,  // 6: 2.5 - 2.0
                                            0.5f,  // 7: 3.0 - 2.5
                                            0.5f,  // 8: 3.5 - 3.0
                                            0.5f,  // 9: 4.0 - 3.5
                                            0.5f,  // 10: 4.5 - 4.0
                                            0.5f,  // 11: 5.0 - 4.5
                                            0.5f,  // 12: 5.5 - 5.0
                                            0.5f,  // 13: 6.0 - 5.5
                                            0.0f,  // 14: 6.0 - 6.0
                                           -1.0f,  // 15: 5.0 - 6.0
                                           -1.0f,  // 16: 4.0 - 5.0
                                           -1.0f,  // 17: 3.0 - 4.0
                                           -1.0f,  // 18: 2.0 - 3.0
                                           -1.0f,  // 19: 1.0 - 2.0
                                           -1.0f,  // 20: 0.0 - 1.0
                                           -1.0f,  // 21: -1.0 - 0.0
                                           -1.0f,  // 22: -2.0 - -1.0
                                           -1.0f,  // 23: -3.0 - -2.0
                                           -1.0f,  // 24: -4.0 - -3.0
                                           -0.5f,  // 25: -4.5 - -4.0
                                           -0.5f,  // 26: -5.0 - -4.5
                                            0.0f,  // 27: -5.0 - -5.0
                                            1.0f,  // 28: -4.0 - -5.0
                                            2.0f,  // 29: -2.0 - -4.0
                                            2.0f   // 30: 0.0 -  -2.0
                                          };
            // MP7
            case 1202: return new float[] { 0.1f, 0.2f, 0.3f };
            // Shotgun
            case 1301: return new float[] { 0.6f };
            // Pistol
            case 1401: return new float[] { 0.15f, 0.15f };
            
            default: return new float[0];
        }
    }

    // Weapon 판단 (itemType 무시)
    private bool IsWeaponItem(ItemInfo item)
    {
        if(item == null) return false;

        // 1100~1499 = 무기 (SR/AR/SG/PISTOL)
        return item.id >= 1100 && item.id < 1500;
    }

    // UI용 탄창 정보
    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? weapon.magazinesize : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool HasWeaponEquipped() => weapon != null;
}
