using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 귀환석 HUD — 스킬바(V/RMB/Q/E/R)에 통합된 슬롯. ReturnStoneManager가 런타임 생성/구동한다.
//   스킬바의 첫 자식(맨 왼쪽 = V 옆)으로 들어가, 같은 프레임/링/키뱃지 스프라이트와 구조를 써
//   디자인·위치·페이드(스킬바 CanvasGroup 상속)를 스킬 슬롯과 동일하게 맞춘다.
//   발동은 H키(ReturnStoneManager) — 다른 슬롯도 키 발동이라 통일된다. HUD는 표시 전용.
//   아이콘 미지정 시 절차적 크리스탈 젬을 사용.
public class ReturnStoneHudUI : MonoBehaviour
{
    private ReturnStoneManager _mgr;
    private CanvasGroup _cg;      // 미보유 dim (부모 스킬바 페이드와 곱해짐)
    private Image _ring;
    private Image _icon;
    private TextMeshProUGUI _cdText;

    private static readonly Color SkillRing = new Color32(80, 200, 235, 255);   // 스킬바와 동일한 시안
    private static readonly Color RingDim   = new Color(0.45f, 0.5f, 0.55f, 1f);

    public static ReturnStoneHudUI Build(ReturnStoneManager mgr, SkillBarUI bar, float size)
    {
        if (bar == null) { Debug.LogWarning("[ReturnStone] 스킬바를 찾지 못해 HUD 생성 실패."); return null; }

        var go = new GameObject("ReturnStoneSlot", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(bar.transform, false);
        rt.SetSiblingIndex(0);   // 맨 왼쪽(V 옆)
        var hud = go.AddComponent<ReturnStoneHudUI>();
        hud.Construct(mgr, rt, size);
        return hud;
    }

    private void Construct(ReturnStoneManager mgr, RectTransform colRt, float size)
    {
        _mgr = mgr;

        const float badgeH = 22f, gap = 12f;
        float colH = size + gap + badgeH;   // 스킬 슬롯과 동일 구조(원 + 간격 + 키뱃지)

        colRt.anchorMin = colRt.anchorMax = new Vector2(0.5f, 0.5f);
        colRt.pivot = new Vector2(0.5f, 0.5f);
        colRt.sizeDelta = new Vector2(size, colH);
        var le = colRt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;
        le.preferredHeight = colH; le.minHeight = colH;

        _cg = colRt.gameObject.AddComponent<CanvasGroup>();

        var frameSp = Res("slot_frame_circle");
        var ringSp  = Res("cooldown_ring");
        var badgeSp = Res("key_badge");

        // 원형 슬롯(프레임 = 이 오브젝트의 Image) — 스킬 슬롯과 동일
        var circle = NewChild("Circle", colRt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
        var frame = circle.gameObject.AddComponent<Image>();
        frame.sprite = frameSp != null ? frameSp : CircleSprite();
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        if (frameSp == null) frame.color = new Color(0.10f, 0.14f, 0.20f, 0.95f);

        // 아이콘(지정 없으면 절차 생성 크리스탈 젬). 스킬 아이콘과 시각 크기를 맞춤.
        _icon = NewImage("Icon", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.68f, size * 0.68f));
        _icon.sprite = _mgr.HudIcon != null ? _mgr.HudIcon : Gem();
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        // 쿨다운 링(스킬과 동일 sprite/색/방식 — 링이 줄며 쿨타임 표시)
        _ring = NewImage("Ring", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        _ring.sprite = ringSp != null ? ringSp : CircleSprite();
        _ring.type = Image.Type.Filled;
        _ring.fillMethod = Image.FillMethod.Radial360;
        _ring.fillOrigin = (int)Image.Origin360.Top;
        _ring.fillClockwise = true;
        _ring.fillAmount = 1f;
        _ring.color = SkillRing;
        _ring.raycastTarget = false;

        // 남은시간 텍스트(스킬 Sec 텍스트와 동일 위치/스타일)
        _cdText = NewText("Sec", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), size * 0.34f);
        _cdText.fontStyle = FontStyles.Bold;
        _cdText.text = "";

        // 키 뱃지(원 아래) — 스킬 슬롯과 동일
        if (badgeSp != null)
            NewImage("KeyBadge", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(48f, 22f)).sprite = badgeSp;
        var keyT = NewText("Key", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(50f, 22f), 14f);
        keyT.text = _mgr.UseKey == KeyCode.None ? "" : _mgr.UseKey.ToString();

        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (_mgr == null) return;

        bool owned = _mgr.IsOwned;
        _cg.alpha = owned ? 1f : 0.4f;   // 부모(스킬바) 페이드와 곱해짐

        if (!owned)
        {
            _ring.fillAmount = 1f; _ring.color = RingDim;
            _cdText.text = "";
            SetIconAlpha(0.6f);
            return;
        }

        if (_mgr.IsChanneling)
        {
            _ring.fillAmount = 1f; _ring.color = SkillRing;
            _cdText.text = "…";
            SetIconAlpha(1f);
            return;
        }

        float rem = _mgr.CooldownRemaining;
        if (rem > 0f)
        {
            float total = Mathf.Max(1f, _mgr.CooldownTotal);
            _ring.fillAmount = Mathf.Clamp01(rem / total);   // 링이 줄며 쿨타임 진행 표시
            _ring.color = RingDim;
            int s = Mathf.CeilToInt(rem);
            _cdText.text = $"{s / 60}:{s % 60:00}";           // mm:ss
            SetIconAlpha(0.5f);
        }
        else
        {
            _ring.fillAmount = 1f; _ring.color = SkillRing;
            _cdText.text = "";
            SetIconAlpha(1f);
        }
    }

    private void SetIconAlpha(float a)
    {
        if (_icon == null) return;
        var c = _icon.color; c.a = a; _icon.color = c;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────
    private static Sprite Res(string n) => Resources.Load<Sprite>("SkillBar/" + n);

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
        t.textWrappingMode = TextWrappingModes.NoWrap;   // 스킬바와 동일
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

    // ── 절차적 스프라이트(스킬바 스프라이트가 없을 때 폴백 + 젬 아이콘) ──────
    private static Sprite _circle;
    private static Sprite CircleSprite()
    {
        if (_circle != null) return _circle;
        const int R = 64;
        var tex = NewTex(R * 2, R * 2);
        var px = new Color[R * 2 * R * 2];
        float edge = R - 1.5f;
        for (int y = 0; y < R * 2; y++)
            for (int x = 0; x < R * 2; x++)
            {
                float dx = x - R + 0.5f, dy = y - R + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * R * 2 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge - d + 1f));
            }
        return _circle = Bake(tex, px);
    }

    // 크리스탈 젬(세로형 육각 크리스탈, 청록) — 귀환'석' 아이콘.
    private static Sprite _gem;
    private static Sprite Gem()
    {
        if (_gem != null) return _gem;
        const int W = 96, H = 128;
        var tex = NewTex(W, H);
        var px = new Color[W * H];
        Color crystal = new Color(0.34f, 0.80f, 1f);
        for (int j = 0; j < H; j++)
        {
            float y = j / (float)(H - 1) * 2f - 1f;
            float halfW = CrystalHalfWidth(y);
            for (int i = 0; i < W; i++)
            {
                float x = i / (float)(W - 1) * 2f - 1f;
                float ax = Mathf.Abs(x);
                float inside = halfW - ax;
                if (inside <= 0f) { px[j * W + i] = Color.clear; continue; }

                float shade = 0.62f + x * 0.18f + y * 0.16f;
                shade += Mathf.Clamp01(1f - ax * 6f) * 0.16f;
                shade = Mathf.Clamp(shade, 0.28f, 1.25f);
                Color c = crystal * shade;
                float edgeLine = Mathf.Clamp01(1f - inside * 8f);
                c = Color.Lerp(c, new Color(0.85f, 0.96f, 1f), edgeLine * 0.5f);
                c.a = Mathf.Clamp01(inside * W * 0.5f);
                px[j * W + i] = c;
            }
        }
        return _gem = Bake(tex, px);
    }

    private static float CrystalHalfWidth(float y)
    {
        const float maxHalf = 0.6f, shoulder = 0.45f;
        float ay = Mathf.Abs(y);
        if (ay <= shoulder) return maxHalf * (0.85f + 0.15f * (1f - ay / shoulder));
        return maxHalf * (1f - (ay - shoulder) / (1f - shoulder));
    }

    private static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

    private static Sprite Bake(Texture2D tex, Color[] px)
    {
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
