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