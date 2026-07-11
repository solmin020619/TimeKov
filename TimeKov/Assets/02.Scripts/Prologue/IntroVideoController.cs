using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 프롤로그 씬 시작 시 인트로 영상을 전체화면으로 재생한다.
/// 영상이 끝나면 자동으로 Canvas를 숨기고 기존 프롤로그 흐름에 복귀한다.
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
    bool _finished;

    void Awake()
    {
        // 영상 재생 중 플레이어 입력 차단 (PlayerInputComponent.IsBlocked = true)
        PlayerInputComponent.IsBlocked = true;

        if (_videoClip == null)
        {
            // 영상 없으면 즉시 종료
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

        // RenderTexture: 영상 해상도에 맞춰 생성
        int w = Mathf.Max(1, (int)_videoClip.width);
        int h = Mathf.Max(1, (int)_videoClip.height);
        _renderTexture = new RenderTexture(w, h, 0);

        // VideoPlayer 설정
        _videoPlayer.clip             = _videoClip;
        _videoPlayer.renderMode       = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture    = _renderTexture;
        _videoPlayer.audioOutputMode  = VideoAudioOutputMode.AudioSource;
        _videoPlayer.playOnAwake      = false;
        _videoPlayer.isLooping        = false;
        _videoPlayer.SetTargetAudioSource(0, _audioSource);

        if (_videoDisplay != null)
            _videoDisplay.texture = _renderTexture;

        // 영상 종료 이벤트
        _videoPlayer.loopPointReached += OnVideoEnded;

        // 미리 준비
        _videoPlayer.Prepare();
    }

    void Start()
    {
        if (_videoPlayer == null) return;
        StartCoroutine(PlayWhenReady());
    }

    System.Collections.IEnumerator PlayWhenReady()
    {
        // VideoPlayer가 준비될 때까지 대기
        while (!_videoPlayer.isPrepared)
            yield return null;

        _videoPlayer.Play();

        // 안전 장치: 영상 길이 + 여유 시간 후에도 안 끝나면 강제 종료
        float safeTimeout = (float)_videoClip.length + 2f;
        yield return new WaitForSeconds(safeTimeout);

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

    System.Collections.IEnumerator FadeAndFinish()
    {
        _finished = true;

        // 영상 정지
        if (_videoPlayer != null)
            _videoPlayer.Stop();

        // 페이드 아웃
        if (_canvasGroup != null && _fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _fadeOutDuration);
                yield return null;
            }
        }

        FinishIntro();
    }

    void FinishIntro()
    {
        // 입력 차단 해제
        PlayerInputComponent.IsBlocked = false;

        // 영상 Canvas 비활성화
        gameObject.SetActive(false);

        // RenderTexture 해제
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
