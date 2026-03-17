using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingSceneManager : MonoBehaviour
{
    [Header("UI Components")]
    public Slider loadingSlider;
    public TextMeshProUGUI loadingText;
    public CanvasGroup fadeCanvasGroup;

    [Header("Settings")]
    public float fadeDuration = 0.5f;
    public float loadingSpeed = 1.0f;

    private void Start()
    {
        loadingSlider.value = 0f;
        if (loadingText != null) loadingText.text = "0%";

        StartCoroutine(LoadProcess());
    }

    IEnumerator LoadProcess()
    {
        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 1f, 0f, fadeDuration));

        AsyncOperation op = SceneManager.LoadSceneAsync(CoreUtilities.NextSceneName);
        op.allowSceneActivation = false;

        float targetValue = 0f;

        while (!op.isDone)
        {
            yield return null;

            targetValue = op.progress < 0.9f ? op.progress : 1.0f;

            loadingSlider.value = Mathf.MoveTowards(loadingSlider.value, targetValue, Time.deltaTime * loadingSpeed);

            if (loadingText != null)
                loadingText.text = ((int)(loadingSlider.value * 100)) + "%";

            if (op.progress >= 0.9f && loadingSlider.value >= 0.99f)
            {
                loadingSlider.value = 1.0f;
                if (loadingText != null) loadingText.text = "100%";
                break;
            }
        }

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));

        op.allowSceneActivation = true;
    }
}