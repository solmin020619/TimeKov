using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/FacilityProcessComplete")]
public class FacilityProcessCompleteObjective : ObjectiveSO
{
    [Tooltip("설비 ID. 0이면 모든 설비.")]
    public int facilityId;
    [Tooltip("출력될 아이템 ID")]
    public int outputItemId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnFacilityProcessComplete += OnComplete; }
    public override void Deactivate() => GameEvents.OnFacilityProcessComplete -= OnComplete;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnComplete(int facId, int outputId, int count)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && facId != facilityId) return;
        if (outputId != outputItemId) return;
        _count += count;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (outputItemId <= 0)
            Debug.LogWarning($"[FacilityProcessComplete] '{name}' outputItemId 미설정", this);
    }
#endif
}
