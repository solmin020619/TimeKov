// =====================================================================
// CreditsPanelController.cs
// 메인메뉴 "제작진" 패널. 패널의 글자들은 MainMenu 씬에 실물로 있고
// (생성용 에디터 빌더는 08-03 에 팀 합의로 제거), 이 스크립트는 두 가지를 한다.
//
//   1) 배치·색  — 씬 조각들의 위치/크기/색을 잡는다
//   2) 배경 연출 — 시계 다이얼·타임라인 같은 '글자가 아닌' 장식을 만든다
//
// ★글자는 하나도 만들지 않는다. 팀원이 씬을 훑어 번역 문구를 모으기 때문에,
//   코드가 만든 라벨은 그 수집에서 통째로 빠진다. .text 는 읽지도 쓰지도 않는다.
//
// [디자인 — 시간]
//   게임의 축이 '시간'이라 제작진 화면도 시계로 읽히게 했다.
//     · 배경에 큰 다이얼(눈금 60개 + 두 겹 링)이 아주 느리게 돈다
//     · 초침이 6°/s 로 돈다 — 실제 초침과 같은 속도라 화면이 살아 있는 느낌만 준다
//     · 명단은 가운데 세로선 + 사람마다 점 = 타임라인. 이름과 역할이 그 선을 사이에 두고 갈린다
//   전부 아주 낮은 대비라 글자 가독성을 해치지 않는다.
//
// [색을 전부 불투명으로 쓰는 이유]
//   이 프로젝트는 Linear 컬러스페이스라 '검정 위 흰색 3.5%' 같은 반투명이 의도(#0F0F10)보다
//   훨씬 밝은 회색(#343434)으로 합성된다. 예전 제작진 카드가 큼직한 회색 판으로 보였던 이유다.
//   그래서 배경 그라디언트조차 알파가 아니라 '색' 으로 굽는다(UIColors.cs 의 같은 메모 참고).
// =====================================================================

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreditsPanelController : MonoBehaviour
{
    // ── 색 (전부 불투명) ──────────────────────────────────────────────
    static readonly Color CBgCenter  = new Color(0.071f, 0.106f, 0.161f, 1f);   // #121B29
    static readonly Color CBgEdge    = new Color(0.020f, 0.031f, 0.047f, 1f);   // #05080C

    static readonly Color CRingOuter = new Color(0.078f, 0.106f, 0.149f, 1f);   // #141B26
    static readonly Color CRingInner = new Color(0.106f, 0.145f, 0.204f, 1f);   // #1B2534
    static readonly Color CTickMinor = new Color(0.114f, 0.149f, 0.204f, 1f);   // #1D2634
    static readonly Color CTickMajor = new Color(0.180f, 0.235f, 0.318f, 1f);   // #2E3C51
    static readonly Color CHand      = new Color(0.208f, 0.318f, 0.451f, 1f);   // #355173
    static readonly Color CAccent    = new Color(0.435f, 0.659f, 0.878f, 1f);   // #6FA8E0

    static readonly Color CTitle     = new Color(0.945f, 0.965f, 1f, 1f);       // #F1F6FF
    static readonly Color CStudio    = new Color(0.475f, 0.529f, 0.608f, 1f);   // #79879B
    static readonly Color CName      = new Color(0.910f, 0.925f, 0.949f, 1f);   // #E8ECF2
    static readonly Color CRole      = new Color(0.435f, 0.659f, 0.878f, 1f);
    static readonly Color CHint      = new Color(0.353f, 0.392f, 0.447f, 1f);   // #5A6472
    static readonly Color CRail      = new Color(0.137f, 0.180f, 0.243f, 1f);   // #232E3E

    // ── 치수 ──────────────────────────────────────────────────────────
    const float ContentW = 700f;
    const float RowH = 46f, RowGap = 6f;
    // ★두 단의 폭이 같아야 한다. 가로 레이아웃은 전체를 가운데 정렬하므로, 폭이 다르면
    //   두 단 사이의 빈 칸이 x=0 에서 밀린다. 그 빈 칸 한가운데로 타임라인이 지나가는데,
    //   폭이 어긋나면 선과 점이 역할 텍스트 위로 올라타 글자를 가린다(실제로 그랬다).
    const float ColW = 300f, ColGap = 56f;

    const float DialOuter = 900f, DialInner = 560f;
    const float TickRing = 418f;            // 눈금이 놓이는 반지름
    const float HandMarker = 30f;           // 눈금 위를 도는 초침 마커의 길이

    // 다이얼은 '흐르는 배경'이라 아주 느리게, 초침만 실제 속도로.
    const float SpinOuter = 1.2f, SpinInner = -2.0f, SpinHand = 6f;   // 도/초

    bool _styled;
    RectTransform _dialOuter, _dialInner, _hand;

    // ==================================================================
    //  열고 닫기 (씬의 버튼들이 이 두 개를 직접 부른다)
    // ==================================================================
    public void OpenCredits()
    {
        gameObject.SetActive(true);
        BuildOnce();                       // 배치를 먼저 잡고 연출을 태운다
        GameSfx.Play(SfxId.MenuClick);
        MenuPanelAnim.Open(gameObject);
    }

    public void CloseCredits()
    {
        if (!MenuPanelAnim.IsOpen(gameObject)) return;   // 닫는 중에 또 눌러도 한 번만
        GameSfx.Play(SfxId.MenuClick);

        // ★끄는 것은 연출이 끝난 뒤. SetActive(false) 를 먼저 하면 코루틴이 죽어
        //   연출이 한 프레임도 안 보이고 창이 툭 사라진다(MenuPanelAnim 이 처리한다).
        MenuPanelAnim.Close(this, gameObject);
    }

    void Update()
    {
        if (!MenuPanelAnim.IsOpen(gameObject)) return;   // 닫히는 중에는 입력을 받지 않는다
        if (Input.GetKeyDown(KeyCode.Escape)) { CloseCredits(); return; }

        // 메인메뉴는 timeScale 이 0 일 수 있다 — 멈춘 화면 위에서도 돌아야 한다.
        float dt = Time.unscaledDeltaTime;
        Spin(_dialOuter, SpinOuter * dt);
        Spin(_dialInner, SpinInner * dt);
        Spin(_hand, -SpinHand * dt);   // 시계 방향(화면 좌표계라 부호가 반대)
    }

    static void Spin(RectTransform rt, float deg)
    {
        if (rt != null) rt.Rotate(0f, 0f, deg);
    }

    // ==================================================================
    //  조립 — 명단 길이에서 나머지를 역산한다
    // ==================================================================
    void BuildOnce()
    {
        if (_styled) return;
        _styled = true;

        var bg = GetComponent<Image>();
        if (bg != null)
        {
            // 알파 없이 '색'으로 구운 방사형 그라디언트. 가운데가 살짝 밝아 시선이 모인다.
            bg.sprite = Sprites.RadialBg(CBgCenter, CBgEdge);
            bg.color = Color.white;
            bg.type = Image.Type.Simple;
        }

        BuildBackdrop();

        var title   = Find("Title");
        var lineL   = Find("LineL");
        var lineR   = Find("LineR");
        var diamond = Find("Diamond");
        var studio  = Find("Studio");
        var card    = Find("ListCard");
        var list    = Find("MemberList");
        var hint    = Find("EscHint");

        // 예전의 큼직한 회색 판은 없앤다. 구조는 아래 타임라인이 대신 잡아 준다.
        if (card != null) card.gameObject.SetActive(false);

        float listH = LayoutMemberList(list);

        // 위에서 아래로 쌓은 뒤 블록 전체를 화면 한가운데에 놓는다.
        const float TitleH = 72f, StudioH = 28f, HintH = 24f;
        const float GapTitle = 22f, GapDiv = 26f, GapList = 40f, GapHint = 52f;

        float total = TitleH + GapTitle + 1f + GapDiv + StudioH + GapList
                    + listH + GapHint + HintH;
        float y = total * 0.5f;

        Place(title, y - TitleH * 0.5f, 900f, TitleH);
        y -= TitleH + GapTitle;

        float divY = y - 0.5f;
        Place(lineL, divY, 150f, 1f, x: -108f);
        Place(lineR, divY, 150f, 1f, x: 108f);
        Place(diamond, divY, 9f, 9f);
        Tint(lineL, CRail); Tint(lineR, CRail); Tint(diamond, CAccent);
        if (diamond != null) diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        y -= 1f + GapDiv;

        Place(studio, y - StudioH * 0.5f, ContentW, StudioH);
        y -= StudioH + GapList;

        float listTop = y;
        if (list != null)
        {
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 0.5f);
            list.pivot = new Vector2(0.5f, 1f);      // 위에서 아래로 자란다
            list.anchoredPosition = new Vector2(0f, listTop);
        }
        BuildTimeline(listTop, listH, list != null ? list.childCount : 0);
        y -= listH + GapHint;

        Place(hint, y - HintH * 0.5f, ContentW, HintH);

        SetText(title, 56f, CTitle, FontWeight.Black, 16f);
        SetText(studio, 17f, CStudio, FontWeight.SemiBold, 10f);
        SetText(hint, 14f, CHint, FontWeight.Medium, 8f);

        StyleClose();
    }

    // ── 배경 연출 ─────────────────────────────────────────────────────
    // 전부 Image(도형)다. 글자는 없다.
    void BuildBackdrop()
    {
        var fx = NewRect("_TimeDial", (RectTransform)transform);
        Stretch(fx);
        fx.SetAsFirstSibling();          // 내용보다 뒤에 그려져야 한다
        var block = fx.gameObject.AddComponent<CanvasGroup>();
        block.blocksRaycasts = false;    // 장식이 클릭을 먹지 않게

        // 바깥 링 + 눈금 60개를 한 덩어리로 묶어 같이 돌린다.
        _dialOuter = NewRect("Outer", fx);
        Center(_dialOuter, Vector2.zero, new Vector2(DialOuter, DialOuter));
        Ring(_dialOuter, DialOuter, CRingOuter);

        for (int i = 0; i < 60; i++)
        {
            bool major = i % 5 == 0;
            float len = major ? 22f : 10f;
            float wdt = major ? 3f : 1f;
            var tick = NewImage(_dialOuter, "_tick", major ? CTickMajor : CTickMinor);

            float a = i * 6f * Mathf.Deg2Rad;
            var rt = tick.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(wdt, len);
            rt.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * TickRing;
            rt.localRotation = Quaternion.Euler(0f, 0f, -i * 6f);
        }

        // 안쪽 링은 반대로 돈다 — 두 겹이 어긋나며 도는 게 시계태엽처럼 읽힌다.
        _dialInner = NewRect("Inner", fx);
        Center(_dialInner, Vector2.zero, new Vector2(DialInner, DialInner));
        Ring(_dialInner, DialInner, CRingInner);

        // 초침 — ★중앙에서 뻗는 바늘이 아니라 다이얼 '테두리를 도는 마커'다.
        //   화면 한가운데는 제작진 명단 자리라, 중앙에 축을 두면 바늘도 축의 점도
        //   글자 위를 지나간다(실제로 축의 점이 타임라인 점들 사이에 껴 보였다).
        //   회전축만 가운데 두고, 보이는 것은 눈금 반지름에 올려 바깥을 돌게 한다.
        _hand = NewRect("Hand", fx);
        Center(_hand, Vector2.zero, new Vector2(10f, 10f));
        var marker = NewImage(_hand, "_marker", CAccent);
        Center(marker.rectTransform, new Vector2(0f, TickRing), new Vector2(3f, HandMarker));
        var trail = NewImage(_hand, "_trail", CHand);
        Center(trail.rectTransform, new Vector2(0f, TickRing), new Vector2(1f, HandMarker * 2.4f));
    }

    // 명단 가운데를 지나는 세로선 + 사람마다 점. 이름과 역할이 이 선을 사이에 두고 갈린다.
    void BuildTimeline(float listTop, float listH, int count)
    {
        if (count <= 0) return;

        var rail = NewRect("_Timeline", (RectTransform)transform);
        Center(rail, new Vector2(0f, listTop - listH * 0.5f), new Vector2(40f, listH));
        rail.SetAsFirstSibling();
        rail.SetSiblingIndex(1);   // 배경 다이얼 바로 위, 글자보다는 아래

        // 선은 첫 점에서 마지막 점까지 딱 맞게. 점들을 잇는 선으로 읽혀야 한다.
        float span = (count - 1) * (RowH + RowGap);
        var line = NewImage(rail, "_line", CRail);
        Center(line.rectTransform, Vector2.zero, new Vector2(1f, span));

        // ★모든 줄에 같은 점을 찍는다. 예전엔 첫 줄만 강조색이고 나머지는 선 색이라
        //   어두운 배경에서 거의 안 보였다 — 점이 있다 없다 하는 것처럼 보인 원인.
        for (int i = 0; i < count; i++)
        {
            float y = span * 0.5f - i * (RowH + RowGap);
            var dot = NewImage(rail, "_node", CAccent);
            dot.sprite = Sprites.Disc();
            Center(dot.rectTransform, new Vector2(0f, y), new Vector2(8f, 8f));
        }
    }

    // ── 명단 ──────────────────────────────────────────────────────────
    /// <summary>줄 규격을 잡고, 쌓인 실제 높이를 돌려준다. 사람이 늘거나 줄어도 알아서 맞는다.</summary>
    float LayoutMemberList(RectTransform list)
    {
        if (list == null) return 0f;

        var v = list.GetComponent<VerticalLayoutGroup>();
        if (v != null)
        {
            v.spacing = RowGap;
            v.padding = new RectOffset(0, 0, 0, 0);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;  v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        }
        var fit = list.GetComponent<ContentSizeFitter>();
        if (fit != null)
        {
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        list.sizeDelta = new Vector2(ColW * 2f + ColGap, list.sizeDelta.y);

        for (int i = 0; i < list.childCount; i++)
        {
            var row = list.GetChild(i) as RectTransform;
            if (row == null) continue;

            var le = Ensure<LayoutElement>(row.gameObject);
            le.minHeight = RowH; le.preferredHeight = RowH; le.flexibleHeight = 0f;

            var h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null)
            {
                h.spacing = ColGap;
                h.padding = new RectOffset(0, 0, 0, 0);
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true;  h.childControlHeight = true;
                h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            }

            Column(row, "Name", ColW, TextAlignmentOptions.Right, 24f, CName, FontWeight.Bold);
            Column(row, "Role", ColW, TextAlignmentOptions.Left, 21f, CRole, FontWeight.SemiBold);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        return list.rect.height;
    }

    void Column(RectTransform row, string name, float width, TextAlignmentOptions align,
                float size, Color color, FontWeight weight)
    {
        var t = row.Find(name) as RectTransform;
        if (t == null) return;

        var le = Ensure<LayoutElement>(t.gameObject);
        le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
        le.minHeight = RowH; le.preferredHeight = RowH;

        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.alignment = align;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontWeight = weight;
        tmp.fontStyle = weight >= FontWeight.Bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ── 닫기 버튼 — 사각 판 대신 얇은 링 ──────────────────────────────
    void StyleClose()
    {
        var close = Find("Btn_Close");
        if (close == null) return;

        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-56f, -56f);
        close.sizeDelta = new Vector2(46f, 46f);

        // 판을 링으로 바꾼다. 사각형이 사라지고 X 만 남는다.
        var img = close.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = Sprites.Ring(46f);
            img.type = Image.Type.Simple;
            img.color = CRail;
        }
        // 뒤에 깔린 테두리 판이 있으면(눌림 연출 때 드러난다) 치운다.
        var legacy = close.parent != null ? close.parent.Find("Btn_Close_Border") as RectTransform : null;
        if (legacy != null) legacy.gameObject.SetActive(false);

        var btn = close.GetComponent<Button>();
        if (btn != null)
        {
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor     = new Color(0.65f, 0.65f, 0.65f, 1f);
            cb.fadeDuration     = 0.12f;
            btn.colors = cb;
        }

        var t = close.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.fontSize = 19f;
            t.color = CStudio;
            t.fontWeight = FontWeight.Medium;
            t.alignment = TextAlignmentOptions.Center;
        }
    }

    // ==================================================================
    //  작은 도구
    // ==================================================================
    RectTransform Find(string name) => transform.Find(name) as RectTransform;

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image NewImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Ring(RectTransform parent, float size, Color color)
    {
        var img = NewImage(parent, "_ring", color);
        img.sprite = Sprites.Ring(size);
        Center(img.rectTransform, Vector2.zero, new Vector2(size, size));
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Place(RectTransform rt, float y, float w, float h, float x = 0f)
    {
        if (rt == null) return;
        Center(rt, new Vector2(x, y), new Vector2(w, h));
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Tint(RectTransform rt, Color c)
    {
        var img = rt != null ? rt.GetComponent<Image>() : null;
        if (img != null) img.color = c;
    }

    // ★.text 는 건드리지 않는다 — 씬 라벨은 팀원이 번역을 붙이는 대상이다.
    static void SetText(RectTransform rt, float size, Color color, FontWeight weight, float spacing)
    {
        var t = rt != null ? rt.GetComponent<TMP_Text>() : null;
        if (t == null) return;
        t.fontSize = size;
        t.color = color;
        t.fontWeight = weight;
        t.fontStyle = weight >= FontWeight.Bold ? FontStyles.Bold : FontStyles.Normal;
        t.characterSpacing = spacing;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // ==================================================================
    //  절차적 스프라이트 (원·링·그라디언트)
    //    코드로 만든 텍스처는 씬에 직렬화되지 않지만, 이건 '글자'가 아니라 장식이라
    //    매번 만들어도 문제가 없다. 캐시해서 패널을 여러 번 열어도 한 번만 만든다.
    // ==================================================================
    static class Sprites
    {
        static readonly Dictionary<int, Sprite> _rings = new();
        static Sprite _disc, _bg;

        /// 지름 size 로 쓸 얇은 링. 화면 크기에 맞춰 두께가 일정해 보이도록 해상도를 맞춘다.
        public static Sprite Ring(float size)
        {
            int res = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.RoundToInt(size)), 64, 512);
            if (_rings.TryGetValue(res, out var cached)) return cached;

            float outer = res * 0.5f - 1f;
            float inner = outer - Mathf.Max(1.5f, res / size * 1.5f);
            var tex = New(res);
            var px = new Color32[res * res];
            float c = res * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            _rings[res] = sp;
            return sp;
        }

        public static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int res = 64;
            var tex = New(res);
            var px = new Color32[res * res];
            float c = res * 0.5f, r = c - 1f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            _disc = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            return _disc;
        }

        /// 가운데가 밝고 가장자리가 어두운 배경. ★알파가 아니라 '색' 으로 굽는다 —
        /// Linear 컬러스페이스에서 반투명은 의도보다 훨씬 밝게 합성되기 때문.
        public static Sprite RadialBg(Color center, Color edge)
        {
            if (_bg != null) return _bg;
            const int res = 128;
            var tex = New(res);
            var px = new Color32[res * res];
            float c = res * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float k = Mathf.Clamp01(d * 1.15f);
                    k = k * k;                                   // 가운데 밝은 영역을 넓게
                    px[y * res + x] = Color.Lerp(center, edge, k);
                }
            tex.SetPixels32(px); tex.Apply();
            _bg = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            return _bg;
        }

        static Texture2D New(int res)
        {
            var t = new Texture2D(res, res, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }
    }
}
