using UnityEngine;

[CreateAssetMenu(fileName = "MeleeEnemyData", menuName = "Enemy/Melee Enemy Data")]
public class MeleeEnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Melee";

    [Header("Health")]
    public float maxHP = 100f;

    [Header("Movement")]
    public float moveSpeed = 4f;

    [Header("Vision")]
    public float visionRange = 12f;
    [Range(0f, 360f)] public float visionAngle = 110f;

    [Header("Attack")]
    public float attackDamage = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Attack Timing")]
    [Tooltip("공격 애니메이션 시작 후 데미지 들어가는 시점(초)")]
    public float hitDelay = 0.5f;
    [Tooltip("공격 애니메이션 전체 길이(초). 이 시간 동안 다음 행동 잠금")]
    public float animLength = 1.5f;

    [Header("Animator Triggers")]
    [Tooltip("피격 시 Animator에 호출할 trigger 이름. Animator Controller에 같은 이름의 Trigger 파라미터가 있어야 함. 없으면 호출 무시됨.")]
    public string hitTrigger = "Hit";
    [Tooltip("플레이어 첫 발견 시 trigger")]
    public string detectTrigger = "Detect";
    [Tooltip("사망 시 trigger")]
    public string dieTrigger = "Die";

    [Header("Behavior")]
    [Tooltip("시야에서 벗어난 후에도 N초 동안 마지막 타깃 기억 (즉시 정지 방지)")]
    public float targetLostMemory = 1.5f;
    [Tooltip("사망 애니메이션 재생 시간. 이 시간 후 GameObject Destroy.")]
    public float deathAnimDuration = 1.5f;
    [Tooltip("Detect 애니메이션(Roar/Howl 등) 재생 동안 적이 멈춰있는 시간(초). 끝나면 추격 시작. 0이면 멈추지 않음.")]
    public float detectStunDuration = 1.5f;

    [Header("Feedback - Spawn")]
    public GameObject spawnVFX;
    public AudioClip spawnSound;

    [Header("Feedback - Detect")]
    public GameObject detectVFX;
    public AudioClip detectSound;

    [Header("Feedback - Hit")]
    public GameObject hitVFX;
    public AudioClip hitSound;

    [Header("Feedback - Attack")]
    public GameObject attackVFX;
    public AudioClip attackSound;

    [Header("Feedback - Death")]
    public GameObject deathVFX;
    public AudioClip deathSound;
}
