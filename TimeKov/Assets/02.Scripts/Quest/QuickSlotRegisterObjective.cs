using System;
using UnityEngine;

// 소모품을 퀵슬롯(V)에 등록하면 완료. 인벤토리 우클릭 메뉴의 "퀵슬롯 등록" -> GameEvents.RaiseQuickSlotRegistered.
// 갭에서 미리 등록했거나(lookback) 이미 등록돼 있으면(라이브 상태) 즉시 인정 -> 소프트락 방지.
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/퀵슬롯 등록")]
public class QuickSlotRegisterObjective : ObjectiveSO
{
    [NonSerialized] bool _done;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnQuickSlotRegistered += OnRegistered;
        if (GameEvents.RecentCount(GameEvents.KeyQuickRegister) > 0 || AlreadyRegistered())
            _done = true;
    }

    public override void Deactivate() => GameEvents.OnQuickSlotRegistered -= OnRegistered;

    public override float Progress => _done ? 1f : 0f;
    protected override bool IsAlreadySatisfied() => _done;

    void OnRegistered(int itemId)
    {
        if (IsInGracePeriod) return;
        _done = true;
        ReportProgress(1f);
        Complete();
    }

    static bool AlreadyRegistered()
    {
        var p = UnityEngine.Object.FindAnyObjectByType<Player>();
        return p != null && p.QuickSlot != null && p.QuickSlot.HasRegistered;
    }
}
