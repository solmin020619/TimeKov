using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUIManager : MonoBehaviour
{
    public GameObject gameOverPanel;
    public CanvasGroup gameOverCanvasGroup;
    public CanvasGroup screenBlur;

    public AudioSource deathAudio;
    public float flatlineStartTime = 6.8f;

    [Header("HP 기반 블러 시작 비율 (0~1, 예: 0.3 = HP 30% 이하)")]
    public float blurStartHpRatio = 0.3f;

    public float fadeDuration = 1f;
    public float returnDelay = 3f;

    private Player _player;
    private bool isDead = false;

    // RespawnManager가 씬에 있으면 GameOverUIManager는 동작하지 않음
    // (RespawnManager가 사망 처리를 담당하는 씬에서 충돌 방지)
    private bool _disabled = false;

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverCanvasGroup != null) gameOverCanvasGroup.alpha = 0f;

        if (screenBlur != null)
        {
            screenBlur.alpha = 0f;
            screenBlur.gameObject.SetActive(false);
        }

        // RespawnManager가 있으면 이 컴포넌트는 비활성화
        if (FindAnyObjectByType<RespawnManager>() != null)
        {
            _disabled = true;
            return;
        }

        _player = FindFirstObjectByType<Player>();
        if (_player != null) _player.Stat.OnDead += TriggerGameOver;
    }

    void Update()
    {
        if (_disabled || isDead || _player == null) return;

        float hpRatio = _player.Stat.CurrentHp / _player.Stat.MaxHp;
        float targetAlpha = 0f;

        // hpRatio > 0f 조건 제거: HP = 0이 되는 순간에도 블러가 꺼지지 않고 유지
        // (이전에는 HP = 0 되는 순간 targetAlpha = 0 → 블러가 깜빡이며 꺼지는 버그 존재)
        if (hpRatio <= blurStartHpRatio)
        {
            targetAlpha = 1f - (hpRatio / blurStartHpRatio);
            targetAlpha = Mathf.Clamp01(targetAlpha);

            if (screenBlur != null && !screenBlur.gameObject.activeSelf)
                screenBlur.gameObject.SetActive(true);

            if (deathAudio != null && hpRatio > 0f)
            {
                if (!deathAudio.isPlaying) deathAudio.Play();

                if (deathAudio.time >= flatlineStartTime)
                    deathAudio.time = 0f;

                deathAudio.volume = targetAlpha;
            }
        }
        else
        {
            if (deathAudio != null && deathAudio.isPlaying)
                deathAudio.Stop();
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
        if (_disabled || isDead) return;
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
            if (screenBlur != null)
                screenBlur.alpha = Mathf.Lerp(startBlur, 1f, t / currentFadeDuration);
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
                gameOverCanvasGroup.alpha = Mathf.Lerp(0f, 1f, fadeT / fadeDuration);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(returnDelay);

        Time.timeScale = 1f;
        SceneManager.LoadScene("Base");
    }

    void OnDestroy()
    {
        if (!_disabled && _player != null)
            _player.Stat.OnDead -= TriggerGameOver;
    }
}