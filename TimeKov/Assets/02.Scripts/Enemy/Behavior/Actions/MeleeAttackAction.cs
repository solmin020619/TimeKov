using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Melee Attack",
    description: "Triggers attack animation, applies damage at HitDelay if target still in range, locks for AnimLength.",
    story: "[Agent] melee attacks [Target]",
    category: "Action/Custom",
    id: "7f3a4b2c1d8e9f0a1b2c3d4e5f6a7b8c")]
internal partial class MeleeAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> Damage = new BlackboardVariable<float>(15f);
    [SerializeReference] public BlackboardVariable<float> AttackRange = new BlackboardVariable<float>(2f);
    [SerializeReference] public BlackboardVariable<float> HitDelay = new BlackboardVariable<float>(0.5f);
    [SerializeReference] public BlackboardVariable<float> AnimLength = new BlackboardVariable<float>(1.5f);
    [SerializeReference] public BlackboardVariable<string> AttackTrigger = new BlackboardVariable<string>("Attack");

    private Animator m_Animator;
    private float m_Elapsed;
    private bool m_DamageApplied;
    private float m_HitDelay;
    private float m_AnimLength;

    protected override Status OnStart()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        m_Animator = Agent.Value.GetComponentInChildren<Animator>();

        // 기본값 = 그래프에 박힌 값(HitDelay 0.5 / AnimLength 1.5).
        m_HitDelay = HitDelay.Value;
        m_AnimLength = AnimLength.Value;
        string trigger = AttackTrigger.Value;

        // SO 값이 있으면 그걸 우선한다.
        // 이유: 이 노드의 HitDelay/AnimLength/AttackTrigger 는 그래프에서 블랙보드에 링크돼 있지 않고
        // 로컬 상수로 박혀 있다. 게다가 BehaviorGraphAgent.SetVariableValue 는 변수를 못 찾으면
        // 조용히 false 만 반환해서(EnemyBrain 은 반환값을 안 본다) 에러 없이 무시돼 왔다.
        // 그 결과 전 몬스터가 0.5/1.5 로 고정 동작했다. 여기서 SO 를 직접 읽어 그 통로를 되살린다.
        var src = Agent.Value.GetComponent<IEnemyDataSource>();
        if (src != null && src.Data != null)
        {
            m_HitDelay = src.Data.hitDelay;
            m_AnimLength = src.Data.animLength;
            if (!string.IsNullOrEmpty(src.Data.attackTrigger))
                trigger = src.Data.attackTrigger;
        }

        if (m_Animator != null && !string.IsNullOrEmpty(trigger))
            m_Animator.SetTrigger(trigger);

        EnemyFeedback feedback = Agent.Value.GetComponent<EnemyFeedback>();
        if (feedback != null)
            feedback.PlayAttack();

        m_Elapsed = 0f;
        m_DamageApplied = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Agent.Value == null)
            return Status.Failure;

        m_Elapsed += Time.deltaTime;

        if (!m_DamageApplied && m_Elapsed >= m_HitDelay)
        {
            m_DamageApplied = true;
            ApplyDamage();
        }

        if (m_Elapsed >= m_AnimLength)
            return Status.Success;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        m_Animator = null;
    }

    private void ApplyDamage()
    {
        if (Target.Value == null) return;

        float distance = Vector3.Distance(Agent.Value.transform.position, Target.Value.transform.position);
        if (distance > AttackRange.Value)
            return;

        var playerStat = Target.Value.GetComponent<PlayerStatComponent>();
        if (playerStat != null)
        {
            playerStat.TakeDamage(Damage.Value);
            return;
        }

        var enemyHealth = Target.Value.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector3 hitPoint = Target.Value.transform.position + Vector3.up * 1.5f;
            enemyHealth.TakeDamage(Damage.Value, false, hitPoint);
        }
    }
}
