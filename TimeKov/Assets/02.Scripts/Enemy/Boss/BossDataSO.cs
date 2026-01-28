using UnityEngine;

public enum BossType { Hunter, Araxia }

[CreateAssetMenu(fileName = "NewBossData", menuName = "Enemy/Boss Data")]
public class BossDataSO : ScriptableObject
{
    [Header("Common Info")]
    public string bossName;
    public BossType type;
    public float maxHP = 1000f;
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 8.0f;
    public float rotationSpeed = 10.0f;

    [Header("Defense & State")]
    [Range(0f, 1f)] public float damageReduction = 0.7f; // 데미지 감소율
    public float groggyDuration = 5.0f; // 그로기(무력화) 시간

    [Header("Detection")]
    public float patrolRadius = 15f;
    public float visionRange = 20f;
    public float visionAngle = 120f;
    public float proximityRange = 5.0f;
    public float giveUpChaseRange = 30f;
    public float provokedDuration = 10f;

    [Header("Basic Attack")]
    public float basicAttackDamage = 20f;
    public float basicAttackRange = 3.0f;
    public float basicAttackCooldown = 1.5f;
    public float basicAttackHitDelay = 0.3f;
    public float basicAttackAnimLength = 1.0f;

    [Header("Pattern: Hunter (Rush)")]
    public float rushDamage = 35f;
    public float rushSpeed = 20.0f;
    public float rushPrepareTime = 2.0f;
    public float rushCooldown = 8.0f;

    [Header("Pattern: Araxia (Summon)")]
    public int phase1SummonCount = 2; // HP 100%
    public int phase2SummonCount = 3; // HP 70%
    public int phase3SummonCount = 4; // HP 30%
}