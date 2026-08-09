using UnityEngine;

/// <summary>
/// 안내/설명 전용 Objective. 액션 없이 라벨 텍스트만 표시.
/// Activate 시점에 즉시 완료 처리되지만 ObjectiveLine은 collapse 안 함 (체크박스만 표시 + 라벨 유지).
/// 한 Quest 안에 다른 Objective와 같이 두고 안내 문구로 사용.
/// </summary>
[CreateAssetMenu(menuName = "TIMEKOV/퀘스트/목표/안내 문구")]
public class InfoMessageObjective : ObjectiveSO
{
    public override ActivationTiming Timing => ActivationTiming.OnUIActivated;

    public override void Activate() { Complete(); }
    public override void Deactivate() { }
    public override float Progress => 1f;
}
