using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class SettingManager : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Sound Settings (Sliders)")]
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("Audio Sources (Speakers)")]
    public AudioSource bgmSpeaker;
    public AudioSource sfxSpeaker;

    [Header("Control Settings")]
    public Slider sensitivitySlider;

    List<Resolution> resolutions = new List<Resolution>();

    void Start()
    {
        // 1. 해상도 & 토글 설정 (기존과 동일)
        Resolution[] allResolutions = Screen.resolutions;
        resolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        HashSet<string> uniqueResolutions = new HashSet<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < allResolutions.Length; i++)
        {
            string option = allResolutions[i].width + " x " + allResolutions[i].height;
            if (!uniqueResolutions.Contains(option))
            {
                uniqueResolutions.Add(option);
                options.Add(option);
                resolutions.Add(allResolutions[i]);
                if (allResolutions[i].width == Screen.width && allResolutions[i].height == Screen.height)
                    currentResolutionIndex = resolutions.Count - 1;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        // 2. 사운드 슬라이더 연결
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            // 시작할 때 슬라이더 위치를 현재 스피커 볼륨에 맞춤
            if (bgmSpeaker != null) bgmSlider.value = bgmSpeaker.volume;
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            if (sfxSpeaker != null) sfxSlider.value = sfxSpeaker.volume;
        }

        // 3. 마우스 감도 연결
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        }

        fullscreenToggle.isOn = Screen.fullScreen;
    }

    // --- 기능 함수들 ---

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }

    public void SetBGMVolume(float volume)
    {
        if (bgmSpeaker != null)
        {
            bgmSpeaker.volume = volume;
        }
    }
    public void SetSFXVolume(float volume)
    {
        if (sfxSpeaker != null)
        {
            sfxSpeaker.volume = volume;
        }
    }

    public void SetSensitivity(float sens)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sens);
        PlayerPrefs.Save();
    }

    // --- 그래픽 품질 ---
    public void SetQualityLow(bool isOn) { if (isOn) QualitySettings.SetQualityLevel(0); }
    public void SetQualityMedium(bool isOn) { if (isOn) QualitySettings.SetQualityLevel(1); }
    public void SetQualityHigh(bool isOn) { if (isOn) QualitySettings.SetQualityLevel(2); }
}