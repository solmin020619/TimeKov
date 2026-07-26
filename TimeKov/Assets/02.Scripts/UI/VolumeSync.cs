// VolumeSync.cs
// SFX 볼륨을 씬의 모든 AudioSource에 동기화 (BGM 제외)
//
// [사용법]
//   씬에 딱 하나만 배치. 같은 컴포넌트가 여러 개 있어도 하나만 살고 나머지 자동 제거.
//
// [BGM 제외 방법]
//   excludeSources 에 BGM AudioSource(예: InGameAudioManager의 gameBGMSpeaker)를 드래그
//   → 해당 소스는 SFX 볼륨 변경에서 제외됨 (BGM은 InGameAudioManager가 별도 처리)
//
// [동적 오브젝트]
//   볼륨 변경 이벤트 시 씬 전체를 재검색하므로 런타임 스폰 오브젝트도 포함됨
//   스폰 직후 즉시 적용이 필요하면 프리팹에 VolumeSyncReceiver 추가

using UnityEngine;
using System.Collections.Generic;

public class VolumeSync : MonoBehaviour
{
    [Header("제외할 AudioSource (BGM 전용 소스 등)")]
    [Tooltip("SFX 볼륨 적용에서 제외할 AudioSource — InGameAudioManager의 gameBGMSpeaker 등을 드래그")]
    [SerializeField] private AudioSource[] excludeSources;

    private bool _active = false;
    private HashSet<AudioSource> _excludeSet;

    void Awake()
    {
        // 씬에 VolumeSync가 이미 있으면 자신을 제거 (중복 방지)
        var all = FindObjectsByType<VolumeSync>(FindObjectsSortMode.None);
        foreach (var vs in all)
        {
            if (vs != this)
            {
                Destroy(this);
                return;
            }
        }

        // 제외 목록을 HashSet으로 변환 (검색 최적화)
        _excludeSet = new HashSet<AudioSource>();
        if (excludeSources != null)
            foreach (var s in excludeSources)
                if (s != null) _excludeSet.Add(s);

        _active = true;
    }

    void Start()
    {
        if (!_active) return;

        // 씬 시작 시 저장된 SFX 볼륨 즉시 적용
        UpdateVolume(GlobalSettingsManager.CurrentSFXVolume);

        GlobalSettingsManager.OnSFXVolumeChanged += UpdateVolume;
    }

    void OnDestroy()
    {
        if (!_active) return;
        GlobalSettingsManager.OnSFXVolumeChanged -= UpdateVolume;
    }

    void UpdateVolume(float vol)
    {
        // 매 호출 시 씬 전체 재검색 — 동적 스폰 오브젝트까지 포함
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var audio in sources)
        {
            if (audio == null) continue;
            if (_excludeSet != null && _excludeSet.Contains(audio)) continue; // BGM 등 제외(인스펙터 지정)
            if (audio.GetComponent<BgmVolumeSource>() != null) continue;       // 런타임 BGM 소스(BattleBgm 등) 제외 — SFX 볼륨에 안 덮이게
            audio.volume = vol;
        }
    }
}
