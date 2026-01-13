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

    [Header("Animation Settings")]
    [Tooltip("공격 모션 시작 후 실제 타격이 들어가는 시간 (선딜)")]
    public float attackHitDelay = 0.5f;

    [Tooltip("공격 모션의 전체 길이 (이 시간이 지나야 다음 연타가 나감)")]
    public float attackAnimLength = 1.5f;

    [Header("Jump Attack Settings")] // 점프 공격 설정
    public float jumpAttackDamage = 25f;
    public float jumpAttackRadius = 3.0f; // 일반 공격보다 범위가 넓어야 함 (광역기)
    [Tooltip("애니메이션 시작 후, 발이 땅에서 떨어지는 시간 (점프 시작)")]
    public float jumpAttackWindup = 0.5f;
    [Tooltip("애니메이션 시작 후, 땅을 찍는 시간 (타격)")]
    public float jumpAttackHitDelay = 0.8f;
    public float jumpAttackFullTime = 2.0f; // 점프 애니메이션 전체 시간
    [Range(0f, 1f)] public float jumpChanceOnMiss = 0.7f; // 빗나갔을 때 점프 쓸 확률 (70%)
    public float jumpLungeSpeed = 10.0f;

    // 내부 변수
    private NavMeshAgent agent;
    private Transform playerTransform;   // 플레이어 위치
    private PlayerTime playerTime;       // 플레이어 Time 스크립트 (공격용)
    private EnemyHealth myHealth;        // 내 체력 스크립트 (드랍용)

    private float lastAttackTime;
    private float lastProvokedTime = -999f; // 마지막으로 공격당한 시간
    private bool isAttacking = false;
    private Animator anim;
    private bool hasPerformedFirstAttack = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<EnemyHealth>();
        anim = GetComponentInChildren<Animator>();

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

        if (anim != null)
        {
            anim.SetFloat("Speed", agent.velocity.magnitude);
        }

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
        if (hasPerformedFirstAttack) hasPerformedFirstAttack = false;

        if (!agent.isOnNavMesh) return;
        agent.speed = patrolSpeed;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distToPlayer <= proximityRange && HasLineOfSight(distToPlayer))
        { 
            currentState = State.Chase; return; 
        }

        if (CanSeePlayer())
        { 
            currentState = State.Chase; return; 
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!IsInvoking(nameof(SetRandomPatrolDestination))) Invoke(nameof(SetRandomPatrolDestination), patrolWaitTime);
        }

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

        if (agent.isStopped) agent.isStopped = false;

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
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (dist > attackRange && !isAttacking)
        {
            currentState = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
        {
            if (!hasPerformedFirstAttack)
            {
                StartCoroutine(PerformJumpAttack());
                hasPerformedFirstAttack = true;
            }
            else
            {
                StartCoroutine(PerformAttack());
            }
        }
        else if (!isAttacking)
        {
            RotateTowards(playerTransform.position);
        }
    }

    IEnumerator PerformJumpAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        Debug.Log("<color=purple>점프 공격(광역) 시전!</color>");

        if (anim != null) anim.SetTrigger("JumpAttack");

        float timer = 0f;
        while (timer < jumpAttackWindup)
        {
            RotateTowards(playerTransform.position, 10f);
            timer += Time.deltaTime;
            yield return null;
        }

        Vector3 targetPosition = playerTransform.position;

        Debug.DrawLine(transform.position, targetPosition, Color.red, 2.0f);


        float airTime = jumpAttackHitDelay - jumpAttackWindup;
        if (airTime < 0) airTime = 0;

        timer = 0f;
        while (timer < airTime)
        {
            RotateTowards(targetPosition, 10f);

            Vector3 dir = (targetPosition - transform.position).normalized;

            if (Vector3.Distance(transform.position, targetPosition) > 0.2f)
            {
                agent.Move(dir * jumpLungeSpeed * Time.deltaTime);
            }

            timer += Time.deltaTime;
            yield return null;
        }


        Collider[] hitColliders = Physics.OverlapSphere(transform.position, jumpAttackRadius, targetMask);
        bool hitAnyone = false;
        foreach (var hitCollider in hitColliders)
        {
            PlayerTime targetTime = hitCollider.GetComponent<PlayerTime>();
            if (targetTime != null)
            {
                targetTime.TakeDamage(jumpAttackDamage);
                Debug.Log($"<color=red>점프 찍기 쾅! -{jumpAttackDamage}</color>");
                hitAnyone = true;
            }
        }

        if (!hitAnyone) Debug.Log("<color=blue>점프 공격 빗나감 (회피 성공)</color>");

        float remainingWait = jumpAttackFullTime - jumpAttackHitDelay;
        if (remainingWait < 0) remainingWait = 0;
        yield return new WaitForSeconds(remainingWait);

        isAttacking = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }

    IEnumerator PerformAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        int comboCount = Random.Range(1, 3);

        for (int i = 0; i < comboCount; i++)
        {
            if (anim != null) anim.SetTrigger("Attack");

            float timer = 0f;
            while (timer < attackHitDelay)
            {
                RotateTowards(playerTransform.position, 15f);
                timer += Time.deltaTime;
                yield return null;
            }

            float d = Vector3.Distance(transform.position, playerTransform.position);
            bool isHit = d <= attackRange + 0.8f;

            if (isHit)
            {
                if (playerTime != null) playerTime.TakeDamage(damageToTime);

                float remainingWait = attackAnimLength - attackHitDelay;
                if (remainingWait < 0) remainingWait = 0;
                yield return new WaitForSeconds(remainingWait);
            }
            else
            {
                Debug.Log("일반 공격 빗나감!");

                if (Random.value < jumpChanceOnMiss)
                {
                    Debug.Log("<color=orange>공격 빗나감 -> 분노의 점프 공격 연계!</color>");

                    StartCoroutine(PerformJumpAttack());
                    yield break;
                }
                else
                {
                    Debug.Log("추적으로 복귀");
                    isAttacking = false;
                    currentState = State.Chase;
                    agent.isStopped = false;
                    yield break;
                }
            }
        }

        isAttacking = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }

    void RotateTowards(Vector3 target, float turnSpeed = 10f)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
        }
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

        Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
        Gizmos.DrawSphere(transform.position, jumpAttackRadius);
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}