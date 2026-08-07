using System;
using UnityEngine;

// 설비 연료 슬롯에 연료를 넣으면 완료. (MachineBase.AddFuel -> GameEvents.OnFuelAdded)
[CreateAssetMenu(menuName = "Quest/Objective/FuelAdd")]
public class FuelAddObjective : ObjectiveSO
{
    [Tooltip("연료를 넣을 설비 ID. 0이면 아무 설비나.")]
    public int facilityId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnFuelAdded += OnFuel;
        _count += GameEvents.RecentCount(GameEvents.KeyFuel(facilityId));   // 갭에서 미리 넣은 연료 인정
    }
    public override void Deactivate() => GameEvents.OnFuelAdded -= OnFuel;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({_count}/{requiredCount})" : label;

    void OnFuel(int fid)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && fid != facilityId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate() { requiredCount = Mathf.Max(1, requiredCount); }
#endif
}
