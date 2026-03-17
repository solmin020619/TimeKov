using System.Collections;
using UnityEngine;

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
            yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));
        }

        if (PlayerSessionData.Instance != null)
            PlayerSessionData.Instance.CaptureCurrent();

        CoreUtilities.LoadViaLoading(targetSceneName, loadingSceneName);
    }
}