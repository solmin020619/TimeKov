using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/ItemUse")]
public class ItemUseObjective : ObjectiveSO
{
    [Tooltip("사용할 아이템 ID")]
    public int itemId;
    public int requiredCount = 1;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        GameEvents.OnItemUsed += OnUsed;
        _count += GameEvents.RecentCount(GameEvents.KeyUsed(itemId));   // 갭에서 미리 사용한 분 인정
    }
    public override void Deactivate() => GameEvents.OnItemUsed -= OnUsed;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{label} ({_count}/{requiredCount})" : label;

    void OnUsed(int id)
    {
        if (IsInGracePeriod) return;
        if (id != itemId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        requiredCount = Mathf.Max(1, requiredCount);
        if (itemId <= 0)
            Debug.LogWarning($"[ItemUse] '{name}' itemId 미설정", this);
    }
#endif
}
