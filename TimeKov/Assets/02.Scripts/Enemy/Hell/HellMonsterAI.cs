using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 헬 몬스터(용암 바이옴) 지상 4종 공용 컨트롤러.
// 헬하운드 / 헬뱃 / 헬버그 / 헬사이클롭 이 같은 회사 에셋이라 클립 규격을 공유한다.
// 몹별 차이는 전부 HellMonsterData 의 attacks 배열과 시그니처 플래그로 낸다.
//
// [핵심 규칙] ★모든 공격은 전조를 거친다.
//   공격 진입점이 PerformAttack 하나뿐이고, 그 안에서 반드시 Telegraph 를 먼저 await 한다.
//   전조 없이 때리는 경로 자체를 만들지 않는 것이 이 클래스의 설계 의도다.
//   일반몹이라 전조 시간을 넉넉히 준다(기본 1초). 플레이어가 보고 피할 수 있어야 한다.
//
// 기존 EnemyBrain/BehaviorGraph 미사용. 보스처럼 전용 컨트롤러다.
// 재원의 FieldMonster 와도 별개다(에셋 회사가 달라 애니 규격이 안 맞는다).
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class HellMonsterAI : MonoBehaviour, IEnemyDataSource
{
    [Header("데이터")]
    [SerializeField] private HellMonsterData data;
    public MeleeEnemyData Data => data;

    [Header("애니메이터")]
    [SerializeField] private string speedParam = "Speed";

    [Header("입/총구 위치")]
    [Tooltip("기준이 될 본. 빌더가 머리 본을 찾아 넣는다. 비면 발밑 + telegraphHeight 를 쓴다.")]
    [SerializeField] private Transform telegraphAnchor;
    [Tooltip("앵커에서 얼마나 띄울지. x=좌우, y=위아래, z=앞뒤. 씬 뷰의 주황 구가 실제 발사 지점이다.")]
    [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0f, 0.5f);
    [Tooltip("씬 뷰에 발사 지점을 표시한다. 위치 맞추고 나면 꺼도 된다.")]
    [SerializeField] private bool showMuzzleGizmo = true;

    // 도약/잠복은 착지 지점을 알려주는 게 맞으므로 바닥 링 전조를 쓴다.
    private HellAttack _leapTele, _burrowTele;

    private BossMotor _motor;
    private VisionSensor _vision;
    private EnemyHealth _health;
    private Transform _target;

    private bool _dead;
    private bool _busy;              // 공격/도약/잠복 중 = 이동 로직 정지
    private bool _roared;            // 발견 포효를 이미 했나
    private readonly List<(int idx, float w)> _cand = new List<(int, float)>();
    private float[] _atkCd;
    private float _leapCd, _burrowCd;
    private int _lastAtk = -1;

    private string _curState;        // 매 프레임 CrossFade 재시작을 막기 위한 현재 상태 기억
    private bool _hitReacting;
    private float _lastHitReact = -99f;
    private int _hitIdx;
    private float _fillTarget = 1f;
    private Vector3 _chargePivot;   // 차지 VFX 내부 중심 보정(프리팹마다 다름)
    private GameObject _outlineFx;  // 착지 예고 테두리(범위 고정 표시)
    private float _actionGap;       // 행동 사이 강제 휴식(초)

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _vision = GetComponent<VisionSensor>();
        _health = GetComponent<EnemyHealth>();

        if (data != null)
        {
            _motor.ApplyData(data);
            _atkCd = new float[data.attacks != null ? data.attacks.Length : 0];

            // 착지 지점에 구슬이 모였다가 터지는 식. 지면 마법진보다 읽기 쉽고 가볍다.
            // 착지 자리에 원이 뜨고 다 차오르는 순간 덮친다.
            _leapTele = new HellAttack
            {
                telegraph = HellTelegraphKind.FillCircle,
                radius = data.leapRadius,
                telegraphTime = data.leapTelegraphTime,
                telegraphScaleMul = data.leapTelegraphScale,
                lockFacing = true,   // 착지점을 이미 정했으니 몸이 따라 돌면 안 된다
            };
            _burrowTele = new HellAttack
            {
                telegraph = HellTelegraphKind.GroundCharge,
                radius = data.burrowRadius,
                telegraphScaleMul = data.leapTelegraphScale,
            };
            if (_vision != null)
            {
                _vision.ApplyVisionParameters(data.visionRange, data.visionAngle);
                _vision.ApplyLostMemory(data.targetLostMemory);
            }
        }
    }

    private void Start()
    {
        _motor.AcquirePlayer();
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
            _health.OnDamage += HandleDamage;
        }
        _motor.Feedback?.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnDamage -= HandleDamage;
        }
    }

    // 맞으면 흠칫 + 살짝 밀림.
    // ★공격/도약/잠복 중에는 끊지 않는다. 전조를 보고 피하는 게임인데 패턴이 씹히면
    //   플레이어가 읽은 정보가 무효가 되고, 몹은 두들기면 아무것도 못 하는 허수아비가 된다.
    private void HandleDamage()
    {
        if (_dead || data == null || !data.useHitReaction) return;
        if (_busy || _hitReacting) return;
        if (Time.time - _lastHitReact < data.hitReactionCooldown) return;
        if (data.hitStates == null || data.hitStates.Length == 0) return;

        _lastHitReact = Time.time;
        StartCoroutine(HitReaction());
    }

    private IEnumerator HitReaction()
    {
        _hitReacting = true;
        _motor.StopMove();

        // 맞은 지점 반대쪽 = 밀려날 방향
        Vector3 push = Vector3.zero;
        if (_health != null && data.knockbackDistance > 0f)
        {
            Vector3 d = transform.position - _health.LastHitPoint;
            d.y = 0f;
            // 히트포인트가 몸 중앙과 겹치면 방향이 안 나오므로 뒤로 민다
            push = (d.sqrMagnitude > 0.01f ? d.normalized : -transform.forward) * data.knockbackDistance;
        }

        string st = data.hitStates[_hitIdx % data.hitStates.Length];
        _hitIdx++;
        Play(st);

        float dur = Mathf.Max(0.05f, data.hitReactionTime);
        float kbT = Mathf.Max(0.01f, data.knockbackTime);
        float t = 0f;
        Vector3 from = transform.position;

        while (t < dur && !_dead)
        {
            t += Time.deltaTime;
            if (push != Vector3.zero && t <= kbT)
            {
                // ★transform 을 직접 옮기면 NavMeshAgent 의 내부 위치와 어긋나 떨린다.
                //   에이전트가 살아 있으면 Move 로 밀어야 한다.
                Vector3 step = push * (Time.deltaTime / kbT);
                if (_motor.AgentReady()) _motor.Agent.Move(step);
                else transform.position = GroundSpot(transform.position + step);
            }
            yield return null;
        }

        _hitReacting = false;
        _curState = null;   // 다음 Play 가 확실히 먹도록 리셋
    }

    // ★StopAllCoroutines 는 코루틴을 yield 지점에서 즉사시키므로 코루틴 안의 정리 코드가 안 돈다.
    //   잠복 중(렌더러 off)이나 도약 중(에이전트 off) 죽는 경우가 있어 여기서 직접 되돌린다.
    private void HandleDeath()
    {
        _dead = true;
        StopAllCoroutines();
        _busy = false;
        _hitReacting = false;
        RestoreBodyState();
        _motor.StopMove();
    }

    // 비활성화 시에도 상태를 되돌려 둔다(풀링이 들어와도 안전하게).
    private void OnDisable()
    {
        StopAllCoroutines();
        _busy = false;
        _hitReacting = false;
        RestoreBodyState();
    }

    // 잠복/도약이 중간에 끊겨도 몸이 사라지거나 공중에 뜬 채 남지 않게 한다.
    private void RestoreBodyState()
    {
        SetRenderers(true);
        if (_motor == null || _motor.Agent == null) return;

        // ★도약 중(공중)에 죽으면 코루틴이 즉사해 착지 스냅이 안 돈다.
        //   그대로 두면 시체가 공중에 뜨고, 에이전트를 켤 때 NavMesh 밖이라 에러가 난다.
        if (!_motor.Agent.enabled)
        {
            transform.position = GroundSpot(transform.position);
            _motor.Agent.enabled = true;
        }
    }

    // 형제 보스 컨트롤러들과 동일. BossMotor 가 Agent.updateRotation 을 꺼놨기 때문에
    // 여기서 돌려주지 않으면 추격 중 몸이 안 돌아 게걸음으로 다가온다.
    // _busy 중에는 억제해서 공격 방향 커밋(= 회피 성립)을 유지한다.
    private void LateUpdate()
    {
        if (_dead || data == null) return;
        _motor.TickFacing(_busy || _hitReacting, _target, data);
    }

    private void Update()
    {
        if (_dead || data == null) return;

        TickCooldowns();
        _target = ResolveTarget();

        if (_target == null)
        {
            _roared = false;
            if (!_busy) { _motor.StopMove(); Play(data.idleState); }
            _motor.TickSpeedParam();
            return;
        }

        if (_busy || _hitReacting) { _motor.TickSpeedParam(); return; }

        // 처음 발견하면 포효 한 번(예고 겸 어그로 표시)
        if (data.roarOnDetect && !_roared)
        {
            _roared = true;
            StartCoroutine(Roar());
            return;
        }

        float dist = _motor.PlanarDistance(_target.position);
        if (TryPickAction(dist)) return;

        // 쓸 패턴이 없으면 붙는다
        _motor.Chase(_target.position);
        Play(data.moveState);
        _motor.TickSpeedParam();
    }

    // 행동 하나가 끝났다. 다음 행동까지 숨 돌릴 틈을 준다.
    private void EndAction()
    {
        _busy = false;
        _actionGap = Random.Range(data.actionGapMin, Mathf.Max(data.actionGapMin, data.actionGapMax));
    }

    // ★같은 상태를 매 프레임 CrossFade 하면 전환이 계속 리셋돼 클립이 앞부분에서 떨린다.
    //   바뀔 때만 넘긴다. Update 에서 Idle/Locomotion 을 조건 없이 부르기 때문에 필수다.
    private void Play(string state)
    {
        if (string.IsNullOrEmpty(state) || _curState == state) return;
        _curState = state;
        _motor.PlayState(state);
    }

    private Transform ResolveTarget()
    {
        Transform t = _vision != null ? _vision.SpottedTarget : _motor.Player;
        if (t == null) return null;

        var ps = _motor.PlayerStat;
        if (ps != null && (ps.IsDead || ps.IsInBase)) return null;
        return t;
    }

    private void TickCooldowns()
    {
        float dt = Time.deltaTime;
        if (_atkCd != null)
            for (int i = 0; i < _atkCd.Length; i++)
                if (_atkCd[i] > 0f) _atkCd[i] -= dt;
        if (_actionGap > 0f) _actionGap -= dt;
        if (_leapCd > 0f) _leapCd -= dt;
        if (_burrowCd > 0f) _burrowCd -= dt;
    }

    // 사거리/쿨 조건을 만족하는 행동을 가중치 랜덤으로 하나 고른다.
    private bool TryPickAction(float dist)
    {
        // ★행동 사이 최소 간격. 이 동안은 추격/대기만 하며, 피격 반응이 나올 수 있는 창이기도 하다.
        if (_actionGap > 0f) return false;

        _cand.Clear();

        if (data.attacks != null)
            for (int i = 0; i < data.attacks.Length; i++)
            {
                var a = data.attacks[i];
                if (a == null || _atkCd[i] > 0f) continue;
                if (dist < a.minRange || dist > a.maxRange) continue;
                float w = Mathf.Max(0.01f, a.weight);
                if (i == _lastAtk) w *= (1f - data.repeatPenalty);
                _cand.Add((i, w));
            }

        // 시그니처는 음수 인덱스로 섞는다(-1 = 도약, -2 = 잠복)
        if (data.useLeap && _leapCd <= 0f && dist >= data.leapMinRange && dist <= data.leapMaxRange)
            _cand.Add((-1, Mathf.Max(0.01f, data.leapWeight)));
        if (data.useBurrow && _burrowCd <= 0f)
            _cand.Add((-2, Mathf.Max(0.01f, data.burrowWeight)));

        if (_cand.Count == 0) return false;

        float total = 0f;
        for (int i = 0; i < _cand.Count; i++) total += _cand[i].w;
        float r = Random.value * total;
        int pick = _cand[_cand.Count - 1].idx;
        for (int i = 0; i < _cand.Count; i++)
        {
            r -= _cand[i].w;
            if (r <= 0f) { pick = _cand[i].idx; break; }
        }

        if (pick == -1) StartCoroutine(LeapAttack());
        else if (pick == -2) StartCoroutine(BurrowAttack());
        else StartCoroutine(PerformAttack(pick));
        return true;
    }

    private IEnumerator Roar()
    {
        _busy = true;
        _motor.StopMove();
        if (_target != null) _motor.FaceInstant(_target.position);
        _motor.Feedback?.PlayDetect();
        Play(data.roarState);
        yield return new WaitForSeconds(data.roarTime);
        _busy = false;
    }

    // ★공격 단일 진입점. 전조를 반드시 먼저 거친다.
    private IEnumerator PerformAttack(int idx)
    {
        var a = data.attacks[idx];
        _busy = true;
        _lastAtk = idx;
        _atkCd[idx] = a.cooldown;

        _motor.StopMove();

        // 1) 전조: 형태는 패턴마다 다르다(원거리=입앞 차지 / 범위근접=바닥링 / 빠른평타=없음)
        yield return Telegraph(a);
        if (_dead) yield break;

        // 2) 공격 모션
        Play(a.state);
        _motor.Feedback?.PlayAttack();
        yield return new WaitForSeconds(Mathf.Max(0f, a.hitTime));
        if (_dead) yield break;

        // 3) 타격 (근접은 판정, 원거리는 발사)
        if (a.kind == HellAttackKind.Ranged) yield return FireVolley(a);
        else ApplyHit(a);
        if (_dead) yield break;

        // 4) 회복
        float rest = Mathf.Max(0f, a.totalTime - a.hitTime);
        yield return new WaitForSeconds(rest);
        EndAction();
    }

    // 입에서 투사체를 쏜다. 연발/부채꼴 지원.
    private IEnumerator FireVolley(HellAttack a)
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning($"[{data.enemyName}] 원거리 패턴인데 projectilePrefab 이 비었다: {a.label}");
            yield break;
        }

        int n = Mathf.Max(1, a.shots);
        for (int i = 0; i < n && !_dead; i++)
        {
            FireOne(a, i, n);
            if (i < n - 1) yield return new WaitForSeconds(Mathf.Max(0.02f, a.shotGap));
        }
    }

    private void FireOne(HellAttack a, int index, int total)
    {
        if (_motor.Player == null) return;

        Vector3 origin = MuzzlePoint();
        Vector3 aim = (_motor.Player.position + Vector3.up) - origin;
        if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;

        // 부채꼴: 가운데 0 기준 좌우 대칭
        if (a.spreadAngle > 0f && total > 1)
        {
            float step = index - (total - 1) * 0.5f;
            aim = Quaternion.AngleAxis(step * a.spreadAngle, Vector3.up) * aim;
        }

        if (data.muzzleVfx != null)
        {
            var m = Instantiate(data.muzzleVfx, origin, Quaternion.LookRotation(aim));
            PrepareTelegraph(m, data.telegraphColor);
            Destroy(m, 2f);
        }

        var go = Instantiate(data.projectilePrefab, origin, Quaternion.LookRotation(aim));
        // 날아가는 탄도 같은 색으로. 안 하면 전조만 빨갛고 탄은 원본 색이라 따로 논다.
        PrepareTelegraph(go, data.telegraphColor);

        var fb = go.GetComponent<WyvernFireball>();
        if (fb == null) { Destroy(go, 5f); return; }

        fb.Launch(aim, data.attackDamage * a.damageMul, _motor.Player);
        // 유도는 가운데 한 발만. 전탄 유도는 근접 전용 플레이어에게 회피 불가다.
        bool isCenter = (total % 2 == 1) && index == total / 2;
        if (a.homing && isCenter) fb.SetHoming(true);
    }

    // 발사/차지 기준점. 에디터 기즈모에서도 부르므로 data 가 없어도 안전해야 한다.
    private Vector3 MuzzlePoint()
    {
        Vector3 basePos = telegraphAnchor != null
            ? telegraphAnchor.position
            : transform.position + Vector3.up * (data != null ? data.telegraphHeight : 1.2f);

        // 오프셋은 모델 기준으로 준다. 몸집(scale)을 바꿔도 입에서 같은 비율로 떨어지게 곱해준다.
        float s = transform.lossyScale.x;

        return basePos
             + transform.forward * (muzzleOffset.z * s)
             + transform.up * (muzzleOffset.y * s)
             + transform.right * (muzzleOffset.x * s);
    }

    // 씬 뷰에서 발사 지점을 눈으로 보면서 muzzleOffset 을 맞출 수 있게 한다.
    private void OnDrawGizmos()
    {
        if (!showMuzzleGizmo) return;

        Vector3 muzzle = MuzzlePoint();

        // 기준 본 위치(노랑) -> 실제 발사 지점(주황) 을 선으로 잇는다
        if (telegraphAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.95f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(telegraphAnchor.position, 0.12f);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
            Gizmos.DrawLine(telegraphAnchor.position, muzzle);
        }

        Gizmos.color = new Color(1f, 0.45f, 0.05f, 1f);
        Gizmos.DrawWireSphere(muzzle, 0.25f);
        // 발사 방향
        Gizmos.DrawLine(muzzle, muzzle + transform.forward * 1.2f);
    }

    // 전조. 패턴이 지정한 형태로 띄운다.
    //  Charge = 입 앞에 모여드는 구슬(원거리) / Ground = 바닥 링(범위 근접) / None = 애니 준비동작만
    //  Charge      = 입 앞에 모이는 구슬(원거리). 입을 따라다닌다.
    //  GroundCharge= 같은 구슬을 지면 목표 지점에 고정(도약 착지 예고 등)
    //  Ground      = 저작된 시전 마법진(범위 근접)
    //  None        = 애니 준비동작만
    //
    // ★스케일로 수렴을 흉내내지 않는다. 이 VFX 는 자체 수렴 연출(Phase_1/2, wide_ring)을 갖고 있고,
    //   루트를 키우면 자식의 로컬 오프셋(z=0.8 등)까지 같이 밀려나서 몸 앞에서 시작해 입으로
    //   빨려오는 이상한 그림이 된다. 크기는 처음부터 고정하고 연출은 에셋에 맡긴다.
    private IEnumerator Telegraph(HellAttack a, Vector3? groundSpot = null)
    {
        float dur = a.telegraphTime > 0f ? a.telegraphTime : data.telegraphTime;
        dur = Mathf.Max(0.05f, dur);

        Color c = a.telegraphColorOverride.a > 0f ? a.telegraphColorOverride : data.telegraphColor;
        GameObject fx = null;
        bool followMuzzle = false;
        bool filling = false;

        if (a.telegraph == HellTelegraphKind.Charge && data.telegraphVfx != null)
        {
            // ★차지가 입에서 안 뜨고 좌우로 왔다갔다 하던 원인 3개를 여기서 전부 잡는다.
            //   (1) Quaternion.identity 로 띄우면 몹이 회전해도 VFX 는 월드 방향 고정이라,
            //       프리팹 안의 z 오프셋(Charge_07 은 자식이 z=0.8)이 몹 기준 좌/우/뒤로 제멋대로 튄다.
            //       -> 몹 회전을 그대로 물려주고 매 프레임 같이 돌린다.
            //   (2) transform 에 자식으로 붙이면 몹 스케일(1.5배)까지 곱해져 크기가 의도와 달라진다.
            //       -> 붙이지 않고 위치/회전만 따라가게 한다.
            //   (3) 프리팹 자체가 원점이 아니라 앞쪽에 치우쳐 있다.
            //       -> 내부 중심을 재서 그만큼 빼준다. 그래야 "보이는 덩어리"가 입에 온다.
            float s = data.telegraphScale * a.telegraphScaleMul;
            fx = Instantiate(data.telegraphVfx, MuzzlePoint(), transform.rotation);
            fx.transform.localScale = Vector3.one * s;
            _chargePivot = LocalCenter(fx) * s;
            fx.transform.position = MuzzlePoint() - transform.rotation * _chargePivot;
            PrepareTelegraph(fx, c);
            followMuzzle = true;
            Destroy(fx, dur + 0.3f);
        }
        else if (a.telegraph == HellTelegraphKind.GroundCharge && data.telegraphVfx != null)
        {
            Vector3 spot = groundSpot ?? GroundSpot(transform.position + transform.forward * 3f);
            fx = Instantiate(data.telegraphVfx, spot + Vector3.up * 0.15f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * data.telegraphScale * a.telegraphScaleMul;
            PrepareTelegraph(fx, c);
            Destroy(fx, dur + 0.3f);
        }
        else if (a.telegraph == HellTelegraphKind.FillCircle && data.fillCircleVfx != null)
        {
            // ★테두리는 처음부터 실제 범위 그대로 떠 있고, 안쪽이 차올라 테두리에 닿는 순간 공격.
            //   범위를 처음부터 알 수 있어야 피할지 말지 판단이 된다.
            float r = a.radius > 0f ? a.radius : data.attackRange;
            Vector3 spot = (groundSpot ?? GroundSpot(transform.position + transform.forward * r))
                         + Vector3.up * 0.06f;
            Color cc = data.fillCircleColor.a > 0f ? data.fillCircleColor : c;

            // 스케일 1 일 때 반경을 재서 원하는 반경에 정확히 맞춘다(= 보이는 원 = 피해 범위)
            var probe = Instantiate(data.fillCircleVfx);
            float unit = MeasureMeshRadius(probe);
            Destroy(probe);
            if (unit <= 0.01f) unit = Mathf.Max(0.05f, data.fillCircleUnitRadius);
            _fillTarget = (r / unit) * a.telegraphScaleMul;

            // 1) 테두리: 전체 범위, 고정. 살짝 어둡게 해서 안쪽 채움이 도드라지게.
            _outlineFx = Instantiate(data.fillCircleVfx, spot, Quaternion.identity);
            _outlineFx.transform.localScale = Vector3.one * _fillTarget;
            TintRenderers(_outlineFx, cc * data.fillOutlineDim);
            Destroy(_outlineFx, dur + data.fillCircleLinger);

            // 2) 채움: 가운데서 시작해 테두리까지 차오른다.
            fx = Instantiate(data.fillCircleVfx, spot, Quaternion.identity);
            fx.transform.localScale = Vector3.one * _fillTarget * data.fillCircleFromScale;
            TintRenderers(fx, cc);
            filling = true;
            Destroy(fx, dur + data.fillCircleLinger);
        }
        else if (a.telegraph == HellTelegraphKind.Ground && data.groundTelegraphVfx != null)
        {
            float r = a.radius > 0f ? a.radius : (a.reach > 0f ? a.reach : data.attackRange);
            Vector3 spot = groundSpot ?? GroundSpot(transform.position + transform.forward * r * 0.5f);
            fx = Instantiate(data.groundTelegraphVfx, spot + Vector3.up * 0.05f, Quaternion.identity);
            float unit = Mathf.Max(0.05f, data.groundTelegraphUnitRadius);
            fx.transform.localScale = Vector3.one * (r / unit) * a.telegraphScaleMul;
            PrepareTelegraph(fx, c);
            Destroy(fx, dur + 0.3f);
        }

        float t = 0f;
        while (t < dur && !_dead)
        {
            t += Time.deltaTime;

            // 입에 물린 것만 따라다닌다. 지면 고정형은 그 자리에 그대로 둬야 예고 역할을 한다.
            // 회전도 같이 따라가야 프리팹 내부 오프셋이 몹 정면 기준으로 유지된다.
            if (followMuzzle && fx != null)
            {
                fx.transform.rotation = transform.rotation;
                fx.transform.position = MuzzlePoint() - transform.rotation * _chargePivot;
            }

            // 원이 차오른다. 끝나는 순간 딱 꽉 찬다.
            if (filling && fx != null)
            {
                float k = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(data.fillCircleFromScale, 1f, k) * _fillTarget;
                fx.transform.localScale = Vector3.one * s;
            }

            // 전조 중 플레이어를 향해 돈다. 단 착지점을 이미 확정한 도약은 고정해야
            // 몸 방향과 착지 원이 따로 노는 그림이 안 나온다.
            if (_target != null && !a.lockFacing) _motor.TickFacing(false, _target, data);
            _motor.TickSpeedParam();
            yield return null;
        }

        if (followMuzzle && fx != null) Destroy(fx);
        if (_outlineFx != null) { Destroy(_outlineFx); _outlineFx = null; }
    }

    private void ApplyHit(HellAttack a)
    {
        var ps = _motor.PlayerStat;
        if (ps == null || ps.IsDead || _motor.Player == null) return;

        float dmg = data.attackDamage * a.damageMul;
        float reach = a.reach > 0f ? a.reach : data.attackRange;

        if (a.radius > 0f)
        {
            // 범위 판정: 내 앞쪽 지점 기준
            Vector3 center = transform.position + transform.forward * reach * 0.5f;
            if (a.impactVfx != null) { var v = Instantiate(a.impactVfx, center, Quaternion.identity); PrepareTelegraph(v, data.telegraphColor); Destroy(v, 4f); }
            Vector3 p = _motor.Player.position; p.y = 0f;
            Vector3 c = center; c.y = 0f;
            if (Vector3.Distance(p, c) <= a.radius) ps.TakeDamage(dmg, transform.position);
        }
        else
        {
            // 부채꼴 근접 판정
            if (a.impactVfx != null)
            {
                Vector3 spot = transform.position + transform.forward * reach * 0.6f + Vector3.up * 0.8f;
                var v = Instantiate(a.impactVfx, spot, Quaternion.identity);
                PrepareTelegraph(v, data.telegraphColor);
                Destroy(v, 4f);
            }
            if (_motor.PlayerInArc(reach, a.halfAngle)) ps.TakeDamage(dmg, transform.position);
        }
    }

    // 도약 돌진: 멀리서 붙는 용도. 전조 -> 점프 -> 착지 강타.
    private IEnumerator LeapAttack()
    {
        _busy = true;
        _leapCd = data.leapCooldown;
        _motor.StopMove();

        // ★착지 지점을 먼저 확정하고 거기에 예고를 띄운다.
        //   예고를 보고 걸어 나오면 헛착지 = 회피 성립. 전조 후에 위치를 다시 읽으면 절대 못 피한다.
        if (_target == null) { _busy = false; yield break; }

        // 플레이어 발밑에 정확히 착지하면 몸이 겹쳐 파묻힌다. 조금 못 미치게 잡는다.
        Vector3 aim = _target.position;
        Vector3 toDir = aim - transform.position; toDir.y = 0f;
        if (toDir.sqrMagnitude > 0.01f)
            aim -= toDir.normalized * Mathf.Min(1.2f, toDir.magnitude * 0.25f);
        Vector3 to = GroundSpot(aim);
        _motor.FaceInstant(to);

        yield return Telegraph(_leapTele, to);
        if (_dead) yield break;

        Vector3 from = transform.position;

        Play(data.leapStartState);
        yield return new WaitForSeconds(0.25f);
        if (_dead) yield break;

        Play(data.leapFlyState);
        float fly = Mathf.Max(0.1f, data.leapFlyTime);
        float t = 0f;
        bool navOff = _motor.AgentReady();
        if (navOff) _motor.Agent.enabled = false;   // 포물선 이동은 에이전트를 잠깐 끈다

        while (t < fly && !_dead)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fly);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * data.leapArcHeight;   // 포물선
            transform.position = p;
            yield return null;
        }

        if (navOff)
        {
            transform.position = GroundSpot(to);
            _motor.Agent.enabled = true;
        }
        if (_dead) yield break;

        Play(data.leapEndState);
        if (data.leapImpactVfx != null)
        {
            var v = Instantiate(data.leapImpactVfx, transform.position, Quaternion.identity);
            PrepareTelegraph(v, data.telegraphColor);
            Destroy(v, 4f);
        }
        var ps = _motor.PlayerStat;
        if (ps != null && !ps.IsDead && _motor.Player != null
            && _motor.PlanarDistance(_motor.Player.position) <= data.leapRadius)
            ps.TakeDamage(data.attackDamage * data.leapDamageMul, transform.position);

        yield return new WaitForSeconds(0.5f);
        EndAction();
    }

    // 잠복: 땅에 숨었다가 플레이어 옆으로 튀어나온다. 헬버그 시그니처.
    private IEnumerator BurrowAttack()
    {
        _busy = true;
        _burrowCd = data.burrowCooldown;
        _motor.StopMove();

        Play(data.burrowInState);
        yield return new WaitForSeconds(0.6f);
        if (_dead) yield break;

        SetRenderers(false);
        yield return new WaitForSeconds(Mathf.Max(0.1f, data.burrowUnderTime));
        if (_dead) { SetRenderers(true); yield break; }

        // 플레이어 근처로 순간이동 후 등장
        if (_target != null)
        {
            Vector3 dir = (transform.position - _target.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            Vector3 spot = _target.position + dir.normalized * data.burrowEmergeDistance;
            WarpTo(GroundSpot(spot));
            _motor.FaceInstant(_target.position);
        }
        SetRenderers(true);

        // 튀어나오는 것 자체가 전조 역할이지만, 규칙대로 전조도 같이 띄운다
        Play(data.burrowOutState);
        yield return Telegraph(_burrowTele);
        if (_dead) yield break;

        if (data.burrowImpactVfx != null)
        {
            var v = Instantiate(data.burrowImpactVfx, transform.position, Quaternion.identity);
            PrepareTelegraph(v, data.telegraphColor);
            Destroy(v, 4f);
        }
        var ps = _motor.PlayerStat;
        if (ps != null && !ps.IsDead && _motor.Player != null
            && _motor.PlanarDistance(_motor.Player.position) <= data.burrowRadius)
            ps.TakeDamage(data.attackDamage * data.burrowDamageMul, transform.position);

        yield return new WaitForSeconds(0.5f);
        EndAction();
    }

    private void SetRenderers(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    private void WarpTo(Vector3 pos)
    {
        if (_motor.AgentReady()) _motor.Agent.Warp(pos);
        else transform.position = pos;
    }

    private Vector3 GroundSpot(Vector3 pos)
        => NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas) ? hit.position : pos;

    // 전조 프리팹 준비. 두 가지를 한 번에 한다.
    // 1) 흰색 머티리얼이라 파티클 색으로 몹별 색을 낸다(화염보스와 같은 방식).
    // 2) ★scalingMode 를 Hierarchy 로 바꾼다. 이 에셋의 자식 파티클은 전부 Local 이라
    //    부모(루트) 스케일을 무시한다 -> 안 바꾸면 아래 스케일 보간이 화면에 전혀 안 나타난다.
    private static void PrepareTelegraph(GameObject go, Color c)
    {
        Color pc = ToParticleColor(c);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(pc);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // ★파티클 색만 바꾸면 일부가 원래 색(보라/파랑)으로 남는다.
        //   이 팩은 셰이더그래프가 자체 HDR 색 프로퍼티(Color_882B4FC 등, 8~32배)를 들고 있어서
        //   그게 최종 색을 지배한다. 재질 쪽 색도 같이 물들여야 전부 통일된다.
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
                TintMaterialColors(m, pc);
    }

    // 재질의 Color 프로퍼티를 색조만 바꿔치기한다. 세기(HDR 배율)와 알파는 그대로 둔다.
    // 세기를 건드리면 이펙트가 뭉개지고, 색조만 바꾸면 원본 연출을 살린 채 톤만 맞출 수 있다.
    private static void TintMaterialColors(Material m, Color hue)
    {
        if (m == null || m.shader == null) return;

        var sh = m.shader;
        int n = sh.GetPropertyCount();
        for (int i = 0; i < n; i++)
        {
            if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;

            int id = sh.GetPropertyNameId(i);
            Color c = m.GetColor(id);

            // 검게 꺼둔 프로퍼티는 건드리지 않는다(켜버리면 없던 발광이 생긴다)
            float intensity = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (intensity <= 0.001f) continue;

            m.SetColor(id, new Color(hue.r * intensity, hue.g * intensity, hue.b * intensity, c.a));
        }
    }

    // ★VFX 프리팹의 "보이는 덩어리"가 원점에서 얼마나 치우쳐 있는지 잰다.
    //   Charge_07 처럼 자식이 z=0.8 에 있으면 원점에 맞춰 띄워도 눈에는 앞쪽에 뜬다.
    //   방출량이 많은 파티클일수록 시각적 비중이 크므로 그걸 가중치로 쓴다.
    //   헬 5형제가 어떤 Charge 번호를 쓰든 이 계산이 알아서 보정한다.
    private static Vector3 LocalCenter(GameObject go)
    {
        Vector3 sum = Vector3.zero;
        float total = 0f;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var em = ps.emission;
            if (!em.enabled) continue;

            // 방출률 + 버스트를 대충 합쳐 비중으로 삼는다
            float w = em.rateOverTime.constantMax;
            for (int i = 0; i < em.burstCount; i++) w += em.GetBurst(i).count.constantMax;
            if (w <= 0f) w = 1f;

            // 루트 기준 로컬 좌표(루트 스케일은 빼고 순수 오프셋만)
            Vector3 local = go.transform.InverseTransformPoint(ps.transform.position);
            sum += local * w;
            total += w;
        }

        return total > 0f ? sum / total : Vector3.zero;
    }

    // 메시 기반 지시자 색칠. 파티클이 아니라 MeshRenderer 라 startColor 가 아니라 재질을 건드린다.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintId = Shader.PropertyToID("_TintColor");

    private static void TintRenderers(GameObject go, Color c)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
            {
                if (m == null) continue;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
                if (m.HasProperty(TintId)) m.SetColor(TintId, c);
            }
        // 파티클도 섞여 있으면 같이 물들인다
        PrepareTelegraph(go, c);
    }

    // 스케일 1 일 때 이 오브젝트가 바닥에 그리는 반경(m).
    // ★파티클 shape 반경으로 재면 안 된다. 그건 "입자를 어디서 뿜는가"지 "화면에 보이는 크기"가 아니다.
    //   실제로 그렇게 쟀다가 원이 3배 넘게 커졌다. 메시 bounds 는 스폰 직후에도 유효하고 정확하다.
    private static float MeasureMeshRadius(GameObject go)
    {
        float max = 0f;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;

            // 눕혀둔 경우 로컬 축이 바뀌므로, 월드로 바꿔서 바닥(XZ) 반경을 잰다.
            Vector3 e = m.bounds.extents;
            Matrix4x4 toRoot = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Vector3 ex = toRoot.MultiplyVector(new Vector3(e.x, 0f, 0f));
            Vector3 ey = toRoot.MultiplyVector(new Vector3(0f, e.y, 0f));
            Vector3 ez = toRoot.MultiplyVector(new Vector3(0f, 0f, e.z));

            float r = 0f;
            foreach (var v in new[] { ex, ey, ez })
                r = Mathf.Max(r, new Vector2(v.x, v.z).magnitude);

            if (r > max) max = r;
        }
        return max;
    }

    // ★파티클 색은 8비트 정점 스트림이라 1을 넘으면 잘린다.
    //   (8, 1.1, 0.1) 같은 HDR 값을 그대로 넣으면 (1, 1, 0.1) = 노랑이 돼버린다.
    //   빨강을 넣었는데 노랗게 나오던 원인. 최대 성분으로 나눠서 색조를 보존한 채 0~1 로 내린다.
    private static Color ToParticleColor(Color c)
    {
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (m > 1f) return new Color(c.r / m, c.g / m, c.b / m, c.a);
        return c;
    }
}
