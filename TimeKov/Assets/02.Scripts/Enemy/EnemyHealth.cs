using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;

    [Header("UI")]
    [SerializeField] private EnemyWorldUI enemyWorldUI;

    public event Action OnDeath;
    public event Action OnDamage;

    private bool isDead = false;
    public Vector3 LastHitPoint { get; private set; }

    private EnemyFeedback feedback;

    private void Awake()
    {
        currentHP = maxHP;

        feedback = GetComponent<EnemyFeedback>();
        if (feedback == null)
            feedback = GetComponentInChildren<EnemyFeedback>();

        if (enemyWorldUI == null)
            enemyWorldUI = GetComponentInChildren<EnemyWorldUI>(true);

        if (enemyWorldUI != null)
            enemyWorldUI.Initialize(this, gameObject.name);
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
        if (isDead) return;

        LastHitPoint = hitPoint;

        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0f);

        OnDamage?.Invoke();
        feedback?.PlayHit(hitPoint);

        if (currentHP <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        feedback?.PlayDeath();

        if (enemyWorldUI != null)
            enemyWorldUI.gameObject.SetActive(false);

        OnDeath?.Invoke();

        Destroy(gameObject);
    }
}
