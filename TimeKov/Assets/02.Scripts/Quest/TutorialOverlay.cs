using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 단계별 코치마크 오버레이.
/// - 화면을 어둡게 깔고, 지정한 UI 타깃 영역만 밝게(4-스트립 스포트라이트).
/// - 상단 배너 텍스트 + "아무 곳이나 눌러 계속하기".
/// ContinueObjective / GuidedTourObjective 가 ShowContinueStep / Hide 를 호출한다.
///
/// [08-02] 런타임 자체생성 -> 씬 실물 오브젝트로 전환. 계층은 씬에 있는 실물이 원본이다.
/// (생성용 에디터 빌더는 08-03 에 팀 합의로 제거. 다시 구워야 하면 git 이력에서 꺼낸다)
///   스포트라이트 '위치'는 타깃을 따라가야 해서 여전히 매 프레임 계산하지만,
///   구성 요소/색/두께/프레임 스프라이트는 이제 인스펙터에서 조정한다(프레임은 Resources.Load 대신 직접 참조).
///   오브젝트는 항상 활성으로 두고 Canvas/Raycaster 만 토글한다 - LateUpdate 가 계속 돌아야
///   설정창이 위에 떴다 닫히는 걸 감지해 복귀할 수 있다(끄면 복귀 불가).
/// </summary>
[SingleInstance]
public class TutorialOverlay : MonoBehaviour
{
    private static TutorialOverlay _i;
    public static bool HasInstance => _i != null;
    private static bool _warnedMissing;

    /// <summary>씬에 있는 인스턴스(없으면 null). 호출측은 null 이면 해당 단계를 건너뛴다.</summary>
    public static TutorialOverlay I
    {
        get
        {
            if (_i == null && Application.isPlaying)
            {
                _i = FindAnyObjectByType<TutorialOverlay>(FindObjectsInactive.Include);
                if (_i == null && !_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogError("[TutorialOverlay] 씬 Canvas 에 코치마크 오버레이가 없다. 메뉴 Tools/TIMEKOV/튜토리얼 오버레이 생성 을 실행해라. (없으면 코치마크 단계가 그냥 넘어간다)");
                }
            }
            return _i;
        }
    }

    // ── 타깃 레지스트리 (UI 요소가 id로 등록) ─────────────────────────
    // 같은 id에 여러 rect 등록 가능 (예: 재료 슬롯 2칸 -> 합집합 영역으로 한 번에 강조).
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

    [Header("구성 요소 (빌더가 자동 연결)")]
    [SerializeField] private Canvas canvasComp;
    [SerializeField] private GraphicRaycaster raycaster;
    [Tooltip("타깃이 없을 때 쓰는 전체 딤.")]
    [SerializeField] private Image fullDim;
    [Tooltip("타깃이 있을 때 구멍을 만드는 4스트립(상/하/좌/우).")]
    [SerializeField] private Image dimTop, dimBottom, dimLeft, dimRight;
    [Tooltip("프레임 스프라이트가 없을 때 쓰는 4변 단색 테두리.")]
    [SerializeField] private Image borderTop, borderBottom, borderLeft, borderRight;
    [Tooltip("구멍을 감싸는 sci-fi 프레임(9-slice). 비우면 위 4변 테두리로 폴백.")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Button clickCatcher;
    [SerializeField] private RectTransform bannerBg;
    [SerializeField] private TMP_Text banner;
    [SerializeField] private TMP_Text continueLabel;

    // 딤 색 슬롯은 없앴다. 어둡게 덮는 색은 fullDim / dimTop~Right 이미지 각자의 Color 가 그린다.
    // 여기에 색을 넣어도 아무 데도 안 쓰이는 칸이었다.
    [Header("색")]
    [Tooltip("구멍 테두리 색(계속 라벨과 동일 금색).")]
    [SerializeField] private Color borderColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("강조 연출")]
    [Tooltip("구멍을 타깃보다 이만큼(px) 넉넉하게 판다.")]
    [SerializeField] private float spotlightPadPx = 8f;
    [SerializeField] private float borderThicknessPx = 3f;
    [Tooltip("표시 직후 잔여 입력으로 즉시 넘어가는 것 방지(초).")]
    [SerializeField] private float continueCooldown = 0.2f;
    [Tooltip("큰 네모에서 타깃으로 수렴하는 시간(초).")]
    [SerializeField] private float focusDuration = 0.28f;
    [Tooltip("수렴 시작 시 사방 확대량(px). 0으로 수렴한다.")]
    [SerializeField] private float focusStartExpandPx = 120f;
    [Tooltip("프레임 스프라이트 금테가 안쪽으로 들어가 있으면 그만큼 바깥으로 빼서 구멍 경계에 맞춘다.")]
    [SerializeField] private float frameOutsetPx = 0f;

    private bool _useSpriteFrame;
    private string _spotlightId;
    private Action _onContinue;
    private KeyCode _advanceKey;
    private string _bannerRaw;   // 배너 원문 보관 - 언어(번역 테이블) 갱신 시 다시 그리는 용
    private bool _active;
    private bool _suppressed;   // 설정창(ESC 일시정지)이 위에 떠서 잠시 숨김 - 닫히면 복귀
    private float _shownTime;
    private float _focusStartTime = -999f;

    /// <summary>현재 단계가 진행을 위해 요구하는 키 (없으면 None). GameUIController가 코치 중 그 키만 통과시키는 데 사용.</summary>
    public KeyCode ActiveAdvanceKey => _active ? _advanceKey : KeyCode.None;

    /// <summary>코치마크가 지금 화면에 떠 있는지 (HUD 자동표시에서 '설명 중 강제표시' 판단용).</summary>
    public bool IsActive => _active;

    private void Awake()
    {
        if (UIDuplicateGuard.Report(_i, this)) { Destroy(gameObject); return; }
        _i = this;

        if (canvasComp == null) canvasComp = GetComponent<Canvas>();
        if (raycaster == null) raycaster = GetComponent<GraphicRaycaster>();

        _useSpriteFrame = frameImage != null && frameImage.sprite != null;
        if (frameImage != null) frameImage.enabled = false;

        // ★튜토 첫 배너는 게임 시작 직후에 떠서, 웹에서 받아오는 번역 테이블보다 빠르다.
        //   그때 Loc.Get 은 한국어 폴백을 돌려주고, 배너는 한 번 쓰고 마는 글자라 영영 한국어로
        //   남는다(첫 배너만 번역 안 되던 QA 건). 테이블 도착/언어 변경 이벤트에 다시 그린다.
        Loc.OnLanguageChanged += RefreshBannerTexts;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        Loc.OnLanguageChanged -= RefreshBannerTexts;
        if (_i == this) _i = null;
    }

    // 표시 중인 배너/계속 라벨을 현재 언어로 다시 쓴다.
    //   배너 원문이 한국어(테이블 도착 전 표시)였으면 이제 번역이 잡히고,
    //   이미 번역돼 들어온 문자열이면 키 미스로 원문 그대로 나와 무해하다.
    private void RefreshBannerTexts()
    {
        if (!_active) return;
        if (banner != null && !string.IsNullOrEmpty(_bannerRaw)) banner.text = Loc.Get(_bannerRaw);
        if (continueLabel != null && continueLabel.gameObject.activeSelf)
            continueLabel.text = _advanceKey == KeyCode.None
                ? Loc.Get("아무 곳이나 클릭하여 계속")
                : $"{_advanceKey}" + " " + Loc.Get("키를 눌러 계속");
    }

    // ── 외부 API ──────────────────────────────────────────────────────

    /// <summary>안내 단계 표시: 배너 + 스포트라이트 + 계속(클릭 또는 지정 키).</summary>
    public void ShowContinueStep(string bannerText, string spotlightTargetId, Action onContinue, KeyCode advanceKey = KeyCode.None)
    {
        GameSfx.Play(SfxId.TutorialHighlight);   // 특정 부분 강조(코치마크/스포트라이트) 등장음
        _spotlightId = spotlightTargetId;
        _onContinue = onContinue;
        _advanceKey = advanceKey;
        _active = true;
        _shownTime = Time.unscaledTime;
        _focusStartTime = Time.unscaledTime;   // 이 단계 강조 수렴 효과 시작

        if (banner != null)
        {
            _bannerRaw = bannerText;
            banner.text = Loc.Get(bannerText ?? string.Empty);
            if (bannerBg != null) bannerBg.gameObject.SetActive(!string.IsNullOrEmpty(bannerText));
        }

        if (clickCatcher != null) clickCatcher.gameObject.SetActive(true);
        if (continueLabel != null)
        {
            continueLabel.gameObject.SetActive(true);
            continueLabel.text = advanceKey == KeyCode.None
                ? Loc.Get("아무 곳이나 클릭하여 계속")
                : $"{advanceKey}" + " " + Loc.Get("키를 눌러 계속");
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

    // 표시/숨김은 Canvas/Raycaster 토글로 처리한다(GameObject는 항상 활성 -> LateUpdate가 계속 돌아
    // 설정창 열고닫힘을 self-poll로 감지해 복귀할 수 있음). GameObject를 끄면 LateUpdate가 멈춰 복귀 불가.
    private void SetVisible(bool v)
    {
        if (canvasComp != null) canvasComp.enabled = v;
        if (raycaster != null) raycaster.enabled = v;
    }

    private void LateUpdate()
    {
        if (!_active) return;

        // 설정창(ESC 일시정지)은 WindowManager가 직접 열어 _currentState를 안 거치므로 여기서 직접 감지.
        // 떠 있는 동안 오버레이를 숨겨(진행도 멈춤) 설정창이 가장 앞에 보이게 하고, 닫히면 자동 복귀.
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

        // 계속 처리 - uGUI Button(EventSystem) 대신 Input 폴링 (나머지 튜토와 동일 경로, 확실히 작동).
        if (_onContinue == null) return;
        if (Time.unscaledTime - _shownTime <= continueCooldown) return;

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

                float nxMin = Mathf.Clamp01((Mathf.Min(bl.x, tr.x) - spotlightPadPx) / w);
                float nxMax = Mathf.Clamp01((Mathf.Max(bl.x, tr.x) + spotlightPadPx) / w);
                float nyMin = Mathf.Clamp01((Mathf.Min(bl.y, tr.y) - spotlightPadPx) / h);
                float nyMax = Mathf.Clamp01((Mathf.Max(bl.y, tr.y) + spotlightPadPx) / h);

                if (!hasTarget) { xMin = nxMin; xMax = nxMax; yMin = nyMin; yMax = nyMax; hasTarget = true; }
                else { xMin = Mathf.Min(xMin, nxMin); xMax = Mathf.Max(xMax, nxMax); yMin = Mathf.Min(yMin, nyMin); yMax = Mathf.Max(yMax, nyMax); }
            }
        }

        // 타깃 없으면 전체 딤(스트립 숨김), 있으면 4스트립으로 구멍 + 테두리/프레임
        if (fullDim != null) fullDim.enabled = !hasTarget;
        SetStripsEnabled(hasTarget);
        SetBordersEnabled(hasTarget && !_useSpriteFrame);
        if (frameImage != null) frameImage.enabled = hasTarget && _useSpriteFrame;

        // 계속 라벨 위치 - 타깃이 화면 하단 + 가로 중앙을 덮을 때(퀵슬롯 바)만 구멍 위로.
        PositionContinue(hasTarget, xMin, xMax, yMin, yMax);

        if (!hasTarget) { PositionBanner(true); return; }   // 타깃 없으면 배너 상단(기본)

        // ── 강조 효과 ── 큰 네모 -> 타깃으로 수렴(focus-in). 스트립/테두리에만 적용(라벨/배너는 실제 위치 기준).
        float ft = Mathf.Clamp01((Time.unscaledTime - _focusStartTime) / Mathf.Max(0.01f, focusDuration));
        float ease = 1f - (1f - ft) * (1f - ft);                 // ease-out quad
        float expandPx = Mathf.Lerp(focusStartExpandPx, 0f, ease);
        float ex = expandPx / Mathf.Max(1, Screen.width);
        float ey = expandPx / Mathf.Max(1, Screen.height);
        float bxMin = Mathf.Clamp01(xMin - ex), bxMax = Mathf.Clamp01(xMax + ex);
        float byMin = Mathf.Clamp01(yMin - ey), byMax = Mathf.Clamp01(yMax + ey);

        if (dimTop != null) SetAnchors(dimTop.rectTransform, 0f, byMax, 1f, 1f);
        if (dimBottom != null) SetAnchors(dimBottom.rectTransform, 0f, 0f, 1f, byMin);
        if (dimLeft != null) SetAnchors(dimLeft.rectTransform, 0f, byMin, bxMin, byMax);
        if (dimRight != null) SetAnchors(dimRight.rectTransform, bxMax, byMin, 1f, byMax);

        // 강조: 수렴 중엔 흐리게 시작 -> 정착 후 은은한 호흡(펄스). 프레임 스프라이트면 그걸, 아니면 4변 테두리.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);     // 0..1
        float frameAlpha = Mathf.Lerp(0.3f, 0.82f + 0.18f * pulse, ease);
        if (_useSpriteFrame)
        {
            // 9-slice 프레임을 구멍(확대 박스)에 맞춰 배치 - 코너 고정 크기, 변은 늘어남. 수렴/펄스 그대로 적용.
            var frt = frameImage.rectTransform;
            SetAnchors(frt, bxMin, byMin, bxMax, byMax);
            frt.offsetMin = new Vector2(-frameOutsetPx, -frameOutsetPx);
            frt.offsetMax = new Vector2(frameOutsetPx, frameOutsetPx);
            var fc = frameImage.color; fc.a = frameAlpha; frameImage.color = fc;
        }
        else
        {
            // 폴백: 4변 단색 테두리 (수렴 중 굵게->얇게 + 호흡)
            float thick = Mathf.Lerp(borderThicknessPx + 5f, borderThicknessPx + pulse * 1.5f, ease);
            SetBorderAlpha(frameAlpha);
            if (borderTop != null) SetEdge(borderTop.rectTransform, bxMin, byMax, bxMax, byMax, 0f, 0f, 0f, thick);
            if (borderBottom != null) SetEdge(borderBottom.rectTransform, bxMin, byMin, bxMax, byMin, 0f, -thick, 0f, 0f);
            if (borderLeft != null) SetEdge(borderLeft.rectTransform, bxMin, byMin, bxMin, byMax, -thick, 0f, 0f, 0f);
            if (borderRight != null) SetEdge(borderRight.rectTransform, bxMax, byMin, bxMax, byMax, 0f, 0f, thick, 0f);
        }

        // 배너가 타깃을 가리지 않게 - 타깃이 화면 위쪽이면 배너를 아래로. (실제 위치 기준)
        PositionBanner(yMax <= 0.7f);
    }

    private void SetStripsEnabled(bool v)
    {
        if (dimTop != null) dimTop.enabled = v;
        if (dimBottom != null) dimBottom.enabled = v;
        if (dimLeft != null) dimLeft.enabled = v;
        if (dimRight != null) dimRight.enabled = v;
    }

    private void SetBordersEnabled(bool v)
    {
        if (borderTop != null) borderTop.enabled = v;
        if (borderBottom != null) borderBottom.enabled = v;
        if (borderLeft != null) borderLeft.enabled = v;
        if (borderRight != null) borderRight.enabled = v;
    }

    // 4변 테두리 알파를 한 번에 설정 (수렴 페이드인 + 펄스용).
    private void SetBorderAlpha(float a)
    {
        var col = borderColor; col.a = Mathf.Clamp01(a);
        if (borderTop != null) borderTop.color = col;
        if (borderBottom != null) borderBottom.color = col;
        if (borderLeft != null) borderLeft.color = col;
        if (borderRight != null) borderRight.color = col;
    }

    // 배너 위치: top=true 상단, false 하단(계속 라벨 위). 타깃을 가리지 않도록.
    private void PositionBanner(bool top)
    {
        if (bannerBg == null) return;
        if (top) SetAnchors(bannerBg, 0.12f, 0.84f, 0.88f, 0.93f);
        else     SetAnchors(bannerBg, 0.12f, 0.27f, 0.88f, 0.38f);   // "클릭하여 계속"(하단)과 겹치지 않게 위로
    }

    // "클릭하여 계속" 라벨 위치 - 한 코치 세션(=한 영역) 동안 고정. 첫 계산값을 잠그고 Hide서 해제.
    // GuidedTour는 스텝 교체 시 Hide를 안 부르므로(내용만 교체) 락이 투어 내내 유지 -> 스텝마다 안 튐.
    private bool _continueLocked;
    private Vector2 _contAnchorMin, _contAnchorMax;

    // 계속 라벨 위치: 타깃이 화면 하단(yMin<0.22)이면서 가로 중앙(0.5)을 덮을 때만(퀵슬롯 바) 구멍 위로 올린다.
    private void PositionContinue(bool hasTarget, float holeLeftX, float holeRightX, float holeBottomY, float holeTopY)
    {
        if (continueLabel == null) return;
        var rt = continueLabel.rectTransform;

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
