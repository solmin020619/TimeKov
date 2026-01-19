using UnityEngine;

public enum EnemyType
{
    Melee,
    SuicideBomber,
    Gun,
    Turret
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Basic Info")]
    public string enemyName;
    public EnemyType enemyType;
    public float maxHP = 100f;
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 6.0f;

    public float patrolRadius = 10f;

    [Header("Detection")]
    public float visionRange = 12f;
    public float visionAngle = 110f;
    public float proximityRange = 4.0f;
    public float giveUpChaseRange = 20f;
    public float provokedDuration = 10f;

    [Header("Attack (Common / Suicide)")]
    public float attackDamage = 15f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2.0f;
    public float attackHitDelay = 0.5f;
    public float attackAnimLength = 1.5f;

    [Header("Specific: Suicide Bomber")]
    public float explosionRadius = 4.0f;
    public bool dieAfterAttack = true;

    [Header("Specific: Melee Jump Attack")]
    public bool useJumpAttack = false;
    public float jumpAttackDamage = 25f;
    public float jumpAttackRadius = 3.0f;
    public float jumpWindup = 0.35f;
    public float jumpHitDelay = 1.1f;
    public float jumpFullTime = 2.867f;
    public float jumpLungeSpeed = 10f;
    [Range(0f, 1f)] public float jumpChanceOnMiss = 0.7f;
}