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

    private bool isStealthActive;
    private Renderer[] renderers;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerTime playerTime;
    private EnemyHealth myHealth;
    private Animator anim;
    private AudioSource audioSource;

    private QuestUIManager questManager;

    private Vector3 startPosition;
    private float lastAttackTime;
    private float lastProvokedTime = -999f;
    private float lastRoarTime = -999f;
    private bool isAttacking = false;
    private bool hasPerformedFirstAttack = false;
    private bool isSelfDestructing = false;

    private bool isHitStunned = false;
    private float hitStunTimer = 0f;
    private Coroutine currentAttackRoutine;

    private float footstepTimer = 0f;
    private State previousState;

    private float pathUpdateDelay = 0.2f;
    private float pathUpdateTimer = 0f;

    private float attackRangeSqr;
    private float proximityRangeSqr;
    private float giveUpRangeSqr;
    private float visionRangeSqr;

    public LayerMask targetMask;
    public LayerMask obstacleMask;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (data != null && data.useStealth)
        {
            isStealthActive = true;
            SetStealthVisual(data.stealthAlpha);
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<EnemyHealth>();
        anim = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 5f;
        audioSource.maxDistance = 30f;
        audioSource.volume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        GlobalSettingsManager.OnSFXVolumeChanged += OnSFXVolumeChanged;

        if (data != null)
        {
            myHealth.maxHP = data.maxHP;
            myHealth.currentHP = data.maxHP;
            agent.speed = data.moveSpeed;

            attackRangeSqr = data.attackRange * data.attackRange;
            proximityRangeSqr = data.proximityRange * data.proximityRange;
            giveUpRangeSqr = data.giveUpChaseRange * data.giveUpChaseRange;
            visionRangeSqr = data.visionRange * data.visionRange;
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

        questManager = FindFirstObjectByType<QuestUIManager>();

        myHealth.OnDeath += DropLoot;
        myHealth.OnDeath += HandleDeathPresentation;
        myHealth.OnDamage += OnTakeDamage;

        currentState = State.Patrol;
        previousState = currentState;

        PlaySpawnPresentation();
        SetRandomPatrolDestination();
    }

    void OnDestroy()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= OnSFXVolumeChanged;

        if (myHealth != null)
        {
            myHealth.OnDeath -= DropLoot;
            myHealth.OnDeath -= HandleDeathPresentation;
            myHealth.OnDamage -= OnTakeDamage;
        }
    }

    void OnSFXVolumeChanged(float vol)
    {
        if (audioSource != null)
            audioSource.volume = vol;
    }

    void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip, volumeScale);
    }

    void SpawnVFX(GameObject prefab, Vector3 position, float lifeTime = 1f)
    {
        if (prefab == null) return;

        GameObject vfx = Instantiate(prefab, position, Quaternion.identity);

        if (lifeTime > 0f)
            Destroy(vfx, lifeTime);
    }

    void PlaySpawnPresentation()
    {
        if (data == null) return;

        SpawnVFX(data.spawnVFXPrefab, transform.position, 2f);
        PlayClip(data.spawnSound);
    }

    void HandleDeathPresentation()
    {
        if (data == null) return;

        SpawnVFX(data.deathVFXPrefab, transform.position, 2f);

        if (data.deathSound != null)
            AudioSource.PlayClipAtPoint(data.deathSound, transform.position, audioSource != null ? audioSource.volume : 1f);
    }

    void OnTakeDamage()
    {
        if (data == null) return;

        if (data.hitVFXPrefab != null)
        {
            GameObject vfx = Instantiate(data.hitVFXPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
            Destroy(vfx, 1f);
        }

        if (data.hitSound != null && audioSource != null)
            audioSource.PlayOneShot(data.hitSound);

        lastProvokedTime = Time.time;

        if (currentState != State.Chase && currentState != State.Attack)
            currentState = State.Chase;

        if (!data.useHitStun)
            return;

        if (data.interruptAttackOnHit && isAttacking)
        {
            if (currentAttackRoutine != null)
            {
                StopCoroutine(currentAttackRoutine);
                currentAttackRoutine = null;
            }

            isAttacking = false;
            currentState = State.Chase;

            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        isHitStunned = true;
        hitStunTimer = data.hitStunDuration;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (data.playHitAnimation && anim != null)
            anim.SetTrigger("Hit");
    }

    void Update()
    {
        if (playerTransform == null || data == null) return;

        if (isHitStunned)
        {
            hitStunTimer -= Time.deltaTime;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (anim != null)
                anim.SetFloat("Speed", 0f);

            if (hitStunTimer <= 0f)
            {
                isHitStunned = false;

                if (agent != null && agent.isOnNavMesh && currentState != State.Attack)
                    agent.isStopped = false;
            }

            return;
        }

        if (isStealthActive)
        {
            float alpha = data.stealthAlpha + Mathf.Sin(Time.time * 5f) * 0.05f;
            SetStealthVisual(alpha);
        }

        if (anim != null)
            anim.SetFloat("Speed", agent.velocity.magnitude);

        if (currentState == State.Chase && previousState != State.Chase)
        {
            if (Time.time >= lastRoarTime + 4f)
            {
                PlayClip(data.chaseRoarSound);
                lastRoarTime = Time.time;
            }
        }

        previousState = currentState;

        if (agent.velocity.magnitude > 0.1f && !isAttacking)
        {
            footstepTimer += Time.deltaTime;
            float interval = (currentState == State.Chase) ? 0.35f : 0.6f;

            if (footstepTimer >= interval)
            {
                PlayClip(data.footstepSound, 0.6f);
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = 0f;
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
        if (!agent.isOnNavMesh) return;

        agent.speed = data.moveSpeed;

        if (data.enemyType == EnemyType.Melee)
            hasPerformedFirstAttack = false;

        float distSqr = (playerTransform.position - transform.position).sqrMagnitude;

        if (distSqr <= proximityRangeSqr && HasLineOfSight(Mathf.Sqrt(distSqr)))
        {
            currentState = State.Chase;
            return;
        }

        if (CanSeePlayer())
        {
            currentState = State.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            if (!IsInvoking(nameof(SetRandomPatrolDestination)))
                Invoke(nameof(SetRandomPatrolDestination), 2f);
        }
    }

    void ChaseLogic()
    {
        if (!agent.isOnNavMesh) return;

        agent.speed = data.chaseSpeed;

        if (agent.isStopped)
            agent.isStopped = false;

        float distSqr = (playerTransform.position - transform.position).sqrMagnitude;
        bool isProvoked = (Time.time < lastProvokedTime + data.provokedDuration);

        if (distSqr > giveUpRangeSqr && !isProvoked)
        {
            currentState = State.Patrol;
            agent.ResetPath();
            SetRandomPatrolDestination();
            return;
        }

        pathUpdateTimer += Time.deltaTime;
        if (pathUpdateTimer >= pathUpdateDelay)
        {
            agent.SetDestination(playerTransform.position);
            pathUpdateTimer = 0f;
        }

        if (distSqr <= attackRangeSqr)
        {
            currentState = State.Attack;
            agent.ResetPath();
        }
    }
    void BeginAttackRoutine(IEnumerator routine)
    {
        if (currentAttackRoutine != null)
            StopCoroutine(currentAttackRoutine);

        currentAttackRoutine = StartCoroutine(routine);
    }
    void AttackLogic()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        float distSqr = (playerTransform.position - transform.position).sqrMagnitude;

        if (distSqr > attackRangeSqr && !isAttacking)
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
                        BeginAttackRoutine(PerformJumpAttack());
                        hasPerformedFirstAttack = true;
                    }
                    else
                    {
                        BeginAttackRoutine(PerformMeleeAttack());
                    }
                    break;

                case EnemyType.SuicideBomber:
                    BeginAttackRoutine(PerformSuicideAttack());
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

        if (anim != null)
            anim.SetTrigger("Attack");

        yield return new WaitForSeconds(data.attackHitDelay);

        SpawnVFX(data.explosionVFXPrefab, transform.position, 2f);

        if (data.explosionSound != null)
            AudioSource.PlayClipAtPoint(data.explosionSound, transform.position, audioSource.volume);

        Collider[] hits = Physics.OverlapSphere(transform.position, data.explosionRadius, targetMask);
        foreach (var hit in hits)
        {
            PlayerTime target = hit.GetComponent<PlayerTime>();
            if (target != null)
                target.TakeDamage(data.attackDamage);
        }

        if (data.dieAfterAttack)
        {
            isSelfDestructing = true;

            if (myHealth != null)
                myHealth.TakeDamage(99999f);
            else
                Destroy(gameObject);
        }
        else
        {
            isAttacking = false;
            currentState = State.Chase;
        }
        currentAttackRoutine = null;
    }

    IEnumerator PerformMeleeAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        int comboCount = Random.Range(1, 3);
        for (int i = 0; i < comboCount; i++)
        {
            if (anim != null)
                anim.SetTrigger("Attack");

            SpawnVFX(data.attackVFXPrefab, transform.position + transform.forward * 0.8f + Vector3.up * 1.0f, 1f);
            PlayClip(data.normalAttackSound);

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
                if (playerTime != null)
                    playerTime.TakeDamage(data.attackDamage);

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
        currentAttackRoutine = null;
    }

    IEnumerator PerformJumpAttack()
    {
        isAttacking = true;
        lastAttackTime = Time.time;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (anim != null)
            anim.SetTrigger("JumpAttack");

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
                agent.Move(dir * data.jumpLungeSpeed * Time.deltaTime);

            timer += Time.deltaTime;
            yield return null;
        }

        SpawnVFX(data.jumpLandVFXPrefab, transform.position, 1.5f);
        PlayClip(data.jumpAttackSound);

        Collider[] hits = Physics.OverlapSphere(transform.position, data.jumpAttackRadius, targetMask);
        foreach (var hit in hits)
        {
            PlayerTime pt = hit.GetComponent<PlayerTime>();
            if (pt != null)
                pt.TakeDamage(data.jumpAttackDamage);
        }

        float wait = data.jumpFullTime - data.jumpHitDelay;
        yield return new WaitForSeconds(wait < 0 ? 0 : wait);

        isAttacking = false;
        currentState = State.Chase;
        agent.isStopped = false;
        currentAttackRoutine = null;
    }

    void SetStealthVisual(float alpha)
    {
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                Color color = mat.color;
                color.a = alpha;
                mat.color = color;
            }
        }
    }

    public void RevealFromHit()
    {
        if (!isStealthActive) return;
        if (data == null) return;
        if (!data.revealOnHit) return;

        isStealthActive = false;
        SetStealthVisual(data.visibleAlpha);
    }

    void RotateTowards(Vector3 target, float speed = 10f)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0f;

        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                Time.deltaTime * speed
            );
        }
    }

    bool HasLineOfSight(float dist)
    {
        return !Physics.Raycast(
            transform.position + Vector3.up,
            (playerTransform.position - transform.position).normalized,
            dist,
            obstacleMask
        );
    }

    bool CanSeePlayer()
    {
        float distSqr = (playerTransform.position - transform.position).sqrMagnitude;

        if (distSqr < visionRangeSqr)
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < data.visionAngle / 2f)
                return HasLineOfSight(Mathf.Sqrt(distSqr));
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
        if (!isSelfDestructing && !string.IsNullOrEmpty(targetQuestName) && questManager != null)
            questManager.AddQuestProgress(targetQuestName, 1);
    }

    public string GetDropSourceId()
    {
        if (data == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(data.dropSourceId))
            return data.dropSourceId;

        return data.enemyName;
    }

    public int GetDropTier()
    {
        if (data == null)
            return 0;

        return data.dropTier;
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.visionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.attackRange);

        if (data.enemyType == EnemyType.SuicideBomber)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, data.explosionRadius);
        }
    }
}