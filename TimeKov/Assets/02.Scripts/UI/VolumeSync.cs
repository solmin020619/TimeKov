// VolumeSync.cs
// BGM 또는 SFX 볼륨을 씬의 모든 AudioSource에 동기화
//
// [사용법]
//   씬에 VolumeSync(BGM) 하나, VolumeSync(SFX) 하나만 있으면 됨
//   같은 타입이 여러 개 있어도 첫 번째만 살고 나머지는 자동 제거됨
//
// [동적 오브젝트 처리]
//   볼륨 변경 이벤트가 발생할 때마다 씬 전체를 재검색하므로
//   게임 도중 스폰된 오브젝트도 자동 포함됨
//   단, 스폰 직후 현재 볼륨 적용은 VolumeSyncReceiver를 프리팹에 추가

using UnityEngine;

public class VolumeSync : MonoBehaviour
{
    public enum SoundType { BGM, SFX }
    public SoundType soundType = SoundType.SFX;

    private bool _active = false;

    void Awake()
    {
        // 같은 soundType의 VolumeSync가 이미 씬에 있으면 자신을 제거
        var all = FindObjectsByType<VolumeSync>(FindObjectsSortMode.None);
        foreach (var vs in all)
        {
            if (vs != this && vs.soundType == this.soundType)
            {
                Destroy(this);
                return;
            }
        }

        _active = true;
    }

    void Start()
    {
        if (!_active) return;

        // 씬 시작 시 현재 저장된 볼륨 즉시 적용
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
        if (!_active) return;

        if (soundType == SoundType.BGM)
            GlobalSettingsManager.OnBGMVolumeChanged -= UpdateVolume;
        else
            GlobalSettingsManager.OnSFXVolumeChanged -= UpdateVolume;
    }

    void UpdateVolume(float vol)
    {
        // 매 호출 시 씬 전체 재검색 — 동적 스폰 오브젝트까지 포함
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var audio in sources)
        {
            if (audio != null)
                audio.volume = vol;
        }
    }
}
