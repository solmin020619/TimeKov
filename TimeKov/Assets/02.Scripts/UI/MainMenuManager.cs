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

    // 메뉴 클릭음은 GameSfx(SfxId.MenuClick)로 통합 — GameSfxConfig 에서 관리.

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
        // ★널 검사가 없으면 ESC 를 누를 때마다 NullReferenceException 이 난다.
        //   현재 MainMenu 씬의 GameManager 는 이 컴포넌트의 참조가 전부 비어 있어서
        //   (메뉴 항목들이 TitleManager / SettingsPanel / CreditsPanel 을 직접 부르도록
        //   바뀐 뒤 남은 잔재), 월드 선택·제작진·설정을 ESC 로 닫을 때마다 콘솔이 도배됐다.
        //   Start() 쪽은 이미 널 검사가 있어서 여기만 새어 있었다.
        if (quitConfirmPanel == null) return;
        if (Input.GetKeyDown(KeyCode.Escape) && quitConfirmPanel.activeSelf)
            OnClickQuitNo();
    }

    public void PlayClickSound() => GameSfx.Play(SfxId.MenuClick);

    public void OnClickNewGame()
    {
        PlayClickSound();
        CoreUtilities.NextSceneName = firstSceneName;
        StartCoroutine(FadeOutAndLoad(loadingSceneName));
    }

    public void OnClickLoadGame()
    {
        PlayClickSound();
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