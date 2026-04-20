using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalSettingsManager : MonoBehaviour
{
    public static event System.Action<float> OnBGMVolumeChanged;
    public static event System.Action<float> OnSFXVolumeChanged;

    [Header("UI Components")]
    public GameObject settingsPanel;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensitivitySlider;
    public GameObject pauseMenuPanel;

    [Header("Effect")]
    public AudioClip clickSound;
    public AudioSource uiSFXSpeaker;

    List<Resolution> resolutions = new List<Resolution>();

    void Start()
    {
        LoadAndApplySettings();
        InitResolutionOptions();

        SyncUIValues();

        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            PlayClickSound();
            settingsPanel.SetActive(true);
            SyncUIValues();

            if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
            {
                pauseMenuPanel.SetActive(false);
            }
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            PlayClickSound();
            settingsPanel.SetActive(false);

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                if (pauseMenuPanel != null)
                {
                    pauseMenuPanel.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    Time.timeScale = 1f;
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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

    public void PlayClickSound()
    {
        if (uiSFXSpeaker != null && clickSound != null)
            uiSFXSpeaker.PlayOneShot(clickSound);
    }

    void LoadAndApplySettings()
    {
        float bgm = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        SetBGMVolume(bgm);
        SetSFXVolume(sfx);
    }

    void SyncUIValues()
    {
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 1.0f));
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 1.0f));
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensitivity", 1.0f));
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    public void SetBGMVolume(float volume)
    {
        PlayerPrefs.SetFloat("BGMVolume", volume);
        PlayerPrefs.Save();
        OnBGMVolumeChanged?.Invoke(volume);
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
        OnSFXVolumeChanged?.Invoke(volume);
    }

    public void SetSensitivity(float sens)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sens);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

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
        Resolution[] allRes = Screen.resolutions;
        resolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        HashSet<string> uniqueRes = new HashSet<string>();
        int currentResIndex = 0;

        for (int i = 0; i < allRes.Length; i++)
        {
            if (allRes[i].width < 1280 || allRes[i].height < 720) continue;
            string option = allRes[i].width + " x " + allRes[i].height;
            if (!uniqueRes.Contains(option))
            {
                uniqueRes.Add(option);
                options.Add(option);
                resolutions.Add(allRes[i]);
                if (allRes[i].width == Screen.width && allRes[i].height == Screen.height)
                    currentResIndex = resolutions.Count - 1;
            }
        }
        if (options.Count == 0)
        {
            options.Add(Screen.width + " x " + Screen.height);
            resolutions.Add(Screen.currentResolution);
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();
    }
}