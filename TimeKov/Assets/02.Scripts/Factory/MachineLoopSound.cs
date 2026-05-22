// MachineLoopSound.cs
// 설비 생산 중 루프 사운드 컴포넌트 (3D 거리 감쇠 지원)
//
// [사용법]
//   1. 설비 게임오브젝트에 이 컴포넌트 추가
//   2. productionLoopClip: 생산 중 반복 재생할 클립
//   3. ProcessingMachine이 StartProduction() / StopProduction() 자동 호출
//
// [3D 설정]
//   Min Distance: 이 거리 안에서는 최대 볼륨
//   Max Distance: 이 거리 밖에서는 무음
//   Inspector에서 Spatial Blend = 1 로 설정해도 동일 효과

using UnityEngine;

public class MachineLoopSound : MonoBehaviour
{
    [Header("오디오 소스 (비워두면 자동 생성)")]
    [SerializeField] private AudioSource loopSource;

    [Header("생산 중 루프 클립")]
    [SerializeField] private AudioClip productionLoopClip;

    [Header("생산 시작 / 완료 1회 사운드 (선택)")]
    [SerializeField] private AudioClip productionStartClip;
    [SerializeField] private AudioClip productionDoneClip;

    [Header("3D 거리 감쇠 설정")]
    [Tooltip("이 거리 안에서는 최대 볼륨 (미터)")]
    [SerializeField] private float minDistance = 3f;
    [Tooltip("이 거리 밖에서는 소리가 거의 안 들림 (미터)")]
    [SerializeField] private float maxDistance = 20f;
    [Tooltip("Logarithmic = 자연스러운 감쇠 / Linear = 일정하게 감소")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (loopSource == null) loopSource = GetComponent<AudioSource>();
        if (loopSource == null) loopSource = gameObject.AddComponent<AudioSource>();

        loopSource.loop        = true;
        loopSource.playOnAwake = false;
        loopSource.volume      = PlayerPrefs.GetFloat("SFXVolume", 1f);

        // 3D 공간 설정 적용
        loopSource.spatialBlend  = 1f;              // 완전 3D
        loopSource.minDistance   = minDistance;
        loopSource.maxDistance   = maxDistance;
        loopSource.rolloffMode   = rolloffMode;
    }

    private void OnEnable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged += ApplySFXVolume;
    }

    private void OnDisable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= ApplySFXVolume;
        StopProduction();
    }

    // ─── 볼륨 동기화 ─────────────────────────────────────────────────────────

    private void ApplySFXVolume(float vol)
    {
        if (loopSource != null) loopSource.volume = vol;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>생산 시작 — 루프 사운드 재생 시작.</summary>
    public void StartProduction()
    {
        if (loopSource == null) return;

        // 시작 1회 사운드 — 설비 위치에서 3D 재생
        if (productionStartClip != null)
            loopSource.PlayOneShot(productionStartClip);

        if (productionLoopClip != null)
            loopSource.clip = productionLoopClip;

        if (!loopSource.isPlaying)
            loopSource.Play();
    }

    /// <summary>생산 완료 또는 중단 — 루프 사운드 정지.</summary>
    public void StopProduction(bool playDoneSound = false)
    {
        if (loopSource != null && loopSource.isPlaying)
            loopSource.Stop();

        // 완료 1회 사운드 — 설비 위치에서 3D 재생
        if (playDoneSound && productionDoneClip != null && loopSource != null)
            loopSource.PlayOneShot(productionDoneClip);
    }

    /// <summary>현재 생산 루프가 재생 중인지 여부.</summary>
    public bool IsPlaying => loopSource != null && loopSource.isPlaying;
}
