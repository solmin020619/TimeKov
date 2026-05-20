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
