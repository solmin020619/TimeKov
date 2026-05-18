using System.Collections;
using UnityEngine;

public abstract class ComboAttackBase : ScriptableObject
{
    [Header("Combo")]
    public int ComboIndex = 0;       // 콤보 순서 (0 = 1타, 1 = 2타, 2 = 3타)
    public float ComboWindow = 1.2f;    // 다음 콤보 입력 가능 시간 (초)

    [Header("Attack")]
    public float Damage = 10f;     // 기본 데미지 (flat 수치)
    public float HitRadius = 2.5f;    // 공격 범위 반경 (m)
    public float HitHeight = 1.0f;    // 판정 높이 (지면 기준)
    public LayerMask EnemyLayer;            // 적 레이어 마스크

    [Header("Gauge")]
    public SkillSheetId GaugeTarget;        // 적중 시 충전할 스킬 게이지
    public float GaugeAmount = 20f;  // 적중 시 충전량

    // 스킬 실행 진입점, PlayerSkillComponent에서 호출
    public virtual IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        var movement = caster.GetComponent<PlayerMovementComponent>();

        // 공격 중 이동 잠금
        movement.LockMovement(true);

        anim?.PlayAttack(ComboIndex);

        yield return new WaitForSeconds(GetAnimDuration());

        OnAttackHit(caster);

        // 공격 끝나면 이동 해제
        movement.LockMovement(false);
    }

    // 히트 판정 및 데미지 적용, 하위 클래스에서 override 가능
    protected virtual void OnAttackHit(GameObject caster)
    {
        var stat = caster.GetComponent<PlayerStatComponent>();
        var skill = caster.GetComponent<PlayerSkillComponent>();

        Collider[] hits = Physics.OverlapSphere(
            caster.transform.position + Vector3.up * HitHeight,
            HitRadius,
            EnemyLayer
        );

        bool hitAny = false;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<EnemyHealth>(out var enemy)) continue;

            float enemyDef = 0f;
            float finalDamage = stat != null
                              ? stat.CalculateAttackDamage(Damage, enemyDef)
                              : Damage;

            enemy.TakeDamage(finalDamage, false, hit.transform.position + Vector3.up * HitHeight);
            hitAny = true;
        }

        // 적 적중 시에만 게이지 충전
        if (hitAny) skill?.AddGauge(GaugeTarget, GaugeAmount);
    }

    // 스킬 중단 시 호출, 하위 클래스에서 cleanup override
    public virtual void OnInterrupt(GameObject caster)
    {
        // 중단 시 이동 잠금 해제
        caster.GetComponent<PlayerMovementComponent>()?.LockMovement(false);
    }

    // 애니메이션 길이, 하위 클래스에서 반드시 구현
    protected abstract float GetAnimDuration();
}