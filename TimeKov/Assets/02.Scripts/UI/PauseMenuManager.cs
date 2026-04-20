using UnityEngine;

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

    void Update() { }

    public void PlayClickSound()
    {
        if (globalSettings != null)
            globalSettings.PlayClickSound();
    }

    public void PauseGame()
    {
        isPaused = true;

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.SetState(UIStateManager.UIState.Pause);
            return;
        }

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
        PlayClickSound();
        ResumeGame();
    }

    public void OnClickSettings()
    {
        PlayClickSound();

        if (UIStateManager.Instance != null)
        {
            UIStateManager.Instance.OpenPauseSettings();
            return;
        }

        if (globalSettings != null)
            globalSettings.OpenSettings();
    }

    public void OnClickQuit()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        CoreUtilities.LoadDirect(mainMenuSceneName);
    }
}