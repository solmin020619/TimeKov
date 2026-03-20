using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;

    [Header("Corpse Spawn")]
    public GameObject corpsePrefab;

    public event Action OnDeath;
    public event Action OnDamage;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        OnDamage?.Invoke();

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