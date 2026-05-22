using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill2_CycloneBreak", menuName = "Skills/Skill2_CycloneBreak")]
public class Skill2_CycloneBreak : SkillBase
{
    [Header("Rotation Hits (1~4타)")]
    public float RotationDamage = 35f;
    public float RotationRadius = 2.5f;
    public float Hit1Time = 0.4f;
    public float Hit2Time = 0.6f;
    public float Hit3Time = 0.8f;
    public float Hit4Time = 1.0f;

    [Header("Jump Slash (5타)")]
    public float JumpDamage  = 140f;
    public float JumpRadius  = 3.5f;
    public float JumpHitTime = 1.6f;

    [Header("Settings")]
    public float TotalDuration = 2.2f;
    public float HitHeight     = 1.0f;
    public LayerMask EnemyLayer;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(1);

        // 시전 이펙트: 사이클론 시작 시 캐릭터 중심 스폰
        SpawnCastVfx(caster);

        var audio = caster.GetComponent<Player>()?.Audio;

        yield return new WaitForSeconds(Hit1Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(Hit2Time - Hit1Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(Hit3Time - Hit2Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(Hit4Time - Hit3Time);
        AttackUtils.HitSphere(caster, RotationRadius, RotationDamage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(JumpHitTime - Hit4Time);
        AttackUtils.HitSphere(caster, JumpRadius, JumpDamage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(TotalDuration - JumpHitTime);
    }
}
