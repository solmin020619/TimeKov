using System;
using UnityEngine;

// 레일로 설비(포트-포트)를 연결하면 완료. (RailBuildManager.CompleteRoute -> GameEvents.OnRailConnected)
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/레일 연결")]
public class RailConnectObjective : ObjectiveSO
{
    public int requiredCount = 1;
    [Tooltip("이 설비의 '출력'에서 시작한 연결만 인정. 0이면 아무 설비나. (방향 판정 - 반대로 이으면 미인정)")]
    public int sourceFacilityId;
    [Tooltip("이 설비의 '입력'으로 끝난 연결만 인정. 0이면 아무 설비나.")]
    public int targetFacilityId;

    [NonSerialized] int _count;

    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    // 방향 지정이면 그 방향 키로, 아니면 any 레일 키로 갭-인정.
    string LookbackKey => (sourceFacilityId == 0 && targetFacilityId == 0)
        ? GameEvents.KeyRail
        : GameEvents.KeyRailDir(sourceFacilityId, targetFacilityId);

    public override void Activate()
    {
        GameEvents.OnRailConnected += OnConnected;
        _count += GameEvents.RecentCount(LookbackKey);   // 갭에서 미리 이은(방향 일치) 레일 인정
    }
    public override void Deactivate() => GameEvents.OnRailConnected -= OnConnected;
    public override float Progress => Mathf.Clamp01((float)_count / Mathf.Max(1, requiredCount));
    protected override bool IsAlreadySatisfied() => _count >= requiredCount;

    public override string GetDisplayLabel()
        => requiredCount > 1 ? $"{Loc.Get(label)} ({_count}/{requiredCount})" : label;

    void OnConnected(int src, int tgt)
    {
        if (IsInGracePeriod) return;
        // 방향 필터: 지정된 source(출력)/target(입력)과 일치해야 인정. 반대로 이으면 무시. 0은 아무 설비나.
        if (sourceFacilityId != 0 && src != sourceFacilityId) return;
        if (targetFacilityId != 0 && tgt != targetFacilityId) return;
        _count++;
        ReportProgress(Progress);
        if (_count >= requiredCount) Complete();
    }

#if UNITY_EDITOR
    void OnValidate() { requiredCount = Mathf.Max(1, requiredCount); }
#endif
}
