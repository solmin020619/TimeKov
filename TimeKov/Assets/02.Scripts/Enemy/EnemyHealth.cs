using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;

    [Header("Corpse Spawn")]
    public GameObject corpsePrefab;

    // 이벤트 정의
    public event Action OnDeath;
    public event Action OnDamage;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;

        // 데미지 입음 이벤트 호출 -> AI가 듣고 반응함
        OnDamage?.Invoke();

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        OnDeath?.Invoke();

        if (corpsePrefab != null)
        {
            Instantiate(corpsePrefab, transform.position, transform.rotation);
        }

        Debug.Log($"{gameObject.name} 사망");
        Destroy(gameObject);
    }
}