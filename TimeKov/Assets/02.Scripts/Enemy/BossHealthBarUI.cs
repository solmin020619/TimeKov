using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 팰월드식 상단 보스 체력바. 런타임 지연 싱글톤(씬 세팅/프리팹 불필요).
// 보스 교전 시작 시 BossHealthBarUI.Show(health, 이름) 호출 -> 상단 중앙에 등장. 보스 사망/소멸 시 자동 페이드 아웃.
// 66%/33% 위치에 페이즈 눈금(포효 페이즈와 일치).
public class BossHealthBarUI : MonoBehaviour
{
    private static BossHealthBarUI _instance;
    private static bool _quitting;

    private CanvasGroup _cg;
    private Image _fill;
    private TMP_Text _nameText;
    private TMP_Text _subText;

    private EnemyHealth _target;
    private bool _visible;
    private Coroutine _fadeCo;

    // ── 외부 API ──
    public static void Show(EnemyHealth health, string bossName, string subtitle = null)
    {
        if (_quitting || health == null) return;
        var inst = Instance;
        if (inst != null) inst.ShowInternal(health, bossName, subtitle);
    }

    public static void Hide()
    {
        if (_instance != null) _instance.HideInternal();
    }

    private static BossHealthBarUI Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("BossHealthBarUI");
                _instance = go.AddComponent<BossHealthBarUI>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    // ── 구성 ──
    private static readonly Color BackCol  = new Color(0.04f, 0.06f, 0.09f, 0.55f);
    private static readonly Color TrackCol = new Color(0.10f, 0.05f, 0.06f, 0.92f);
    private static readonly Color FillCol  = new Color(0.86f, 0.16f, 0.18f, 1f);
    private static readonly Color FrameCol = new Color(0.85f, 0.78f, 0.55f, 0.55f);
    private static readonly Color NotchCol = new Color(1f, 1f, 1f, 0.5f);

    private void Build()
    {
        var canvasGo = new GameObject("BossBarCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // 포효 비네트(100) 위 = 어두워져도 보스바는 또렷
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = NewRect("Root", (RectTransform)canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(920f, 96f));
        _cg = root.gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;

        // 배경 패널(가독성)
        var back = NewRect("Back", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, 96f));
        Stretch(back);
        Img(back, UISpriteFactory.RoundedRect(48, 16), BackCol);

        // 이름
        _nameText = Txt("Name", root, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(880f, 40f), 30f, FontStyles.Bold);
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = new Color(0.96f, 0.95f, 0.92f, 1f);

        // 부제(작게)
        _subText = Txt("Sub", root, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(880f, 18f), 14f, FontStyles.Normal);
        _subText.alignment = TextAlignmentOptions.Center;
        _subText.color = new Color(0.78f, 0.6f, 0.62f, 0.9f);

        // 체력 트랙
        var track = NewRect("Track", root, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(872f, 22f));
        Img(track, UISpriteFactory.RoundedRect(32, 8), TrackCol);
        var frame = NewRect("Frame", track, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(frame);
        var frImg = Img(frame, UISpriteFactory.RoundedRect(32, 8), new Color(0, 0, 0, 0));
        var ol = frame.gameObject.AddComponent<UnityEngine.UI.Outline>();   // 전역 QuickOutline 충돌 회피(한정명 필수)
        ol.effectColor = FrameCol; ol.effectDistance = new Vector2(2f, -2f);

        // 채움(빨강, 좌->우 비움)
        var fillRt = NewRect("Fill", track, new Vector2(0f, 0.5f), new Vector2(3f, 0f), Vector2.zero);
        fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = new Vector2(3f, 3f); fillRt.offsetMax = new Vector2(-3f, -3f);
        _fill = Img(fillRt, UISpriteFactory.RoundedRect(32, 7), FillCol);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 1f;

        // 페이즈 눈금(66% / 33% = 포효 페이즈)
        AddNotch(track, 0.66f);
        AddNotch(track, 0.33f);

        root.gameObject.SetActive(true);
    }

    private void AddNotch(RectTransform track, float frac)
    {
        var n = NewRect("Notch", track, new Vector2(frac, 0.5f), Vector2.zero, new Vector2(2.5f, 18f));
        Img(n, null, NotchCol);
    }

    // ── 동작 ──
    private void ShowInternal(EnemyHealth health, string bossName, string subtitle)
    {
        if (_target != null) _target.OnDeath -= OnTargetDeath;
        _target = health;
        _target.OnDeath += OnTargetDeath;

        if (_nameText != null) _nameText.text = string.IsNullOrEmpty(bossName) ? "보스" : bossName;
        if (_subText != null) _subText.text = subtitle ?? "";
        if (_fill != null) _fill.fillAmount = SafeFrac();

        _visible = true;
        StartFade(1f);
    }

    private void HideInternal()
    {
        if (_target != null) { _target.OnDeath -= OnTargetDeath; _target = null; }
        _visible = false;
        StartFade(0f);
    }

    private void OnTargetDeath() => HideInternal();

    private void Update()
    {
        if (!_visible || _fill == null) return;
        if (_target == null) { HideInternal(); return; }   // 보스 GameObject 소멸 시
        float frac = SafeFrac();
        _fill.fillAmount = Mathf.Lerp(_fill.fillAmount, frac, Time.deltaTime * 8f);
    }

    private float SafeFrac()
    {
        if (_target == null || _target.maxHP <= 0f) return 0f;
        return Mathf.Clamp01(_target.currentHP / _target.maxHP);
    }

    private void StartFade(float to)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(to));
    }

    private IEnumerator FadeRoutine(float to)
    {
        float from = _cg != null ? _cg.alpha : 0f;
        float t = 0f; const float dur = 0.35f;
        while (t < dur && _cg != null)
        {
            t += Time.deltaTime;
            _cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        if (_cg != null) _cg.alpha = to;
        _fadeCo = null;
    }

    // ── 생성 헬퍼 ──
    private static RectTransform NewRect(string name, RectTransform parent, Vector2 anchorPivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchorPivot;
        rt.pivot = anchorPivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        if (sprite != null) img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Txt(string name, RectTransform parent, Vector2 anchorPivot, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
    {
        var rt = NewRect(name, parent, anchorPivot, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
