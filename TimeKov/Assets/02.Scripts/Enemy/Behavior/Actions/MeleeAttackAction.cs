using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

/// <summary>
/// 근접 공격 Action 노드.
/// - OnStart: Animator 의 Attack Trigger 발동.
/// - HitDelay 시점에 사거리 안 타겟에게 1회 데미지 적용.
/// - AnimLength 만큼 행동 잠금 후 Success.
/// 데미지 진입점은 PlayerStatComponent.TakeDamage 우선, 없으면 EnemyHealth.TakeDamage(float, bool, Vector3) 시도.
/// </summary>
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
    [Tooltip("애니메이션 시작 후 데미지가 들어가는 시점 (초).")]
    [SerializeReference] public BlackboardVariable<float> HitDelay = new BlackboardVariable<float>(0.5f);
    [Tooltip("공격 한 사이클 전체 시간 (초). 이 시간 동안 BT 가 다음 노드로 못 넘어감.")]
    [SerializeReference] public BlackboardVariable<float> AnimLength = new BlackboardVariable<float>(1.5f);
    [Tooltip("Animator 트리거 이름. 비워두면 애니 발동 안 함.")]
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

        // EnemyFeedback 의 Attack VFX/Sound 재생.
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

        // 공격 시작 후 타겟이 사거리 밖으로 도망갔으면 헛스윙.
        float distance = Vector3.Distance(Agent.Value.transform.position, Target.Value.transform.position);
        if (distance > AttackRange.Value)
            return;

        // 1) Player 의 새 데미지 진입점.
        var playerStat = Target.Value.GetComponent<PlayerStatComponent>();
        if (playerStat != null)
        {
            playerStat.TakeDamage(Damage.Value);
            return;
        }

        // 2) 적이 다른 적을 때리는 케이스 (테스트/특수 상황).
        var enemyHealth = Target.Value.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector3 hitPoint = Target.Value.transform.position + Vector3.up * 1.5f;
            enemyHealth.TakeDamage(Damage.Value, false, hitPoint);
        }
    }
}
