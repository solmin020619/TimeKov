using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;

    private void Awake()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        Debug.Log($"{gameObject.name} 피격! 남은 HP: {currentHP}");

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
        Destroy(gameObject);
    }
}