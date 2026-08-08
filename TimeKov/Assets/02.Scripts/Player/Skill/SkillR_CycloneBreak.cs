using System.Collections;
using UnityEngine;

// R 키 궁극기. 어느 키로 나가는지는 에셋의 SkillSheetId(=Skill2)가 정한다(PlayerSkillComponent 디스패치).
[CreateAssetMenu(fileName = "SkillR_CycloneBreak", menuName = "Skills/SkillR_CycloneBreak")]
public class SkillR_CycloneBreak : SkillBase
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
    [Tooltip("판정 원기둥의 높이(발밑 기준, m). 바닥 원 광역기라 반경만 중요하고 이 값은\n" +
             "'공중에 높이 뜬 적을 뺄지' 만 정한다. 3 이면 지상 적은 전부 포함된다.")]
    public float HitCylinderHeight = 3.0f;
    public LayerMask EnemyLayer;

    [Header("Camera Shake")]
    public float RotationShakeDuration  = 0.08f;
    public float RotationShakeMagnitude = 0.04f;
    public float JumpShakeDuration      = 0.18f;
    public float JumpShakeMagnitude     = 0.10f;

    public override IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlaySkill(1);

        // 시전 이펙트: 사이클론 시작 시 캐릭터 중심 스폰
        SpawnCastVfx(caster);

        var audio = caster.GetComponent<Player>()?.Audio;

        yield return new WaitForSeconds(Hit1Time);
        AttackUtils.HitCylinder(caster, RotationRadius, HitCylinderHeight, RotationDamage, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(RotationShakeDuration, RotationShakeMagnitude); });

        yield return new WaitForSeconds(Hit2Time - Hit1Time);
        AttackUtils.HitCylinder(caster, RotationRadius, HitCylinderHeight, RotationDamage, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(RotationShakeDuration, RotationShakeMagnitude); });

        yield return new WaitForSeconds(Hit3Time - Hit2Time);
        AttackUtils.HitCylinder(caster, RotationRadius, HitCylinderHeight, RotationDamage, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(RotationShakeDuration, RotationShakeMagnitude); });

        yield return new WaitForSeconds(Hit4Time - Hit3Time);
        AttackUtils.HitCylinder(caster, RotationRadius, HitCylinderHeight, RotationDamage, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(RotationShakeDuration, RotationShakeMagnitude); });

        yield return new WaitForSeconds(JumpHitTime - Hit4Time);
        AttackUtils.HitCylinder(caster, JumpRadius, HitCylinderHeight, JumpDamage, EnemyLayer,
                               HitVfxPrefab, HitVfxOffset, HitVfxLifeTime,
                               onHitEnemy: () => { audio?.PlaySkillHit(); ThirdPersonCamera.Shake(JumpShakeDuration, JumpShakeMagnitude); });

        yield return new WaitForSeconds(TotalDuration - JumpHitTime);
    }
}
