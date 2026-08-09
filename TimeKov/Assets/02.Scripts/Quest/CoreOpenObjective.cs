using UnityEngine;

// 코어 강화 단말(UI)을 열면 완료되는 objective.
// CoreUpgradeUI.Open() 에서 GameEvents.RaiseCoreUIOpened() 발화 → 여기서 감지.
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/코어 창 열기")]
public class CoreOpenObjective : ObjectiveSO
{
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnCoreUIOpened += OnOpened;
        // 퀘 갭 동안 미리 연 경우 인정 (EVENT-IN-GAP lookback)
        if (GameEvents.RecentCount(GameEvents.KeyCoreOpen) > 0) Complete();
    }

    public override void Deactivate() => GameEvents.OnCoreUIOpened -= OnOpened;

    // 퀘 뜨기 전에 이미 코어 UI가 열려 있으면 즉시 완료
    protected override bool IsAlreadySatisfied()
    {
        var ui = CoreUpgradeUI.Instance;
        return ui != null && ui.IsOpen;
    }

    void OnOpened()
    {
        if (IsInGracePeriod) return;
        Complete();
    }
}
