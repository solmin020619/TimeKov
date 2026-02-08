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
        fadeCanvasGroup.alpha = 1f;
        float fadeTimer = 0f;

        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;

        AsyncOperation op = SceneManager.LoadSceneAsync(LoadingData.nextSceneName);
        op.allowSceneActivation = false;

        float targetValue = 0f;

        while (!op.isDone)
        {
            yield return null;

            if (op.progress < 0.9f)
            {
                targetValue = op.progress;
            }
            else
            {
                targetValue = 1.0f;
            }

            loadingSlider.value = Mathf.MoveTowards(loadingSlider.value, targetValue, Time.deltaTime * loadingSpeed);

            if (loadingText != null)
            {
                loadingText.text = ((int)(loadingSlider.value * 100)).ToString() + "%";
            }

            if (op.progress >= 0.9f && loadingSlider.value >= 0.99f)
            {
                loadingSlider.value = 1.0f;
                if (loadingText != null) loadingText.text = "100%";
                break;
            }
        }

        yield return new WaitForSeconds(0.5f);


        fadeTimer = 0f;
        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeTimer / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        op.allowSceneActivation = true;
    }
}