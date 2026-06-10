// G 키로 "즉시완료"(비용 소모)할 수 있는 상호작용. 기본 F 상호작용(IInteractable)과 병행.
// 예: 파밍 상자 — F로 걸어두고 기다리거나, G로 HP(시간) 소모해 즉시 완료.
public interface IInstantInteractable : IInteractable
{
    // 즉시완료 가능 여부 (예: HP가 비용보다 충분한지, 아직 안 열린 상태인지)
    bool CanInstantComplete(Player player);

    // 즉시완료 실행 (비용 소모하고 바로 완료)
    void OnInstantComplete(Player player);
}
