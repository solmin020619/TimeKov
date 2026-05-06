using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    private EnemyAI enemyAI;

    public float maxHP = 100f;
    public float currentHP;

    [Header("Corpse Spawn")]
    public GameObject corpsePrefab;

    [Header("UI")]
    [SerializeField] private EnemyWorldUI enemyWorldUI;
    [SerializeField] private Transform uiAnchor;

    public event Action OnDeath;
    public event Action OnDamage;

    private bool isDead = false;
    public Vector3 LastHitPoint { get; private set; }

    private EnemyFeedback feedback;

    private void Awake()
    {
        currentHP = maxHP;
        enemyAI = GetComponent<EnemyAI>();

        feedback = GetComponent<EnemyFeedback>();
        if (feedback == null)
            feedback = GetComponentInChildren<EnemyFeedback>();

        if (enemyWorldUI == null)
            enemyWorldUI = GetComponentInChildren<EnemyWorldUI>(true);

        if (enemyWorldUI != null)
        {
            string enemyName = (enemyAI != null && enemyAI.data != null && !string.IsNullOrWhiteSpace(enemyAI.data.enemyName))
                ? enemyAI.data.enemyName
                : gameObject.name;

            enemyWorldUI.Initialize(this, enemyName);
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false, transform.position + Vector3.up * 1.5f);
    }

    public void TakeDamage(float amount, bool isCritical)
    {
        TakeDamage(amount, isCritical, transform.position + Vector3.up * 1.5f);
    }

    public void TakeDamage(float amount, bool isCritical, Vector3 hitPoint)
    {
        if (isDead)
            return;

        LastHitPoint = hitPoint;

        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0f);

        OnDamage?.Invoke();
        feedback?.PlayHit(hitPoint);

        if (enemyAI != null)
            enemyAI.RevealFromHit();

        if (currentHP <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        feedback?.PlayDeath();

        if (enemyWorldUI != null)
            enemyWorldUI.gameObject.SetActive(false);

        bool useDeathAnimation = enemyAI != null &&
                                 enemyAI.data != null &&
                                 enemyAI.data.useDeathAnimation;

        Debug.Log($"[EnemyHealth] Die called | useDeathAnimation = {useDeathAnimation}", gameObject);

        if (useDeathAnimation)
        {
            StartCoroutine(DeathRoutine());
        }
        else
        {
            Debug.Log("[EnemyHealth] useDeathAnimation false -> SpawnCorpseLootObject ��� ����", gameObject);
            SpawnCorpseLootObject();
            Destroy(gameObject);
        }
    }

    private void PlayDeathEffect()
    {
        if (enemyAI == null || enemyAI.data == null)
            return;

        if (enemyAI.data.deathVFXPrefab != null)
        {
            Instantiate(
                enemyAI.data.deathVFXPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null && enemyAI.data.deathSound != null)
        {
            audioSource.PlayOneShot(enemyAI.data.deathSound);
        }
    }

    private IEnumerator DeathRoutine()
    {
        Debug.Log("[EnemyHealth] DeathRoutine ����", gameObject);

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        Animator anim = GetComponentInChildren<Animator>();

        Collider[] allCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < allCols.Length; i++)
        {
            if (allCols[i] != null)
                allCols[i].enabled = false;
        }

        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            agent.enabled = false;
        }

        if (enemyAI != null)
            enemyAI.enabled = false;

        if (anim != null)
            anim.SetTrigger("Dead");

        float delay = 1f;
        if (enemyAI != null && enemyAI.data != null)
            delay = enemyAI.data.deathAnimDuration;

        // �״� �ִ� �߰���, �ٴڿ� ���� Ÿ�̹��� ����Ʈ/����
        yield return new WaitForSeconds(delay * 0.7f);

        PlayDeathEffect();

        yield return new WaitForSeconds(delay * 0.3f);

        SpawnCorpseLootObject();
        Destroy(gameObject);
    }
    private void SpawnCorpseLootObject()
    {
        Debug.Log("[EnemyHealth] SpawnCorpseLootObject ȣ���", gameObject);

        if (corpsePrefab == null)
            return;

        GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);

        MonsterLoot monsterLoot = corpse.GetComponent<MonsterLoot>();
        if (monsterLoot != null && enemyAI != null)
        {
            string dropSourceId = enemyAI.GetDropSourceId();
            int dropTier = enemyAI.GetDropTier();

            monsterLoot.sourceType = "monster";
            monsterLoot.monsterType = dropSourceId;
            monsterLoot.dropTier = dropTier;

            if (string.IsNullOrWhiteSpace(dropSourceId))
            {
                Debug.LogWarning($"[EnemyHealth] dropSourceId�� ��� ����: {gameObject.name}", gameObject);
            }
        }
    }
}