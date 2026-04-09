using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    private EnemyAI enemyAI;

    public float maxHP = 100f;
    public float currentHP;

    [Header("Corpse Spawn")]
    public GameObject corpsePrefab;

    public event Action OnDeath;
    public event Action OnDamage;

    private bool isDead = false;

    private void Awake()
    {
        currentHP = maxHP;
        enemyAI = GetComponent<EnemyAI>();
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHP -= amount;
        OnDamage?.Invoke();

        if (enemyAI != null)
            enemyAI.RevealFromHit();

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDeath?.Invoke();

        GameObject corpse = null;

        if (corpsePrefab != null)
        {
            corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);

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
                    Debug.LogWarning($"[EnemyHealth] dropSourceId가 비어 있음: {gameObject.name}", gameObject);
                }
            }
        }

        Destroy(gameObject);
    }
}