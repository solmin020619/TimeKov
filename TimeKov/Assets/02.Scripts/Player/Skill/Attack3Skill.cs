using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack3Skill", menuName = "TIMEKOV/스킬/평타 3타")]
public class Attack3Skill : ComboAttackBase
{
    [Header("Slash")]
    public float SlashForce = 12f;
    public float SlashDuration = 0.3f;

    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 1.0f;

    [Header("Attack3 Extra VFX")]
    public GameObject SlashMoveVfxPrefab;
    public Vector3 SlashMoveVfxOffset = new Vector3(0f, 1f, 0.5f);
    public float SlashMoveVfxLifeTime = 1f;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();
        var rb = caster.GetComponent<Rigidbody>();

        movement.LockMovement(true);

        anim?.PlayAttack(ComboIndex);

        // 돌진/슬래시 이펙트: 즉시 (돌진 시작과 동기화)
        VfxUtils.SpawnAtCaster(
            SlashMoveVfxPrefab,
            caster,
            SlashMoveVfxOffset,
            SlashMoveVfxLifeTime,
            true
        );

        movement.StartSlash(SlashForce, SlashDuration);

        // 스윙 피크 딜레이 후 공격 이펙트 스폰
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

        float remaining = Mathf.Max(0f, AnimDuration - AttackVfxDelay);
        yield return new WaitForSeconds(remaining);

        OnAttackHit(caster);

        // 데미지 후 마무리 시간: 애니 잔여 동안 이동 잠금 유지 (Attack3는 돌진+슬래시라 특히 미끄러짐)
        if (PostHitLockDuration > 0f)
            yield return new WaitForSeconds(PostHitLockDuration);

        if (rb != null)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        movement.LockMovement(false);
    }

    public override void OnInterrupt(GameObject caster)
    {
        var movement = caster.GetComponent<PlayerMovementComponent>();
        movement?.CancelSlash();
        movement?.LockMovement(false);
    }
}