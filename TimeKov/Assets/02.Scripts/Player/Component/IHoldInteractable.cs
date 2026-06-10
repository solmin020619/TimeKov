// 꾹 눌러(홀드) 완료하거나, 즉시완료(비용 소모)할 수 있는 상호작용.
// PlayerInteractComponent가 가장 가까운 대상에 대해 홀드/즉시완료를 구동한다.
// HoldDuration이 0이면 일반 IInteractable처럼 즉시(F 누른 순간) Interact가 호출된다.
public interface IHoldInteractable : IInteractable
{
    // 홀드 완료까지 걸리는 시간(초)
    float HoldDuration { get; }

    // 즉시완료 가능 여부 (예: HP가 비용보다 충분한지)
    bool CanInstantComplete(Player player);

    // 홀드 진행도(0~1) 갱신 — 진행 UI 표시용
    void OnHoldProgress(float t01);

    // 홀드 완료 (대기 후 정상 완료)
    void OnHoldComplete(Player player);

    // 홀드 취소 (범위 밖으로 나가거나 키를 뗌)
    void OnHoldCancel();

    // 즉시완료 (비용 소모하고 바로 완료)
    void OnInstantComplete(Player player);
}
