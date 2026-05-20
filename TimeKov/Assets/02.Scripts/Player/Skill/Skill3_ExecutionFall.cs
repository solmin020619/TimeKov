using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill3_ExecutionFall", menuName = "Skills/Skill3_ExecutionFall")]
public class Skill3_ExecutionFall : SkillBase
{
    [Header("Hit 1")]
    public float Hit1Delay = 0.7f;   // 선딜 (이 구간 피격 시 스킬 중단)
    public float Hit1Damage = 80f;
    public float Hit1Radius = 2.5f;

    [Header("Hit 2")]
    public float Hit2Delay = 1.2f;
    public float Hit2Damage = 220f;
    public float Hit2Radius = 3.0f;

    [Header("Settings")]
    public float TotalDuration = 1.8f;
    public float HitHeight = 1.0f;
    public LayerMask EnemyLayer;

    private bool _interrupted;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        _interrupted = false;

        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var skillComp = caster.GetComponent<PlayerSkillComponent>();

        anim?.PlaySkill(2);

        // 선딜 구간 시작 : 피격 시 Interrupt 허용
        if (skillComp != null) skillComp.CurrentSkillIsInterruptible = true;

        yield return new WaitForSeconds(Hit1Delay);

        // 선딜 구간 종료 : Interrupt 불허
        if (skillComp != null) skillComp.CurrentSkillIsInterruptible = false;

        if (_interrupted) yield break;

        AttackUtils.HitSphere(caster, Hit1Radius, Hit1Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit2Delay - Hit1Delay);
        AttackUtils.HitSphere(caster, Hit2Radius, Hit2Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(TotalDuration - Hit2Delay);
    }

    public override void OnInterrupt(GameObject caster)
    {
        _interrupted = true;

        var skillComp = caster.GetComponent<PlayerSkillComponent>();
        if (skillComp != null) skillComp.CurrentSkillIsInterruptible = false;
    }
}