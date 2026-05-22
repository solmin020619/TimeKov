// MachineLoopSound.cs
// 설비 생산 중 루프 사운드 컴포넌트
// MachineUI 또는 ProcessingMachine에서 StartProduction() / StopProduction() 호출
//
// [사용법]
//   1. 설비 게임오브젝트(또는 MachineUI 오브젝트)에 이 컴포넌트 추가
//   2. loopSource: 이 오브젝트의 AudioSource (없으면 자동 생성)
//      → loop = true, playOnAwake = false 로 설정됨
//   3. productionLoopClip: 생산 중 반복 재생할 클립
//   4. MachineUI.cs의 OpenFor() 또는 생산 시작·완료 처리 부분에서
//      GetComponent<MachineLoopSound>()?.StartProduction() / StopProduction() 호출
//
// [볼륨 동기화]
//   SFX 볼륨 변경 시 자동으로 loopSource 볼륨이 조정됩니다.

using UnityEngine;

public class MachineLoopSound : MonoBehaviour
{
    [Header("오디오 소스 (비워두면 자동 생성)")]
    [SerializeField] private AudioSource loopSource;

    [Header("생산 중 루프 클립")]
    [SerializeField] private AudioClip productionLoopClip;

    [Header("생산 시작 / 완료 1회 사운드 (선택)")]
    [SerializeField] private AudioClip productionStartClip;  // 생산 시작 시 1회
    [SerializeField] private AudioClip productionDoneClip;   // 생산 완료 시 1회

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (loopSource == null)
            loopSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        loopSource.loop        = true;
        loopSource.playOnAwake = false;
        loopSource.volume      = PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    private void OnEnable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged += ApplySFXVolume;
    }

    private void OnDisable()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= ApplySFXVolume;
        StopProduction(); // 씬 전환 등으로 비활성화될 때 루프 정지
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

        // 시작 1회 사운드
        if (productionStartClip != null)
            UISoundManager.Instance?.PlayClip(productionStartClip);

        // 루프 사운드
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

        if (playDoneSound && productionDoneClip != null)
            UISoundManager.Instance?.PlayClip(productionDoneClip);
    }

    /// <summary>현재 생산 루프가 재생 중인지 여부.</summary>
    public bool IsPlaying => loopSource != null && loopSource.isPlaying;
}
