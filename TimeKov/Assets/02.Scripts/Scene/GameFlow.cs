using UnityEngine;


public enum RaidResult
{
    None,
    Success,
    Fail
}
public class GameFlow : MonoBehaviour
{
    // Loading_Scene이 로드 완료 후 이동해야 할 목표 씬 이름
    // SceneLoader가 LoadTo()를 호출할 떄 세팅해준다.
    public static string NextSceneName {  get; private set; }

    // 마지막으로 끝난 레이드의 결과(Base_Scene에서 결과 반영에 사용 가능)
    public static RaidResult LastRaidResult {  get; private set; } = RaidResult.None;

    // 마지막으로 플레이한 레이드 번호
    public static int LastRaidId { get; private set; } = -1;

    // 새 게임 (true)인지 저장된 게임 로드(false)인지
    public static bool IsNewGame { get; private set; } = true;

    // Loading_Scene이 다음에 로드 해야 할 씬을 지정한다.
    public static void SetNextScene(string sceneName)
    {
        NextSceneName = sceneName;
    }

    // Raid 결과 저장
    public static void SetRaidResult(int raidId, RaidResult result)
    {
        LastRaidId = raidId;
        LastRaidResult = result;
    }

    // Raid 결과 초기화
    public static void ClearRaidResult()
    {
        LastRaidId = -1;
        LastRaidResult = RaidResult.None;
    }

    // "세 게임" 버튼을 눌렀을떄 호출
    // 세이브 초기화는 추후 SaveManager가 생기면 여기서 연결
    public static void StartNewGame()
    {
        IsNewGame = true;
        ClearRaidResult();

        // ToDo : Save 초기화
    }

    // "저장된 게임" 버튼을 눌렀을떄 호출
    // 세이브 로드는 추후 saveManager가 생기면 여기서 연결
    public static void StartLoadGame()
    {
        IsNewGame = false;
        ClearRaidResult();

        // ToDo : Save 로드 
    }
}
