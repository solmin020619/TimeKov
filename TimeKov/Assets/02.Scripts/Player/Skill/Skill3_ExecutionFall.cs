using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill3_ExecutionFall", menuName = "Skills/Skill3_ExecutionFall")]
public class Skill3_ExecutionFall : SkillBase
{
    [Header("Hit 1")]
    public float Hit1Delay = 0.7f;     // 1타 히트 시작 시간 (초)
    public float Hit1Damage = 80f;      // 1타 기본 데미지
    public float Hit1Radius = 2.5f;     // 1타 범위 반경 (m)

    [Header("Hit 2")]
    public float Hit2Delay = 1.2f;     // 2타 히트 시작 시간 (초)
    public float Hit2Damage = 220f;     // 2타 기본 데미지
    public float Hit2Radius = 3.0f;     // 2타 범위 반경 (m)

    [Header("Settings")]
    public float TotalDuration = 1.8f;  // 스킬 전체 길이 (초)
    public float HitHeight = 1.0f;  // 판정 높이
    public LayerMask EnemyLayer;            // 적 레이어 마스크

    // 선딜 중 피격 시 스킬 중단 여부
    private bool _interrupted;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        _interrupted = false;

        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(2);

        // 선딜 구간 (피격 시 중단)
        yield return new WaitForSeconds(Hit1Delay);

        if (_interrupted) yield break;

        AttackUtils.HitSphere(caster, Hit1Radius, Hit1Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit2Delay - Hit1Delay);
        AttackUtils.HitSphere(caster, Hit2Radius, Hit2Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(TotalDuration - Hit2Delay);
    }

    public override void OnInterrupt(GameObject caster)
    {
        _interrupted = true;
    }
}