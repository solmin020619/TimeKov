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

    public override void Activate()
    {
        GameEvents.OnFacilityPlaced += OnPlaced;
        _count += GameEvents.RecentCount(GameEvents.KeyPlaced(facilityId));   // 갭에서 미리 설치한 분 인정
        // 이미 배치된 설비도 인정. 배치물은 (소비되는 재료/연료와 달리) 월드에 계속 남으므로 현재 배치
        // 수를 직접 조회한다. lookback 윈도우(3.5s)를 넘겨 미리 설치했거나 씬에 선배치된 경우도 상태로 인정.
        var bm = FindAnyObjectByType<BuildManager>();
        if (bm != null)
        {
            int placed = bm.CountPlacedFacilities(facilityId);
            if (placed > _count) _count = placed;
        }
    }
    public override void Deactivate() => GameEvents.OnFacilityPlaced -= OnPlaced;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

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
