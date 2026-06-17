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
/// UI/영상을 코드로 런타임 생성하므로 씬 세팅/프리팹 불필요 (lazy 싱글톤, TutorialOverlay 패턴).
/// TutorialVideoObjective 가 Show/Hide 를 호출한다.
///
/// 입력 차단: TutorialOverlay 와 동일하게 GameUIController.SetTutorialCoachActive(true) 로
/// 플레이어 이동/공격/스킬/J/C 를 막는다. 페이지 넘김(Q/E/화살표)은 이 컴포넌트가 raw Input 으로
/// 직접 읽으므로 코치 키보드 차단과 무관하게 동작한다.
/// </summary>
public class TutorialVideoUI : MonoBehaviour
{
    // ── 싱글턴 (lazy, 런타임 자동 생성) ───────────────────────────────
    private static TutorialVideoUI _i;
    public static bool HasInstance => _i != null;
    public static TutorialVideoUI I
    {
        get
        {
            if (_i == null && Application.isPlaying)
            {
                var go = new GameObject("[TutorialVideoUI]");
                _i = go.AddComponent<TutorialVideoUI>();
            }
            return _i;
        }
    }

    // ── UI 요소 ───────────────────────────────────────────────────────
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private BlurredImage _blur;
    private RawImage _videoImage;
    private GameObject _placeholder;
    private TMP_Text _pageCount;   // "1 / 2"
    private TMP_Text _title;       // "설비 가공"
    private TMP_Text _body;
    private GameObject _hintRow;      // [Q][E] 키캡 + 설명 (키캡은 화살표 옆과 동일 디자인)
    private GameObject _confirmBar;   // 마지막/단일 페이지에서 뜨는 "확인" 바
    private GameObject _chevronL, _chevronR;   // 화살표 아이콘(코드 생성) 컨테이너

    // ── 영상 ──────────────────────────────────────────────────────────
    private VideoPlayer _vp;
    private RenderTexture _rt;

    // ── 상태 ──────────────────────────────────────────────────────────
    private VideoTutorialPage[] _pages;
    private int _page;
    private int _maxSeen;          // 도달한 최대 페이지 (이 값이 마지막이면 닫기 허용)
    private Action _onClosed;
    private bool _open;
    private bool _suppressed;      // 설정창(ESC)이 위에 떠서 잠시 숨김 — 닫히면 복귀
    private float _shownTime;
    private float _resumeTimeScale = 1f;   // 팝업 닫을 때 복원할 timeScale

    private const float CloseCooldown = 0.35f;     // 표시 직후 잔여 입력으로 즉시 닫히는 것 방지
    private const int   VideoW = 1280, VideoH = 720;

    // ── 배경 연출 조절값 (여기 숫자만 바꾸면 됨) ─────────────────────
    // "기존 화면 위에 살짝 떠오르는" 느낌 목표 = 약한 블러 + 옅은 어둡기.
    // 더 약하게: BlurStrength/DimAlpha 낮추기. 더 세게: 올리기. (인벤 패널 블러는 6)
    private const float BlurStrength       = 2.2f;  // 블러 강도(낮을수록 배경 또렷). 화면 전환 느낌 나면 더 낮춰라.
    private const int   BlurIterations     = 3;     // 반복(낮을수록 덜 뭉갬)
    private const float BlurSampleDistance = 1.1f;  // 샘플 간격
    private const float DimAlpha           = 0.28f; // 블러 위 어둡기(낮을수록 기존 화면 느낌)

    private static readonly Color DimColor    = new Color(0f, 0f, 0f, DimAlpha);   // 블러 위 살짝 어둡게(시선 집중)
    private static readonly Color AccentColor = new Color(1f, 0.85f, 0.2f, 1f);    // 금색 액센트(페이지표시/확인바/키캡). 흰색은 영상 테두리에만.
    private static readonly Color ArrowColor  = new Color(0.93f, 0.96f, 1f, 0.92f);// 화살표(사진2식 밝은 톤)
    private static readonly Color LineColor   = new Color(1f, 1f, 1f, 0.18f);   // divider 선
    private static Sprite _ringSprite;     // 코드 생성 원형 링(화살표 테두리)
    private static Sprite _keycapSprite;   // 기존 게임 키캡 스프라이트(Resources/Image/UI_Icon/HUD/Keycap)
    private static bool   _keycapTried;

    private static Sprite _chevronSprite;   // 코드 생성 셰브론(공유)

    private bool AllRead => _pages != null && _maxSeen >= _pages.Length - 1;

    private void Awake()
    {
        if (_i != null && _i != this) { Destroy(gameObject); return; }
        _i = this;
        BuildUI();
        SetupVideo();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // 팝업이 열린 채 파괴(씬 전환 등)되면 게임이 timeScale=0 으로 멈춘 채 남지 않도록 복원.
        if (_open && Time.timeScale == 0f) Time.timeScale = _resumeTimeScale;
        if (_i == this) _i = null;
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

        // 블러가 현재 화면 카메라를 보도록 재연결
        if (_blur != null)
        {
            _blur.Common.cameraReference = PickScreenCamera();
            _blur.Common.ValidateBlur();
        }

        GameUIController.Instance?.SetTutorialCoachActive(true);   // 이동/공격/J·C 등 차단
        SetVisible(true);
        RenderPage();
    }

    public void Hide()
    {
        bool wasOpen = _open;
        _open = false;
        _suppressed = false;
        if (_vp != null) _vp.Stop();
        if (wasOpen) Time.timeScale = _resumeTimeScale;   // 게임 재개
        GameUIController.Instance?.SetTutorialCoachActive(false);
        SetVisible(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    // GameObject 는 항상 활성으로 두고 Canvas/Raycaster 토글로 표시/숨김 처리한다.
    // (LateUpdate 가 계속 돌아 설정창 열고닫힘을 self-poll 로 감지해 복귀 — TutorialOverlay 와 동일)
    private void SetVisible(bool v)
    {
        if (_canvas != null) _canvas.enabled = v;
        if (_raycaster != null) _raycaster.enabled = v;
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
            if (_vp != null)
            {
                if (_suppressed) _vp.Pause();
                else if (CurrentClip() != null) _vp.Play();
            }
        }
        if (_suppressed) return;

        // 설정창이 닫히며 timeScale 을 1 로 되돌릴 수 있어, 팝업이 떠 있는 동안엔 0 으로 고정.
        if (Time.timeScale != 0f) Time.timeScale = 0f;

        // 페이지 넘김: Q/E + 좌우 화살표 (raw Input — 코치 키보드 차단과 무관)
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow)) GoNext();
        else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow)) GoPrev();
        // 다 읽었으면 Space/Enter/좌클릭으로 닫기 ("확인")
        else if (AllRead && CanClose() && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)))
            Close();
    }

    private bool CanClose() => Time.unscaledTime - _shownTime > CloseCooldown;

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
        Hide();
        cb?.Invoke();
    }

    private VideoClip CurrentClip()
        => (_pages != null && _page >= 0 && _page < _pages.Length) ? _pages[_page].clip : null;

    private void RenderPage()
    {
        if (_pages == null || _pages.Length == 0) return;
        var p = _pages[_page];
        int n = _pages.Length;

        if (_pageCount != null)
        {
            _pageCount.text = n > 1 ? $"{_page + 1} / {n}" : string.Empty;
            _pageCount.gameObject.SetActive(n > 1);
        }
        if (_title != null) _title.text = p.title ?? string.Empty;
        if (_body != null) _body.text = p.body ?? string.Empty;

        // 영상 / 플레이스홀더
        if (p.clip != null)
        {
            if (_placeholder != null) _placeholder.SetActive(false);
            if (_videoImage != null) _videoImage.enabled = true;
            if (_vp != null)
            {
                if (_vp.clip != p.clip) _vp.clip = p.clip;
                _vp.Play();
            }
        }
        else
        {
            if (_vp != null) _vp.Stop();
            if (_videoImage != null) _videoImage.enabled = false;
            if (_placeholder != null) _placeholder.SetActive(true);
        }

        // 셰브론(양끝, 갈 수 있는 방향만 진하게). 페이지 1개면 숨김.
        //  1/2 = Q(이전) 못 쓰니 왼쪽 흐리게 / 오른쪽(E,다음) 진하게. 마지막은 반대.
        SetChevron(_chevronL, n > 1, _page > 0);
        SetChevron(_chevronR, n > 1, _page < n - 1);

        // 하단: 닫을 수 있는 마지막(또는 단일) 페이지면 "확인" 바, 아니면 페이지 넘김 안내.
        bool showConfirm = AllRead && _page == n - 1;
        if (_confirmBar != null) _confirmBar.SetActive(showConfirm);
        if (_hintRow != null) _hintRow.SetActive(!showConfirm);
    }

    private static void SetChevron(GameObject chevron, bool visible, bool enabledDir)
    {
        if (chevron == null) return;
        chevron.SetActive(visible);
        var cg = chevron.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = enabledDir ? 1f : 0.25f;
    }

    // ── 영상 세팅 ──────────────────────────────────────────────────────

    private void SetupVideo()
    {
        _rt = new RenderTexture(VideoW, VideoH, 0, RenderTextureFormat.ARGB32) { name = "TutorialVideoRT" };
        _rt.filterMode = FilterMode.Bilinear;
        _rt.Create();

        _vp = gameObject.AddComponent<VideoPlayer>();
        _vp.playOnAwake       = false;
        _vp.source            = VideoSource.VideoClip;
        _vp.renderMode        = VideoRenderMode.RenderTexture;
        _vp.targetTexture     = _rt;
        _vp.isLooping         = true;
        _vp.waitForFirstFrame = true;
        _vp.skipOnDrop        = true;
        _vp.audioOutputMode   = VideoAudioOutputMode.None;   // 무음 데모

        if (_videoImage != null) _videoImage.texture = _rt;
    }

    // ── UI 생성 ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _raycaster = gameObject.AddComponent<GraphicRaycaster>();

        var root = (RectTransform)transform;

        // 1) 전체화면 블러 (게임 화면을 통째로 뭉갬 — 시선을 영상으로). 별도 GameObject(Graphic 1개 제한).
        var blurGo = new GameObject("Blur", typeof(RectTransform));
        blurGo.transform.SetParent(root, false);
        Stretch((RectTransform)blurGo.transform);
        _blur = blurGo.AddComponent<BlurredImage>();
        ConfigureBlur(_blur);

        // 2) 블러 위 어두운 틴트(시선 집중 + 텍스트 가독). 뒤 클릭 차단도 담당.
        var dim = NewImage("Dim", root, DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 3) 콘텐츠 (투명 — 패널 박스 없음, 블러 위에 요소만)
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root, false);
        var cr = (RectTransform)content.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(960f, 820f);
        cr.anchoredPosition = Vector2.zero;

        var frameSprite = Resources.Load<Sprite>("tutorial/1");

        // 영상 프레임 (상단 16:9)
        var videoFrame = NewImage("VideoFrame", cr, new Color(0f, 0f, 0f, 1f));
        var vf = videoFrame.rectTransform;
        vf.anchorMin = vf.anchorMax = new Vector2(0.5f, 1f);
        vf.pivot = new Vector2(0.5f, 1f);
        vf.sizeDelta = new Vector2(872f, 491f);
        vf.anchoredPosition = new Vector2(0f, -6f);

        _videoImage = NewRawImage("VideoImage", vf);
        SetInset(_videoImage.rectTransform, 5f);
        _videoImage.texture = _rt;

        // 영상 테두리 = 인벤 영역 강조 프레임(흰색 9-slice, 빌더가 Resources/TutorialVideo/vid_frame 로 복사).
        // 없으면 sci-fi tutorial/1 로 폴백. 영상보다 살짝 바깥으로 빼서 프레임이 감싸게.
        // 액자처럼 영상 가장자리에 딱 붙게: @2x 슬롯프레임 -> ppu 2(코너 22px) + 7px 바깥 outset (인벤 방식).
        var invFrame = Resources.Load<Sprite>("TutorialVideo/vid_frame");
        var vborder = NewImage("VideoBorder", vf, Color.white);
        SetInset(vborder.rectTransform, -7f);   // 영상보다 7px 바깥
        if (invFrame != null)
        {
            vborder.sprite = invFrame;
            vborder.type = Image.Type.Sliced;
            vborder.pixelsPerUnitMultiplier = 2f;
        }
        else if (frameSprite != null)
        {
            vborder.sprite = frameSprite;
            vborder.type = Image.Type.Sliced;
            vborder.pixelsPerUnitMultiplier = 4f;
        }

        // 플레이스홀더 (clip 없을 때)
        _placeholder = NewImage("Placeholder", vf, new Color(0.08f, 0.1f, 0.14f, 1f)).gameObject;
        SetInset((RectTransform)_placeholder.transform, 5f);
        var ph = NewText("PlaceholderText", _placeholder.transform);
        Stretch(ph.rectTransform);
        ph.alignment = TextAlignmentOptions.Center;
        ph.fontSize = 26f;
        ph.color = new Color(1f, 1f, 1f, 0.5f);
        ph.text = "영상 준비 중";
        _placeholder.SetActive(false);

        // 페이지 표시 "1 / 2"
        _pageCount = NewText("PageCount", cr);
        TopCentered(_pageCount.rectTransform, 380f, 28f, -515f);
        _pageCount.alignment = TextAlignmentOptions.Center;
        _pageCount.fontSize = 20f;
        _pageCount.color = AccentColor;

        // 제목 "설비 가공"
        _title = NewText("Title", cr);
        TopCentered(_title.rectTransform, 872f, 46f, -543f);
        _title.alignment = TextAlignmentOptions.Center;
        _title.fontSize = 32f;
        _title.fontStyle = FontStyles.Bold;
        _title.color = Color.white;

        // divider (제목 / 본문 구분)
        var div1 = NewImage("Divider", cr, LineColor);
        TopCentered(div1.rectTransform, 760f, 2f, -594f);

        // 본문
        _body = NewText("Body", cr);
        TopCentered(_body.rectTransform, 820f, 150f, -610f);
        _body.alignment = TextAlignmentOptions.TopLeft;
        _body.fontSize = 22f;
        _body.enableWordWrapping = true;
        _body.color = new Color(0.92f, 0.94f, 0.98f, 1f);

        // 하단 힌트 (콘텐츠 맨 아래) = [Q][E] 키캡 + 설명. 키캡은 화살표 옆과 동일 디자인.
        _hintRow = BuildHintRow(cr);

        // "확인" 바 (마지막/단일 페이지 = 닫기 가능할 때만 표시). 사진2 처럼, 단 흰색 톤.
        _confirmBar = BuildConfirmBar(cr, frameSprite);
        _confirmBar.SetActive(false);

        // 화살표(셰브론) — 화면 좌우 가장자리, 영상 높이쯤. 키캡 Q/E 함께.
        _chevronL = BuildChevron(root, false, "Q");
        _chevronR = BuildChevron(root, true, "E");
    }

    // "확인" 바 (사진2 스타일, 흰색 톤). 마우스 커서는 코치 중 숨김이라 키(E/Space)로 닫고, 이건 시각 표시.
    private GameObject BuildConfirmBar(RectTransform parent, Sprite frameSprite)
    {
        var go = new GameObject("ConfirmBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(320f, 56f);
        rt.anchoredPosition = new Vector2(0f, 6f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.10f);   // 옅은 흰 바
        bg.raycastTarget = false;

        if (frameSprite != null)
        {
            var border = NewImage("ConfirmFrame", rt, AccentColor);
            Stretch(border.rectTransform);
            border.sprite = frameSprite;
            border.type = Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = 4f;
        }

        var label = NewText("ConfirmLabel", rt);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.text = "확인";

        return go;
    }

    // 화살표 = 사진2 식: 원형 링 안에 셰브론, 그 바깥 옆에 키 알파벳(Q/E).
    // right=true 면 오른쪽(>, 다음/E), false 면 왼쪽(<, 이전/Q).
    private GameObject BuildChevron(RectTransform parent, bool right, string keyLabel)
    {
        float s = right ? 1f : -1f;
        var go = new GameObject(right ? "ChevronR" : "ChevronL", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        // 화면 가장자리가 아니라 영상 가장자리 바로 옆(중앙 기준). 영상 반폭 ~436 -> 495에 두면 링이 영상에 붙음.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 120f);
        rt.anchoredPosition = new Vector2(s * 558f, 155f);   // 영상(반폭~436)에서 충분히 띄움
        go.AddComponent<CanvasGroup>();

        float ringX = -s * 22f;   // 링 = 영상쪽(안쪽)
        float keyX  =  s * 54f;   // 키캡 = 바깥쪽 (링과 살짝 띄움)

        // 원형 링
        var ring = NewImage("Ring", rt, ArrowColor);
        ring.sprite = GetRingSprite();
        ring.type = Image.Type.Simple;
        CenterAt(ring.rectTransform, new Vector2(76f, 76f), new Vector2(ringX, 0f));

        // 셰브론 (링 안)
        var icon = NewImage("Icon", rt, ArrowColor);
        icon.sprite = GetChevronSprite();
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        CenterAt(icon.rectTransform, new Vector2(28f, 40f), new Vector2(ringX, 0f));
        icon.rectTransform.localScale = new Vector3(s, 1f, 1f);   // 왼쪽이면 좌우반전

        // 키캡 (링 바깥쪽, 살짝 띄워서) — 화살표 옆/힌트 동일 디자인
        var kc = BuildKeycapGO(rt, keyLabel, 46f);
        CenterAt((RectTransform)kc.transform, new Vector2(46f, 46f), new Vector2(keyX, 0f));

        return go;
    }

    // 하단 힌트 행: [Q 키캡][E 키캡] 를 누르거나 ... (키캡은 화살표 옆과 동일 디자인). 가운데 정렬.
    private GameObject BuildHintRow(RectTransform parent)
    {
        var go = new GameObject("HintRow", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 6f);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 7f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildKeycapGO(rt, "Q", 28f);
        BuildKeycapGO(rt, "E", 28f);

        var txt = NewText("HintText", rt);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.fontSize = 17f;
        txt.enableWordWrapping = false;
        txt.color = new Color(0.72f, 0.76f, 0.82f, 1f);
        txt.text = "를 누르거나 좌우 화살표를 눌러 페이지를 넘길 수 있으며, 모두 읽은 후에는 창을 닫을 수 있습니다.";

        return go;
    }

    // 키캡 GO = 기존 게임 키캡 스프라이트(sci-fi 키캡) + 글자. 없으면 코드 박스 폴백.
    // 화살표 옆(CenterAt 으로 배치) / 힌트(HorizontalLayoutGroup) 양쪽에서 동일하게 사용.
    private GameObject BuildKeycapGO(Transform parent, string letter, float size)
    {
        var go = new GameObject("Keycap", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);

        var box = go.AddComponent<Image>();
        box.raycastTarget = false;
        var kc = GetKeycapSprite();
        if (kc != null)
        {
            box.sprite = kc;                 // 엔드필드식 키캡 (기존 게임 에셋)
            box.type = Image.Type.Simple;
            box.preserveAspect = true;
            box.color = Color.white;
        }
        else
        {
            box.color = new Color(0.85f, 0.9f, 1f, 0.9f);   // 폴백: 밝은 테두리
            var inner = NewImage("Inner", rt, new Color(0.10f, 0.12f, 0.16f, 0.95f));
            SetInset(inner.rectTransform, size * 0.06f);
        }

        var le = go.AddComponent<LayoutElement>();   // 힌트 HorizontalLayoutGroup 에서 크기 고정
        le.preferredWidth = size; le.preferredHeight = size;

        var t = NewText("Letter", rt);
        SetInset(t.rectTransform, size * 0.15f);     // 테두리 피해 가운데
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size * 0.46f;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.text = letter;
        return go;
    }

    private static Sprite GetKeycapSprite()
    {
        if (!_keycapTried) { _keycapSprite = Resources.Load<Sprite>("Image/UI_Icon/HUD/Keycap"); _keycapTried = true; }
        return _keycapSprite;
    }

    // 중앙(0.5,0.5) 앵커로 크기+오프셋 배치
    private static void CenterAt(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    // 원형 링 스프라이트 코드 생성 (속 빈 동그라미)
    private static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "TutorialRing" };
        var px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        float rOuter = S * 0.46f;
        float rInner = rOuter - 5f;   // 링 두께 5px

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(Mathf.Min(d - (rInner - 0.5f), (rOuter + 0.5f) - d));
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _ringSprite;
    }

    // ── 블러 설정 (인벤 InventoryUIBuilder 의 검증된 셋업 복제) ────────
    private static void ConfigureBlur(BlurredImage blur)
    {
        blur.color = Color.white;
        blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = PickScreenCamera();
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            bs.blurAdditionalDistancePerIteration = BlurStrength;   // 약하게 = 배경 알아볼 정도(화면 전환 느낌 방지)
            if (bs.blurSections != null)
                foreach (var sec in bs.blurSections) { sec.iterations = BlurIterations; sec.sampleDistance = BlurSampleDistance; }
            bs.vibrancy = 0f; bs.brightness = 0f; bs.contrast = 0f; bs.referenceResolution = 1080;
        }
        blur.Common.ValidateBlur();
    }

    // 화면에 렌더하는 카메라 (RenderTexture 대상 제외) — 블러 소스
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

    // ── 셰브론 스프라이트 코드 생성 ( ">" 모양, 왼쪽은 반전해서 사용 ) ──
    private static Sprite GetChevronSprite()
    {
        if (_chevronSprite != null) return _chevronSprite;

        const int W = 48, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "TutorialChevron" };
        var px = new Color32[W * H];

        float thick = 7f;                                   // 선 두께(px)
        var vtx    = new Vector2(W * 0.72f, H * 0.5f);      // 오른쪽 꼭짓점
        var topEnd = new Vector2(W * 0.20f, H * 0.92f);
        var botEnd = new Vector2(W * 0.20f, H * 0.08f);

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            var pt = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(DistToSegment(pt, vtx, topEnd), DistToSegment(pt, vtx, botEnd));
            float a = Mathf.Clamp01((thick * 0.5f - d) + 0.5f);   // 0.5px 정도 부드러운 경계
            px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        _chevronSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        return _chevronSprite;
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // ── UI 헬퍼 ───────────────────────────────────────────────────────

    private static Image NewImage(string name, Transform parent, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    private static RawImage NewRawImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetInset(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // 콘텐츠(중앙) 기준 상단정렬 배치: 폭/높이 + 상단에서의 y오프셋(음수)
    private static void TopCentered(RectTransform rt, float w, float h, float yFromTop)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, yFromTop);
    }
}
