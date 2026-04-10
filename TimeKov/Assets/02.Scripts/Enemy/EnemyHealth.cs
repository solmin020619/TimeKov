using UnityEngine;
using System;

public class EnemyHealth : MonoBehaviour
{
    private EnemyAI enemyAI;

    public float maxHP = 100f;
    public float currentHP;

    [Header("Corpse Spawn")]
    public GameObject corpsePrefab;

    [Header("UI")]
    [SerializeField] private EnemyWorldUI enemyWorldUI;   // NEW: 적 머리 위 UI 연결
    [SerializeField] private Transform uiAnchor;          // NEW: 데미지 넘버 / UI 기준 위치

    public event Action OnDeath;
    public event Action OnDamage;

    public event Action<float, bool, Vector3> OnDamageUI; // NEW: 데미지, 치명타 여부, 표시 위치 전달

    private bool isDead = false;

    private void Awake()
    {
        currentHP = maxHP;
        enemyAI = GetComponent<EnemyAI>();

        if (enemyWorldUI == null)
            enemyWorldUI = GetComponentInChildren<EnemyWorldUI>(true);

        if (enemyWorldUI != null)
        {
            string enemyName = enemyAI != null && enemyAI.data != null ? enemyAI.data.enemyName : gameObject.name;
            enemyWorldUI.Initialize(this, enemyName);
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool isCritical)
    {
        if (isDead) return;

        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0f);

        OnDamage?.Invoke();

        Vector3 hitPos = uiAnchor != null ? uiAnchor.position : transform.position + Vector3.up * 2f;
        OnDamageUI?.Invoke(amount, isCritical, hitPos); // NEW: UI 쪽으로 데미지 정보 전달

        if (enemyWorldUI != null)
            enemyWorldUI.OnDamaged(); // NEW: 맞으면 체력바 잠깐 표시

        if (enemyAI != null)
            enemyAI.RevealFromHit();

        if (currentHP <= 0f)
        {
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