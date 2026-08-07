using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/FacilityInput")]
public class FacilityInputObjective : ObjectiveSO
{
    [Tooltip("설비 ID. 0이면 모든 설비.")]
    public int facilityId;
    [Tooltip("투입할 아이템 ID")]
    public int inputItemId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnFacilityInput += OnInput;
        _count += GameEvents.RecentCount(GameEvents.KeyInput(facilityId, inputItemId));   // 갭에서 미리 투입한 분 인정
    }
    public override void Deactivate() => GameEvents.OnFacilityInput -= OnInput;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({_count}/{requiredCount})" : label;

    void OnInput(int facId, int itemId, int count)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && facId != facilityId) return;
        if (itemId != inputItemId) return;
        _count += count;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (inputItemId <= 0)
            Debug.LogWarning($"[FacilityInput] '{name}' inputItemId 미설정", this);
    }
#endif
}
