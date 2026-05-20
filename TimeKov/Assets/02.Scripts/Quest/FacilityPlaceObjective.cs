using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/FacilityPlace")]
public class FacilityPlaceObjective : ObjectiveSO
{
    [Tooltip("DataManager 설비 ID. 0이면 모든 설비 카운트.")]
    public int facilityId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnFacilityPlaced += OnPlaced; }
    public override void Deactivate() => GameEvents.OnFacilityPlaced -= OnPlaced;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnPlaced(int placedId)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && placedId != facilityId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
    }
#endif
}
