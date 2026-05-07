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

    protected override Status OnStart()
    {
        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        m_Animator = Agent.Value.GetComponentInChildren<Animator>();

        string trigger = AttackTrigger.Value;
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

        if (!m_DamageApplied && m_Elapsed >= HitDelay.Value)
        {
            m_DamageApplied = true;
            ApplyDamage();
        }

        if (m_Elapsed >= AnimLength.Value)
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
