using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoreUtilities
{
    public static string NextSceneName = "World";
    public const string DefaultLoadingScene = "Loading";

    public static void LoadDirect(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // 메인 메뉴로 나가기 = 현재 진행을 먼저 저장하고 씬 전환.
    //   설정메뉴 '메인메뉴' 버튼과 우주선 탈출(엔딩)이 공통으로 쓴다 = 나가는 길은 항상 저장부터.
    //   ★씬 전환은 OnApplicationQuit(앱 종료)이 아니라 자동저장(30s)만 걸리므로, 여기서 명시 저장 안 하면
    //     마지막 자동저장 이후 진행분이 유실된다. SaveActive()는 동기+원자적 기록이라 로드 전에 확실히 끝난다.
    public static void SaveAndLoadMainMenu(string mainMenuScene)
    {
        // 저장 '전에' 벨트 위 아이템을 창고로 회수한다. 벨트 점유는 저장 항목에 없고,
        // 씬이 내려갈 때(OnDisable) 자동 회수되는 건 저장이 끝난 뒤라 그대로 두면 증발한다.
        TIMEKOV.Factory.BeltSegment.RescueAllToStorage();

        SaveSlotManager.Instance?.SaveActive();
        Time.timeScale = 1f;   // UI가 timeScale을 멈춰뒀을 수 있으니 로드 전 정상화
        AudioListener.pause = false;   // 같은 이유 — 정지 중 나가면 다음 씬이 통째로 무음이 된다(전역 static)

        // 월드의 소리는 월드에 두고 간다. BattleBgm/GameSfx 는 DDOL 이라 씬과 함께 죽지 않아서,
        // 안 끊으면 전투 BGM 루프나 전투 종료 임팩트(샤악)가 메인메뉴까지 이어서 들린다.
        // 순서 중요: 트래커를 먼저 비워야 씬 언로드의 몬스터 Disengage 연쇄가
        // 마지막에 End() -> 종료 임팩트를 쏘는 일이 없다.
        BattleMusicTracker.ResetForSceneExit();
        BattleBgm.ResetForSceneExit();
        GameSfx.CutAll();

        // 씬 소속 3D 사운드(사망 비명, 설비 루프 등)도 정지. 설정창이 AudioListener.pause 로
        // 얼려뒀던 소리가 위 pause=false 에 풀리면서 전환 프레임에 이어 울리는 것 방지.
        // UI 소리(ignoreListenerPause)는 건드리지 않는다.
        foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (s != null && !s.ignoreListenerPause) s.Stop();

        // 재입장 대비: 인게임 UI(설정창 등)가 꺼둔 입력 게이팅 static 복구.
        //   안 하면 다음 프롤로그엔 GameUIController가 없어 stale하게 남아 마우스룩/입력이 잠긴다.
        GameUIController.ResetInputGatingForSceneExit();
        LoadDirect(mainMenuScene);
    }

    public static void LoadViaLoading(string targetScene, string loadingScene = DefaultLoadingScene)
    {
        NextSceneName = targetScene;
        SceneManager.LoadScene(loadingScene);
    }

    public static IEnumerator Fade(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float timer = 0f;
        canvasGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, timer / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    public static IEnumerator FadeUnscaled(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float timer = 0f;
        canvasGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, timer / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}