// =====================================================================
// PlayerStatPanelStyle.cs
// 캐릭터 스탯창(C키) 외형. Character_stat 오브젝트에 붙인다.
//
// [기존 창을 지우고 새로 짓는다]
//   프리팹에 굳어 있던 360x270 세로 박스의 장식을 전부 지우고 가로형(640x200)으로 다시
//   만든다. 남은 조각을 재활용하면 쓰지 않는 오브젝트가 숨겨진 채 따라다녀서, 나중에
//   치수를 바꿀 때마다 유령 조각을 찾아다니게 된다.
//
//   ★단, 지우면 안 되는 것이 셋 있다:
//     1) TMP 글자 — STATUS / MAX TIME / STAMINA / ATK / DEF.
//        팀원이 씬을 훑어 번역 문구를 모으므로 실행 중에 만든 글자는 수집에서 빠진다.
//        게다가 PlayerStatHUD 가 인스펙터로 이 텍스트들을 물고 있어 지우면 참조가 끊긴다.
//     2) Slider — 게이지. 역시 PlayerStatHUD 가 참조하고, Fill 앵커는 Slider 가 값에
//        따라 매 프레임 정하므로 여기서는 스프라이트와 색만 갈아끼운다.
//     3) 스프라이트가 들어간 Image — 실제로 그린 아이콘이다(칼·방패 등).
//   즉 '색만 칠한 맨 사각형'만 지운다. 판단은 Salvage() 한 곳에 모여 있다.
//
// [배경을 여러 겹으로 쌓는다]
//   방사형 그라디언트 → 격자 → 큰 다이얼(동심원 3 + 12방향 눈금) → 주사선 순으로 깔고,
//   그 위의 섹션 박스를 살짝 비치게 둔다. 박스가 완전히 불투명하면 배경 무늬가 14px 여백
//   에서만 보여서 꾸미는 의미가 없다.
//   ★여기서 알파를 쓰는 건 괜찮다. Linear 색공간에서 문제가 되는 건 '밝은 색'을 반투명으로
//     올릴 때고(의도보다 훨씬 밝게 합성된다), 이건 어두운 남색을 어두운 배경에 올리는 것이라
//     예측대로 나온다. 반대로 게이지 광택처럼 밝은 색은 미리 섞어 둔 불투명 색을 쓴다.
//
// [테두리는 Outline 대신 '한 겹 큰 판'을 뒤에 깐다]
//   UI 의 Outline 컴포넌트는 그래픽을 복제해 사방으로 밀어 그린다. 반투명 박스에 쓰면 그
//   복제본이 채움 너머로 비쳐 가장자리가 지저분해진다. 그래서 2px 큰 판을 뒤에 깔아
//   1px 테두리를 만든다(프리팹이 원래 쓰던 방식과 같다).
//
// [값은 건드리지 않는다]
//   숫자 갱신은 전부 PlayerStatHUD 담당이다. 여기서는 글자의 위치·크기·색만 정한다.
//
// [좌표계]
//   상수는 전부 '왼쪽 위 원점'(시안과 같은 기준)으로 적고, 배치할 때 PanelH 에서 빼서
//   유니티의 좌하단 기준으로 바꾼다. 시안과 숫자를 1:1 로 대조할 수 있게 하려는 것이다.
// =====================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class PlayerStatPanelStyle : MonoBehaviour
{
    // ── 치수 ────────────────────────────────────────────────────────
    public const float PanelW = 640f, PanelH = 200f;

    const float Pad = 14f;          // 좌우 여백 — 양쪽 같은 값이어야 한다
    const float HeaderH = 36f;

    const float LeftX = Pad, LeftY = 46f, LeftW = 392f, LeftH = 140f;
    const float GaugeX = 26f, GaugeW = 366f, BarH = 10f;
    const float PlateSize = 22f;

    const float TimeRowY = 60f, TimeBarY = 88f, TimeRulerY = 101f;
    const float StamRowY = 126f, StamBarY = 154f, StamRulerY = 167f;

    const float RightX = 418f, RightW = 208f, RightH = 66f;
    const float AtkY = 46f, DefY = 120f;
    const float IconD = 32f;

    const float DialCX = 320f, DialCY = 104f;   // 큰 다이얼 중심(왼쪽 위 원점 기준)

    [Header("화면 배치")]
    [Tooltip("체크: 코드가 화면 왼쪽 아래에 자동으로 놓는다(아래 margin 사용).\n" +
             "해제: 위치를 건드리지 않는다 — 인스펙터에서 손으로 옮겨 쓰고 싶을 때.\n" +
             "★해제해도 크기(640x200)는 코드가 정한다. 안쪽 배치가 그 크기를 전제로 하기 때문.")]
    [SerializeField] bool autoPlace = true;

    [Tooltip("왼쪽·아래 모두 이 값을 쓴다. 한 값으로 묶어야 구석에서 여백이 어긋나지 않는다.")]
    [SerializeField] float margin = 20f;

    [Header("연출")]
    [Tooltip("ATK/DEF 아이콘의 끊긴 링이 시계 방향으로 한 바퀴 도는 데 걸리는 시간(초).\n" +
             "0 이면 돌지 않는다. 짧을수록 빠르다 — 너무 빠르면 시선을 뺏는다.")]
    [SerializeField] float ringSpinSeconds = 9f;

    [Header("밑변 맞추기 (선택)")]
    [Tooltip("체력(시간) 게이지의 밑변에 맞춘다.\n" +
             "★켜면 아래 여백이 그 게이지 높이만큼(약 64px) 벌어진다 — 화면 구석에 붙이려면 꺼 둘 것.")]
    [SerializeField] bool alignBottomToTimeBar = false;
    [Tooltip("기준으로 삼을 게이지의 오브젝트 이름. 같은 부모(PlayerHud) 안에서 찾는다.")]
    [SerializeField] string timeBarName = "PlayerTime";

    // ── 색 ──────────────────────────────────────────────────────────
    static readonly Color BgInner = C("#12253A"), BgMid = C("#0A1524"), BgOuter = C("#050A12");
    static readonly Color GridCol = C("#1D3E5C");
    static readonly Color DialCol = C("#2C5679"), DialTick = C("#37678F");
    static readonly Color ScanCol = C("#1F3A56");

    static readonly Color BandCol = new Color32(0x08, 0x12, 0x21, 0xDC);   // 헤더 띠(살짝 비침)
    static readonly Color HeadRule = C("#2C4E6E"), HeadLit = C("#3E9BC4");
    static readonly Color Accent = C("#4FC0EA");

    static readonly Color BoxFill = new Color32(0x0B, 0x17, 0x27, 0x8C);   // 섹션 박스(배경이 비침)
    static readonly Color BoxEdge = C("#22384F"), BoxGloss = C("#1E3450");

    static readonly Color Track = C("#050C15"), TrackEdge = C("#1C3247"), Ruler = C("#3A6288");
    static readonly Color TimeDeep = C("#1F6B96"), TimeLit = C("#78D6F5"), TimeTip = C("#DFF6FF");
    static readonly Color StamDeep = C("#966E2C"), StamLit = C("#F5D89B"), StamTip = C("#FFF0CC");
    static readonly Color PlateTime = C("#132B40"), PlateTimeEdge = C("#2E5A78");
    static readonly Color PlateStam = C("#2A2313"), PlateStamEdge = C("#6B5626");

    static readonly Color Edge = C("#2A4562"), BracketCol = C("#9FC6E8");
    static readonly Color TextMain = C("#EAF4FF"), TextLabel = C("#8FB6D6");

    RectTransform _root;
    readonly List<Transform> _order = new();          // 장식 위로 올릴 순서
    readonly List<RectTransform> _spin = new();       // 계속 돌아가는 링

    /// <summary>이번에 장식으로 쓴 루트 자식의 이름.
    ///
    /// ★Salvage 는 '스프라이트가 붙은 Image' 를 아트로 보고 살려 둔다. 그런데 그 이름이
    ///   장식 이름과 겹치면(예: 프리팹의 StatBG 에 배경 이미지를 끼운 경우) New() 가 그
    ///   살려둔 오브젝트를 그대로 재활용한다 — 즉 '못 살린 조각'이 아니라 지금 쓰고 있는
    ///   배경이다. 이 목록이 없으면 아래 정리 루프가 그걸 꺼 버려서 배경이 통째로 사라지고,
    ///   덤으로 튜토리얼 스포트라이트 타깃(StatBG)까지 같이 죽는다.</summary>
    readonly HashSet<string> _built = new();

    bool _done;

    void Awake() => Build();

    void Build()
    {
        if (_done) return;
        _done = true;

        _root = (RectTransform)transform;
        PlaceOnScreen();

        // 큰 다이얼이 패널 밖으로 삐져나가지 않게 잘라 낸다.
        //   ★이게 없으면 배경 원이 화면 한복판까지 그려진다.
        if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();

        var keep = Salvage();

        BuildBackground();
        BuildHeader(keep);
        BuildGaugeSection(keep);
        BuildStatBox(keep, "ATK", AtkY, Accent, PlateTime, PlateTimeEdge, drawSword: true);
        BuildStatBox(keep, "DEF", DefY, StamLit, PlateStam, PlateStamEdge);
        BuildFrame();

        foreach (var t in _order) if (t != null) t.SetAsLastSibling();

        // 살렸는데 새 배치에서 자리를 안 준 것 — 안 끄면 루트 원점(왼쪽 아래 구석)에
        // 그대로 떠 버린다. 이름이 로그에 찍히니 필요한 것이면 배치를 추가하면 된다.
        //   ★_built 에 든 것은 예외다. 살아남은 조각을 장식으로 재활용한 경우라, 끄면
        //     방금 만든 장식을 끄는 셈이 된다(_built 주석 참고).
        foreach (var kv in keep)
        {
            if (kv.Value == null || _order.Contains(kv.Value) || _built.Contains(kv.Key)) continue;
            kv.Value.gameObject.SetActive(false);
            Debug.LogWarning($"[스탯창] '{kv.Key}' 은(는) 새 배치에 자리가 없어 껐습니다.", this);
        }
    }

    // 해상도·창 크기가 바뀌면 화면 좌하단의 위치도 달라진다. 열 때마다 다시 잡는다.
    void OnEnable() { if (_done) PlaceOnScreen(); }

    /// <summary>ATK/DEF 아이콘의 끊긴 링을 계속 시계 방향으로 돌린다.
    ///   ★unscaledDeltaTime 을 쓴다. 나중에 이 창이 시간이 멈춘 화면 위에서 열려도
    ///     연출이 얼어붙지 않게(프로젝트의 다른 UI 연출과 같은 기준).
    ///   ★유니티의 +Z 회전은 반시계 방향이라, 시계 방향으로 돌리려면 각도를 빼야 한다.
    ///   창이 닫히면 이 컴포넌트도 꺼지므로 안 보일 때는 아무것도 돌지 않는다.</summary>
    void Update()
    {
        if (_spin.Count == 0 || ringSpinSeconds <= 0f) return;

        float step = 360f / ringSpinSeconds * Time.unscaledDeltaTime;
        for (int i = 0; i < _spin.Count; i++)
        {
            var rt = _spin[i];
            if (rt == null) continue;
            rt.localRotation *= Quaternion.Euler(0f, 0f, -step);
        }
    }

    // ── 화면 배치 ───────────────────────────────────────────────────
    /// <summary>패널을 화면 왼쪽 아래 구석에 놓는다.
    ///
    /// ★앵커만 (0,0) 으로 두면 안 된다. 앵커는 '부모 기준'인데 부모(PlayerHud)가 화면 한가운데
    ///   놓인 100x100 짜리 점 노드라, 그 좌하단은 화면 좌하단이 아니라 화면 중앙이다.
    ///   (실제로 이것 때문에 창이 화면 중앙 오른쪽에 떴다)
    ///   그래서 캔버스의 좌하단 모서리를 부모 로컬 좌표로 변환해 직접 놓는다.</summary>
    void PlaceOnScreen()
    {
        var parent = _root.parent as RectTransform;
        var canvas = GetComponentInParent<Canvas>(true);   // 자기 자신이 비활성일 수 있다 → true
        if (parent == null || canvas == null) return;

        var crt = canvas.rootCanvas.transform as RectTransform;
        if (crt == null || crt.rect.width <= 1f) return;   // 아직 레이아웃 전

        // 크기는 자동 배치를 꺼도 코드가 정한다 — 안쪽 배치가 640x200 을 전제로 계산되기 때문.
        _root.sizeDelta = new Vector2(PanelW, PanelH);
        if (!autoPlace) return;   // 위치는 손으로 — 앵커/피벗도 건드리지 않는다

        // 피벗도 좌하단이라 StatPanelRevealEffect 의 '코너에서 자라나는' 연출이 그대로 맞는다.
        _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;

        Vector3 world = crt.TransformPoint(new Vector3(crt.rect.xMin, crt.rect.yMin, 0f));
        Vector2 local = parent.InverseTransformPoint(world);

        // 앵커가 (0,0) 일 때 기준점은 부모 rect 의 좌하단이므로 그만큼 빼 준다.
        _root.anchoredPosition = local - parent.rect.min + new Vector2(margin, BottomMargin(crt, parent));

        // ★위치를 바꾼 뒤라 열림 위치를 다시 잡아 줘야 한다.
        //   안 부르면 C 로 열 때마다 프리팹에 굳어 있던 옛 자리로 되돌아간다.
        var reveal = GetComponent<StatPanelRevealEffect>();
        if (reveal != null) reveal.RecaptureShownPos();
    }

    /// <summary>화면 아래에서 띄울 거리. 체력(시간) 게이지가 있으면 그 밑변에 맞춘다 —
    /// 두 HUD 의 바닥선이 어긋나 있으면 정렬이 안 된 것처럼 보인다.</summary>
    float BottomMargin(RectTransform canvasRt, RectTransform parent)
    {
        if (!alignBottomToTimeBar || string.IsNullOrEmpty(timeBarName)) return margin;

        var bar = parent.Find(timeBarName) as RectTransform;
        if (bar == null) return margin;

        Vector3 world = bar.TransformPoint(new Vector3(0f, bar.rect.yMin, 0f));
        float y = canvasRt.InverseTransformPoint(world).y - canvasRt.rect.yMin;
        return y > 0f ? y : margin;   // 화면 밖이면 신뢰하지 않는다
    }

    // ── 기존 창 정리 ────────────────────────────────────────────────
    /// <summary>살릴 것만 뽑아 루트 바로 아래로 옮기고 나머지는 지운다.</summary>
    Dictionary<string, Transform> Salvage()
    {
        var keep = new Dictionary<string, Transform>();
        var keepSet = new HashSet<Transform>();

        // 1) 글자 — 전부 살린다(번역 수집 대상 + PlayerStatHUD 참조).
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            Mark(t.transform, keep, keepSet);

        // 2) 게이지 — Slider 는 자기 자식(Background/Fill)이 있어야 동작하므로 통째로 살린다.
        foreach (var s in GetComponentsInChildren<Slider>(true))
        {
            Mark(s.transform, keep, keepSet);
            foreach (var c in s.GetComponentsInChildren<Transform>(true)) keepSet.Add(c);
        }

        // 3) 실제로 그린 아이콘 — 스프라이트가 붙은 Image 는 아트라 지우지 않는다.
        foreach (var img in GetComponentsInChildren<Image>(true))
            if (img.sprite != null && img.GetComponent<Slider>() == null && !keepSet.Contains(img.transform))
                Mark(img.transform, keep, keepSet);

        foreach (var t in keep.Values)
            if (t != null && t.parent != _root) t.SetParent(_root, false);

        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            var c = _root.GetChild(i);
            if (keepSet.Contains(c)) continue;
            c.gameObject.SetActive(false);
            // ★부모에서 먼저 떼어낸다. Destroy 는 프레임 끝에 처리돼서, 그 전까지는
            //   Transform.Find 가 죽을 예정인 오브젝트를 그대로 돌려준다 —
            //   같은 이름으로 새로 만들 때 그 시체를 재활용해 버린다.
            c.SetParent(null);
            Destroy(c.gameObject);
        }

        return keep;
    }

    static void Mark(Transform t, Dictionary<string, Transform> keep, HashSet<Transform> set)
    {
        set.Add(t);
        if (!keep.ContainsKey(t.name)) keep[t.name] = t;
    }

    // ── 배경 (아래에서 위로 네 겹) ──────────────────────────────────
    void BuildBackground()
    {
        // ★이름을 StatBG 로 유지해야 한다. GameUIController 가 튜토리얼 스포트라이트
        //   타깃("status_panel")을 statPanel.Find("StatBG") 로 잡는다.
        var bg = New("StatBG");
        Stretch(bg.rectTransform);
        bg.sprite = TimeUiSprites.RadialGradient(BgInner, BgMid, BgOuter, 0.7f, new Vector2(0.3f, 0.6f));
        bg.color = Color.white;   // 스프라이트에 색이 구워져 있다

        // ★스포트라이트 타깃을 여기서 '다시' 등록한다.
        //   GameUIController.Awake 가 Find("StatBG") 로 먼저 등록하지만, 그건 옛 배경이다 —
        //   이 창은 꺼진 채로 시작해서 아래 Build 가 C 를 처음 누를 때야 돌고, 그때
        //   Salvage() 가 옛 배경을 지운다. 다시 등록하지 않으면 죽은 참조만 남아
        //   튜토리얼이 구멍 없이 화면 전체를 덮는다.
        //   등록 목록은 List 이고 쓸 때 null 을 걸러내므로, 죽은 항목은 저절로 무시된다.
        TutorialOverlay.RegisterTarget("status_panel", bg.rectTransform);

        var grid = NewRaw("_Grid");
        grid.texture = Repeat("grid", 32, GridLine);
        grid.color = GridCol;
        grid.uvRect = new Rect(0f, 0f, PanelW / 32f, PanelH / 32f);
        Stretch(grid.rectTransform);

        // 큰 시계 다이얼 — 패널 밖으로 잘려서 '더 큰 장치의 일부'처럼 보인다.
        Ring("_Dial0", DialCX, DialCY, 176f, 1f, DialCol);
        Ring("_Dial1", DialCX, DialCY, 244f, 1f, DialCol);
        Ring("_Dial2", DialCX, DialCY, 300f, 1f, DialCol);

        for (int i = 0; i < 12; i++)
        {
            float ang = i * 30f;
            bool major = (i % 3) == 0;
            float r = 150f - (major ? 5f : 3.5f);
            float rad = ang * Mathf.Deg2Rad;
            float cx = DialCX + Mathf.Sin(rad) * r;
            float cy = DialCY - Mathf.Cos(rad) * r;   // 위쪽이 0도

            var t = New($"_DialTick{i}");
            t.color = DialTick;
            PlaceRotated(t.rectTransform, cx, PanelH - cy, 2f, major ? 10f : 7f, -ang);
        }

        var scan = NewRaw("_Scanlines");
        scan.texture = Repeat("scan", 4, ScanLine);
        scan.color = ScanCol;
        scan.uvRect = new Rect(0f, 0f, 1f, PanelH / 4f);
        Stretch(scan.rectTransform);
    }

    // ── 헤더 ────────────────────────────────────────────────────────
    void BuildHeader(Dictionary<string, Transform> keep)
    {
        var band = New("_HeaderBand");
        band.color = BandCol;
        Place(band.rectTransform, 0f, PanelH - HeaderH, PanelW, HeaderH);

        var rule = New("_HeaderRule");
        rule.color = HeadRule;
        Place(rule.rectTransform, 0f, PanelH - HeaderH, PanelW, 1f);

        var lit = New("_HeaderLit");
        lit.color = HeadLit;
        Place(lit.rectTransform, Pad, PanelH - HeaderH, 150f, 1f);

        // STATUS 글자와 오른쪽 링 사이의 빈 구간 — 계측 눈금 띠로 채운다.
        //   ★사각형을 20여 개 만드는 대신 반복 텍스처 한 장을 늘여 쓴다.
        //     오브젝트 수가 눈에 띄게 줄고, 간격을 바꿀 때 숫자 하나만 고치면 된다.
        //   ★폭을 간격의 '정수 배'로 잡아야 한다. 남는 자투리가 있으면 마지막 눈금 간격만
        //     좁아져서 눈에 띈다.
        const float TickX = 132f, TickStep = 14f;
        const int TickCount = 32, MajorEvery = 8;
        const float TickSpan = TickCount * TickStep;

        var ticks = NewRaw("_HdrTicks");
        ticks.texture = Repeat("hdrtick", 16, HeaderTick);
        ticks.color = HeadRule;
        ticks.uvRect = new Rect(0f, 0f, TickCount, 0.5f);
        Place(ticks.rectTransform, TickX, PanelH - 22f, TickSpan, 8f);

        // 리듬을 주는 긴 눈금. 균등한 점선만 있으면 눈이 미끄러진다.
        //   ★위치를 '비율'(1/4, 2/4 …)로 잡으면 안 된다. 작은 눈금 격자에서 몇 픽셀씩 어긋나
        //     간격이 틀어져 보인다. 반드시 간격의 배수로 놓아야 격자에 딱 얹힌다.
        for (int i = MajorEvery; i < TickCount; i += MajorEvery)
        {
            var m = New($"_HdrTickMajor{i}");
            m.color = HeadLit;
            Place(m.rectTransform, TickX + i * TickStep, PanelH - 25f, 1f, 11f);
        }

        Ring("_DialSmall", 600f, 18f, 6f, 1f, TextLabel);
        Ring("_DialBig", 617f, 18f, 12f, 1f, Accent);
        var dot = New("_DialBigDot");
        dot.sprite = TimeUiSprites.Disc();
        dot.color = Accent;
        Place(dot.rectTransform, 615f, PanelH - 20f, 4f, 4f);

        var title = Text(keep, "Title");
        if (title == null) return;
        Place(title.rectTransform, 20f, PanelH - 30f, 160f, 20f);
        title.alignment = TextAlignmentOptions.Left;
        title.fontSize = 12f;
        title.characterSpacing = 22f;
        title.color = TextMain;
    }

    // ── 게이지 섹션 ─────────────────────────────────────────────────
    void BuildGaugeSection(Dictionary<string, Transform> keep)
    {
        Panel("_LeftBox", LeftX, LeftY, LeftW, LeftH);

        // 세로 액센트 — 섹션 박스의 왼쪽 모서리에 얹는다.
        //   ★패널 맨 끝(x=0)에 두면 안 된다. 왼쪽만 여백이 0 처럼 보여서 오른쪽 여백만
        //     남아 보인다(좌우가 안 맞아 보이던 원인). 박스와 같은 14 에서 시작해야
        //     양쪽 여백이 실제로도, 눈으로도 같아진다.
        var stripe = New("_Accent");
        stripe.color = Accent;
        Place(stripe.rectTransform, LeftX, PanelH - LeftY - LeftH, 3f, LeftH);

        Gauge(keep, "MaxTime", TimeRowY, TimeBarY, TimeRulerY,
              TimeDeep, TimeLit, TimeTip, PlateTime, PlateTimeEdge);
        Gauge(keep, "Stamina", StamRowY, StamBarY, StamRulerY,
              StamDeep, StamLit, StamTip, PlateStam, PlateStamEdge);
    }

    void Gauge(Dictionary<string, Transform> keep, string prefix,
               float rowY, float barY, float rulerY,
               Color deep, Color lit, Color tip, Color plate, Color plateEdge)
    {
        // 아이콘 받침 — 아이콘이 허공에 뜨지 않게.
        Panel($"_{prefix}Plate", GaugeX, rowY, PlateSize, PlateSize, plate, plateEdge);

        var icon = Keep(keep, prefix + "Icon");
        if (icon != null)
        {
            var im = icon.GetComponent<Image>();
            if (im != null) im.color = lit;
            Place((RectTransform)icon, GaugeX + 4f, PanelH - rowY - 18f, 14f, 14f);
            _order.Add(icon);
        }

        var label = Text(keep, prefix + "Label");
        if (label != null)
        {
            Place(label.rectTransform, 58f, PanelH - rowY - 20f, 200f, 18f);
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 11f; label.characterSpacing = 18f; label.color = TextLabel;
        }

        var value = Text(keep, prefix + "Value");
        if (value != null)
        {
            Place(value.rectTransform, GaugeX, PanelH - rowY - 22f, GaugeW, 22f);
            value.alignment = TextAlignmentOptions.Right;
            value.fontSize = 17f; value.characterSpacing = 0f; value.color = TextMain;
        }

        var bar = Keep(keep, prefix + "Bar") as RectTransform;
        if (bar != null)
        {
            Place(bar, GaugeX, PanelH - barY - BarH, GaugeW, BarH);
            _order.Add(bar);

            var track = ChildImg(bar, "Background");
            if (track != null)
            {
                track.sprite = TimeUiSprites.Capsule(BarH);
                track.type = Image.Type.Sliced;
                track.color = Track;
            }
            // 트랙 테두리 — 파인 느낌.
            var tEdge = Child(bar, "_TrackEdge");
            tEdge.sprite = TimeUiSprites.Capsule(BarH + 2f);
            tEdge.type = Image.Type.Sliced;
            tEdge.color = TrackEdge;
            var ert = tEdge.rectTransform;
            ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
            ert.offsetMin = new Vector2(-1f, -1f); ert.offsetMax = new Vector2(1f, 1f);
            tEdge.transform.SetAsFirstSibling();

            // ★Fill 의 앵커는 Slider 가 값에 따라 매 프레임 정한다 — 절대 건드리지 않는다.
            var fill = ChildImg(bar, "Fill");
            if (fill != null)
            {
                fill.sprite = TimeUiSprites.Capsule(BarH - 2f);
                fill.type = Image.Type.Sliced;
                fill.color = deep;

                // 광택 — 위쪽 절반에 밝은 캡슐. 납작한 막대가 원통처럼 보인다.
                //   ★알파를 쓰지 않는다. Linear 에서 반투명 흰색은 의도보다 훨씬 밝게
                //     합성돼서 게이지만 허옇게 뜬다. 그래서 밝은 불투명 색을 직접 쓴다.
                var sheen = Child(fill.rectTransform, "_Sheen");
                sheen.sprite = TimeUiSprites.Capsule(5f);
                sheen.type = Image.Type.Sliced;
                sheen.color = lit;
                var srt = sheen.rectTransform;
                srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = Vector2.one;
                srt.pivot = new Vector2(.5f, 1f);
                srt.offsetMin = new Vector2(2f, -6.5f);   // 둥근 끝을 넘지 않게 좌우로 들인다
                srt.offsetMax = new Vector2(-2f, -1.5f);

                // 진행 끝 발광 점 — Fill 오른쪽 끝에 앵커로 붙이면 값이 변할 때 저절로 따라간다.
                var d = Child(fill.rectTransform, "_Tip");
                d.sprite = TimeUiSprites.Disc();
                d.color = tip;
                var drt = d.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(1f, .5f);
                drt.pivot = new Vector2(.5f, .5f);
                drt.anchoredPosition = Vector2.zero;
                drt.sizeDelta = new Vector2(BarH - 2f, BarH - 2f);
            }
        }

        // 눈금자 — 게이지를 토막 내지 않으면서 눈금을 준다(예전엔 막대 안을 파냈다).
        //   ★길이를 전부 같게 둔다. 몇 개만 길게 하면 시간·스태미나 두 줄에서 긴 눈금 자리가
        //     서로 어긋나 보여 '눈금 길이가 다 다르다'로 읽힌다.
        //   ★간격도 정수로 잡는다. 소수 간격은 픽셀 경계에 반쯤 걸쳐 눈금 굵기가 칸마다 달라진다.
        //   ★줄 전체를 트랙 가운데에 맞춘다. 예전 값(72 시작 · 45.8 간격)은 오른쪽 여백만
        //     2px 좁아서 눈금자가 왼쪽으로 밀린 것처럼 보였다.
        const int RulerCount = 7;
        const float RulerStep = 46f, RulerLen = 4f;
        const float RulerX = GaugeX + (GaugeW - (RulerCount - 1) * RulerStep) * .5f;   // 71

        for (int i = 0; i < RulerCount; i++)
        {
            var t = New($"_{prefix}Ruler{i}");
            t.color = Ruler;
            Place(t.rectTransform, RulerX + i * RulerStep, PanelH - rulerY - RulerLen, 1f, RulerLen);
        }
    }

    // ── ATK / DEF ───────────────────────────────────────────────────
    void BuildStatBox(Dictionary<string, Transform> keep, string prefix, float boxY,
                      Color ringCol, Color plate, Color plateEdge, bool drawSword = false)
    {
        Panel($"_{prefix}Box", RightX, boxY, RightW, RightH);

        float icx = RightX + 34f, icy = boxY + RightH * .5f;

        var disc = New($"_{prefix}Plate");
        disc.sprite = TimeUiSprites.Disc();
        disc.color = plate;
        Place(disc.rectTransform, icx - IconD * .5f, PanelH - icy - IconD * .5f, IconD, IconD);

        var pe = New($"_{prefix}PlateEdge");
        pe.sprite = TimeUiSprites.Ring(IconD, 1f);
        pe.color = plateEdge;
        Place(pe.rectTransform, icx - IconD * .5f, PanelH - icy - IconD * .5f, IconD, IconD);

        // 끊긴 링 — 시계 눈금 느낌. 이것 때문에 RingDashed 를 추가했다.
        //   ★Place 가 아니라 PlaceRotated 를 쓴다. Place 는 피벗을 좌하단에 두는데,
        //     그 상태로 돌리면 제자리에서 도는 게 아니라 모서리를 축으로 크게 휘돌아 버린다.
        //     회전축이 링 한가운데여야 하므로 피벗을 가운데로 두는 쪽을 써야 한다.
        var ring = New($"_{prefix}Ring");
        ring.sprite = TimeUiSprites.RingDashed(IconD, 2f, 4, .66f);
        ring.color = ringCol;
        PlaceRotated(ring.rectTransform, icx, PanelH - icy, IconD, IconD, 0f);
        _spin.Add(ring.rectTransform);

        var icon = Keep(keep, prefix + "Icon");
        if (icon != null)
        {
            _order.Add(icon);   // 자동 정리에 안 걸리게 등록만 해 둔다
            if (drawSword)
            {
                // 씬에 박혀 있던 아이콘은 끄고 직접 그린다.
                icon.gameObject.SetActive(false);
            }
            else
            {
                var im = icon.GetComponent<Image>();
                if (im != null) im.color = TextMain;
                Place((RectTransform)icon, icx - 8f, PanelH - icy - 8f, 16f, 16f);
            }
        }

        if (drawSword) Sword(prefix, icx, PanelH - icy, TextMain);

        var label = Text(keep, prefix + "Label");
        if (label != null)
        {
            Place(label.rectTransform, 482f, PanelH - boxY - 32f, 100f, 16f);
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 10f; label.characterSpacing = 16f; label.color = TextLabel;
        }

        var value = Text(keep, prefix + "Value");
        if (value != null)
        {
            Place(value.rectTransform, 482f, PanelH - boxY - 58f, RightW - 74f, 26f);
            value.alignment = TextAlignmentOptions.Left;
            value.fontSize = 24f; value.color = TextMain;
        }
    }

    // ── 바깥 테두리 · 코너 브래킷 ───────────────────────────────────
    void BuildFrame()
    {
        // 테두리는 1px 사각형 네 개로 그린다.
        //   ★Outline 컴포넌트를 쓰면 안 된다. Outline 은 '그래픽을 복제해 사방으로 밀어' 그리는
        //     것이라, 속이 빈 프레임 스프라이트가 아니라 그냥 투명한 사각형에 붙이면 복제본이
        //     꽉 찬 전체 크기 사각형이 되어 패널을 통째로 덮어 버린다.
        //     (실제로 이것 때문에 창이 단색 판으로 나왔다. 뒤에 깔려 있을 땐 배경처럼
        //      보여서 '왜 시안보다 밝지?' 로만 보였던 것이 같은 원인이다)
        Line("_Border_T", 0f, 0f, PanelW, 1f);
        Line("_Border_B", 0f, PanelH - 1f, PanelW, 1f);
        Line("_Border_L", 0f, 0f, 1f, PanelH);
        Line("_Border_R", PanelW - 1f, 0f, 1f, PanelH);

        const float Arm = 14f, Th = 1.6f, Inset = 5f;
        for (int i = 0; i < 4; i++)
        {
            bool right = (i == 1 || i == 2);
            bool top = (i == 0 || i == 1);

            var h = New($"_Bracket{i}_H");
            h.color = BracketCol;
            Place(h.rectTransform, right ? PanelW - Inset - Arm : Inset,
                                   top ? PanelH - Inset - Th : Inset, Arm, Th);

            var v = New($"_Bracket{i}_V");
            v.color = BracketCol;
            Place(v.rectTransform, right ? PanelW - Inset - Th : Inset,
                                   top ? PanelH - Inset - Arm : Inset, Th, Arm);
        }
    }

    // ── 조각 만들기 ─────────────────────────────────────────────────
    /// <summary>둥근 섹션 판 + 1px 테두리 + 윗변 하이라이트.
    /// 테두리는 2px 큰 판을 뒤에 깔아 만든다 — Outline 컴포넌트를 반투명 판에 쓰면
    /// 복제본이 채움 너머로 비쳐 가장자리가 지저분해진다.</summary>
    void Panel(string name, float x, float y, float w, float h)
        => Panel(name, x, y, w, h, BoxFill, BoxEdge, gloss: true);

    void Panel(string name, float x, float y, float w, float h, Color fill, Color edge)
        => Panel(name, x, y, w, h, fill, edge, gloss: false);

    void Panel(string name, float x, float y, float w, float h, Color fill, Color edge, bool gloss)
    {
        var e = New(name + "Edge");
        e.sprite = TimeUiSprites.Capsule(14f);
        e.type = Image.Type.Sliced;
        e.color = edge;
        Place(e.rectTransform, x - 1f, PanelH - y - h - 1f, w + 2f, h + 2f);

        var f = New(name);
        f.sprite = TimeUiSprites.Capsule(12f);
        f.type = Image.Type.Sliced;
        f.color = fill;
        Place(f.rectTransform, x, PanelH - y - h, w, h);

        if (!gloss) return;

        // 윗변 하이라이트 — 위에서 빛을 받는 것처럼 보여 판이 평평해 보이지 않는다.
        var g = New(name + "Gloss");
        g.sprite = TimeUiSprites.Capsule(2f);
        g.type = Image.Type.Sliced;
        g.color = BoxGloss;
        Place(g.rectTransform, x + 10f, PanelH - y - 3f, w - 20f, 2f);
    }

    /// <summary>테두리용 1px 선. 좌표는 '왼쪽 위 원점' 기준으로 넘긴다.</summary>
    void Line(string name, float x, float yTop, float w, float h)
    {
        var img = New(name);
        img.color = Edge;
        Place(img.rectTransform, x, PanelH - yTop - h, w, h);
    }

    /// <summary>검 아이콘. 사각형 네 개로 조립한다 — 프로젝트에 UI 용 검 스프라이트가 없다
    /// (sword 로 잡히는 건 전부 3D 모델 텍스처라 UI 에 못 쓴다).
    /// 45도로 세운 날 + 직각으로 가로지르는 코등이 + 손잡이 + 폼멜.
    /// 좌표는 유니티 기준(좌하단 원점)으로 받는다.</summary>
    void Sword(string prefix, float cx, float cy, Color col)
    {
        const float Ang = -45f;      // 세로 막대를 오른쪽 위로 눕히는 각도
        const float S = 0.7071f;     // sin45 = cos45 — 날 방향으로 얼마나 옮길지 계산용

        // 날 방향으로 t 만큼 떨어진 지점
        void At(string n, float t, float w, float h, float angle)
        {
            var img = New($"_{prefix}{n}");
            img.color = col;
            PlaceRotated(img.rectTransform, cx + t * S, cy + t * S, w, h, angle);
        }

        At("Blade", 2.5f, 2.6f, 14f, Ang);     // 날
        At("Guard", -3f, 10f, 2.2f, Ang);      // 코등이(가로 막대라 같은 각도면 날과 직각이 된다)
        At("Grip", -5.5f, 2.2f, 5f, Ang);      // 손잡이

        var pommel = New($"_{prefix}Pommel");
        pommel.sprite = TimeUiSprites.Disc();
        pommel.color = col;
        PlaceRotated(pommel.rectTransform, cx - 8f * S, cy - 8f * S, 3.4f, 3.4f, 0f);
    }

    void Ring(string name, float cx, float cy, float d, float thick, Color col)
    {
        var img = New(name);
        img.sprite = TimeUiSprites.Ring(d, thick);
        img.color = col;
        Place(img.rectTransform, cx - d * .5f, PanelH - cy - d * .5f, d, d);
    }

    // ── 도우미 ──────────────────────────────────────────────────────
    static Transform Keep(Dictionary<string, Transform> keep, string name)
        => keep.TryGetValue(name, out var t) && t != null ? t : null;

    TMP_Text Text(Dictionary<string, Transform> keep, string name)
    {
        var t = Keep(keep, name);
        if (t == null) { Debug.LogWarning($"[스탯창] 글자 '{name}' 를 못 찾았습니다.", this); return null; }
        _order.Add(t);
        return t.GetComponent<TMP_Text>();
    }

    Image New(string name)
    {
        _built.Add(name);              // 살아남은 조각을 재활용했을 수 있다 → 정리에서 빼 준다
        return Child(_root, name);
    }

    static Image Child(RectTransform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            t = (RectTransform)go.transform;
            t.SetParent(parent, false);
        }
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();

        // ★기존 조각을 재활용했을 수 있으니 프리팹에서 묻어온 설정을 되돌린다.
        //   특히 type(Sliced/Filled)과 preserveAspect 가 남아 있으면 그림이 잘리거나
        //   가운데만 그려진다. 스프라이트·type 은 부르는 쪽이 바로 다시 정한다.
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        img.enabled = true;
        img.material = null;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = false;
        StripMeshEffects(t.gameObject);
        return img;
    }

    RawImage NewRaw(string name)
    {
        _built.Add(name);
        var t = _root.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            t = (RectTransform)go.transform;
            t.SetParent(_root, false);
        }
        var raw = t.GetComponent<RawImage>();
        if (raw == null) raw = t.gameObject.AddComponent<RawImage>();

        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        raw.enabled = true;
        raw.material = null;
        raw.raycastTarget = false;
        StripMeshEffects(t.gameObject);
        return raw;
    }

    /// <summary>재활용한 조각에 붙어 있을 수 있는 Outline/Shadow 를 떼어 낸다.
    /// ★이건 그래픽을 복제해 사방으로 밀어 그리는 것이라, 속이 빈 프레임이 아닌 판에 붙으면
    ///   복제본이 원본을 통째로 덮어 버린다(예전에 창이 단색 판으로 나왔던 그 원인).</summary>
    static void StripMeshEffects(GameObject go)
    {
        var fx = go.GetComponents<BaseMeshEffect>();
        for (int i = 0; i < fx.Length; i++)
        {
            if (fx[i] == null) continue;
            fx[i].enabled = false;   // Destroy 는 프레임 끝이라, 이번 프레임부터 안 그려지게 먼저 끈다
            Destroy(fx[i]);
        }
    }

    static Image ChildImg(Transform p, string n)
    {
        var t = p.Find(n);
        return t == null ? null : t.GetComponent<Image>();
    }

    /// <summary>좌하단 기준 배치. 호출부는 시안(왼쪽 위 원점) 좌표를 PanelH 에서 빼서 넘긴다.</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>가운데를 기준으로 놓고 돌린다(다이얼 눈금용). 앵커는 좌하단 그대로 두고
    /// 피벗만 가운데로 옮겨야 회전축이 조각의 중심이 된다.</summary>
    static void PlaceRotated(RectTransform rt, float cx, float cy, float w, float h, float angle)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = new Vector2(cx, cy);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(.5f, .5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    // ── 반복 텍스처 ─────────────────────────────────────────────────
    // 격자와 주사선은 한 주기만 만들어 RawImage 의 uvRect 로 늘려 쓴다.
    //   ★한 장을 만들어 계속 쓴다. 창을 열 때마다 만들면 텍스처가 쌓인다.
    static readonly Dictionary<string, Texture2D> _tex = new();

    static Texture2D Repeat(string key, int size, System.Func<int, int, bool> on)
    {
        if (_tex.TryGetValue(key, out var t) && t != null) return t;

        t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point,      // 선이 번지지 않게
            hideFlags = HideFlags.HideAndDontSave
        };
        var px = new Color32[size * size];
        var lit = new Color32(255, 255, 255, 255);
        var clear = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = on(x, y) ? lit : clear;
        t.SetPixels32(px); t.Apply();

        _tex[key] = t;
        return t;
    }

    static bool GridLine(int x, int y) => x == 0 || y == 0;   // 32px 격자
    static bool ScanLine(int x, int y) => y < 2;              // 4px 중 2줄

    // 헤더 눈금 — 한 칸에 1px 짜리 짧은 눈금 하나. 위쪽 절반(uvRect 로 8px 만 쓴다)에서
    // 아래로 5px 내려온 모양이라, 헤더 가로선에 매달린 것처럼 보인다.
    static bool HeaderTick(int x, int y) => x == 0 && y < 5;

    static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
}
