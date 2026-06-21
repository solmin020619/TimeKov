using UnityEngine;

public class InGameAudioManager : MonoBehaviour
{
    public AudioSource gameBGMSpeaker;
    public AudioSource gameSFXSpeaker;

    void Start()
    {
        // 저장된 볼륨 초기 적용
        SetBGMVolume(GlobalSettingsManager.CurrentBGMVolume);
        SetSFXVolume(GlobalSettingsManager.CurrentSFXVolume);

        // 설정창 슬라이더 변경 시 실시간 반영
        GlobalSettingsManager.OnBGMVolumeChanged += SetBGMVolume;
        GlobalSettingsManager.OnSFXVolumeChanged += SetSFXVolume;
    }

    void OnDestroy()
    {
        GlobalSettingsManager.OnBGMVolumeChanged -= SetBGMVolume;
        GlobalSettingsManager.OnSFXVolumeChanged -= SetSFXVolume;
    }

    void SetBGMVolume(float vol)
    {
        if (gameBGMSpeaker != null)
            gameBGMSpeaker.volume = vol;
    }

    void SetSFXVolume(float vol)
    {
        if (gameSFXSpeaker != null)
            gameSFXSpeaker.volume = vol;
    }
}