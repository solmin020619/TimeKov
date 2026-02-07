using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro; // TextMeshPro 필수!

public class SettingManager : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Sound Settings")]
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("Control Settings")]
    public Slider sensitivitySlider;

    List<Resolution> resolutions = new List<Resolution>();

    void Start()
    {
        // --------------------------------------------------------
        // 1. 해상도 목록 초기화 (중복 제거)
        // --------------------------------------------------------
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

                if (allResolutions[i].width == Screen.width &&
                    allResolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = resolutions.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // --------------------------------------------------------
        // 2. 이벤트 자동 연결 (인스펙터에서 안 해도 코드가 연결해줌)
        // --------------------------------------------------------

        // 해상도
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        // 전체화면 토글
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        // 사운드 슬라이더
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);

        // 마우스 감도 슬라이더
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
            // 저장된 감도 불러오기 (기본값 1.0)
            float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
            sensitivitySlider.value = savedSens;
        }

        // --------------------------------------------------------
        // 3. 현재 상태를 UI에 반영 (초기화)
        // --------------------------------------------------------
        fullscreenToggle.isOn = Screen.fullScreen;
        if (sfxSlider != null) sfxSlider.value = AudioListener.volume;
    }

    // =========================================================
    // ▼▼▼ 기능 함수들 (디버그 로그 포함) ▼▼▼
    // =========================================================

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        Debug.Log($"[해상도 변경] {resolution.width} x {resolution.height}");
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        Debug.Log($"[전체화면 설정] {isFullscreen}");
    }

    public void SetBGMVolume(float volume)
    {
        Debug.Log($"[BGM 볼륨] {volume}");
        // 나중에 오디오 소스 연결 필요
    }

    public void SetSFXVolume(float volume)
    {
        AudioListener.volume = volume; // 전체 소리 조절
        Debug.Log($"[SFX(전체) 볼륨] {volume}");
    }

    public void SetSensitivity(float sens)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sens);
        PlayerPrefs.Save();
        Debug.Log($"[마우스 감도 저장됨] {sens}");
    }

    // --- 그래픽 품질 (이것만 인스펙터에서 수동 연결해주세요!) ---

    public void SetQualityLow(bool isOn)
    {
        if (isOn)
        {
            QualitySettings.SetQualityLevel(0);
            Debug.Log("[그래픽 품질] Low");
        }
    }

    public void SetQualityMedium(bool isOn)
    {
        if (isOn)
        {
            QualitySettings.SetQualityLevel(2);
            Debug.Log("[그래픽 품질] Medium");
        }
    }

    public void SetQualityHigh(bool isOn)
    {
        if (isOn)
        {
            QualitySettings.SetQualityLevel(5);
            Debug.Log("[그래픽 품질] High (Ultra)");
        }
    }
}