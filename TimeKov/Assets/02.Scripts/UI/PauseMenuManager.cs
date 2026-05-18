using UnityEngine;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Objects")]
    public GameObject pausePanel;
    public GlobalSettingsManager globalSettings;
    public GameObject questPanel;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";

    private Player _player;

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        _player = FindFirstObjectByType<Player>();
    }

    public void PlayClickSound()
    {
        globalSettings?.PlayClickSound();
    }

    public void PauseGame()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _player?.Movement.LockMovement(true);
    }

    public void ResumeGame()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _player?.Movement.LockMovement(false);
    }

    public void OnClickResume()
    {
        PlayClickSound();
        ResumeGame();
    }

    public void OnClickSettings()
    {
        PlayClickSound();
        globalSettings?.OpenSettings();
    }

    public void OnClickQuit()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        CoreUtilities.LoadDirect(mainMenuSceneName);
    }
}