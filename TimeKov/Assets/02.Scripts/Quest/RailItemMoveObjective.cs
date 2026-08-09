using UnityEngine;

// 레일(벨트)로 아이템이 다음 설비로 자동 이동(전달)하면 완료.
// BeltSegment가 다음 설비에 Receive 시 GameEvents.RaiseRailItemMoved() 발화 → 여기서 감지.
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/레일로 아이템 이동")]
public class RailItemMoveObjective : ObjectiveSO
{
    [Tooltip("필요 이동 횟수(전달된 수량 합산)")]
    public int requiredCount = 1;

    [System.NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnRailItemMoved += OnMoved;
        _count += GameEvents.RecentCount(GameEvents.KeyRailMove);   // 갭 보정
        if (_count >= requiredCount) Complete();
    }

    public override void Deactivate() => GameEvents.OnRailItemMoved -= OnMoved;

    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    void OnMoved(int facilityId, int itemId, int count)
    {
        if (IsInGracePeriod) return;
        _count += count;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }
}
