using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 와이번 지상 보스 - 전용 상태머신.
// 기존 BT(BehaviorGraph)/EnemyBrain은 건드리지 않고(취약), 그 글루(Data 동기화/Speed 파라미터/타게팅/회전)만 직접 복제.
// 재사용 컴포넌트: EnemyHealth(피해/사망), EnemyFeedback(피격/사망 연출), NavMeshAgent, Animator(Wyvern_Override).
// [Stage 1] 추적 + 근접 물기 + 사망까지. 이후 단계서 파이어볼/패턴/포효 디버프/상단 체력바 추가 예정.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class WyvernBossController : MonoBehaviour
{
    [Header("데이터 (HP/속도/공격 수치는 SO에서 튜닝)")]
    [SerializeField] private MeleeEnemyData data;

    [Header("애니메이터 파라미터 (EnemyBase 컨트롤러 규약)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string attackTrigger = "Attack";

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
    private float _attackCd;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<EnemyHealth>();
        _feedback = GetComponent<EnemyFeedback>();
        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        _speedHash = Animator.StringToHash(speedParam);

        // Root Motion 켜져있으면 SO 속도 무시되므로 OFF. 회전은 LateUpdate서 직접(공격 중에도 타깃 향함).
        if (_animator != null) _animator.applyRootMotion = false;
        if (_agent != null) _agent.updateRotation = false;

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

    // 사망 시 AI 정지 (EnemyHealth가 콜라이더/네비/사망애니/삭제는 처리. 이 컨트롤러는 EnemyHealth가 안 꺼서 직접 멈춤).
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

        // Idle <-> Locomotion 전이
        if (_animator != null && _agent != null)
            _animator.SetFloat(_speedHash, _agent.velocity.magnitude);

        if (_attackCd > 0f) _attackCd -= Time.deltaTime;
        if (_attacking) return;   // 공격 모션 중 이동/판단 잠금

        Transform target = ResolveTarget();
        if (target == null) { StopMove(); return; }

        float dist = PlanarDistance(target.position);
        if (data != null && dist <= data.attackRange && _attackCd <= 0f)
            StartCoroutine(BiteRoutine());
        else
            Chase(target.position);
    }

    // 결계 밖/생존 중인 플레이어가 시야(visionRange) 안일 때만 타깃
    private Transform ResolveTarget()
    {
        if (_player == null) return null;
        if (_playerStat != null && (_playerStat.IsDead || _playerStat.IsInBase)) return null;
        float aggro = data != null ? data.visionRange : 25f;
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

    // 근접 물기: 정지 -> Attack 트리거 -> hitDelay 시점 명중 판정 -> animLength 후 해제 + 쿨다운
    private IEnumerator BiteRoutine()
    {
        _attacking = true;
        StopMove();
        if (_animator != null && !string.IsNullOrEmpty(attackTrigger)) _animator.SetTrigger(attackTrigger);
        _feedback?.PlayAttack();

        yield return new WaitForSeconds(data.hitDelay);
        if (!_dead && _playerStat != null && _player != null)
        {
            // hitDelay 시점에 사거리(+여유) 안이면 데미지(공격자 위치 전달=플레이어 피격 방향 피드백)
            if (PlanarDistance(_player.position) <= data.attackRange + 1f)
                _playerStat.TakeDamage(data.attackDamage, transform.position);
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, data.animLength - data.hitDelay));

        _attacking = false;
        _attackCd = data.attackCooldown;
        if (AgentReady()) _agent.isStopped = false;
    }

    // 이동 중이면 진행 방향, 멈춰있고 타깃 있으면 타깃 방향을 바라봄(공격 중에도 플레이어 향함)
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
