// =====================================================================
// ISaveable.cs
// 통합 세이브 시스템에 참여하는 시스템이 구현하는 인터페이스.
//
// Capture: SaveSlotManager.SaveActive() 호출 시, 등록된 모든 ISaveable의
//          Capture(data)를 차례로 호출해 자신의 상태를 GameSaveData에 적어넣는다.
//
// Restore는 이 인터페이스에 없음 — DataBoot/GameDataHolder와 동일한 "끌어오기" 패턴으로,
// 각 시스템이 자기 Awake/Start에서 SaveSlotManager.Instance.Data를 직접 읽어 스스로 복원한다.
// (SaveSlotManager가 모든 시스템의 복원 순서/타이밍을 알 필요가 없어지므로 결합도가 낮아진다.)
// =====================================================================

public interface ISaveable
{
    void Capture(GameSaveData data);
}
