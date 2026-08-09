using UnityEngine;

// 코어 강화. 0이면 강화를 "시도"(성공/실패 무관)하면 완료, targetLevel>0이면 그 레벨 도달(성공) 시 완료.
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/코어 강화")]
public class CoreUpgradeObjective : ObjectiveSO
{
    [Tooltip("이 레벨 이상 도달 시 완료. 0이면 강화를 시도(성공/실패 무관)하면 완료.")]
    public int targetLevel = 0;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnCoreUpgraded += OnUpgraded;            // 성공 + 레벨 도달
        CoreUpgradeManager.OnUpgradeResult += OnAttempt;    // 시도(성공/실패)
        // 갭 보정: targetLevel 0(시도형)은 퀘 갭에서 한 강화 시도를 인정 (실패해도 키트는 소모됐으므로)
        if (targetLevel <= 0 && GameEvents.RecentCount(GameEvents.KeyCoreUpgrade) > 0) Complete();
    }
    public override void Deactivate()
    {
        GameEvents.OnCoreUpgraded -= OnUpgraded;
        CoreUpgradeManager.OnUpgradeResult -= OnAttempt;
    }
    public override float Progress => IsCompleted ? 1f : 0f;

    // 강화를 시도하면(재료 소모 후 확률 판정, 성공/실패 무관) 완료. targetLevel 지정 시엔 레벨 도달로만.
    void OnAttempt(bool success)
    {
        if (IsInGracePeriod) return;
        if (targetLevel > 0) return;
        Complete();
    }

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
