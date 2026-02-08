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
        Resolution[] allResolutions = Screen.resolutions;
        resolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        HashSet<string> uniqueResolutions = new HashSet<string>(); // 중복 제거용 (60Hz, 144Hz 등 같은 해상도 묶기)
        int currentResolutionIndex = 0;

        for (int i = 0; i < allResolutions.Length; i++)
        {
            if (allResolutions[i].width < 1280 || allResolutions[i].height < 720) continue;

            string option = allResolutions[i].width + " x " + allResolutions[i].height;

            if (!uniqueResolutions.Contains(option))
            {
                uniqueResolutions.Add(option);
                options.Add(option);
                resolutions.Add(allResolutions[i]);

                // 현재 내 모니터 해상도와 같다면 그걸 기본 선택으로 지정
                if (allResolutions[i].width == Screen.width &&
                    allResolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = resolutions.Count - 1;
                }
            }
        }

        // 혹시라도 필터링 때문에 목록이 텅 비면(노트북 등) 현재 해상도 하나는 강제로 추가
        if (options.Count == 0)
        {
            string currentOption = Screen.width + " x " + Screen.height;
            options.Add(currentOption);
            resolutions.Add(Screen.currentResolution);
            currentResolutionIndex = 0;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 해상도 & 전체화면 연결
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        // 사운드 슬라이더 연결 & 현재 볼륨 가져오기
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            float savedBGM = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
            bgmSlider.value = savedBGM;
            if (bgmSpeaker != null) bgmSpeaker.volume = savedBGM;
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            sfxSlider.value = savedSFX;
            if (sfxSpeaker != null) sfxSpeaker.volume = savedSFX;
        }

        // 마우스 감도 연결 & 저장된 값 불러오기
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            // 저장된 감도 불러오기 (없으면 기본값 1.0)
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        }

        fullscreenToggle.isOn = Screen.fullScreen;
    }

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
        PlayerPrefs.SetFloat("BGMVolume", volume);
        PlayerPrefs.Save();
    }
    public void SetSFXVolume(float volume)
    {
        if (sfxSpeaker != null)
        {
            sfxSpeaker.volume = volume;
        }
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
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