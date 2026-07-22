using UnityEngine;

// 와이번 파이어볼 발사체. 직선 비행 -> 플레이어 근접/지면/수명초과 시 폭발(VFX + 범위 데미지).
// 보스(WyvernBossController)가 Launch로 방향/데미지/타깃을 주입해 발사. 프리팹은 WyvernBossBuilder가 조립.
public class WyvernFireball : MonoBehaviour
{
    [Header("비행")]
    [SerializeField] private float speed = 16f;
    [SerializeField] private float life = 5f;          // 수명(초). 빗나가도 이 시간 후 폭발/소멸
    [SerializeField] private float hitRadius = 1.3f;    // 플레이어 직격 판정 반경
    [SerializeField] private float explodeRadius = 2.6f;// 폭발 범위(이 안의 플레이어 피해)
    [SerializeField] private float groundY = -999f;     // 이 높이 이하로 내려가면 착탄(기본 비활성)

    [Header("연출")]
    [SerializeField] private GameObject explodeVfx;     // 착탄 VFX (WyvernBossBuilder가 할당)

    [Header("지형 충돌")]
    [SerializeField] private LayerMask groundMask = 1 << 7;   // 기본 Ground(7). 터레인이 다른 레이어면 인스펙터서 조정.
    [SerializeField] private float terrainHitRadius = 0.4f;   // 지형 충돌 판정 두께(SphereCast 반경)

    [Header("유도 (보스가 페이즈2+에서 켬)")]
    [SerializeField] private float homingTurnRate = 80f;     // 초당 회전(deg). 클수록 잘 따라옴
    [SerializeField] private float homingDuration = 2.5f;    // 이 시간만 유도(그 후 직선 = 회피 가능)

    private float _damage;
    private Vector3 _dir;
    private Transform _target;
    private float _timer;
    private bool _done;
    private bool _homing;

    // 보스 발사 시 호출: 방향 + 데미지 + 추적 대상(명중 판정용)
    public void Launch(Vector3 dir, float damage, Transform target)
    {
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        _damage = damage;
        _target = target;
        transform.rotation = Quaternion.LookRotation(_dir);
    }

    // 보스가 페이즈2+ 발사 시 켬: 일정 시간 플레이어 쪽으로 곡선 추적(직선이라 걸어서 피하던 것 보완)
    public void SetHoming(bool on) => _homing = on;

    // 이 탄과 착탄 VFX 를 지정한 색으로 물들인다. 안 부르면 원본 색 그대로다.
    public void SetTint(Color c)
    {
        _tint = c;
        _tinted = true;
        VfxTint.Apply(gameObject, c);
    }

    private Color _tint = Color.white;
    private bool _tinted;

    private void Update()
    {
        if (_done) return;
        float dt = Time.deltaTime;

        // 유도: 일정 시간 동안 방향을 플레이어 쪽으로 점진 회전
        if (_homing && _timer < homingDuration && _target != null)
        {
            Vector3 desired = (_target.position + Vector3.up) - transform.position;
            if (desired.sqrMagnitude > 0.0001f)
            {
                _dir = Vector3.RotateTowards(_dir, desired.normalized, homingTurnRate * Mathf.Deg2Rad * dt, 0f).normalized;
                transform.rotation = Quaternion.LookRotation(_dir);
            }
        }

        Vector3 prev = transform.position;
        Vector3 next = prev + _dir * (speed * dt);
        transform.position = next;
        _timer += dt;

        // 지형(터레인/건물 등) 충돌 -> 그 자리서 폭발(뚫고 지나가지 않게). 이번 프레임 이동구간을 SphereCast.
        float step = (next - prev).magnitude;
        if (step > 0f && Physics.SphereCast(prev, terrainHitRadius, _dir, out var hit, step, groundMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            Explode();
            return;
        }

        // 플레이어 직격
        if (_target != null)
        {
            Vector3 tp = _target.position + Vector3.up;   // 가슴 높이 근사
            if ((transform.position - tp).sqrMagnitude <= hitRadius * hitRadius) { Explode(); return; }
        }

        if (transform.position.y <= groundY || _timer >= life) Explode();
    }

    private void Explode()
    {
        if (_done) return;
        _done = true;

        if (explodeVfx != null)
        {
            var fx = Instantiate(explodeVfx, transform.position, Quaternion.identity);
            // ★착탄 VFX 는 여기서 새로 생기므로 발사체에 칠한 색이 안 따라온다.
            //   안 물들이면 맞는 순간에만 원래 색(보라 등)이 튀어나온다.
            if (_tinted) VfxTint.Apply(fx, _tint);
        }

        // 범위 데미지(플레이어만)
        if (_target != null)
        {
            var ps = _target.GetComponent<PlayerStatComponent>();
            if (ps != null && !ps.IsDead)
            {
                float d = Vector3.Distance(transform.position, _target.position + Vector3.up);
                if (d <= explodeRadius) ps.TakeDamage(_damage, transform.position);
            }
        }

        Destroy(gameObject);
    }
}
