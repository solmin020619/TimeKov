using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill1_ReaperSlash", menuName = "Skills/Skill1_ReaperSlash")]
public class Skill1_ReaperSlash : SkillBase
{
    [Header("Hit 1")]
    public float Hit1Delay = 0.25f;     // 1타 히트 시작 시간 (초)
    public float Hit1Damage = 80f;      // 1타 기본 데미지
    public float Hit1Radius = 2.5f;     // 1타 범위 반경 (m)

    [Header("Hit 2")]
    public float Hit2Delay = 0.6f;     // 2타 히트 시작 시간 (초)
    public float Hit2Damage = 120f;     // 2타 기본 데미지
    public float Hit2Radius = 3.0f;     // 2타 범위 반경 (m)

    [Header("Settings")]
    public float TotalDuration = 0.9f;  // 스킬 전체 길이 (초)
    public float HitHeight = 1.0f;  // 판정 높이
    public LayerMask EnemyLayer;            // 적 레이어 마스크

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(0);

        yield return new WaitForSeconds(Hit1Delay);
        AttackUtils.HitSphere(caster, Hit1Radius, Hit1Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit2Delay - Hit1Delay);
        AttackUtils.HitSphere(caster, Hit2Radius, Hit2Damage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(TotalDuration - Hit2Delay);
    }
}