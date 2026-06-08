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

    void OnEntered()
    {
        if (IsInGracePeriod) return;
        Complete();
    }
}
