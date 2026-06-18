using System;
using UnityEngine;

public static class AttackUtils
{
    // OverlapSphereNonAlloc 재사용 버퍼(타격마다 배열 할당 방지). 적 콜라이더 수 합보다 넉넉히 -> 광역에 다 들어가도 히트 누락 없음(적이 자식 콜라이더 다수여도 여유).
    private static readonly Collider[] _hitBuffer = new Collider[64];

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

        int count = Physics.OverlapSphereNonAlloc(
            caster.transform.position + Vector3.up * heightOffset,
            radius,
            _hitBuffer,
            enemyLayer
        );

        bool hitAny = false;

        for (int i = 0; i < count; i++)
        {
            var hit = _hitBuffer[i];
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
