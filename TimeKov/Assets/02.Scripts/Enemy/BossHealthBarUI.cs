using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 팰월드식 상단 보스 체력바. 런타임 지연 싱글톤(씬 세팅/프리팹 불필요).
// 보스 교전 시작 시 BossHealthBarUI.Show(health, 이름) 호출 -> 상단 중앙에 등장. 보스 사망/소멸 시 자동 페이드 아웃.
public class BossHealthBarUI : MonoBehaviour
{
    private static BossHealthBarUI _instance;
    private static bool _quitting;

    private CanvasGroup _cg;
    private Image _fill;
    private RectTransform _fillRt;   // 채움 막대(폭으로 비운다 - Filled 아님)
    private float _fillMaxWidth;     // 채움 100% 일 때 폭
    private float _fillFrac = 1f;    // 현재 표시 비율(부드럽게 보간)
    private TMP_Text _nameText;
    private TMP_Text _subText;
    private RectTransform _decoLine1, _decoLine2;   // 수식어 양옆 장식 선(수식어 없으면 숨김)

    private EnemyHealth _target;
    private bool _visible;
    private Camera _cam;

    // 가까이 있을 때만 표시: 카메라-보스 거리가 이 값 안일 때만 보임(보스 어그로/리쉬보다 약간 넓게).
    private const float ShowDistance = 40f;
    private const float FadeDur = 0.35f;

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
    private static readonly Color FillCol     = new Color(0.86f, 0.16f, 0.18f, 1f);
    private static readonly Color TrackBgCol  = new Color(0.06f, 0.02f, 0.03f, 0.80f);   // 빈 체력 트랙(어두운 배경)
    private static readonly Color TextEdgeCol = new Color(0.30f, 0.03f, 0.04f, 0.95f);   // 이름/수식어 공통 빨강 엣지(빨강/하양 통일)

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

        // 루트: 배경 패널 없음(월드 위에 텍스트+바만). 위->아래: 수식어(설명) / 이름 / 체력바.
        var root = NewRect("Root", (RectTransform)canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(820f, 108f));
        _cg = root.gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f; _cg.blocksRaycasts = false; _cg.interactable = false;

        // 수식어 행: [선] 수식어 [선] - 텍스트 폭에 맞춰 양옆에 딱 붙는다(팰월드식 " - a - " 느낌).
        // HorizontalLayoutGroup 이 폭을 재서 항상 hug. childForceExpand=false 라 높이 늘림 이슈 없음.
        var subRow = NewRect("SubRow", root, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(800f, 22f));
        var hlg = subRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 12f;
        hlg.childControlWidth = true;  hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        _decoLine1 = MakeDecoLine(subRow);   // 수식어 왼쪽 선

        // 수식어(설명) - 이름과 '똑같은' 엣지 효과(짙은 빨강). 알파로 차등하지 않고 크기만 다르게.
        _subText = Txt("Sub", subRow, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 22f), 17f, FontStyles.Normal);
        _subText.alignment = TextAlignmentOptions.Center;
        _subText.color = new Color(0.93f, 0.95f, 0.97f, 1f);
        _subText.characterSpacing = 5f;
        AddTextOutline(_subText, TextEdgeCol, 0.2f);

        _decoLine2 = MakeDecoLine(subRow);   // 수식어 오른쪽 선

        // 이름 - 수식어 아래, 크고 굵게. 흰 글자 + 짙은 빨강 엣지(빨강/하양 통일).
        _nameText = Txt("Name", root, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(800f, 44f), 32f, FontStyles.Bold);
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = new Color(0.98f, 0.97f, 0.94f, 1f);
        AddTextOutline(_nameText, TextEdgeCol, 0.22f);

        // 체력 트랙(바닥) - 절차 둥근사각 9-slice(Sliced). Filled 가 아니라 Sliced 라 늘려도 끝이 안 뾰족.
        // 디자인 프레임 스프라이트(hpbar_rect)는 양끝에 세로 장식선이 있어 안 쓴다 - 깔끔한 단색 트랙.
        var track = NewRect("Track", root, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(760f, 20f));
        Img(track, UISpriteFactory.RoundedRect(32, 8), TrackBgCol);

        // 채움(빨강) - 같은 9-slice(Sliced). fillAmount(Filled) 대신 '폭'을 직접 줄여 비운다
        // (Filled 는 스프라이트를 통째로 늘려 끝이 뾰족해진다). 좌측 고정, 폭 = 트랙폭 * 비율.
        const float fillInset = 3f;
        var fillRt = NewRect("Fill", track, new Vector2(0f, 0.5f), new Vector2(fillInset, 0f), new Vector2(0f, 20f - fillInset * 2f));
        _fill = Img(fillRt, UISpriteFactory.RoundedRect(32, 8), FillCol);
        _fillRt = fillRt;
        _fillMaxWidth = 760f - fillInset * 2f;
        _fillFrac = 1f;
        _fillRt.sizeDelta = new Vector2(_fillMaxWidth, _fillRt.sizeDelta.y);

        root.gameObject.SetActive(true);
    }

    // TMP 텍스트에 색 아웃라인(인스턴스 머티리얼에만 - 공유 머티리얼 오염 방지).
    // 배경 패널 없이 밝은 씬(눈밭/초원)에서도 읽히게 + 이름/수식어 동일한 빨강 엣지로 통일.
    private static void AddTextOutline(TMP_Text t, Color color, float width)
    {
        if (t == null) return;
        try
        {
            var mat = t.fontMaterial;   // getter 접근 시 인스턴스 머티리얼 생성
            if (mat == null) return;
            mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        }
        catch { /* 아웃라인 실패해도 바 자체는 정상 동작 */ }
    }

    // 수식어 양옆 장식 선 한 개 (HorizontalLayoutGroup 자식). 얇은 빨강, 폭 고정(56px)이라 텍스트 옆에 딱 붙는다.
    private static RectTransform MakeDecoLine(RectTransform parent)
    {
        var rt = NewRect("Deco", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 3f));
        Img(rt, null, new Color(FillCol.r, FillCol.g, FillCol.b, 0.5f));
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 56f; le.minWidth = 56f; le.flexibleWidth = 0f;
        return rt;
    }

    // ── 동작 ──
    private void ShowInternal(EnemyHealth health, string bossName, string subtitle)
    {
        if (_target != null) _target.OnDeath -= OnTargetDeath;
        _target = health;
        _target.OnDeath += OnTargetDeath;

        if (_nameText != null) _nameText.text = string.IsNullOrEmpty(bossName) ? "보스" : bossName;
        if (_subText != null) _subText.text = subtitle ?? "";
        // 수식어가 없으면 양옆 장식 선도 숨긴다(" -  - " 처럼 텅 빈 선만 뜨는 걸 방지).
        bool hasSub = !string.IsNullOrEmpty(subtitle);
        if (_decoLine1 != null) _decoLine1.gameObject.SetActive(hasSub);
        if (_decoLine2 != null) _decoLine2.gameObject.SetActive(hasSub);
        _fillFrac = SafeFrac();
        if (_fillRt != null) _fillRt.sizeDelta = new Vector2(_fillMaxWidth * _fillFrac, _fillRt.sizeDelta.y);

        _visible = true;   // 알파 페이드인은 Update가 거리/UI 조건 보고 처리
    }

    private void HideInternal()
    {
        if (_target != null) { _target.OnDeath -= OnTargetDeath; _target = null; }
        _visible = false;
    }

    private void OnTargetDeath() => HideInternal();

    private void Update()
    {
        if (_cg == null) return;

        // 표시 조건: 교전 중(_visible) + 타깃 생존 + 일정 거리 안 + 막는 UI(설정창/인벤 등) 안 떠 있음.
        bool show = _visible && _target != null && WithinShowDistance() && !BlockingUIOpen();

        // 설정창은 timeScale=0 으로 게임을 멈춰 Time.deltaTime=0 이 되므로 페이드가 안 먹는다 -> unscaled 사용.
        float targetAlpha = show ? 1f : 0f;
        _cg.alpha = Mathf.MoveTowards(_cg.alpha, targetAlpha, Time.unscaledDeltaTime / FadeDur);

        if (_fillRt != null && _target != null)
        {
            _fillFrac = Mathf.Lerp(_fillFrac, SafeFrac(), Time.unscaledDeltaTime * 8f);
            _fillRt.sizeDelta = new Vector2(_fillMaxWidth * _fillFrac, _fillRt.sizeDelta.y);
        }
    }

    // 카메라-보스 거리가 ShowDistance 안인지 (멀리 가면 자동 숨김 -> 보스 리쉬 리셋과 짝).
    private bool WithinShowDistance()
    {
        if (_target == null) return false;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return true;
        float sqr = (_cam.transform.position - _target.transform.position).sqrMagnitude;
        return sqr <= ShowDistance * ShowDistance;
    }

    // 설정/인벤/공장 등 막는 UI가 떠 있으면 보스바를 숨긴다(그 위로 덮어쓰던 문제 수정).
    private static bool BlockingUIOpen()
    {
        var gui = GameUIController.Instance;
        return gui != null && gui.IsUIBlocking();
    }

    private float SafeFrac()
    {
        if (_target == null || _target.maxHP <= 0f) return 0f;
        return Mathf.Clamp01(_target.currentHP / _target.maxHP);
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
