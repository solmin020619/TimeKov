using System;
using UnityEngine;

public static class AttackUtils
{
    // 구체 범위 공격 + 피격 이펙트 (옵션)
    // onHitEnemy: 적이 1명 이상 맞았을 때 호출되는 콜백 (스킬 적중 사운드 등)
    public static void HitSphere(
        GameObject caster,
        float radius,
        float damage,
        float heightOffset,
        LayerMask enemyLayer,
        GameObject hitVfxPrefab = null,
        Vector3 hitVfxOffset = default,
        float hitVfxLifeTime = 1.5f,
        Action onHitEnemy = null)
    {
        var stat = caster.GetComponent<PlayerStatComponent>();

        Collider[] hits = Physics.OverlapSphere(
            caster.transform.position + Vector3.up * heightOffset,
            radius,
            enemyLayer
        );

        bool hitAny = false;

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

            hitAny = true;
        }

        // 1명 이상 적중 시 콜백 (스킬 적중 사운드 등)
        if (hitAny) onHitEnemy?.Invoke();
    }
}
