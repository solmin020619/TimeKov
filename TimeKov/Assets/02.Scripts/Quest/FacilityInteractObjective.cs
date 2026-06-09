using System;
using UnityEngine;

/// <summary>
/// 특정 설비와 실제 상호작용(MachineUI 열기) 시 진행.
/// 단순 F 키 누르기가 아니라 MachineInteraction.OpenUI()가 호출되어야 발화.
/// 빌드 모드에서 F만 눌러도 깨지던 PressKey 방식의 한계 해결용.
/// </summary>
[CreateAssetMenu(menuName = "Quest/Objective/FacilityInteract")]
public class FacilityInteractObjective : ObjectiveSO
{
    [Tooltip("설비 ID. 0이면 모든 설비.")]
    public int facilityId;

    [Tooltip("필요 상호작용 횟수")]
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnFacilityInteract += OnInteract;
        _count += GameEvents.RecentCount(GameEvents.KeyInteract(facilityId));   // 갭에서 미리 연 분 인정
    }
    public override void Deactivate() => GameEvents.OnFacilityInteract -= OnInteract;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnInteract(int facId)
    {
        if (IsInGracePeriod) return;
        if (facilityId != 0 && facId != facilityId) return;
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
