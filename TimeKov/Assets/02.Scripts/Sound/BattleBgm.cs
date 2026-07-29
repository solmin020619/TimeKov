using System.Collections;
using UnityEngine;

// 전투 전용 BGM 재생기 (지연 싱글톤, 씬 세팅 불필요 — GameSfx 와 동일 패턴).
//   교전 시 Begin → 기존 BGM(InGameAudioManager.gameBGMSpeaker)을 "페이드아웃하며" 일시정지하고
//                   전투 BGM 을 "페이드인" 하며 루프 재생(크로스페이드).
//   처치/이탈 시 End → 전투 BGM 을 페이드아웃 → 종료 임팩트(샤악) 1회 → 기존 BGM 을 페이드인 재개.
//   다중 보스/혼전 대비 참조 카운트(모두 끝나야 원복).
//   클립은 GameSfxConfig 에서 SfxId 로 관리(WyvernBattleBgm=보스, NormalBattleBgm=일반몹). 볼륨은 BGM 볼륨을 따른다.
public class BattleBgm : MonoBehaviour
{
    private static BattleBgm _i;
    private static bool _quitting;

    // 전환 페이드 길이(초). 교전 진입은 짧고, 종료는 여운 있게 조금 길게.
    private const float FadeIn  = 0.6f;   // 전투 BGM 페이드인 / 필드 BGM 복귀 페이드인
    private const float FadeOut = 0.8f;   // 전투 BGM 페이드아웃

    public static BattleBgm I
    {
        get
        {
            if (_i == null && !_quitting)
            {
                var go = new GameObject("[BattleBgm]");
                _i = go.AddComponent<BattleBgm>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    private AudioSource _src;
    private AudioSource _baseBgm;   // 페이드아웃 후 일시정지한 기존 BGM 스피커(End 에서 페이드인 재개)
    private int _active;            // 진행 중 전투 수(다중 보스/혼전 대비)
    private float _clipVol = 1f;    // 현재 전투 BGM 의 config 볼륨(볼륨 변경 시 재계산용)
    private Coroutine _co;          // 진행 중인 전환/페이드 코루틴(항상 1개만)
    private bool _suspended;        // 게임오버로 중단됨(전투 소스 페이드아웃 · base BGM 재개는 리스폰까지 보류)

    private void Build()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;   // 2D
        gameObject.AddComponent<BgmVolumeSource>();   // VolumeSync(SFX 일괄 동기화)가 이 BGM 소스를 덮지 않게 제외 표시
        GlobalSettingsManager.OnBGMVolumeChanged += OnBgmVolumeChanged;
    }

    private void OnBgmVolumeChanged(float vol)
    {
        // 전환 코루틴 진행 중엔 코루틴이 볼륨을 실시간(CurrentBGMVolume 기준)으로 몰기 때문에 직접 세팅하지 않는다.
        if (_co != null) return;
        if (_src != null) _src.volume = _clipVol * vol;
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy()
    {
        GlobalSettingsManager.OnBGMVolumeChanged -= OnBgmVolumeChanged;
        if (_i == this) _i = null;
    }

    // 전투 BGM 시작(교전). 이미 진행 중이면 카운트만 증가(재시작 X).
    public static void Begin(SfxId bgmId) { if (!_quitting) I.BeginInternal(bgmId); }
    // 전투 BGM 종료(처치/이탈). 마지막 전투가 끝나면 기존 BGM 재개.
    public static void End() { if (!_quitting && _i != null) _i.EndInternal(); }

    // 사망(게임오버) 시: 전투 BGM 을 서서히 페이드아웃하며 정지. 기존 BGM 은 게임오버 사운드가 들리도록 계속 정지 유지.
    public static void SuspendForGameOver(float duration = 1.5f) { if (!_quitting && _i != null) _i.SuspendInternal(duration); }
    // 리스폰 시: 정지해 뒀던 기존(필드) BGM 페이드인 재개.
    public static void ResumeAfterGameOver() { if (!_quitting && _i != null) _i.ResumeInternal(); }

    private void StopCo() { if (_co != null) { StopCoroutine(_co); _co = null; } }

    private void BeginInternal(SfxId id)
    {
        _active++;
        if (_active > 1) return;   // 이미 전투 중 — 중복 시작 방지

        // 클립이 없으면 전투 BGM 없이 진행(기존 필드 BGM 유지). ★base 를 건드리지 않아 무음 방지.
        if (!GameSfx.TryGet(id, out var clip, out var vol) || clip == null) return;

        StopCo();

        // 페이드아웃 대상이 될 기존 BGM 캡처. ★내가 실제로 재생 중이던 것만 기억 → End 에서 그때만 재개.
        var mgr = FindFirstObjectByType<InGameAudioManager>();
        var bgm = mgr != null ? mgr.gameBGMSpeaker : null;
        _baseBgm = (bgm != null && bgm.isPlaying) ? bgm : null;

        _clipVol = vol;
        _src.clip = clip;
        _src.volume = 0f;
        _src.Play();
        _co = StartCoroutine(CrossToBattle());
    }

    // 전투 BGM 을 0→목표 볼륨으로 페이드인, 동시에 기존 BGM 을 현재볼륨→0 페이드아웃 후 일시정지(크로스페이드).
    private IEnumerator CrossToBattle()
    {
        float baseStart = _baseBgm != null ? _baseBgm.volume : 0f;
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
        {
            float f = t / FadeIn;
            if (_src != null) _src.volume = _clipVol * GlobalSettingsManager.CurrentBGMVolume * f;   // 슬라이더 실시간 반영
            if (_baseBgm != null) _baseBgm.volume = baseStart * (1f - f);
            yield return null;   // 정지(timeScale 0) 중에도 진행되도록 unscaled
        }
        if (_src != null) _src.volume = _clipVol * GlobalSettingsManager.CurrentBGMVolume;
        if (_baseBgm != null) { _baseBgm.Pause(); _baseBgm.volume = GlobalSettingsManager.CurrentBGMVolume; }   // 재개 대비 볼륨 원복(정지 중이라 무음)
        _co = null;
    }

    private void EndInternal()
    {
        if (_active <= 0) return;
        _active--;
        if (_active > 0) return;   // 아직 다른 전투 진행 중

        // 전투 BGM 도 없고 재개할 필드 BGM 도 없으면(클립 없이 시작했던 경우) 아무 것도 하지 않음(임팩트도 X).
        if ((_src == null || !_src.isPlaying) && _baseBgm == null) return;

        StopCo();
        _co = StartCoroutine(CrossToField());
    }

    // 전투 BGM 페이드아웃 → 종료 임팩트(샤악) 1회 → 기존 BGM 페이드인 재개.
    private IEnumerator CrossToField()
    {
        bool hadBattle = _src != null && _src.isPlaying;

        if (hadBattle)
        {
            // ★엔드필드식 포인트: 전투 사운드 "끝부분에 겹쳐" 샤악- 임팩트 1회(클립 없으면 무음 · SFX 볼륨 따름).
            //   전투 BGM 페이드아웃과 동시에 시작 → 샤악이 전투 꼬리를 덮고, 이어서 필드 BGM 이 피어오른다.
            GameSfx.Play(SfxId.BattleEndAccent);

            float srcStart = _src.volume;
            for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
            {
                _src.volume = srcStart * (1f - t / FadeOut);
                yield return null;
            }
            _src.Stop();
        }

        // 기존(필드) BGM 을 0→CurrentBGMVolume 페이드인 재개.
        if (_baseBgm != null)
        {
            _baseBgm.UnPause();
            _baseBgm.volume = 0f;
            for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
            {
                _baseBgm.volume = GlobalSettingsManager.CurrentBGMVolume * (t / FadeIn);
                yield return null;
            }
            _baseBgm.volume = GlobalSettingsManager.CurrentBGMVolume;
            _baseBgm = null;
        }
        _co = null;
    }

    // 게임오버: 전투 소스를 페이드아웃 후 정지. base BGM 은 게임오버 사운드가 들리도록 재개하지 않고(정지 유지),
    //   리스폰 시 ResumeAfterGameOver 로 재개한다. 모든 전투를 종료 처리(_active=0)한다.
    private void SuspendInternal(float duration)
    {
        if (_active <= 0 && (_src == null || !_src.isPlaying)) return;   // 전투 중이 아니면 무시
        _active = 0;
        _suspended = true;
        StopCo();
        if (_src != null && _src.isPlaying) _co = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = _src.volume;
        for (float t = 0f; t < duration && _src != null; t += Time.unscaledDeltaTime)
        {
            _src.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;   // 사망 연출로 timeScale 이 0 이어도 진행되도록 unscaled
        }
        if (_src != null) _src.Stop();
        _co = null;
    }

    private void ResumeInternal()
    {
        if (!_suspended) return;
        _suspended = false;
        if (_baseBgm == null) return;
        StopCo();
        _co = StartCoroutine(ResumeBaseRoutine());   // 필드 BGM 페이드인 재개
    }

    private IEnumerator ResumeBaseRoutine()
    {
        _baseBgm.UnPause();
        _baseBgm.volume = 0f;
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
        {
            _baseBgm.volume = GlobalSettingsManager.CurrentBGMVolume * (t / FadeIn);
            yield return null;
        }
        _baseBgm.volume = GlobalSettingsManager.CurrentBGMVolume;
        _baseBgm = null;
        _co = null;
    }
}
