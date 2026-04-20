using UnityEngine;

public class VolumeSync : MonoBehaviour
{
    public enum SoundType { BGM, SFX }
    public SoundType soundType = SoundType.SFX;

    private AudioSource[] audioSources;

    void Awake()
    {
        audioSources = GetComponentsInChildren<AudioSource>(true);
    }

    void Start()
    {
        float savedVol = (soundType == SoundType.BGM)
            ? PlayerPrefs.GetFloat("BGMVolume", 1.0f)
            : PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        UpdateVolume(savedVol);

        if (soundType == SoundType.BGM)
            GlobalSettingsManager.OnBGMVolumeChanged += UpdateVolume;
        else
            GlobalSettingsManager.OnSFXVolumeChanged += UpdateVolume;
    }

    void OnDestroy()
    {
        if (soundType == SoundType.BGM)
            GlobalSettingsManager.OnBGMVolumeChanged -= UpdateVolume;
        else
            GlobalSettingsManager.OnSFXVolumeChanged -= UpdateVolume;
    }

    void UpdateVolume(float vol)
    {
        if (audioSources == null) return;

        foreach (var audio in audioSources)
        {
            if (audio != null)
                audio.volume = vol;
        }
    }
}