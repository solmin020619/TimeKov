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
    private static readonly Dictionary<string, RectTransform> _targets = new();

    public static void RegisterTarget(string id, RectTransform rect)
    {
        if (string.IsNullOrEmpty(id) || rect == null) return;
        _targets[id] = rect;
    }

    public static void UnregisterTarget(string id, RectTransform rect)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_targets.TryGetValue(id, out var r) && r == rect) _targets.Remove(id);
    }

    // ── UI 요소 ───────────────────────────────────────────────────────
    private RectTransform _root;
    private Image _fullDim;
    private Image _top, _bottom, _left, _right;
    private Button _clickCatcher;
    private TMP_Text _banner;
    private TMP_Text _continueLabel;

    private string _spotlightId;
    private Action _onContinue;
    private bool _active;

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
    private const float SpotlightPadPx = 8f;

    private void Awake()
    {
        if (_i != null && _i != this) { Destroy(gameObject); return; }
        _i = this;
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy() { if (_i == this) _i = null; }

    // ── 외부 API ──────────────────────────────────────────────────────

    /// <summary>안내 단계 표시: 배너 + 스포트라이트 + 클릭하여 계속.</summary>
    public void ShowContinueStep(string banner, string spotlightTargetId, Action onContinue)
    {
        _spotlightId = spotlightTargetId;
        _onContinue = onContinue;
        _active = true;

        if (_banner != null)
        {
            _banner.text = banner ?? string.Empty;
            _banner.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(banner));
        }

        _clickCatcher.gameObject.SetActive(true);
        if (_continueLabel != null) _continueLabel.gameObject.SetActive(true);

        SetVisible(true);
        UpdateSpotlight();   // 첫 프레임부터 정확한 위치
    }

    public void Hide()
    {
        _active = false;
        _spotlightId = null;
        _onContinue = null;
        SetVisible(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    private void SetVisible(bool v)
    {
        if (_root != null) _root.gameObject.SetActive(v);
    }

    private void LateUpdate()
    {
        if (_active) UpdateSpotlight();
    }

    private void UpdateSpotlight()
    {
        RectTransform target = null;
        if (!string.IsNullOrEmpty(_spotlightId))
            _targets.TryGetValue(_spotlightId, out target);

        bool hasTarget = target != null && target.gameObject.activeInHierarchy;

        // 타깃 없으면 전체 딤(스트립 숨김), 있으면 4스트립으로 구멍
        _fullDim.enabled = !hasTarget;
        _top.enabled = _bottom.enabled = _left.enabled = _right.enabled = hasTarget;

        if (!hasTarget) return;

        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c);   // 0=BL 1=TL 2=TR 3=BR (world)

        Canvas tc = target.GetComponentInParent<Canvas>();
        Camera cam = (tc != null && tc.renderMode != RenderMode.ScreenSpaceOverlay) ? tc.worldCamera : null;

        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, c[2]);

        float w = Mathf.Max(1, Screen.width);
        float h = Mathf.Max(1, Screen.height);
        float xMin = Mathf.Clamp01((Mathf.Min(bl.x, tr.x) - SpotlightPadPx) / w);
        float xMax = Mathf.Clamp01((Mathf.Max(bl.x, tr.x) + SpotlightPadPx) / w);
        float yMin = Mathf.Clamp01((Mathf.Min(bl.y, tr.y) - SpotlightPadPx) / h);
        float yMax = Mathf.Clamp01((Mathf.Max(bl.y, tr.y) + SpotlightPadPx) / h);

        SetAnchors(_top.rectTransform, 0f, yMax, 1f, 1f);
        SetAnchors(_bottom.rectTransform, 0f, 0f, 1f, yMin);
        SetAnchors(_left.rectTransform, 0f, yMin, xMin, yMax);
        SetAnchors(_right.rectTransform, xMax, yMin, 1f, yMax);
    }

    private void OnClickCatcher()
    {
        _onContinue?.Invoke();
    }

    // ── UI 생성 ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;   // 거의 최상단 (다른 UI 위)

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        _root = (RectTransform)transform;

        _fullDim = NewImage("FullDim", _root, DimColor);
        Stretch(_fullDim.rectTransform, 0f, 0f, 1f, 1f);

        _top = NewImage("DimTop", _root, DimColor);
        _bottom = NewImage("DimBottom", _root, DimColor);
        _left = NewImage("DimLeft", _root, DimColor);
        _right = NewImage("DimRight", _root, DimColor);

        // 클릭 캐처 (투명, 전체 화면 — 클릭하여 계속)
        _clickCatcher = NewButton("ClickCatcher", _root);
        Stretch((RectTransform)_clickCatcher.transform, 0f, 0f, 1f, 1f);
        _clickCatcher.onClick.AddListener(OnClickCatcher);

        // 상단 배너 (배경 박스 + 텍스트)
        var bannerBg = NewImage("BannerBg", _root, new Color(0f, 0f, 0f, 0.85f));
        SetAnchors(bannerBg.rectTransform, 0.12f, 0.85f, 0.88f, 0.94f);
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
        _continueLabel.text = "▶  아무 곳이나 눌러 계속하기  ◀";
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
}
