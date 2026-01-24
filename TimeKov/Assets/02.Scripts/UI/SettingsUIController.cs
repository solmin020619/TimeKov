using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;

public class SettingsUIController : MonoBehaviour
{
    [Header("Sensitivity UI")]
    [SerializeField] private Slider sensSlider;
    [SerializeField] private TextMeshProUGUI sensValueText;

    [Header("Volume UI")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer masterMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";

    [Header("Back Scene")]
    [SerializeField] private string fallbackBackScene = "MainMenu_Scene";
    private const string BACK_KEY = "settings_back_scene";

    private void Start()
    {
        // 저장값 불러오기 → UI 반영
        sensSlider.value = SettingsData.MouseSensitivity;
        volumeSlider.value = SettingsData.MasterVolume;

        RefreshTexts();
        ApplyMasterVolume(volumeSlider.value);

        // 슬라이더 움직일 때 즉시 반영(특히 볼륨)
        sensSlider.onValueChanged.AddListener(_ => RefreshTexts());
        volumeSlider.onValueChanged.AddListener(v =>
        {
            RefreshTexts();
            ApplyMasterVolume(v);
        });
    }

    private void RefreshTexts()
    {
        if (sensValueText != null)
            sensValueText.text = sensSlider.value.ToString("0.00");

        if (volumeValueText != null)
            volumeValueText.text = Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
    }

    // 0~1 값을 dB로 변환하여 믹서에 적용
    private void ApplyMasterVolume(float linear)
    {
        linear = Mathf.Clamp01(linear);
        float db = (linear <= 0.0001f) ? -80f : Mathf.Log10(linear) * 20f;
        if (masterMixer != null)
            masterMixer.SetFloat(masterVolumeParam, db);
    }

    public void OnClickApply()
    {
        SettingsData.MouseSensitivity = sensSlider.value;
        SettingsData.MasterVolume = volumeSlider.value;
        ApplyMasterVolume(volumeSlider.value);
    }

    public void OnClickBack()
    {
        // 저장하고 나가기(선택)
        OnClickApply();

        // 어디서 왔는지 → 그 씬으로
        string back = PlayerPrefs.GetString(BACK_KEY, fallbackBackScene);
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadTo(back);
    }
}
