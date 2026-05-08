using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/ReachTrigger")]
public class ReachTriggerObjective : ObjectiveSO
{
    public string targetTriggerId;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() => GameEvents.OnTriggerEntered += OnTrigger;
    public override void Deactivate() => GameEvents.OnTriggerEntered -= OnTrigger;

    /// <summary>플레이어가 이미 트리거 안에 서 있는 상태로 퀘스트 시작 시 즉시 완료</summary>
    protected override bool IsAlreadySatisfied()
    {
        var t = QuestTrigger.Get(targetTriggerId);
        return t != null && t.IsPlayerInside;
    }

    void OnTrigger(string id) { if (id == targetTriggerId) Complete(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(targetTriggerId))
            Debug.LogError($"[ReachTrigger] '{name}' targetTriggerId 필수", this);
    }
#endif
}
