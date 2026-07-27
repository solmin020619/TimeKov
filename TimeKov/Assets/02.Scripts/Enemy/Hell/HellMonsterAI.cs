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
    private HellAttack _leapTele;

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
    private Vector3 _fillPivot;
    private Vector3 _fillSpot;
    private GameObject _leapFill;
    private GameObject _outlineFx;  // 착지 예고 테두리(범위 고정 표시)
    private Coroutine _actionCo;
    private bool _interruptible;   // 준비동작 중 = 끊어도 되는 구간
    private float _actionGap;       // 행동 사이 강제 휴식(초)
    private Coroutine _wanderCo;    // 평상시 어슬렁
    private Vector3 _homePos;       // 스폰 지점. 어슬렁 중심이자 복귀 지점
    private EnemyWorldUI _worldUI;  // 머리 위 체력바. 잠복 때 같이 숨겨야 한다
    private float _combatStopDistance;   // 전투용 정지거리. 순찰 중엔 0 으로 낮췄다가 되돌린다

    private void Awake()
    {
        _motor = new BossMotor(this, speedParam);
        _vision = GetComponent<VisionSensor>();
        _health = GetComponent<EnemyHealth>();

        if (data != null)
        {
            _motor.ApplyData(data);
            // ★ApplyData 가 Agent.stoppingDistance 를 attackRange x attackApproachRatio 로 박는다(3.9x0.9=3.51m).
            //   전투에선 맞는 값이지만 순찰에는 치명적이라 여기서 기억해뒀다가 순찰 중엔 0 으로 낮춘다.
            _combatStopDistance = _motor.Agent != null ? _motor.Agent.stoppingDistance : 0f;
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
            // 잠복은 별도 전조를 안 쓴다. 땅에서 솟구치는 것 자체가 예고이자 공격이다.
            if (_vision != null)
            {
                _vision.ApplyVisionParameters(data.visionRange, data.visionAngle);
                _vision.ApplyLostMemory(data.targetLostMemory);
            }
        }
    }

    private void Start()
    {
        // ★어슬렁 중심. 스포너가 Warp 로 자리를 잡은 뒤여야 하므로 Awake 가 아니라 Start 에서 잡는다.
        _homePos = transform.position;

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
        BattleMusicTracker.Disengage(GetInstanceID());   // 파괴 시 BGM 카운트 누수 방지
    }

    // 맞으면 흠칫 + 살짝 밀림.
    // ★공격/도약/잠복 중에는 끊지 않는다. 전조를 보고 피하는 게임인데 패턴이 씹히면
    //   플레이어가 읽은 정보가 무효가 되고, 몹은 두들기면 아무것도 못 하는 허수아비가 된다.
    private void HandleDamage()
    {
        if (_dead || data == null) return;

        // ★맞았으면 무조건 그쪽을 인지한다. 시야각(300도) 밖에서 맞으면 SpottedTarget 이 안 잡혀서
        //   흠칫만 하고 태연히 순찰을 계속 가는 그림이 나온다. 재원의 FieldMonsterAI 도 여기서 같은 처리를 한다.
        //   피격 반응을 안 쓰는 몹도 어그로는 끌려야 하므로 useHitReaction 검사보다 위에 둔다.
        if (_vision != null && _motor.Player != null) _vision.ForceSetTarget(_motor.Player);

        if (!data.useHitReaction) return;
        if (_hitReacting) return;
        if (Time.time - _lastHitReact < data.hitReactionCooldown) return;
        if (data.hitStates == null || data.hitStates.Length == 0) return;

        // 어슬렁거리다 맞았으면 즉시 멈춘다. 안 그러면 흠칫하는 동안에도 목적지로 계속 걸어간다.
        StopWander();

        if (_busy)
        {
            // ★준비동작(전조) 중에만 끊는다.
            //   전조는 길어서 대부분의 피격이 여기 걸리므로 흠칫이 자주 보인다.
            //   반대로 이미 나간 공격이나 공중에 뜬 도약을 끊으면
            //   공중에 멈춘 몹 같은 이상한 그림이 나오고, 읽고 피한 플레이어만 손해다.
            if (!data.hitCanInterruptAttack || !_interruptible) return;
            InterruptAction();
        }

        StartCoroutine(HitReaction());
    }

    // 준비동작을 끊고 원래대로 되돌린다.
    private void InterruptAction()
    {
        if (_actionCo != null) { StopCoroutine(_actionCo); _actionCo = null; }
        _interruptible = false;
        _busy = false;
        ClearLeapCircle();
        // 코루틴을 중간에 죽이면 그 안의 정리 코드가 안 돈다. 몸 상태는 여기서 되돌린다.
        RestoreBodyState();
        _motor.StopMove();
        // 끊긴 직후 바로 다음 공격이 나가면 흠칫이 무의미해진다. 다른 행동과 같은 텀을 준다.
        _actionGap = Random.Range(data.actionGapMin, Mathf.Max(data.actionGapMin, data.actionGapMax));
    }

    private IEnumerator HitReaction()
    {
        _hitReacting = true;
        _motor.StopMove();

        // 1) 흠칫 모션
        string st = data.hitStates[_hitIdx % data.hitStates.Length];
        _hitIdx++;
        Play(st);
        yield return WaitBusy(data.hitReactionTime);

        // 2) ★회복 한 박자. 이게 없으면 흠칫 모션이 끝나기도 전에 다음 행동이 시작돼
        //    전조가 피격 모션 위로 덮어써진다("맞는 도중에 전조가 뜬다").
        //    전투 자세로 잠깐 서서 맞은 걸 추스르는 그림을 만든다.
        if (!_dead && data.hitRecoveryTime > 0f)
        {
            Play(WindupState());
            yield return WaitBusy(data.hitRecoveryTime);
        }

        // ★경직 쿨은 "끝난 시점"부터 잰다.
        //   시작 시점부터 재면 흠칫이 길어질수록 쿨을 스스로 잡아먹어서
        //   맞는 족족 흠칫만 반복하는 허수아비가 된다.
        _lastHitReact = Time.time;
        _hitReacting = false;
        _curState = null;   // 다음 Play 가 확실히 먹도록 리셋
    }

    // 경직 중에도 Speed 파라미터는 갱신해야 블렌드트리가 안 튄다.
    private IEnumerator WaitBusy(float dur)
    {
        float t = 0f;
        float end = Mathf.Max(0.05f, dur);
        while (t < end && !_dead)
        {
            t += Time.deltaTime;
            _motor.TickSpeedParam();
            yield return null;
        }
    }

    // ★StopAllCoroutines 는 코루틴을 yield 지점에서 즉사시키므로 코루틴 안의 정리 코드가 안 돈다.
    //   잠복 중(렌더러 off)이나 도약 중(에이전트 off) 죽는 경우가 있어 여기서 직접 되돌린다.
    private void HandleDeath()
    {
        _dead = true;
        BattleMusicTracker.Disengage(GetInstanceID());   // 처치 → 교전 종료(Update 가 더는 안 도므로 여기서)
        StopAllCoroutines();   // ★코루틴 핸들이 남아 있으면 죽은 뒤에도 살아있는 걸로 오해한다
        _actionCo = null;
        _wanderCo = null;
        _busy = false;
        _hitReacting = false;
        RestoreBodyState();
        ClearLeapCircle();
        _motor.StopMove();
    }

    // 비활성화 시에도 상태를 되돌려 둔다(풀링이 들어와도 안전하게).
    private void OnDisable()
    {
        StopAllCoroutines();
        _actionCo = null;
        _wanderCo = null;
        _busy = false;
        _hitReacting = false;
        RestoreBodyState();
        ClearLeapCircle();
        BattleMusicTracker.Disengage(GetInstanceID());   // 비활성/풀링 시 교전 종료
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
            BattleMusicTracker.Disengage(GetInstanceID());   // 타깃 상실 → 교전 종료
            if (!_busy && !_hitReacting)
            {
                // ★가만히 서 있으면 죽어 있는 것처럼 보인다.
                //   기존 일반몹은 BT 가 순찰해주지만 이 컨트롤러는 직접 해야 한다.
                if (data.wander) StartWander();
                else { _motor.StopMove(); Play(data.idleState); }
            }
            _motor.TickSpeedParam();
            return;
        }

        // 플레이어를 봤다. 어슬렁은 즉시 중단하고 전투 속도로 되돌린다.
        StopWander();
        if (data != null) BattleMusicTracker.Engage(GetInstanceID(), data.region);   // 교전 시작 → 맵 테마 BGM

        if (_busy || _hitReacting) { _motor.TickSpeedParam(); return; }

        // 처음 발견하면 포효 한 번(예고 겸 어그로 표시)
        if (data.roarOnDetect && !_roared)
        {
            _roared = true;
            _actionCo = StartCoroutine(Roar());
            return;
        }

        float dist = _motor.PlanarDistance(_target.position);
        if (TryPickAction(dist)) return;

        // ★이미 충분히 가까우면 더 파고들지 않고 전투 자세로 선다.
        //   안 그러면 행동 사이 휴식 내내 플레이어 몸에 겹치도록 밀고 들어와서
        //   "쉬는 틈"이 그림으로 안 보이고 카메라도 몹에 파묻힌다.
        if (data.standoffDistance > 0f && dist <= data.standoffDistance)
        {
            _motor.StopMove();
            Play(WindupState());
            _motor.TickSpeedParam();
            return;
        }

        // 쓸 패턴이 없으면 붙는다
        _motor.Chase(_target.position);
        Play(data.moveState);
        _motor.TickSpeedParam();
    }

    // 준비동작/대기용 전투 자세. 비어 있으면 Idle 로 떨어진다.
    private string WindupState()
        => string.IsNullOrEmpty(data.windupState) ? data.idleState : data.windupState;

    // ── 평상시 어슬렁 ──
    // 기존 일반몹 19종은 EnemyBrain(BT) 이라 EnemySpawnPoint 가 웨이포인트를 주입해준다.
    // 헬 몹은 보스와 같은 전용 컨트롤러라 그 경로를 안 탄다(EnemySpawnPoint.cs 의 주석 참고).
    // 그래서 웨이포인트 없이 스폰 지점 기준으로 스스로 돈다. 씬 세팅이 필요 없는 게 장점.
    private void StartWander()
    {
        if (_wanderCo == null) _wanderCo = StartCoroutine(WanderRoutine());
    }

    // 매 프레임 호출되므로 돌고 있을 때만 일한다.
    private void StopWander()
    {
        if (_wanderCo == null) return;
        StopCoroutine(_wanderCo);
        _wanderCo = null;
        _motor.StopMove();
        RestoreMoveSpeed();
    }

    // 어슬렁은 느린 속도 + 정지거리 0 으로 걷는다. 끝나면 반드시 전투용 값으로 되돌린다.
    //
    // ★★정지거리를 안 낮추면 순찰이 통째로 망가진다.
    //   BossMotor.ApplyData 가 stoppingDistance 를 attackRange x attackApproachRatio(=3.51m) 로 잡는데,
    //   NavMeshAgent 는 목적지 3.51m 앞에서 감속해 멈추고 remainingDistance 도 거기서 수렴한다.
    //   그래서 "도착했나(remainingDistance <= 0.4)" 가 영원히 참이 안 되고 매번 타임아웃까지 서 있게 된다.
    //   심지어 뽑은 목적지가 3.51m 안쪽이면 아예 한 발짝도 안 움직인다.
    //   재원의 FieldMonsterAI 와 자폭거미는 정지거리가 0 이라 같은 코드가 멀쩡히 돌았다. 헬만 걸린 함정.
    private void BeginWanderMove()
    {
        if (_motor.Agent == null) return;
        _motor.Agent.speed = data.moveSpeed * data.wanderSpeedMul;
        _motor.Agent.stoppingDistance = 0f;
    }

    private void RestoreMoveSpeed()
    {
        if (_motor == null || _motor.Agent == null) return;
        _motor.Agent.speed = data.moveSpeed;
        _motor.Agent.stoppingDistance = _combatStopDistance;
    }

    private IEnumerator WanderRoutine()
    {
        while (!_dead && _target == null)
        {
            // 1) 갈 수 있는 곳을 찾는다.
            //    ★한 번 실패하면 그대로 쉬어버리면 "가끔 한 번씩만 움직이는" 그림이 된다.
            //      실패는 조용히 여러 번 다시 뽑는다(비용은 SamplePosition 몇 번뿐).
            bool moving = false;
            for (int tries = 0; tries < 6 && !moving; tries++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 want = _homePos + new Vector3(dir.x, 0f, dir.y)
                                        * Random.Range(data.wanderRadius * 0.35f, data.wanderRadius);

                if (!NavMesh.SamplePosition(want, out var hit, 3f, NavMesh.AllAreas)) continue;
                if (!_motor.AgentReady()) break;

                BeginWanderMove();
                _motor.Chase(hit.position);
                moving = true;

                // ★경로가 안 이어지는 지점이면(섬처럼 끊긴 NavMesh) 즉시 다른 곳을 고른다.
                //   예전엔 이 경우 8초 타임아웃을 그대로 기다려서, 몹이 한참 서 있다가
                //   가끔 한 번 움직이는 것처럼 보였다. "10초에 한 번" 의 진범.
                while (_motor.Agent.pathPending) yield return null;
                if (_motor.Agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    _motor.Agent.ResetPath();
                    moving = false;
                    continue;
                }

                Play(data.moveState);
                float giveUp = Time.time + 6f;
                while (!_dead && _target == null && Time.time < giveUp)
                {
                    if (_motor.Agent.remainingDistance <= 0.5f) break;
                    _motor.TickSpeedParam();
                    yield return null;
                }
            }
            if (_dead || _target != null) break;

            // 2) 도착했으면 잠깐 쉰다. 기존 BT 몹들이 활발한 만큼 텀은 짧게.
            _motor.StopMove();
            RestoreMoveSpeed();
            Play(data.idleState);

            // ★대기 시간은 반드시 0 보다 크게 클램프한다.
            //   0 이 들어오고 목적지 뽑기까지 실패하면 이 while 이 한 프레임 안에서 무한히 돌아
            //   게임이 통째로 멈춘다(yield 가 이 안에만 있다).
            float pause = Mathf.Max(0.1f, Random.Range(data.wanderPauseRange.x,
                                    Mathf.Max(data.wanderPauseRange.x, data.wanderPauseRange.y)));
            float end = Time.time + pause;
            while (!_dead && _target == null && Time.time < end)
            {
                _motor.TickSpeedParam();
                yield return null;
            }

            // 위 루프가 어떤 이유로든 한 번도 안 돌았을 때를 대비한 안전장치.
            yield return null;
        }

        RestoreMoveSpeed();
        _wanderCo = null;
    }

    // 행동 하나가 끝났다. 다음 행동까지 숨 돌릴 틈을 준다.
    private void EndAction()
    {
        _busy = false;
        _interruptible = false;
        _actionCo = null;
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

        // ★반환값을 반드시 _actionCo 에 담는다.
        //   안 담으면 InterruptAction 의 StopCoroutine 이 null 을 보고 그냥 지나가서,
        //   "준비동작 중 피격이면 공격이 끊긴다"가 통째로 죽는다. 그것만으로 끝이 아니라
        //   _busy 만 풀린 채 코루틴은 살아 있어서 흠칫이 끝나는 순간 두 번째 행동이 겹쳐 돈다.
        if (pick == -1) _actionCo = StartCoroutine(LeapAttack());
        else if (pick == -2) _actionCo = StartCoroutine(BurrowAttack());
        else _actionCo = StartCoroutine(PerformAttack(pick));
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
        // 포효 직후 바로 공격이 나가면 포효가 전조 없는 기습처럼 보인다. 다른 행동과 같은 텀을 준다.
        EndAction();
    }

    // ★공격 단일 진입점. 전조를 반드시 먼저 거친다.
    private IEnumerator PerformAttack(int idx)
    {
        var a = data.attacks[idx];
        _busy = true;
        _lastAtk = idx;
        _atkCd[idx] = a.cooldown;

        _motor.StopMove();

        // ★분출류: 전조가 뜨는 순간의 플레이어 발밑으로 자리를 못박는다.
        //   여기서 한 번만 읽으므로, 표시를 보고 걸어 나오면 헛방이 된다(= 회피가 성립).
        //   나중에 다시 읽으면 절대 못 피하는 공격이 된다.
        Vector3? lockedSpot = null;
        if (a.groundTargetsPlayer && _motor.Player != null)
            lockedSpot = GroundSpot(_motor.Player.position);

        // 1) 전조: 형태는 패턴마다 다르다(원거리=입앞 차지 / 범위근접=바닥링 / 빠른평타=없음)
        // ★애니메이터도 같이 준비 자세로 넘긴다.
        //   여기서 상태를 안 바꾸면 전조 1초 내내 직전 상태에 굳어 있다.
        //   걷다가 들어오면 제자리걸음, 맞고 들어오면 흠칫한 자세 그대로 불을 모으는 그림이 된다.
        //   (컨트롤러에 전이가 하나도 없고 전부 CrossFade 로만 움직이는 구조라 스스로 안 빠져나온다)
        // 패턴 전용 준비 자세가 있으면 그걸 쓴다(시전처럼 자세를 먼저 잡아야 하는 공격).
        Play(string.IsNullOrEmpty(a.windupState) ? WindupState() : a.windupState);
        _interruptible = true;
        yield return Telegraph(a, lockedSpot);
        _interruptible = false;
        if (_dead) yield break;

        // 2) 공격 모션
        Play(a.state);
        _motor.Feedback?.PlayAttack();
        yield return new WaitForSeconds(Mathf.Max(0f, a.hitTime));
        if (_dead) yield break;

        // 3) 타격 (근접은 판정, 원거리는 발사)
        if (a.kind == HellAttackKind.Ranged) yield return FireVolley(a);
        else ApplyHit(a, lockedSpot);
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
            TintVfx(m);
            Destroy(m, 2f);
        }

        var go = Instantiate(data.projectilePrefab, origin, Quaternion.LookRotation(aim));
        var fb = go.GetComponent<WyvernFireball>();
        if (fb == null) { TintVfx(go); Destroy(go, 5f); return; }

        // 탄과 착탄 VFX 를 한 번에 물들인다(착탄은 나중에 생기므로 여기서 예약해둬야 한다)
        if (data.tintVfx) fb.SetTint(data.telegraphColor);

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
            TintVfx(fx);
            followMuzzle = true;
            Destroy(fx, dur + 0.3f);
        }
        else if (a.telegraph == HellTelegraphKind.GroundCharge && data.telegraphVfx != null)
        {
            Vector3 spot = groundSpot ?? GroundSpot(transform.position + transform.forward * 3f);
            fx = Instantiate(data.telegraphVfx, spot + Vector3.up * 0.15f, Quaternion.identity);
            fx.transform.localScale = Vector3.one * data.telegraphScale * a.telegraphScaleMul;
            TintVfx(fx);
            Destroy(fx, dur + 0.3f);
        }
        else if (a.telegraph == HellTelegraphKind.FillCircle && data.fillCircleVfx != null)
        {
            // ★테두리는 처음부터 실제 범위 그대로 떠 있고, 안쪽이 차올라 테두리에 닿는 순간 공격.
            //   범위를 처음부터 알 수 있어야 피할지 말지 판단이 된다.
            float r = a.radius > 0f ? a.radius : data.attackRange;
            Vector3 spot = (groundSpot ?? GroundSpot(AttackCenter(a))) + Vector3.up * 0.06f;
            Color cc = data.fillCircleColor.a > 0f ? data.fillCircleColor : c;

            // ★파티클 VFX 로 그린다. 링 메시를 쓰려 했으나 그 머티리얼들은 파티클 전용이라
            //   (정점 색을 받아야 보이고 _CoreColor 알파가 0) 맨 메시에 붙이면 투명하게 나온다.
            //   입에서 모이는 차지는 이미 잘 보이는 게 확인됐으니 같은 걸 바닥에 쓴다.
            _fillTarget = r / Mathf.Max(0.05f, data.fillCircleUnitRadius) * a.telegraphScaleMul;

            // 1) 테두리: 전체 범위를 처음부터 보여준다. 어둡게 깔아서 채움이 도드라지게.
            _outlineFx = Instantiate(data.fillCircleVfx, spot, Quaternion.identity);
            _outlineFx.transform.localScale = Vector3.one * _fillTarget;
            _outlineFx.transform.position = spot - _outlineFx.transform.rotation * (LocalCenter(_outlineFx) * _fillTarget);
            PrepareTelegraph(_outlineFx, cc * data.fillOutlineDim);
            Destroy(_outlineFx, dur + data.fillCircleLinger);

            // 2) 채움: 가운데서 시작해 테두리까지 차오른다. 꽉 차는 순간 = 덮치는 순간.
            fx = Instantiate(data.fillCircleVfx, spot, Quaternion.identity);
            float s0 = _fillTarget * data.fillCircleFromScale;
            fx.transform.localScale = Vector3.one * s0;
            _fillPivot = LocalCenter(fx);
            fx.transform.position = spot - fx.transform.rotation * (_fillPivot * s0);
            PrepareTelegraph(fx, cc);
            filling = true;
            _fillSpot = spot;
            Destroy(fx, dur + data.fillCircleLinger);
        }
        else if (a.telegraph == HellTelegraphKind.Ground && data.groundTelegraphVfx != null)
        {
            float r = a.radius > 0f ? a.radius : (a.reach > 0f ? a.reach : data.attackRange);
            Vector3 spot = groundSpot ?? GroundSpot(AttackCenter(a));
            fx = Instantiate(data.groundTelegraphVfx, spot + Vector3.up * 0.05f, Quaternion.identity);
            float unit = Mathf.Max(0.05f, data.groundTelegraphUnitRadius);
            float gs = (r / unit) * a.telegraphScaleMul;
            fx.transform.localScale = Vector3.one * gs;
            // 프리팹 내부 중심이 원점에서 벗어나 있으면 원이 옆으로 밀린다. 도약 원과 같은 보정.
            fx.transform.position = (spot + Vector3.up * 0.05f) - fx.transform.rotation * (LocalCenter(fx) * gs);
            // ★바닥 원은 도약 착지 예고와 같은 빨강으로 묶는다. 색이 곧 "여기 맞는다" 신호가 된다.
            PrepareTelegraph(fx, data.fillCircleColor.a > 0f ? data.fillCircleColor : c);
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
                // 커지면서 내부 오프셋도 같이 커지므로 매번 다시 중심을 맞춘다
                fx.transform.position = _fillSpot - fx.transform.rotation * (_fillPivot * s);
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

    // ★범위 공격의 중심점. 전조 원을 그리는 쪽과 피해를 주는 쪽이 반드시 이 하나를 같이 써야 한다.
    //   따로 계산하던 시절엔 원은 앞쪽 2.6m, 판정은 1.7m 라 "원 밖인데 맞았다"가 나왔다.
    private Vector3 AttackCenter(HellAttack a)
    {
        float reach = a.reach > 0f ? a.reach : data.attackRange;
        return transform.position + transform.forward * reach * 0.5f;
    }

    private void ApplyHit(HellAttack a, Vector3? lockedSpot = null)
    {
        var ps = _motor.PlayerStat;
        if (ps == null || ps.IsDead || _motor.Player == null) return;

        float dmg = data.attackDamage * a.damageMul;
        float reach = a.reach > 0f ? a.reach : data.attackRange;

        if (a.radius > 0f)
        {
            // 범위 판정: 내 앞쪽 지점(또는 분출류면 못박아둔 플레이어 발밑).
            // ★전조 원과 반드시 같은 값을 쓴다.
            Vector3 center = lockedSpot ?? AttackCenter(a);
            // ★여기엔 높이 보정이 없어서 착탄 VFX 가 발밑(지면)에 그대로 생겼다.
            //   대부분의 착탄 VFX 는 원점이 덩어리 한가운데라 절반이 땅에 묻힌다.
            //   덩치 큰 몹(헬사이클롭)일수록 VFX 도 커서 더 심하게 박힌다.
            SpawnImpact(a, center + Vector3.up * a.impactLift);
            Vector3 p = _motor.Player.position; p.y = 0f;
            Vector3 c = center; c.y = 0f;
            if (Vector3.Distance(p, c) <= a.radius) ps.TakeDamage(dmg, transform.position);
        }
        else
        {
            // 부채꼴 근접 판정
            SpawnImpact(a, transform.position + transform.forward * reach * 0.6f
                          + Vector3.up * Mathf.Max(0.8f, a.impactLift));
            if (_motor.PlayerInArc(reach, a.halfAngle)) ps.TakeDamage(dmg, transform.position);
        }
    }

    // 착탄 VFX. 크기/높이를 패턴별로 조절할 수 있어야 한다.
    // 이 VFX 팩은 공중 착탄 기준으로 만들어져서 지면 강타에 그대로 쓰면 크고 묻힌다.
    private void SpawnImpact(HellAttack a, Vector3 pos)
    {
        if (a.impactVfx == null) return;
        var v = Instantiate(a.impactVfx, pos, Quaternion.identity);
        if (a.impactScale > 0f && !Mathf.Approximately(a.impactScale, 1f))
            v.transform.localScale *= a.impactScale;
        TintVfx(v);
        Destroy(v, 4f);
    }

    // 착지 예고 원. 테두리(범위 고정) + 채움(차오름) 2겹.
    // 보이는 반경 = data.leapRadius = 실제 피해 반경. 셋이 같은 값에서 나온다.
    private void SpawnLeapCircle(Vector3 spot)
    {
        ClearLeapCircle();
        if (data.fillCircleVfx == null) return;

        _fillSpot = spot + Vector3.up * 0.06f;
        _fillTarget = data.leapRadius / Mathf.Max(0.05f, data.fillCircleUnitRadius);
        Color c = data.fillCircleColor.a > 0f ? data.fillCircleColor : data.telegraphColor;

        _outlineFx = Instantiate(data.fillCircleVfx, _fillSpot, Quaternion.identity);
        _outlineFx.transform.localScale = Vector3.one * _fillTarget;
        PrepareTelegraph(_outlineFx, c * data.fillOutlineDim);

        _leapFill = Instantiate(data.fillCircleVfx, _fillSpot, Quaternion.identity);
        _fillPivot = LocalCenter(_leapFill);
        PrepareTelegraph(_leapFill, c);
        TickLeapCircle(0f);
    }

    // k = 0~1. 1 이 되는 순간이 착지이자 피해 판정 시점이다.
    private void TickLeapCircle(float k)
    {
        if (_leapFill == null) return;
        float s = Mathf.Lerp(data.fillCircleFromScale, 1f, Mathf.Clamp01(k)) * _fillTarget;
        _leapFill.transform.localScale = Vector3.one * s;
        _leapFill.transform.position = _fillSpot - _leapFill.transform.rotation * (_fillPivot * s);
    }

    private void ClearLeapCircle()
    {
        if (_leapFill != null) { Destroy(_leapFill, data.fillCircleLinger); _leapFill = null; }
        if (_outlineFx != null) { Destroy(_outlineFx, data.fillCircleLinger); _outlineFx = null; }
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

        // ★원이 "차오르는 게이지" 역할을 한다. 전조 + 도약준비 + 체공 전체에 걸쳐 채워서
        //   ★꽉 차는 순간 = 착지 = 피해 판정 순간. 셋이 정확히 같은 시점이다.
        //   그래서 원 안에 있으면 맞고 밖이면 안 맞는 게 눈으로 보장된다.
        float tele = Mathf.Max(0.05f, data.leapTelegraphTime);
        // ★도약 준비 동작은 클립 길이 그대로 쓴다(빌더 실측).
        //   예전엔 0.25초 상수라, 웅크리는 모션이 반도 안 나오고 튀어나갔다.
        float startT = Mathf.Max(0.05f, data.leapStartTime);
        float fly = Mathf.Max(0.1f, data.leapFlyTime);
        float total = tele + startT + fly;

        SpawnLeapCircle(to);
        float elapsed = 0f;
        _interruptible = true;   // 준비 구간만 끊을 수 있다

        // 전조 동안은 전투 자세로 노려본다. 여기서 상태를 안 잡으면 걷던 자세로 원만 차오른다.
        Play(WindupState());

        // 1) 전조: 제자리에서 원이 차오르기 시작
        while (elapsed < tele && !_dead)
        {
            elapsed += Time.deltaTime;
            TickLeapCircle(elapsed / total);
            _motor.TickSpeedParam();
            yield return null;
        }
        _interruptible = false;   // 여기부터는 커밋. 공중에서 멈추면 안 된다
        if (_dead) yield break;

        Vector3 from = transform.position;

        // 2) 도약 준비 동작
        Play(data.leapStartState);
        while (elapsed < tele + startT && !_dead)
        {
            elapsed += Time.deltaTime;
            TickLeapCircle(elapsed / total);
            yield return null;
        }
        if (_dead) yield break;

        // 3) 체공. 착지 시점에 원이 100% 가 된다.
        Play(data.leapFlyState);
        float t = 0f;
        bool navOff = _motor.AgentReady();
        if (navOff) _motor.Agent.enabled = false;   // 포물선 이동은 에이전트를 잠깐 끈다

        while (t < fly && !_dead)
        {
            t += Time.deltaTime;
            elapsed += Time.deltaTime;
            TickLeapCircle(elapsed / total);

            float k = Mathf.Clamp01(t / fly);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * data.leapArcHeight;   // 포물선
            transform.position = p;
            yield return null;
        }
        TickLeapCircle(1f);

        if (navOff)
        {
            // ★부유 몹은 baseOffset 만큼 떠 있어야 한다. 지면에 그대로 내려놓으면
            //   에이전트를 켜는 순간 한 프레임 위로 튄다. 지상 몹은 baseOffset 0 이라 영향 없다.
            transform.position = GroundSpot(to) + Vector3.up * _motor.Agent.baseOffset;
            _motor.Agent.enabled = true;
        }
        if (_dead) yield break;

        Play(data.leapEndState);
        _motor.Feedback?.PlayAttack();   // 착지 강타 — 기존엔 VFX만 있고 소리가 없었음
        if (data.leapImpactVfx != null)
        {
            // ★도약은 착지 원이 빨강이므로 폭발도 빨강으로 묶는다.
            //   원은 빨강인데 터질 때만 보라면 같은 공격으로 안 읽힌다.
            //   원거리(보라)와 도약(빨강)이 색으로 구분되는 게 오히려 읽기 쉽다.
            var v = Instantiate(data.leapImpactVfx, transform.position, Quaternion.identity);
            PrepareTelegraph(v, data.fillCircleColor.a > 0f ? data.fillCircleColor : data.telegraphColor);
            Destroy(v, 4f);
        }
        // ★판정 중심 = 원 중심(착지 지점). 몹 위치로 재면 착지가 살짝 밀렸을 때 원과 어긋난다.
        var ps = _motor.PlayerStat;
        if (ps != null && !ps.IsDead && _motor.Player != null)
        {
            Vector3 pp = _motor.Player.position; pp.y = 0f;
            Vector3 cc2 = to; cc2.y = 0f;
            if (Vector3.Distance(pp, cc2) <= data.leapRadius)
                ps.TakeDamage(data.attackDamage * data.leapDamageMul, transform.position);
        }

        ClearLeapCircle();

        // ★착지 마무리 동작이 끝날 때까지 기다린다(빌더가 JumpEnd 클립 길이를 재서 넣는다).
        //   상수 0.5초였을 땐 일어서다 말고 걷기로 튀었다.
        yield return new WaitForSeconds(Mathf.Max(0.05f, data.leapEndTime));
        EndAction();
    }

    // 잠복: 땅에 숨었다가 플레이어 옆으로 튀어나온다. 헬버그 시그니처.
    private IEnumerator BurrowAttack()
    {
        _busy = true;
        _burrowCd = data.burrowCooldown;
        _motor.StopMove();

        // ★파고들기 -> 이동 -> 튀어나오기. 튀어나오는 것 자체가 공격이다.
        //   예전엔 나온 뒤에 전조를 또 띄우고 때렸는데, 땅에서 솟구치는 것보다 더 좋은 예고가 없다.
        //   나오고 나서 딴 걸 한 번 더 하면 기습의 맛이 죽는다.
        Play(data.burrowInState);
        yield return new WaitForSeconds(Mathf.Max(0.05f, data.burrowInTime));
        if (_dead) yield break;

        SetRenderers(false);

        // ★땅속 구간을 둘로 나눈다.
        //   (1) 추적: 플레이어를 따라 땅속을 쫓는다.
        //   (2) 확정: 나올 자리를 못박고 그대로 솟구친다.
        //   이렇게 해야 "가만히 있었으면 발밑에서 터져서 맞는 게 납득이 가고,
        //   움직였으면 엉뚱한 데서 튀어나와 헛방" 이 된다.
        //   확정 없이 나오는 순간의 플레이어 위치를 쓰면 절대 못 피하는 공격이 되고,
        //   반대로 멀찍이 떨어져 나오면(예전 2.6m) 옆에서 튀어나왔는데 딜이 들어가서 이상하다.
        float under = Mathf.Max(0.2f, data.burrowUnderTime);
        float commit = Mathf.Clamp(data.burrowCommitTime, 0.05f, under);
        yield return new WaitForSeconds(under - commit);
        if (_dead) { SetRenderers(true); yield break; }

        // 나올 자리 확정. 기본은 플레이어 발밑 정확히(burrowEmergeDistance = 0).
        Vector3 emergeSpot = transform.position;
        if (_target != null)
        {
            emergeSpot = _target.position;
            if (data.burrowEmergeDistance > 0.01f)
            {
                Vector3 back = transform.position - _target.position;
                back.y = 0f;
                if (back.sqrMagnitude < 0.01f) back = -transform.forward;
                emergeSpot += back.normalized * data.burrowEmergeDistance;
            }
            emergeSpot = GroundSpot(emergeSpot);
            WarpTo(emergeSpot);

            Vector3 look = _target.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) _motor.FaceInstant(_target.position);
        }

        // 자리를 잡고 잠깐 뜸을 들인 뒤 솟구친다. 이 사이에 걸어 나오면 피해진다.
        yield return new WaitForSeconds(commit);
        if (_dead) { SetRenderers(true); yield break; }
        SetRenderers(true);

        // 솟구치는 모션. 이 모션 안에서 타격이 들어간다.
        Play(data.burrowOutState);
        _motor.Feedback?.PlayAttack();   // 솟구침 = 공격 순간 — 기존엔 소리 없었음
        float hit = Mathf.Clamp(data.burrowHitTime, 0.05f, Mathf.Max(0.1f, data.burrowOutTime));
        yield return new WaitForSeconds(hit);
        if (_dead) yield break;

        if (data.burrowImpactVfx != null)
        {
            var v = Instantiate(data.burrowImpactVfx, transform.position + Vector3.up * 0.4f, Quaternion.identity);
            TintVfx(v);
            Destroy(v, 4f);
        }
        // ★판정 중심 = 확정해둔 등장 지점. 솟구치면서 콜라이더에 밀려 몸이 살짝 어긋나도
        //   "튀어나온 자리"를 기준으로 맞는지가 정해진다.
        var ps = _motor.PlayerStat;
        if (ps != null && !ps.IsDead && _motor.Player != null)
        {
            Vector3 pp = _motor.Player.position; pp.y = 0f;
            Vector3 sp = emergeSpot; sp.y = 0f;
            if (Vector3.Distance(pp, sp) <= data.burrowRadius)
                ps.TakeDamage(data.attackDamage * data.burrowDamageMul, transform.position);
        }

        // 남은 등장 모션을 끝까지 보여준다
        yield return new WaitForSeconds(Mathf.Max(0.1f, data.burrowOutTime - hit));
        EndAction();
    }

    // 잠복 등으로 몸을 통째로 감춘다.
    private void SetRenderers(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;

        // ★체력바/이름표는 위 루프에 안 걸린다(UI 는 Renderer 가 아니라 CanvasRenderer).
        //   따로 안 꺼주면 몸은 땅속에 들어갔는데 체력바만 그 자리에 둥둥 남는다.
        if (_worldUI == null) _worldUI = GetComponentInChildren<EnemyWorldUI>(true);
        if (_worldUI != null) _worldUI.SetHidden(!on);
    }

    private void WarpTo(Vector3 pos)
    {
        if (_motor.AgentReady()) _motor.Agent.Warp(pos);
        else transform.position = pos;
    }

    private Vector3 GroundSpot(Vector3 pos)
        => NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas) ? hit.position : pos;

    // 지정한 색으로 칠한다(바닥 원처럼 색이 곧 의미인 것들).
    private static void PrepareTelegraph(GameObject go, Color c) => VfxTint.Apply(go, c);

    // ★차지/총구/탄/착탄 = 번호별로 짝이 맞게 제작된 한 세트다.
    //   안 건드리면 모을 때 / 나갈 때 / 맞을 때 색이 저절로 일치한다.
    //   틴트를 먹이면 파티클은 바뀌는데 텍스처로 그린 부분은 안 바뀌어서 오히려 어긋난다.
    //   그래서 기본은 "손대지 않음"이고, 굳이 색을 바꿀 몹만 tintVfx 를 켠다.
    private void TintVfx(GameObject go)
    {
        if (data.tintVfx) VfxTint.Apply(go, data.telegraphColor);
    }

    // ★VFX 프리팹의 "보이는 덩어리"가 원점에서 얼마나 치우쳐 있는지 잰다.
    //   Charge_07 처럼 자식이 z=0.8 에 있으면 원점에 맞춰 띄워도 눈에는 앞쪽에 뜬다.
    //   방출량이 많은 파티클일수록 시각적 비중이 크므로 그걸 가중치로 쓴다.
    //   헬 5형제가 어떤 VFX 를 쓰든 이 계산이 알아서 보정한다.
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

            Vector3 local = go.transform.InverseTransformPoint(ps.transform.position);
            sum += local * w;
            total += w;
        }

        return total > 0f ? sum / total : Vector3.zero;
    }
}
