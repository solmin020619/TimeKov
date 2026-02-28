using System.Collections;
using System.Collections.Generic;
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

    [Header("Player Control")]
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
        if (globalSettings != null && globalSettings.settingsPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                globalSettings.CloseSettings();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (questPanel != null && questPanel.activeSelf)
            {
                return;
            }

            if (!isPaused && (pausePanel == null || !pausePanel.activeSelf))
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pausePanel != null) pausePanel.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null) playerController.enabled = false;
    }

    public void ResumeGame()
    {
        isPaused = false;
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

    public void OnClickSettings()
    {
        if (globalSettings != null)
        {
            globalSettings.OpenSettings();
        }
    }

    public void OnClickQuit()
    {
        Time.timeScale = 1f;
        LoadingData.nextSceneName = mainMenuSceneName;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}