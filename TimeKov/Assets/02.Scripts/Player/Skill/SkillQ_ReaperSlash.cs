using System.Collections;
using UnityEngine;

// Q 키 스킬. 어느 키로 나가는지는 에셋의 SkillSheetId(=Skill1)가 정한다(PlayerSkillComponent 디스패치).
[CreateAssetMenu(fileName = "SkillQ_ReaperSlash", menuName = "TIMEKOV/스킬/Q 리퍼 슬래시")]
public class SkillQ_ReaperSlash : SkillBase
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

    [Header("Camera Shake")]
    public float Hit1ShakeDuration  = 0.10f;
    public float Hit1ShakeMagnitude = 0.05f;
    public float Hit2ShakeDuration  = 0.15f;
    public float Hit2ShakeMagnitude = 0.08f;

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
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(Hit1ShakeDuration, Hit1ShakeMagnitude); });

        yield return new WaitForSeconds(Hit2Delay - Hit1Delay);
        AttackUtils.HitSphere(caster, Hit2Radius, Hit2Damage, HitHeight, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(Hit2ShakeDuration, Hit2ShakeMagnitude); });

        yield return new WaitForSeconds(TotalDuration - Hit2Delay);
    }

    // SkillData 시트 적용. 히트 슬롯 2개를 그대로 받는다(hit1Count/hit2Count 는 항상 1이라 안 쓴다).
    public override void ApplySheetValues(SkillDataSheetData row)
    {
        base.ApplySheetValues(row);
        Hit1Damage = row.hit1Damage;
        Hit1Radius = row.hit1Radius;
        Hit2Damage = row.hit2Damage;
        Hit2Radius = row.hit2Radius;
    }
}
