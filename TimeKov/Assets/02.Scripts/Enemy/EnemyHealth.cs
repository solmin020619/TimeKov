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

    private void Awake()
    {
        currentHP = maxHP;
        enemyAI = GetComponent<EnemyAI>();
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        OnDamage?.Invoke();

        // 은신 몬스터라면 피격 시 은신 해제
        if (enemyAI != null)
            enemyAI.RevealFromHit();

        if (currentHP <= 0f)
            Die();
    }

    void Die()
    {
        OnDeath?.Invoke();

        if (corpsePrefab != null)
            Instantiate(corpsePrefab, transform.position, transform.rotation);

        Destroy(gameObject);
    }
}