using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using JeffGrawAssets.FlexibleUI;

/// <summary>
/// 엔드필드식 "영상 팝업" 튜토리얼 UI.
/// 화면 전체를 블러 처리하고, 그 위 중앙에 (영상 + 제목 + 본문)을 페이지 단위로 올린다.
/// 시선이 영상으로 가도록 패널 박스 없이 블러 배경 위에 요소만 깔끔하게 얹는다(레퍼런스 충실).
/// Q/E 또는 좌우 화살표로 페이지를 넘기고, 모두 읽으면 닫을 수 있다.
/// 팝업 동안 게임은 정지(timeScale=0)된다.
///
/// [08-02] 런타임 자체생성 -> 씬 실물 오브젝트로 전환.
///   이전에는 Awake 에서 계층 전체(영상 프레임/제목/본문/화살표/키캡)를 코드로 만들었다. 그러면
///   (1) 에디터에 없으니 로컬라이징 컴포넌트를 못 붙이고 (2) 레이아웃 숫자를 코드에서만 만질 수 있었다.
///   지금은 Canvas/Overlays 아래 실물로 존재하고, 이 스크립트는 '참조를 쓰기만' 한다.
///   계층은 씬에 있는 실물이 원본이다(생성용 에디터 빌더는 08-03 에 팀 합의로 제거).
///
///   런타임에 남는 것은 저장이 불가능한 것뿐이다:
///     - RenderTexture (영상 출력 버퍼) - 매 실행 새로 만든다
///     - 블러가 볼 카메라 - Camera.main 은 실행 중에만 잡힌다
///     - 링/셰브론 스프라이트 - RuntimeGeneratedSprite 부품이 스스로 채운다
///
/// 표시/숨김은 오브젝트 비활성이 아니라 Canvas/Raycaster 토글로 한다(항상 활성 = LateUpdate 가 계속 돌아
/// 설정창 열고닫힘을 감지할 수 있고, I 로 항상 찾을 수 있음 - TutorialOverlay 와 동일).
///
/// TutorialVideoObjective / DiscoveryCueManager 가 Show/Hide 를 호출한다.
///
/// 입력 차단: TutorialOverlay 와 동일하게 GameUIController.SetTutorialCoachActive(true) 로
/// 플레이어 이동/공격/스킬/J/C 를 막는다. 페이지 넘김(Q/E/화살표)은 이 컴포넌트가 raw Input 으로
/// 직접 읽으므로 코치 키보드 차단과 무관하게 동작한다.
/// </summary>
public class TutorialVideoUI : MonoBehaviour
{
    // ── 싱글턴 (씬에 있는 실물을 찾는다) ─────────────────────────────
    private static TutorialVideoUI _i;
    private static bool _warnedMissing;
    public static bool HasInstance => _i != null;
    /// <summary>현재 팝업이 떠 있는지(발견 큐 등 다른 팝업이 겹쳐 뜨지 않게 게이트용).</summary>
    public static bool IsShowing => _i != null && _i._open;
    public bool IsOpen => _open;
    public static TutorialVideoUI I
    {
        get
        {
            // 평소엔 실물의 Awake 가 _i 를 채운다. 여기 탐색은 순서가 꼬였을 때의 보험이라 1회만 돈다
            // (매 프레임 호출되는 자리가 있어서, 없을 때 계속 씬을 뒤지지 않게 막는다).
            if (_i == null && !_warnedMissing)
            {
                _i = FindAnyObjectByType<TutorialVideoUI>(FindObjectsInactive.Include);
                if (_i == null)
                {
                    _warnedMissing = true;
                    Debug.LogError("[TutorialVideoUI] 씬 Canvas 에 영상 팝업이 없다. 메뉴 Tools/TIMEKOV/튜토리얼 영상 팝업 생성 을 실행해라.");
                }
            }
            return _i;
        }
    }

    // ── UI 요소 (빌더가 자동 연결) ────────────────────────────────────
    [Header("루트")]
    [Tooltip("표시/숨김 대상. 이걸 끄면 화면에서 사라진다(오브젝트는 계속 활성).")]
    [SerializeField] private Canvas canvasComp;
    [SerializeField] private GraphicRaycaster raycaster;
    [Tooltip("페이드 인/아웃용. 블러+틴트+콘텐츠를 통째로 알파 보간한다.")]
    [SerializeField] private CanvasGroup group;
    [Tooltip("배경 블러. 강도/반복은 이 컴포넌트의 Blur Instance Settings 에서 조절한다.")]
    [SerializeField] private BlurredImage blur;

    [Header("영상")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [Tooltip("클립이 비어 있는 페이지에서 대신 보여주는 '영상 준비 중' 판.")]
    [SerializeField] private GameObject placeholder;

    [Header("글")]
    [SerializeField] private TMP_Text pageCount;   // "1 / 2"
    [SerializeField] private TMP_Text titleText;   // "설비 가공"
    [SerializeField] private TMP_Text bodyText;

    [Header("하단 / 좌우")]
    [Tooltip("[Q][E] 키캡 + 안내 문구. 아직 다 안 읽었을 때 표시.")]
    [SerializeField] private GameObject hintRow;
    [Tooltip("마지막(또는 단일) 페이지에서 뜨는 '확인' 바.")]
    [SerializeField] private GameObject confirmBar;
    [SerializeField] private CanvasGroup chevronLeft;
    [SerializeField] private CanvasGroup chevronRight;

    [Header("연출 값")]
    [Tooltip("표시 직후 이 시간 동안은 닫히지 않는다(직전 입력이 묻어 즉시 닫히는 것 방지).")]
    [SerializeField] private float closeCooldown = 0.35f;
    [Tooltip("팝업이 떠오르는 시간(초). 게임 정지는 즉시, 화면만 부드럽게.")]
    [SerializeField] private float fadeInTime = 0.16f;
    [Tooltip("닫힐 때 화면 페이드 시간(초). 게임플레이는 이미 재개된 상태.")]
    [SerializeField] private float fadeOutTime = 0.13f;
    [Tooltip("더 이상 갈 수 없는 방향의 화살표 흐리기 정도.")]
    [SerializeField] private float chevronDisabledAlpha = 0.25f;
    [Tooltip("영상 출력 버퍼 해상도. 클립보다 크게 잡을 필요 없다.")]
    [SerializeField] private int videoWidth = 1280;
    [SerializeField] private int videoHeight = 720;

    // ── 런타임 ────────────────────────────────────────────────────────
    private RenderTexture _rt;

    private VideoTutorialPage[] _pages;
    private int _page;
    private int _maxSeen;          // 도달한 최대 페이지 (이 값이 마지막이면 닫기 허용)
    private Action _onClosed;
    private bool _open;
    private bool _suppressed;      // 설정창(ESC)이 위에 떠서 잠시 숨김 - 닫히면 복귀
    private float _shownTime;
    private float _resumeTimeScale = 1f;   // 팝업 닫을 때 복원할 timeScale
    private Coroutine _fadeCo;

    private bool AllRead => _pages != null && _maxSeen >= _pages.Length - 1;

    private void Awake()
    {
        if (_i != null && _i != this) { Destroy(gameObject); return; }
        _i = this;
        _warnedMissing = false;   // 실물이 등록됐으니 '없음' 판정 해제(씬 재입장 대비)
        SetupVideo();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // 팝업이 열린 채 파괴(씬 전환 등)되면 게임이 timeScale=0 으로 멈춘 채 남지 않도록 복원.
        if (_open && Time.timeScale == 0f) Time.timeScale = _resumeTimeScale;
        if (_i == this) _i = null;
        if (videoPlayer != null) videoPlayer.errorReceived -= OnVideoError;
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
    }

    // ── 외부 API ──────────────────────────────────────────────────────

    /// <summary>영상 팝업 표시. pages 를 순서대로 넘겨 보고, 다 읽고 닫으면 onClosed 호출.</summary>
    public void Show(VideoTutorialPage[] pages, Action onClosed)
    {
        if (pages == null || pages.Length == 0) { onClosed?.Invoke(); return; }

        _pages = pages;
        _onClosed = onClosed;
        _page = 0;
        _maxSeen = 0;
        _open = true;
        _suppressed = false;
        _shownTime = Time.unscaledTime;

        // 게임 정지 (현재 값 기억 후 0). 영상/입력은 unscaled 라 계속 동작.
        _resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        // 블러가 현재 화면 카메라를 보도록 재연결(Camera.main 은 실행 중에만 잡힌다).
        if (blur != null)
        {
            blur.Common.cameraReference = PickScreenCamera();
            blur.Common.ValidateBlur();
        }

        GameUIController.Instance?.SetTutorialCoachActive(true);   // 이동/공격/J·C 등 차단(키보드)
        // 영상 팝업은 마우스로 닫는다(확인/아무곳 클릭). GameUIController.RefreshCursorState가 매 프레임
        //   커서를 숨기므로(영상은 UIState가 아님) 플래그로 알려 강제 표시하게 함(LateUpdate 순서 경합 제거).
        if (GameUIController.Instance != null) GameUIController.Instance.TutorialVideoCursor = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (group != null) group.alpha = 0f;   // 페이드 인 시작점(스냅 인 방지)
        SetVisible(true);
        RenderPage();
        StartFade(1f, fadeInTime);             // 정지는 이미 즉시, 시각만 부드럽게 떠오름
    }

    // 즉시 숨김(씬 전환/Deactivate 등 teardown 경로). 게임 재개 + 캔버스 즉시 off.
    public void Hide() => HideInternal(false);

    private void HideInternal(bool fade)
    {
        bool wasOpen = _open;
        _open = false;
        _suppressed = false;
        if (videoPlayer != null) videoPlayer.Stop();
        if (wasOpen) Time.timeScale = _resumeTimeScale;   // 게임 즉시 재개(완료 콜백/입력 지연 없음)
        if (GameUIController.Instance != null) GameUIController.Instance.TutorialVideoCursor = false;
        GameUIController.Instance?.SetTutorialCoachActive(false);

        // fade=true(유저가 닫음): 게임은 즉시 재개하고 화면만 페이드 아웃. 그 외(teardown)엔 즉시 off.
        if (fade && wasOpen && isActiveAndEnabled && group != null)
        {
            if (raycaster != null) raycaster.enabled = false;   // 입력 즉시 게임으로 통과, 화면만 페이드
            StartFade(0f, fadeOutTime, hideAtEnd: true);
        }
        else
        {
            if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
            SetVisible(false);
        }
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    // GameObject 는 항상 활성으로 두고 Canvas/Raycaster 토글로 표시/숨김 처리한다.
    // (LateUpdate 가 계속 돌아 설정창 열고닫힘을 self-poll 로 감지해 복귀 - TutorialOverlay 와 동일)
    private void SetVisible(bool v)
    {
        if (canvasComp != null) canvasComp.enabled = v;
        if (raycaster != null) raycaster.enabled = v;
    }

    // 알파 페이드 시작(중첩 방지). 비활성/그룹 없으면 즉시 적용.
    private void StartFade(float target, float duration, bool hideAtEnd = false)
    {
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (!isActiveAndEnabled || group == null)
        {
            if (group != null) group.alpha = target;
            if (hideAtEnd) SetVisible(false);
            return;
        }
        _fadeCo = StartCoroutine(FadeRoutine(target, duration, hideAtEnd));
    }

    // 정지(timeScale=0) 중에도 도는 unscaled 알파 보간. 게임 정지/재개와 무관하게 시각만 ease.
    private System.Collections.IEnumerator FadeRoutine(float target, float duration, bool hideAtEnd)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, duration > 0f ? Mathf.Clamp01(t / duration) : 1f);
            yield return null;
        }
        group.alpha = target;
        if (hideAtEnd) SetVisible(false);
        _fadeCo = null;
    }

    private void LateUpdate()
    {
        if (!_open) return;

        // 설정창(ESC 일시정지)이 위에 뜨면 잠시 숨기고 영상 정지, 닫히면 복귀.
        bool settingsOpen =
            (TimeKov.UI.WindowManager.I != null && TimeKov.UI.WindowManager.I.IsOpen("Settings"))
            || (GameUIController.Instance != null
                && GameUIController.Instance.GetCurrentState() == GameUIController.UIState.Settings);
        if (settingsOpen != _suppressed)
        {
            _suppressed = settingsOpen;
            SetVisible(!_suppressed);
            if (videoPlayer != null)
            {
                if (_suppressed) videoPlayer.Pause();
                else if (CurrentClip() != null) videoPlayer.Play();
            }
        }
        if (_suppressed) return;

        // 코치모드/게임이 매 프레임 커서를 숨길 수 있으니, 팝업 떠 있는 동안 커서를 계속 보이게 재고정(마우스로 닫음).
        if (!Cursor.visible) Cursor.visible = true;
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;

        // 설정창이 닫히며 timeScale 을 1 로 되돌릴 수 있어, 팝업이 떠 있는 동안엔 0 으로 고정.
        if (Time.timeScale != 0f) Time.timeScale = 0f;

        // 페이지 넘김: Q/E + 좌우 화살표 (raw Input - 코치 키보드 차단과 무관)
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) GoNext();
        else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) GoPrev();
        // 다 읽었으면 Space/Enter/좌클릭으로 닫기 ("확인")
        else if (AllRead && CanClose() && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            Close();
    }

    private bool CanClose() => Time.unscaledTime - _shownTime > closeCooldown;

    private void GoNext()
    {
        if (_page < _pages.Length - 1)
        {
            _page++;
            if (_page > _maxSeen) _maxSeen = _page;
            RenderPage();
        }
        else if (AllRead && CanClose())
        {
            Close();   // 마지막 페이지에서 E/-> = 닫기
        }
    }

    private void GoPrev()
    {
        if (_page > 0) { _page--; RenderPage(); }
    }

    private void Close()
    {
        var cb = _onClosed;
        _onClosed = null;
        HideInternal(true);   // 화면은 페이드 아웃, 게임플레이는 즉시 재개(완료 콜백 지연 없음)
        cb?.Invoke();
    }

    private VideoClip CurrentClip()
        => (_pages != null && _page >= 0 && _page < _pages.Length) ? _pages[_page].clip : null;

    private void RenderPage()
    {
        if (_pages == null || _pages.Length == 0) return;
        var p = _pages[_page];
        CodexDiscovery.WatchTutorial(p.title);   // 도감: 이 영상 페이지를 봤다고 기록(제목 기준)
        int n = _pages.Length;

        if (pageCount != null)
        {
            pageCount.text = n > 1 ? $"{_page + 1} / {n}" : string.Empty;
            pageCount.gameObject.SetActive(n > 1);
        }
        if (titleText != null) titleText.text = p.title ?? string.Empty;
        if (bodyText != null) bodyText.text = p.body ?? string.Empty;

        // 영상 / 플레이스홀더
        if (p.clip != null)
        {
            if (placeholder != null) placeholder.SetActive(false);
            if (videoImage != null) videoImage.enabled = true;
            if (videoPlayer != null)
            {
                if (videoPlayer.clip != p.clip) videoPlayer.clip = p.clip;
                videoPlayer.Play();
            }
        }
        else
        {
            if (videoPlayer != null) videoPlayer.Stop();
            if (videoImage != null) videoImage.enabled = false;
            if (placeholder != null) placeholder.SetActive(true);
        }

        // 셰브론(양끝, 갈 수 있는 방향만 진하게). 페이지 1개면 숨김.
        //  1/2 = Q(이전) 못 쓰니 왼쪽 흐리게 / 오른쪽(E,다음) 진하게. 마지막은 반대.
        SetChevron(chevronLeft, n > 1, _page > 0);
        SetChevron(chevronRight, n > 1, _page < n - 1);

        // 하단: 닫을 수 있는 마지막(또는 단일) 페이지면 "확인" 바, 아니면 페이지 넘김 안내.
        bool showConfirm = AllRead && _page == n - 1;
        if (confirmBar != null) confirmBar.SetActive(showConfirm);
        if (hintRow != null) hintRow.SetActive(!showConfirm);
    }

    private void SetChevron(CanvasGroup chevron, bool visible, bool enabledDir)
    {
        if (chevron == null) return;
        chevron.gameObject.SetActive(visible);
        chevron.alpha = enabledDir ? 1f : chevronDisabledAlpha;
    }

    // ── 영상 세팅 (RenderTexture 는 에셋이 아니라 매 실행 새로 만든다) ──

    private void SetupVideo()
    {
        _rt = new RenderTexture(videoWidth, videoHeight, 0, RenderTextureFormat.ARGB32) { name = "TutorialVideoRT" };
        _rt.filterMode = FilterMode.Bilinear;
        _rt.Create();

        // 갓 만든 RenderTexture 는 내용이 미정의다(드라이버에 따라 흰색/쓰레기값).
        //   영상이 안 그려졌을 때 흰 사각형이 뜨던 원인 중 하나라 검정으로 한 번 밀어둔다.
        //   이러면 실패해도 뒤의 VideoFrame(검정)과 같은 색이라 화면이 덜 튄다.
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = prev;

        if (videoPlayer != null)
        {
            // ★씬에 직렬화된 값에 기대지 말고 코드에서 못 박는다.
            //   런타임 생성 -> 씬 실물로 전환할 때(커밋 d110e9f56) 아래 설정들이 코드에서 통째로 빠지고
            //   씬 컴포넌트의 저장값에만 의존하게 됐다. 그 뒤로 영상이 흰 화면으로만 떴다.
            //   VideoPlayer 는 source/renderMode 가 한 칸만 틀어져도 에러 없이 조용히 아무것도 안 그린다.
            //   누가 인스펙터를 잘못 만지거나 컴포넌트를 다시 만들어도 여기서 항상 바로잡힌다.
            videoPlayer.source            = VideoSource.VideoClip;
            videoPlayer.renderMode        = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode   = VideoAudioOutputMode.None;   // 무음 데모
            videoPlayer.playOnAwake       = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.isLooping         = true;
            videoPlayer.targetTexture     = _rt;

            videoPlayer.errorReceived  += OnVideoError;
            videoPlayer.prepareCompleted += OnVideoPrepared;
        }
        if (videoImage != null) videoImage.texture = _rt;
    }

    // 디코딩 실패는 조용히 넘어가면 원인을 못 찾는다(흰 화면만 남는다). 콘솔에 파일명까지 찍는다.
    private void OnVideoError(VideoPlayer vp, string message)
    {
        string clipName = vp != null && vp.clip != null ? vp.clip.name : "(클립 없음)";
        Debug.LogError($"[TutorialVideoUI] 영상 재생 실패 - '{clipName}': {message}");
    }

    // 준비 완료 로그. 이게 안 찍히면 디코딩 단계에서 막힌 것이고,
    //   찍히는데도 화면이 희면 출력(RenderTexture -> RawImage) 쪽 문제다. 둘을 가르는 유일한 신호라 남겨둔다.
    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (vp == null) return;
        Debug.Log($"[TutorialVideoUI] 준비 완료 - '{(vp.clip != null ? vp.clip.name : "?")}' "
                + $"{vp.width}x{vp.height} / renderMode={vp.renderMode} / source={vp.source} "
                + $"/ targetTexture={(vp.targetTexture != null ? vp.targetTexture.name : "null")} "   // 우리 RT 가 맞는지
                + $"/ timeUpdateMode={vp.timeUpdateMode} / timeScale={Time.timeScale}");
    }

    // 화면에 렌더하는 카메라 (RenderTexture 대상 제외) - 블러 소스
    private static Camera PickScreenCamera()
    {
        var main = Camera.main;
        if (main != null && main.targetTexture == null) return main;
        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (c.targetTexture != null) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best != null ? best : main;
    }
}
