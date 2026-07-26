using System.Collections;
using UnityEngine;

// 보스 전투 전용 BGM 재생기 (지연 싱글톤, 씬 세팅 불필요 — GameSfx 와 동일 패턴).
//   교전 시 Begin → 기존 BGM(InGameAudioManager.gameBGMSpeaker)을 일시정지하고 전투 BGM 을 루프 재생.
//   처치/이탈 시 End → 전투 BGM 정지 + 기존 BGM 재개. 다중 보스 대비 참조 카운트(모두 끝나야 원복).
//   클립은 GameSfxConfig 에서 SfxId 로 관리(예: WyvernBattleBgm). 볼륨은 BGM 볼륨을 따른다.
public class BattleBgm : MonoBehaviour
{
    private static BattleBgm _i;
    private static bool _quitting;

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
    private AudioSource _baseBgm;   // 일시정지한 기존 BGM 스피커(End 에서 재개)
    private int _active;            // 진행 중 전투 수(다중 보스 대비)
    private float _clipVol = 1f;    // 현재 전투 BGM 의 config 볼륨(볼륨 변경 시 재계산용)
    private Coroutine _fadeCo;      // 페이드아웃 코루틴
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
    // 리스폰 시: 정지해 뒀던 기존(필드) BGM 재개.
    public static void ResumeAfterGameOver() { if (!_quitting && _i != null) _i.ResumeInternal(); }

    private void BeginInternal(SfxId id)
    {
        _active++;
        if (_active > 1) return;   // 이미 전투 중 — 중복 시작 방지

        // 기존 BGM 일시정지(볼륨 변경 이벤트에 영향받지 않게 Stop 대신 Pause).
        //   ★내가 실제로 일시정지한 경우에만 _baseBgm 을 기억 → End 에서 그때만 재개(원래 꺼져있던 BGM 을 켜버리지 않게).
        var mgr = FindFirstObjectByType<InGameAudioManager>();
        var bgm = mgr != null ? mgr.gameBGMSpeaker : null;
        if (bgm != null && bgm.isPlaying) { bgm.Pause(); _baseBgm = bgm; }
        else _baseBgm = null;

        if (GameSfx.TryGet(id, out var clip, out var vol) && clip != null)
        {
            _clipVol = vol;
            _src.clip = clip;
            _src.volume = _clipVol * GlobalSettingsManager.CurrentBGMVolume;
            _src.Play();
        }
    }

    private void EndInternal()
    {
        if (_active <= 0) return;
        _active--;
        if (_active > 0) return;   // 아직 다른 보스 전투 진행 중

        if (_src != null) _src.Stop();
        if (_baseBgm != null) { _baseBgm.UnPause(); _baseBgm = null; }   // Unity fake-null 이면 조건에서 걸러짐
    }

    // 게임오버: 전투 소스를 페이드아웃 후 정지. base BGM 은 게임오버 사운드가 들리도록 재개하지 않고(정지 유지),
    //   리스폰 시 ResumeAfterGameOver 로 재개한다. 모든 전투를 종료 처리(_active=0)한다.
    private void SuspendInternal(float duration)
    {
        if (_active <= 0 && (_src == null || !_src.isPlaying)) return;   // 전투 중이 아니면 무시
        _active = 0;
        _suspended = true;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        if (_src != null && _src.isPlaying) _fadeCo = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = _src.volume;
        for (float t = 0f; t < duration && _src != null; t += Time.unscaledDeltaTime)
        {
            _src.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;   // 사망 연출로 timeScale 이 0 이어도 진행되도록 unscaled
        }
        if (_src != null) { _src.Stop(); _src.volume = startVol; }   // 볼륨 원복(다음 전투 대비)
        _fadeCo = null;
    }

    private void ResumeInternal()
    {
        if (!_suspended) return;
        _suspended = false;
        if (_baseBgm != null) { _baseBgm.UnPause(); _baseBgm = null; }   // 필드 BGM 재개
    }
}
