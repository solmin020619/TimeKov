using UnityEngine;

[CreateAssetMenu(fileName = "MeleeEnemyData", menuName = "Enemy/Melee Enemy Data")]
public class MeleeEnemyData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("HP 바 표시 이름 (한국어 OK)")]
    public string enemyName = "Melee";

    [Tooltip("퀘스트/드롭 매칭 ID (영어 snake_case). 비우면 enemyName 사용")]
    public string enemyId = "";

    [Header("Health")]
    [Tooltip("최대 체력")]
    public float maxHP = 100f;

    [Header("Movement")]
    [Tooltip("이동 속도 (m/s)")]
    public float moveSpeed = 4f;

    [Tooltip("가속도 (m/s²). moveSpeed × 2~4 권장. 낮으면 짧은 거리에서 max speed 도달 못함")]
    public float acceleration = 12f;

    [Tooltip("회전 속도 (deg/sec). 360~720 권장")]
    public float angularSpeed = 480f;

    [Tooltip("목표 도달 정지 거리 (m). 0이면 정확히 도달까지 이동")]
    public float stoppingDistance = 0f;

    [Header("Vision")]
    [Tooltip("시야 거리 (m)")]
    public float visionRange = 12f;

    [Tooltip("시야 각도 (도). 좌우 합산")]
    [Range(0f, 360f)] public float visionAngle = 110f;

    [Header("Attack")]
    [Tooltip("공격 1회 데미지")]
    public float attackDamage = 15f;

    [Tooltip("공격 가능 거리 (m). 이 거리까지 추적 후 공격")]
    public float attackRange = 2f;

    [Tooltip("추적 도달 거리 = attackRange × 이 값. 1.0 미만이면 더 가까이 접근 후 공격 진입. 콜라이더 두께/부동소수점 오차 보정용. 공격 사거리 안에서 안정적으로 공격하려면 0.8~0.9 권장.")]
    [Range(0.5f, 1.0f)]
    public float attackApproachRatio = 0.85f;

    [Tooltip("공격 간 대기 시간 (초)")]
    public float attackCooldown = 1.5f;

    [Header("Attack Timing")]
    [Tooltip("공격 시작 후 데미지 들어가는 시점 (초). 애니메이션의 피격 프레임에 맞춰 조정")]
    public float hitDelay = 0.5f;

    [Tooltip("공격 모션 전체 길이 (초). 이 시간 동안 다음 행동 잠금")]
    public float animLength = 1.5f;

    [Header("Animator Triggers")]
    [Tooltip("피격 시 호출할 Animator Trigger 이름")]
    public string hitTrigger = "Hit";

    [Tooltip("플레이어 첫 발견 시 호출할 Animator Trigger 이름")]
    public string detectTrigger = "Detect";

    [Tooltip("사망 시 호출할 Animator Trigger 이름")]
    public string dieTrigger = "Die";

    [Header("Behavior")]
    [Tooltip("시야에서 벗어난 후에도 타깃을 기억하는 시간 (초). 즉시 정지 방지")]
    public float targetLostMemory = 1.5f;

    [Tooltip("사망 모션 재생 시간 (초). 이 시간 후 GameObject 삭제")]
    public float deathAnimDuration = 1.5f;

    [Tooltip("발견 후 정지하는 시간 (초). 발견 모션 재생용. 0이면 즉시 추격")]
    public float detectStunDuration = 1.5f;

    [Header("Feedback - Spawn")]
    [Tooltip("출현 시 재생 VFX")]
    public GameObject spawnVFX;
    [Tooltip("출현 시 재생 소리")]
    public AudioClip spawnSound;

    [Header("Feedback - Detect")]
    [Tooltip("플레이어 발견 시 재생 VFX")]
    public GameObject detectVFX;
    [Tooltip("플레이어 발견 시 재생 소리")]
    public AudioClip detectSound;

    [Header("Feedback - Hit")]
    [Tooltip("피격 시 재생 VFX (피격 지점)")]
    public GameObject hitVFX;
    [Tooltip("피격 시 재생 소리")]
    public AudioClip hitSound;

    [Header("Feedback - Attack")]
    [Tooltip("공격 시 재생 VFX")]
    public GameObject attackVFX;
    [Tooltip("공격 시 재생 소리")]
    public AudioClip attackSound;

    [Header("Feedback - Death")]
    [Tooltip("사망 시 재생 VFX")]
    public GameObject deathVFX;
    [Tooltip("사망 시 재생 소리")]
    public AudioClip deathSound;
}
