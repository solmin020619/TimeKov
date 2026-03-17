using System.Collections;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    public string loadingSceneName = "Loading";
    public string firstSceneName = "Base";

    [Header("UI Groups")]
    public GameObject mainButtonGroup;
    public GameObject quitConfirmPanel;

    [Header("Settings Link")]
    public GlobalSettingsManager globalSettingsManager;

    [Header("Fade Effect")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.0f;

    [Header("Sound")]
    public AudioSource sfxAudioSource;
    public AudioClip clickSound;

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (mainButtonGroup != null) mainButtonGroup.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && quitConfirmPanel.activeSelf)
            OnClickQuitNo();
    }

    public void PlayClickSound()
    {
        if (sfxAudioSource != null && clickSound != null)
            sfxAudioSource.PlayOneShot(clickSound);
    }

    public void OnClickNewGame()
    {
        PlayClickSound();
        CoreUtilities.NextSceneName = firstSceneName;
        StartCoroutine(FadeOutAndLoad(loadingSceneName));
    }

    public void OnClickLoadGame()
    {
        PlayClickSound();
        Debug.Log("로드 게임 기능은 아직 구현되지 않았습니다.");
    }

    public void OnClickOption()
    {
        if (globalSettingsManager != null)
            globalSettingsManager.OpenSettings();
        else
            Debug.LogError("GlobalSettingsManager가 연결되지 않았습니다!");
    }

    public void OnClickQuit()
    {
        PlayClickSound();
        quitConfirmPanel.SetActive(true);
    }

    public void OnClickQuitYes()
    {
        PlayClickSound();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void OnClickQuitNo()
    {
        PlayClickSound();
        quitConfirmPanel.SetActive(false);
    }

    IEnumerator FadeOutAndLoad(string targetScene)
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            yield return StartCoroutine(CoreUtilities.Fade(fadeCanvasGroup, 0f, 1f, fadeDuration));
        }

        CoreUtilities.LoadDirect(targetScene);
    }
}