using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Objects")]
    public GameObject pausePanel;
    public GlobalSettingsManager globalSettings;
    public GameObject questPanel;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";

    private PlayerController playerController;
    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;

        playerController = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        // ❌ ESC 처리 금지 (UIStateManager가 총괄)
    }

    public void PauseGame()
    {
        isPaused = true;

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.Pause);
            return;
        }

        // (예외) UIStateManager 없는 씬 호환
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (playerController != null) playerController.enabled = false;
    }

    public void ResumeGame()
    {
        isPaused = false;

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.CloseAllUI();
            return;
        }

        // (예외) UIStateManager 없는 씬 호환
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.SyncSettings();
        }
    }

    public void OnClickResume()
    {
        ResumeGame();
    }

    // ✅✅ 여기만 바뀐 핵심: Settings 버튼은 UIStateManager로 위임
    public void OnClickSettings()
    {
        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.OpenPauseSettings();
            return;
        }

        // (예외) UIStateManager 없는 씬 호환
        if (globalSettings != null)
            globalSettings.OpenSettings();
    }

    public void OnClickQuit()
    {
        Time.timeScale = 1f;
        LoadingData.nextSceneName = mainMenuSceneName;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}