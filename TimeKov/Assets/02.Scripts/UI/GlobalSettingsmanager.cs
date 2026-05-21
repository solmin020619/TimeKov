// =====================================================================
// GlobalSettingsManager.cs
// 설정창 콘텐츠 관리 — 해상도, 전체화면, 볼륨, 감도
// ESC / X 버튼 → GameUIController.CloseSettings() 로 닫힘
// 게임 종료 버튼도 이 스크립트에서 처리
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalSettingsManager : MonoBehaviour
{
    public static event System.Action<float> OnBGMVolumeChanged;
    public static event System.Action<float> OnSFXVolumeChanged;
    public static event System.Action<float> OnSensitivityChanged;

    [Header("UI Components")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensitivitySlider;

    [Header("Sound")]
    public AudioClip clickSound;
    public AudioSource uiSFXSpeaker;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    private List<Resolution> _resolutions = new List<Resolution>();

    // ── 초기화 ───────────────────────────────────────────────────────

    void Start()
    {
        LoadAndApplySettings();
        InitResolutionOptions();
        SyncUIValues();

        if (bgmSlider != null)          bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null)          sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (sensitivitySlider != null)  sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (fullscreenToggle != null)   fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    // ── 설정창 열기 / 닫기 ───────────────────────────────────────────

    public void OpenSettings()
    {
        PlayClickSound();
        SyncUIValues();
        GameUIController.Instance?.OpenSettings();
    }

    public void CloseSettings()
    {
        PlayClickSound();
        GameUIController.Instance?.CloseSettings();
    }

    public void ToggleSettings()
    {
        var ui = GameUIController.Instance;
        if (ui == null) return;

        if (ui.GetCurrentState() == GameUIController.UIState.Settings)
            CloseSettings();
        else
            OpenSettings();
    }

    // ── 게임 종료 (메인 메뉴로) ──────────────────────────────────────

    /// <summary>설정창 내 '게임 종료' 버튼에 연결</summary>
    public void QuitToMainMenu()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        CoreUtilities.LoadDirect(mainMenuSceneName);
    }

    // ── 클릭 사운드 ──────────────────────────────────────────────────

    public void PlayClickSound()
    {
        if (uiSFXSpeaker != null && clickSound != null)
            uiSFXSpeaker.PlayOneShot(clickSound);
    }

    // ── 설정값 적용 ──────────────────────────────────────────────────

    void LoadAndApplySettings()
    {
        SetBGMVolume(PlayerPrefs.GetFloat("BGMVolume", 1f));
        SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume", 1f));
        SetSensitivity(PlayerPrefs.GetFloat("MouseSensitivity", 1f));
    }

    void SyncUIValues()
    {
        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("BGMVolume", 1f));
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("SFXVolume", 1f));
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("MouseSensitivity", 1f));
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
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
        OnSensitivityChanged?.Invoke(sens);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetResolution(int index)
    {
        if (index >= 0 && index < _resolutions.Count)
        {
            Resolution res = _resolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }
    }

    // ── 해상도 드롭다운 초기화 ────────────────────────────────────────

    void InitResolutionOptions()
    {
        if (resolutionDropdown == null) return;

        Resolution[] allRes = Screen.resolutions;
        _resolutions.Clear();
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        var seen = new HashSet<string>();
        int currentIndex = 0;

        foreach (var r in allRes)
        {
            if (r.width < 1280 || r.height < 720) continue;
            string label = $"{r.width} x {r.height}";
            if (!seen.Add(label)) continue;

            options.Add(label);
            _resolutions.Add(r);
            if (r.width == Screen.width && r.height == Screen.height)
                currentIndex = _resolutions.Count - 1;
        }

        if (options.Count == 0)
        {
            options.Add($"{Screen.width} x {Screen.height}");
            _resolutions.Add(Screen.currentResolution);
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();
    }
}
