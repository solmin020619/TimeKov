using UnityEngine;

public static class AttackUtils
{
    // 구체 범위 내 적에게 데미지 적용
    public static void HitSphere(
        GameObject caster,
        float radius,
        float damage,
        float heightOffset,
        LayerMask enemyLayer)
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

            // 적 DEF는 팀원 코드에 추가되면 여기서 가져옴
            // float enemyDef = hit.GetComponent<EnemyAI>()?.data?.def ?? 0f;
            float enemyDef = 0f;

            // 최종 데미지 = 기본 데미지 + 플레이어 ATK - 적 DEF, 최솟값 1
            float finalDamage = stat != null
                              ? stat.CalculateAttackDamage(damage, enemyDef)
                              : damage;

            enemy.TakeDamage(finalDamage, false, hit.transform.position + Vector3.up * heightOffset);
        }
    }
}