using UnityEngine;

public static class AttackUtils
{
    // 구체 범위 공격 + 피격 이펙트 (옵션)
    public static void HitSphere(
        GameObject caster,
        float radius,
        float damage,
        float heightOffset,
        LayerMask enemyLayer,
        GameObject hitVfxPrefab = null,
        Vector3 hitVfxOffset = default,
        float hitVfxLifeTime = 1.5f)
    {
        var stat = caster.GetComponent<PlayerStatComponent>();

        Collider[] hits = Physics.OverlapSphere(
            caster.transform.position + Vector3.up * heightOffset,
            radius,
            enemyLayer
        );

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<EnemyHealth>(out var enemy)) continue;

            float enemyDef = 0f;
            float finalDamage = stat != null
                              ? stat.CalculateAttackDamage(damage, enemyDef)
                              : damage;

            enemy.TakeDamage(finalDamage, false, hit.transform.position + Vector3.up * heightOffset);

            // 피격 이펙트
            if (hitVfxPrefab != null)
                VfxUtils.SpawnAtHit(hitVfxPrefab, hit, hitVfxOffset, hitVfxLifeTime);
        }
    }
}
