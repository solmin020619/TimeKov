using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Objects")]
    public GameObject pausePanel; 
    public GlobalSettingsManager globalSettings;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenu";

    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);

        Time.timeScale = 1f;
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
            if (isPaused)
            {
                ResumeGame();
            }
            else
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
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
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
        LoadingData.nextSceneName = mainMenuSceneName; // (로딩바 쓰고 싶으면 사용)
        SceneManager.LoadScene(mainMenuSceneName); // 바로 이동 or 로딩씬 경유
    }
}