using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/지점 도달")]
public class ReachTriggerObjective : ObjectiveSO
{
    public string targetTriggerId;

    [Tooltip("이 설비ID가 이미 해금돼 있으면(0=미사용) 도착 안 해도 자동 완료. " +
             "설비 해금 퀘의 '이동' 목표용 - 미리 해금하고 떠난 플레이어가 그 위치로 헛걸음하지 않게.")]
    public int satisfiedIfFacilityUnlocked = 0;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnTriggerEntered += OnTrigger;
        if (satisfiedIfFacilityUnlocked != 0) GameEvents.OnFacilityUnlocked += OnUnlocked;
    }
    public override void Deactivate()
    {
        GameEvents.OnTriggerEntered -= OnTrigger;
        if (satisfiedIfFacilityUnlocked != 0) GameEvents.OnFacilityUnlocked -= OnUnlocked;
    }

    /// <summary>플레이어가 이미 트리거 안에 있거나, (지정 시) 해당 설비가 이미 해금된 상태로 퀘스트 시작 시 즉시 완료</summary>
    protected override bool IsAlreadySatisfied()
    {
        if (satisfiedIfFacilityUnlocked != 0
            && FacilityUnlockManager.Instance != null
            && FacilityUnlockManager.Instance.IsUnlocked(satisfiedIfFacilityUnlocked))
            return true;
        var t = QuestTrigger.Get(targetTriggerId);
        return t != null && t.IsPlayerInside;
    }

    void OnTrigger(string id) { if (id == (targetTriggerId ?? "").Trim()) Complete(); }
    void OnUnlocked(int facilityId) { if (facilityId == satisfiedIfFacilityUnlocked) Complete(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(targetTriggerId))
            Debug.LogError($"[ReachTrigger] '{name}' targetTriggerId 필수", this);
    }
#endif
}
