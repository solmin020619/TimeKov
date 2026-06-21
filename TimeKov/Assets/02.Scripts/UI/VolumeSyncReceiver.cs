// VolumeSyncReceiver.cs
// 동적으로 스폰되는 오브젝트(적, 설비 등) 프리팹에 붙이는 SFX 볼륨 수신 컴포넌트
//
// [사용법]
//   AudioSource가 있는 프리팹 루트에 추가
//   → 스폰 즉시 현재 SFX 볼륨 적용, 이후 슬라이더 변경에도 실시간 반응

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VolumeSyncReceiver : MonoBehaviour
{
    private AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();

        // 스폰 즉시 현재 SFX 볼륨 적용
        _source.volume = GlobalSettingsManager.CurrentSFXVolume;
    }

    void OnEnable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged += SetVolume;
    }

    void OnDisable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= SetVolume;
    }

    private void SetVolume(float vol)
    {
        if (_source != null)
            _source.volume = vol;
    }
}
