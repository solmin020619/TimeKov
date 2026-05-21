using System.Collections;
using UnityEngine;

public abstract class ComboAttackBase : ScriptableObject
{
    [Header("Combo")]
    public int   ComboIndex  = 0;
    public float ComboWindow = 1.2f;

    [Header("Attack")]
    public float    Damage     = 10f;
    public float    HitRadius  = 2.5f;
    public float    HitHeight  = 1.0f;
    public LayerMask EnemyLayer;

    [Header("Gauge")]
    public SkillSheetId GaugeTarget;
    public float        GaugeAmount = 20f;

    // ─────────────────────────────────────────────────────────
    // VFX 설정
    // ─────────────────────────────────────────────────────────
    [Header("VFX - 공격 이펙트")]
    public GameObject    AttackVfxPrefab;
    // 스폰 위치 기준 뼈 (RightHand = 검을 쥔 손 위치)
    public HumanBodyBones AttackVfxBone = HumanBodyBones.RightHand;
    // root(캐릭터) 축 기준 위치 오프셋
    public Vector3 AttackVfxOffset         = new Vector3(0f, 0f, 0.3f);
    // root.rotation 기준 추가 회전 (슬래시 각도 조정 — Hovl Studio 기준)
    public Vector3 AttackVfxRotationOffset = new Vector3(0f, 0f, 0f);
    public float   AttackVfxLifeTime       = 0.5f;
    // 애니메이션 시작 후 VFX 스폰까지 딜레이 (스윙 피크 타이밍)
    [Tooltip("0 = 즉시 스폰. 스윙 모션 중간에 맞추려면 0.1~0.25 권장")]
    public float   AttackVfxDelay          = 0.15f;

    [Header("VFX - 피격 이펙트")]
    public GameObject HitVfxPrefab;
    public Vector3    HitVfxOffset   = new Vector3(0f, 1f, 0f);
    public float      HitVfxLifeTime = 1.5f;

    // ─────────────────────────────────────────────────────────
    public virtual IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim     = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();
        var rb       = caster.GetComponent<Rigidbody>();

        movement.LockMovement(true);
        anim?.PlayAttack(ComboIndex);

        // 스윙 피크까지 딜레이 후 이펙트 스폰
        if (AttackVfxDelay > 0f)
            yield return new WaitForSeconds(AttackVfxDelay);

        VfxUtils.SpawnAtBone(
            AttackVfxPrefab,
            caster,
            AttackVfxBone,
            AttackVfxOffset,
            AttackVfxRotationOffset,
            AttackVfxLifeTime
        );

        // 남은 시간 대기 (총 AnimDuration - 이미 소비한 딜레이)
        float remaining = Mathf.Max(0f, GetAnimDuration() - AttackVfxDelay);
        yield return new WaitForSeconds(remaining);

        OnAttackHit(caster);

        if (rb != null)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        movement.LockMovement(false);
    }

    protected virtual void OnAttackHit(GameObject caster)
    {
        var stat  = caster.GetComponent<PlayerStatComponent>();
        var skill = caster.GetComponent<PlayerSkillComponent>();

        Collider[] hits = Physics.OverlapSphere(
            caster.transform.position + Vector3.up * HitHeight,
            HitRadius,
            EnemyLayer
        );

        bool hitAny = false;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<EnemyHealth>(out var enemy)) continue;

            float enemyDef   = 0f;
            float finalDamage = stat != null
                              ? stat.CalculateAttackDamage(Damage, enemyDef)
                              : Damage;

            enemy.TakeDamage(finalDamage, false, hit.transform.position + Vector3.up * HitHeight);

            VfxUtils.SpawnAtHit(HitVfxPrefab, hit, HitVfxOffset, HitVfxLifeTime);

            hitAny = true;
        }

        if (hitAny)
            skill?.AddGauge(GaugeTarget, GaugeAmount);
    }

    public virtual void OnInterrupt(GameObject caster)
    {
        var rb       = caster.GetComponent<Rigidbody>();
        var movement = caster.GetComponent<PlayerMovementComponent>();

        if (rb != null)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        movement?.LockMovement(false);
    }

    protected abstract float GetAnimDuration();
}
