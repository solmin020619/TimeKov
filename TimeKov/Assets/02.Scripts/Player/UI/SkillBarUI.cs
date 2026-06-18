using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 명조(Wuthering Waves)식 가로 5칸 스킬바. 왼쪽부터 [퀵슬롯][대쉬][Q][E][R].
// 쿨다운 = 테두리 시계방향 색 링 + (스킬만) 남은 초 숫자. 대쉬 = 숫자 없이 깜빡임, 퀵슬롯 = 개수.
// PlayerHudUI가 런타임 생성한다. 디자인 스프라이트 = Resources/SkillBar, 없으면 절차 생성 폴백.
public class SkillBarUI : MonoBehaviour
{
    enum Kind { Skill, Dash, Quick }

    class Slot
    {
        public Kind kind;
        public SkillSheetId skillId;
        public RectTransform circle;
        public Image icon, ring, flash;
        public TMP_Text seconds, count;
        public Image countBadge;
        public int shownItemId = int.MinValue;
    }

    private Player _player;
    private readonly List<Slot> _slots = new();
    private readonly List<(string id, RectTransform rt)> _spotlights = new();
    private TMP_FontAsset _font;   // HUD 폰트 (PlayerHudUI에서 넘겨받아 통일)
    private Slot _quickSlot;
    private Coroutine _flashCo;

    private static readonly Color SkillRing = new Color32(80, 200, 235, 255);
    private static readonly Color DashRing  = new Color32(255, 168, 56, 255);   // 앰버 - 스킬 시안 림과 대비되게(돌아가는 게 잘 보이도록)
    private static readonly Color QuickTint = new Color32(150, 232, 188, 255);   // 퀵슬롯 프레임만 살짝 초록(소모품 구분)
    private static readonly Color DashIcon  = new Color32(190, 205, 220, 255);
    private static readonly Color TextMain  = new Color32(232, 240, 247, 255);

    private const float SkillSize = 90f;
    private const float UtilSize  = 78f;

    private static Sprite Load(string n) => Resources.Load<Sprite>("SkillBar/" + n);

    public void Setup(RectTransform canvasParent, Player player, Sprite[] skillIcons, KeyCode quickKey, TMP_FontAsset font)
    {
        _player = player;
        _font = font;

        var frameSp = Load("slot_frame_circle");
        var ringSp  = Load("cooldown_ring");   // 디자인 링 사용(절차 링은 모양이 어색해서 되돌림)
        var countBadge = Load("count_badge");
        var keyBadge   = Load("key_badge");
        var dashIcon   = Load("Skill_Icon_Dash");

        var rt = (RectTransform)transform;
        rt.SetParent(canvasParent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-44f, 46f);   // 우하단. 위치는 인스펙터/씬에서 미세조정 가능.

        var hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 18f;
        hlg.childAlignment = TextAnchor.LowerCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var fit = gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 왼쪽부터: 퀵슬롯, 대쉬, Q, E, R
        BuildSlot(Kind.Quick, default, UtilSize, frameSp, ringSp, null, countBadge, keyBadge, KeyLabel(quickKey), QuickTint, null);
        BuildSlot(Kind.Dash,  default, UtilSize, frameSp, ringSp, dashIcon, null, keyBadge, "RMB", Color.white, null);
        BuildSlot(Kind.Skill, SkillSheetId.Skill1, SkillSize, frameSp, ringSp, Pick(skillIcons, 0), null, keyBadge, "Q", Color.white, "skill_q");
        BuildSlot(Kind.Skill, SkillSheetId.Skill2, SkillSize, frameSp, ringSp, Pick(skillIcons, 1), null, keyBadge, "E", Color.white, "skill_e");
        BuildSlot(Kind.Skill, SkillSheetId.Skill3, SkillSize, frameSp, ringSp, Pick(skillIcons, 2), null, keyBadge, "R", Color.white, "skill_r");

        _quickSlot = _slots.Find(s => s.kind == Kind.Quick);
        if (player.QuickSlot != null) player.QuickSlot.OnUsed += HandleQuickUsed;
    }

    static Sprite Pick(Sprite[] arr, int i) => (arr != null && i < arr.Length) ? arr[i] : null;

    void BuildSlot(Kind kind, SkillSheetId skillId, float size, Sprite frameSp, Sprite ringSp,
                   Sprite iconSp, Sprite countBadge, Sprite keyBadge, string keyText, Color frameColor,
                   string spotlightId)
    {
        float colH = size + 12f + 22f;   // 원 + 간격 + 키 배지

        var colRt = NewChild(kind + "Slot", (RectTransform)transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, colH));
        var le = colRt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size;
        le.preferredHeight = colH; le.minHeight = colH;

        // 원형 슬롯 (프레임 = 이 오브젝트의 Image)
        var circle = NewChild("Circle", colRt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size));
        var frame = circle.gameObject.AddComponent<Image>();
        frame.sprite = frameSp;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.color = frameColor;
        frame.raycastTarget = false;

        // 아이콘
        var icon = NewImage("Icon", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.58f, size * 0.58f));
        icon.preserveAspect = true;
        if (iconSp != null)
        {
            icon.sprite = iconSp;
            if (kind == Kind.Dash) icon.color = DashIcon;
        }
        else icon.enabled = false;

        // 쿨다운 링 (스킬/대쉬만)
        Image ring = null;
        TMP_Text seconds = null;
        if (kind != Kind.Quick)
        {
            ring = NewImage("Ring", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            ring.sprite = ringSp;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;
            ring.color = (kind == Kind.Dash) ? DashRing : SkillRing;

            if (kind == Kind.Skill)
            {
                seconds = NewText("Sec", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), size * 0.4f);
                seconds.fontStyle = FontStyles.Bold;
                seconds.text = "";
            }
        }

        // 개수 배지 + 사용 플래시 (퀵슬롯만)
        TMP_Text count = null;
        Image countBadgeImg = null;
        Image flashImg = null;
        if (kind == Kind.Quick)
        {
            if (countBadge != null)
            {
                countBadgeImg = NewImage("CountBadge", circle, new Vector2(1f, 0f), new Vector2(5f, -3f), new Vector2(34f, 22f));
                countBadgeImg.sprite = countBadge;
            }
            count = NewText("Count", circle, new Vector2(1f, 0f), new Vector2(5f, -3f), new Vector2(36f, 22f), 16f);
            count.fontStyle = FontStyles.Bold;

            // 사용 시 번쩍이는 오버레이(맨 위, 평소 투명)
            flashImg = NewImage("Flash", circle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            flashImg.sprite = UISpriteFactory.Disc(128);
            flashImg.color = new Color(1f, 1f, 1f, 0f);
        }

        // 키 배지 (원 아래)
        if (keyBadge != null)
            NewImage("KeyBadge", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(48f, 22f)).sprite = keyBadge;
        var keyT = NewText("Key", colRt, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(50f, 22f), 14f);
        keyT.text = keyText;

        if (!string.IsNullOrEmpty(spotlightId))
        {
            TutorialOverlay.RegisterTarget(spotlightId, circle);
            _spotlights.Add((spotlightId, circle));
        }

        _slots.Add(new Slot { kind = kind, skillId = skillId, circle = circle, icon = icon, ring = ring, flash = flashImg, seconds = seconds, count = count, countBadge = countBadgeImg });
    }

    private void Update()
    {
        if (_player == null) return;
        for (int i = 0; i < _slots.Count; i++) UpdateSlot(_slots[i]);
    }

    void UpdateSlot(Slot s)
    {
        if (s.kind == Kind.Skill)
        {
            if (_player.Skill == null) return;
            float readiness = Mathf.Clamp01(_player.Skill.GetGauge(s.skillId) / 100f);
            bool ready = readiness >= 0.999f;
            if (s.ring != null) s.ring.fillAmount = readiness;
            if (s.icon != null && s.icon.enabled) SetAlpha(s.icon, ready ? 1f : 0.22f);
            if (s.seconds != null)
            {
                float rem = _player.Skill.GetCooldown(s.skillId);
                s.seconds.text = rem > 0.05f ? Mathf.CeilToInt(rem).ToString() : "";
            }
        }
        else if (s.kind == Kind.Dash)
        {
            if (_player.Dash == null) return;
            float max = _player.Dash.MaxCooldown;
            float rem = _player.Dash.CooldownRemaining;
            float readiness = max > 0f ? Mathf.Clamp01(1f - rem / max) : 1f;
            bool ready = !_player.Dash.IsOnCooldown && !_player.Dash.IsDashing;
            if (s.ring != null) s.ring.fillAmount = readiness;
            if (s.icon != null && s.icon.enabled) SetAlpha(s.icon, ready ? 1f : 0.4f);
        }
        else // Quick
        {
            var qs = _player.QuickSlot;
            if (qs == null) return;
            int id = qs.RegisteredItemId;
            int cnt = qs.Count;

            if (id != s.shownItemId)
            {
                s.shownItemId = id;
                var data = id > 0 ? ItemDatabase.GetItem(id) : null;
                var sp = data != null ? ItemDatabase.GetIcon(data.iconKey) : null;
                if (s.icon != null) { s.icon.sprite = sp; s.icon.enabled = sp != null; }
            }

            bool usable = id > 0 && cnt > 0;
            if (s.icon != null && s.icon.enabled) SetAlpha(s.icon, usable ? 1f : 0.4f);
            if (s.countBadge != null) s.countBadge.enabled = id > 0;   // 빈 슬롯엔 개수 배지 숨김
            if (s.count != null)
            {
                s.count.text = id > 0 ? cnt.ToString() : "";
                s.count.color = cnt > 0 ? TextMain : new Color32(220, 110, 110, 255);
            }
        }
    }

    private void OnDestroy()
    {
        if (_player != null && _player.QuickSlot != null) _player.QuickSlot.OnUsed -= HandleQuickUsed;
        foreach (var (id, rt) in _spotlights) TutorialOverlay.UnregisterTarget(id, rt);
        _spotlights.Clear();
    }

    // 퀵슬롯 사용 성공 시 슬롯에 흰 빛 번쩍(숫자 안 봐도 "썼다"가 보이게).
    void HandleQuickUsed()
    {
        if (_quickSlot == null || _quickSlot.flash == null) return;
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(FlashRoutine(_quickSlot));
    }

    IEnumerator FlashRoutine(Slot s)
    {
        var flash = s.flash;
        if (flash == null) yield break;
        var frt = flash.rectTransform;
        float dur = 0.32f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            frt.localScale = Vector3.one * (1f + 0.5f * k);
            var c = flash.color; c.a = 0.75f * (1f - k); flash.color = c;
            yield return null;
        }
        var cc = flash.color; cc.a = 0f; flash.color = cc;
        frt.localScale = Vector3.one;
        _flashCo = null;
    }

    // ── 생성 헬퍼 ──
    static RectTransform NewChild(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image NewImage(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = NewChild(name, parent, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    TMP_Text NewText(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewChild(name, parent, anchor, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = TextMain;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; g.color = c; }
    }

    static string KeyLabel(KeyCode key)
    {
        string s = key.ToString();
        if (s.StartsWith("Alpha")) s = s.Substring(5);
        return s;
    }
}
