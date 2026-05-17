// =====================================================================
// PlayerSessionData.cs
// 플레이어 세션 데이터 스텁 — GameOverUIManager 참조
// =====================================================================
public class PlayerSessionData
{
    // 싱글톤 인스턴스
    public static PlayerSessionData Instance { get; private set; } = new PlayerSessionData();

    public int score = 0;
    public float survivalTime = 0f;

    // 세션 스냅샷 초기화 — GameOverUIManager 에서 씬 전환 전 호출
    public void ClearSnapshot() { }
}