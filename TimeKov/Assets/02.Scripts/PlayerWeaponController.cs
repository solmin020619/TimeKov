using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Weapon")]
    public WeaponData equippedWeapon;    // 현재 들고 있는 무기 null 이면 맨손
    public Transform muzzle;             // 총구 위치
    public LayerMask hitMask;            // 맞출 대상 (Enemy 레이어)

    [Header("Bullet visual")]
    public GameObject bulletPrefab;     // 눈에 보이는 탄 프리팹 
    public float bulletSpeed = 40f;     // 탄 이동속도
    public float bulletLifeTime = 2f;   // 탄 사라지는 시간

    private PlayerController playerController;
    private float fireCooldown = 0f;    // 발사 간격을 위한 쿨타임 타이머
    private int currentAmmoInMag;       // 현재 탄창에 남은 탄 수
    private bool isReloading = false;   // 재장전 중인지 여부 판단
    private int recoilIndex = 0;      // 지금 몇 번째 패턴 각도를 쓰는 중인지
    private float lastFireTime = 0f;  // 마지막 발사 시간 (패턴 리셋용)

    private void Start()
    {
        playerController = GetComponent<PlayerController>();

        if (equippedWeapon != null)
        {
            // 시작할떄 탄창을 꽉 채워줌
            currentAmmoInMag = equippedWeapon.magazineSize;
        }
    }

    private void Update()
    {
        // 쿨타임 감소
        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        // 재장전 중이면 발사 입력 무시
        if (isReloading) return;

        // 수동 재장전
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (equippedWeapon != null && currentAmmoInMag < equippedWeapon.magazineSize)
                StartCoroutine(ReloadRoutine());
        }

        // 발사 입력
        bool fireInput = equippedWeapon != null && equippedWeapon.isAutomatic
            ? Input.GetMouseButton(0)      // 자동: 꾹 누르면 계속 발사
            : Input.GetMouseButtonDown(0); // 단발: 클릭마다 한 발

        if (Input.GetMouseButtonDown(0))
        {
            TryFire();
        }
    }

    void TryFire()
    {
        // 무기가 없으면 아무일도 일어나지않음
        if (equippedWeapon == null) return;

        // 발사 쿨타임 체크
        if (fireCooldown > 0f) return;

        // 탄창 비었으면 리턴
        if (currentAmmoInMag <= 0)
        {
            Debug.Log("탄창 비었음 → 재장전 필요");
            return;
        }

        // 실제 발사
        Fire();

        // 발사 간격 설정 fireRate = 초당 발사 수 → 간격 = 1 / fireRate
        fireCooldown = 1f / Mathf.Max(0.01f, equippedWeapon.fireRate);

        // 탄 소모
        currentAmmoInMag--;
    }

    void Fire()
    {
        // 총구 위치가 설정되어 있으면 그 위치 아니면 플레이어 머리쯤에서 발사
        Vector3 origin = muzzle != null
            ? muzzle.position
            : (transform.position + Vector3.up * 1.0f);

        // 플레이어가 바라보는 방향
        Vector3 forward = transform.forward;

        // 무기 데이터 기반으로 각도 패턴 + 랜덤 반동 적용
        Vector3 recoiledForward = ApplyRecoil(forward);

        // 그 위에 spreadAngle로 탄 퍼짐(샷건/정확도) 추가
        int pellets = Mathf.Max(1, equippedWeapon.pelletsPerShot);

        for (int i = 0; i < pellets; i++)
        {
            // 퍼짐(Spread)을 적용한 실제 발사 방향 계산
            Vector3 dir = GetSpreadDirection(recoiledForward, equippedWeapon.spreadAngle);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, equippedWeapon.range, hitMask))
            {
                // 맞았을떄 디버그 라인
                Debug.DrawLine(origin, hit.point, Color.red, 0.2f);

                // 맞은 대상이 EnemyHealth를 가지고 있으면 데미지 적용
                EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    float damage = equippedWeapon.baseDamage;
                    // TODO: 여기 나중에 bulletTier, 방어력 등 공식 추가
                    enemy.TakeDamage(damage);
                }
            }
            else
            {
                // 아무것도 안맞으면 사거리까지 노란색 디버그 레이
                Debug.DrawRay(origin, dir * equippedWeapon.range, Color.yellow, 0.2f);
            }

            // 눈에 보이는 탄 모델은 따로 앞으로 날림
            SpawnVisualBullet(origin, dir);
        }

        // 마지막 발사 시간 기록 -> 다음 발사에서 패턴 리셋 여부 체크에 사용
        lastFireTime = Time.time;

        // TODO: 여기서 총구 이펙트 / 사운드 / 반동 호출
    }

    void SpawnVisualBullet(Vector3 origin,Vector3 dir)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(dir));

        // 프리팹에 RigidBody가 있다면 속도 적용
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = dir * bulletSpeed;

        // 일정시간 뒤 자동 삭제
        Destroy(bullet,bulletLifeTime);
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

        Vector3 dir = new Vector3(
            Mathf.Sin(finalYaw * Mathf.Deg2Rad),
            0f,
            Mathf.Cos(finalYaw * Mathf.Deg2Rad)
        );

        return dir.normalized;
    }

    Vector3 ApplyRecoil(Vector3 forward)
    {
        if(equippedWeapon == null)
            return forward;

        float baseYaw = Mathf.Atan2(forward.x,forward.z) * Mathf.Rad2Deg;

        float patternOffset = 0f;
        float randomOffset = 0f;

        // recoilResetTime 이상 안 쐈으면 패턴 인덱스 리셋
        if(Time.time - lastFireTime > equippedWeapon.recoilResetTime)
        {
            recoilIndex = 0;
        }

        // 반동패턴 발동
        if(equippedWeapon.useRecoilPattern && 
            equippedWeapon.recoilPattern != null &&
            equippedWeapon.recoilPattern.Length > 0)
        {
            int index = Mathf.Clamp(recoilIndex, 0, equippedWeapon.recoilPattern.Length - 1);
            patternOffset = equippedWeapon.recoilPattern[index];

            // 다음 발사에는 다음 패턴 사용 
            recoilIndex++;

            // 패턴 끝까지 가면 마지막 값 유지(혹은 0으로 돌리고 싶으면 여기서 0으로)
            if (recoilIndex >= equippedWeapon.recoilPattern.Length)
                recoilIndex = equippedWeapon.recoilPattern.Length - 1;
        }

        // 패턴 위에 랜덤 반동 섞기
        if (equippedWeapon.randomRecoilAngle > 0f)
            randomOffset = Random.Range(-equippedWeapon.randomRecoilAngle, equippedWeapon.randomRecoilAngle);

        float finalYaw = baseYaw + patternOffset + randomOffset;

        // 반동 확인용 디버그
        Debug.Log($"[Recoil] idx:{recoilIndex - 1}, pattern:{patternOffset}, random:{randomOffset}, finalYaw:{finalYaw}");

        float rad = finalYaw * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(rad), 0f ,Mathf.Cos(rad));

        return dir.normalized;
    }

    IEnumerator ReloadRoutine()
    {
        isReloading = true;
        Debug.Log("재장전 시작");

        yield return new WaitForSeconds(equippedWeapon.reloadTime);

        currentAmmoInMag = equippedWeapon.magazineSize;
        isReloading = false;
        Debug.Log("재장전 완료");
    }

    // 무기 장착
    public void EquipWeapon(WeaponData newWeapon)
    {
        equippedWeapon = newWeapon;

        if(equippedWeapon != null)
        {
            currentAmmoInMag = equippedWeapon.magazineSize;

            recoilIndex = 0;
            fireCooldown = 0;

            Debug.Log($"[Weapon] Equipped: {equippedWeapon.weaponName}");
        }
    }

    // 무기 해제
    public void UnequipWeapon()
    {
        equippedWeapon = null;
        currentAmmoInMag = 0;

        recoilIndex = 0;
        fireCooldown = 0f;

        Debug.Log("[Weapon] Unequipped");
    }

    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => equippedWeapon != null ? equippedWeapon.magazineSize : 0;
}
