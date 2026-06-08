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

    // 퀘스트 뜨기 전에 이미 목표 레벨이면 즉시 완료.
    protected override bool IsAlreadySatisfied()
    {
        var m = CoreUpgradeManager.Instance;
        if (m == null) return false;
        int need = targetLevel > 0 ? targetLevel : 1;
        return m.CurrentCoreLevel >= need;
    }

    void OnUpgraded(int newLevel)
    {
        if (IsInGracePeriod) return;
        if (targetLevel > 0 && newLevel < targetLevel) return;
        Complete();
    }
}
