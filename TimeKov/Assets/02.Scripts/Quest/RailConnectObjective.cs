using System;
using UnityEngine;

// 레일로 설비(포트-포트)를 연결하면 완료. (RailBuildManager.CompleteRoute -> GameEvents.OnRailConnected)
[CreateAssetMenu(menuName = "Quest/Objective/RailConnect")]
public class RailConnectObjective : ObjectiveSO
{
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnRailConnected += OnConnected; }
    public override void Deactivate() => GameEvents.OnRailConnected -= OnConnected;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnConnected()
    {
        if (IsInGracePeriod) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate() { requiredCount = Mathf.Max(1, requiredCount); }
#endif
}
