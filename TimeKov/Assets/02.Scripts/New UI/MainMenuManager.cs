using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings (씬 이름 설정)")]
    [Tooltip("로딩 화면으로 쓸 씬의 정확한 이름을 적으세요")]
    public string loadingSceneName = "LoadingScene";

    [Tooltip("게임 시작(New Game)시 넘어갈 맵의 이름을 적으세요")]
    public string nextSceneName = "Base_Scene";

    [Header("Panel Groups")]
    public GameObject mainButtonGroup;
    public GameObject optionPanel;
    public GameObject quitConfirmPanel;

    [Header("Fade Effect")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.0f;

    [Header("Sound Settings")]
    public AudioSource sfxAudioSource;
    public AudioClip clickSound;

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (mainButtonGroup != null) mainButtonGroup.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (quitConfirmPanel.activeSelf) OnClickQuitNo();
            else if (optionPanel.activeSelf) OnClickCloseOption();
        }
    }

    public void PlayClickSound()
    {
        if (sfxAudioSource != null && clickSound != null)
        {
            sfxAudioSource.PlayOneShot(clickSound);
        }
    }

    public void OnClickNewGame()
    {
        PlayClickSound();
        LoadingData.nextSceneName = nextSceneName;
        StartCoroutine(FadeOutAndLoad(loadingSceneName));
    }

    public void OnClickLoadGame()
    {
        PlayClickSound();
        Debug.Log("로드 기능은 추후 구현 예정입니다.");
    }

    public void OnClickOption()
    {
        PlayClickSound();
        optionPanel.SetActive(true);
    }

    public void OnClickCloseOption()
    {
        PlayClickSound();
        optionPanel.SetActive(false);
    }

    public void OnClickQuit()
    {
        PlayClickSound();
        quitConfirmPanel.SetActive(true);
    }

    public void OnClickQuitYes()
    {
        PlayClickSound();
        Debug.Log("게임 종료!");
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

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        SceneManager.LoadScene(targetScene);
    }
}