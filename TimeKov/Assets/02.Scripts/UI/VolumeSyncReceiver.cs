// VolumeSyncReceiver.cs
// 동적으로 스폰되는 오브젝트(적, 설비 등) 프리팹에 붙이는 볼륨 수신 컴포넌트
//
// [사용법]
//   1. 적 프리팹, 설비 프리팹 등 AudioSource가 있는 프리팹 루트에 추가
//   2. soundType을 BGM 또는 SFX로 설정
//   → 스폰 즉시 현재 저장된 볼륨이 적용되고, 이후 슬라이더 조절에도 실시간 반응

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VolumeSyncReceiver : MonoBehaviour
{
    public VolumeSync.SoundType soundType = VolumeSync.SoundType.SFX;

    private AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();

        // 스폰 즉시 현재 볼륨 적용 (PlayerPrefs 기준)
        float vol = (soundType == VolumeSync.SoundType.BGM)
            ? PlayerPrefs.GetFloat("BGMVolume", 1f)
            : PlayerPrefs.GetFloat("SFXVolume", 1f);

        _source.volume = vol;
    }

    void OnEnable()
    {
        if (soundType == VolumeSync.SoundType.BGM)
            GlobalSettingsManager.OnBGMVolumeChanged += SetVolume;
        else
            GlobalSettingsManager.OnSFXVolumeChanged += SetVolume;
    }

    void OnDisable()
    {
        GlobalSettingsManager.OnBGMVolumeChanged -= SetVolume;
        GlobalSettingsManager.OnSFXVolumeChanged -= SetVolume;
    }

    private void SetVolume(float vol)
    {
        if (_source != null)
            _source.volume = vol;
    }
}
