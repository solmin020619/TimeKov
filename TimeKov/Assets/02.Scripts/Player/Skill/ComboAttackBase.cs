using System.Collections;
using UnityEngine;

public abstract class ComboAttackBase : ScriptableObject
{
    [Header("Combo")]
    public int ComboIndex = 0;
    public float ComboWindow = 1.2f;

    [Header("Attack")]
    public float Damage = 10f;
    public float HitRadius = 1.5f;          // 공격 범위
    public float HitDistance = 1.5f;        // 앞으로 얼마나
    public LayerMask EnemyLayer;            // Enemy 레이어

    public virtual IEnumerator ExecuteRoutine(GameObject caster)
    {
        var anim = caster.GetComponent<PlayerAnimatorComponent>();
        anim?.PlayAttack(ComboIndex);

        yield return new WaitForSeconds(GetAnimDuration());

        OnAttackHit(caster);
    }

    protected virtual void OnAttackHit(GameObject caster)
    {
        // 캐릭터 앞쪽 범위 내 적 탐지
        Vector3 hitCenter = caster.transform.position
                          + caster.transform.forward * HitDistance
                          + Vector3.up;

        Collider[] hits = Physics.OverlapSphere(hitCenter, HitRadius, EnemyLayer);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<EnemyHealth>(out var enemy))
            {
                enemy.TakeDamage(Damage, false, hit.transform.position + Vector3.up * 1.5f);
            }
        }
    }
    protected abstract float GetAnimDuration();
    public virtual void OnInterrupt(GameObject caster) { }
}