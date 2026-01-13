using UnityEngine;


public enum WeaponType
{
    Pistol,
    Rifle,
    SMG,
    Shotgun,
    Sniper
}

// ScriptableObject로 각 무기 스탯을 에셋으로 관리   
[CreateAssetMenu(menuName ="TimeKov/Weapon Data", fileName = "NewWeaponData")]
public class WeaponData : ScriptableObject
{
    public GameObject equipPrefab;  // 손에 붙일 무기 모델

    [Header("Basic Info")]
    public string weaponId;          // "PM", "AK", "MP7"
    public string weaponName;        // 표시용 이름
    public WeaponType weaponType;
    public float weight;

    [Header("Fire Stats")]
    public bool isAutomatic = false; // true면 꾹 눌러서 연사
    public float fireRate = 4f;      // 초당 발사 수 
    public float baseDamage = 20f;   // 한 발 데미지
    public float range = 20f;        // 사거리

    [Header("Magazine / Reload")]
    public int magazineSize = 12;    // 탄창 용량
    public float reloadTime = 1.2f;  // 재장전 시간(초)

    [Header("Recoil")]
    [Tooltip("true면 무기별 고정 패턴 + 랜덤 반동 사용")]
    public bool useRecoilPattern = true;

    // 샷 n발째에 적용할 탄각 패턴
    [Tooltip("AK 같은 무기에서 사용할 샷별 반동 패턴 (각도 배열)")]
    public float[] recoilPattern;

    // 패턴 위에 추가로 섞일 랜덤 반동
    [Tooltip("패턴에 추가되는 랜덤 반동 (좌우 ±도 단위)")]
    public float randomRecoilAngle = 1.0f;

    // 이 시간(초) 이상 안 쏘면 패턴 인덱스를 0으로 리셋
    [Tooltip("이 시간 동안 발사 안 하면 반동 패턴 초기화")]
    public float recoilResetTime = 0.25f;

    [Header("Spread / Shotgun")]
    public int pelletsPerShot = 1;   // 샷건이면 6~12, 나머지 1
    public float spreadAngle = 0f;   // 퍼짐 각도(도 단위, 0이면 직선)

    [Header("Bullet Tier")]
    public int bulletTier = 1;       // 탄 레벨 (1,2,3...)
}
