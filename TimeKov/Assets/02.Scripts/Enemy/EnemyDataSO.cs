using UnityEngine;


public enum EnemyType { Melee, SuicideBomber, Gun, Turret } // 적의 타입 구분 (하나의 SO로 여러 적을 관리하기 위한 분기 기준)

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemy/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string enemyName;             // 적 이름 (디버그 / UI 표시용)
    public EnemyType enemyType;          // 적 타입 (행동 분기용 핵심 값)
    public float maxHP = 100f;           // 최대 체력
    public float moveSpeed = 3.5f;       // 기본 이동 속도 (순찰 등)
    public float chaseSpeed = 6.0f;      // 추적 시 이동 속도


    [Header("Detection")]
    public float patrolRadius = 10f;     // 순찰 범위 (랜덤 이동 반경)
    public float visionRange = 12f;      // 시야 거리 (플레이어 감지 거리)
    public float visionAngle = 110f;     // 시야 각도 (부채꼴 감지 각도)
    public float proximityRange = 4.0f;  // 근접 감지 거리 (각도 무시 감지)
    public float giveUpChaseRange = 20f; // 추적 포기 거리 (이 거리 넘어가면 추적 중단)
    public float provokedDuration = 10f; // 도발 상태 유지 시간 (플레이어를 잃어도 일정 시간 유지)

    [Header("Attack Settings")]
    public float attackDamage = 15f;     // 기본 공격 데미지
    public float attackRange = 1.5f;     // 공격 가능 거리 (근접 공격 기준)
    public float attackCooldown = 2.0f;  // 공격 간 쿨타임
    public float attackHitDelay = 0.5f;  // 공격 시작 → 실제 데미지 적용까지 딜레이 (애니 싱크용)
    public float attackAnimLength = 1.5f;// 공격 애니메이션 전체 길이 (상태 유지 시간)

    [Header("Type Specific: Suicide Bomber")]
    public float explosionRadius = 4.0f; // 폭발 범위
    public bool dieAfterAttack = true;   // 공격 후 즉시 사망 여부


    [Header("Type Specific: Melee Jump")]
    public bool useJumpAttack = false;           // 점프 공격 사용 여부
    public float jumpAttackDamage = 25f;         // 점프 공격 데미지
    public float jumpAttackRadius = 3.0f;        // 점프 공격 범위
    public float jumpWindup = 0.35f;             // 점프 준비 시간 (딜레이)
    public float jumpHitDelay = 1.1f;            // 점프 후 실제 타격까지 시간
    public float jumpFullTime = 2.8f;            // 점프 공격 전체 지속 시간
    public float jumpLungeSpeed = 10f;           // 점프 돌진 속도
    [Range(0f, 1f)] public float jumpChanceOnMiss = 0.7f; // 공격 빗나갔을 때 점프 공격 확률

    // 은신관련
    [Header("Type Specific: stealth")]
    public bool useStealth;             // 은신 기능 사용 여부
    public float stealthAlpha = 0.35f;  // 은신 상태 투명도
    public float visibleAlpha = 1f;     // 일반 상태 투명도
    public bool revealOnHit = true;     // 피격 시 은신 해제 여부

    //터렛관련
    [Header("Type Specific: Turret")]
    public float turretRange = 15f;                 // 터렛 사거리
    public float turretRotateSpeed = 8f;            // 타겟 추적 회전 속도
    public float turretFireCooldown = 1.0f;         // 발사 쿨타임
    public float turretProjectileSpeed = 12f;       // 발사체 속도
    public float turretProjectileLifeTime = 3f;     // 발사체 생존 시간

    [Header("DropTable Link")]
    public string dropSourceId;   // dropTable.csv의 sourceId와 정확히 일치해야 함
    public int dropTier = 0;      // 현재 구조상 대부분 0이면 충분


    [Header("VFX & Audio")]
    public GameObject hitVFXPrefab;     // 피격 시 이펙트 프리팹
    public AudioClip hitSound;          // 피격 사운드
    public AudioClip footstepSound;     // 이동 시 발소리
    public AudioClip chaseRoarSound;    // 추적 시작 시 사운드
    public AudioClip normalAttackSound; // 일반 공격 사운드
    public AudioClip jumpAttackSound;   // 점프 공격 사운드
    public AudioClip explosionSound;    // 폭발 사운드
}