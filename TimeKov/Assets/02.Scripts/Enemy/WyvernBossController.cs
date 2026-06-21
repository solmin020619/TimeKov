using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 와이번 지상 보스 - 전용 상태머신.
// 기존 BT(BehaviorGraph)/EnemyBrain은 건드리지 않고(취약), 그 글루(Data 동기화/Speed 파라미터/타게팅/회전)만 직접 복제.
// 재사용 컴포넌트: EnemyHealth(피해/사망), EnemyFeedback(피격/사망 연출), NavMeshAgent, Animator(WyvernBoss 전용 컨트롤러).
// 공격은 상태 이름으로 CrossFade 재생(전용 컨트롤러에 공격별 상태 존재).
// [Stage 1] 추적+물기+사망 [Stage 2] 원거리 파이어볼 [Stage 3] 근접 패턴(물기/꼬리침/화염방사/강타)+텔레그래프.
// 공격 시작 시 방향 커밋(추적 정지) -> dash로 옆/뒤 회피 가능. 강타는 긴 윈드업=읽히는 텔레그래프.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class WyvernBossController : MonoBehaviour
{
    [Header("데이터 (HP/속도/근접공격 기본 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;

    [Header("원거리 파이어볼")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Vector3 fireOffset = new Vector3(0f, 2.5f, 2.5f); // 발사 위치(로컬: 위+앞=입 근처)
    [SerializeField] private float rangedRange = 22f;
    [SerializeField] private float rangedCooldown = 3.5f;
    [SerializeField] private float fireballWindup = 0.55f;
    [SerializeField] private float fireballRecover = 0.7f;

    [Header("화염방사 VFX (SpreadFire 시 정면 분사)")]
    [SerializeField] private GameObject spreadFireVfx;

    [Header("애니메이터 (전용 컨트롤러 상태명)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string fireballState = "Fireball";

    // 근접 공격 정의 (state=컨트롤러 상태명 / reachMul=사거리배수 x attackRange / halfAngle=정면 호 반각
    //  / windup=발사프레임 / recover=후딜 / dmgMul=데미지배수 x attackDamage / cd=개별쿨 / weight=선택가중)
    private struct AtkDef
    {
        public string state; public float reachMul, halfAngle, windup, recover, dmgMul, cd, weight;
        public bool spreadFx;
    }
    private static readonly AtkDef[] MeleeAttacks =
    {
        new AtkDef { state="Bite",       reachMul=1.0f, halfAngle=50f, windup=0.45f, recover=0.5f, dmgMul=1.0f, cd=2.0f, weight=3f, spreadFx=false },
        new AtkDef { state="Stinger",    reachMul=1.4f, halfAngle=70f, windup=0.50f, recover=0.6f, dmgMul=0.9f, cd=3.5f, weight=2f, spreadFx=false },
        new AtkDef { state="SpreadFire", reachMul=1.2f, halfAngle=95f, windup=0.70f, recover=0.7f, dmgMul=0.8f, cd=6.0f, weight=2f, spreadFx=true  },
        new AtkDef { state="FinishBite", reachMul=1.3f, halfAngle=40f, windup=1.00f, recover=1.2f, dmgMul=2.2f, cd=9.0f, weight=1f, spreadFx=false },
    };
    private const float MeleeMaxReachMul = 1.4f;   // 근접 고려 최대 사거리(가장 긴 reachMul)
    private const float MeleeGap = 0.45f;          // 근접 공격 간 최소 간격

    // 컴포넌트(자동 캐싱)
    private NavMeshAgent _agent;
    private EnemyHealth _health;
    private EnemyFeedback _feedback;
    private Animator _animator;
    private int _speedHash;

    // 타깃
    private Transform _player;
    private PlayerStatComponent _playerStat;

    // 상태
    private bool _dead;
    private bool _attacking;
    private float _rangedCd;
    private float _meleeGapCd;
    private float[] _atkCd;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _feedback = GetComponent<EnemyFeedback>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);
        _atkCd = new float[MeleeAttacks.Length];

        if (_animator != null) _animator.applyRootMotion = false;
        if (_agent != null) _agent.updateRotation = false;   // 회전 직접 처리

        ApplyData();
    }

    // SO -> 컴포넌트 동기화 (EnemyBrain 역할 대체)
    private void ApplyData()
    {
        if (data == null) return;
        if (_agent != null)
        {
            _agent.speed = data.moveSpeed;
            _agent.acceleration = data.acceleration;
            _agent.angularSpeed = data.angularSpeed;
            _agent.stoppingDistance = Mathf.Max(0f, data.attackRange * data.attackApproachRatio);
        }
        if (_health != null) { _health.maxHP = data.maxHP; _health.currentHP = data.maxHP; }
        if (_feedback != null) _feedback.SetData(data);
    }

    private void Start()
    {
        AcquirePlayer();
        if (_health != null) _health.OnDeath += HandleDeath;
        _feedback?.PlaySpawn();
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        _dead = true;
        StopAllCoroutines();
        StopMove();
    }

    private void AcquirePlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        _player = p.transform;
        _playerStat = p.GetComponent<PlayerStatComponent>();
    }

    private void Update()
    {
        if (_dead) return;
        if (_player == null) AcquirePlayer();

        if (_animator != null && _agent != null)
            _animator.SetFloat(_speedHash, _agent.velocity.magnitude);

        TickCooldowns();
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target == null) { StopMove(); return; }
        if (data == null) return;

        float dist = PlanarDistance(target.position);
        float meleeMax = data.attackRange * MeleeMaxReachMul;

        if (dist <= meleeMax)
        {
            if (_meleeGapCd <= 0f && TrySelectMelee(dist, out int idx))
                StartCoroutine(MeleeAttack(idx));
            else
                StopMove();   // 사거리 안인데 쿨 -> 제자리 대기(견제 호흡)
        }
        else if (fireballPrefab != null && dist <= rangedRange)
        {
            if (_rangedCd <= 0f) StartCoroutine(SpitFireball());
            else Chase(target.position);
        }
        else
        {
            Chase(target.position);
        }
    }

    private void TickCooldowns()
    {
        float dt = Time.deltaTime;
        if (_rangedCd > 0f) _rangedCd -= dt;
        if (_meleeGapCd > 0f) _meleeGapCd -= dt;
        for (int i = 0; i < _atkCd.Length; i++)
            if (_atkCd[i] > 0f) _atkCd[i] -= dt;
    }

    // 사거리/각도/쿨 충족하는 근접 공격을 가중 랜덤으로 선택
    private bool TrySelectMelee(float dist, out int chosen)
    {
        chosen = -1;
        float total = 0f;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            var a = MeleeAttacks[i];
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * a.reachMul) continue;
            if (!PlayerInArc(data.attackRange * a.reachMul + 0.5f, a.halfAngle)) continue;
            total += a.weight;
        }
        if (total <= 0f) return false;

        float r = Random.value * total;
        for (int i = 0; i < MeleeAttacks.Length; i++)
        {
            var a = MeleeAttacks[i];
            if (_atkCd[i] > 0f) continue;
            if (dist > data.attackRange * a.reachMul) continue;
            if (!PlayerInArc(data.attackRange * a.reachMul + 0.5f, a.halfAngle)) continue;
            r -= a.weight;
            if (r <= 0f) { chosen = i; return true; }
        }
        return false;
    }

    private Transform ResolveTarget()
    {
        if (_player == null) return null;
        if (_playerStat != null && (_playerStat.IsDead || _playerStat.IsInBase)) return null;
        float aggro = data != null ? Mathf.Max(data.visionRange, rangedRange) : 25f;
        if (PlanarDistance(_player.position) > aggro) return null;
        return _player;
    }

    private void Chase(Vector3 dest)
    {
        if (!AgentReady()) return;
        _agent.isStopped = false;
        _agent.SetDestination(dest);
    }

    private void StopMove()
    {
        if (!AgentReady()) return;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
    }

    private bool AgentReady() => _agent != null && _agent.enabled && _agent.isOnNavMesh;

    private void PlayState(string stateName)
    {
        if (_animator != null && !string.IsNullOrEmpty(stateName))
            _animator.CrossFadeInFixedTime(stateName, 0.1f, 0);
    }

    // 근접 공격: 방향 커밋(정지+스냅) -> 상태 재생 -> windup 후 호(arc) 판정 데미지 -> recover -> 쿨 등록
    private IEnumerator MeleeAttack(int idx)
    {
        var a = MeleeAttacks[idx];
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);   // 시작 시 방향 커밋(이후 추적 정지 = 회피 가능)
        PlayState(a.state);
        _feedback?.PlayAttack();

        yield return new WaitForSeconds(a.windup);

        if (a.spreadFx && spreadFireVfx != null)
            Instantiate(spreadFireVfx, transform.TransformPoint(fireOffset), transform.rotation);

        if (!_dead && _playerStat != null &&
            PlayerInArc(data.attackRange * a.reachMul + 1f, a.halfAngle))
            _playerStat.TakeDamage(data.attackDamage * a.dmgMul, transform.position);

        yield return new WaitForSeconds(a.recover);

        _attacking = false;
        _atkCd[idx] = a.cd;
        _meleeGapCd = MeleeGap;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 원거리 파이어볼: 방향 커밋 -> Fireball -> windup 후 발사체 -> recover -> 쿨
    private IEnumerator SpitFireball()
    {
        _attacking = true;
        StopMove();
        if (_player != null) FaceInstant(_player.position);
        PlayState(fireballState);
        _feedback?.PlayAttack();

        yield return new WaitForSeconds(fireballWindup);
        if (!_dead) SpawnFireball();

        yield return new WaitForSeconds(fireballRecover);

        _attacking = false;
        _rangedCd = rangedCooldown;
        if (AgentReady()) _agent.isStopped = false;
    }

    private void SpawnFireball()
    {
        if (fireballPrefab == null || _player == null) return;
        Vector3 origin = transform.TransformPoint(fireOffset);
        Vector3 aim = (_player.position + Vector3.up) - origin;
        var go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(aim.sqrMagnitude > 0.0001f ? aim : transform.forward));
        var fb = go.GetComponent<WyvernFireball>();
        float dmg = data != null ? data.attackDamage : 20f;
        if (fb != null) fb.Launch(aim, dmg, _player);
    }

    // 정면 호(arc) 안에 플레이어가 있는지 (range 이내 + 정면 halfAngle 이내). 회피 판정의 핵심.
    private bool PlayerInArc(float range, float halfAngleDeg)
    {
        if (_player == null) return false;
        Vector3 to = _player.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > range * range) return false;
        if (halfAngleDeg >= 180f) return true;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        return Vector3.Angle(fwd, to) <= halfAngleDeg;
    }

    private void FaceInstant(Vector3 pos)
    {
        Vector3 to = pos - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to);
    }

    // 평소엔 이동방향/타깃을 향함. 공격 중엔 회전 정지(방향 커밋 = 플레이어가 옆/뒤로 dash 회피 가능).
    private void LateUpdate()
    {
        if (_dead || _attacking) return;
        Vector3 faceDir = Vector3.zero;
        if (_agent != null)
        {
            Vector3 vel = _agent.velocity; vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f) faceDir = vel;
        }
        if (faceDir.sqrMagnitude < 0.0001f)
        {
            Transform t = ResolveTarget();
            if (t != null) { Vector3 to = t.position - transform.position; to.y = 0f; faceDir = to; }
        }
        if (faceDir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(faceDir);
        float ang = data != null ? data.angularSpeed : 480f;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, ang * Time.deltaTime);
    }

    private float PlanarDistance(Vector3 worldPos)
    {
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = worldPos; b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
