/// <summary>
/// 씬 로드 + 모든 Start() 완료 후 SaveSlotManager가 1회 발화하는 훅.
/// 로드된 세이브 데이터 기반으로 "시작 상태"를 보정해야 하는 시스템(사망 상태 복구 등)이 구현한다.
/// Awake()에서 SaveSlotManager.Instance?.RegisterListener(this), OnDestroy()에서 UnregisterListener(this).
/// </summary>
public interface ISaveLoadListener
{
    void OnAfterLoad();
}
