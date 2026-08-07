using UnityEngine;

// 설비를 F로 해금하면 완료. 퀘스트 뜨기 전에 이미 해금했어도 즉시 완료.
[CreateAssetMenu(menuName = "Quest/Objective/FacilityUnlock")]
public class FacilityUnlockObjective : ObjectiveSO
{
    [Tooltip("해금할 설비 ID. 0이면 아무 설비나 해금 시 카운트.")]
    public int facilityId;
    public int requiredCount = 1;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnFacilityUnlocked += OnUnlocked; }
    public override void Deactivate() => GameEvents.OnFacilityUnlocked -= OnUnlocked;
    public override float Progress => Mathf.Clamp01((float)CurrentCount / Mathf.Max(1, requiredCount));

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({Mathf.Min(CurrentCount, requiredCount)}/{requiredCount})" : label;

    // 이벤트 누적이 아니라 "현재 해금 상태" 기준 (미리 해금해도 인식).
    int CurrentCount
    {
        get
        {
            var m = FacilityUnlockManager.Instance;
            if (m == null) return 0;
            if (facilityId != 0) return m.IsUnlocked(facilityId) ? 1 : 0;
            return m.UnlockedIds.Count;
        }
    }

    protected override bool IsAlreadySatisfied() => CurrentCount >= requiredCount;

    void OnUnlocked(int unlockedId)
    {
        if (facilityId != 0 && unlockedId != facilityId) return;
        ReportProgress(Progress);
        if (CurrentCount >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate() { requiredCount = Mathf.Max(1, requiredCount); }
#endif
}
