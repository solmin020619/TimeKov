using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class MeleeEnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack }

    [Header("State")]
    public State currentState;
    public string enemyName = "Melee Bot";

    [Header("Movement Stats")]
    public float patrolSpeed = 3.5f;
    public float chaseSpeed = 6.0f;
    public float patrolWaitTime = 2f;   // 순찰 지점 도착 후 대기 시간

    [Header("Combat Stats")]
    public float damageToTime = 15f;    // 공격 성공 시 깎을 Player Time
    public float attackRange = 1.5f;    // 공격 사거리
    public float attackCooldown = 2.0f; // 공격 후 딜레이

    [Header("Vision & Chase Settings")]
    public float visionRange = 12f;                    // 감지 거리
    public float proximityRange = 4.0f;                // 절대 감지 거리 (등 뒤도 감지, 단 벽은 못 뚫음)
    public float giveUpChaseRange = 20f;               // 추적 포기 거리 (이보다 멀어지면 집으로)
    public float provokedDuration = 10f;               // 피격 시 강제 추적 유지 시간 (초)
    [Range(0, 360)] public float visionAngle = 110f;   // 시야각
    public LayerMask targetMask;                       // 플레이어 레이어
    public LayerMask obstacleMask;                     // 벽 레이어

    [Header("Patrol Area")]
    public float patrolRadius = 10f;     // 시작 위치 기준 배회 반경
    private Vector3 startPosition;       // 스폰 위치 저장

    // 내부 변수
    private NavMeshAgent agent;
    private Transform playerTransform;   // 플레이어 위치
    private PlayerTime playerTime;       // 플레이어 Time 스크립트 (공격용)
    private EnemyHealth myHealth;        // 내 체력 스크립트 (드랍용)

    private float lastAttackTime;
    private float lastProvokedTime = -999f; // 마지막으로 공격당한 시간
    private bool isAttacking = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<EnemyHealth>();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
            startPosition = hit.position;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            playerTransform = pc.transform;
            playerTime = pc.GetComponent<PlayerTime>();
        }

        myHealth.OnDeath += DropLoot;
        myHealth.OnDamage += OnTakeDamage; // 맞으면 반응하는 함수 연결

        currentState = State.Patrol;
        agent.speed = patrolSpeed;

        SetRandomPatrolDestination();
    }

    void OnDestroy()
    {
        if (myHealth != null)
        {
            myHealth.OnDeath -= DropLoot;
            myHealth.OnDamage -= OnTakeDamage;
        }
    }

    // 데미지 입었을 때 실행
    void OnTakeDamage()
    {
        // 1. 맞은 시간 기록 (어그로 갱신)
        lastProvokedTime = Time.time;

        // 2. 이미 공격 중이면 상태 변경 불필요
        if (currentState == State.Attack) return;

        // 3. 순찰 중이거나 추적 중일 때 즉시 추적 모드 강제
        if (currentState != State.Chase)
        {
            Debug.Log("<color=red>피격! 거리 무시하고 추적 시작!</color>");
            currentState = State.Chase;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        switch (currentState)
        {
            case State.Patrol:
                PatrolLogic();
                break;
            case State.Chase:
                ChaseLogic();
                break;
            case State.Attack:
                AttackLogic();
                break;
        }
    }

    void PatrolLogic()
    {
        if (!agent.isOnNavMesh) return;
        agent.speed = patrolSpeed;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 1. 근접 감지 (거리 + 벽 체크)
        if (distToPlayer <= proximityRange)
        {
            // 거리는 가깝지만, 벽이 가로막고 있는지 확인
            if (HasLineOfSight(distToPlayer))
            {
                Debug.Log("인기척 감지! (Proximity - 벽 없음)");
                currentState = State.Chase;
                return;
            }
        }

        // 2. 시각 감지 (시야각 + 거리 + 벽 체크)
        if (CanSeePlayer())
        {
            Debug.Log("시각 감지! (Vision)");
            currentState = State.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!IsInvoking(nameof(SetRandomPatrolDestination)))
            {
                Invoke(nameof(SetRandomPatrolDestination), patrolWaitTime);
            }
        }
    }

    bool HasLineOfSight(float distance)
    {
        // 바닥이 아닌 눈 높이에서 체크
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        Vector3 targetPos = playerTransform.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - origin).normalized;

        // 장애물에 걸리지 않아야 true
        if (!Physics.Raycast(origin, dir, distance, obstacleMask))
        {
            return true;
        }
        return false;
    }

    void SetRandomPatrolDestination()
    {
        Vector3 randomPoint = startPosition + Random.insideUnitSphere * patrolRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, 5.0f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void ChaseLogic()
    {
        if (!agent.isOnNavMesh) return;

        agent.speed = chaseSpeed;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // 피격 당한지 얼마 안 됐으면 거리가 멀어도 포기하지 않음
        bool isProvoked = (Time.time < lastProvokedTime + provokedDuration);

        // 포기 조건 - 거리가 멀어짐 AND 화난 상태가 아님
        if (dist > giveUpChaseRange && !isProvoked)
        {
            Debug.Log("타겟 놓침. 순찰 복귀.");
            currentState = State.Patrol;
            agent.ResetPath();
            SetRandomPatrolDestination();
            return;
        }

        // 플레이어 위치로 이동
        agent.SetDestination(playerTransform.position);

        // 공격 사거리 진입 시 Attack 상태 전환
        if (dist <= attackRange)
        {
            currentState = State.Attack;
            agent.ResetPath();
        }
    }

    void AttackLogic()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // 사거리 벗어나면 다시 Chase
        if (dist > attackRange && !isAttacking)
        {
            currentState = State.Chase;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
        {
            StartCoroutine(PerformAttack());
        }
        else if (!isAttacking)
        {
            RotateTowards(playerTransform.position);
        }
    }

    IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        int comboCount = Random.Range(1, 3);

        for (int i = 0; i < comboCount; i++)
        {
            yield return new WaitForSeconds(0.3f);

            float d = Vector3.Distance(transform.position, playerTransform.position);
            if (d <= attackRange + 0.5f)
            {
                if (playerTime != null)
                {
                    playerTime.TakeDamage(damageToTime);
                }
            }
            yield return new WaitForSeconds(0.4f);
        }
        isAttacking = false;
    }

    void DropLoot() { Debug.Log("아이템 드랍"); }

    bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector3 dirToTarget = (playerTransform.position - transform.position).normalized;
        float dstToTarget = Vector3.Distance(transform.position, playerTransform.position);

        if (dstToTarget < visionRange)
        {
            // 각도 체크
            if (Vector3.Angle(transform.forward, dirToTarget) < visionAngle / 2)
            {
                // 장애물 체크
                return HasLineOfSight(dstToTarget);
            }
        }
        return false;
    }

    void RotateTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 순찰 범위 (초록)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition == Vector3.zero ? transform.position : startPosition, patrolRadius);

        // 감지 범위 (노랑)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // 절대 감지 범위 (보라색)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, proximityRange);

        // 추적 포기 범위 (파랑)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, giveUpChaseRange);

        // 공격 범위 (빨강)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}