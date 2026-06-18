using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔드필드식 단계별 코치마크 오버레이.
/// - 화면을 어둡게 깔고, 지정한 UI 타깃 영역만 밝게(4-스트립 스포트라이트).
/// - 상단 배너 텍스트 + "아무 곳이나 눌러 계속하기".
/// UI를 코드로 자동 생성하므로 씬 세팅 불필요 (lazy 싱글턴).
/// ContinueObjective 가 ShowContinueStep / Hide 를 호출한다.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    // ── 싱글턴 (lazy, 런타임 자동 생성) ───────────────────────────────
    private static TutorialOverlay _i;
    public static bool HasInstance => _i != null;
    public static TutorialOverlay I
    {
        get
        {
            if (_i == null && Application.isPlaying)
            {
                var go = new GameObject("[TutorialOverlay]");
                _i = go.AddComponent<TutorialOverlay>();
            }
            return _i;
        }
    }

    // ── 타깃 레지스트리 (UI 요소가 id로 등록) ─────────────────────────
    // 같은 id에 여러 rect 등록 가능 (예: 재료 슬롯 2칸 → 합집합 영역으로 한 번에 강조).
    private static readonly Dictionary<string, List<RectTransform>> _targets = new();

    public static void RegisterTarget(string id, RectTransform rect)
    {
        if (string.IsNullOrEmpty(id) || rect == null) return;
        if (!_targets.TryGetValue(id, out var list)) { list = new List<RectTransform>(); _targets[id] = list; }
        if (!list.Contains(rect)) list.Add(rect);
    }

    public static void UnregisterTarget(string id, RectTransform rect)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_targets.TryGetValue(id, out var list))
        {
            list.Remove(rect);
            if (list.Count == 0) _targets.Remove(id);
        }
    }

    // ── UI 요소 ───────────────────────────────────────────────────────
    private RectTransform _root;
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private Image _fullDim;
    private Image _top, _bottom, _left, _right;
    private Image _borderTop, _borderBottom, _borderLeft, _borderRight;
    private Image _frameImage;        // 게임톤 sci-fi 프레임 스프라이트(9-slice). 있으면 4변 테두리 대신 사용.
    private bool _useSpriteFrame;     // 프레임 스프라이트 로드 성공 시 true
    private Button _clickCatcher;
    private RectTransform _bannerBg;
    private TMP_Text _banner;
    private TMP_Text _continueLabel;

    private string _spotlightId;
    private Action _onContinue;
    private KeyCode _advanceKey;
    private bool _active;
    private bool _suppressed;   // 설정창(ESC 일시정지)이 위에 떠서 잠시 숨김 — 닫히면 복귀

    /// <summary>현재 단계가 진행을 위해 요구하는 키 (없으면 None). GameUIController가 코치 중 그 키만 통과시키는 데 사용.</summary>
    public KeyCode ActiveAdvanceKey => _active ? _advanceKey : KeyCode.None;

    /// <summary>코치마크가 지금 화면에 떠 있는지 (HUD 자동표시에서 '설명 중 강제표시' 판단용).</summary>
    public bool IsActive => _active;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.88f);
    private static readonly Color BorderColor = new Color(1f, 0.85f, 0.2f, 1f);   // 계속 라벨과 동일 금색
    private const float SpotlightPadPx = 8f;
    private const float BorderThicknessPx = 3f;
    private const float ContinueCooldown = 0.2f;   // 표시 직후 잔여 입력으로 즉시 넘어가는 것 방지
    private float _shownTime;

    // 강조 등장 효과 — 큰 네모에서 타깃으로 수렴(focus-in) + 정착 후 은은한 펄스.
    private const float FocusDuration = 0.28f;        // 수렴 시간
    private const float FocusStartExpandPx = 120f;    // 시작 시 사방 확대량(px) → 0으로 수렴
    private float _focusStartTime = -999f;            // 새 타깃 표시 시각 (수렴 기준)

    // 프레임 스프라이트 금색 선이 가장자리에 딱 붙은(edge-to-edge) 버전이라 여백 보정 불필요(0).
    // 금테가 안쪽으로 들어간 스프라이트를 쓰면 그 여백만큼 값을 올려 구멍 경계에 맞춘다.
    private const float FrameOutsetPx = 0f;

    private void Awake()
    {
        if (_i != null && _i != this) { Destroy(gameObject); return; }
        _i = this;
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy() { if (_i == this) _i = null; }

    // ── 외부 API ──────────────────────────────────────────────────────

    /// <summary>안내 단계 표시: 배너 + 스포트라이트 + 계속(클릭 또는 지정 키).</summary>
    public void ShowContinueStep(string banner, string spotlightTargetId, Action onContinue, KeyCode advanceKey = KeyCode.None)
    {
        _spotlightId = spotlightTargetId;
        _onContinue = onContinue;
        _advanceKey = advanceKey;
        _active = true;
        _shownTime = Time.unscaledTime;
        _focusStartTime = Time.unscaledTime;   // 이 단계 강조 수렴 효과 시작

        if (_banner != null)
        {
            _banner.text = banner ?? string.Empty;
            _banner.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(banner));
        }

        _clickCatcher.gameObject.SetActive(true);
        if (_continueLabel != null)
        {
            _continueLabel.gameObject.SetActive(true);
            _continueLabel.text = advanceKey == KeyCode.None
                ? "▶  아무 곳이나 클릭하여 계속  ◀"
                : $"▶  {advanceKey} 키를 눌러 계속  ◀";
        }

        // 코치마크 표시 중 좌측 퀘스트 트래커 숨김 (배너와 중복 방지, 토스트 느낌)
        GameUIController.Instance?.SetTutorialCoachActive(true);

        SetVisible(true);
        UpdateSpotlight();   // 첫 프레임부터 정확한 위치
    }

    public void Hide()
    {
        _active = false;
        _suppressed = false;
        _spotlightId = null;
        _onContinue = null;
        _advanceKey = KeyCode.None;
        _continueLocked = false;   // 다음 영역은 위치 새로 계산
        GameUIController.Instance?.SetTutorialCoachActive(false);   // 트래커 복귀
        SetVisible(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    // 표시/숨김은 Canvas·Raycaster 토글로 처리한다(GameObject는 항상 활성 → LateUpdate가 계속 돌아
    // 설정창 열고닫힘을 self-poll로 감지해 복귀할 수 있음). GameObject를 끄면 LateUpdate가 멈춰 복귀 불가.
    private void SetVisible(bool v)
    {
        if (_canvas != null) _canvas.enabled = v;
        if (_raycaster != null) _raycaster.enabled = v;
    }

    private void LateUpdate()
    {
        if (!_active) return;

        // 설정창(ESC 일시정지)은 WindowManager가 직접 열어 _currentState를 안 거치므로 여기서 직접 감지.
        // 떠 있는 동안 오버레이를 숨겨(진행도 멈춤) 설정창이 가장 앞에 보이게 하고, 닫히면 자동 복귀.
        // WM 경로(IsOpen)와 폴백 경로(_currentState) 둘 다 커버.
        bool settingsOpen =
            (TimeKov.UI.WindowManager.I != null && TimeKov.UI.WindowManager.I.IsOpen("Settings"))
            || (GameUIController.Instance != null
                && GameUIController.Instance.GetCurrentState() == GameUIController.UIState.Settings);
        if (settingsOpen != _suppressed)
        {
            _suppressed = settingsOpen;
            SetVisible(!_suppressed);
        }
        if (_suppressed) return;

        UpdateSpotlight();

        // 계속 처리 — uGUI Button(EventSystem) 대신 Input 폴링 (나머지 튜토와 동일 경로, 확실히 작동).
        if (_onContinue == null) return;
        if (Time.unscaledTime - _shownTime <= ContinueCooldown) return;

        bool advance = _advanceKey != KeyCode.None
            ? Input.GetKeyDown(_advanceKey)         // 특정 키로만 (예: C)
            : Input.GetMouseButtonDown(0);          // 좌클릭에만 (아무 키로 넘어가는 것 방지)

        if (advance) _onContinue.Invoke();
    }

    private void UpdateSpotlight()
    {
        // 같은 id에 등록된 모든 활성 rect의 합집합(union)으로 구멍 영역 계산 (재료 슬롯 2칸 등).
        bool hasTarget = false;
        float xMin = 1f, xMax = 0f, yMin = 1f, yMax = 0f;

        if (!string.IsNullOrEmpty(_spotlightId) && _targets.TryGetValue(_spotlightId, out var list))
        {
            Vector3[] c = new Vector3[4];
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);

            foreach (var target in list)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;

                target.GetWorldCorners(c);   // 0=BL 1=TL 2=TR 3=BR (world)
                Canvas tc = target.GetComponentInParent<Canvas>();
                Camera cam = (tc != null && tc.renderMode != RenderMode.ScreenSpaceOverlay) ? tc.worldCamera : null;

                Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
                Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, c[2]);

                float nxMin = Mathf.Clamp01((Mathf.Min(bl.x, tr.x) - SpotlightPadPx) / w);
                float nxMax = Mathf.Clamp01((Mathf.Max(bl.x, tr.x) + SpotlightPadPx) / w);
                float nyMin = Mathf.Clamp01((Mathf.Min(bl.y, tr.y) - SpotlightPadPx) / h);
                float nyMax = Mathf.Clamp01((Mathf.Max(bl.y, tr.y) + SpotlightPadPx) / h);

                if (!hasTarget) { xMin = nxMin; xMax = nxMax; yMin = nyMin; yMax = nyMax; hasTarget = true; }
                else { xMin = Mathf.Min(xMin, nxMin); xMax = Mathf.Max(xMax, nxMax); yMin = Mathf.Min(yMin, nyMin); yMax = Mathf.Max(yMax, nyMax); }
            }
        }

        // 타깃 없으면 전체 딤(스트립 숨김), 있으면 4스트립으로 구멍 + 테두리/프레임
        _fullDim.enabled = !hasTarget;
        _top.enabled = _bottom.enabled = _left.enabled = _right.enabled = hasTarget;
        // 프레임 스프라이트 있으면 그걸로, 없으면 4변 단색 테두리로 강조
        _borderTop.enabled = _borderBottom.enabled = _borderLeft.enabled = _borderRight.enabled = hasTarget && !_useSpriteFrame;
        if (_frameImage != null) _frameImage.enabled = hasTarget && _useSpriteFrame;

        // 계속 라벨 위치 — 타깃이 화면 하단 + 가로 중앙을 덮을 때(퀵슬롯 바)만 구멍 위로.
        PositionContinue(hasTarget, xMin, xMax, yMin, yMax);

        if (!hasTarget) { PositionBanner(true); return; }   // 타깃 없으면 배너 상단(기본)

        // ── 강조 효과 ── 큰 네모 → 타깃으로 수렴(focus-in). 스트립/테두리에만 적용(라벨/배너는 실제 위치 기준).
        float ft = Mathf.Clamp01((Time.unscaledTime - _focusStartTime) / FocusDuration);
        float ease = 1f - (1f - ft) * (1f - ft);                 // ease-out quad
        float expandPx = Mathf.Lerp(FocusStartExpandPx, 0f, ease);
        float ex = expandPx / Mathf.Max(1, Screen.width);
        float ey = expandPx / Mathf.Max(1, Screen.height);
        float bxMin = Mathf.Clamp01(xMin - ex), bxMax = Mathf.Clamp01(xMax + ex);
        float byMin = Mathf.Clamp01(yMin - ey), byMax = Mathf.Clamp01(yMax + ey);

        SetAnchors(_top.rectTransform, 0f, byMax, 1f, 1f);
        SetAnchors(_bottom.rectTransform, 0f, 0f, 1f, byMin);
        SetAnchors(_left.rectTransform, 0f, byMin, bxMin, byMax);
        SetAnchors(_right.rectTransform, bxMax, byMin, 1f, byMax);

        // 강조: 수렴 중엔 흐리게 시작 → 정착 후 은은한 호흡(펄스). 프레임 스프라이트면 그걸, 아니면 4변 테두리.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);     // 0..1
        float frameAlpha = Mathf.Lerp(0.3f, 0.82f + 0.18f * pulse, ease);
        if (_useSpriteFrame)
        {
            // 9-slice 프레임을 구멍(확대 박스)에 맞춰 배치 — 코너 고정 크기, 변은 늘어남. 수렴/펄스 그대로 적용.
            // 스프라이트 안쪽 여백 보정: rect를 FrameOutsetPx만큼 바깥으로 빼 금테가 구멍 경계에 딱 붙게.
            var frt = _frameImage.rectTransform;
            SetAnchors(frt, bxMin, byMin, bxMax, byMax);
            frt.offsetMin = new Vector2(-FrameOutsetPx, -FrameOutsetPx);
            frt.offsetMax = new Vector2(FrameOutsetPx, FrameOutsetPx);
            var fc = _frameImage.color; fc.a = frameAlpha; _frameImage.color = fc;
        }
        else
        {
            // 폴백: 4변 단색 테두리 (수렴 중 굵게→얇게 + 호흡)
            float thick = Mathf.Lerp(BorderThicknessPx + 5f, BorderThicknessPx + pulse * 1.5f, ease);
            SetBorderAlpha(frameAlpha);
            SetEdge(_borderTop.rectTransform,    bxMin, byMax, bxMax, byMax, 0f, 0f, 0f, thick);
            SetEdge(_borderBottom.rectTransform, bxMin, byMin, bxMax, byMin, 0f, -thick, 0f, 0f);
            SetEdge(_borderLeft.rectTransform,   bxMin, byMin, bxMin, byMax, -thick, 0f, 0f, 0f);
            SetEdge(_borderRight.rectTransform,  bxMax, byMin, bxMax, byMax, 0f, 0f, thick, 0f);
        }

        // 배너가 타깃을 가리지 않게 — 타깃이 화면 위쪽이면 배너를 아래로. (실제 위치 기준)
        PositionBanner(yMax <= 0.7f);
    }

    // 4변 테두리 알파를 한 번에 설정 (수렴 페이드인 + 펄스용).
    private void SetBorderAlpha(float a)
    {
        var col = BorderColor; col.a = Mathf.Clamp01(a);
        if (_borderTop) _borderTop.color = col;
        if (_borderBottom) _borderBottom.color = col;
        if (_borderLeft) _borderLeft.color = col;
        if (_borderRight) _borderRight.color = col;
    }

    // 배너 위치: top=true 상단, false 하단(계속 라벨 위). 타깃을 가리지 않도록.
    private void PositionBanner(bool top)
    {
        if (_bannerBg == null) return;
        if (top) SetAnchors(_bannerBg, 0.12f, 0.84f, 0.88f, 0.93f);
        else     SetAnchors(_bannerBg, 0.12f, 0.27f, 0.88f, 0.38f);   // "클릭하여 계속"(하단)과 겹치지 않게 위로
    }

    // "클릭하여 계속" 라벨 위치 - 한 코치 세션(=한 영역) 동안 고정. 첫 계산값을 잠그고 Hide서 해제.
    // GuidedTour는 스텝 교체 시 Hide를 안 부르므로(내용만 교체) 락이 투어 내내 유지 -> 스텝마다 안 튐.
    // 영역(인트로/공장/코어)마다는 Hide로 락이 풀려 새로 계산되니 서로 달라도 됨.
    private bool _continueLocked;
    private Vector2 _contAnchorMin, _contAnchorMax;

    // 계속 라벨 위치: 타깃이 화면 하단(yMin<0.22)이면서 가로 중앙(0.5)을 덮을 때만(퀵슬롯 바)
    // 구멍 위로 올린다. 하단-구석 패널(Status/시간막대)은 중앙 라벨과 안 겹치므로 기본 하단 유지.
    private void PositionContinue(bool hasTarget, float holeLeftX, float holeRightX, float holeBottomY, float holeTopY)
    {
        if (_continueLabel == null) return;
        var rt = _continueLabel.rectTransform;

        // 이미 이 영역에서 위치가 잡혔으면 그대로 유지 (스텝 바뀌어도 안 튐).
        if (_continueLocked)
        {
            SetAnchors(rt, _contAnchorMin.x, _contAnchorMin.y, _contAnchorMax.x, _contAnchorMax.y);
            return;
        }

        float aMinX = 0.25f, aMaxX = 0.75f, aMinY, aMaxY;

        // 건설 모드(건축투어): 하단에 퀵슬롯 바가 항상 있으니 어떤 칸을 강조하든 라벨을 바 위로 고정.
        bool buildMode = GameUIController.Instance != null
                      && GameUIController.Instance.GetCurrentState() == GameUIController.UIState.Build;
        if (buildMode)
        {
            // 퀵슬롯 바와 살짝 겹쳐서 조금 더 위로 올림 (#2)
            aMinY = 0.26f; aMaxY = 0.32f;
        }
        else
        {
            bool coversCenterBottom = hasTarget && holeBottomY < 0.22f && holeLeftX < 0.5f && holeRightX > 0.5f;
            if (coversCenterBottom)
            {
                aMinY = Mathf.Clamp(holeTopY + 0.03f, 0.14f, 0.74f);
                aMaxY = Mathf.Min(aMinY + 0.05f, 0.79f);
            }
            else
            {
                aMinY = 0.06f; aMaxY = 0.12f;   // 기본 하단(중앙)
            }
        }

        SetAnchors(rt, aMinX, aMinY, aMaxX, aMaxY);
        _contAnchorMin = new Vector2(aMinX, aMinY);
        _contAnchorMax = new Vector2(aMaxX, aMaxY);
        _continueLocked = true;   // 이 영역 동안 고정
    }

    // ── UI 생성 ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000;   // 거의 최상단 (다른 UI 위)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _raycaster = gameObject.AddComponent<GraphicRaycaster>();

        _root = (RectTransform)transform;

        _fullDim = NewImage("FullDim", _root, DimColor);
        Stretch(_fullDim.rectTransform, 0f, 0f, 1f, 1f);

        _top = NewImage("DimTop", _root, DimColor);
        _bottom = NewImage("DimBottom", _root, DimColor);
        _left = NewImage("DimLeft", _root, DimColor);
        _right = NewImage("DimRight", _root, DimColor);

        // 스포트라이트 구멍 테두리 — 딤 스트립 위에 그려지도록 이후 생성
        _borderTop = NewImage("BorderTop", _root, BorderColor);
        _borderBottom = NewImage("BorderBottom", _root, BorderColor);
        _borderLeft = NewImage("BorderLeft", _root, BorderColor);
        _borderRight = NewImage("BorderRight", _root, BorderColor);

        // 게임톤 sci-fi 프레임 스프라이트(9-slice) — 있으면 4변 단색 테두리 대신 이걸로 강조.
        // "tutorial/1" = 완전 프레임(가장자리 딱 붙음). 코너만 원하면 "tutorial/2"로 교체.
        var frameSprite = Resources.Load<Sprite>("tutorial/1");
        _useSpriteFrame = frameSprite != null;
        _frameImage = NewImage("FocusFrame", _root, Color.white);
        if (_useSpriteFrame)
        {
            _frameImage.sprite = frameSprite;
            _frameImage.type = Image.Type.Sliced;
            _frameImage.pixelsPerUnitMultiplier = 3f;   // 9-slice 코너 렌더 크기(보더140/3 ≈ 47px). 크면 코너 작아짐.
        }
        _frameImage.enabled = false;

        // 클릭 캐처 (투명, 전체 화면 — 뒤 UI 클릭 차단용. 진행은 LateUpdate 폴링이 담당)
        _clickCatcher = NewButton("ClickCatcher", _root);
        Stretch((RectTransform)_clickCatcher.transform, 0f, 0f, 1f, 1f);

        // 상단 배너 (배경 박스 + 텍스트)
        var bannerBg = NewImage("BannerBg", _root, new Color(0f, 0f, 0f, 0.85f));
        SetAnchors(bannerBg.rectTransform, 0.12f, 0.84f, 0.88f, 0.93f);
        _bannerBg = bannerBg.rectTransform;
        _banner = NewText("BannerText", bannerBg.transform);
        Stretch(_banner.rectTransform, 0f, 0f, 1f, 1f);
        _banner.alignment = TextAlignmentOptions.Center;
        _banner.fontSize = 30f;
        _banner.enableAutoSizing = true; _banner.fontSizeMin = 16f; _banner.fontSizeMax = 32f;
        _banner.enableWordWrapping = true;
        _banner.color = Color.white;
        _banner.margin = new Vector4(24f, 8f, 24f, 8f);

        // 하단 "아무 곳이나 눌러 계속하기"
        _continueLabel = NewText("ContinueLabel", _root);
        SetAnchors(_continueLabel.rectTransform, 0.25f, 0.06f, 0.75f, 0.12f);
        _continueLabel.alignment = TextAlignmentOptions.Center;
        _continueLabel.fontSize = 24f;
        _continueLabel.color = new Color(1f, 0.85f, 0.2f, 1f);
        _continueLabel.text = "▶  아무 곳이나 클릭하여 계속  ◀";
    }

    private static Image NewImage(string name, Transform parent, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    private static Button NewButton(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);   // 투명하지만 raycast는 받음
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        return btn;
    }

    private static TMP_Text NewText(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        => SetAnchors(rt, xMin, yMin, xMax, yMax);

    private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 앵커는 구멍 경계선(0폭/0높이)에 두고 offset(px)으로 두께를 줘 얇은 테두리 바를 만든다.
    private static void SetEdge(RectTransform rt, float axMin, float ayMin, float axMax, float ayMax,
                               float ox0, float oy0, float ox1, float oy1)
    {
        rt.anchorMin = new Vector2(axMin, ayMin);
        rt.anchorMax = new Vector2(axMax, ayMax);
        rt.offsetMin = new Vector2(ox0, oy0);
        rt.offsetMax = new Vector2(ox1, oy1);
    }
}
