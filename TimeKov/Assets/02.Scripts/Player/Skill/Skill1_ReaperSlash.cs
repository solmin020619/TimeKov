using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Skill1_ReaperSlash", menuName = "Skills/Skill1_ReaperSlash")]
public class Skill1_ReaperSlash : SkillBase
{
    [Header("Hit 1")]
    public float Hit1Delay  = 0.25f;
    public float Hit1Damage = 80f;
    public float Hit1Radius = 2.5f;

    [Header("Hit 2")]
    public float Hit2Delay  = 0.6f;
    public float Hit2Damage = 120f;
    public float Hit2Radius = 3.0f;

    [Header("Settings")]
    public float TotalDuration = 0.9f;
    public float HitHeight     = 1.0f;
    public LayerMask EnemyLayer;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(0);

        // 시전 이펙트: 스킬 시작 직후 RightHand 뼈 위치에 스폰
        SpawnCastVfx(caster);

        var audio = caster.GetComponent<Player>()?.Audio;

        yield return new WaitForSeconds(Hit1Delay);
        AttackUtils.HitSphere(caster, Hit1Radius, Hit1Damage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(Hit2Delay - Hit1Delay);
        AttackUtils.HitSphere(caster, Hit2Radius, Hit2Damage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => audio?.PlaySkillHit());

        yield return new WaitForSeconds(TotalDuration - Hit2Delay);
    }
}
