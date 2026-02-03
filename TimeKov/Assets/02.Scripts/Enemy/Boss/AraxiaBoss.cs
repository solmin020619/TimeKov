using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class AraxiaBoss : MonoBehaviour
{
    [Header("Core Settings (SO)")]
    public BossDataSO bossData;

    [Header("Runtime Status")]
    [SerializeField] private float currentHP;
    public bool isCoreExposed = false;
    private bool isDead = false;
    private bool isAttacking = false;
    private bool isBattleStarted = false;
    private bool isPhaseStarting = false;

    [Header("References")]
    public Transform targetPlayer;
    public Animator animator;
    public GameObject coreProtectorVisual;
    public AraxiaCore coreScript;

    [Header("Summon Settings")]
    public GameObject spiderPrefab;
    public Transform[] summonPoints;
    private List<AraxiaMinion> activeMinions = new List<AraxiaMinion>();

    [Header("Attack Settings")]
    public Transform firePoint;
    public float attackCooldown = 3.0f;

    [Header("Pattern Prefabs")]
    public GameObject warningMarkerPrefab;
    public GameObject explosionPrefab;
    public GameObject missilePrefab; 
    public GameObject mortarMeshPrefab;

    [Header("Pattern Details")]
    public float mortarDelay = 1.5f;
    public float mortarHeight = 20.0f;
    public float missileSpeed = 15f;
    public int bombCount = 6;
    public float bombingRadius = 8.0f;

    private bool phase70Triggered = false;
    private bool phase30Triggered = false;

    void Start()
    {
        // 바닥 높이 보정
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.Warp(hit.position);
        }

        if (bossData != null) currentHP = bossData.maxHP;
        else currentHP = 1000f;

        if (targetPlayer == null)
            targetPlayer = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (coreProtectorVisual != null) coreProtectorVisual.SetActive(true);
    }

    void Update()
    {
        if (isDead || targetPlayer == null) return;

        if (!isBattleStarted)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.position);
            float detectRange = bossData != null ? bossData.visionRange : 15f;

            if (dist <= detectRange)
            {
                Debug.Log("보스: 침입자 감지! 전투 모드 가동.");
                isBattleStarted = true;
                StartCoroutine(StartPhase(bossData != null ? bossData.phase1SummonCount : 2));
            }
        }


        RotateTowards(targetPlayer.position);

        if (isCoreExposed) return;
        if (isPhaseStarting) return;

        if (activeMinions.Count == 0 && !isAttacking)
        {
            ExposeCore();
        }
    }

    IEnumerator StartPhase(int spawnCount)
    {
        isPhaseStarting = true;

        isCoreExposed = false;
        if (coreProtectorVisual != null) coreProtectorVisual.SetActive(true);

        animator.SetTrigger("Roar");
        yield return new WaitForSeconds(2.0f);

        SpawnSpiders(spawnCount);

        yield return new WaitForSeconds(0.5f);

        isPhaseStarting = false;

        StopCoroutine("CombatLoop");
        StartCoroutine("CombatLoop");
    }

    IEnumerator CombatLoop()
    {
        while (!isCoreExposed && !isDead)
        {
            if (targetPlayer != null && activeMinions.Count > 0 && !isAttacking)
            {
                int rand = Random.Range(0, 100);
                if (rand < 40) yield return StartCoroutine(Pattern_Mortar());
                else if (rand < 70) yield return StartCoroutine(Pattern_DirectShot());
                else yield return StartCoroutine(Pattern_CarpetBombing());

                yield return new WaitForSeconds(attackCooldown);
            }
            else
            {
                yield return null;
            }
        }
    }

    void SpawnSpiders(int count)
    {
        if (spiderPrefab == null || summonPoints == null) return;

        for (int i = 0; i < count; i++)
        {
            Transform spawnPos = summonPoints[i % summonPoints.Length];
            if (spawnPos != null)
            {
                GameObject obj = Instantiate(spiderPrefab, spawnPos.position, Quaternion.identity);
                AraxiaMinion minion = obj.GetComponent<AraxiaMinion>();
                if (minion != null)
                {
                    minion.boss = this;
                    activeMinions.Add(minion);
                }
            }
        }
    }

    IEnumerator Pattern_Mortar()
    {
        isAttacking = true;
        animator.SetTrigger("AttackA");

        Vector3 targetPos = targetPlayer.position;
        targetPos.y = transform.position.y;

        if (warningMarkerPrefab != null)
        {
            GameObject marker = Instantiate(warningMarkerPrefab, targetPos + Vector3.up * 0.1f, Quaternion.identity);
            Destroy(marker, mortarDelay);
        }

        yield return StartCoroutine(DropMortar(targetPos, mortarDelay));

        isAttacking = false;
    }

    IEnumerator Pattern_DirectShot()
    {
        isAttacking = true;
        animator.SetTrigger("AttackB");
        yield return new WaitForSeconds(0.5f);

        if (missilePrefab != null && firePoint != null)
        {
            GameObject missile = Instantiate(missilePrefab, firePoint.position, firePoint.rotation);
            missile.transform.LookAt(targetPlayer.position + Vector3.up);

            Rigidbody rb = missile.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = missile.transform.forward * missileSpeed;
        }
        isAttacking = false;
    }

    IEnumerator Pattern_CarpetBombing()
    {
        isAttacking = true;
        animator.SetTrigger("Skill");

        for (int i = 0; i < bombCount; i++)
        {
            if (targetPlayer == null) break;

            Vector2 randomCircle = Random.insideUnitCircle * bombingRadius;
            Vector3 dropPos = targetPlayer.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            dropPos.y = transform.position.y;

            if (warningMarkerPrefab != null)
            {
                GameObject marker = Instantiate(warningMarkerPrefab, dropPos + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(marker, mortarDelay);
            }

            StartCoroutine(DropMortar(dropPos, mortarDelay));

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(1.0f);
        isAttacking = false;
    }

    IEnumerator DropMortar(Vector3 impactPos, float duration)
    {
        GameObject projectile = null;

        if (mortarMeshPrefab != null)
        {
            Vector3 startPos = impactPos + Vector3.up * mortarHeight;
            projectile = Instantiate(mortarMeshPrefab, startPos, Quaternion.identity);
            projectile.transform.LookAt(impactPos);
        }

        float timer = 0f;
        Vector3 start = impactPos + Vector3.up * mortarHeight;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            if (projectile != null)
                projectile.transform.position = Vector3.Lerp(start, impactPos, t);

            yield return null;
        }

        if (projectile != null) Destroy(projectile);

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, impactPos, Quaternion.identity);
        }
    }

    IEnumerator SpawnExplosionDelayed(Vector3 pos, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (explosionPrefab != null) Instantiate(explosionPrefab, pos, Quaternion.identity);
    }

    public void OnMinionDead(AraxiaMinion minion)
    {
        if (activeMinions.Contains(minion)) activeMinions.Remove(minion);
    }

    void ExposeCore()
    {
        if (isCoreExposed) return;
        Debug.Log("코어 노출!");
        isCoreExposed = true;
        isAttacking = false;
        StopAllCoroutines();
        animator.SetBool("IsGroggy", true);
        if (coreProtectorVisual != null) coreProtectorVisual.SetActive(false);
    }

    public void TakeDamage(float damage)
    {
        if (!isCoreExposed) return;
        float reduction = bossData != null ? bossData.damageReduction : 0f;
        currentHP -= damage * (1f - reduction);

        float hpPercent = (currentHP / (bossData != null ? bossData.maxHP : 1000f)) * 100f;
        if (hpPercent <= 70f && !phase70Triggered)
        {
            phase70Triggered = true; RecoverFromGroggy(bossData != null ? bossData.phase2SummonCount : 3);
        }
        else if (hpPercent <= 30f && !phase30Triggered)
        {
            phase30Triggered = true; RecoverFromGroggy(bossData != null ? bossData.phase3SummonCount : 4);
        }
        else if (currentHP <= 0) Die();
    }

    void RecoverFromGroggy(int nextSpawnCount)
    {
        animator.SetBool("IsGroggy", false);
        StartCoroutine(StartPhase(nextSpawnCount));
    }

    void RotateTowards(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }
    }

    void Die()
    {
        isDead = true;
        StopAllCoroutines();
        animator.SetTrigger("Die");
    }
}