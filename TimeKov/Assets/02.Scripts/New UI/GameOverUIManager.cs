using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUIManager : MonoBehaviour
{
    public PlayerTime playerTime;
    public GameObject gameOverPanel;
    public CanvasGroup gameOverCanvasGroup;

    public CanvasGroup screenBlur;

    public AudioSource deathAudio;
    public float flatlineStartTime = 6.8f;

    public float effectStartTime = 8f;

    public float fadeDuration = 1f;
    public float returnDelay = 3f;

    private bool isDead = false;

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverCanvasGroup != null) gameOverCanvasGroup.alpha = 0f;

        if (screenBlur != null)
        {
            screenBlur.alpha = 0f;
            screenBlur.gameObject.SetActive(false);
        }

        if (playerTime != null) playerTime.onTimeDepleted += TriggerGameOver;
    }

    void Update()
    {
        if (isDead) return;
        if (playerTime == null) return;

        float current = playerTime.currentTime;
        float targetAlpha = 0f;

        if (current <= effectStartTime && current > 0)
        {
            targetAlpha = 1f - (current / effectStartTime);

            if (screenBlur != null && !screenBlur.gameObject.activeSelf)
            {
                screenBlur.gameObject.SetActive(true);
            }

            if (deathAudio != null)
            {
                if (!deathAudio.isPlaying)
                {
                    deathAudio.Play();
                }

                if (deathAudio.time >= flatlineStartTime)
                {
                    deathAudio.time = 0f;
                }

                deathAudio.volume = targetAlpha;
            }
        }
        else
        {
            if (deathAudio != null && deathAudio.isPlaying)
            {
                deathAudio.Stop();
            }
        }

        if (screenBlur != null && screenBlur.gameObject.activeSelf)
        {
            screenBlur.alpha = Mathf.Lerp(screenBlur.alpha, targetAlpha, Time.deltaTime * 3f);

            if (targetAlpha <= 0f && screenBlur.alpha < 0.01f)
            {
                screenBlur.alpha = 0f;
                screenBlur.gameObject.SetActive(false);
            }
        }
    }

    void TriggerGameOver()
    {
        if (isDead) return;
        isDead = true;

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        Time.timeScale = 0.2f;

        float startBlur = 0f;
        if (screenBlur != null)
        {
            if (!screenBlur.gameObject.activeSelf) screenBlur.gameObject.SetActive(true);
            startBlur = screenBlur.alpha;
        }

        float currentFadeDuration = startBlur < 0.1f ? 3f : 1.5f;

        if (startBlur < 0.1f && deathAudio != null)
        {
            deathAudio.time = 0f;
            if (!deathAudio.isPlaying) deathAudio.Play();
            deathAudio.volume = 1f;

            yield return new WaitForSecondsRealtime(1.5f);
        }

        if (deathAudio != null)
        {
            deathAudio.time = flatlineStartTime;
            if (!deathAudio.isPlaying) deathAudio.Play();
            deathAudio.volume = 1f;
        }

        float t = 0f;
        while (t < currentFadeDuration)
        {
            t += Time.unscaledDeltaTime;

            if (screenBlur != null) screenBlur.alpha = Mathf.Lerp(startBlur, 1f, t / currentFadeDuration);

            yield return null;
        }

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        float fadeT = 0f;
        while (fadeT < fadeDuration)
        {
            fadeT += Time.unscaledDeltaTime;
            if (gameOverCanvasGroup != null)
            {
                gameOverCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeT / fadeDuration);
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(returnDelay);

        Time.timeScale = 1f;
        SceneManager.LoadScene("Base");
    }

    void OnDestroy()
    {
        if (playerTime != null)
        {
            playerTime.onTimeDepleted -= TriggerGameOver;
        }
    }
}