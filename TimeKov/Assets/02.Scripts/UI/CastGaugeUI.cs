using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시전(채널) 게이지 - 화면 상단 중앙에 "무엇이 얼마나 진행됐는지"를 선으로 채워 보여주는 공용 HUD.
//   워프 충전(WarpManager)과 귀환석 채널링(ReturnStoneManager)이 같은 UI 를 공유한다.
//   덕코프식: 지점에 서 있으면 게이지가 차오르고, 다 차면 발동. 남은 시간도 함께 표시.
//
//   런타임에 자체 생성되는 싱글톤(씬 세팅 불필요). idle 일 때는 숨어 있다가 Begin -> Report -> Hide 로 구동.
//   호출측이 매 프레임 Report(진행도, 남은초) 를 넣어준다. 취소/완료 모두 Hide 로 정리.
public class CastGaugeUI : MonoBehaviour
{
    public static CastGaugeUI Instance { get; private set; }

    private CanvasGroup _cg;
    private Image _fill;
    private TextMeshProUGUI _label;
    private TextMeshProUGUI _count;
    private Image _icon;
    private Coroutine _fadeCo;
    private int _lastShownSec = -1;

    private static readonly Color Cyan = new Color32(80, 200, 235, 255);   // 스킬바/워프와 동일 시안
    private static readonly Color PillBg = new Color(0.06f, 0.09f, 0.13f, 0.88f);
    private static readonly Color TrackBg = new Color(0.02f, 0.03f, 0.05f, 0.9f);

    /// <summary>인스턴스를 보장(없으면 생성). 호출측은 이 결과로 Begin/Report/Hide 를 부른다.</summary>
    public static CastGaugeUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[CastGauge]");
        Instance = go.AddComponent<CastGaugeUI>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        SetVisible(false, instant: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 공개 API ──────────────────────────────────────────────────────
    /// <summary>게이지 표시 시작(0%). label 은 "워프 중"/"귀환 중" 등, icon 은 없으면 기본 링.</summary>
    public void Begin(string label, Sprite icon = null)
    {
        if (_label != null) _label.text = label;
        if (_icon != null)
        {
            // 커스텀 아이콘은 원색 그대로(흰색), 없으면 기본 시안 링.
            if (icon != null) { _icon.sprite = icon; _icon.color = Color.white; }
            else { _icon.sprite = UISpriteFactory.Ring(64, 6f); _icon.color = Cyan; }
        }
        if (_fill != null) _fill.fillAmount = 0f;
        _lastShownSec = -1;
        if (_count != null) _count.text = "";
        SetVisible(true, instant: false);
    }

    /// <summary>매 프레임 갱신. progress01=0~1(다 차면 발동), remainingSeconds=남은 시간.</summary>
    public void Report(float progress01, float remainingSeconds)
    {
        if (_fill != null) _fill.fillAmount = Mathf.Clamp01(progress01);

        // 초 단위가 바뀔 때만 텍스트 갱신(매 프레임 문자열 할당 방지).
        int s = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (s != _lastShownSec)
        {
            _lastShownSec = s;
            if (_count != null) _count.text = $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>완료/취소 공통 - 게이지를 숨긴다.</summary>
    public void Hide() => SetVisible(false, instant: false);

    // ── 표시/페이드 ───────────────────────────────────────────────────
    private void SetVisible(bool show, bool instant)
    {
        if (_cg == null) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (instant) { _cg.alpha = show ? 1f : 0f; return; }
        _fadeCo = StartCoroutine(FadeRoutine(show ? 1f : 0f));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float from = _cg.alpha;
        float t = 0f; const float dur = 0.15f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(from, target, t / dur);
            yield return null;
        }
        _cg.alpha = target;
        _fadeCo = null;
    }

    // ── 구성(런타임 생성) ─────────────────────────────────────────────
    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8000;   // HUD 위, 워프 검은 페이드(9000) 아래

        var scaler = gameObject.AddComponent<CanvasScaler>();   // 해상도 독립(다른 HUD와 동일 기준)
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        const float W = 340f, pillH = 42f, barH = 8f, gap = 6f;

        // 루트(상단중앙에서 아래로 약간)
        var root = NewChild("Root", transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(W, pillH + gap + barH));

        // 알약 배경
        var pill = NewImage("Pill", root, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(W, pillH));
        pill.sprite = UISpriteFactory.RoundedRect(64, 20);
        pill.type = Image.Type.Sliced;
        pill.color = PillBg;

        // 아이콘(기본 = 시안 링)
        _icon = NewImage("Icon", pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(26f, 26f));
        _icon.sprite = UISpriteFactory.Ring(64, 6f);
        _icon.color = Cyan;
        _icon.preserveAspect = true;

        // 라벨(아이콘 오른쪽)
        _label = NewText("Label", pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(W - 120f, pillH), 20f);
        _label.alignment = TextAlignmentOptions.Left;
        _label.fontStyle = FontStyles.Bold;

        // 남은시간(오른쪽)
        _count = NewText("Count", pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(70f, pillH), 20f);
        _count.alignment = TextAlignmentOptions.Right;
        _count.fontStyle = FontStyles.Bold;
        _count.color = Cyan;

        // 진행 바 트랙(알약 아래)
        var track = NewImage("Track", root, new Vector2(0.5f, 1f), new Vector2(0f, -(pillH + gap)), new Vector2(W, barH));
        track.sprite = UISpriteFactory.RoundedRect(32, 12);
        track.type = Image.Type.Sliced;
        track.color = TrackBg;

        // 채워지는 게이지(선이 왼쪽에서 오른쪽으로 참)
        _fill = NewImage("Fill", track.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, barH));
        _fill.sprite = UISpriteFactory.RoundedRect(32, 12);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 0f;
        _fill.color = Cyan;
    }

    // ── 헬퍼(ReturnStoneHudUI 와 동일 패턴) ────────────────────────────
    private static RectTransform NewChild(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = ap;
        rt.pivot = ap;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size)
    {
        var rt = NewChild(name, parent, ap, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 ap, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewChild(name, parent, ap, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        var font = HudFont();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static TMP_FontAsset _font;
    private static TMP_FontAsset HudFont()
    {
        if (_font != null) return _font;
        var any = FindFirstObjectByType<TMP_Text>();
        _font = any != null ? any.font : null;
        return _font;
    }
}
