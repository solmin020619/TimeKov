using UnityEngine;

/// <summary>
/// 근거리 적 변종을 데이터만으로 정의하는 ScriptableObject.
/// EnemyBrain 이 Awake/Start 에서 이 SO 의 값을 EnemyHealth / NavMeshAgent / VisionSensor /
/// BT Blackboard 변수에 주입하므로, prefab 1개 + SO N개로 변종 N개를 만들 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "MeleeEnemyData", menuName = "Enemy/Melee Enemy Data")]
public class MeleeEnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Melee";

    [Header("Health")]
    public float maxHP = 100f;

    [Header("Movement")]
    [Tooltip("NavMeshAgent.speed 에 적용.")]
    public float moveSpeed = 4f;

    [Header("Vision")]
    public float visionRange = 12f;
    [Range(0f, 360f)] public float visionAngle = 110f;

    [Header("Attack")]
    public float attackDamage = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    // ─── 피드백 (VFX/Sound) ────────────────────────────────────────────────
    // EnemyFeedback 이 행동별로 호출. 비워두면 해당 이벤트는 조용히 skip.

    [Header("Feedback - Spawn (생성 시)")]
    public GameObject spawnVFX;
    public AudioClip spawnSound;

    [Header("Feedback - Detect (Player 발견 시)")]
    public GameObject detectVFX;
    public AudioClip detectSound;

    [Header("Feedback - Hit (피격)")]
    public GameObject hitVFX;
    public AudioClip hitSound;

    [Header("Feedback - Attack (공격 시작)")]
    public GameObject attackVFX;
    public AudioClip attackSound;

    [Header("Feedback - Death (사망)")]
    public GameObject deathVFX;
    public AudioClip deathSound;
}
