using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/ItemAcquire")]
public class ItemAcquireObjective : ObjectiveSO
{
    [Tooltip("DataManager 아이템 ID")]
    public int itemId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnItemAcquired += OnAcquired; }
    public override void Deactivate() => GameEvents.OnItemAcquired -= OnAcquired;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnAcquired(int id, int count)
    {
        if (IsInGracePeriod) return;
        if (id != itemId) return;
        _count += count;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (itemId <= 0)
            Debug.LogWarning($"[ItemAcquire] '{name}' itemId 미설정", this);
    }
#endif
}
