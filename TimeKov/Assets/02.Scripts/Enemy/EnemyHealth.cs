using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    // 인스펙터에서 잠근다 = 여기서 바꿔도 소용없기 때문.
    // 몬스터 25종 전부 AI(EnemyBrain / BossMotor.ApplyData / FieldMonsterAI / HellMonsterAI /
    // SelfDestructSpiderAI)가 Awake 에서 자기 SO 의 maxHP 로 덮어쓴다. 예외 프리팹 없음.
    [FilledBy("몬스터 SO 의 maxHP (06.ScriptableObjects/Enemy)")]
    public float maxHP = 100f;

    // 순수 런타임 상태. Awake 에서 maxHP 로 채우므로 저장된 값은 의미가 없다.
    // 플레이 중 깎이는 걸 눈으로 보는 용도라 숨기지 않고 잠그기만 한다.
    [FilledBy("게임 로직 (시작할 때 maxHP 로 채움)")]
    public float currentHP;

    [Header("UI")]
    [SerializeField] private EnemyWorldUI enemyWorldUI;

    public event Action OnDeath;
    public event Action OnDamage;

    // [07-29] 사망 직전 가로채기(보스 페이즈 전환 등). null이 아니고 true 반환 시 사망 대신 HP를 1로 유지.
    // opt-in: 안 걸면(null) 기존대로 즉사. 다른 적 영향 0.
    public System.Func<bool> LethalGuard;

    // 무적(보스 회복 페이즈 등에서 켬). 시그니처 변경 없이 본문서 가드.
    public bool Invulnerable { get; set; }

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

        Loc.OnLanguageChanged += RefreshDisplayName;
    }

    private void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshDisplayName;
    }

    private void RefreshDisplayName()
    {
        if (enemyWorldUI != null)
            enemyWorldUI.SetName(ResolveDisplayName());
    }

    private string ResolveDisplayName()
    {
        // 일반몹은 EnemyBrain, 보스는 전용 컨트롤러가 IEnemyDataSource 를 구현한다.
        var src = GetComponent<IEnemyDataSource>();
        Debug.Log($"[{gameObject.name}] src={src}, data={src?.Data}, name={src?.Data?.enemyName}");
        if (src != null && src.Data != null && !string.IsNullOrEmpty(src.Data.enemyName))
            return Loc.Get(src.Data.enemyName);
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
        if (isDead || Invulnerable) return;

        LastHitPoint = hitPoint;

        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0f);

        OnDamage?.Invoke();
        feedback?.PlayHit(hitPoint);

        if (currentHP <= 0f)
        {
            // [07-29] 사망 가드가 막으면(보스 페이즈 전환) 죽지 않고 HP 1로 보류.
            if (LethalGuard != null && LethalGuard()) { currentHP = 1f; return; }
            Die();
        }
    }

    // 리쉬(이탈) 리셋 등에서 풀피로 되돌릴 때. 사망 상태면 무시.
    // OnDamage 를 쏴서 머리 위 체력바/상단 보스바를 즉시 갱신.
    public void ResetToFull()
    {
        if (isDead) return;
        currentHP = maxHP;
        OnDamage?.Invoke();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // 보스는 EnemyBrain 이 없다(전용 컨트롤러 사용). IEnemyDataSource 로 받아야
        // 보스 킬도 "unknown" 이 아닌 제 ID 로 올라간다 = 도감/드롭/퀘스트가 인식한다.
        var src = GetComponent<IEnemyDataSource>();
        string monsterId = "unknown";
        if (src != null && src.Data != null)
        {
            // 표시명(enemyName)과 매칭ID(enemyId) 분리. enemyId 비었으면 enemyName으로 fallback.
            monsterId = !string.IsNullOrEmpty(src.Data.enemyId)
                ? src.Data.enemyId
                : src.Data.enemyName;
        }
        GameEvents.RaiseEnemyKilled(monsterId);

        feedback?.PlayDeath();

        if (enemyWorldUI != null)
            enemyWorldUI.gameObject.SetActive(false);

        OnDeath?.Invoke();

        // BehaviorGraphAgent + NavMeshAgent + VisionSensor 등 즉시 멈춤 (애니만 재생)
        DisableActiveSystems();

        float delay = src != null && src.Data != null ? src.Data.deathAnimDuration : 1.5f;
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
