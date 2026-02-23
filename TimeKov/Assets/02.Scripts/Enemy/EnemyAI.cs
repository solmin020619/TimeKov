using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    [Header("Settings")]
    public EnemyDataSO data;

    public enum State { Patrol, Chase, Attack }
    [Header("Current State")]
    public State currentState;

    [Header("Quest Settings")]
    public string targetQuestName;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerTime playerTime;
    private EnemyHealth myHealth;
    private Animator anim;

    private Vector3 startPosition;
    private float lastAttackTime;
    private float lastProvokedTime = -999f;
    private bool isAttacking = false;
    private bool hasPerformedFirstAttack = false;
    private bool isSelfDestructing = false;

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

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
            startPosition = hit.position;
        }
        else
        {
            startPosition = transform.position;
        }

            PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            playerTransform = pc.transform;
            playerTime = pc.GetComponent<PlayerTime>();
        }

        myHealth.OnDeath += DropLoot;
        myHealth.OnDamage += OnTakeDamage;

        currentState = State.Patrol;
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

    void OnTakeDamage()
    {
        lastProvokedTime = Time.time;
        if (currentState != State.Chase && currentState != State.Attack)
        {
            currentState = State.Chase;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;
        if (data == null) return;

        if (anim != null) anim.SetFloat("Speed", agent.velocity.magnitude);

        switch (currentState)
        {
            case State.Patrol: PatrolLogic(); break;
            case State.Chase: ChaseLogic(); break;
            case State.Attack: AttackLogic(); break;
        }
    }

    void PatrolLogic()
    {
        if (!agent.isOnNavMesh) return;
        agent.speed = data.moveSpeed;

        if (data.enemyType == EnemyType.Melee) hasPerformedFirstAttack = false;

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
        if (!agent.isOnNavMesh) return;
        agent.speed = data.chaseSpeed;
        if (agent.isStopped) agent.isStopped = false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool isProvoked = (Time.time < lastProvokedTime + data.provokedDuration);

        if (dist > data.giveUpChaseRange && !isProvoked)
        {
            currentState = State.Patrol;
            agent.ResetPath();
            SetRandomPatrolDestination();
            return;
        }

        agent.SetDestination(playerTransform.position);

        if (dist <= data.attackRange)
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

        if (dist > data.attackRange && !isAttacking)
        {
            currentState = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (Time.time >= lastAttackTime + data.attackCooldown && !isAttacking)
        {
            switch (data.enemyType)
            {
                case EnemyType.Melee:
                    if (data.useJumpAttack && !hasPerformedFirstAttack)
                    {
                        StartCoroutine(PerformJumpAttack());
                        hasPerformedFirstAttack = true;
                    }
                    else
                    {
                        StartCoroutine(PerformMeleeAttack());
                    }
                    break;

                case EnemyType.SuicideBomber:
                    StartCoroutine(PerformSuicideAttack());
                    break;
            }
        }
        else if (!isAttacking)
        {
            RotateTowards(playerTransform.position);
        }
    }

    IEnumerator PerformSuicideAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        Debug.Log("<color=orange>ÀÚÆø ½ÃÄö½º ½ÃÀÛ! (ºÎÇ®¾î ¿À¸§)</color>");

        if (anim != null) anim.SetTrigger("Attack");

        yield return new WaitForSeconds(data.attackHitDelay);

        Collider[] hits = Physics.OverlapSphere(transform.position, data.explosionRadius, targetMask);
        bool hitPlayer = false;

        Debug.Log("Äç!!!!");

        foreach (var hit in hits)
        {
            PlayerTime target = hit.GetComponent<PlayerTime>();
            if (target != null)
            {
                target.TakeDamage(data.attackDamage);
                hitPlayer = true;
            }
        }

        if (data.dieAfterAttack)
        {
            isSelfDestructing = true;

            if (myHealth != null)
            {
                myHealth.TakeDamage(99999f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            isAttacking = false;
            currentState = State.Chase;
        }
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        int comboCount = Random.Range(1, 3);
        for (int i = 0; i < comboCount; i++)
        {
            if (anim != null) anim.SetTrigger("Attack");

            float timer = 0f;
            while (timer < data.attackHitDelay)
            {
                RotateTowards(playerTransform.position, 15f);
                timer += Time.deltaTime;
                yield return null;
            }

            float d = Vector3.Distance(transform.position, playerTransform.position);
            if (d <= data.attackRange + 0.8f)
            {
                if (playerTime != null) playerTime.TakeDamage(data.attackDamage);

                float wait = data.attackAnimLength - data.attackHitDelay;
                yield return new WaitForSeconds(wait < 0 ? 0 : wait);
            }
            else
            {
                if (data.useJumpAttack && Random.value < data.jumpChanceOnMiss)
                {
                    StartCoroutine(PerformJumpAttack());
                    yield break;
                }
                else
                {
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

    IEnumerator PerformJumpAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (anim != null) anim.SetTrigger("JumpAttack");

        float timer = 0f;
        while (timer < data.jumpWindup)
        {
            RotateTowards(playerTransform.position, 10f);
            timer += Time.deltaTime;
            yield return null;
        }

        Vector3 targetPos = playerTransform.position;

        float airTime = data.jumpHitDelay - data.jumpWindup;
        timer = 0f;
        while (timer < airTime)
        {
            RotateTowards(targetPos, 10f);
            Vector3 dir = (targetPos - transform.position).normalized;
            if (Vector3.Distance(transform.position, targetPos) > 0.2f)
            {
                agent.Move(dir * data.jumpLungeSpeed * Time.deltaTime);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, data.jumpAttackRadius, targetMask);
        foreach (var hit in hits)
        {
            PlayerTime pt = hit.GetComponent<PlayerTime>();
            if (pt != null) pt.TakeDamage(data.jumpAttackDamage);
        }

        float wait = data.jumpFullTime - data.jumpHitDelay;
        yield return new WaitForSeconds(wait < 0 ? 0 : wait);

        isAttacking = false;
        currentState = State.Chase;
        agent.isStopped = false;
    }


    void RotateTowards(Vector3 target, float speed = 10f)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * speed);
        }
    }

    bool HasLineOfSight(float dist)
    {
        return !Physics.Raycast(transform.position + Vector3.up, (playerTransform.position - transform.position).normalized, dist, obstacleMask);
    }

    bool CanSeePlayer()
    {
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist < data.visionRange)
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < data.visionAngle / 2)
                return HasLineOfSight(dist);
        }
        return false;
    }

    void SetRandomPatrolDestination()
    {
        Vector3 randomPoint = startPosition + Random.insideUnitSphere * data.patrolRadius;
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void DropLoot()
    {
        Debug.Log("Loot Dropped / Àû »ç¸Á Ã³¸®");

        if (!isSelfDestructing)
        {
            if (!string.IsNullOrEmpty(targetQuestName))
            {
                QuestUIManager questManager = FindFirstObjectByType<QuestUIManager>();

                if (questManager != null)
                {
                    questManager.AddQuestProgress(targetQuestName, 1);
                    Debug.Log($"{targetQuestName} Äù½ºÆ® Ä«¿îÆ® Áõ°¡!");
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, data.visionRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, data.attackRange);

        if (data.enemyType == EnemyType.SuicideBomber)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
            Gizmos.DrawSphere(transform.position, data.explosionRadius);
        }
    }
}