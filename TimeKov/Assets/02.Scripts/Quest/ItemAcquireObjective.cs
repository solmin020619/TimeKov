using UnityEngine;

[CreateAssetMenu(menuName = "Quest/Objective/ItemAcquire")]
public class ItemAcquireObjective : ObjectiveSO
{
    [Tooltip("DataManager 아이템 ID")]
    public int itemId;
    public int requiredCount = 1;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnItemAcquired += OnAcquired; }
    public override void Deactivate() => GameEvents.OnItemAcquired -= OnAcquired;
    public override float Progress => Mathf.Clamp01((float)CurrentCount / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({Mathf.Min(CurrentCount, requiredCount)}/{requiredCount})" : label;

    // 획득 이벤트 누적이 아니라 "지금 인벤에 몇 개 있는지" 기준.
    // 퀘스트 뜨기 전에 미리 모아둬도 인식되고, 중복 카운트도 없다.
    int CurrentCount => CountOwned(itemId);

    protected override bool IsAlreadySatisfied() => CurrentCount >= requiredCount;

    void OnAcquired(int id, int count)
    {
        if (id != itemId) return;
        ReportProgress(Progress);
        if (CurrentCount >= requiredCount) Complete();
    }

    // 가방 + 창고(저장고) 합산 보유량
    static int CountOwned(int itemId)
    {
        int n = 0;
        if (InventoryManager.Instance != null) n += InventoryManager.Instance.GetTotalItemCount(itemId);
        if (InventoryManager.StorageInstance != null) n += InventoryManager.StorageInstance.GetTotalItemCount(itemId);
        return n;
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
