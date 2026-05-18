using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack3Skill", menuName = "Skills/Attack3")]
public class Attack3Skill : ComboAttackBase
{
    [Header("Slash")]
    public float SlashForce = 12f;   // 전진 슬래시 힘
    public float SlashDuration = 0.3f;  // 슬래시 지속 시간 (초)

    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 1.0f;   // 전체 애니메이션 길이 (초)

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();
        var rb = caster.GetComponent<Rigidbody>();

        // 공격 중 이동 잠금
        movement.LockMovement(true);

        anim?.PlayAttack(ComboIndex);

        // 슬래시 시작
        movement.StartSlash(SlashForce, SlashDuration);

        // 전체 애니메이션 동안 이동 잠금 유지
        yield return new WaitForSeconds(AnimDuration);

        OnAttackHit(caster);

        // 공격 끝나고 수평 속도 초기화
        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);

        // 애니메이션 완전히 끝난 후 이동 해제
        movement.LockMovement(false);
    }

    public override void OnInterrupt(GameObject caster)
    {
        // 중단 시 이동 잠금 해제
        caster.GetComponent<PlayerMovementComponent>()?.LockMovement(false);
    }
}