using UnityEngine;

public enum BossType { Hunter, Arachnia }

[CreateAssetMenu(fileName = "NewBossData", menuName = "TimeKov/Boss Data")]
public class BossData : ScriptableObject
{
    [Header("Common Info")]
    public string bossName;
    public BossType type;
    public float maxHP = 1000f;
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 8.0f;
    public float rotationSpeed = 10.0f;

    [Header("Defense Stats")]
    [Range(0f, 1f)] public float damageReduction = 0.7f;

    [Header("Detection & Patrol")]
    public float patrolRadius = 15f;
    public float visionRange = 15f;
    public float visionAngle = 120f;
    public float proximityRange = 5.0f;
    public float giveUpChaseRange = 30f;
    public float provokedDuration = 10f;

    [Header("Basic Attack")]
    public float basicAttackDamage = 20f;
    public float basicAttackRange = 2.0f;
    public float basicAttackCooldown = 1.5f;
    public float basicAttackHitDelay = 0.3f;
    public float basicAttackAnimLength = 1.0f;

    [Header("Skill: Rush (Hunter)")]
    public float rushDamage = 50f;
    public float rushSpeed = 25.0f;
    public float rushPrepareTime = 1.0f;
    public float groggyDuration = 4.0f;
    public float rushCooldown = 10.0f;
}