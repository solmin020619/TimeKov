using System;
using UnityEngine;

// 설비를 F로 해금하면 완료. (FacilityUnlockManager -> GameEvents.OnFacilityUnlocked)
[CreateAssetMenu(menuName = "Quest/Objective/FacilityUnlock")]
public class FacilityUnlockObjective : ObjectiveSO
{
    [Tooltip("해금할 설비 ID. 0이면 아무 설비나 해금 시 카운트.")]
    public int facilityId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnFacilityUnlocked += OnUnlocked; }
    public override void Deactivate() => GameEvents.OnFacilityUnlocked -= OnUnlocked;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnUnlocked(int unlockedId)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && unlockedId != facilityId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate() { requiredCount = Mathf.Max(1, requiredCount); }
#endif
}
