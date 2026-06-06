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
            enemyWorldUI.Initialize(this, ResolveDisplayName());
    }

    private string ResolveDisplayName()
    {
        var brain = GetComponent<EnemyBrain>();
        if (brain != null && brain.Data != null && !string.IsNullOrEmpty(brain.Data.enemyName))
            return brain.Data.enemyName;
        // SO 없을 때 fallback: "Enemy_X(Clone)" → "Enemy_X"
        return gameObject.name.Replace("(Clone)", "").Trim();
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

        var brain = GetComponent<EnemyBrain>();
        string monsterId = "unknown";
        if (brain != null && brain.Data != null)
        {
            // 표시명(enemyName)과 매칭ID(enemyId) 분리. enemyId 비었으면 enemyName으로 fallback.
            monsterId = !string.IsNullOrEmpty(brain.Data.enemyId)
                ? brain.Data.enemyId
                : brain.Data.enemyName;
        }
        GameEvents.RaiseEnemyKilled(monsterId);

        feedback?.PlayDeath();

        if (enemyWorldUI != null)
            enemyWorldUI.gameObject.SetActive(false);

        OnDeath?.Invoke();

        // BehaviorGraphAgent + NavMeshAgent + VisionSensor 등 즉시 멈춤 (애니만 재생)
        DisableActiveSystems();

        float delay = brain != null && brain.Data != null ? brain.Data.deathAnimDuration : 1.5f;
        Destroy(gameObject, delay);
    }

    void DisableActiveSystems()
    {
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;

        var btAgent = GetComponent<Unity.Behavior.BehaviorGraphAgent>();
        if (btAgent != null) btAgent.enabled = false;

        var vs = GetComponentInChildren<VisionSensor>();
        if (vs != null) vs.enabled = false;

        // Collider 비활성 (사망 후 추가 데미지 받지 않음 + 플레이어 통과 가능)
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        // EnemyBrain 정지 — 끄지 않으면 LateUpdate 회전 로직이 계속 돌아
        // 죽은 적이 플레이어를 향해 계속 돌아서며 "따라오는" 것처럼 보임.
        // (Die 애니는 Any State 트리거라 EnemyBrain 꺼도 정상 재생됨)
        var brain = GetComponent<EnemyBrain>();
        if (brain != null) brain.enabled = false;
    }
}
