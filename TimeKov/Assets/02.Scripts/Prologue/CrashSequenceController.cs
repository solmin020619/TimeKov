using System.Collections;
using UnityEngine;

/// <summary>
/// 추락 연출 두 단계 관리.
/// 1단계(BeginAmbient): 영상 종료 후 ~ 비상 제어 장치 조작 전 — 미세한 긴장감.
/// 2단계(Play):         비상 제어 장치 조작 후 ~ World씬 전환 — 강렬한 추락.
/// </summary>
public class CrashSequenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Light         _directionalLight;
    [SerializeField] CanvasGroup   _fadeCanvas;
    [SerializeField] GameObject    _warningLights;

    [Header("Audio")]
    [SerializeField] AudioSource _ambientSource;   // 루프용 (경보음)
    [SerializeField] AudioSource _sfxSource;       // 원샷용 (충격음)
    [SerializeField] AudioClip   _clipAlarm;       // 경보 루프
    [SerializeField] AudioClip   _clipImpact1;     // 1차 충격
    [SerializeField] AudioClip   _clipImpact2;     // 2차 충격
    [SerializeField] AudioClip   _clipImpact3;     // 3차 충격

    // ─── 런타임 상태 ───────────────────────────────────────────
    VattalusLightController[] _lightControllers;
    VattalusStrobe[]           _warningStrobes;
    Color                      _originalLightColor;
    float                      _originalLightIntensity;
    float                      _baseFov = 60f;
    const float AlarmBaseVolume = 0.045f;

    // Update에서 읽는 카메라 롤 파라미터 (코루틴이 값만 변경)
    float _rollAmplitude;
    float _rollFreq;

    // ─── Unity ─────────────────────────────────────────────────
    void Awake()
    {
        if (_directionalLight != null)
        {
            _originalLightColor     = _directionalLight.color;
            _originalLightIntensity = _directionalLight.intensity;
        }
        if (Camera.main != null)
            _baseFov = Camera.main.fieldOfView;
    }

    void Update()
    {
        // 카메라 Z-Roll — ThirdPersonCamera는 localPosition만 수정하므로 충돌 없음
        if (Camera.main == null) return;
        float roll = _rollAmplitude * Mathf.Sin(Time.time * _rollFreq);
        var euler  = Camera.main.transform.localEulerAngles;
        euler.z    = roll;
        Camera.main.transform.localEulerAngles = euler;
    }

    // ─── 1단계: 배경 긴장감 ────────────────────────────────────
    /// <summary>영상 종료 직후 호출. 미세한 긴장감 효과를 시작한다.</summary>
    public void BeginAmbient()
    {
        // 씬 전체 VattalusLightController 플리커링 활성
        _lightControllers = Object.FindObjectsByType<VattalusLightController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var lc in _lightControllers)
        {
            lc.enableFlickering = true;
            lc.ChangeState(lc.currentState); // UpdateFlickering() 트리거
        }

        // 경보등 활성 (느린 주기)
        if (_warningLights != null)
        {
            _warningLights.SetActive(true);
            _warningStrobes = _warningLights.GetComponentsInChildren<VattalusStrobe>(true);
            foreach (var s in _warningStrobes) s.strobePeriod = 0.8f;
        }

        // 경보음 루프 시작 — SFX 볼륨 설정 반영
        if (_ambientSource != null && _clipAlarm != null)
        {
            _ambientSource.clip   = _clipAlarm;
            _ambientSource.loop   = true;
            _ambientSource.volume = AlarmBaseVolume * GlobalSettingsManager.CurrentSFXVolume;
            _ambientSource.Play();
        }
        GlobalSettingsManager.OnSFXVolumeChanged += OnSfxVolumeChanged;

        // 미세 Roll + 주기적 소진동
        _rollAmplitude = 1f;
        _rollFreq      = Mathf.PI * 2f / 3f; // 주기 3s
        StartCoroutine(AmbientShakeLoop());
    }

    IEnumerator AmbientShakeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(4f, 6f));
            ThirdPersonCamera.Shake(0.3f, 0.15f);
        }
    }

    // ─── 2단계: 추락 시퀀스 ───────────────────────────────────
    /// <summary>비상 제어 장치 조작 후 PrologueManager가 호출한다.</summary>
    public void Play()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= OnSfxVolumeChanged;
        StopAllCoroutines(); // 1단계 코루틴 종료
        StartCoroutine(CrashCoroutine());
    }

    void OnSfxVolumeChanged(float vol)
    {
        if (_ambientSource != null && _ambientSource.isPlaying)
            _ambientSource.volume = AlarmBaseVolume * vol;
    }

    void OnDestroy()
    {
        GlobalSettingsManager.OnSFXVolumeChanged -= OnSfxVolumeChanged;
    }

    IEnumerator CrashCoroutine()
    {
        // t=0.0 —————————————————————————————————
        PlayerInputComponent.IsBlocked = true;
        _rollAmplitude = 0f;
        // 추락 시작 — 경보음 볼륨 조금 올림 (설정 볼륨 기준)
        if (_ambientSource != null)
            _ambientSource.volume = AlarmBaseVolume * 1.5f * GlobalSettingsManager.CurrentSFXVolume;

        // ──────────────────── t=0.2 — 1차 충격 ────────────────
        yield return new WaitForSeconds(0.2f);
        ThirdPersonCamera.Shake(0.6f, 0.7f);
        StartCoroutine(FovPulse(66f, 0.2f));
        _rollAmplitude = 3f;
        _rollFreq      = Mathf.PI * 2f / 1.5f;
        if (_sfxSource != null && _clipImpact1 != null)
            _sfxSource.PlayOneShot(_clipImpact1, GlobalSettingsManager.CurrentSFXVolume);

        // ──────────────────── t=1.0 — 2차 충격 ────────────────
        yield return new WaitForSeconds(0.8f);
        ThirdPersonCamera.Shake(1.0f, 1.1f);
        StartCoroutine(FovPulse(69f, 0.2f));
        _rollAmplitude = 5f;
        _rollFreq      = Mathf.PI * 2f / 1.0f;
        StartCoroutine(LerpLightColor(
            _originalLightColor,
            new Color(1f, 0.45f, 0.1f),
            1.5f));
        SetStrobePeriod(0.4f);
        if (_sfxSource != null && _clipImpact2 != null)
            _sfxSource.PlayOneShot(_clipImpact2, GlobalSettingsManager.CurrentSFXVolume * 1.1f);

        // ──────────────────── t=2.5 — 3차 충격 ────────────────
        yield return new WaitForSeconds(1.5f);
        ThirdPersonCamera.Shake(3.0f, 1.8f);
        StartCoroutine(FovPulse(73f, 0.25f));
        _rollAmplitude = 8f;
        _rollFreq      = Mathf.PI * 2f / 0.7f;
        if (_directionalLight != null)
        {
            _directionalLight.color     = new Color(1f, 0.12f, 0.05f);
            _directionalLight.intensity = 0.5f;
        }
        SetStrobePeriod(0.2f);
        if (_sfxSource != null && _clipImpact2 != null)
            StartCoroutine(ImpactBurst(_clipImpact2, 5, 0.18f, GlobalSettingsManager.CurrentSFXVolume));

        // ──────────────────── t=4.0 — 충격 후 감쇠 ───────────
        yield return new WaitForSeconds(1.5f);
        StartCoroutine(DampenRoll(0.3f));
        StartCoroutine(FadeLightIntensity(0f, 1.5f));

        // ──────────────────── t=4.5 — 화면 페이드 ─────────────
        yield return new WaitForSeconds(0.5f);
        if (_fadeCanvas != null)
            yield return StartCoroutine(CoreUtilities.Fade(_fadeCanvas, 0f, 1f, 1.0f));
        else
            yield return new WaitForSeconds(1.0f);

        // ──────────────────── t=5.5 — 씬 전환 ─────────────────
        // 여기까지 왔으면 프롤로그를 끝까지 본 것. 다음부터 이 월드는 World 로 바로 들어간다.
        SaveSlotManager.Instance?.MarkPrologueDone();
        CoreUtilities.LoadViaLoading("World");
    }

    // ─── 헬퍼 코루틴 ──────────────────────────────────────────
    IEnumerator FovPulse(float targetFov, float duration)
    {
        float half    = duration * 0.25f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            Camera.main.fieldOfView = Mathf.Lerp(_baseFov, targetFov, elapsed / half);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration - half)
        {
            elapsed += Time.deltaTime;
            Camera.main.fieldOfView = Mathf.Lerp(targetFov, _baseFov, elapsed / (duration - half));
            yield return null;
        }
        Camera.main.fieldOfView = _baseFov;
    }

    IEnumerator LerpLightColor(Color from, Color to, float duration)
    {
        if (_directionalLight == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _directionalLight.color = Color.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _directionalLight.color = to;
    }

    IEnumerator FadeLightIntensity(float target, float duration)
    {
        if (_directionalLight == null) yield break;
        float start   = _directionalLight.intensity;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _directionalLight.intensity = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        _directionalLight.intensity = target;
    }

    IEnumerator DampenRoll(float duration)
    {
        float startAmp = _rollAmplitude;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed        += Time.deltaTime;
            _rollAmplitude  = Mathf.Lerp(startAmp, 0f, elapsed / duration);
            yield return null;
        }
        _rollAmplitude = 0f;
    }

    // count번 연속 재생, interval 간격, 재생마다 볼륨 0.75씩 감쇠
    IEnumerator ImpactBurst(AudioClip clip, int count, float interval, float baseVolume)
    {
        float vol = baseVolume * 1.3f;
        for (int i = 0; i < count; i++)
        {
            if (_sfxSource != null) _sfxSource.PlayOneShot(clip, vol);
            vol *= 0.65f;
            yield return new WaitForSeconds(interval);
        }
    }

    void SetStrobePeriod(float period)
    {
        if (_warningStrobes == null) return;
        foreach (var s in _warningStrobes) s.strobePeriod = period;
    }
}
