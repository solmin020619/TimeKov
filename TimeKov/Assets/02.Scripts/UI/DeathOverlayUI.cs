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

    [Header("폰트")]
    [Tooltip("타이틀(TIME OVER)·로만 숫자에 쓸 세리프 폰트. 비우면 자동으로 'Cinzel'을 찾고, 못 찾으면 기본 폰트 사용.")]
    [SerializeField] private TMP_FontAsset titleFont;
    [Tooltip("본문·버튼·키커 한글에 쓸 부드러운 폰트. 비우면 자동으로 'Maeumgyeol'을 찾고, 못 찾으면 기본 폰트 사용.")]
    [SerializeField] private TMP_FontAsset bodyFont;

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

    // 중앙 시계 엠블럼(장식 전용) — 카운트다운은 아래 세그먼트 링이 담당
    private RectTransform _emblemRt;         // 엠블럼 컨테이너(등장 스케일)
    private Image         _emblemGlow;      // 엠블럼 뒤 붉은 후광(맥동)
    private RectTransform _ringA, _ringB, _ringC;         // 회전하는 3중 링 컨테이너
    private RectTransform _handHour, _handMin, _handSec;  // 되감기(역회전) 시/분/초침
    private RectTransform _sweep;            // 쓸고 지나가는 발광 섹터
    private Image         _timeArc;          // 남은 시간 진행 아크(카운트다운 연동)

    private Image         _vignette;
    private RawImage      _scan;            // CRT 스캔라인(uvRect 타일링 = 단일 쿼드)
    private RawImage      _grid;            // 배경 테크 그리드(uvRect 타일링)
    private RectTransform _frame;           // 코너 브래킷용 풀스크린 프레임(비네트 위)
    private TMP_Text      _title;           // 소프트 글로우 타이틀
    private Image         _titleGlow;       // 타이틀 뒤 은은한 발광

    // 배경 오로라 + 시간 입자(부드러운 분위기)
    private RectTransform _aur1, _aur2;
    private Vector2       _aur1Home, _aur2Home;
    private RectTransform[] _particles;
    private float[]       _partSpeed, _partPhase;

    // 리스폰 대기 시계(카운트다운) → 완료 시 버튼 활성
    private CanvasGroup   _ringGroup;
    private RectTransform _ringRt;
    private TMP_Text      _ringNumber;      // 시계 중앙 카운트다운 숫자
    private TMP_Text      _loss;            // 아이템 상실 문구(shimmer)
    private TMP_Text      _kicker;          // 타이틀 아래 부제 (SYNC LOST · 시간 소멸)
    private TMP_Text      _buttonLabel;     // 리스폰 버튼 텍스트

    private Button        _respawnButton;
    private CanvasGroup   _buttonGroup;
    private DeathRespawnButton _buttonFx;
    private Coroutine     _morphRoutine;
    private RectTransform _btnRt;           // 버튼 부유(float)용
    private Vector2       _btnHome;
    private Image         _btnGlowImg;      // 버튼 뒤 상시 소프트 글로우(맥동)

    // ── 상태 ───────────────────────────────────────────────────────
    private float   _countdown0;
    private float   _countTotal;            // 최초 리스폰 딜레이(진행 아크 비율 계산)
    private bool    _counting;
    private int     _lastSecs = -1;
    private float   _numPop;                // 카운트다운 숫자 팝
    private Action  _onRespawn;
    private Coroutine _fadeRoutine, _cursorRoutine;

    private bool    _built;
    private float   _t;                     // Show 이후 누적 시간(unscaled)

    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        EnsureTopmostCanvas();
        Build();
        gameObject.SetActive(false);
        Loc.OnLanguageChanged += RefreshLocalization;
    }

    void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshLocalization;
    }

    void RefreshLocalization()
    {
        if (_loss != null) _loss.text = Loc.Get(lossString);
        if (_kicker != null) _kicker.text = "SYNC LOST · " + Loc.Get("시간 소멸");
        if (_buttonLabel != null) _buttonLabel.text = Loc.Get(buttonString);
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
            if (_ringNumber != null)
                _ringNumber.text = secs.ToString();
        }

        if (_countdown0 <= 0f)
        {
            _counting = false;
            if (_ringNumber != null) _ringNumber.text = "";
            SetButtonReady(true);   // 링 → 버튼 전환
        }
    }

    // 등장: 타이틀 + 엠블럼 스케일 인(부드럽게)
    void AnimateIntro()
    {
        float p = Mathf.Clamp01(_t / Mathf.Max(0.001f, fadeInDuration));
        float ease = 1f - Mathf.Pow(1f - p, 3f);   // easeOutCubic

        if (_title != null) SetScale(_title.rectTransform, Mathf.LerpUnclamped(1.2f, 1f, ease));
        if (_emblemRt != null && _t <= fadeInDuration)
            _emblemRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.35f, 1f, ease);
    }

    // 상시: 부드러운 분위기 애니메이션 — 후광/비네트/스캔 · 3중 링 회전 · 되감기 침 · 발광 섹터 · 입자/오로라 · 타이틀 글로우
    void AnimateIdle(float dt)
    {
        // 엠블럼 후광 맥동
        if (_emblemGlow != null)
        {
            float pulse = Mathf.Sin(_t * 2.0f) * 0.5f + 0.5f;
            var c = _emblemGlow.color; c.a = 0.24f + pulse * 0.16f; _emblemGlow.color = c;
            float gs = 1f + pulse * 0.05f;
            _emblemGlow.rectTransform.localScale = new Vector3(gs, gs, 1f);
        }

        // 타이틀 뒤 발광 맥동
        if (_titleGlow != null)
        {
            float p = Mathf.Sin(_t * 2.4f) * 0.5f + 0.5f;
            var c = _titleGlow.color; c.a = 0.16f + p * 0.12f; _titleGlow.color = c;
        }

        // 비네트 미세 호흡
        if (_vignette != null)
        {
            float v = Mathf.Sin(_t * 1.3f) * 0.5f + 0.5f;
            var c = _vignette.color; c.a = 0.72f + v * 0.12f; _vignette.color = c;
        }

        // 스캔라인 흐름(아주 옅게)
        if (_scan != null)
        {
            var c = _scan.color; c.a = 0.07f + (Mathf.Sin(_t * 9f) * 0.5f + 0.5f) * 0.04f; _scan.color = c;
            var uv = _scan.uvRect; uv.y = (_t * 2.5f) % 1f; _scan.uvRect = uv;
        }

        // 3중 링 회전(정/역 교차)
        if (_ringA != null) _ringA.localRotation = Quaternion.Euler(0f, 0f, -_t * 6f);
        if (_ringB != null) _ringB.localRotation = Quaternion.Euler(0f, 0f,  _t * 9f);
        if (_ringC != null) _ringC.localRotation = Quaternion.Euler(0f, 0f, -_t * 15f);

        // 시/분/초침 되감기(반시계 = +Z) — "되감기" 컨셉
        if (_handHour != null) _handHour.localRotation = Quaternion.Euler(0f, 0f, _t * 4f);
        if (_handMin  != null) _handMin.localRotation  = Quaternion.Euler(0f, 0f, _t * 12f);
        if (_handSec  != null) _handSec.localRotation  = Quaternion.Euler(0f, 0f, _t * 45f);

        // 발광 섹터: 천천히 쓸고 지나감
        if (_sweep != null) _sweep.localRotation = Quaternion.Euler(0f, 0f, _t * 26f);

        // 시간 아크: 은은한 밝기 펄스(감소는 UpdateCountdown이 fillAmount로 담당)
        if (_timeArc != null)
        {
            float ap = Mathf.Sin(_t * 2.2f) * 0.5f + 0.5f;
            var ac = _timeArc.color; ac.a = 0.75f + ap * 0.2f; _timeArc.color = ac;
        }

        // 타이틀 소프트 글로우(알파 미세 맥동)
        if (_title != null)
        {
            float tp = Mathf.Sin(_t * 2.4f) * 0.5f + 0.5f;
            SetAlpha(_title, 0.9f + tp * 0.1f);
        }

        // 아이템 문구 shimmer(알파 왕복)
        if (_loss != null)
        {
            float ls = Mathf.Sin(_t * 1.8f) * 0.5f + 0.5f;
            SetAlpha(_loss, 0.6f + ls * 0.35f);
        }

        // 오로라 드리프트
        if (_aur1 != null) _aur1.anchoredPosition = _aur1Home + new Vector2(Mathf.Sin(_t * 0.45f) * 26f, Mathf.Cos(_t * 0.40f) * 18f);
        if (_aur2 != null) _aur2.anchoredPosition = _aur2Home + new Vector2(Mathf.Sin(_t * 0.40f + 1.5f) * 22f, Mathf.Cos(_t * 0.50f + 1f) * 16f);

        // 시간 입자: 아래→위로 떠오르며 페이드
        AnimateParticles(dt);

        // 버튼 부유(float) + 뒤 소프트 글로우 맥동
        if (_btnRt != null) _btnRt.anchoredPosition = _btnHome + new Vector2(0f, Mathf.Sin(_t * 1.5f) * 3f);
        if (_btnGlowImg != null)
        {
            float bp = Mathf.Sin(_t * 2.0f) * 0.5f + 0.5f;
            var bc = _btnGlowImg.color; bc.a = 0.12f + bp * 0.12f; _btnGlowImg.color = bc;
        }

        // 카운트다운 숫자 팝(스케일)
        if (_ringNumber != null)
        {
            float s = 1f + _numPop * 0.14f;
            SetScale(_ringNumber.rectTransform, s);
        }
    }

    // 시간 입자: 아래 → 위로 서서히 떠오르며 미세하게 반짝임. 위로 벗어나면 아래에서 재등장.
    void AnimateParticles(float dt)
    {
        if (_particles == null || _canvasRect == null) return;
        float halfH = _canvasRect.rect.size.y * 0.5f + 20f;
        float halfW = _canvasRect.rect.size.x * 0.5f;
        for (int i = 0; i < _particles.Length; i++)
        {
            var p = _particles[i];
            if (p == null) continue;
            var ap = p.anchoredPosition;
            ap.y += _partSpeed[i] * dt;
            if (ap.y > halfH) { ap.y = -halfH; ap.x = UnityEngine.Random.Range(-halfW, halfW); }
            p.anchoredPosition = ap;
            var img = p.GetComponent<Image>();
            if (img != null)
            {
                float fl = 0.5f + 0.5f * Mathf.Sin(_t * 2f + _partPhase[i]);
                var c = img.color; c.a = 0.10f + 0.16f * fl; img.color = c;
            }
        }
    }

    // ── 내부: 상태/버튼 ──────────────────────────────────────────
    void OnRespawnClicked()
    {
        // 클릭 시 링을 되살리지 않고, 버튼만 스르륵 사라지게 한다.
        if (_morphRoutine != null) { StopCoroutine(_morphRoutine); _morphRoutine = null; }
        if (_respawnButton != null) _respawnButton.interactable = false;
        if (_buttonGroup != null) { _buttonGroup.interactable = false; _buttonGroup.blocksRaycasts = false; }
        _morphRoutine = StartCoroutine(MorphButtonOut());
        _onRespawn?.Invoke();
    }

    // 버튼 클릭 후: 버튼을 축소·페이드아웃(링은 건드리지 않음).
    IEnumerator MorphButtonOut()
    {
        var btnRt = _respawnButton != null ? _respawnButton.transform as RectTransform : null;
        float dur = 0.5f, e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(e / dur);
            if (_buttonGroup != null) _buttonGroup.alpha = 1f - t;
            if (btnRt != null) btnRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            yield return null;
        }
        if (_buttonGroup != null) _buttonGroup.alpha = 0f;
        _morphRoutine = null;
    }

    void SetButtonReady(bool ready)
    {
        if (_morphRoutine != null) { StopCoroutine(_morphRoutine); _morphRoutine = null; }
        if (_respawnButton != null) _respawnButton.interactable = ready;

        if (ready)
        {
            // 카운트다운 링 → 버튼 스르륵 전환
            if (_buttonGroup != null) { _buttonGroup.interactable = true; _buttonGroup.blocksRaycasts = true; }
            _morphRoutine = StartCoroutine(MorphRingToButton());
        }
        else
        {
            // 대기 상태: 카운트다운 링 표시, 버튼 숨김
            if (_ringGroup != null) { _ringGroup.alpha = 1f; _ringGroup.blocksRaycasts = false; if (_ringRt != null) _ringRt.localScale = Vector3.one; }
            if (_buttonGroup != null)
            {
                _buttonGroup.interactable   = false;
                _buttonGroup.blocksRaycasts = false;
                _buttonGroup.alpha = 0f;
            }
        }
    }

    // 카운트다운 완료: 대기 링을 축소·페이드아웃하며 버튼을 페이드 인.
    IEnumerator MorphRingToButton()
    {
        float dur = 0.5f, e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(e / dur);
            if (_ringGroup != null)
            {
                _ringGroup.alpha = 1f - t;
                if (_ringRt != null) _ringRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            }
            if (_buttonGroup != null) _buttonGroup.alpha = t;
            yield return null;
        }
        if (_ringGroup != null) { _ringGroup.alpha = 0f; _ringGroup.blocksRaycasts = false; }
        if (_buttonGroup != null) _buttonGroup.alpha = 1f;
        _morphRoutine = null;
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
        // 인스펙터 지정이 최우선(빌드/에디터 모두 신뢰 가능). 미지정 시에만 로드된 폰트에서 자동 탐색 시도.
        if (titleFont == null) titleFont = TryFindFontByName("Cinzel");
        if (bodyFont  == null) bodyFont  = TryFindFontByName("Maeumgyeol");
        // ★본문 폰트 경고는 뺐다. 찾으라던 GabiaMaeumgyeol 은 지금 미사용 폰트고,
        //   비었을 때 쓰이는 TMP 기본 폰트가 곧 프로젝트 표준(Pretendard-SemiBold)이라
        //   폴백 결과가 오히려 정상이다. 매 실행 뜨는데 고칠 게 없는 경고였다.
        //   제목(Cinzel)은 세리프 연출 의도가 있어 비면 알린다.
        if (titleFont == null) Debug.LogWarning("[DeathOverlay] Title Font(라틴 세리프)가 비어 있어 기본 폰트로 표시됩니다. DeathOverlayUI 컴포넌트의 'Title Font'에 'Cinzel SDF'를 할당하세요.", this);

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
        _backdrop.color = new Color(0.02f, 0.017f, 0.028f, 0.955f);   // 씬이 비쳐 분위기 씻기지 않게 더 어둡게
        _backdrop.raycastTarget = true;

        // 1b) 배경 테크 그리드(아주 옅게) — HUD 느낌
        var gridRt = NewRect("Grid", root);
        CenterFull(gridRt);
        _grid = gridRt.gameObject.AddComponent<RawImage>();
        _grid.texture = MakeGrid(38);
        _grid.raycastTarget = false;
        _grid.color = new Color(ColCyan.r * 0.6f, ColCyan.g * 0.6f, ColCyan.b * 0.65f, 0.03f);

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

        // 3b) 배경 오로라(붉은/스틸 광원) + 시간 입자 — 부드러운 분위기
        BuildAurora(root);
        BuildParticles(root, 26);

        // 화면 네 모서리 HUD 브래킷(시스템 경보 느낌) — 전용 풀스크린 프레임(비네트/스캔 위)에 코너 고정
        _frame = NewRect("Frame", root);
        CenterFull(_frame);
        BuildCornerBrackets(_frame);
        BuildHudFrame(_frame);

        // 4) 중앙 시계 엠블럼(3중 링 + 로만 시계 + 되감기 침) — 상단쪽 배치(더 위로 올림).
        BuildEmblem(root, new Vector2(0f, 225f));

        // 5) 타이틀(소프트 글로우) + 양옆 라인 + 키커 (엠블럼과 함께 더 위로 올림)
        BuildTitle(root, new Vector2(0f, 5f));

        // 6) 아이템 상실 문구 — 다이아몬드 캡 라인으로 감싼 캡션 + 은은한 shimmer (위치 아래로)
        MakeCapLine(root, 360f, new Vector2(0f, -215f));
        _loss = NewText("Loss", root, Loc.Get(lossString), 24f, ColText, FontStyles.Normal, TextAlignmentOptions.Center);
        ApplyBodyFont(_loss);
        Center(_loss.rectTransform, 1200f, 34f, new Vector2(0f, -247f));
        MakeCapLine(root, 360f, new Vector2(0f, -279f));

        // 7) 리스폰 컨트롤 — 같은 자리에 '카운트다운 세그먼트 링'과 '버튼'을 겹쳐 두고,
        //    카운트다운 동안 링(54321) 표시 → 완료 시 링 → 버튼으로 스르륵 전환.
        Vector2 respawnPos = new Vector2(0f, -360f);
        BuildCountdownRing(root, respawnPos);
        BuildButton(root, respawnPos);

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
        if (_grid      != null)
        {
            _grid.rectTransform.sizeDelta = size;
            _grid.uvRect = new Rect(0f, 0f, size.x / 38f, size.y / 38f);
        }
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

    // 중앙 시계 엠블럼: 3중 회전 링(+궤도 점) + 로만 시계면(60눈금·되감기 침·발광 섹터)
    // + 남은 시간 진행 아크 + 중앙 카운트다운 숫자. 카운트다운과 연동하며 항상 표시된다.
    void BuildEmblem(Transform parent, Vector2 pos)
    {
        var rt = NewRect("Emblem", parent);
        Center(rt, 300f, 300f, pos);
        _emblemRt = rt;   // 장식 전용(클릭/카운트다운 없음)

        // 후광
        _emblemGlow = NewImage("EmblemGlow", rt);
        Center(_emblemGlow.rectTransform, 360f, 360f, Vector2.zero);
        _emblemGlow.sprite = SpriteOf(MakeRadial(128));
        _emblemGlow.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.32f);

        // 링 A(바깥): 촘촘한 세그먼트(점선) + 붉은 짧은 아크
        _ringA = NewRect("RingA", rt);
        Center(_ringA, 300f, 300f, Vector2.zero);
        var raDash = NewImage("RingA_dash", _ringA);
        Stretch(raDash.rectTransform);
        raDash.sprite = SpriteOf(MakeSegmentedRing(256, 3f, 60, 4f));
        raDash.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.38f);
        var raArc = NewImage("RingA_arc", _ringA);
        Stretch(raArc.rectTransform);
        raArc.sprite = SpriteOf(MakeRing(256, 3f));
        raArc.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.75f);
        raArc.type = Image.Type.Filled;
        raArc.fillMethod = Image.FillMethod.Radial360;
        raArc.fillOrigin = (int)Image.Origin360.Top;
        raArc.fillAmount = 0.08f;

        // 링 B(중간): 점선 + 스틸 궤도 점
        _ringB = NewRect("RingB", rt);
        Center(_ringB, 264f, 264f, Vector2.zero);
        var rbDash = NewImage("RingB_dash", _ringB);
        Stretch(rbDash.rectTransform);
        rbDash.sprite = SpriteOf(MakeSegmentedRing(256, 2f, 40, 6f));
        rbDash.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.42f);
        var dotB = NewImage("RingB_dot", _ringB);
        Center(dotB.rectTransform, 8f, 8f, new Vector2(0f, 132f));
        dotB.sprite = SpriteOf(MakeDisc(32));
        dotB.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 1f);

        // 링 C(안쪽): 밝은 아크 + 붉은 궤도 점
        _ringC = NewRect("RingC", rt);
        Center(_ringC, 224f, 224f, Vector2.zero);
        var rcArc = NewImage("RingC_arc", _ringC);
        Stretch(rcArc.rectTransform);
        rcArc.sprite = SpriteOf(MakeRing(256, 2.5f));
        rcArc.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.5f);
        rcArc.type = Image.Type.Filled;
        rcArc.fillMethod = Image.FillMethod.Radial360;
        rcArc.fillOrigin = (int)Image.Origin360.Top;
        rcArc.fillAmount = 0.42f;
        var dotC = NewImage("RingC_dot", _ringC);
        Center(dotC.rectTransform, 7f, 7f, new Vector2(0f, 112f));
        dotC.sprite = SpriteOf(MakeDisc(32));
        dotC.color = new Color(ColRed.r, ColRed.g, ColRed.b, 1f);

        // 시계면 배경 디스크 + 외곽 얇은 링
        var face = NewImage("ClockFace", rt);
        Center(face.rectTransform, 156f, 156f, Vector2.zero);
        face.sprite = SpriteOf(MakeDisc(128));
        face.color = new Color(0.08f, 0.07f, 0.10f, 0.5f);
        var faceRing = NewImage("ClockFaceRing", rt);
        Center(faceRing.rectTransform, 160f, 160f, Vector2.zero);
        faceRing.sprite = SpriteOf(MakeRing(256, 1.5f));
        faceRing.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.45f);

        // 60 눈금 링(+ 12개 주요 눈금, 더 길고 밝게)
        var ticks60 = NewImage("ClockTicks", rt);
        Center(ticks60.rectTransform, 200f, 200f, Vector2.zero);
        ticks60.sprite = SpriteOf(MakeSegmentedRing(256, 8f, 60, 5.2f));
        ticks60.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.42f);
        var ticks12 = NewImage("ClockTicks5", rt);
        Center(ticks12.rectTransform, 208f, 208f, Vector2.zero);
        ticks12.sprite = SpriteOf(MakeSegmentedRing(256, 12f, 12, 26f));
        ticks12.color = new Color(ColText.r, ColText.g, ColText.b, 0.68f);

        // 장식용 밝은 아크(비감소) — 시계면 눈금 위 하이라이트
        var accentArc = NewImage("AccentArc", rt);
        Center(accentArc.rectTransform, 200f, 200f, Vector2.zero);
        accentArc.sprite = SpriteOf(MakeSegmentedRing(256, 8f, 60, 5.2f));
        accentArc.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.85f);
        accentArc.type = Image.Type.Filled;
        accentArc.fillMethod = Image.FillMethod.Radial360;
        accentArc.fillOrigin = (int)Image.Origin360.Top;
        accentArc.fillClockwise = true;
        accentArc.fillAmount = 0.33f;

        // 로만 숫자(XII·III·VI·IX)
        AddRoman(rt, "XII", new Vector2(0f, 60f));
        AddRoman(rt, "III", new Vector2(62f, 0f));
        AddRoman(rt, "VI",  new Vector2(0f, -62f));
        AddRoman(rt, "IX",  new Vector2(-62f, 0f));

        // 발광 섹터(쓸고 지나감)
        _sweep = NewRect("Sweep", rt);
        Center(_sweep, 156f, 156f, Vector2.zero);
        var sw = NewImage("SweepImg", _sweep);
        Stretch(sw.rectTransform);
        sw.sprite = SpriteOf(MakeRadial(128));
        sw.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.3f);
        sw.type = Image.Type.Filled;
        sw.fillMethod = Image.FillMethod.Radial360;
        sw.fillOrigin = (int)Image.Origin360.Top;
        sw.fillAmount = 0.14f;

        // 시/분/초침(되감기: 아래에서 위로, 중심에서 회전)
        _handHour = MakeHand(rt, 3.5f, 44f, new Color(ColText.r, ColText.g, ColText.b, 0.92f));
        _handMin  = MakeHand(rt, 2.5f, 62f, new Color(ColText.r, ColText.g, ColText.b, 0.72f));
        _handSec  = MakeHand(rt, 1.5f, 70f, new Color(ColRed.r,  ColRed.g,  ColRed.b,  0.9f));

        // 중앙 허브
        var hub = NewImage("Hub", rt);
        Center(hub.rectTransform, 12f, 12f, Vector2.zero);
        hub.sprite = SpriteOf(MakeDisc(32));
        hub.color = ColRed;

        // 12시 크림슨 눈금
        var top = NewImage("TopTick", rt);
        Center(top.rectTransform, 3f, 16f, new Vector2(0f, 96f));
        top.color = ColRed;
    }

    // 리스폰 대기 세그먼트 링(카운트다운): 후광 + 외곽 프레임 + 세그먼트 게이지(남은 시간 밝게)
    // + 안쪽 미세눈금 + 중앙 숫자(54321) + 12시 눈금. 카운트다운 연동, 완료 시 버튼으로 전환.
    void BuildCountdownRing(Transform parent, Vector2 pos)
    {
        var ringRt = NewRect("CountdownRing", parent);
        Center(ringRt, 96f, 96f, pos);
        _ringRt = ringRt;
        _ringGroup = ringRt.gameObject.AddComponent<CanvasGroup>();
        _ringGroup.alpha = 1f;
        _ringGroup.blocksRaycasts = false;   // 모달 차단은 백드롭이 담당

        // 후광
        var halo = NewImage("Halo", ringRt);
        Center(halo.rectTransform, 150f, 150f, Vector2.zero);
        halo.sprite = SpriteOf(MakeRadial(128));
        halo.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.13f);

        // 외곽 프레임 링(얇게)
        var frameRing = NewImage("FrameRing", ringRt);
        Stretch(frameRing.rectTransform);
        frameRing.sprite = SpriteOf(MakeRing(256, 3f));
        frameRing.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.42f);

        var seg = SpriteOf(MakeSegmentedRing(256, 22f, 20, 7f));   // 볼드 세그먼트(트랙/아크 공용)

        // 세그먼트 트랙(전체, 어둡게)
        var track = NewImage("SegTrack", ringRt);
        Center(track.rectTransform, 84f, 84f, Vector2.zero);
        track.sprite = seg;
        track.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.2f);

        // 세그먼트 진행(남은 시간, 밝게) — 12시에서 시계방향 감소
        _timeArc = NewImage("SegArc", ringRt);
        Center(_timeArc.rectTransform, 84f, 84f, Vector2.zero);
        _timeArc.sprite = seg;
        _timeArc.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.95f);
        _timeArc.type = Image.Type.Filled;
        _timeArc.fillMethod = Image.FillMethod.Radial360;
        _timeArc.fillOrigin = (int)Image.Origin360.Top;
        _timeArc.fillClockwise = true;
        _timeArc.fillAmount = 1f;

        // 안쪽 미세눈금 링
        var minor = NewImage("MinorTicks", ringRt);
        Center(minor.rectTransform, 52f, 52f, Vector2.zero);
        minor.sprite = SpriteOf(MakeSegmentedRing(128, 4f, 36, 5f));
        minor.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.3f);

        // 중앙 카운트다운 숫자
        _ringNumber = NewText("RingNumber", ringRt, "", 34f, ColCyan, FontStyles.Bold, TextAlignmentOptions.Center);
        if (titleFont != null && titleFont.HasCharacters("0123456789")) _ringNumber.font = titleFont;
        Center(_ringNumber.rectTransform, 80f, 50f, Vector2.zero);

        // 12시 크림슨 눈금(감소 시작점)
        var top = NewImage("TopTick", ringRt);
        Center(top.rectTransform, 3f, 15f, new Vector2(0f, 42f));
        top.color = ColRed;
    }

    // 되감기 침: 아래 끝(피벗 0.5,0)이 시계 중심에 오도록 배치 → localRotation 으로 회전.
    RectTransform MakeHand(Transform parent, float w, float len, Color col)
    {
        var img = NewImage("Hand", parent);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(w, len);
        rt.anchoredPosition = Vector2.zero;
        img.color = col;
        return rt;
    }

    void AddRoman(Transform parent, string s, Vector2 pos)
    {
        var t = NewText("Roman", parent, s, 15f, new Color(ColText.r, ColText.g, ColText.b, 0.5f), FontStyles.Normal, TextAlignmentOptions.Center);
        if (titleFont != null) t.font = titleFont;   // 세리프 로만 숫자
        Center(t.rectTransform, 40f, 24f, pos);
    }

    // 타이틀(소프트 글로우) + 양옆 붉은 라인 + 키커.
    void BuildTitle(Transform parent, Vector2 pos)
    {
        var cluster = NewRect("TitleCluster", parent);
        Center(cluster, 1000f, 200f, pos);

        // 타이틀 뒤 은은한 발광
        _titleGlow = NewImage("TitleGlow", cluster);
        Center(_titleGlow.rectTransform, 660f, 280f, Vector2.zero);
        _titleGlow.sprite = SpriteOf(MakeRadial(128));
        _titleGlow.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.2f);

        // 타이틀 — 세리프(Cinzel) 우선
        _title = NewText("Title", cluster, titleString, 84f, ColRed, FontStyles.Bold, TextAlignmentOptions.Center);
        if (titleFont != null) _title.font = titleFont;
        Center(_title.rectTransform, 1000f, 140f, Vector2.zero);
        _title.characterSpacing = 12f;

        // 양옆 붉은 라인(안쪽으로 페이드)
        foreach (float sx in new[] { -1f, 1f })
        {
            var line = NewImage("TitleLine", cluster);
            Center(line.rectTransform, 46f, 1.5f, new Vector2(sx * 300f, 0f));
            line.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.55f);
        }

        // 키커
        _kicker = NewText("Kicker", cluster, "SYNC LOST · " + Loc.Get("시간 소멸"), 18f, new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.6f), FontStyles.Bold, TextAlignmentOptions.Center);
        var kicker = _kicker;
        ApplyBodyFont(kicker);
        Center(kicker.rectTransform, 1000f, 28f, new Vector2(0f, -64f));
        kicker.characterSpacing = 16f;
    }

    // 배경 오로라: 붉은/스틸 광원 두 개(천천히 드리프트).
    void BuildAurora(Transform parent)
    {
        // 밝은 중심을 화면 모서리 밖으로 밀어, 넓게 퍼진 은은한 코너 광만 보이게(배경에 녹아듦).
        _aur1 = NewRect("Aurora1", parent);
        _aur1Home = new Vector2(-620f, 320f);
        Center(_aur1, 1160f, 1160f, _aur1Home);
        var a1 = _aur1.gameObject.AddComponent<Image>();
        a1.sprite = SpriteOf(MakeRadial(128));
        a1.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.08f);   // 세기 낮춤
        a1.raycastTarget = false;

        _aur2 = NewRect("Aurora2", parent);
        _aur2Home = new Vector2(640f, -340f);
        Center(_aur2, 1060f, 1060f, _aur2Home);
        var a2 = _aur2.gameObject.AddComponent<Image>();
        a2.sprite = SpriteOf(MakeRadial(128));
        a2.color = new Color(0.42f, 0.40f, 0.46f, 0.05f);   // 채도 낮춘 뉴트럴 쿨그레이(청록 튐 방지)
        a2.raycastTarget = false;
    }

    // 시간 입자: 작은 점들을 화면 전역에 흩뿌리고 위로 서서히 떠오르게 한다.
    void BuildParticles(Transform parent, int count)
    {
        var container = NewRect("Particles", parent);
        CenterFull(container);
        _particles = new RectTransform[count];
        _partSpeed = new float[count];
        _partPhase = new float[count];
        var disc = SpriteOf(MakeDisc(16));
        for (int i = 0; i < count; i++)
        {
            var img = NewImage("P", container);
            float sz = UnityEngine.Random.Range(1.5f, 3.5f);
            Center(img.rectTransform, sz, sz, new Vector2(UnityEngine.Random.Range(-900f, 900f), UnityEngine.Random.Range(-540f, 540f)));
            img.sprite = disc;
            img.color = new Color(0.78f, 0.59f, 0.55f, 0.12f);
            _particles[i]  = img.rectTransform;
            _partSpeed[i]  = UnityEngine.Random.Range(10f, 32f);
            _partPhase[i]  = UnityEngine.Random.Range(0f, 6.28f);
        }
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

    // 리스폰 버튼(둥근 알약형): 상시 소프트 글로우 + 호버 글로우 + 부유. 카운트다운 완료 시 페이드 인.
    void BuildButton(Transform parent, Vector2 pos)
    {
        var btnRt = NewRect("RespawnButton", parent);
        Center(btnRt, 320f, 60f, pos);
        _btnRt   = btnRt;
        _btnHome = pos;

        _buttonGroup = btnRt.gameObject.AddComponent<CanvasGroup>();
        _buttonGroup.alpha = 0f;

        // 상시 소프트 글로우(맥동) — 알약 뒤
        _btnGlowImg = NewImage("BtnSoftGlow", btnRt);
        Center(_btnGlowImg.rectTransform, 460f, 170f, Vector2.zero);
        _btnGlowImg.sprite = SpriteOf(MakeRadial(128));
        _btnGlowImg.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.12f);

        // 호버 글로우(DeathRespawnButton 제어)
        var glow = NewImage("BtnGlow", btnRt);
        Center(glow.rectTransform, 440f, 150f, Vector2.zero);
        glow.sprite = SpriteOf(MakeRadial(128));
        glow.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0f);

        // 알약 베이스 판(둥근 9-slice)
        var bg = NewImage("BtnBG", btnRt);
        Stretch(bg.rectTransform);
        bg.sprite = RoundedSprite(64, 28, 0);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.09f, 0.08f, 0.11f, 0.60f);
        bg.raycastTarget = true;

        // 알약 테두리(둥근 9-slice)
        var border = NewImage("BtnBorder", btnRt);
        Stretch(border.rectTransform);
        border.sprite = RoundedSprite(64, 28, 2);
        border.type = Image.Type.Sliced;
        border.color = new Color(ColRed.r, ColRed.g, ColRed.b, 0.5f);
        border.raycastTarget = false;

        // 안쪽 보조 알약 프레임(스틸, 아주 옅게) — 깊이감
        var inner = NewImage("BtnInner", btnRt);
        Center(inner.rectTransform, 320f - 12f, 60f - 12f, Vector2.zero);
        inner.sprite = RoundedSprite(64, 22, 1);
        inner.type = Image.Type.Sliced;
        inner.color = new Color(ColCyan.r, ColCyan.g, ColCyan.b, 0.16f);
        inner.raycastTarget = false;

        _buttonLabel = NewText("BtnLabel", btnRt, Loc.Get(buttonString), 26f, new Color(ColText.r, ColText.g, ColText.b, 0.9f), FontStyles.Bold, TextAlignmentOptions.Center);
        var label = _buttonLabel;
        ApplyBodyFont(label);
        Stretch(label.rectTransform);
        label.characterSpacing = 6f;

        _respawnButton = btnRt.gameObject.AddComponent<Button>();
        _respawnButton.transition = Selectable.Transition.None;   // 시각은 DeathRespawnButton이 전담
        _respawnButton.targetGraphic = bg;
        _respawnButton.onClick.AddListener(OnRespawnClicked);

        _buttonFx = btnRt.gameObject.AddComponent<DeathRespawnButton>();
        _buttonFx.button       = _respawnButton;
        _buttonFx.background    = bg;
        _buttonFx.border        = border;
        _buttonFx.glow          = glow;
        _buttonFx.label         = label;
        _buttonFx.baseColor     = new Color(0.09f, 0.08f, 0.11f, 0.60f);
        _buttonFx.hoverColor    = new Color(ColRed.r * 0.5f, ColRed.g * 0.28f, ColRed.b * 0.28f, 0.72f);
        _buttonFx.borderBase    = new Color(ColRed.r, ColRed.g, ColRed.b, 0.5f);
        _buttonFx.borderHover   = new Color(ColRed.r, ColRed.g, ColRed.b, 0.95f);
        _buttonFx.labelBase     = new Color(ColText.r, ColText.g, ColText.b, 0.85f);
        _buttonFx.labelHover    = Color.white;
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

    // 부드러운 한글 폰트를 적용하되, 정적 아틀라스에 글리프가 하나라도 빠지면(누락 박스 방지) 기본 폰트를 유지.
    void ApplyBodyFont(TMP_Text t)
    {
        if (t != null && bodyFont != null && bodyFont.HasCharacters(t.text))
            t.font = bodyFont;
    }

    // 로드된 TMP 폰트 애셋 중 이름에 namePart(예: "Cinzel")가 포함된 것을 찾는다(에디터/참조된 경우).
    static TMP_FontAsset TryFindFontByName(string namePart)
    {
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in all)
            if (f != null && f.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        return null;
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

    // 꽉 찬 원(1px AA) — 점/허브/시계면/입자용.
    static Texture2D MakeDisc(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f, r = c - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 둥근 사각 부호거리(중심 기준). 음수=내부. w,h=전체 크기, r=코너 반지름.
    static float RoundedRectSD(float px, float py, float w, float h, float r)
    {
        float qx = Mathf.Abs(px) - (w * 0.5f - r);
        float qy = Mathf.Abs(py) - (h * 0.5f - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inside  = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - r;
    }

    // 둥근 사각 텍스처. thickness<=0 → 채움, >0 → 안쪽 테두리. 9-slice 로 늘려 알약/둥근 버튼에 사용.
    static Texture2D MakeRoundedRect(int size, int radius, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float cx = size * 0.5f, cy = size * 0.5f, wh = size - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectSD(x + 0.5f - cx, y + 0.5f - cy, wh, wh, radius);
                float aOut = Mathf.Clamp01(0.5f - d);                 // 외곽 경계 AA
                float a = thickness <= 0
                    ? aOut
                    : Mathf.Min(aOut, Mathf.Clamp01(d + thickness + 0.5f));   // 안쪽 경계까지의 밴드
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return tex;
    }

    // 둥근 9-slice 스프라이트(코너 반지름 유지, 중앙만 늘어남).
    static Sprite RoundedSprite(int size, int radius, int thickness)
    {
        var tex = MakeRoundedRect(size, radius, thickness);
        float b = radius + 2f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }
}
