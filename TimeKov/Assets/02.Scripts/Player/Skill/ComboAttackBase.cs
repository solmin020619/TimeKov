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

    [Header("스킬 쿨감 (평타 적중 시)")]
    [Tooltip("평타가 적에게 적중할 때마다 모든 스킬의 남은 쿨다운을 이 초만큼 줄인다 (LoL 나보리신속검 방식). 0이면 쿨감 없음.")]
    public float CdReducePerHit = 5f;

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
    [Tooltip("true = 캐릭터 회전/이동을 따라감. 슬래시는 보통 true 권장 (false면 캐릭터가 움직이는 동안 검 위치와 분리됨)")]
    public bool    AttackVfxFollowCaster   = true;

    [Header("VFX - 피격 이펙트")]
    public GameObject HitVfxPrefab;
    public Vector3    HitVfxOffset   = new Vector3(0f, 1f, 0f);
    public float      HitVfxLifeTime = 1.5f;

    [Header("Post-Hit")]
    [Tooltip("데미지 들어간 후 이동 잠금 유지 시간(초). 애니 마무리 동안 미끄러짐 방지. 0이면 즉시 해제. 콤보 입력 자체엔 영향 없음.")]
    public float PostHitLockDuration = 0f;

    [Header("Camera Shake")]
    public float ShakeDuration  = 0.08f;
    public float ShakeMagnitude = 0.04f;

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
            AttackVfxLifeTime,
            AttackVfxFollowCaster
        );

        // 남은 시간 대기 (총 AnimDuration - 이미 소비한 딜레이)
        float remaining = Mathf.Max(0f, GetAnimDuration() - AttackVfxDelay);
        yield return new WaitForSeconds(remaining);

        OnAttackHit(caster);

        // 데미지 후 마무리 시간: 애니 잔여 동안 이동 잠금 유지 (미끄러짐 방지)
        if (PostHitLockDuration > 0f)
            yield return new WaitForSeconds(PostHitLockDuration);

        // 공격 애니메이션이 완전히 끝날 때까지 이동 잠금 유지
        // (GetAnimDuration이 실제 애니보다 짧을 경우 끌림 방지)
        if (anim != null)
            yield return new WaitUntil(() => !anim.IsInActionAnim());

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
        {
            skill?.ReduceAllCooldowns(CdReducePerHit);
            // 공격 적중 사운드
            caster.GetComponent<Player>()?.Audio?.PlayAttackHit();
            // 카메라 셰이크
            ThirdPersonCamera.Shake(ShakeDuration, ShakeMagnitude);
        }
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
