using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class AraxiaBoss : MonoBehaviour
{
    [Header("Settings")]
    public BossDataSO bossData;

    [Header("Runtime Status")]
    [SerializeField] private float currentHP;
    public bool isCoreExposed = false;
    private bool isChasing = false; // 추격 상태 체크용
    private bool isSpawning = false; // ★ 추가됨: 소환 중인지 체크

    [Header("References")]
    public Transform targetPlayer;
    public NavMeshAgent navAgent;
    public Animator animator;
    public GameObject coreProtectorVisual;
    public AraxiaCore coreScript;

    [Header("Summon Settings")]
    public GameObject spiderPrefab;
    public Transform[] summonPoints;
    private List<AraxiaMinion> activeMinions = new List<AraxiaMinion>();

    private float lastAttackTime;
    private bool phase70Triggered = false;
    private bool phase30Triggered = false;

    void Start()
    {
        // 1. 공중 부양 방지 (바닥으로 강제 이동)
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            navAgent.Warp(hit.position);
        }

        // 2. 데이터 초기화
        if (bossData != null)
        {
            currentHP = bossData.maxHP;
            navAgent.speed = bossData.moveSpeed;
            navAgent.angularSpeed = bossData.rotationSpeed;
        }

        // 3. 1페이즈 시작
        if (bossData != null)
            StartCoroutine(StartPhase(bossData.phase1SummonCount));
    }

    void Update()
    {
        // 데이터가 없거나, 기절 상태거나(딜 타임), 죽었거나, ★소환 중이면 행동 중단
        if (bossData == null || isCoreExposed || currentHP <= 0 || isSpawning) return;

        // 소환수가 살아있으면 -> 플레이어를 쫓아가서 공격
        if (activeMinions.Count > 0)
        {
            DetectAndAction();
        }
        else
        {
            // 소환수가 다 죽었으면 -> 약점 노출 (그로기)
            ExposeCore();
        }
    }

    void DetectAndAction()
    {
        if (targetPlayer == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.position);

        // [상태 1] 아직 발견 못함 (대기)
        if (!isChasing)
        {
            // 플레이어가 감지 범위 안에 들어오면 추격 시작
            if (distanceToTarget <= bossData.visionRange)
            {
                isChasing = true;
                Debug.Log("보스: 침입자 발견! 추격 시작.");
            }
            else
            {
                navAgent.isStopped = true;
                return;
            }
        }

        // [상태 2] 추격 중
        if (isChasing)
        {
            // 플레이어가 너무 멀어지면 추격 포기
            if (distanceToTarget > bossData.giveUpChaseRange)
            {
                isChasing = false;
                navAgent.isStopped = true;
                return;
            }

            // 항상 플레이어를 바라봄
            RotateTowards(targetPlayer.position);

            // 공격 사거리 안인가?
            if (distanceToTarget <= bossData.basicAttackRange)
            {
                navAgent.isStopped = true; // 공격할 땐 멈춤
                if (Time.time > lastAttackTime + bossData.basicAttackCooldown)
                {
                    StartCoroutine(PerformAttack());
                }
            }
            else
            {
                // 사거리 밖이면 계속 쫓아감
                navAgent.isStopped = false;
                navAgent.speed = bossData.chaseSpeed;
                navAgent.SetDestination(targetPlayer.position);
            }
        }
    }

    IEnumerator PerformAttack()
    {
        lastAttackTime = Time.time;
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(bossData.basicAttackHitDelay);

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance <= bossData.basicAttackRange + 1.0f)
        {
            // 플레이어에게 데미지 적용 (PlayerTime 스크립트가 있다면 주석 해제)
            // PlayerTime pt = targetPlayer.GetComponent<PlayerTime>();
            // if (pt != null) pt.TakeDamage(bossData.basicAttackDamage);
            Debug.Log($"보스 공격 적중! 데미지: {bossData.basicAttackDamage}");
        }
    }

    IEnumerator StartPhase(int spawnCount)
    {
        isSpawning = true; // ★ 소환 시작 (Update 행동 멈춤)

        // 기존 상태 초기화
        isCoreExposed = false;
        if (coreProtectorVisual != null) coreProtectorVisual.SetActive(true);

        animator.SetTrigger("Roar");
        navAgent.isStopped = true;

        yield return new WaitForSeconds(2.0f); // 포효 연출 대기

        SpawnSpiders(spawnCount);

        isSpawning = false; // ★ 소환 끝 (이제 Update에서 추격 시작함)
        navAgent.isStopped = false;
    }

    void SpawnSpiders(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Transform spawnPos = summonPoints[i % summonPoints.Length];
            GameObject obj = Instantiate(spiderPrefab, spawnPos.position, Quaternion.identity);

            AraxiaMinion minion = obj.GetComponent<AraxiaMinion>();
            minion.boss = this;
            activeMinions.Add(minion);
        }
    }

    public void OnMinionDead(AraxiaMinion minion)
    {
        if (activeMinions.Contains(minion))
        {
            activeMinions.Remove(minion);
        }
    }

    void ExposeCore()
    {
        if (isCoreExposed) return;

        Debug.Log("보스: 으악! 코어 노출됨!");
        isCoreExposed = true;
        navAgent.isStopped = true;
        animator.SetBool("IsGroggy", true);

        if (coreProtectorVisual != null) coreProtectorVisual.SetActive(false);
    }

    public void TakeDamage(float damage)
    {
        if (!isCoreExposed) return;

        float finalDamage = damage * (1f - bossData.damageReduction);
        currentHP -= finalDamage;

        Debug.Log($"보스 체력: {currentHP}");

        float hpPercent = (currentHP / bossData.maxHP) * 100f;

        if (hpPercent <= 70f && !phase70Triggered)
        {
            phase70Triggered = true;
            RecoverFromGroggy(bossData.phase2SummonCount);
        }
        else if (hpPercent <= 30f && !phase30Triggered)
        {
            phase30Triggered = true;
            RecoverFromGroggy(bossData.phase3SummonCount);
        }
        else if (currentHP <= 0)
        {
            Die();
        }
    }

    void RecoverFromGroggy(int nextSpawnCount)
    {
        Debug.Log("보스: 회복하고 다음 페이즈 시작!");
        animator.SetBool("IsGroggy", false);
        StartCoroutine(StartPhase(nextSpawnCount));
    }

    void RotateTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * bossData.rotationSpeed);
    }

    void Die()
    {
        animator.SetTrigger("Die");
        navAgent.isStopped = true;
        this.enabled = false;
    }
}