using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class BossHunterAI : MonoBehaviour
{
    [Header("Settings")]
    public BossDataSO data; // 통합된 BossDataSO 사용

    public enum State { Patrol, Chase, BasicAttack, PrepareRush, Rush, Groggy, Dead }
    public State currentState;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private EnemyHealth myHealth;
    private Animator anim;

    private Vector3 startPosition;
    private float lastBasicAttackTime;
    private float lastRushTime;
    private float lastProvokedTime = -999f;
    private bool isActing = false;
    private bool isGroggy = false;

    public LayerMask targetMask;
    public LayerMask obstacleMask;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<EnemyHealth>();
        anim = GetComponentInChildren<Animator>();

        if (data != null)
        {
            myHealth.maxHP = data.maxHP;
            myHealth.currentHP = data.maxHP;
            agent.speed = data.moveSpeed;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            startPosition = hit.position;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) playerTransform = pc.transform;

        myHealth.OnDamage += OnTakeDamage;

        currentState = State.Patrol;
        SetRandomPatrolDestination();
    }

    void Update()
    {
        if (currentState == State.Dead || playerTransform == null || isActing || isGroggy) return;
        if (anim != null) anim.SetFloat("Speed", agent.velocity.magnitude);

        switch (currentState)
        {
            case State.Patrol: PatrolLogic(); break;
            case State.Chase: ChaseLogic(); break;
        }
    }

    void PatrolLogic()
    {
        agent.speed = data.moveSpeed;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= data.proximityRange && HasLineOfSight(dist)) { currentState = State.Chase; return; }
        if (CanSeePlayer()) { currentState = State.Chase; return; }
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!IsInvoking(nameof(SetRandomPatrolDestination))) Invoke(nameof(SetRandomPatrolDestination), 2f);
        }
    }

    void ChaseLogic()
    {
        agent.speed = data.chaseSpeed;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool isProvoked = (Time.time < lastProvokedTime + data.provokedDuration);

        if (dist > data.giveUpChaseRange && !isProvoked)
        {
            currentState = State.Patrol;
            SetRandomPatrolDestination();
            return;
        }

        // 헌터의 돌진 패턴
        if (Time.time >= lastRushTime + data.rushCooldown && dist > 4.0f)
        {
            if (HasLineOfSight(dist))
            {
                StartCoroutine(PerformRushSkill());
            }
            else
            {
                agent.SetDestination(playerTransform.position);
            }
        }
        else if (dist <= data.basicAttackRange && Time.time >= lastBasicAttackTime + data.basicAttackCooldown)
        {
            StartCoroutine(PerformBasicAttack());
        }
        else
        {
            agent.SetDestination(playerTransform.position);
        }
    }

    IEnumerator PerformBasicAttack()
    {
        isActing = true;
        currentState = State.BasicAttack;
        lastBasicAttackTime = Time.time;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (anim) anim.SetTrigger("Attack");

        float timer = 0f;
        while (timer < data.basicAttackHitDelay)
        {
            RotateTowards(playerTransform.position, data.rotationSpeed * 2);
            timer += Time.deltaTime;
            yield return null;
        }

        float d = Vector3.Distance(transform.position, playerTransform.position);
        if (d <= data.basicAttackRange + 1.0f)
        {
            PlayerTime pt = playerTransform.GetComponent<PlayerTime>();
            if (pt != null) pt.TakeDamage(data.basicAttackDamage);
        }

        float remaining = data.basicAttackAnimLength - data.basicAttackHitDelay;
        yield return new WaitForSeconds(remaining);
        isActing = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }

    IEnumerator PerformRushSkill()
    {
        isActing = true;
        currentState = State.PrepareRush;
        lastRushTime = Time.time;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (anim) anim.SetTrigger("RushPrepare");

        float prepareTimer = 0f;
        while (prepareTimer < data.rushPrepareTime)
        {
            RotateTowards(playerTransform.position, data.rotationSpeed);
            prepareTimer += Time.deltaTime;
            yield return null;
        }

        currentState = State.Rush;
        if (anim) anim.SetTrigger("RushStart");
        Debug.Log("[Boss] 돌진 시작!");

        Vector3 rushDir = transform.forward;
        float rushTimer = 0f;
        float maxRushDuration = 3.0f;
        bool hitWall = false;

        agent.enabled = false;

        while (rushTimer < maxRushDuration)
        {
            float moveStep = data.rushSpeed * Time.deltaTime;
            transform.Translate(Vector3.forward * moveStep);

            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit hit, 1.5f, obstacleMask))
            {
                hitWall = true;
                Debug.Log($"[Boss] 벽 충돌 감지! (충돌체: {hit.collider.name})");
                break;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f, targetMask);
            foreach (var h in hits)
            {
                PlayerTime pt = h.GetComponent<PlayerTime>();
                if (pt != null) pt.TakeDamage(data.rushDamage);
            }

            rushTimer += Time.deltaTime;
            yield return null;
        }

        agent.enabled = true;

        if (hitWall)
        {
            StartCoroutine(GroggyState());
        }
        else
        {
            Debug.Log("[Boss] 돌진 종료 (벽 충돌 없음 - 복귀 시도)");

            lastProvokedTime = Time.time;

            yield return new WaitForSeconds(0.5f);
            isActing = false;
            currentState = State.Chase;
        }
    }

    IEnumerator GroggyState()
    {
        currentState = State.Groggy;
        isGroggy = true;
        Debug.Log($"[Boss] 그로기 상태 진입!");
        if (anim) anim.SetTrigger("Groggy");
        yield return new WaitForSeconds(data.groggyDuration);
        Debug.Log("[Boss] 그로기 종료");
        if (anim) anim.SetTrigger("Recover");
        yield return new WaitForSeconds(1.0f);
        isGroggy = false;
        isActing = false;
        currentState = State.Chase;
    }

    void OnDestroy() { if (myHealth != null) myHealth.OnDamage -= OnTakeDamage; }
    void OnTakeDamage() { lastProvokedTime = Time.time; if (currentState == State.Patrol) currentState = State.Chase; }
    public float GetDamageMultiplier() { return isGroggy ? 1.0f : 1.0f - data.damageReduction; }
    void RotateTowards(Vector3 target, float speed)
    {
        Vector3 dir = (target - transform.position).normalized; dir.y = 0;
        if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * speed);
    }
    bool HasLineOfSight(float dist) { return !Physics.Raycast(transform.position + Vector3.up, (playerTransform.position - transform.position).normalized, dist, obstacleMask); }
    bool CanSeePlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist < data.visionRange)
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < data.visionAngle / 2) return HasLineOfSight(dist);
        }
        return false;
    }
    void SetRandomPatrolDestination()
    {
        Vector3 randomPoint = startPosition + Random.insideUnitSphere * data.patrolRadius;
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 5f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }
    public bool IsGroggy() => isGroggy;
}