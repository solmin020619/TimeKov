using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill2_CycloneBreak", menuName = "Skills/Skill2_CycloneBreak")]
public class Skill2_CycloneBreak : SkillBase
{
    [Header("Rotation Hits (1~4타)")]
    public float RotationDamage = 35f;      // 회전 각 타격 데미지
    public float RotationRadius = 2.5f;     // 회전 범위 반경 (m)
    public float Hit1Time = 0.4f;     // 1타 타이밍 (초)
    public float Hit2Time = 0.6f;     // 2타 타이밍 (초)
    public float Hit3Time = 0.8f;     // 3타 타이밍 (초)
    public float Hit4Time = 1.0f;     // 4타 타이밍 (초)

    [Header("Jump Slash (5타)")]
    public float JumpDamage = 140f;        // 점프 베기 데미지
    public float JumpRadius = 3.5f;        // 점프 베기 범위 반경 (m)
    public float JumpHitTime = 1.6f;        // 점프 베기 타이밍 (초)

    [Header("Settings")]
    public float TotalDuration = 2.2f;  // 스킬 전체 길이 (초)
    public float HitHeight = 1.0f;  // 판정 높이
    public LayerMask EnemyLayer;            // 적 레이어 마스크

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(1);

        yield return new WaitForSeconds(Hit1Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit2Time - Hit1Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit3Time - Hit2Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(Hit4Time - Hit3Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(JumpHitTime - Hit4Time);
        AttackUtils.HitSphere(caster, JumpRadius, JumpDamage, HitHeight, EnemyLayer);

        yield return new WaitForSeconds(TotalDuration - JumpHitTime);
    }
}