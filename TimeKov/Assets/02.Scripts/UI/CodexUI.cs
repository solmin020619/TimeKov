// =====================================================================
// CodexUI.cs
// 도감(엔드필드 백과 풍) - 전체화면 정지 UI. K로 열고 닫음(상태/정지/입력/커서는
// GameUIController UIState.Codex가 관리, 여기선 패널 표시/빌드만).
//
// 디자인 = Assets/11.UI/Codex/Codex Frame.dc.html (라이트 간유리 풍).
// 틀(프레임)만 구현 - 데이터는 placeholder(실제 게임데이터/해금상태 연동은 다음 단계).
//   탭: 튜토리얼 / 몬스터 / 설비 (상단 중앙). 좌측 항목 리스트. 우측은 탭마다 레이아웃 다름.
//   리스트 상태: 공개 / 실루엣 / ???(미발견).
// PNG = Resources/Codex/ 에서 로드(없으면 절차생성 폴백). 종욱이 11.UI/Codex 의 PNG를
//   Resources/Codex 로 옮기면 자동 반영(9-slice .meta 같이). Inven_Ash 금지.
// =====================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CodexUI : MonoBehaviour
{
    private static CodexUI _i;
    private static bool _quitting;

    public static CodexUI I
    {
        get
        {
            if (_i == null && !_quitting)
            {
                var go = new GameObject("[CodexUI]");
                _i = go.AddComponent<CodexUI>();
                DontDestroyOnLoad(go);
                _i.Build();
            }
            return _i;
        }
    }

    // 켜질 때만 생성(한 번도 안 열면 비용 0). GameUIController.ApplyState에서 호출.
    public static void SetVisible(bool on)
    {
        if (!on && _i == null) return;
        I.SetOpenInternal(on);
    }

    // ── 색 토큰 (dc.html 핸드오프) ──────────────────────────────────
    static readonly Color Bg        = C32(216, 221, 226);   // 라이트 그레이 배경(패널이 더 밝게 떠 보이도록 살짝 어둡게)
    static readonly Color PanelLight = C32(248, 250, 252);  // 패널 = 거의 흰 라이트 면(엔필식). 회색 panel_ash는 인벤용이라 안 씀.
    static readonly Color PanelBd   = new Color32(120, 140, 160, 40); // 패널 얇은 테두리
    static readonly Color Divider   = new Color32(120, 140, 160, 60);
    static readonly Color TabPillBg  = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color TabPillBd  = new Color32(120, 140, 160, 56);
    static readonly Color Accent    = C32(95, 196, 255);
    static readonly Color TabActiveTxt   = C32(22, 51, 63);
    static readonly Color TabInactiveTxt = C32(91, 99, 109);
    static readonly Color TxtMain  = C32(36, 42, 49);
    static readonly Color TxtSub   = C32(76, 84, 93);
    static readonly Color TxtDim   = C32(170, 178, 187);
    static readonly Color TxtMono  = C32(124, 132, 140);
    // 인벤 슬롯과 동일 소스(InventoryUIBuilder). 각진 반투명 다크 + 4변 쿨슬레이트 테두리.
    static readonly Color SlotFill = new Color(12f / 255f, 16f / 255f, 24f / 255f, 0.10f);   // 칸 = 살짝만 어둡게(테두리가 정의)
    static readonly Color SlotLine = new Color(40f / 255f, 54f / 255f, 74f / 255f, 0.45f);   // 4변 균일 테두리(쿨 슬레이트)
    static readonly Color CloseBg  = new Color(1f, 1f, 1f, 0.60f);
    static readonly Color CardBg   = new Color(1f, 1f, 1f, 0.40f);
    static readonly Color CardBd   = new Color32(120, 140, 160, 41);

    // ── 데이터 모델 (placeholder) ──────────────────────────────────
    enum St { Public, Silhouette, Hidden }
    class Entry { public string name; public St state; public string status; public CodexPreviewConfig.MonsterEntry preview; public Sprite icon; public int facilityId; public VideoTutorialPage tutPage; public string monsterSourceId; }
    class Drop  { public string name; public string rarity; public Color rc; public string desc; public string pct; public bool got; public Sprite icon; public int grade; }

    static readonly string[] TabNames = { "튜토리얼", "몬스터", "설비" };

    static readonly Entry[] Tutorial =
    {
        new Entry{ name="기본 이동",     state=St.Public },
        new Entry{ name="시간 자원",     state=St.Public },
        new Entry{ name="근접 전투",     state=St.Public },
        new Entry{ name="조립대 사용",   state=St.Public },
        new Entry{ name="컨베이어 연결", state=St.Hidden, status="미시청" },
        new Entry{ name="전력 공급",     state=St.Hidden, status="미시청" },
        new Entry{ name="위험 지대",     state=St.Hidden, status="미시청" },
    };
    static readonly Entry[] Monsters =
    {
        new Entry{ name="사구 포식자", state=St.Public },
        new Entry{ name="강철 진드기", state=St.Public },
        new Entry{ name="시간 포자",   state=St.Silhouette, status="포착" },
        new Entry{ name="부식 군체",   state=St.Silhouette, status="포착" },
        new Entry{ state=St.Hidden, status="미발견" },
        new Entry{ state=St.Hidden, status="미발견" },
        new Entry{ state=St.Hidden, status="미발견" },
        new Entry{ state=St.Hidden, status="미발견" },
    };
    static readonly Entry[] Facilities =
    {
        new Entry{ name="정련로",        state=St.Public },
        new Entry{ name="분쇄기",        state=St.Public },
        new Entry{ name="성형기",        state=St.Public },
        new Entry{ name="재배기",        state=St.Silhouette, status="미해금" },
        new Entry{ name="씨앗 추출기",   state=St.Silhouette, status="미해금" },
        new Entry{ name="오염수 처리기", state=St.Silhouette, status="미해금" },
    };
    Entry[] CurList => _tab == 0 ? (_tutorial ?? Tutorial) : _tab == 1 ? (_monsters ?? Monsters) : (_facilities ?? Facilities);

    // ── 런타임 상태 ────────────────────────────────────────────────
    private RectTransform _root;
    private RectTransform _tabRow;
    private RectTransform _listBox;
    private RectTransform _mainBox;
    private RectTransform _completionBox;   // 헤더 우측: 현재 카테고리 도감 달성률
    private TMP_FontAsset _font;
    private int _tab = 1;   // 시작 = 몬스터(중간 상태 mock 확인용)
    private int _sel = 0;

    private CodexPreviewConfig _cfg;        // Resources/Codex/CodexPreviewConfig (있으면 몬스터탭 구동)
    private CodexTutorialConfig _tutCfg;    // Resources/Codex/CodexTutorialConfig (있으면 튜토탭 구동)
    private CodexMonsterPortrait _portrait; // 흉상 라이브 렌더러(지연 생성)
    private CodexVideoScreen _video;        // 튜토 인라인 영상 재생기(지연 생성)
    private Entry[] _tutorial;              // 런타임 리스트(개발자 해금 적용)
    private Entry[] _monsters;              // 플레이스홀더 + 설정 병합(인덱스 매칭)
    private Entry[] _facilities;
    private static bool _devUnlockAll = true;   // 개발자모드: 전부 해금(테스트용). 도감에서 F9로 토글.

    private void OnApplicationQuit() => _quitting = true;
    private void OnDestroy()
    {
        if (_i == this) _i = null;
        DataBoot.OnDataLoaded -= HandleDataLoaded;
    }

    private bool _dataLoading;

    // 게임 데이터(설비/레시피/아이템)는 Loading 씬의 DataBoot가 구글시트서 로드한다.
    // World에서 바로 Play하면 비어 있으니, 정상 부팅(DataBoot)이 없을 때만 직접 로드(테스트 편의).
    private void EnsureGameData()
    {
        if (_dataLoading) return;
        if (GameDataUtility.GetFacility(1) != null) return;   // 이미 로드됨
        if (DataBoot.Instance != null) return;                // 정상 부팅 경로 있으면 그쪽에 맡김(이중 다운로드 방지)
        _dataLoading = true;
        GameDataHolder.I.LoadAllFromGoogle(this, ok =>
        {
            _dataLoading = false;
            HandleDataLoaded();
        });
    }

    // 데이터 로드 완료 시(정상 부팅이 늦게 끝나도) 리스트/본문 갱신
    private void HandleDataLoaded()
    {
        RebuildLists();
        if (_root != null && _root.gameObject.activeSelf) Refresh();
    }

    // 도감 열려있을 때 F9 = 개발자 전부해금 토글(테스트용)
    private void Update()
    {
        if (_root == null || !_root.gameObject.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.F9))
        {
            _devUnlockAll = !_devUnlockAll;
            RebuildLists();
            _sel = 0;
            if (_portrait != null) _portrait.ClearInstance();
            Refresh();
        }
    }

    private void SetOpenInternal(bool on)
    {
        if (on)
        {
            // 열 때마다 최신 설정 반영(populate 메뉴를 플레이 중 돌려도 잡히게)
            _cfg = Resources.Load<CodexPreviewConfig>("Codex/CodexPreviewConfig");
            _tutCfg = Resources.Load<CodexTutorialConfig>("Codex/CodexTutorialConfig");
            EnsureGameData();   // 게임 데이터(구글시트) 안 떠 있으면 로드 시도
            RebuildLists();
            _sel = 0;
            Refresh();
        }
        if (_root != null) _root.gameObject.SetActive(on);
        if (!on) CloseSourcePopup();                               // 닫으면 떠 있던 출처 팝업 정리
        if (!on) HideTooltip();
        if (!on && _portrait != null) _portrait.ClearInstance();   // 닫으면 림보 인스턴스/카메라 정리
        if (!on && _video != null) _video.Stop();                  // 닫으면 영상 정지
    }

    // ── 빌드 ───────────────────────────────────────────────────────
    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _font = TMP_Settings.defaultFontAsset;

        DataBoot.OnDataLoaded += HandleDataLoaded;   // 데이터가 나중에 로드돼도 갱신되게
        _cfg = Resources.Load<CodexPreviewConfig>("Codex/CodexPreviewConfig");   // 없으면 플레이스홀더로 폴백
        _tutCfg = Resources.Load<CodexTutorialConfig>("Codex/CodexTutorialConfig");
        RebuildLists();

        _root = Make("Root", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Img(_root, null, Bg);

        var panel = Make("Panel", _root, Vector2.zero, Vector2.one, new Vector2(40f, 40f), new Vector2(-40f, -40f));
        Img(panel, RoundedFallback(22), PanelLight);   // 라이트 면(엔필식). 회색 panel_ash 안 씀.
        Line(panel, PanelBd);
        BuildPanelDecor(panel);   // 빈 흰면 -> 등고선/그라데이션/코너 브래킷으로 장치 화면 느낌

        var header = Make("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -96f), new Vector2(-30f, 0f));
        BuildHeader(header);

        var hLine = Make("HeaderLine", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -98f), new Vector2(-30f, -97f));
        Img(hLine, null, Divider);

        _listBox = Make("ListBox", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(30f, 22f), new Vector2(460f, -108f));

        var vLine = Make("VLine", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(467f, 30f), new Vector2(468f, -118f));
        Img(vLine, null, Divider);

        _mainBox = Make("MainBox", panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(480f, 24f), new Vector2(-30f, -108f));

        Refresh();
        _root.gameObject.SetActive(false);
    }

    // ── 헤더 (타이틀 + 중앙 탭 + 닫기) ─────────────────────────────
    private void BuildHeader(RectTransform header)
    {
        var title = Make("Title", header, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        title.sizeDelta = new Vector2(300f, 56f); title.anchoredPosition = new Vector2(150f, 8f);
        var thlg = title.gameObject.AddComponent<HorizontalLayoutGroup>();
        thlg.childAlignment = TextAnchor.LowerLeft; thlg.spacing = 12f;
        thlg.childControlWidth = true; thlg.childControlHeight = true;
        thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
        Txt(NewChild("도감", title), "도감", 36f, FontStyles.Bold, TxtMain, TextAlignmentOptions.BottomLeft);
        Txt(NewChild("CODEX", title), "CODEX", 13f, FontStyles.Normal, Accent, TextAlignmentOptions.BottomLeft);

        _tabRow = Make("Tabs", header, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _tabRow.sizeDelta = new Vector2(560f, 56f); _tabRow.anchoredPosition = new Vector2(0f, 8f);
        Img(_tabRow, RoundedFallback(16), TabPillBg);
        Line(_tabRow, TabPillBd);
        var hlg = _tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 6f;
        hlg.padding = new RectOffset(7, 7, 7, 7);
        hlg.childControlWidth = false; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
        var fit = _tabRow.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        RebuildTabs();

        var close = Make("Close", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        close.sizeDelta = new Vector2(54f, 54f); close.anchoredPosition = new Vector2(-27f, 8f);
        Img(close, RoundedFallback(14), CloseBg);
        Line(close, TabPillBd);
        Btn(close, () => GameUIController.Instance?.ToggleCodex());
        var ci = Make("X", close, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ci.sizeDelta = new Vector2(22f, 22f);
        var icon = Codex("ic_close");
        if (icon != null) { var im = Img(ci, icon, new Color(0f, 0f, 0f, 0.45f)); im.preserveAspect = true; }
        else Txt(ci, "X", 22f, FontStyles.Bold, new Color(0f, 0f, 0f, 0.45f), TextAlignmentOptions.Center);

        // 닫기 왼쪽: 현재 카테고리 도감 달성률(탭 전환 시 갱신). 기존 장식 telemetry 자리를 의미있게 대체.
        _completionBox = Make("Completion", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        _completionBox.sizeDelta = new Vector2(216f, 42f); _completionBox.anchoredPosition = new Vector2(-192f, 8f);
        RebuildCompletion();
    }

    // 현재 탭(카테고리)의 도감 달성률 = 해금(St.Public) 수 / 전체.
    // 개발자 전부해금(F9/_devUnlockAll)이면 전부 Public이라 100%로 뜬다(실제 해금 붙이면 자동 반영).
    private (int got, int total) CurrentCompletion()
    {
        var list = CurList;
        int got = 0, total = 0;
        if (list != null)
            foreach (var e in list) { total++; if (e.state == St.Public) got++; }
        return (got, total);
    }

    private void RebuildCompletion()
    {
        if (_completionBox == null) return;
        ClearChildren(_completionBox);
        var (got, total) = CurrentCompletion();
        float ratio = total > 0 ? (float)got / total : 0f;
        int pct = Mathf.RoundToInt(ratio * 100f);

        // 상단: 카테고리 + 수치(모노, 우측정렬)
        Txt(Make("ctxt", _completionBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -17f), new Vector2(0f, 0f)),
            TabNames[_tab] + " 도감  " + got + " / " + total + "  ·  " + pct + "%", 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Right);

        // 하단: 진행 바(트랙 + 채움)
        var track = Make("track", _completionBox, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(0f, 9f));
        Img(track, RoundedFallback(3), new Color32(120, 140, 160, 46)).raycastTarget = false;
        var fill = Make("fill", track, new Vector2(0f, 0f), new Vector2(Mathf.Clamp01(ratio), 1f), Vector2.zero, Vector2.zero);
        Img(fill, RoundedFallback(3), Accent).raycastTarget = false;
    }

    private void BuildTelemetry(RectTransform tele)
    {
        // 하단 행: 모노 주파수 (우측정렬)
        Txt(Make("freq", tele, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 16f)),
            "FREQ 432.500MHz", 11f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Right);
        // 상단 행: 미니 이퀄라이저 바
        var bar = Make("seg", tele, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -16f), new Vector2(-20f, 0f));
        BuildSegBar(bar);
        // REC 점
        var dot = Make("recdot", tele, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        dot.sizeDelta = new Vector2(7f, 7f); dot.anchoredPosition = new Vector2(-9f, -8f);
        Deco(dot, UISpriteFactory.Disc(16), C32(224, 96, 96));
    }

    private void BuildSegBar(RectTransform parent)
    {
        var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2f; hlg.childAlignment = TextAnchor.LowerRight;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        int[] hs = { 5, 8, 6, 11, 7, 9, 13, 6, 10, 7, 12, 8 };
        for (int i = 0; i < hs.Length; i++)
        {
            var s = NewChild("s", parent);
            var le = s.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 3f; le.minWidth = 3f; le.preferredHeight = hs[i]; le.minHeight = hs[i];
            Deco(s, null, new Color32(96, 150, 196, (byte)(90 + i * 8)));
        }
    }

    private void RebuildTabs()
    {
        if (_tabRow == null) return;
        for (int i = _tabRow.childCount - 1; i >= 0; i--) Destroy(_tabRow.GetChild(i).gameObject);
        for (int i = 0; i < TabNames.Length; i++) BuildTab(i);
        BuildAddTabButton();
    }

    private void BuildTab(int index)
    {
        bool active = index == _tab;
        var tab = NewChild("Tab_" + TabNames[index], _tabRow);
        var le = tab.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 42f; le.preferredHeight = 42f;
        var hlg = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 10f;
        hlg.padding = new RectOffset(22, 22, 0, 0);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fit = tab.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        Img(tab, active ? RoundedFallback(11) : null, active ? Accent : new Color(0f, 0f, 0f, 0f));
        Btn(tab, () => { _tab = index; _sel = 0; RebuildTabs(); Refresh(); });

        Color icCol = active ? TabActiveTxt : TabInactiveTxt;
        var ico = NewChild("ico", tab);
        var icoLe = ico.gameObject.AddComponent<LayoutElement>();
        icoLe.preferredWidth = 18f; icoLe.minWidth = 18f; icoLe.preferredHeight = 18f; icoLe.minHeight = 18f;
        BuildTabIcon(index, ico, icCol);

        Txt(NewChild("lbl", tab), TabNames[index], 18f, FontStyles.Bold, icCol, TextAlignmentOptions.Center);
    }

    private void BuildTabIcon(int index, RectTransform parent, Color col)
    {
        if (index == 0)        Img(parent, UISpriteFactory.Ring(48, 4f), col).preserveAspect = true;   // 원
        else if (index == 1) { Img(parent, RoundedFallback(2), col); parent.localRotation = Quaternion.Euler(0f, 0f, 45f); parent.localScale = Vector3.one * 0.72f; } // 다이아
        else                   Img(parent, UISpriteFactory.Disc(48), col).preserveAspect = true;       // 육각(폴백 디스크)
    }

    private void BuildAddTabButton()
    {
        var add = NewChild("AddTab", _tabRow);
        var le = add.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 42f; le.preferredWidth = 42f; le.minHeight = 42f; le.preferredHeight = 42f;
        Img(add, RoundedFallback(11), new Color(0f, 0f, 0f, 0f));
        Line(add, new Color32(120, 140, 160, 102));
        // 텍스트는 별도 자식에(한 GameObject에 Image+TMP_Text 같이 두면 그래픽 충돌로 NRE)
        var plus = Make("plus", add, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Txt(plus, "+", 24f, FontStyles.Normal, new Color32(120, 140, 160, 178), TextAlignmentOptions.Center);
    }

    // ── 리스트 + 메인 재빌드 ───────────────────────────────────────
    private void Refresh()
    {
        CloseSourcePopup();   // 뷰 재생성 시 떠 있던 출처 팝업 정리(스크롤/탭/네비게이션 등)
        HideTooltip();
        BuildList();
        BuildMain();
        RebuildCompletion();   // 현재 카테고리 달성률 갱신(탭/해금 변화 반영)
    }

    private int _lastBuiltTab = -1;

    private void BuildList()
    {
        // 같은 탭에서 항목 클릭 시(리스트 재생성) 스크롤 위치 보존. 탭 바뀌면 맨 위로.
        var oldScroll = _listBox.GetComponent<ScrollRect>();
        float prevY = (_tab == _lastBuiltTab && oldScroll != null && oldScroll.content != null)
            ? oldScroll.content.anchoredPosition.y : 0f;
        _lastBuiltTab = _tab;

        ClearChildren(_listBox);

        var label = Make("Label", _listBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -28f), new Vector2(-14f, -4f));
        Txt(label, "목록 - LIST", 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);

        // 항목이 많으면(튜토 영상 16개 등) 패널 밖으로 넘치니 스크롤. 뷰포트(마스크)+콘텐츠(VLG+Fitter).
        var viewport = Make("Viewport", _listBox, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(-12f, -32f));
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;   // 휠/드래그 받기
        viewport.gameObject.AddComponent<RectMask2D>();

        var rows = Make("Rows", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rows.pivot = new Vector2(0.5f, 1f);
        var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fit = rows.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var list = CurList;
        for (int i = 0; i < list.Length; i++) BuildListRow(rows, list[i], i);

        var scroll = _listBox.gameObject.GetComponent<ScrollRect>();
        if (scroll == null) scroll = _listBox.gameObject.AddComponent<ScrollRect>();
        scroll.content = rows; scroll.viewport = viewport;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        // 장식용 스크롤바 트랙(실제 스크롤은 휠/드래그)
        var sb = Make("Scrollbar", viewport, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 2f), new Vector2(0f, -2f));
        Img(sb, Codex("scrollbar_track") ?? RoundedFallback(3), new Color32(120, 140, 160, 36)).raycastTarget = false;

        // 레이아웃 즉시 계산 후 스크롤 위치 복원(클릭해도 맨 위로 안 튀게)
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
        float maxY = Mathf.Max(0f, rows.rect.height - viewport.rect.height);
        var ap = rows.anchoredPosition; ap.y = Mathf.Clamp(prevY, 0f, maxY); rows.anchoredPosition = ap;
    }

    private void BuildListRow(RectTransform parent, Entry e, int index)
    {
        bool selected = e.state == St.Public && index == _sel;
        bool textOnly = _tab != 2;   // 설비만 아이콘 썸네일. 튜토(영상제목)/몬스터(얼굴은 본문 3D)는 텍스트 전용.
        var row = NewChild("Row_" + index, parent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 92f; le.preferredHeight = 92f;   // 헤더 큼직하게 - 컬럼 꽉 채움(스크롤 있어 길어도 OK)

        if (selected)
        {
            Img(row, RoundedFallback(10), new Color32(95, 196, 255, 50));   // 선택 = 뚜렷한 강조(조금 더 세게)
            Line(row, new Color32(95, 196, 255, 130));
            var bar = Make("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(4f, -8f));
            Img(bar, RoundedFallback(3), Accent);
        }
        else
        {
            // 비선택 헤더도 기본 카드로 어느정도 강조(엔필처럼). 미발견은 더 흐리게.
            byte a = e.state == St.Public ? (byte)32 : (byte)16;
            Img(row, RoundedFallback(10), new Color32(120, 140, 160, a));
            if (e.state == St.Public) Line(row, new Color32(120, 140, 160, 42));
        }

        if (e.state == St.Public) Btn(row, () => { _sel = index; Refresh(); });

        float textLeft = 96f;
        if (!textOnly)
        {
            var thumb = Make("Thumb", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            thumb.sizeDelta = new Vector2(68f, 68f); thumb.anchoredPosition = new Vector2(48f, 0f);   // 큼직하게
            SlotBg(thumb);
            if (e.state == St.Public && e.icon != null)
            {
                var ic = Make("ic", thumb, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                var im = Img(ic, e.icon, Color.white); im.preserveAspect = true; im.raycastTarget = false;
            }
            else BuildThumbContent(thumb, e.state, selected);
        }
        else textLeft = 28f;   // 아이콘 없으니 왼쪽 패딩만

        var nameRt = Make("Name", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(textLeft, 0f), new Vector2(-12f, 0f));
        if (e.state == St.Public)
        {
            Txt(nameRt, e.name, 20f, selected ? FontStyles.Bold : FontStyles.Normal, selected ? TxtMain : C32(60, 68, 76), TextAlignmentOptions.Left);
            if (!selected) Hatch(row);   // 엔필식 미세 해칭 - 세 카테고리 공개행 통일(선택행 제외)
        }
        else
        {
            Txt(nameRt, "???", 20f, FontStyles.Normal, TxtDim, TextAlignmentOptions.Left);
            if (!string.IsNullOrEmpty(e.status))
            {
                var st = Make("Status", row, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-72f, 0f), new Vector2(-10f, 0f));
                Txt(st, e.status, 11f, FontStyles.Normal, C32(188, 195, 203), TextAlignmentOptions.Right);
            }
        }
    }

    // 엔필식 미세 해칭(행 우측 짧은 대각선 3개)
    private void Hatch(RectTransform row)
    {
        for (int i = 0; i < 3; i++)
        {
            var s = Make("hk", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            s.sizeDelta = new Vector2(2f, 13f);
            s.anchoredPosition = new Vector2(-16f - i * 6f, 0f);
            s.localRotation = Quaternion.Euler(0f, 0f, 24f);
            Deco(s, null, new Color32(120, 140, 160, 55));
        }
    }

    private void BuildThumbContent(RectTransform thumb, St state, bool selected)
    {
        if (state == St.Silhouette)
        {
            var blob = Make("Sil", thumb, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blob.sizeDelta = new Vector2(22f, 24f);
            Img(blob, UISpriteFactory.Disc(48), Color.black);
        }
        else if (state == St.Hidden)
        {
            var q = Make("q", thumb, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Txt(q, "?", 16f, FontStyles.Bold, new Color32(150, 165, 180, 102), TextAlignmentOptions.Center);
        }
        if (selected) Line(thumb, Accent, 1.5f);
    }

    // ── 우측 메인 (탭별) ───────────────────────────────────────────
    private void BuildMain()
    {
        ClearChildren(_mainBox);
        if (_tab != 1 && _portrait != null) _portrait.ClearInstance();   // 몬스터탭 아니면 라이브 프리뷰 정리
        if (_tab != 0 && _video != null) _video.Stop();                  // 튜토탭 아니면 영상 정지
        string selName = SelectedName();
        if (_tab == 0) BuildTutorialMain(selName);
        else if (_tab == 1) BuildMonsterMain(selName);
        else BuildFacilityMain(selName);
    }

    private string SelectedName()
    {
        var list = CurList;
        if (_sel >= 0 && _sel < list.Length && list[_sel].state == St.Public) return list[_sel].name;
        foreach (var e in list) if (e.state == St.Public) return e.name;
        return "-";
    }

    private Entry SelectedEntry()
    {
        var list = CurList;
        if (_sel >= 0 && _sel < list.Length && list[_sel].state == St.Public) return list[_sel];
        foreach (var e in list) if (e.state == St.Public) return e;
        return (_sel >= 0 && _sel < list.Length) ? list[_sel] : null;
    }

    // 런타임 리스트 3개 재생성(개발자 해금/설정 반영). F9 토글 시 다시 호출.
    private void RebuildLists()
    {
        _tutorial = BuildTutorialEntries();
        _monsters = BuildMonsterEntries();
        _facilities = BuildFacilityEntries();
    }

    // 기존 영상 튜토(TutorialVideoObjective)의 페이지를 복붙한 설정으로 구성. 없으면 placeholder.
    private Entry[] BuildTutorialEntries()
    {
        if (_tutCfg == null || _tutCfg.pages == null || _tutCfg.pages.Count == 0)
            return CloneDev(Tutorial);
        var list = new Entry[_tutCfg.pages.Count];
        for (int i = 0; i < _tutCfg.pages.Count; i++)
        {
            var p = _tutCfg.pages[i];
            var e = new Entry
            {
                name = (p != null && !string.IsNullOrEmpty(p.title)) ? p.title : ("영상 " + (i + 1)),
                state = St.Hidden, status = "미시청", tutPage = p
            };
            if (_devUnlockAll) { e.state = St.Public; e.status = null; }   // 시청여부는 나중에 - 지금은 전부 해금
            list[i] = e;
        }
        return list;
    }

    private void EnsureVideo()
    {
        if (_video != null) return;
        var go = new GameObject("[CodexVideo]");
        go.transform.SetParent(transform, false);
        _video = go.AddComponent<CodexVideoScreen>();
    }

    // 실제 설비 데이터(1..8)로 헤더 구성 + 아이콘(FacilityIconDatabase). 데이터 못 읽으면 placeholder 폴백.
    private Entry[] BuildFacilityEntries()
    {
        var list = new List<Entry>();
        for (int id = 1; id <= 8; id++)
        {
            var fd = GameDataUtility.GetFacility(id);
            if (fd == null) continue;
            var e = new Entry { name = fd.facilityName, state = St.Silhouette, status = "미해금", facilityId = id };
            if (FacilityIconDatabase.Instance != null) e.icon = FacilityIconDatabase.Instance.GetIcon(id);
            if (_devUnlockAll) { e.state = St.Public; e.status = null; }   // 해금은 나중에 - 지금은 개발자 전부해금
            list.Add(e);
        }
        return list.Count > 0 ? list.ToArray() : CloneDev(Facilities);
    }

    // 정적 플레이스홀더 복제 + 개발자 해금 적용(튜토/설비용. 둘 다 이름 있음).
    private Entry[] CloneDev(Entry[] src)
    {
        var list = new Entry[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var b = src[i];
            var e = new Entry { name = b.name, state = b.state, status = b.status };
            if (_devUnlockAll) { e.state = St.Public; e.status = null; }
            list[i] = e;
        }
        return list;
    }

    // 플레이스홀더 몬스터 + 설정(CodexPreviewConfig)을 인덱스로 병합.
    // 설정이 없으면 플레이스홀더 그대로(디자인 확인용). 프리팹 연결된 슬롯만 라이브 프리뷰.
    private Entry[] BuildMonsterEntries()
    {
        var list = new Entry[Monsters.Length];
        for (int i = 0; i < Monsters.Length; i++)
        {
            var b = Monsters[i];
            var e = new Entry { name = b.name, state = b.state, status = b.status };
            if (_cfg != null && i < _cfg.monsters.Count && _cfg.monsters[i] != null)
            {
                var c = _cfg.monsters[i];
                if (!string.IsNullOrEmpty(c.displayName)) e.name = c.displayName;
                e.state = MapState(c.state);
                e.status = e.state == St.Public ? null : (e.state == St.Silhouette ? "포착" : "미발견");
                if (c.prefab != null)
                {
                    e.preview = c;
                    e.monsterSourceId = c.prefab.GetComponentInChildren<EnemyDropOnDeath>(true)?.SourceId;
                }
            }
            if (_devUnlockAll) { e.state = St.Public; e.status = null; }
            if (e.state == St.Public && string.IsNullOrEmpty(e.name)) e.name = "몬스터 " + (i + 1);
            list[i] = e;
        }
        return list;
    }

    private static St MapState(CodexPreviewConfig.RevealState s)
        => s == CodexPreviewConfig.RevealState.Revealed ? St.Public
         : s == CodexPreviewConfig.RevealState.Silhouette ? St.Silhouette : St.Hidden;

    private void EnsurePortrait()
    {
        if (_portrait != null) return;
        var go = new GameObject("[CodexPortrait]");
        go.transform.SetParent(transform, false);
        _portrait = go.AddComponent<CodexMonsterPortrait>();
    }

    // 페이스 칸에 라이브 흉상 렌더(가능하면). 못 하면 false -> 호출부서 실루엣 폴백.
    private bool TryRenderPortrait(RectTransform face, Entry e)
    {
        if (e == null || e.state != St.Public || e.preview == null || e.preview.prefab == null) return false;
        EnsurePortrait();
        var tex = _portrait.Render(e.preview, 256);
        if (tex == null) return false;
        var raw = Make("portrait", face, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        var ri = raw.gameObject.AddComponent<RawImage>();
        ri.texture = tex; ri.raycastTarget = false;
        return true;
    }

    private RectTransform MainHeader(string monoLabel, string rightText, Color rightCol, bool rightBig)
    {
        var hdr = Make("MHeader", _mainBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), new Vector2(0f, 0f));
        Txt(Make("l", hdr, new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero), monoLabel, 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
        if (!string.IsNullOrEmpty(rightText))
            Txt(Make("r", hdr, new Vector2(0.4f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero), rightText, rightBig ? 20f : 11f, rightBig ? FontStyles.Bold : FontStyles.Normal, rightCol, TextAlignmentOptions.Right);
        var line = Make("MLine", _mainBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -49f), new Vector2(0f, -48f));
        Img(line, null, Divider);
        return Make("MBody", _mainBox, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -58f));
    }

    private void BuildTutorialMain(string selName)
    {
        var body = MainHeader("튜토리얼 - GUIDE", "", TxtMono, false);
        var page = SelectedEntry()?.tutPage;

        // 영상은 꽉 채우지 않고 여백 넉넉히(좌우 90 / 상 28 / 하 184) - 엔필처럼 여유있게 가운데로.
        var media = Make("Media", body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(90f, 184f), new Vector2(-90f, -28f));
        Img(media, RoundedFallback(10), C32(20, 26, 33));
        Line(media, new Color32(0, 0, 0, 76));

        // 클립 있으면 인라인 재생(다시보기), 없으면 재생 링 placeholder
        bool playing = false;
        if (page != null && page.clip != null)
        {
            EnsureVideo();
            var tex = _video.Play(page.clip);
            if (tex != null)
            {
                var raw = Make("vid", media, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                var ri = raw.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
                playing = true;
            }
        }
        if (!playing)
        {
            _video?.Stop();
            var play = Make("Play", media, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            play.sizeDelta = new Vector2(70f, 70f);
            Img(play, UISpriteFactory.Ring(96, 3f), new Color(1f, 1f, 1f, 0.5f));
        }
        DecorateDarkViewport(media, true);   // 코너+REC 틀(영상 위에)
        Txt(Make("ml", media, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -30f), new Vector2(0f, -10f)), "MEDIA - 영상", 12f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Left);

        // 제목/설명도 영상과 같은 좌우 폭으로(센터 컬럼처럼 정렬). 본문은 기존 튜토 텍스트 복붙(강조색만 라이트용 치환).
        var title = Make("T", body, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(90f, 116f), new Vector2(-90f, 156f));
        Txt(title, page != null ? page.title : selName, 26f, FontStyles.Bold, TxtMain, TextAlignmentOptions.TopLeft);
        var desc = Make("D", body, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(90f, 24f), new Vector2(-90f, 110f));
        string descText = (page != null && !string.IsNullOrEmpty(page.body))
            ? AdaptEmphasis(page.body)
            : "이 항목의 가이드 설명이 여기에 들어갑니다. 핵심은 시간 자원 관리이며, 단계별 안내와 이미지, 주의사항이 본문에 배치됩니다.";
        Txt(desc, descText, 16f, FontStyles.Normal, TxtSub, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildMonsterMain(string selName)
    {
        var selE = SelectedEntry();
        var data = MonsterData(selE);   // 프리팹의 MeleeEnemyData (실제 스탯)

        var thr = Make("ThreatDots", _mainBox, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-120f, -32f), new Vector2(0f, -8f));
        var body = MainHeader("몬스터 - THREAT", "위협도", C32(154, 162, 170), false);
        int threat = data != null ? Mathf.Clamp(Mathf.RoundToInt(data.maxHP / 300f + data.attackDamage / 20f), 1, 5) : 3;
        BuildThreatDots(thr, threat, 5);   // 기존 스탯서 산출

        var face = Make("Face", body, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        face.sizeDelta = new Vector2(176f, 176f); face.anchoredPosition = new Vector2(88f, -106f);   // 큼직하게(가독성)
        Img(face, RoundedFallback(10), C32(208, 218, 230));   // 렌더 배경과 동일 톤(밝은 쿨 글래스)
        Line(face, new Color32(120, 140, 160, 90));   // 카드 가족 쿨 슬레이트 테두리
        // 설정(프리팹)이 있으면 라이브 흉상, 없으면 실루엣 폴백
        if (!TryRenderPortrait(face, SelectedEntry()))
        {
            var blob = Make("blob", face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blob.sizeDelta = new Vector2(74f, 84f);
            Img(blob, UISpriteFactory.Disc(96), new Color32(40, 70, 96, 128));
        }
        DecorateDarkViewport(face, false, new Color(0.40f, 0.47f, 0.57f, 0.62f));   // 밝은 배경용 어두운 쿨 브래킷
        Txt(Make("rl", face, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(9f, -24f), new Vector2(0f, -6f)), "RENDER", 10f, FontStyles.Normal, new Color(0.40f, 0.47f, 0.57f, 0.72f), TextAlignmentOptions.Left);

        var statArea = Make("Stats", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -150f), new Vector2(0f, -18f));
        Txt(Make("nm", statArea, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 0f)), selName, 28f, FontStyles.Bold, TxtMain, TextAlignmentOptions.TopLeft);
        var statRow = Make("StatRow", statArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -50f));
        var hlg = statRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;       // flexibleWidth 균등분배 적용되게 control=true
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
        // 실제 스탯(MeleeEnemyData). 값 없으면 "?".
        string atkSpd = data != null && data.attackCooldown > 0.01f ? (1f / data.attackCooldown).ToString("0.0") : "?";
        AddStat(statRow, "체력", data != null ? Mathf.RoundToInt(data.maxHP).ToString("N0") : "?", "");
        AddStat(statRow, "공격력", data != null ? Mathf.RoundToInt(data.attackDamage).ToString() : "?", "");
        AddStat(statRow, "공격속도", atkSpd, "/s");
        AddStat(statRow, "이동속도", data != null ? data.moveSpeed.ToString("0.0") : "?", "m/s");
        AddStat(statRow, "사거리", data != null ? data.attackRange.ToString("0.0") : "?", "m");
        AddStat(statRow, "시야", data != null ? Mathf.RoundToInt(data.visionRange).ToString() : "?", "m");

        var line = Make("MidLine", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -204f), new Vector2(0f, -203f));
        Img(line, null, Divider);

        Txt(Make("dl", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -236f), new Vector2(0f, -212f)), "드랍 아이템 - DROPS", 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
        var grid = Make("DropGrid", body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -244f));
        var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(540f, 104f);   // 한 줄에 2개, 큼직하게
        glg.spacing = new Vector2(22f, 16f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        // 실제 드롭 + 확률(가중치 비율, 처음부터 표시). 흔한 것부터.
        var drops = BuildMonsterDrops(selE);
        if (drops.Count > 0)
            foreach (var d in drops) BuildDropCard(grid, d);
        else
            Txt(Make("nd", body, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, -244f)), "드롭 정보가 없습니다.", 14f, FontStyles.Normal, TxtDim, TextAlignmentOptions.TopLeft);
    }

    // 프리팹 -> MeleeEnemyData(스탯)
    private static MeleeEnemyData MonsterData(Entry e)
    {
        var pf = e?.preview != null ? e.preview.prefab : null;
        if (pf == null) return null;
        var brain = pf.GetComponentInChildren<EnemyBrain>(true);
        return brain != null ? brain.Data : null;
    }

    // 프리팹의 드롭 출처 ID -> DropTable 조회 -> 확률(가중치 비율) 계산해 Drop 목록
    private List<Drop> BuildMonsterDrops(Entry e)
    {
        var list = new List<Drop>();
        var pf = e?.preview != null ? e.preview.prefab : null;
        if (pf == null) return list;
        var dod = pf.GetComponentInChildren<EnemyDropOnDeath>(true);
        string srcId = dod != null ? dod.SourceId : null;
        var rows = GameDataUtility.GetMonsterDrops(srcId);
        if (rows.Count == 0) return list;

        rows.Sort((a, b) => b.weight.CompareTo(a.weight));
        int totalW = 0;
        foreach (var r in rows) totalW += Mathf.Max(0, r.weight);

        foreach (var r in rows)
        {
            var item = GameDataUtility.GetItem(r.itemId);
            if (item == null) continue;
            int gi = (int)item.itemGrade;
            float pct = totalW > 0 ? r.weight * 100f / totalW : 0f;
            string cnt = r.minCount == r.maxCount ? $"{r.minCount}개" : $"{r.minCount}~{r.maxCount}개";
            list.Add(new Drop
            {
                name = item.itemName, rarity = GradeVisual.GetName(gi), rc = GradeVisual.GetColor(gi),
                grade = gi, desc = cnt, pct = pct.ToString("0") + "%", got = true,
                icon = ItemDatabase.GetIcon(item.iconKey)
            });
        }
        return list;
    }

    private void BuildThreatDots(RectTransform parent, int filled, int total)
    {
        var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f; hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        for (int i = 0; i < total; i++)
        {
            var dot = NewChild("dot", parent);
            var le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 9f; le.preferredHeight = 9f; le.minWidth = 9f; le.minHeight = 9f;
            Img(dot, UISpriteFactory.Disc(32), i < filled ? Accent : new Color32(120, 140, 160, 76));
        }
    }

    private void AddStat(RectTransform parent, string label, string value, string unit)
    {
        var col = NewChild("stat", parent);
        var le = col.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 92f; le.flexibleWidth = 1f; le.preferredHeight = 76f;   // 6칸 동일 폭으로 균등분배(간격 일정)
        var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        Txt(NewChild("l", col), label, 14f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
        // 숫자는 크게, 단위는 작게 인라인 -> 칸 폭 절약하면서 숫자 강조
        string v = string.IsNullOrEmpty(unit) ? value : value + "<size=60%> " + unit + "</size>";
        Txt(NewChild("v", col), v, 28f, FontStyles.Bold, TxtMain, TextAlignmentOptions.Left);
    }

    private void BuildDropCard(RectTransform parent, Drop d)
    {
        var card = NewChild("drop", parent);
        Img(card, RoundedFallback(10), CardBg);
        Line(card, CardBd);
        var thumb = Make("t", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        thumb.sizeDelta = new Vector2(78f, 78f); thumb.anchoredPosition = new Vector2(56f, 0f);
        SlotBg(thumb);
        GradeAurora(thumb, d.grade, 40f);   // 등급 표현(바+오로라, 인벤 슬롯과 동일)
        if (d.icon != null)
        {
            var ic = Make("ic", thumb, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            var im = Img(ic, d.icon, Color.white); im.preserveAspect = true; im.raycastTarget = false;
        }
        else if (!d.got)
        {
            var blob = Make("sil", thumb, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            blob.sizeDelta = new Vector2(40f, 44f);
            Img(blob, UISpriteFactory.Disc(48), Color.black);
        }
        var info = Make("i", card, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(108f, 8f), new Vector2(-78f, -8f));
        var nameRow = Make("nr", info, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(0f, 0f));
        var dotR = Make("rcdot", nameRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        dotR.sizeDelta = new Vector2(9f, 9f); dotR.anchoredPosition = new Vector2(5f, 0f);
        Img(dotR, UISpriteFactory.Disc(16), d.rc);
        Txt(Make("nm", nameRow, new Vector2(0f, 0f), new Vector2(0.7f, 1f), new Vector2(20f, 0f), new Vector2(0f, 0f)), d.name, 19f, FontStyles.Bold, d.got ? TxtMain : TxtDim, TextAlignmentOptions.Left);
        Txt(Make("ra", nameRow, new Vector2(0.55f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero), d.rarity, 16f, FontStyles.Bold, C32(92, 100, 112), TextAlignmentOptions.Left);
        Txt(Make("ds", info, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 26f)), d.desc, 17f, FontStyles.Bold, C32(84, 92, 104), TextAlignmentOptions.BottomLeft);
        var pct = Make("pct", card, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        pct.sizeDelta = new Vector2(80f, 52f); pct.anchoredPosition = new Vector2(-44f, 0f);
        Txt(pct, d.pct, 26f, FontStyles.Bold, C32(60, 68, 76), TextAlignmentOptions.Right);
    }

    private void BuildFacilityMain(string selName)
    {
        var body = MainHeader("설비 - FACILITY", selName, TxtMain, true);
        Txt(Make("rl", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, -6f)), "제작 가능 레시피 - RECIPES", 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
        // 재료가 클릭 가능하다는 상시 힌트(호버 발광/툴팁과 함께 발견성 확보). RECIPES 라벨 바로 옆.
        Txt(Make("rlhint", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(212f, -30f), new Vector2(0f, -6f)), "·  재료를 누르면 출처 보기", 12f, FontStyles.Normal, Accent, TextAlignmentOptions.Left);

        // 레시피 많은 설비(용해로 등)는 패널 밖으로 넘치니 스크롤. 뷰포트(마스크)+콘텐츠.
        var vp = Make("RecipeViewport", body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -40f));
        var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;
        vp.gameObject.AddComponent<RectMask2D>();
        var rows = Make("RecipeRows", vp, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rows.pivot = new Vector2(0.5f, 1f);
        var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.content = rows; scroll.viewport = vp;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var selE = SelectedEntry();
        var recipes = (selE != null && selE.facilityId > 0) ? GameDataUtility.GetRecipesByFacilityId(selE.facilityId) : null;
        if (recipes != null && recipes.Count > 0)
        {
            // 설비 내 최대 재료수 -> 재료 영역 폭을 행마다 동일하게(= 와 결과물 열 정렬)
            int maxIn = 1;
            foreach (var r in recipes) maxIn = Mathf.Max(maxIn, GameDataUtility.GetRecipeInputs(r.SheetId).Count);
            foreach (var r in recipes) BuildRecipeRowReal(rows, r, maxIn);
        }
        else
            Txt(NewChild("none", rows), "표시할 레시피가 없습니다.", 14f, FontStyles.Normal, TxtDim, TextAlignmentOptions.Left);
    }

    // 실제 레시피 데이터로 행 구성 (재료 + 재료 = 결과물, 전부 실제 아이템 아이콘)
    private void BuildRecipeRowReal(RectTransform parent, RecipeDataSheetData r, int maxInputs)
    {
        var row = NewChild("recipe", parent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 86f; le.preferredHeight = 86f;   // 위아래 공백 타이트하게(셀은 큼직)
        var top = Make("top", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 0f));
        top.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 구분선은 레이아웃 흐름서 제외(overlay)
        Img(top, null, new Color32(120, 140, 160, 46));
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(4, 4, 0, 0);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;   // 안 늘려서 좌측 패킹(재료 안 퍼짐)

        // 입력 재료(왼쪽). 플레이어 직관 = 재료 + 재료 = 결과물. 재료 영역을 (설비 최대 재료수 기준)
        // 고정폭으로 묶어 -> 재료 개수가 행마다 달라도 = 와 결과물이 같은 x에 정렬된다.
        var ins = NewChild("ins", row);
        var insLe = ins.gameObject.AddComponent<LayoutElement>();
        float insW = 372f * Mathf.Max(1, maxInputs) - 164f;   // 372*N-178 = 재료 N개 콘텐츠폭(이름112/+150 기준), +14 우측여백(=/결과물 위치 고정)
        insLe.preferredWidth = insW; insLe.minWidth = insW;
        var ihlg = ins.gameObject.AddComponent<HorizontalLayoutGroup>();
        ihlg.spacing = 14f; ihlg.childAlignment = TextAnchor.MiddleLeft;
        ihlg.childControlWidth = true; ihlg.childControlHeight = true;
        ihlg.childForceExpandWidth = false; ihlg.childForceExpandHeight = false;

        var inputs = GameDataUtility.GetRecipeInputs(r.SheetId);
        for (int m = 0; m < inputs.Count; m++)
        {
            if (m > 0)
            {
                var pl = NewChild("plus", ins);
                pl.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;   // 넓은 거터(2번째 재료만 우측으로)
                Txt(pl, "+", 24f, FontStyles.Bold, C32(96, 106, 120), TextAlignmentOptions.Left).margin = new Vector4(32f, 0f, 0f, 0f);   // 좌측 패딩 = + 좌우 위치(왼쪽쏠림 완화)
            }
            var it = ItemDatabase.GetItem(inputs[m].itemId);
            int srcItemId = inputs[m].itemId;   // 루프 변수 m 캡처 방지(람다서 마지막 값으로 고정되는 것)
            var cell = AddItemCell(ins, 68f, it, inputs[m].count);
            HoverButton(cell, () => ShowSourcePopup(srcItemId));   // 재료 클릭 -> 출처 팝업(+호버 발광으로 발견성)
            var mn = NewChild("mn", ins);
            mn.gameObject.AddComponent<LayoutElement>().preferredWidth = 112f;   // 이름 고정폭(타이트) -> 열 정렬 + 트레일링 여백 축소
            Txt(mn, it != null ? it.itemName : inputs[m].itemId.ToString(), 15f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);
        }

        // = 거터(고정폭 -> 행마다 동일 위치)
        var eq = NewChild("eq", row);
        eq.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;   // 넓은 거터 + 가운데
        Txt(eq, "=", 28f, FontStyles.Bold, C32(96, 106, 120), TextAlignmentOptions.Center);

        // = 와 결과물 사이 간격(너무 붙지 않게). = 는 그대로, 결과물만 우측으로.
        var ogap = NewChild("ogap", row);
        ogap.gameObject.AddComponent<LayoutElement>().preferredWidth = 42f;

        // 결과물(오른쪽). 굵게 + 큰 아이콘이라 끝에 와도 눈에 띔(제품 스캔용).
        var outItem = GetItemById(r.outputItemId);
        AddItemCell(row, 76f, outItem, r.outputCount);
        var oName = NewChild("on", row);
        oName.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
        Txt(oName, outItem != null ? outItem.itemName : (string)r.outputItemId, 18f, FontStyles.Bold, TxtMain, TextAlignmentOptions.Left);
    }

    // ── 재료 출처 팝업 + 도감 내 네비게이션 ───────────────────────────
    private RectTransform _popup;

    private void CloseSourcePopup()
    {
        if (_popup != null) { Destroy(_popup.gameObject); _popup = null; }
    }

    // 재료 클릭 -> 이 아이템의 모든 획득 경로(제작/상자/몬스터)를 팝업으로.
    // 가공템이면 제작 설비로 이동, 몬스터 드롭이면 해금 시 이름+도감이동, 미해금이면 ???.
    private void ShowSourcePopup(int itemId)
    {
        CloseSourcePopup();
        HideTooltip();
        var item = GameDataUtility.GetItem(itemId);

        // 백드롭(바깥 클릭 시 닫힘)
        _popup = Make("SourcePopup", _root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Img(_popup, null, new Color(0f, 0f, 0f, 0.45f));
        Btn(_popup, CloseSourcePopup);

        // 카드(가운데). 높이는 ContentSizeFitter.
        var card = Make("card", _popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0.5f, 0.5f); card.sizeDelta = new Vector2(500f, 0f);
        Img(card, RoundedFallback(14), PanelLight);
        Line(card, PanelBd);
        Btn(card, () => { });   // 카드 안쪽 클릭은 닫힘 막기(이벤트 소비)
        var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 20, 22); vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 헤더: 아이콘 + 이름 + 등급
        var head = NewChild("head", card);
        head.gameObject.AddComponent<LayoutElement>().minHeight = 56f;
        var hhlg = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        hhlg.spacing = 14f; hhlg.childAlignment = TextAnchor.MiddleLeft;
        hhlg.childControlWidth = true; hhlg.childControlHeight = true;
        hhlg.childForceExpandWidth = false; hhlg.childForceExpandHeight = false;
        var hicon = NewChild("hi", head);
        var hil = hicon.gameObject.AddComponent<LayoutElement>();
        hil.preferredWidth = 56f; hil.minWidth = 56f; hil.preferredHeight = 56f; hil.minHeight = 56f;
        SlotBg(hicon);
        if (item != null) GradeAurora(hicon, (int)item.itemGrade, 28f);
        Sprite isp = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        if (isp != null)
        {
            var ic = Make("ic", hicon, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var im = Img(ic, isp, Color.white); im.preserveAspect = true; im.raycastTarget = false;
        }
        var htxt = NewChild("ht", head);
        htxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Txt(htxt, item != null ? item.itemName : itemId.ToString(), 24f, FontStyles.Bold, TxtMain, TextAlignmentOptions.Left);
        if (item != null)
        {
            var gr = NewChild("gr", head);
            gr.gameObject.AddComponent<LayoutElement>().preferredWidth = 66f;
            Txt(gr, GradeVisual.GetName((int)item.itemGrade), 16f, FontStyles.Bold, GradeVisual.GetColor((int)item.itemGrade), TextAlignmentOptions.Right);
        }

        var div = NewChild("div", card);
        div.gameObject.AddComponent<LayoutElement>().minHeight = 1f;
        Img(div, null, Divider);
        Txt(NewChild("hint", card), "획득 방법", 13f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);

        int rowCount = 0;
        // 제작 출처(가공템) -> 설비로 이동
        var recipe = GameDataUtility.GetRecipeByOutputItem(itemId);
        if (recipe != null && int.TryParse((string)recipe.facilityId, out int fid))
        {
            var fac = GameDataUtility.GetFacility(fid);
            string fname = fac != null ? fac.facilityName : ("설비 " + fid);
            AddSourceRow(card, "제작", Accent, fname + " 에서 제작", "이동", () => JumpToFacility(fid));
            rowCount++;
        }
        // 드롭 출처(상자/몬스터)
        bool chestShown = false;
        foreach (var d in GameDataUtility.GetItemDropSources(itemId))
        {
            if (d.type == SourceType.Chest)
            {
                if (chestShown) continue;
                chestShown = true;
                AddSourceRow(card, "상자", C32(150, 162, 178), "상자 파밍에서 획득", null, null);
                rowCount++;
            }
            else   // Monster
            {
                int mi = FindMonsterIndexBySourceId(d.sourceId);
                var mlist = _monsters ?? Monsters;
                if (mi >= 0 && mlist[mi].state == St.Public)
                {
                    string mn = string.IsNullOrEmpty(mlist[mi].name) ? "몬스터" : mlist[mi].name;
                    int idx = mi;
                    AddSourceRow(card, "처치", C32(214, 120, 120), mn + " 처치", "도감", () => JumpToMonster(idx));
                }
                else
                {
                    AddSourceRow(card, "처치", C32(150, 156, 164), "??? (미발견 몬스터)", null, null);
                }
                rowCount++;
            }
        }
        if (rowCount == 0)
            AddSourceRow(card, "정보", C32(150, 156, 164), "출처 정보가 없습니다", null, null);
    }

    // 출처 한 줄: [태그 칩][라벨][액션 버튼?]
    private void AddSourceRow(RectTransform card, string tag, Color tagColor, string label, string actionText, UnityEngine.Events.UnityAction action)
    {
        var row = NewChild("srow", card);
        row.gameObject.AddComponent<LayoutElement>().minHeight = 46f;
        Img(row, RoundedFallback(8), CardBg);
        Line(row, CardBd);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(12, 12, 0, 0);
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var chip = NewChild("chip", row);
        var cl = chip.gameObject.AddComponent<LayoutElement>();
        cl.preferredWidth = 52f; cl.minWidth = 52f; cl.preferredHeight = 26f; cl.minHeight = 26f;
        Img(chip, RoundedFallback(6), new Color(tagColor.r, tagColor.g, tagColor.b, 0.18f));
        Txt(chip, tag, 13f, FontStyles.Bold, tagColor, TextAlignmentOptions.Center);

        var lab = NewChild("lab", row);
        lab.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Txt(lab, label, 16f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);

        if (!string.IsNullOrEmpty(actionText) && action != null)
        {
            var btn = NewChild("act", row);
            var bl = btn.gameObject.AddComponent<LayoutElement>();
            bl.preferredWidth = 74f; bl.minWidth = 74f; bl.preferredHeight = 30f; bl.minHeight = 30f;
            Img(btn, RoundedFallback(7), Accent);
            Txt(btn, actionText, 14f, FontStyles.Bold, C32(22, 51, 63), TextAlignmentOptions.Center);
            Btn(btn, action);
        }
    }

    private int FindMonsterIndexBySourceId(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return -1;
        var list = _monsters ?? Monsters;
        for (int i = 0; i < list.Length; i++)
            if (!string.IsNullOrEmpty(list[i].monsterSourceId) && list[i].monsterSourceId == sourceId)
                return i;
        return -1;
    }

    private void JumpToFacility(int facilityId)
    {
        CloseSourcePopup();
        var list = _facilities ?? Facilities;
        for (int i = 0; i < list.Length; i++)
            if (list[i].facilityId == facilityId && list[i].state == St.Public)
            {
                _tab = 2; _sel = i; RebuildTabs(); Refresh();
                return;
            }
    }

    private void JumpToMonster(int monsterIndex)
    {
        CloseSourcePopup();
        var list = _monsters ?? Monsters;
        if (monsterIndex < 0 || monsterIndex >= list.Length || list[monsterIndex].state != St.Public) return;
        _tab = 1; _sel = monsterIndex; RebuildTabs(); Refresh();
    }

    // 인벤 슬롯과 동일 구성: 각진 반투명 다크 + 4변 얇은 쿨슬레이트 테두리(격자). 라운드/sheen 없음.
    private void SlotBg(RectTransform cell)
    {
        Img(cell, null, SlotFill);
        SlotEdge(cell, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));   // 상
        SlotEdge(cell, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));   // 하
        SlotEdge(cell, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));   // 좌
        SlotEdge(cell, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));   // 우
    }

    private void SlotEdge(RectTransform cell, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        var e = Make("edge", cell, aMin, aMax, Vector2.zero, Vector2.zero);
        e.pivot = pivot; e.sizeDelta = size; e.anchoredPosition = Vector2.zero;
        Img(e, null, SlotLine).raycastTarget = false;
    }

    // 인벤 슬롯과 동일한 등급 표현: 하단 등급 언더라인 바(또렷) + 위로 번지는 오로라(소프트).
    // Common도 은은한 은청색이 들어감(인벤과 동일 - 최하위 재료도 "약간 색").
    private void GradeAurora(RectTransform cell, int grade, float height)
    {
        var c = GradeVisual.GetColor(grade);
        // 소프트 오로라(아래->위 번짐) - UIFrostGradient(top투명/bottom불투명) × 등급색
        var a = Make("aurora", cell, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        a.pivot = new Vector2(0.5f, 0f); a.sizeDelta = new Vector2(0f, height); a.anchoredPosition = new Vector2(0f, 2f);
        Img(a, null, new Color(c.r, c.g, c.b, c.a * 0.5f)).raycastTarget = false;
        var g = a.gameObject.AddComponent<UIFrostGradient>();
        g.topColor = new Color(1f, 1f, 1f, 0f); g.bottomColor = new Color(1f, 1f, 1f, 1f);
        // 등급 언더라인 바(또렷, 인벤 GradeBorder)
        var bar = Make("gradebar", cell, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        bar.pivot = new Vector2(0.5f, 0f); bar.sizeDelta = new Vector2(0f, 4f); bar.anchoredPosition = Vector2.zero;
        Img(bar, null, new Color(c.r, c.g, c.b, 0.85f)).raycastTarget = false;
    }

    // 레시피 셀(슬롯 배경 + 아이템 아이콘 + 수량 배지). HLG 행에 LayoutElement로 추가.
    private RectTransform AddItemCell(RectTransform row, float size, ItemDataSheetData item, int count)
    {
        var cell = NewChild("cell", row);
        var le = cell.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size; le.minWidth = size; le.preferredHeight = size; le.minHeight = size;
        SlotBg(cell);
        if (item != null) GradeAurora(cell, (int)item.itemGrade, size * 0.5f);   // 등급 표현(바+오로라, 아이콘 밑)
        Sprite icon = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        if (icon != null)
        {
            var ic = Make("ic", cell, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var im = Img(ic, icon, Color.white); im.preserveAspect = true; im.raycastTarget = false;
        }
        if (count > 1)   // 수량 배지(1은 생략). 시트에서 개수 바꾸면 자동 반영.
        {
            float bw = count > 9 ? 24f : 20f;
            var badge = Make("badge", cell, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            badge.sizeDelta = new Vector2(bw, 16f);
            badge.anchoredPosition = new Vector2(-bw * 0.5f - 3f, 15f);   // 마스크 라운드 모서리 피해 살짝 안쪽
            Img(badge, RoundedFallback(7), new Color(0f, 0f, 0f, 0.62f)).raycastTarget = false;
            Txt(badge, "x" + count, 11f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        }
        return cell;
    }

    private static ItemDataSheetData GetItemById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        return GameDataHolder.I.ItemData.TryGet(itemId, out var d) ? d : null;
    }

    // 기존 튜토 본문 강조는 어두운 팝업용 골드(#FFCC00)라 라이트 패널선 안 읽힘.
    // 강조 위치/구조는 그대로 두고 색만 라이트 배경서 읽히는 블루로 치환(복붙 유지).
    private static string AdaptEmphasis(string s)
        => string.IsNullOrEmpty(s) ? s : s.Replace("#FFCC00", "#1E78C8").Replace("#ffcc00", "#1E78C8");

    // ── 패널 데코 (불투명 흰면 위에 직접 - 빈 느낌 제거) ───────────
    // 패널이 불투명이라 뒤 배경이 안 비침 -> root에 깔던 emblem/speck은 죽은 코드였음.
    // 등고선/그라데이션/코너 브래킷을 패널 자식(index 앞)으로 둬서 내용 밑에 깔리게 함.
    private void BuildPanelDecor(RectTransform panel)
    {
        var decor = Make("Decor", panel, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -16f));

        // 1) 하단 음영 그라데이션 - 평평한 흰면에 깊이감
        var grad = Make("Grad", decor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Deco(grad, GradSprite(), new Color32(96, 112, 132, 255));

        // 2) 등고선 텍스처(엔필 시그니처) - 좌하단 크게 + 우상단 작게
        var c1 = Make("ContourBL", decor, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        c1.sizeDelta = new Vector2(680f, 680f); c1.anchoredPosition = new Vector2(150f, 150f);
        Deco(c1, ContourSprite(), new Color32(96, 150, 196, 52));
        var c2 = Make("ContourTR", decor, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        c2.sizeDelta = new Vector2(460f, 460f); c2.anchoredPosition = new Vector2(-150f, -150f);
        Deco(c2, ContourSprite(), new Color32(96, 150, 196, 34));

        // 3) 본문 영역 코너 브래킷 4개 - 헤더선 아래부터 (헤더 텍스트와 안 겹치게)
        Color brk = new Color32(96, 150, 196, 120);
        CornerBracket(panel, 0, 1, brk);
        CornerBracket(panel, 1, 1, brk);
        CornerBracket(panel, 0, 0, brk);
        CornerBracket(panel, 1, 0, brk);
    }

    // cx: 0=좌 1=우, cy: 0=하 1=상. ix/iy = 코너서 안쪽 여백, arm = 팔 길이.
    private void Bracket(RectTransform parent, int cx, int cy, float ix, float iy, float arm, float thick, Color col)
    {
        var anchor = new Vector2(cx, cy);
        float px = cx == 0 ? ix : -ix;
        float py = cy == 1 ? -iy : iy;

        var h = Make("brkH", parent, anchor, anchor, Vector2.zero, Vector2.zero);
        h.sizeDelta = new Vector2(arm, thick);
        h.anchoredPosition = new Vector2(px + (cx == 0 ? arm * 0.5f : -arm * 0.5f), py);
        Deco(h, null, col);

        var v = Make("brkV", parent, anchor, anchor, Vector2.zero, Vector2.zero);
        v.sizeDelta = new Vector2(thick, arm);
        v.anchoredPosition = new Vector2(px, py + (cy == 1 ? -arm * 0.5f : arm * 0.5f));
        Deco(v, null, col);
    }

    // 본문 영역 코너(헤더선 아래부터)
    private void CornerBracket(RectTransform panel, int cx, int cy, Color col)
        => Bracket(panel, cx, cy, 20f, cy == 1 ? 104f : 18f, 38f, 2f, col);

    // 어두운 뷰포트(영상/렌더 박스) 틀 - 내부 코너 브래킷 + (옵션)REC readout.
    // 실제 영상/렌더가 박스를 덮으므로 내부 텍스처(스캔라인)는 안 넣음(무의미).
    private void DecorateDarkViewport(RectTransform media, bool withReadout, Color? bracketColor = null)
    {
        Color b = bracketColor ?? new Color(1f, 1f, 1f, 0.22f);
        Bracket(media, 0, 1, 10f, 10f, 22f, 2f, b);
        Bracket(media, 1, 1, 10f, 10f, 22f, 2f, b);
        Bracket(media, 0, 0, 10f, 10f, 22f, 2f, b);
        Bracket(media, 1, 0, 10f, 10f, 22f, 2f, b);

        if (withReadout)
        {
            var dot = Make("rdot", media, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            dot.sizeDelta = new Vector2(6f, 6f); dot.anchoredPosition = new Vector2(-92f, -19f);
            Deco(dot, UISpriteFactory.Disc(16), C32(224, 96, 96));
            Txt(Make("rtx", media, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -27f), new Vector2(-14f, -11f)),
                "REC 432.500", 10f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Right);
        }
    }

    // ── 헬퍼 (전부 RectTransform 받음) ─────────────────────────────
    static Color C32(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    static RectTransform Make(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        return rt;
    }

    static RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image Img(RectTransform rt, Sprite spr, Color col)
    {
        var img = rt.gameObject.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.sprite = spr;
        img.color = col;
        // 9-slice 보더가 있는 스프라이트(RoundedRect/panel_ash/scrollbar 등)는 Sliced -> 늘려도 모서리 안 깨짐.
        bool hasBorder = spr != null && (spr.border.x + spr.border.y + spr.border.z + spr.border.w) > 0f;
        img.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = true;
        return img;
    }

    TMP_Text Txt(RectTransform rt, string txt, float size, FontStyles style, Color col, TextAlignmentOptions align)
    {
        // 한 GameObject에 Image + TMP_Text 를 같이 두면 그래픽 충돌로 NRE. 이미 그래픽 있으면 자식에 생성.
        if (rt.GetComponent<Graphic>() != null)
            rt = Make("txt", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = txt; t.fontSize = size; t.fontStyle = style; t.color = col;
        t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Btn(RectTransform rt, UnityEngine.Events.UnityAction onClick)
    {
        var img = rt.gameObject.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(onClick);
    }

    // 클릭 가능한 셀: 클릭 + 호버 시 발광 + 툴팁(발견성 확보). 평소엔 클릭 신호가 없어 아무도 안 눌러보는 문제 해결.
    void HoverButton(RectTransform cell, UnityEngine.Events.UnityAction onClick)
    {
        var hl = Make("hover", cell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var hlImg = Img(hl, null, Color.white); hlImg.raycastTarget = false;   // 호버 글로우 오버레이(평소 투명)
        var cellImg = cell.gameObject.GetComponent<Image>();
        if (cellImg != null) cellImg.raycastTarget = true;
        var btn = cell.gameObject.AddComponent<Button>();
        btn.targetGraphic = hlImg;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(Accent.r, Accent.g, Accent.b, 0.18f);
        cb.pressedColor = new Color(Accent.r, Accent.g, Accent.b, 0.30f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.12f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // 호버 툴팁("자세히 보기 / 좌클릭") - 마우스 올리면 셀 위에 표시
        var et = cell.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener(e => ShowTooltip(cell));
        et.triggers.Add(enter);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener(e => HideTooltip());
        et.triggers.Add(exit);
    }

    private RectTransform _tooltip;

    private void EnsureTooltip()
    {
        if (_tooltip != null) return;
        _tooltip = Make("HoverTip", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _tooltip.pivot = new Vector2(0.5f, 0f); _tooltip.sizeDelta = new Vector2(128f, 54f);
        Img(_tooltip, RoundedFallback(10), new Color(0.10f, 0.13f, 0.18f, 0.96f)).raycastTarget = false;
        Line(_tooltip, new Color32(120, 140, 160, 80));
        Txt(Make("t1", _tooltip, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(0f, -6f)),
            "자세히 보기", 14f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Txt(Make("t2", _tooltip, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 24f)),
            "좌클릭", 12f, FontStyles.Normal, new Color(0.62f, 0.78f, 0.95f, 1f), TextAlignmentOptions.Center);
        _tooltip.gameObject.SetActive(false);
    }

    // 셀 상단 중앙 위에 툴팁 배치(마스크 밖 _root 자식이라 스크롤 영역에 안 잘림)
    private void ShowTooltip(RectTransform cell)
    {
        if (cell == null || _root == null) return;
        EnsureTooltip();
        var corners = new Vector3[4];
        cell.GetWorldCorners(corners);   // 0 BL, 1 TL, 2 TR, 3 BR
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, topCenter);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local))
        {
            _tooltip.anchoredPosition = local + new Vector2(0f, 12f);
            _tooltip.SetAsLastSibling();
            _tooltip.gameObject.SetActive(true);
        }
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.gameObject.SetActive(false);
    }

    static void Line(RectTransform rt, Color col, float width = 1f)
    {
        // 프로젝트에 3D용 커스텀 Outline(QuickOutline)이 있어 이름 충돌 -> uGUI 외곽선은 풀네임으로.
        var o = rt.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = col;
        o.effectDistance = new Vector2(width, -width);
    }

    static void ClearChildren(RectTransform rt)
    {
        if (rt == null) return;
        for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject);
    }

    static Sprite Codex(string name) => Resources.Load<Sprite>("Codex/" + name);
    static Sprite RoundedFallback(int radius) => UISpriteFactory.RoundedRect(Mathf.Max(16, radius * 3), radius);

    // 데코용 Img(클릭 안 막게 raycast 끔)
    static Image Deco(RectTransform rt, Sprite spr, Color col)
    {
        var im = Img(rt, spr, col);
        im.raycastTarget = false;
        return im;
    }

    // ── 절차생성 텍스처 (런타임 1회 생성 후 캐시. 흰색으로 그려 Image.color로 틴트) ──
    static Sprite _contour;
    static Sprite _grad;

    // 등고선(지형도 풍) - 중심에서 방사 + 각도 웨이브로 물결치는 동심 라인
    static Sprite ContourSprite()
    {
        if (_contour != null) return _contour;
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        float c = N * 0.5f;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float ang = Mathf.Atan2(dy, dx);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float warp = 0.06f * Mathf.Sin(ang * 3f) + 0.04f * Mathf.Sin(ang * 5f + 1.3f);
                float v = (dist + warp) * 7f;                 // 동심 밴드 수
                float band = Mathf.Abs(v - Mathf.Round(v));   // 밴드 경계서 0
                float lineA = Mathf.Clamp01(1f - band / 0.07f);
                float fade = Mathf.Clamp01(1f - dist);        // 텍스처 가장자리서 사라지게
                byte a = (byte)(lineA * fade * 255f);
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px); tex.Apply();
        _contour = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        return _contour;
    }

    // 세로 그라데이션 - 아래로 갈수록 진해짐(하단 음영용). 흰색 a값만 그라데이션.
    static Sprite GradSprite()
    {
        if (_grad != null) return _grad;
        const int H = 64;
        var tex = new Texture2D(2, H, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[2 * H];
        for (int y = 0; y < H; y++)
        {
            float t = 1f - y / (float)(H - 1);   // y=0(텍스처 하단)서 1
            byte a = (byte)(t * t * 70f);
            var col = new Color32(255, 255, 255, a);
            px[y * 2] = col; px[y * 2 + 1] = col;
        }
        tex.SetPixels32(px); tex.Apply();
        _grad = Sprite.Create(tex, new Rect(0, 0, 2, H), new Vector2(0.5f, 0.5f));
        return _grad;
    }
}
