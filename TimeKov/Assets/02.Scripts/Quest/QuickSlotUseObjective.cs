using System;
using UnityEngine;

// 등록한 소모품을 퀵슬롯(V)으로 사용 시도하면 완료. PlayerQuickSlotComponent.TryUse -> GameEvents.RaiseQuickSlotUsed.
// 만피로 실제 회복이 막혀도 'V로 시도'는 인정(만피 상태 영구 미완료=소프트락 방지). 갭 사용분은 lookback으로 인정.
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/퀵슬롯 사용")]
public class QuickSlotUseObjective : ObjectiveSO
{
    [NonSerialized] bool _done;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnQuickSlotUsed += OnUsed;
        if (GameEvents.RecentCount(GameEvents.KeyQuickUse) > 0) _done = true;
    }

    public override void Deactivate() => GameEvents.OnQuickSlotUsed -= OnUsed;

    public override float Progress => _done ? 1f : 0f;
    protected override bool IsAlreadySatisfied() => _done;

    void OnUsed(int itemId)
    {
        if (IsInGracePeriod) return;
        _done = true;
        ReportProgress(1f);
        Complete();
    }
}
