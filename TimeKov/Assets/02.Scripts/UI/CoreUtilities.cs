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
        SaveSlotManager.Instance?.SaveActive();
        Time.timeScale = 1f;   // UI가 timeScale을 멈춰뒀을 수 있으니 로드 전 정상화
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