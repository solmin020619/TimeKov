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

    // =====================================================================
    // 바닥 원(수직 실린더) 광역 판정
    //
    // [왜 HitSphere 로는 안 되나 - 바닥에 원이 그려지는 광역기 전용 문제]
    //   HitSphere 는 발밑이 아니라 heightOffset(보통 1m) 위에 뜬 '공'이다. 그래서
    //     1) 바닥 기준 반경이 sqrt(r^2 - h^2) 로 줄어든다. r=2.5,h=1 이면 바닥에선 2.29.
    //        보이는 원에 반경을 맞춰도 원 테두리의 적이 안 맞는다.
    //     2) 키 큰 적은 몸통이 공에 닿아 맞고, 납작한 적은 같은 거리에서도 안 맞는다.
    //        같은 원 안인데 결과가 갈려서 "판정이 이상하다"가 된다.
    //   바닥 원 광역기는 '수평 거리'만으로 판정해야 보이는 것과 일치한다.
    //
    // [어떻게]
    //   넉넉한 구체로 후보를 모으고(브로드페이즈), 수평 거리 <= radius 인 것만 남긴다.
    //   height 는 원기둥의 높이(발밑 기준) - 공중에 높이 뜬 적을 제외하는 용도.
    //   판정 중심은 캐스터 발밑(root)이라, 시전 VFX 도 발밑에서 스폰해야 원 중심이 맞는다
    //   (SkillBase.CastVfxAtRoot 참고 - 손 뼈에서 스폰하면 원이 손 위치로 밀려난다).
    // =====================================================================
    public static void HitCylinder(
        GameObject caster,
        float radius,
        float height,
        float damage,
        LayerMask enemyLayer,
        GameObject hitVfxPrefab = null,
        Vector3 hitVfxOffset = default,
        float hitVfxLifeTime = 1.5f,
        Action onHitEnemy = null)
    {
        var stat = caster.GetComponent<PlayerStatComponent>();
        Vector3 foot = caster.transform.position;

        // 브로드페이즈: 원기둥을 감싸는 구체. 반지름은 대각선 길이라야 모서리 후보가 안 빠진다.
        float probe = Mathf.Sqrt(radius * radius + height * height * 0.25f);
        Vector3 center = foot + Vector3.up * (height * 0.5f);
        int count = Physics.OverlapSphereNonAlloc(center, probe, _hitBuffer, enemyLayer);

        bool hitAny = false;

        for (int i = 0; i < count; i++)
        {
            var hit = _hitBuffer[i];
            if (!hit.TryGetComponent<EnemyHealth>(out var enemy)) continue;

            // 수평 거리는 '콜라이더에서 기둥 축에 가장 가까운 점'으로 잰다.
            // transform.position 으로 재면 원점이 발밑에 있는 큰 적이 원 테두리에서 억울하게 빠진다.
            Vector3 p = hit.ClosestPoint(new Vector3(foot.x, hit.bounds.center.y, foot.z));
            float dx = p.x - foot.x;
            float dz = p.z - foot.z;
            if (dx * dx + dz * dz > radius * radius) continue;

            // 높이: 적의 몸통 범위가 기둥 구간과 겹치는지(발밑 ~ 발밑+height).
            if (hit.bounds.max.y < foot.y || hit.bounds.min.y > foot.y + height) continue;

            float enemyDef = 0f;
            float finalDamage = stat != null
                              ? stat.CalculateAttackDamage(damage, enemyDef)
                              : damage;

            enemy.TakeDamage(finalDamage, false, hit.transform.position + Vector3.up * 1f);

            if (hitVfxPrefab != null)
                VfxUtils.SpawnAtHit(hitVfxPrefab, hit, hitVfxOffset, hitVfxLifeTime);

            hitAny = true;
        }

        DrawRangeCircle(caster, radius);

        if (hitAny) onHitEnemy?.Invoke();
    }

    // =====================================================================
    // 판정 반경 확인용 원 (기본 OFF)
    //
    // ★VFX 파티클의 '보이는 크기'는 코드로 측정할 수 없다. 렌더러 bounds 는 컬링 볼륨이라
    //   실제보다 훨씬 크고, startSize/텍스처 여백/크기 커브 때문에 프리팹 값으로도 못 맞춘다
    //   (모래정령 궁극에서 이걸로 오래 헤맸다). 그래서 정답은 '판정 반경을 그려서 눈으로 맞추기'다.
    //   이 원은 판정에 쓰는 radius 를 그대로 그리므로, VFX 원과 겹쳐 보이면 맞은 것이다.
    //   맞춘 뒤에는 다시 끈다(연출용이 아니다 - 07-30 에 코드 원은 미관상 폐기된 이력이 있다).
    // =====================================================================
    public static bool ShowRangeCircle = false;

    private static void DrawRangeCircle(GameObject caster, float radius)
    {
        if (!ShowRangeCircle || caster == null) return;

        const int Seg = 64;
        var go = new GameObject("[판정범위]");
        go.transform.position = caster.transform.position + Vector3.up * 0.05f;   // 지면 z-fighting 방지
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = Seg;
        lr.widthMultiplier = 0.08f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(1f, 0.25f, 0.25f, 0.9f);
        for (int i = 0; i < Seg; i++)
        {
            float a = i / (float)Seg * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        UnityEngine.Object.Destroy(go, 1.5f);
    }
}
