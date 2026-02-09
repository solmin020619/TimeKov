using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GlobalSettingsManager : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject settingsPanel;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensitivitySlider;

    [Header("Scene Audio")]
    public AudioSource sceneBGMSpeaker;
    public AudioSource sceneSFXSpeaker;

    [Header("Effect")]
    public AudioClip clickSound;

    List<Resolution> resolutions = new List<Resolution>();

    void Start()
    {
        LoadAndApplySettings();
        InitResolutionOptions();

        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    void Update()
    {
        // 1. 현재 씬 이름 알아내기
        string currentScene = SceneManager.GetActiveScene().name;

        // 2. 메인 메뉴("MainScene")거나 맵 선택창("MapSelectScene")이면 ESC 키 무시
        // (메인 메뉴는 클릭으로 열고, 맵 선택창은 별도 로직이 있으므로)
        if (currentScene == "MainMenu" || currentScene == "RaidSelect")
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettings();
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            PlayClickSound();
            settingsPanel.SetActive(true);
            SyncUIValues();

        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            PlayClickSound();
            settingsPanel.SetActive(false);
        }
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null) return;

        bool isActive = settingsPanel.activeSelf;
        if (isActive)
            CloseSettings();
        else
            OpenSettings();
    }

    void PlayClickSound()
    {
        if (sceneSFXSpeaker != null && clickSound != null)
            sceneSFXSpeaker.PlayOneShot(clickSound);
    }

    void LoadAndApplySettings()
    {
        float bgm = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        if (sceneBGMSpeaker != null) sceneBGMSpeaker.volume = bgm;
        if (sceneSFXSpeaker != null) sceneSFXSpeaker.volume = sfx;
    }

    void SyncUIValues()
    {
        if (bgmSlider != null) bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;
    }

    public void SetBGMVolume(float volume)
    {
        if (sceneBGMSpeaker != null) sceneBGMSpeaker.volume = volume;
        PlayerPrefs.SetFloat("BGMVolume", volume); PlayerPrefs.Save();
    }
    public void SetSFXVolume(float volume)
    {
        if (sceneSFXSpeaker != null) sceneSFXSpeaker.volume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume); PlayerPrefs.Save();
    }
    public void SetSensitivity(float sens)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sens); PlayerPrefs.Save();
    }
    public void SetFullscreen(bool isFullscreen) { Screen.fullScreen = isFullscreen; }
    public void SetResolution(int index)
    {
        if (index >= 0 && index < resolutions.Count)
        {
            Resolution res = resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }
    void InitResolutionOptions()
    {
        if (resolutionDropdown == null) return;
        Resolution[] allRes = Screen.resolutions; resolutions.Clear(); resolutionDropdown.ClearOptions();
        List<string> options = new List<string>(); HashSet<string> uniqueRes = new HashSet<string>(); int currentResIndex = 0;
        for (int i = 0; i < allRes.Length; i++)
        {
            if (allRes[i].width < 1280 || allRes[i].height < 720) continue;
            string option = allRes[i].width + " x " + allRes[i].height;
            if (!uniqueRes.Contains(option))
            {
                uniqueRes.Add(option); options.Add(option); resolutions.Add(allRes[i]);
                if (allRes[i].width == Screen.width && allRes[i].height == Screen.height) currentResIndex = resolutions.Count - 1;
            }
        }
        if (options.Count == 0) { options.Add(Screen.width + " x " + Screen.height); resolutions.Add(Screen.currentResolution); }
        resolutionDropdown.AddOptions(options); resolutionDropdown.value = currentResIndex; resolutionDropdown.RefreshShownValue();
    }
}