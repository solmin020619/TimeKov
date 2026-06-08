using UnityEngine;

// 코어를 강화(성공)하면 완료. (CoreUpgradeManager -> GameEvents.OnCoreUpgraded)
[CreateAssetMenu(menuName = "Quest/Objective/CoreUpgrade")]
public class CoreUpgradeObjective : ObjectiveSO
{
    [Tooltip("이 레벨 이상 도달 시 완료. 0이면 강화 1회 성공 시 완료.")]
    public int targetLevel = 0;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnCoreUpgraded += OnUpgraded; }
    public override void Deactivate() => GameEvents.OnCoreUpgraded -= OnUpgraded;
    public override float Progress => IsCompleted ? 1f : 0f;

    void OnUpgraded(int newLevel)
    {
        if (IsInGracePeriod) return;
        if (targetLevel > 0 && newLevel < targetLevel) return;
        Complete();
    }
}
