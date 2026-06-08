using UnityEngine;

// 건설 모드에 "실제로 진입"하면 완료. (BuildManager.EnterBuildMode 성공 -> GameEvents.OnBuildModeEntered)
// PressKey(B)와 달리 건설 구역 밖에서 B만 눌러(토스트, 진입 안 됨) 깨지는 일이 없다.
[CreateAssetMenu(menuName = "Quest/Objective/EnterBuildMode")]
public class EnterBuildModeObjective : ObjectiveSO
{
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate() { GameEvents.OnBuildModeEntered += OnEntered; }
    public override void Deactivate() => GameEvents.OnBuildModeEntered -= OnEntered;
    public override float Progress => IsCompleted ? 1f : 0f;

    // 퀘스트가 뜨기 전 미리 B를 눌러 이미 건설 모드에 들어와 있으면(진입 이벤트는 지나감)
    // 활성 즉시 완료 처리한다. 안 그러면 "이미 진입"이라 이벤트를 영원히 기다려 깨지지 않는 꼬임 발생.
    protected override bool IsAlreadySatisfied()
    {
        var bm = Object.FindAnyObjectByType<BuildManager>();
        return bm != null && bm.IsBuildMode;
    }

    void OnEntered()
    {
        if (IsInGracePeriod) return;
        Complete();
    }
}
