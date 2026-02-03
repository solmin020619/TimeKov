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
    [Range(0f, 1f)] public float damageReduction = 0.7f;
    public float groggyDuration = 5.0f;

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

    [Header("Pattern 1: Rush (Charge)")]
    public float rushDamage = 35f;
    public float rushSpeed = 20.0f;
    public float rushPrepareTime = 2.0f; // 2초 대기 후 돌진
    public float rushCooldown = 12.0f;


    [Header("Pattern 2: Wide Swing (휘두르기)")]
    public float swingDamage = 45f;       // 강력한 한 방
    public float swingRadius = 6.0f;      // 전방 6m 범위
    public float swingAngle = 180f;       // 전방 180도 부채꼴
    public float swingChargeTime = 2.0f;  // 2초 기 모으기 (회피 시간)
    public float swingCooldown = 10.0f;
    public float swingHitDelay = 0.2f;    // 휘두르는 순간의 판정 딜레이

    [Header("Pattern 3: Earth Shatter (지면 폭파)")]
    public float shatterDamage = 40f;     // 첫 내려찍기 데미지
    public float shatterRange = 10.0f;    // 전방 10m 길이
    public float shatterAngle = 45f;      // 전방 45도 부채꼴 (꽤 좁고 긴 범위)
    public float shatterCooldown = 15.0f;
    public float shatterHitDelay = 0.6f;  // 내려찍는 모션 시간

    [Header("Earth Shatter - Aftershocks (후속타)")]
    public float explosionDamage = 30f;   // 추가 폭발 데미지
    public float explosionRadius = 2.5f;  // 폭발 반경
    public float explosionDelay = 0.3f;   // 폭발 간격 (시간차)
    public float explosionGap = 2.5f;     // 폭발이 옆으로 퍼지는 거리 간격


    [Header("Pattern: Araxia (Summon)")]
    public int phase1SummonCount = 2;
    public int phase2SummonCount = 3;
    public int phase3SummonCount = 4;
}