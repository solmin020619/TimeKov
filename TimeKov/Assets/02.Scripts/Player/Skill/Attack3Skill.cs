using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Attack3Skill", menuName = "Skills/Attack3")]
public class Attack3Skill : ComboAttackBase
{
    [Header("Slash")]
    public float SlashForce = 12f;
    public float SlashDuration = 0.3f;

    protected override float GetAnimDuration() => AnimDuration;
    public float AnimDuration = 1.0f;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();

        anim?.PlayAttack(ComboIndex);

        movement.StartSlash(SlashForce, SlashDuration);

        yield return new WaitForSeconds(AnimDuration);

        OnAttackHit(caster);
    }

    public override void OnInterrupt(GameObject caster)
    {
        caster.GetComponent<PlayerMovementComponent>()?.LockMovement(false);
    }
}