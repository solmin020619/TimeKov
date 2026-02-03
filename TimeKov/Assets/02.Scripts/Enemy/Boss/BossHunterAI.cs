using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class BossHunterAI : MonoBehaviour
{
    [Header("Settings")]
    public BossDataSO data;

    public enum State { Patrol, Chase, BasicAttack, PrepareRush, Rush, Swing, Shatter, Groggy, Dead }
    public State currentState;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private EnemyHealth myHealth;
    private Animator anim;

    private Vector3 startPosition;

    // 쿨타임 관리
    private float lastBasicAttackTime;
    private float lastRushTime;
    private float lastSwingTime;
    private float lastShatterTime;

    private float lastProvokedTime = -999f;
    private bool isActing = false;
    private bool isGroggy = false;

    private bool isBattleStarted = false;

    [Header("Effects")]
    // warningMarkerPrefab 제거됨 (범위 표시 안 함)
    public GameObject explosionPrefab;     // 지면 폭발 이펙트 (Shatter 후속타용)

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
            startPosition = hit.position;

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
        // [1] 전투 개시 초기화 로직 (조우 시 1회만 실행)
        if (!isBattleStarted)
        {
            isBattleStarted = true;
            float now = Time.time;

            // 쿨타임을 현재 시간으로 설정 -> 지금부터 쿨타임만큼 기다려야 스킬 사용 가능
            lastRushTime = now;
            lastSwingTime = now;
            lastShatterTime = now;
            lastBasicAttackTime = now; // 기본 공격도 잠시 대기 (원하면 -999f로 해서 바로 쓰게 해도 됨)

            Debug.Log("보스: 전투 개시! 스킬 쿨타임 카운트 시작.");
        }

        agent.speed = data.chaseSpeed;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool isProvoked = (Time.time < lastProvokedTime + data.provokedDuration);

        // 추격 포기 조건
        if (dist > data.giveUpChaseRange && !isProvoked)
        {
            currentState = State.Patrol;
            SetRandomPatrolDestination();
            return;
        }

        bool canShatter = (Time.time >= lastShatterTime + data.shatterCooldown);
        if (dist <= data.shatterRange)
        {
            if (canShatter)
            {
                Debug.Log($"[패턴 발동] 지면 폭파! (거리: {dist})");
                StartCoroutine(PerformShatter());
                return;
            }
        }

        bool canSwing = (Time.time >= lastSwingTime + data.swingCooldown);
        if (dist <= data.swingRadius)
        {
            if (canSwing)
            {
                Debug.Log($"[패턴 발동] 휘두르기! (거리: {dist})");
                StartCoroutine(PerformSwing());
                return;
            }
        }

        bool canRush = (Time.time >= lastRushTime + data.rushCooldown);
        if (dist > 6.0f && canRush)
        {
            if (HasLineOfSight(dist))
            {
                Debug.Log("[패턴 발동] 돌진!");
                StartCoroutine(PerformRushSkill());
                return;
            }
            else
            {
                agent.SetDestination(playerTransform.position);
            }
        }

        bool canAttack = (Time.time >= lastBasicAttackTime + data.basicAttackCooldown);
        if (dist <= data.basicAttackRange && canAttack)
        {
            StartCoroutine(PerformBasicAttack());
            return;
        }

        agent.SetDestination(playerTransform.position);
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

        yield return new WaitForSeconds(0.5f);
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

        // 2초간 플레이어 응시
        float prepareTimer = 0f;
        while (prepareTimer < data.rushPrepareTime)
        {
            RotateTowards(playerTransform.position, data.rotationSpeed);
            prepareTimer += Time.deltaTime;
            yield return null;
        }

        currentState = State.Rush;
        if (anim) anim.SetTrigger("RushStart");

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
                break;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f, targetMask);
            foreach (var h in hits)
            {
                h.GetComponent<PlayerTime>()?.TakeDamage(data.rushDamage);
            }

            rushTimer += Time.deltaTime;
            yield return null;
        }

        agent.enabled = true;
        if (hitWall) StartCoroutine(GroggyState());
        else { yield return new WaitForSeconds(0.5f); isActing = false; currentState = State.Chase; }
    }

    // ★ [수정됨] 패턴 2: 광역 휘두르기 (범위 표시 삭제)
    IEnumerator PerformSwing()
    {
        isActing = true;
        currentState = State.Swing;
        lastSwingTime = Time.time;
        agent.isStopped = true;

        // 1. 차징 애니메이션 (기 모으기)
        if (anim) anim.SetTrigger("SwingPrepare");
        Debug.Log("Boss: Swing Charging... (Watch out!)");

        // 2. 2초간 대기 (플레이어는 보스의 모션만 보고 피해야 함)
        // 빨간 원 표시 코드 삭제됨
        float chargeTimer = 0f;
        while (chargeTimer < data.swingChargeTime)
        {
            // 차징 중에는 아주 천천히만 회전 (뒤를 잡을 기회 제공)
            RotateTowards(playerTransform.position, 2.0f);
            chargeTimer += Time.deltaTime;
            yield return null;
        }

        // 3. 공격!
        if (anim) anim.SetTrigger("SwingAttack");
        yield return new WaitForSeconds(data.swingHitDelay);

        // 180도 부채꼴 판정
        Collider[] targets = Physics.OverlapSphere(transform.position, data.swingRadius, targetMask);
        foreach (var target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
            // 전방 180도 체크
            if (Vector3.Angle(transform.forward, dirToTarget) < data.swingAngle / 2)
            {
                target.GetComponent<PlayerTime>()?.TakeDamage(data.swingDamage);
            }
        }

        yield return new WaitForSeconds(1.0f);
        isActing = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }

    // ★ 패턴 3: 지면 폭파
    IEnumerator PerformShatter()
    {
        isActing = true;
        currentState = State.Shatter;
        lastShatterTime = Time.time;
        agent.isStopped = true;

        if (anim) anim.SetTrigger("Shatter");

        // 1. 내려찍기 전까지 플레이어 조준
        float timer = 0f;
        while (timer < data.shatterHitDelay)
        {
            RotateTowards(playerTransform.position, data.rotationSpeed * 3);
            timer += Time.deltaTime;
            yield return null;
        }

        // 2. 쾅! 전방 내려찍기 (좁고 긴 범위)
        Collider[] targets = Physics.OverlapSphere(transform.position, data.shatterRange, targetMask);
        foreach (var target in targets)
        {
            Vector3 dirToTarget = (target.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToTarget) < data.shatterAngle / 2)
            {
                target.GetComponent<PlayerTime>()?.TakeDamage(data.shatterDamage);
            }
        }

        // 3. 후속타: 양 옆으로 퍼져나가는 폭발
        Vector3 centerPoint = transform.position + transform.forward * 3.0f;
        Vector3 rightDir = transform.right;

        for (int i = 1; i <= 3; i++)
        {
            yield return new WaitForSeconds(data.explosionDelay);

            float distance = i * data.explosionGap;
            Vector3 leftPos = centerPoint - rightDir * distance;
            Vector3 rightPos = centerPoint + rightDir * distance;

            leftPos.y = transform.position.y;
            rightPos.y = transform.position.y;

            SpawnExplosion(leftPos);
            SpawnExplosion(rightPos);
        }

        yield return new WaitForSeconds(1.0f);
        isActing = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }

    void SpawnExplosion(Vector3 pos)
    {
        if (explosionPrefab != null) Instantiate(explosionPrefab, pos, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(pos, data.explosionRadius, targetMask);
        foreach (var hit in hits)
        {
            hit.GetComponent<PlayerTime>()?.TakeDamage(data.explosionDamage);
        }
    }

    IEnumerator GroggyState()
    {
        currentState = State.Groggy;
        isGroggy = true;
        if (anim) anim.SetTrigger("Groggy");
        yield return new WaitForSeconds(data.groggyDuration);
        if (anim) anim.SetTrigger("Recover");
        yield return new WaitForSeconds(1.0f);
        isGroggy = false;
        isActing = false;
        currentState = State.Chase;
    }

    void OnDestroy() { if (myHealth != null) myHealth.OnDamage -= OnTakeDamage; }
    void OnTakeDamage() { lastProvokedTime = Time.time; if (currentState == State.Patrol) currentState = State.Chase; }
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
}