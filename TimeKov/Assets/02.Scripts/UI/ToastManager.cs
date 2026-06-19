// =====================================================================
// ToastManager.cs
// 공통 토스트(알림) — 전역 싱글톤. 클로드디자인 웹 프로토타입(Resources/toast)을
// Unity uGUI 로 이식. 화면 상단 중앙 pill, 스타일별 색/글리프, 큐/디바운스.
// 같은 메시지가 또 오면 새로 쌓지 않고 떠 있는 토스트를 살짝 흔들고 유지시간만 갱신.
//
// 호출(어디서든): ToastManager.Show("메시지", ToastStyle.Warning);
//   또는 ToastManager.Info/Success/Warning/Error("메시지");
//
// 씬 세팅 불필요 — 최초 호출 시 자동 생성(런타임 UI). 프리팹/스프라이트 자동 생성.
// =====================================================================

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ToastStyle { Info, Success, Warning, Error }

public class ToastManager : MonoBehaviour
{
    // ── 싱글톤 (지연 자동 생성) ─────────────────────────────────────
    private static ToastManager _i;
    public static ToastManager I
    {
        get
        {
            if (_i == null)
            {
                var go = new GameObject("[ToastManager]");
                _i = go.AddComponent<ToastManager>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    // ── 정적 진입점 ─────────────────────────────────────────────────
    public static void Show(string message, ToastStyle style = ToastStyle.Info, Action onHideStart = null)
        => I.ShowInternal(message, style, onHideStart);
    public static void Info(string m)    => Show(m, ToastStyle.Info);
    public static void Success(string m) => Show(m, ToastStyle.Success);
    public static void Warning(string m) => Show(m, ToastStyle.Warning);
    public static void Error(string m)   => Show(m, ToastStyle.Error);

    // ── 설정값 (디자인 확정값: TWEAK_DEFAULTS) ──────────────────────
    const float InOut = 0.24f;        // 인/아웃 시간
    const float Hold = 1.6f;          // 표시 유지
    const float DebounceSec = 2.5f;   // 같은 메시지 병합 창
    const int   MaxVisible = 3;       // 동시 표시 최대
    const float LayerTopPx = 90f;     // 화면 상단에서 내려온 거리 (상단 HUD 아래)
    const float SpacingPx = 10f;

    // 스타일 색 (info/success(gold)/warning/error)
    static readonly Color CInfo    = new Color32(205, 214, 224, 255);
    static readonly Color CSuccess = new Color32(255, 204,   0, 255);
    static readonly Color CWarning = new Color32(255, 154,  46, 255);
    static readonly Color CError   = new Color32(255,  91,  99, 255);
    static readonly Color CText    = new Color32(241, 245, 249, 255);
    static readonly Color CPill    = new Color32( 15,  20,  28, 200); // 다크 반투명 알약
    static readonly Color CPillBorder = new Color32(255, 255, 255, 26);

    static Color StyleColor(ToastStyle s) => s switch
    {
        ToastStyle.Success => CSuccess,
        ToastStyle.Warning => CWarning,
        ToastStyle.Error   => CError,
        _                  => CInfo,
    };

    // ── 런타임 상태 ─────────────────────────────────────────────────
    private RectTransform _layer;
    private Sprite _pillSprite;
    private readonly Dictionary<ToastStyle, Sprite> _glyphs = new();

    private class Item
    {
        public int id;
        public string message;
        public ToastStyle style;
        public Action onHideStart;
        public float lastShown;
        public bool leaving;
        public GameObject go;
        public RectTransform rt;
        public CanvasGroup cg;
        public Image icon;
        public TMP_Text msgText;
        public Tween hideTween;
    }

    private readonly List<Item> _items = new();
    private readonly Queue<Item> _queue = new();
    private int _seq;

    // ── 빌드 (캔버스 + 레이어 + 스프라이트) ─────────────────────────
    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500; // 게임플레이/HUD 위

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // GraphicRaycaster 없음 — 토스트는 입력을 가로채지 않는다.

        _pillSprite = MakeRoundedSprite(64, 64, 28f);

        var layerGo = new GameObject("ToastLayer", typeof(RectTransform));
        _layer = (RectTransform)layerGo.transform;
        _layer.SetParent(transform, false);
        _layer.anchorMin = new Vector2(0.5f, 1f);
        _layer.anchorMax = new Vector2(0.5f, 1f);
        _layer.pivot = new Vector2(0.5f, 1f);
        _layer.anchoredPosition = new Vector2(0f, -LayerTopPx);

        var vlg = layerGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = SpacingPx;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var csf = layerGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ── 표시 로직 ───────────────────────────────────────────────────
    private void ShowInternal(string message, ToastStyle style, Action onHideStart)
    {
        if (string.IsNullOrEmpty(message)) return;
        float now = Time.unscaledTime;

        // 같은 메시지가 아직 떠 있으면(디바운스 창 내) 새로 쌓지 않고
        // 떠 있는 토스트를 살짝 흔들어 다시 강조 + 유지시간만 갱신 (x2,x3 카운트 폐기)
        for (int k = 0; k < _items.Count; k++)
        {
            var it = _items[k];
            if (it.leaving || it.message != message) continue;
            if (now - it.lastShown > DebounceSec) continue;
            it.style = style;
            it.lastShown = now;
            ApplyStyle(it);
            PunchRepeat(it);
            ScheduleHide(it);
            return;
        }

        var item = new Item
        {
            id = ++_seq, message = message, style = style,
            onHideStart = onHideStart, lastShown = now,
        };

        int visible = 0;
        for (int k = 0; k < _items.Count; k++) if (!_items[k].leaving) visible++;
        if (visible >= MaxVisible) _queue.Enqueue(item);
        else Spawn(item);
    }

    private void Spawn(Item item)
    {
        BuildView(item);
        _items.Add(item);

        item.cg.alpha = 0f;
        item.rt.localScale = Vector3.one * 0.985f;
        item.cg.DOFade(1f, InOut).SetUpdate(true).SetEase(Ease.OutCubic);
        item.rt.DOScale(1f, InOut).SetUpdate(true).SetEase(Ease.OutCubic);

        ScheduleHide(item);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_layer);
    }

    private void ScheduleHide(Item item)
    {
        item.hideTween?.Kill();
        item.hideTween = DOVirtual.DelayedCall(Hold, () =>
        {
            try { item.onHideStart?.Invoke(); } catch { /* noop */ }
            StartLeave(item);
        }, ignoreTimeScale: true);
    }

    private void StartLeave(Item item)
    {
        if (item.leaving) return;
        item.leaving = true;
        item.hideTween?.Kill();

        item.cg.DOFade(0f, InOut).SetUpdate(true).SetEase(Ease.InCubic);
        item.rt.DOScale(0.98f, InOut).SetUpdate(true).SetEase(Ease.InCubic)
            .OnComplete(() => Remove(item));
    }

    // 같은 메시지가 또 왔을 때: 떠 있는 토스트를 살짝 튕겨 반응을 준다(스케일 punch).
    // VerticalLayoutGroup 이 위치/크기는 잡지만 localScale 은 안 건드려서 안전.
    private void PunchRepeat(Item item)
    {
        if (item.rt == null) return;
        item.rt.DOKill();
        item.rt.localScale = Vector3.one;
        item.rt.DOPunchScale(Vector3.one * 0.06f, 0.28f, 1, 0.6f).SetUpdate(true);
    }

    private void Remove(Item item)
    {
        _items.Remove(item);
        if (item.go != null) Destroy(item.go);

        // 대기열 승격
        int visible = 0;
        for (int k = 0; k < _items.Count; k++) if (!_items[k].leaving) visible++;
        if (_queue.Count > 0 && visible < MaxVisible)
            Spawn(_queue.Dequeue());

        if (_layer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_layer);
    }

    // ── 뷰 생성 ─────────────────────────────────────────────────────
    private void BuildView(Item item)
    {
        var go = new GameObject($"Toast_{item.id}", typeof(RectTransform));
        item.go = go;
        item.rt = (RectTransform)go.transform;
        item.rt.SetParent(_layer, false);

        var bg = go.AddComponent<Image>();
        bg.sprite = _pillSprite;
        bg.type = Image.Type.Sliced;
        bg.color = CPill;
        bg.raycastTarget = false;

        item.cg = go.AddComponent<CanvasGroup>();
        item.cg.blocksRaycasts = false;
        item.cg.interactable = false;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(26, 26, 14, 14);
        hlg.spacing = 15;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var minH = go.AddComponent<LayoutElement>();
        minH.minHeight = 64f;

        // 아이콘
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(item.rt, false);
        item.icon = iconGo.AddComponent<Image>();
        item.icon.sprite = GetGlyph(item.style);
        item.icon.raycastTarget = false;
        item.icon.preserveAspect = true;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.preferredWidth = 28; iconLe.preferredHeight = 28;
        iconLe.minWidth = 28; iconLe.minHeight = 28;

        // 메시지
        var msgGo = new GameObject("Msg", typeof(RectTransform));
        msgGo.transform.SetParent(item.rt, false);
        item.msgText = msgGo.AddComponent<TextMeshProUGUI>();
        item.msgText.text = item.message;
        item.msgText.fontSize = 22f;
        item.msgText.color = CText;
        item.msgText.alignment = TextAlignmentOptions.MidlineLeft;
        item.msgText.enableWordWrapping = false;
        item.msgText.overflowMode = TextOverflowModes.Overflow;
        item.msgText.raycastTarget = false;

        ApplyStyle(item);
    }

    private void ApplyStyle(Item item)
    {
        var c = StyleColor(item.style);
        if (item.icon != null) { item.icon.sprite = GetGlyph(item.style); item.icon.color = c; }
    }

    private Sprite GetGlyph(ToastStyle style)
    {
        if (!_glyphs.TryGetValue(style, out var sp))
        {
            sp = MakeGlyphSprite(style);
            _glyphs[style] = sp;
        }
        return sp;
    }

    // ── 둥근 사각형(pill) 스프라이트 생성 (9-slice) ─────────────────
    private static Sprite MakeRoundedSprite(int w, int h, float radius)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, radius, w - radius);
                float cy = Mathf.Clamp(fy, radius, h - radius);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float cov = Mathf.Clamp01(radius - d + 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(cov * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // ── 상태 글리프 스프라이트 생성 (toast.jsx 의 SVG 벡터 그대로) ──
    // 흰색으로 그리고 Image.color 로 스타일색 틴트. SVG(24x24, y-down) → tex(64x64, y-up).
    private static Sprite MakeGlyphSprite(ToastStyle style)
    {
        const int N = 64;
        const float S = N / 24f;
        float hw = 1.0f * S; // 선 반폭 (svg strokeWidth 2)
        var buf = new float[N * N];

        Vector2 P(float sx, float sy) => new Vector2(sx * S, (24f - sy) * S);

        void Plot(int x, int y, float cov)
        {
            if (x < 0 || x >= N || y < 0 || y >= N || cov <= 0f) return;
            int idx = y * N + x;
            if (cov > buf[idx]) buf[idx] = cov;
        }
        void Line(Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - hw - 1), 0, N - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + hw + 1), 0, N - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - hw - 1), 0, N - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + hw + 1), 0, N - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    Plot(x, y, Mathf.Clamp01(hw - d + 0.5f));
                }
        }
        void Ring(Vector2 c, float r)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    Plot(x, y, Mathf.Clamp01(hw - Mathf.Abs(d - r) + 0.5f));
                }
        }
        void Disc(Vector2 c, float r)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    Plot(x, y, Mathf.Clamp01(r - d + 0.5f));
                }
        }

        switch (style)
        {
            case ToastStyle.Success: // 체크
                Line(P(4f, 12.1f), P(9.2f, 17.3f));
                Line(P(9.2f, 17.3f), P(20f, 6.5f));
                break;
            case ToastStyle.Warning: // 삼각형 + !
                Line(P(12f, 3.6f), P(21.2f, 19.5f));
                Line(P(21.2f, 19.5f), P(2.8f, 19.5f));
                Line(P(2.8f, 19.5f), P(12f, 3.6f));
                Line(P(12f, 10f), P(12f, 14.2f));
                Disc(P(12f, 17.4f), hw);
                break;
            case ToastStyle.Error: // 원 + !
                Ring(P(12f, 12f), 9f * S);
                Line(P(12f, 7.4f), P(12f, 12.4f));
                Disc(P(12f, 16.2f), hw);
                break;
            default: // info: 원 + i
                Ring(P(12f, 12f), 9f * S);
                Line(P(12f, 11.2f), P(12f, 16.4f));
                Disc(P(12f, 7.9f), hw);
                break;
        }

        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[N * N];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(buf[i]) * 255));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }
}
