using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 와이번 지상 보스 - 전용 상태머신.
// 기존 BT(BehaviorGraph)/EnemyBrain은 건드리지 않고(취약), 그 글루(Data 동기화/Speed 파라미터/타게팅/회전)만 직접 복제.
// 재사용 컴포넌트: EnemyHealth(피해/사망), EnemyFeedback(피격/사망 연출), NavMeshAgent, Animator(WyvernBoss 전용 컨트롤러).
// 공격은 상태 이름으로 CrossFade 재생(전용 컨트롤러에 공격별 상태 존재).
// [Stage 1] 추적 + 근접 물기 + 사망. [Stage 2] 원거리 파이어볼 추가. 이후 화염방사/꼬리침/강타/포효 디버프/상단 체력바.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class WyvernBossController : MonoBehaviour
{
    [Header("데이터 (HP/속도/근접공격 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;

    [Header("원거리 파이어볼")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Vector3 fireOffset = new Vector3(0f, 2.5f, 2.5f); // 발사 위치(로컬: 위+앞=입 근처)
    [SerializeField] private float rangedRange = 22f;       // 이 거리 안이면 파이어볼 고려
    [SerializeField] private float rangedCooldown = 3.5f;
    [SerializeField] private float fireballWindup = 0.55f;  // 발사 애니 시작 후 실제 발사까지
    [SerializeField] private float fireballRecover = 0.7f;  // 발사 후 경직

    [Header("애니메이터 (전용 컨트롤러 상태명)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string biteState = "Bite";
    [SerializeField] private string fireballState = "Fireball";

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
    private float _meleeCd;
    private float _rangedCd;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _feedback = GetComponent<EnemyFeedback>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);

        if (_animator != null) _animator.applyRootMotion = false;
        if (_agent != null) _agent.updateRotation = false;   // 회전 직접 처리(공격 중에도 타깃 향함)

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

        if (_meleeCd > 0f) _meleeCd -= Time.deltaTime;
        if (_rangedCd > 0f) _rangedCd -= Time.deltaTime;
        if (_attacking) return;

        Transform target = ResolveTarget();
        if (target == null) { StopMove(); return; }

        float dist = PlanarDistance(target.position);

        // 근접: 물기(쿨이면 제자리 대기) / 중거리: 파이어볼(쿨이면 접근) / 원거리: 접근
        if (data != null && dist <= data.attackRange)
        {
            if (_meleeCd <= 0f) StartCoroutine(MeleeBite());
            else StopMove();
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

    // 근접 물기: 정지 -> Bite -> hitDelay 명중판정 -> animLength 후 해제 + 쿨다운
    private IEnumerator MeleeBite()
    {
        _attacking = true;
        StopMove();
        PlayState(biteState);
        _feedback?.PlayAttack();

        yield return new WaitForSeconds(data.hitDelay);
        if (!_dead && _playerStat != null && _player != null &&
            PlanarDistance(_player.position) <= data.attackRange + 1f)
            _playerStat.TakeDamage(data.attackDamage, transform.position);

        yield return new WaitForSeconds(Mathf.Max(0.05f, data.animLength - data.hitDelay));

        _attacking = false;
        _meleeCd = data.attackCooldown;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 원거리 파이어볼: 정지 -> Fireball -> windup 후 발사체 생성 -> recover 후 해제 + 쿨다운
    private IEnumerator SpitFireball()
    {
        _attacking = true;
        StopMove();
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
        Vector3 aim = (_player.position + Vector3.up) - origin;   // 가슴 높이 조준
        var go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(aim.sqrMagnitude > 0.0001f ? aim : transform.forward));
        var fb = go.GetComponent<WyvernFireball>();
        float dmg = data != null ? data.attackDamage : 20f;
        if (fb != null) fb.Launch(aim, dmg, _player);
    }

    // 이동 중이면 진행 방향, 멈춰있고 타깃 있으면 타깃 방향(공격 중에도 플레이어 향함)
    private void LateUpdate()
    {
        if (_dead) return;
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
