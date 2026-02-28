using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPortal : MonoBehaviour
{
    public string loadingSceneName = "Loading";
    public string targetSceneName = "RaidScene";

    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.0f;

    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isTriggered)
        {
            isTriggered = true;
            StartCoroutine(GoToMapSelect());
        }
    }

    IEnumerator GoToMapSelect()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        LoadingData.nextSceneName = targetSceneName;
        SceneManager.LoadScene(loadingSceneName);
    }
}