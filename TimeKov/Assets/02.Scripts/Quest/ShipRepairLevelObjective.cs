using UnityEngine;

// 우주선 수리 레벨이 목표 이상 도달하면 완료.
// 레벨은 감소하지 않는 상태값이라 IsAlreadySatisfied 로 갭 안전(TransmissionRate/CoreUpgrade 와 동일 부류).
//   targetLevel 예) 3 = 1차 수리(전송 25% 선체 보강재 필요), 5 = 완전 수리(탈출 조건).
public class ShipRepairLevelObjective : ObjectiveSO
{
    [Tooltip("우주선 수리가 이 레벨 이상이면 완료.")]
    public int targetLevel = 2;

    // 상태 조회형이라 present 즉시 활성(백그라운드). 갭에 미리 수리해도 IsAlreadySatisfied 로 복구.
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()   => ShipRepairManager.OnChanged += HandleChanged;
    public override void Deactivate() => ShipRepairManager.OnChanged -= HandleChanged;

    protected override bool IsAlreadySatisfied()
        => ShipRepairManager.Instance != null && ShipRepairManager.Instance.CurrentLevel >= targetLevel;

    private void HandleChanged()
    {
        if (ShipRepairManager.Instance != null && ShipRepairManager.Instance.CurrentLevel >= targetLevel)
            Complete();
    }

    public override float Progress
    {
        get
        {
            if (IsCompleted) return 1f;
            var m = ShipRepairManager.Instance;
            if (m == null || targetLevel <= 0) return 0f;
            return Mathf.Clamp01((float)m.CurrentLevel / targetLevel);
        }
    }
}
