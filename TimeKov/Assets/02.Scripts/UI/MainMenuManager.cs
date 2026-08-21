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
        // ★커서를 반드시 풀어 둔다. Cursor.lockState / visible 은 씬이 바뀌어도 그대로 남는
        //   엔진 전역값이라, 게임에서 메인메뉴로 나오면 인게임에서 걸어 둔 Locked 가 따라온다.
        //   그러면 메뉴에서 마우스가 안 보이고, ESC 로 잠금이 잠깐 풀렸다가 화면을 클릭하는
        //   순간 유니티가 다시 잠가 버린다(누른 적 없는데 커서가 사라지는 것처럼 보인다).
        //   메인메뉴는 마우스로만 조작하므로 이 씬이 커서 상태의 주인이 되어야 한다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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