using UnityEngine;

// 시간에너지 전송률이 목표치(%) 이상 도달하면 완료.
// 전송률은 감소하지 않는 상태값이라 IsAlreadySatisfied 로 갭 안전(FacilityUnlock/CoreUpgrade 와 동일 부류).
//   targetRate 예) 5 = 첫 전송 부트스트랩(시간에너지 합성기 해금 트리거), 100 = 엔딩(탈출) 목표.
public class TransmissionRateObjective : ObjectiveSO
{
    [Tooltip("이 전송률(%) 이상이면 완료.")]
    public int targetRate = 5;

    // 상태 조회형이라 present 즉시 활성(백그라운드). 갭에 미리 전송해도 IsAlreadySatisfied 로 복구.
    public override ActivationTiming Timing => ActivationTiming.OnUIPresented;

    public override void Activate()
    {
        TransmissionManager.OnRateChanged += HandleRate;
    }

    public override void Deactivate()
    {
        TransmissionManager.OnRateChanged -= HandleRate;
    }

    protected override bool IsAlreadySatisfied()
        => TransmissionManager.Instance != null && TransmissionManager.Instance.TransmissionRate >= targetRate;

    private void HandleRate(int rate)
    {
        if (rate >= targetRate) Complete();
    }

    public override float Progress
    {
        get
        {
            if (IsCompleted) return 1f;
            var m = TransmissionManager.Instance;
            if (m == null || targetRate <= 0) return 0f;
            return Mathf.Clamp01((float)m.TransmissionRate / targetRate);
        }
    }
}
