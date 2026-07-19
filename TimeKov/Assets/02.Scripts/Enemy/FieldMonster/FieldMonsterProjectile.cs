using UnityEngine;

/// <summary>
/// 필드 몬스터 원거리 공격용 발사체. 순수 파티클 VFX 프리팹(예: Frost/Projectile_Frost)에
/// 런타임으로 붙여 이동·충돌을 코드가 구동한다. (기존 스크립트는 하나도 수정하지 않음)
///
/// 동작:
///   - 발사 순간 방향으로 직진(homingDeg>0 이면 타깃을 향해 서서히 휘어짐).
///   - 매 프레임 이번 이동 '선분'과 타깃(가슴 높이) 사이 최단거리가 hitRadius 이내면 명중
///     -> PlayerStatComponent.TakeDamage, 임팩트 VFX 생성, 자기 파괴. (빠른 속도 터널링 방지)
///   - 수명(lifeTime) 초과하면 빗나감으로 간주하고 사라짐.
///
/// 회피 가능: 직선(homingDeg=0)이면 발사 시점 위치로 날아가므로 옆으로 피하면 빗나간다.
/// </summary>
public class FieldMonsterProjectile : MonoBehaviour
{
    private Transform target;          // 살아있는 플레이어(유도/명중 판정 대상)
    private Vector3 velocity;          // 현재 진행 속도벡터
    private float speed;
    private float damage;
    private float hitRadius;
    private float homingDeg;           // 유도 회전(도/초). 0=직선
    private float homingUntil;         // 이 시각까지만 유도(초반만 따라감). 0이면 수명 내내
    private float aimHeight;           // 타깃 조준/판정 높이(가슴)
    private float dieAt;               // 수명 종료 시각
    private Vector3 attackerPos;       // 넉백 방향용(쏜 몬스터 위치)
    private GameObject impactVFX;
    private LayerMask blockMask;       // 이 레이어(벽/나무/바위)에 막히면 관통 못 함
    private bool consumed;

    /// <param name="target">살아있는 플레이어 Transform(유도·명중 대상). null 이면 직진만 하다 소멸.</param>
    /// <param name="dir">발사 방향(정규화 안 돼도 됨).</param>
    public void Init(Transform target, Vector3 dir, float speed, float damage, float hitRadius,
                     float lifeTime, float homingDeg, float homingDuration, float aimHeight,
                     Vector3 attackerPos, GameObject impactVFX, LayerMask blockMask)
    {
        this.target = target;
        this.speed = speed;
        this.damage = damage;
        this.hitRadius = hitRadius;
        this.homingDeg = homingDeg;
        this.homingUntil = homingDuration > 0f ? Time.time + homingDuration : 0f;
        this.aimHeight = aimHeight;
        this.attackerPos = attackerPos;
        this.impactVFX = impactVFX;
        this.blockMask = blockMask;
        this.dieAt = Time.time + lifeTime;

        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();
        velocity = dir * speed;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    private void Update()
    {
        if (consumed) return;
        if (Time.time >= dieAt) { Destroy(gameObject); return; }

        // 유도 — 타깃(가슴)을 향해 서서히 방향 회전. (초반 시간대에만: homingUntil)
        bool homingActive = homingDeg > 0f && target != null
                            && (homingUntil <= 0f || Time.time < homingUntil);
        if (homingActive)
        {
            Vector3 want = AimPoint() - transform.position;
            if (want.sqrMagnitude > 0.0001f)
            {
                Vector3 newDir = Vector3.RotateTowards(
                    velocity.normalized, want.normalized,
                    homingDeg * Mathf.Deg2Rad * Time.deltaTime, 0f);
                velocity = newDir * speed;
                transform.rotation = Quaternion.LookRotation(newDir);
            }
        }

        Vector3 from = transform.position;
        Vector3 step = velocity * Time.deltaTime;
        float segLen = step.magnitude;
        Vector3 to = from + step;

        // 플레이어 명중까지 거리(이번 선분 위 파라미터 t). 없으면 +무한.
        float playerDist = float.PositiveInfinity;
        Vector3 playerHitPoint = to;
        if (target != null && segLen > 1e-6f)
        {
            Vector3 aim = AimPoint();
            Vector3 dirN = step / segLen;
            float t = Mathf.Clamp(Vector3.Dot(aim - from, dirN), 0f, segLen);
            Vector3 closest = from + dirN * t;
            if ((aim - closest).sqrMagnitude <= hitRadius * hitRadius)
            {
                playerDist = t;
                playerHitPoint = closest;
            }
        }

        // 장애물(벽/나무/바위)까지 거리 — 반경만큼 두껍게 SphereCast 로 확인.
        float wallDist = float.PositiveInfinity;
        Vector3 wallHitPoint = to;
        if (blockMask.value != 0 && segLen > 1e-6f)
        {
            Vector3 dirN = step / segLen;
            if (Physics.SphereCast(from, hitRadius * 0.5f, dirN, out var wall, segLen,
                                   blockMask, QueryTriggerInteraction.Ignore))
            {
                wallDist = wall.distance;
                wallHitPoint = wall.point;
            }
        }

        // 더 가까운 쪽이 먼저 발생. 벽이 앞이면 관통 못 하고 그 자리서 소멸(피격 없음).
        if (wallDist < playerDist && !float.IsPositiveInfinity(wallDist))
        {
            Blocked(wallHitPoint);
            return;
        }
        if (!float.IsPositiveInfinity(playerDist))
        {
            Hit(playerHitPoint);
            return;
        }

        transform.position = to;
    }

    private Vector3 AimPoint()
        => target.position + Vector3.up * aimHeight;

    // 플레이어 명중 — 피해 + 임팩트.
    private void Hit(Vector3 point)
    {
        consumed = true;
        if (target != null)
        {
            var ps = target.GetComponent<PlayerStatComponent>();
            if (ps != null) ps.TakeDamage(damage, attackerPos);
        }
        SpawnImpact(point);
        Destroy(gameObject);
    }

    // 장애물에 막힘 — 피해 없이 임팩트만.
    private void Blocked(Vector3 point)
    {
        consumed = true;
        SpawnImpact(point);
        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVFX == null) return;
        var fx = Instantiate(impactVFX, point, Quaternion.identity);
        // VFX 팩 데모 스크립트 제거(카메라 없으면 로그 폭탄).
        foreach (var mb in fx.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) Destroy(mb);
        Destroy(fx, 3f);
    }
}
