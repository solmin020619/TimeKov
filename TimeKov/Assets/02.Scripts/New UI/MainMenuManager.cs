using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panel Groups")]
    public GameObject mainButtonGroup;
    public GameObject optionPanel;
    public GameObject quitConfirmPanel;
    public GameObject loadingPanel;

    [Header("Loading Settings")]
    public Slider loadingSlider;
    public Text loadingText;
    public string sceneName = "Base_Scene";

    [Header("Sound Settings")]
    public AudioSource sfxAudioSource;
    public AudioClip clickSound;

    private void Start()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (mainButtonGroup != null) mainButtonGroup.SetActive(true);
    }

    private void Update()
    {
        // ESC 키 기능
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

    // ------------------- 버튼 연결 함수들 -------------------

    public void OnClickNewGame()
    {
        PlayClickSound();
        StartCoroutine(LoadSceneProcess());
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

    IEnumerator LoadSceneProcess()
    {
        loadingPanel.SetActive(true);
        mainButtonGroup.SetActive(false);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float timer = 0.0f;
        while (!op.isDone)
        {
            yield return null;
            timer += Time.deltaTime;

            if (op.progress < 0.9f)
            {
                loadingSlider.value = Mathf.Lerp(loadingSlider.value, op.progress, timer);
                if (op.progress >= loadingSlider.value) timer = 0f;
            }
            else
            {
                loadingSlider.value = Mathf.Lerp(loadingSlider.value, 1f, timer);
                if (loadingSlider.value >= 0.99f)
                {
                    op.allowSceneActivation = true;
                }
            }

            if (loadingText != null)
            {
                // TextMeshPro를 쓴다면 여기를 바꿔야 하지만, 기존 Text라면 그대로 둡니다.
                loadingText.text = ((int)(loadingSlider.value * 100)).ToString() + "%";
            }
        }
    }
}