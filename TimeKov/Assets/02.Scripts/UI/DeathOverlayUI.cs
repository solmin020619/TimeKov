// DeathOverlayUI.cs
// 시간(時) 컨셉 사망 화면 — "TIME OUT / 시간 소멸".
// 레퍼런스(빨간 DEFEAT 다이아몬드 글리치)를 계승하되, 전체 디자인/애니메이션을 런타임에 코드로 생성한다.
//   ─ 다크 백드롭 + 붉은 비네트 + CRT 스캔라인(시공 붕괴 느낌)
//   ─ 회전하는 붉은 다이아몬드 프레임(글리치 점멸)
//   ─ 크로마틱(적/청 분리) 글리치 타이틀 "TIME OUT"
//   ─ 시간 컨셉 문구(재동기화 카운트다운)
//   ─ "시간 되감기" 버튼(호버/누름 인터랙션: DeathRespawnButton)
// 프리팹 아트에 의존하지 않는다. 기존 자식(구 디자인)은 숨기고 새 트리로 대체.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeKov.UI;   // WindowManager / UILayer / WindowSortingSettings (오버레이 sortingOrder)

public class DeathOverlayUI : MonoBehaviour
{
    /// <summary>GameUIController.RefreshCursorState에서 커서 잠금 예외 처리에 사용</summary>
    public static bool IsOpen { get; private set; } = false;

    // ── 문구(시간 컨셉) ─────────────────────────────────────────────
    [Header("문구 (시간 컨셉)")]
    // 타이틀은 코드값 고정(씬 직렬화 "YOU DIED"가 덮지 않도록 SerializeField 제거) — 시간 컨셉 문구.
    private string titleString = "TIME OVER";
    [SerializeField] private string lossString      = "인벤토리의 아이템을 잃어버렸다…";
    [SerializeField] private string buttonString    = "시간 되감기";

    [Header("연출")]
    [SerializeField] private float fadeInDuration  = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    // ── 팔레트 ─────────────────────────────────────────────────────
    // 죽음 느낌 + 자연스러움: near-black 배경, 중성 다크, 절제된 dull 크림슨 액센트, 본화이트/스틸 텍스트.
    static readonly Color ColRed      = new Color32(0xA8, 0x3A, 0x38, 0xFF);   // dull 크림슨(타이틀/다이아몬드 액센트)
    static readonly Color ColRedDeep  = new Color32(0x2E, 0x12, 0x12, 0xFF);   // 깊은 적갈(글로우/딥)
    static readonly Color ColCyan     = new Color32(0x7F, 0xA3, 0xB0, 0xFF);   // 절제된 스틸(시간/재동기화 텍스트)
    static readonly Color ColText     = new Color32(0xDE, 0xD9, 0xD2, 0xFF);   // 본화이트 본문
    static readonly Color ColSub      = new Color32(0x8B, 0x90, 0x99, 0xFF);   // 쿨 그레이 보조
    static readonly Color ColNeutral  = new Color32(0x9A, 0x9E, 0xA6, 0xFF);   // 중성 라인(바/브래킷)

    // ── 런타임 참조(코드 생성) ─────────────────────────────────────
    private CanvasGroup _group;
    private TMP_FontAsset _font;

    private RectTransform _canvasRect;      // 풀스크린 레이어 크기 기준(루트 캔버스)
    private Image         _backdrop;
    private RectTransform _center;          // 다이아몬드 + 타이틀 묶음
    private RectTransform _diamond;
    private Image         _diamondImg;      // 다이아몬드 색/알파 제어용
    private RectTransform _diamond2;        // 내부 역회전 다이아몬드
    private Image         _diamond2Img;
    private Image         _timeArc;          // 시간 엠블럼: 남은 시간 진행 아크(카운트다운 연동)
    private Image         _glow;            // 타이틀 뒤 붉은 발광
    private Image         _vignette;
    private RawImage      _scan;            // CRT 스캔라인(uvRect 타일링 = 단일 쿼드)
    private RawImage      _grid;            // 배경 테크 그리드(uvRect 타일링)
    private RectTransform _glitchBar;       // 세로로 훑는 글리치 바
    private RectTransform _frame;           // 코너 브래킷용 풀스크린 프레임(비네트 위)
    private TMP_Text      _title, _titleR, _titleC;   // 메인 + 적/청 크로마틱 고스트
    private TMP_Text      _countdown;

    private Button        _respawnButton;
    private CanvasGroup   _buttonGroup;
    private DeathRespawnButton _buttonFx;

    // ── 상태 ───────────────────────────────────────────────────────
    private float   _countdown0;
    private float   _countTotal;            // 최초 리스폰 딜레이(진행 아크 비율 계산)
    private bool    _counting;
    private int     _lastSecs = -1;
    private float   _numPop;                // 카운트다운 숫자 팝
    private Action  _onRespawn;
    private Coroutine _fadeRoutine, _cursorRoutine, _btnRoutine;

    private bool    _built;
    private float   _t;                     // Show 이후 누적 시간(unscaled)
    private float   _nextGlitch;            // 다음 타이틀 글리치 시각
    private float   _glitch;                // 현재 글리치 강도(0~1, 감쇠)

    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        EnsureTopmostCanvas();
        Build();
        gameObject.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>사망 오버레이 표시. respawnDelay 후 버튼 활성화, 클릭 시 onRespawn 콜백.</summary>
    public void Show(float respawnDelay, Action onRespawn)
    {
        _onRespawn  = onRespawn;
        _countdown0 = respawnDelay;
        _countTotal = Mathf.Max(0.01f, respawnDelay);
        _counting   = true;
        if (_timeArc != null) _timeArc.fillAmount = 1f;
        _lastSecs   = -1;
        _numPop     = 0f;
        _t          = 0f;
        _nextGlitch = 0.9f;
        _glitch     = 1f;   // 등장 시 글리치 버스트

        IsOpen = true;
        gameObject.SetActive(true);

        SetButtonReady(false);
        if (_buttonFx != null) _buttonFx.ResetVisual();

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(1f, fadeInDuration));

        if (_cursorRoutine != null) StopCoroutine(_cursorRoutine);
        _cursorRoutine = StartCoroutine(ForceCursorWhileOpen());
    }

    /// <summary>오버레이 페이드 아웃 후 숨김.</summary>
    public void Hide()
    {
        _counting = false;
        IsOpen = false;

        if (_cursorRoutine != null) { StopCoroutine(_cursorRoutine); _cursorRoutine = null; }
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    // ── 업데이트(카운트다운 + 애니메이션) ─────────────────────────
    void Update()
    {
        if (!IsOpen) return;

        float dt = Time.unscaledDeltaTime;
        _t += dt;

        FitFullscreen();   // 레이아웃 확정/해상도 변화 후에도 풀스크린 유지
        UpdateCountdown(dt);
        AnimateIntro();
        AnimateIdle(dt);
    }

    void UpdateCountdown(float dt)
    {
        if (_numPop > 0f) _numPop = Mathf.Max(0f, _numPop - dt / 0.35f);

        // 남은 시간 아크(12시에서 시계방향으로 감소)
        if (_timeArc != null)
            _timeArc.fillAmount = _countTotal > 0.01f ? Mathf.Clamp01(_countdown0 / _countTotal) : 0f;

        if (!_counting) return;

        _countdown0 -= dt;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, _countdown0));

        if (secs != _lastSecs)
        {
            _lastSecs = secs;
            _numPop = 1f;   // 매 초 숫자 팝
            if (_countdown != null)
                _countdown.text =
                    $"RESYNC IN   <size=175%><color=#8FB6C2><b>{secs:00}</b></color></size>   SEC";
        }

        if (_countdown0 <= 0f)
        {
            _counting = false;
            if (_countdown != null)
                _countdown.text = "<color=#8FB6C2>RESYNC READY</color>";
            SetButtonReady(true);
        }
    }

    // 등장: 백드롭 페이드(그룹) + 타이틀/다이아몬드 스케일 인 + 초기 글리치
    void AnimateIntro()
    {
        float p = Mathf.Clamp01(_t / Mathf.Max(0.001f, fadeInDuration));
        float ease = 1f - Mathf.Pow(1f - p, 3f);   // easeOutCubic

        if (_title != null)
        {
            float s = Mathf.LerpUnclamped(1.35f, 1f, ease);
            SetScale(_title.rectTransform, s);
            SetScale(_titleR.rectTransform, s);
            SetScale(_titleC.rectTransform, s);
        }
        if (_diamond != null)
        {
            float ds = Mathf.LerpUnclamped(1.7f, 1f, ease);
            _diamond.localScale = new Vector3(ds, ds, 1f);
        }
    }

    // 상시: 스캔라인 점멸 · 비네트/글로우 펄스 · 다이아몬드 회전 · 타이틀 글리치
    void AnimateIdle(float dt)
    {
        // 붉은 글로우 펄스
        if (_glow != null)
        {
            float pulse = Mathf.Sin(_t * 2.4f) * 0.5f + 0.5f;
            var c = _glow.color; c.a = 0.28f + pulse * 0.16f; _glow.color = c;
            float gs = 1f + pulse * 0.06f;
            _glow.rectTransform.localScale = new Vector3(gs, gs, 1f);
        }

        // 비네트 미세 호흡
        if (_vignette != null)
        {
            float v = Mathf.Sin(_t * 1.3f) * 0.5f + 0.5f;
            var c = _vignette.color; c.a = 0.72f + v * 0.12f; _vignette.color = c;
        }

        // 스캔라인 살짝 흐르며 점멸(uvRect 스크롤)
        if (_scan != null)
        {
            var c = _scan.color; c.a = 0.10f + (Mathf.Sin(_t * 9f) * 0.5f + 0.5f) * 0.05f; _scan.color = c;
            var uv = _scan.uvRect; uv.y = (_t * 2.5f) % 1f; _scan.uvRect = uv;
        }

        // 다이아몬드: 45° 기준 미세 흔들림 + 펄스
        if (_diamond != null && _t > fadeInDuration)
        {
            float wob = Mathf.Sin(_t * 0.8f) * 3f + Mathf.Sin(_t * 3.7f) * (_glitch * 6f);
            _diamond.localRotation = Quaternion.Euler(0, 0, 45f + wob);
            float ds = 1f + Mathf.Sin(_t * 2.4f) * 0.02f + _glitch * 0.05f;
            _diamond.localScale = new Vector3(ds, ds, 1f);
            var dc = _diamondImg.color; dc.a = 0.72f - _glitch * 0.4f * (Mathf.Sin(_t * 40f) * 0.5f + 0.5f); _diamondImg.color = dc;
        }

        // 시간 아크: 은은한 밝기 펄스(감소는 UpdateCountdown이 fillAmount로 담당)
        if (_timeArc != null)
        {
            float ap = Mathf.Sin(_t * 2.2f) * 0.5f + 0.5f;
            var ac = _timeArc.color; ac.a = 0.75f + ap * 0.2f; _timeArc.color = ac;
        }

        // 내부 다이아몬드: 반대로 천천히 회전 + 펄스
        if (_diamond2 != null)
        {
            float wob2 = Mathf.Sin(_t * 0.6f + 1f) * 4f;
            _diamond2.localRotation = Quaternion.Euler(0, 0, 45f - wob2);
            float ds2 = 1f - Mathf.Sin(_t * 2.0f) * 0.03f;
            _diamond2.localScale = new Vector3(ds2, ds2, 1f);
        }

        // 글리치 바: 평소 숨김, 주기적으로 화면 위→아래로 빠르게 훑음
        if (_glitchBar != null)
        {
            var gimg = _glitchBar.GetComponent<Image>();
            float sweep = Mathf.Repeat(_t * 0.35f, 1f);   // 0~1 주기
            bool active = sweep < 0.18f;                   // 주기의 앞 18%만 표시(가끔 지나감)
            if (active && _canvasRect != null)
            {
                float h = (_canvasRect.rect.size.y * 0.5f + 20f);
                float y = Mathf.Lerp(h, -h, sweep / 0.18f);
                var ap = _glitchBar.anchoredPosition; ap.y = y; _glitchBar.anchoredPosition = ap;
                if (gimg != null) { var gc = gimg.color; gc.a = 0.25f + UnityEngine.Random.value * 0.25f; gimg.color = gc; }
            }
            else if (gimg != null) { var gc = gimg.color; gc.a = 0f; gimg.color = gc; }
        }

        // 타이틀 크로마틱 글리치
        _glitch = Mathf.Max(0f, _glitch - dt / 0.35f);
        if (_t >= _nextGlitch)
        {
            _glitch = 1f;
            _nextGlitch = _t + UnityEngine.Random.Range(1.4f, 3.2f);
        }
        if (_title != null)
        {
            float jx = (UnityEngine.Random.value - 0.5f) * 10f * _glitch;
            float split = 3f + _glitch * 14f;
            _titleR.rectTransform.anchoredPosition = new Vector2(-split + jx, 0f);
            _titleC.rectTransform.anchoredPosition = new Vector2(split + jx, 0f);
            _title.rectTransform.anchoredPosition  = new Vector2(jx * 0.4f, 0f);

            float flick = (_glitch > 0.05f && UnityEngine.Random.value < 0.22f) ? 0.65f : 1f;
            SetAlpha(_title, flick);
            SetAlpha(_titleR, 0.18f + _glitch * 0.35f);
            SetAlpha(_titleC, 0.18f + _glitch * 0.35f);
        }

        // 카운트다운 숫자 팝(스케일)
        if (_countdown != null)
        {
            float s = 1f + _numPop * 0.14f;
            SetScale(_countdown.rectTransform, s);
        }
    }

    // ── 내부: 상태/버튼 ──────────────────────────────────────────
    void OnRespawnClicked()
    {
        SetButtonReady(false);
        _onRespawn?.Invoke();
    }

    void SetButtonReady(bool ready)
    {
        if (_respawnButton != null) _respawnButton.interactable = ready;
        if (_buttonGroup == null) return;

        if (_btnRoutine != null) StopCoroutine(_btnRoutine);
        if (ready)
        {
            _buttonGroup.interactable   = true;
            _buttonGroup.blocksRaycasts = true;
            _btnRoutine = StartCoroutine(FadeGroup(_buttonGroup, 1f, 0.35f));
        }
        else
        {
            _buttonGroup.interactable   = false;
            _buttonGroup.blocksRaycasts = false;
            _buttonGroup.alpha = 0f;
        }
    }

    IEnumerator FadeGroup(CanvasGroup g, float target, float dur)
    {
        float start = g.alpha, e = 0f;
        while (e < dur) { e += Time.unscaledDeltaTime; g.alpha = Mathf.Lerp(start, target, e / dur); yield return null; }
        g.alpha = target;
    }

    IEnumerator ForceCursorWhileOpen()
    {
        var wait = new WaitForEndOfFrame();
        while (IsOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            yield return wait;
        }
    }

    IEnumerator FadeTo(float target, float duration)
    {
        if (_group == null) yield break;
        float start = _group.alpha, e = 0f;
        while (e < duration) { e += Time.unscaledDeltaTime; _group.alpha = Mathf.Lerp(start, target, e / duration); yield return null; }
        _group.alpha = target;

        bool fadedIn = target >= 1f;
        _group.interactable   = fadedIn;
        _group.blocksRaycasts = fadedIn;
    }

    IEnumerator FadeOutRoutine()
    {
        yield return FadeTo(0f, fadeOutDuration);
        gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 생성(절차적)
    // ═══════════════════════════════════════════════════════════════

    void Build()
    {
        if (_built) return;
        _built = true;

        _font = FindFont();

        // 기존(구 디자인) 자식 숨김 — 파괴하지 않고 비활성화해 새 트리로 대체.
        for (int i = transform.childCount - 1; i >= 0; i--)
            transform.GetChild(i).gameObject.SetActive(false);

        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        if (transform is RectTransform selfRt) Stretch(selfRt);   // 루트를 풀스크린으로 시도
        _canvasRect = (GetComponentInParent<Canvas>()?.rootCanvas)?.transform as RectTransform;

        var root = NewRect("ProcRoot", transform);
        Stretch(root);

        // 1) 다크 백드롭(레이캐스트 차단 = 모달) — 캔버스 크기에 직접 맞춤(FitFullscreen).
        //    (루트 rect가 풀스크린이 아니어도 화면 전체를 확실히 덮게 함)
        _backdrop = NewImage("Backdrop", root);
        CenterFull(_backdrop.rectTransform);
        _backdrop.color = new Color(0.015f, 0.02f, 0.03f, 0.88f);
        _backdrop.raycastTarget = true;

        // 1b) 배경 테크 그리드(아주 옅게) — HUD 느낌
        var gridRt = NewRect("Grid", root);
        CenterFull(gridRt);
        _grid = gridRt.gameObject.AddComponent<RawImage>();
        _grid.texture = MakeGrid(38);
        _grid.raycastTarget = false;
        _grid.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.022f);

        // 2) 비네트(가장자리 어둡게) — 중성 블랙에 아주 옅은 적갈만 섞어 무겁게
        _vignette = NewImage("Vignette", root);
        CenterFull(_vignette.rectTransform);
        _vignette.sprite = SpriteOf(MakeVignette(128));
        _vignette.color = new Color(0.03f, 0.02f, 0.02f, 0.9f);

        // 3) CRT 스캔라인 — RawImage + uvRect 로 세로 타일링(단일 쿼드, 정점 폭증 없음)
        var scanRt = NewRect("Scanlines", root);
        CenterFull(scanRt);
        _scan = scanRt.gameObject.AddComponent<RawImage>();
        _scan.texture = MakeScanline();
        _scan.raycastTarget = false;
        _scan.uvRect = new Rect(0f, 0f, 1f, 220f);   // 세로로 220회 반복 → 촘촘한 스캔라인
        _scan.color = new Color(0f, 0f, 0f, 0.14f);

        // 3b) 세로로 훑는 글리치 스캔 바(가끔 지직 하고 지나감)
        _glitchBar = NewRect("GlitchBar", root);
        CenterFull(_glitchBar);
        _glitchBar.sizeDelta = new Vector2(2000f, 3f);
        var gb = _glitchBar.gameObject.AddComponent<Image>();
        gb.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0f);   // 절제된 스틸 스캔
        gb.raycastTarget = false;

        // 화면 네 모서리 HUD 브래킷(시스템 경보 느낌) — 전용 풀스크린 프레임(비네트/스캔 위)에 코너 고정
        _frame = NewRect("Frame", root);
        CenterFull(_frame);
        BuildCornerBrackets(_frame);
        BuildHudFrame(_frame);

        // 4) 중앙 묶음(다이아몬드 + 타이틀) — 화면 상단쪽 클러스터(위로 이동)
        _center = NewRect("Center", root);
        Center(_center, 1000f, 460f, new Vector2(0f, 185f));

        // 4a) 은은한 글로우(깊은 적갈, 아주 옅게) — 타이틀에 무게감만
        _glow = NewImage("Glow", _center);
        Center(_glow.rectTransform, 560f, 560f, Vector2.zero);
        _glow.sprite = SpriteOf(MakeRadial(128));
        _glow.color = new Color(ColRedDeep.r, ColRedDeep.g, ColRedDeep.b, 0.55f);

        // 4b) 다이아몬드 프레임(정사각 아웃라인을 45° 회전) — 얇고 절제된 dull 크림슨
        _diamond = NewRect("Diamond", _center);
        Center(_diamond, 320f, 320f, Vector2.zero);
        _diamondImg = _diamond.gameObject.AddComponent<Image>();
        _diamondImg.sprite = SpriteOf(MakeSquareOutline(160, 4));
        _diamondImg.type = Image.Type.Simple;
        _diamondImg.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.8f);
        _diamondImg.raycastTarget = false;
        _diamond.localRotation = Quaternion.Euler(0, 0, 45f);

        // 4b-2) 내부 역회전 다이아몬드(더 얇게, 더 옅게) — 깊이감
        _diamond2 = NewRect("Diamond2", _center);
        Center(_diamond2, 250f, 250f, Vector2.zero);
        _diamond2Img = _diamond2.gameObject.AddComponent<Image>();
        _diamond2Img.sprite = SpriteOf(MakeSquareOutline(160, 2));
        _diamond2Img.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.28f);
        _diamond2Img.raycastTarget = false;
        _diamond2.localRotation = Quaternion.Euler(0, 0, 45f);

        // 4b-3) 타이틀 양옆 액센트 바(중성 그레이, 짧게)
        MakeBar(_center, new Vector2(-300f, 0f));
        MakeBar(_center, new Vector2( 300f, 0f));

        // 4c) 타이틀 크로마틱(스틸 → 적 → 메인 순서로 겹침) — 메인은 dull 크림슨
        _titleC = NewText("TitleSteel", _center, titleString, 120f, new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.28f), FontStyles.Bold, TextAlignmentOptions.Center);
        Center(_titleC.rectTransform, 1000f, 190f, Vector2.zero);
        _titleR = NewText("TitleRed", _center, titleString, 120f, new Color(ColRed.r, ColRed.g, ColRed.b, 0.3f), FontStyles.Bold, TextAlignmentOptions.Center);
        Center(_titleR.rectTransform, 1000f, 190f, Vector2.zero);
        _title  = NewText("Title", _center, titleString, 120f, ColRed, FontStyles.Bold, TextAlignmentOptions.Center);
        Center(_title.rectTransform, 1000f, 190f, Vector2.zero);
        _title.characterSpacing = 8f;
        _titleR.characterSpacing = 8f;
        _titleC.characterSpacing = 8f;

        // 4d-2) 타이틀 키커(작은 라벨) — 타이틀 위, 스틸
        var kicker = NewText("Kicker", _center, "SYNC LOST · 시간 소멸", 18f, new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.6f), FontStyles.Bold, TextAlignmentOptions.Center);
        Center(kicker.rectTransform, 1000f, 28f, new Vector2(0f, 74f));
        kicker.characterSpacing = 18f;

        // 4e) 시간 엠블럼 — 타이틀 위 빈 공간(크레스트). 남은 시간이 줄어드는 링(카운트다운 연동).
        BuildTimeEmblem(_center);

        // 5~6) 아이템 드롭 안내 — 위·아래 다이아몬드 캡 라인으로 감싸 캡션처럼
        MakeCapLine(root, 380f, new Vector2(0f, -80f));

        var loss = NewText("Loss", root, lossString, 24f, ColText, FontStyles.Normal, TextAlignmentOptions.Center);
        Center(loss.rectTransform, 1200f, 34f, new Vector2(0f, -112f));

        MakeCapLine(root, 380f, new Vector2(0f, -144f));

        // 7) 카운트다운
        _countdown = NewText("Countdown", root, "", 26f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center);
        _countdown.richText = true;
        Center(_countdown.rectTransform, 1000f, 44f, new Vector2(0f, -235f));

        // 7) 버튼
        BuildButton(root);

        FitFullscreen();
    }

    // 풀스크린 레이어(백드롭/비네트/스캔/글리치 바)를 루트 캔버스 크기에 맞춤.
    // 루트 rect가 풀스크린이 아니어도 화면 전체를 확실히 덮게 하고, 해상도 변화도 추종.
    void FitFullscreen()
    {
        Vector2 size = (_canvasRect != null && _canvasRect.rect.size.sqrMagnitude > 1f)
            ? _canvasRect.rect.size
            : new Vector2(Screen.width, Screen.height);
        size += new Vector2(4f, 4f);   // 가장자리 여유

        if (_backdrop  != null) _backdrop.rectTransform.sizeDelta  = size;
        if (_vignette  != null) _vignette.rectTransform.sizeDelta  = size;
        if (_scan      != null) _scan.rectTransform.sizeDelta      = size;
        if (_frame     != null) _frame.sizeDelta     = size;
        if (_glitchBar != null) _glitchBar.sizeDelta = new Vector2(size.x, _glitchBar.sizeDelta.y);
        if (_grid      != null)
        {
            _grid.rectTransform.sizeDelta = size;
            _grid.uvRect = new Rect(0f, 0f, size.x / 38f, size.y / 38f);
        }
    }

    // 타이틀 옆 액센트 바 + 안쪽 끝 다이아몬드 캡
    void MakeBar(Transform parent, Vector2 pos)
    {
        var bar = NewImage("Accent", parent);
        Center(bar.rectTransform, 140f, 2f, pos);
        bar.color = new Color(ColNeutral.r, ColNeutral.g, ColNeutral.b, 0.4f);

        float innerSign = pos.x < 0f ? 1f : -1f;   // 중앙(타이틀) 쪽 끝
        var cap = NewImage("AccentCap", parent);
        Center(cap.rectTransform, 9f, 9f, pos + new Vector2(innerSign * 70f, 0f));
        cap.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
        cap.sprite = SpriteOf(MakeSquareOutline(32, 3));
        cap.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.5f);
    }

    // 중성 회색 가로 선 + 양끝 다이아몬드 캡(캡션 프레임용)
    void MakeCapLine(Transform parent, float width, Vector2 pos)
    {
        var l = NewImage("Line", parent);
        Center(l.rectTransform, width, 2f, pos);
        l.color = new Color(ColNeutral.r, ColNeutral.g, ColNeutral.b, 0.24f);

        for (int s = -1; s <= 1; s += 2)
        {
            var cap = NewImage("LineCap", parent);
            Center(cap.rectTransform, 8f, 8f, pos + new Vector2(s * width * 0.5f, 0f));
            cap.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            cap.sprite = SpriteOf(MakeSquareOutline(32, 3));
            cap.color = new Color(ColNeutral.r, ColNeutral.g, ColNeutral.b, 0.4f);
        }
    }

    // HUD 프레임: 인셋 테두리 + 모서리 라벨 + 좌우 사이드 눈금.
    void BuildHudFrame(RectTransform fullRect)
    {
        var border = NewImage("HudBorder", fullRect);
        var br = border.rectTransform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(28f, 28f); br.offsetMax = new Vector2(-28f, -28f);
        border.sprite = SlicedOutline(64, 1);
        border.type = Image.Type.Sliced;
        border.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.16f);
        border.raycastTarget = false;

        HudLabel(fullRect, "SYS_STATUS: TERMINATED", new Vector2(0f, 1f), new Vector2( 70f, -62f), TextAlignmentOptions.TopLeft,     new Color(ColRed.r,  ColRed.g,  ColRed.b,  0.6f));
        HudLabel(fullRect, "CHRONO SYNC LOST",       new Vector2(1f, 1f), new Vector2(-70f, -62f), TextAlignmentOptions.TopRight,    new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.4f));
        HudLabel(fullRect, "LOC 47.2 / -13.8",       new Vector2(0f, 0f), new Vector2( 70f,  62f), TextAlignmentOptions.BottomLeft,  new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.32f));
        HudLabel(fullRect, "REWIND // STANDBY",      new Vector2(1f, 0f), new Vector2(-70f,  62f), TextAlignmentOptions.BottomRight, new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.32f));

        SideTicks(fullRect, -1f);
        SideTicks(fullRect,  1f);
    }

    void HudLabel(RectTransform parent, string text, Vector2 anchor, Vector2 offset, TextAlignmentOptions align, Color col)
    {
        var t = NewText("HudLabel", parent, text, 15f, col, FontStyles.Bold, align);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = new Vector2(420f, 24f);
        rt.anchoredPosition = offset;
        t.characterSpacing = 6f;
    }

    void SideTicks(RectTransform parent, float side)   // side: -1 좌, +1 우
    {
        Vector2 anchor = new Vector2(side < 0f ? 0f : 1f, 0.5f);
        float[] lens = { 14f, 8f, 14f, 8f, 20f, 8f, 14f };
        float y0 = (lens.Length - 1) * 0.5f * 8f;
        for (int i = 0; i < lens.Length; i++)
        {
            var b = NewImage("SideTick", parent);
            var rt = b.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = new Vector2(lens[i], 2f);
            rt.anchoredPosition = new Vector2(side * 40f, y0 - i * 8f);
            b.color = (i == 4)
                ? new Color(ColRed.r, ColRed.g, ColRed.b, 0.5f)
                : new Color(ColCyan.r, ColCyan.g, ColCyan.b, (i % 2 == 0) ? 0.38f : 0.22f);
        }
    }

    // 시간 엠블럼(크로노 게이지): 후광 + 외곽 프레임 + 세그먼트 게이지(남은 시간 밝게/지난 시간 어둡게)
    // + 안쪽 미세눈금 링 + 중심 코어(링+점) + 12시 크림슨 눈금. 카운트다운과 연동.
    void BuildTimeEmblem(Transform parent)
    {
        var em = NewRect("TimeRing", parent);
        Center(em, 84f, 84f, new Vector2(0f, 210f));   // 다이아몬드 위 크레스트(정중앙 상단)

        // 후광
        var halo = NewImage("Halo", em);
        Center(halo.rectTransform, 140f, 140f, Vector2.zero);
        halo.sprite = SpriteOf(MakeRadial(128));
        halo.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.13f);

        // 외곽 프레임 링(얇게)
        var frameRing = NewImage("FrameRing", em);
        Stretch(frameRing.rectTransform);
        frameRing.sprite = SpriteOf(MakeRing(256, 3f));
        frameRing.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.42f);

        var seg = SpriteOf(MakeSegmentedRing(256, 22f, 20, 7f));   // 볼드 세그먼트(트랙/아크 공용)

        // 세그먼트 트랙(전체, 어둡게)
        var track = NewImage("SegTrack", em);
        Center(track.rectTransform, 74f, 74f, Vector2.zero);
        track.sprite = seg;
        track.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.2f);

        // 세그먼트 진행(남은 시간, 밝게) — 12시에서 시계방향 감소
        _timeArc = NewImage("SegArc", em);
        Center(_timeArc.rectTransform, 74f, 74f, Vector2.zero);
        _timeArc.sprite = seg;
        _timeArc.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.95f);
        _timeArc.type = Image.Type.Filled;
        _timeArc.fillMethod = Image.FillMethod.Radial360;
        _timeArc.fillOrigin = (int)Image.Origin360.Top;
        _timeArc.fillClockwise = true;
        _timeArc.fillAmount = 1f;

        // 안쪽 미세눈금 링
        var minor = NewImage("MinorTicks", em);
        Center(minor.rectTransform, 45f, 45f, Vector2.zero);
        minor.sprite = SpriteOf(MakeSegmentedRing(128, 4f, 36, 5f));
        minor.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.35f);

        // 중심 코어(작은 링)
        var core = NewImage("Core", em);
        Center(core.rectTransform, 26f, 26f, Vector2.zero);
        core.sprite = SpriteOf(MakeRing(96, 3f));
        core.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.5f);

        // 중심점(크림슨)
        var dot = NewImage("CoreDot", em);
        Center(dot.rectTransform, 6f, 6f, Vector2.zero);
        dot.sprite = SpriteOf(MakeRadial(32));
        dot.color = ColRed;

        // 12시 크림슨 눈금
        var top = NewImage("TopTick", em);
        Center(top.rectTransform, 3f, 13f, new Vector2(0f, 36f));
        top.color = ColRed;
    }

    // 화면 네 모서리 L자 브래킷(HUD 경보 프레임). fullRect(풀스크린) 기준으로 코너에 고정.
    void BuildCornerBrackets(RectTransform fullRect)
    {
        float len = 56f, thick = 2f, margin = 44f;
        var col = new Color(ColNeutral.r, ColNeutral.g, ColNeutral.b, 0.45f);
        // (anchor, hDir, vDir): 각 코너의 앵커와 안쪽 방향
        var corners = new (Vector2 a, float hx, float vy)[]
        {
            (new Vector2(0f, 1f),  1f, -1f),  // 좌상
            (new Vector2(1f, 1f), -1f, -1f),  // 우상
            (new Vector2(0f, 0f),  1f,  1f),  // 좌하
            (new Vector2(1f, 0f), -1f,  1f),  // 우하
        };
        foreach (var c in corners)
        {
            Vector2 origin = new Vector2(c.hx * margin, c.vy * margin);
            // 가로 획
            var h = NewImage("BracketH", fullRect);
            var hr = h.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = c.a;
            hr.sizeDelta = new Vector2(len, thick);
            hr.anchoredPosition = origin + new Vector2(c.hx * len * 0.5f, 0f);
            h.color = col;
            // 세로 획
            var v = NewImage("BracketV", fullRect);
            var vr = v.rectTransform;
            vr.anchorMin = vr.anchorMax = vr.pivot = c.a;
            vr.sizeDelta = new Vector2(thick, len);
            vr.anchoredPosition = origin + new Vector2(0f, c.vy * len * 0.5f);
            v.color = col;
        }
    }

    void BuildButton(Transform parent)
    {
        var btnRt = NewRect("RespawnButton", parent);
        Center(btnRt, 320f, 62f, new Vector2(0f, -325f));

        _buttonGroup = btnRt.gameObject.AddComponent<CanvasGroup>();
        _buttonGroup.alpha = 0f;

        // 호버 글로우(버튼 뒤, dull 크림슨)
        var glow = NewImage("BtnGlow", btnRt);
        Center(glow.rectTransform, 440f, 150f, Vector2.zero);
        glow.sprite = SpriteOf(MakeRadial(128));
        glow.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0f);

        // 베이스 판(중성 다크)
        var bg = NewImage("BtnBG", btnRt);
        Stretch(bg.rectTransform);
        bg.color = new Color(0.07f, 0.08f, 0.10f, 0.60f);
        bg.raycastTarget = true;

        // 테두리(9-slice: 늘려도 테두리 두께 유지)
        var border = NewImage("BtnBorder", btnRt);
        Stretch(border.rectTransform);
        border.sprite = SlicedOutline(64, 2);
        border.type = Image.Type.Sliced;
        border.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.5f);
        border.raycastTarget = false;

        var label = NewText("BtnLabel", btnRt, buttonString, 28f, new Color(ColText.r, ColText.g, ColText.b, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.characterSpacing = 6f;

        _respawnButton = btnRt.gameObject.AddComponent<Button>();
        _respawnButton.transition = Selectable.Transition.None;   // 시각은 DeathRespawnButton이 전담
        _respawnButton.targetGraphic = bg;
        _respawnButton.onClick.AddListener(OnRespawnClicked);

        _buttonFx = btnRt.gameObject.AddComponent<DeathRespawnButton>();
        _buttonFx.button      = _respawnButton;
        _buttonFx.background   = bg;
        _buttonFx.border       = border;
        _buttonFx.glow         = glow;
        _buttonFx.label        = label;
        // 정돈된 톤으로 인터랙션 색 지정(기본값 대신)
        _buttonFx.baseColor    = new Color(0.07f, 0.08f, 0.10f, 0.60f);
        _buttonFx.hoverColor   = new Color(ColRed.r * 0.5f, ColRed.g * 0.28f, ColRed.b * 0.28f, 0.72f);
        _buttonFx.borderBase   = new Color(ColRed.r, ColRed.g, ColRed.b, 0.5f);
        _buttonFx.borderHover  = new Color(ColRed.r, ColRed.g, ColRed.b, 0.95f);
        _buttonFx.labelBase    = new Color(ColText.r, ColText.g, ColText.b, 0.85f);
        _buttonFx.labelHover   = Color.white;

        BuildButtonBrackets(btnRt);
    }

    // 버튼 네 모서리 L자 브래킷(살짝 바깥). 버튼과 함께 페이드/스케일.
    void BuildButtonBrackets(RectTransform btn)
    {
        float len = 11f, thick = 2f, off = 3f;
        var col = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.8f);
        var corners = new (Vector2 a, float hx, float vy)[]
        {
            (new Vector2(0f, 1f),  1f, -1f),
            (new Vector2(1f, 1f), -1f, -1f),
            (new Vector2(0f, 0f),  1f,  1f),
            (new Vector2(1f, 0f), -1f,  1f),
        };
        foreach (var c in corners)
        {
            Vector2 o = new Vector2(-c.hx * off, -c.vy * off);   // 코너 바깥쪽으로
            var h = NewImage("BtnBracketH", btn);
            var hr = h.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = c.a;
            hr.sizeDelta = new Vector2(len, thick);
            hr.anchoredPosition = o + new Vector2(c.hx * len * 0.5f, 0f);
            h.color = col;
            var v = NewImage("BtnBracketV", btn);
            var vr = v.rectTransform;
            vr.anchorMin = vr.anchorMax = vr.pivot = c.a;
            vr.sizeDelta = new Vector2(thick, len);
            vr.anchoredPosition = o + new Vector2(0f, c.vy * len * 0.5f);
            v.color = col;
        }
    }

    // ── 최상단 캔버스(모달) ───────────────────────────────────────
    void EnsureTopmostCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        int order = 500;
        var wm = WindowManager.I;
        if (wm != null && wm.SortingSettings != null)
            order = wm.SortingSettings.GetOrder(UILayer.Overlay);

        canvas.overrideSorting = true;
        canvas.sortingOrder = order;

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

    // ── 헬퍼: 생성 ────────────────────────────────────────────────
    TMP_FontAsset FindFont()
    {
        // 기존 자식 TMP에서 한글 지원 폰트를 확보(없으면 TMP 기본). 서브타이틀 한글 깨짐 방지.
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
            if (t != null && t.font != null) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static Image NewImage(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string name, Transform parent, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, float w, float h, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = pos;
    }

    // 화면 중앙 앵커 + 넉넉한 기본 크기(실제 크기는 FitFullscreen이 캔버스에 맞춰 갱신).
    static void CenterFull(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(2000f, 1200f);
    }

    static void SetScale(RectTransform rt, float s) => rt.localScale = new Vector3(s, s, 1f);
    static void SetAlpha(TMP_Text t, float a) { var c = t.color; c.a = a; t.color = c; }

    static Sprite SpriteOf(Texture2D tex, float ppu = 100f)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);

    // 9-slice 아웃라인 스프라이트(테두리용): 늘려도 모서리/두께 유지.
    static Sprite SlicedOutline(int size, int thickness)
    {
        var tex = MakeSquareOutline(size, thickness);
        float b = thickness + 2f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    // ── 헬퍼: 텍스처 생성 ─────────────────────────────────────────
    // 정사각 아웃라인(테두리만) — 45° 회전하면 다이아몬드. 버튼 테두리(Sliced)로도 재사용.
    static Texture2D MakeSquareOutline(int size, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var clear = new Color(1, 1, 1, 0);
        var line = Color.white;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                tex.SetPixel(x, y, edge ? line : clear);
            }
        tex.Apply();
        return tex;
    }

    // 링(annulus): 반지름 [R-thickness, R] 사이만 채움. 가장자리 1px 안티에일리어싱 → 매끈함.
    static Texture2D MakeRing(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float rOut = c - 1.5f;
        float rIn  = rOut - thickness;
        float mid  = (rOut + rIn) * 0.5f;
        float half = (rOut - rIn) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(half - Mathf.Abs(r - mid) + 0.5f);   // 1px AA 램프
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 배경 테크 그리드(셀 좌/하단 1px 라인) — Repeat 타일링용.
    static Texture2D MakeGrid(int cell)
    {
        var tex = new Texture2D(cell, cell, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
        var clear = new Color(1, 1, 1, 0);
        var line = Color.white;
        for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
                tex.SetPixel(x, y, (x == 0 || y == 0) ? line : clear);
        tex.Apply();
        return tex;
    }

    // 세그먼트 링: annulus를 일정 각도마다 gap으로 끊어 눈금(세그먼트) 게이지.
    static Texture2D MakeSegmentedRing(int size, float thickness, int segCount, float gapDeg)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float rOut = c - 1.5f, rIn = rOut - thickness;
        float mid = (rOut + rIn) * 0.5f, half = (rOut - rIn) * 0.5f;
        float seg = 360f / segCount, tickW = seg - gapDeg;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(half - Mathf.Abs(r - mid) + 0.5f);
                if (a > 0f)
                {
                    float deg = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 450f) % 360f;
                    if (deg % seg > tickW) a = 0f;   // gap
                }
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 중앙이 밝고 가장자리로 갈수록 투명 — 글로우.
    static Texture2D MakeRadial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f, maxR = c;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;   // 부드러운 falloff
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 중앙 투명 → 가장자리 불투명 — 비네트.
    static Texture2D MakeVignette(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f, maxR = c;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                a = a * a;
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 1x4: 한 줄만 어둡게 = 스캔라인. Repeat 타일링.
    static Texture2D MakeScanline()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixel(0, 0, new Color(0, 0, 0, 1));
        tex.SetPixel(0, 1, new Color(0, 0, 0, 0));
        tex.SetPixel(0, 2, new Color(0, 0, 0, 0));
        tex.SetPixel(0, 3, new Color(0, 0, 0, 0));
        tex.Apply();
        return tex;
    }
}
