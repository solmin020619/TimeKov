using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 프롤로그 씬 시작 시 인트로 영상을 전체화면으로 재생한다.
/// 영상 재생 중에는 플레이어 입력 차단 + QuestTrigger 콜라이더 비활성화.
/// 영상이 끝나면 모든 상태를 복원하고 기존 프롤로그 흐름에 복귀한다.
/// 기존 스크립트(QuestManager, PrologueManager 등)는 수정하지 않는다.
/// </summary>
public class IntroVideoController : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("재생할 인트로 영상. 비워두면 즉시 프롤로그로 진행.")]
    [SerializeField] VideoClip _videoClip;

    [Header("References")]
    [Tooltip("영상이 표시될 RawImage")]
    [SerializeField] RawImage _videoDisplay;
    [Tooltip("페이드 아웃용 CanvasGroup (IntroVideoCanvas 루트)")]
    [SerializeField] CanvasGroup _canvasGroup;

    [Header("Timing")]
    [Tooltip("영상 종료 후 페이드 아웃 시간(초)")]
    [SerializeField] float _fadeOutDuration = 0.5f;

    VideoPlayer _videoPlayer;
    AudioSource _audioSource;
    RenderTexture _renderTexture;
    Collider[] _questTriggerColliders;
    float _savedListenerVolume;
    bool _finished;

    void Awake()
    {
        // 플레이어 입력 차단
        PlayerInputComponent.IsBlocked = true;

        // QuestTrigger 콜라이더 전부 끄기 — 스폰 위치가 트리거 안이어도 즉시 완료 방지
        var questTriggers = Object.FindObjectsByType<QuestTrigger>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        _questTriggerColliders = new Collider[questTriggers.Length];
        for (int i = 0; i < questTriggers.Length; i++)
        {
            var col = questTriggers[i].GetComponent<Collider>();
            _questTriggerColliders[i] = col;
            if (col != null) col.enabled = false;
        }

        // 기존 사운드 전체 소거 — 영상 AudioSource만 ignoreListenerVolume으로 면제
        _savedListenerVolume = AudioListener.volume;
        AudioListener.volume = 0f;

        if (_videoClip == null)
        {
            FinishIntro();
            return;
        }

        SetupVideoPlayer();
    }

    void SetupVideoPlayer()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        if (_videoPlayer == null)
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        int w = Mathf.Max(1, (int)_videoClip.width);
        int h = Mathf.Max(1, (int)_videoClip.height);
        _renderTexture = new RenderTexture(w, h, 0);

        _videoPlayer.clip            = _videoClip;
        _videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture   = _renderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        _videoPlayer.playOnAwake     = false;
        _videoPlayer.isLooping       = false;
        _videoPlayer.SetTargetAudioSource(0, _audioSource);
        // AudioListener.volume = 0 상태에서도 영상 오디오는 들리게
        _audioSource.ignoreListenerVolume = true;

        if (_videoDisplay != null)
            _videoDisplay.texture = _renderTexture;

        _videoPlayer.loopPointReached += OnVideoEnded;
        _videoPlayer.Prepare();
    }

    void Start()
    {
        if (_videoPlayer == null) return;
        StartCoroutine(PlayWhenReady());
    }

    IEnumerator PlayWhenReady()
    {
        while (!_videoPlayer.isPrepared)
            yield return null;

        // 준비 완료 후 재생 위치를 정확히 0으로 맞추고 한 프레임 대기
        _videoPlayer.time = 0;
        yield return null;

        _videoPlayer.Play();

        // 안전 장치: 영상 길이 + 2초 후에도 종료 이벤트 미수신 시 강제 종료
        // unscaledTime 사용 — timeScale 변경에 영향 받지 않음
        float startTime = Time.unscaledTime;
        float timeout   = (float)_videoClip.length + 2f;
        while (Time.unscaledTime - startTime < timeout)
            yield return null;

        if (!_finished)
        {
            Debug.LogWarning("[IntroVideoController] 영상 종료 이벤트 미수신 — 강제 종료");
            StartCoroutine(FadeAndFinish());
        }
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        if (_finished) return;
        StartCoroutine(FadeAndFinish());
    }

    IEnumerator FadeAndFinish()
    {
        _finished = true;

        if (_videoPlayer != null)
            _videoPlayer.Stop();

        // 페이드 아웃 — unscaledDeltaTime 사용 (timeScale = 0이어도 동작)
        if (_canvasGroup != null && _fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                yield return null;
            }
        }

        FinishIntro();
    }

    void FinishIntro()
    {
        // QuestTrigger 콜라이더 복원 — 이미 안에 있으면 OnTriggerEnter 자동 재발화
        foreach (var col in _questTriggerColliders)
            if (col != null) col.enabled = true;

        // 사운드 복원
        AudioListener.volume = _savedListenerVolume;

        // 입력 차단 해제
        PlayerInputComponent.IsBlocked = false;

        // 영상 Canvas 비활성화
        gameObject.SetActive(false);

        CleanupRenderTexture();
    }

    void CleanupRenderTexture()
    {
        if (_renderTexture == null) return;
        if (_videoDisplay != null) _videoDisplay.texture = null;
        _renderTexture.Release();
        Destroy(_renderTexture);
        _renderTexture = null;
    }

    void OnDestroy()
    {
        if (_videoPlayer != null)
            _videoPlayer.loopPointReached -= OnVideoEnded;
        CleanupRenderTexture();
    }
}
