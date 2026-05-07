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
