using System.Collections;
using UnityEngine;

public class SceneEntry : MonoBehaviour
{
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.0f;

    void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            StartCoroutine(FadeInRoutine());
        }
    }

    IEnumerator FadeInRoutine()
    {
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 1f, 0f, fadeDuration));

        fadeCanvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
}