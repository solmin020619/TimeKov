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
    static readonly Color Bg        = C32(199, 205, 214);   // 맨뒤 배경(더 어둡게 톤다운). 게임서 K 눌러도 눈 안 부시게
    static readonly Color PanelLight = C32(233, 238, 244);  // 패널 면(순백 248 -> 톤다운). 좌측 리스트 강조는 행 알파로 보강(묻힘 방지).
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

    static readonly string[] TabNames = { "튜토리얼", "몬스터", "설비", "아이템" };

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
    private int _highlightOutputItemId;   // 출처점프 시 강조할 결과물 아이템(그 레시피 행을 지속 강조). 0=없음
    private RectTransform _vLine;         // 좌/우 구분선(모든 탭 공통)
    private RectTransform _itemGrid;      // 아이템 카드 그리드 콘텐츠
    private int _itemCatIndex = 0;        // 아이템 분류 필터(0=전체, 1~6=카테고리)

    private CodexPreviewConfig _cfg;        // Resources/Codex/CodexPreviewConfig (있으면 몬스터탭 구동)
    private CodexTutorialConfig _tutCfg;    // Resources/Codex/CodexTutorialConfig (있으면 튜토탭 구동)
    private CodexRecipeRewardConfig _rewardCfg;   // Resources/Codex/CodexRecipeRewardConfig (설비별 보상 개수)
    private CodexMonsterPortrait _portrait; // 흉상 라이브 렌더러(지연 생성)
    private CodexVideoScreen _video;        // 튜토 인라인 영상 재생기(지연 생성)
    private Entry[] _tutorial;              // 런타임 리스트(개발자 해금 적용)
    private Entry[] _monsters;              // 플레이스홀더 + 설정 병합(인덱스 매칭)
    private Entry[] _facilities;
    private static bool _devUnlockAll = false;   // 기본 = 실제 발견/해금 게이팅. F9로 "전부 해금"(개발 확인용) 토글.

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
        bool wasOpen = _root != null && _root.gameObject.activeSelf;
        if (on != wasOpen) GameSfx.Play(on ? SfxId.CodexOpen : SfxId.CodexClose);   // 패널 열기/닫기음(상태 변할 때만)
        if (on)
        {
            // 열 때마다 최신 설정 반영(populate 메뉴를 플레이 중 돌려도 잡히게)
            _cfg = Resources.Load<CodexPreviewConfig>("Codex/CodexPreviewConfig");
            _tutCfg = Resources.Load<CodexTutorialConfig>("Codex/CodexTutorialConfig");
            _rewardCfg = Resources.Load<CodexRecipeRewardConfig>("Codex/CodexRecipeRewardConfig");
            EnsureGameData();   // 게임 데이터(구글시트) 안 떠 있으면 로드 시도
            RebuildLists();
            _sel = 0;
            _highlightOutputItemId = 0;   // 새로 열면 출처점프 강조 초기화
            Refresh();
            CodexNotice.MarkSeen(_tab);   // 열 때 현재 탭은 본 것으로(HUD 배지 즉시 갱신)
        }
        if (_root != null) _root.gameObject.SetActive(on);
        // 탭 재빌드(배지 반영)는 반드시 루트 활성화 뒤에 — 비활성 중 빌드하면 탭 ContentSizeFitter가
        // 레이아웃을 못 잡아 첫 오픈에 탭이 퍼져 보인다(클릭하면 활성상태 재빌드라 정상화되던 버그).
        if (on) RebuildTabs();
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
        _rewardCfg = Resources.Load<CodexRecipeRewardConfig>("Codex/CodexRecipeRewardConfig");
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

        _vLine = Make("VLine", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(467f, 30f), new Vector2(468f, -118f));
        Img(_vLine, null, Divider);

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
        if (_tab == 3)   // 아이템 = 획득 수 / 전체 아이템 수
        {
            int g = 0, t = 0;
            foreach (var it in GameDataHolder.I.ItemData.All)
            {
                t++;
                if (_devUnlockAll || ItemDiscovery.IsObtained(ItemIdOf(it))) g++;
            }
            return (g, t);
        }
        var list = CurList;
        int got = 0, total = 0;
        if (list != null)
            foreach (var e in list) { total++; if (e.state == St.Public) got++; }
        return (got, total);
    }

    // ItemDataSheetData 의 정수 itemId (SheetId 문자열 파싱). 실패 시 0.
    private static int ItemIdOf(ItemDataSheetData it)
        => (it != null && int.TryParse((string)it.SheetId, out int id)) ? id : 0;

    private void RebuildCompletion()
    {
        if (_completionBox == null) return;
        ClearChildren(_completionBox);
        var (got, total) = CurrentCompletion();
        float ratio = total > 0 ? (float)got / total : 0f;
        int pct = Mathf.RoundToInt(ratio * 100f);

        // 상단: 카테고리 + 수치(모노, 우측정렬)
        Txt(Make("ctxt", _completionBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -17f), new Vector2(0f, 0f)),
            TabNames[_tab] + " 도감  " + got + " / " + total + "  ·  " + pct + "%", 14f, FontStyles.Bold, TxtSub, TextAlignmentOptions.Right);

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
        // 옛 탭은 즉시 비활성(Destroy는 지연 -> 안 빼면 옛 4개+새 4개가 같이 레이아웃돼 퍼짐).
        for (int i = _tabRow.childCount - 1; i >= 0; i--)
        {
            var c = _tabRow.GetChild(i).gameObject;
            c.SetActive(false);
            Destroy(c);
        }
        for (int i = 0; i < TabNames.Length; i++) BuildTab(i);
        // 중첩 ContentSizeFitter + TMP는 한 프레임에 안 잡혀 첫 오픈에 탭이 퍼져 보였다.
        // TMP 메시 갱신 후 레이아웃을 즉시 확정해 항상 동일 배치(사진1 고정).
        if (_tabRow.gameObject.activeInHierarchy)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tabRow);
        }
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
        Btn(tab, () => { _tab = index; _sel = 0; _highlightOutputItemId = 0; CodexNotice.MarkSeen(index); RebuildTabs(); Refresh(); }, SfxId.CodexTabClick);   // 탭 위젯은 별도 사운드

        Color icCol = active ? TabActiveTxt : TabInactiveTxt;
        var ico = NewChild("ico", tab);
        var icoLe = ico.gameObject.AddComponent<LayoutElement>();
        icoLe.preferredWidth = 18f; icoLe.minWidth = 18f; icoLe.preferredHeight = 18f; icoLe.minHeight = 18f;
        BuildTabIcon(index, ico, icCol);

        var lbl = Txt(NewChild("lbl", tab), TabNames[index], 18f, FontStyles.Bold, icCol, TextAlignmentOptions.Center);
        lbl.ForceMeshUpdate();   // 프리퍼드폭 즉시 확정(첫 오픈 탭 퍼짐 방지)

        // 안 본 새 항목이 있는 카테고리(비활성 탭)면 우상단 알림(!) 배지. 활성 탭은 열며 MarkSeen 되어 안 뜸.
        if (CodexNotice.IsUnseen(index)) AddTabNotice(tab);
    }

    // 탭 우상단 알림(!) 배지 - HUD K 아이콘과 같은 Alert_amber 사용. 레이아웃서 제외해 탭 크기 안 흔듦.
    private void AddTabNotice(RectTransform tab)
    {
        var badge = NewChild("notice", tab);
        var le = badge.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(36f, 36f);
        badge.anchoredPosition = new Vector2(-2f, 3f);   // 우상단 모서리에 크게 걸치게(잘 보이게)
        var sprite = Resources.Load<Sprite>("Image/UI_Icon/HUD/Alert_amber");
        var img = badge.gameObject.AddComponent<Image>();
        if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
        else img.color = new Color(0.96f, 0.62f, 0.12f, 1f);   // 절차 폴백(앰버 점)
        img.raycastTarget = false;
    }

    private void BuildTabIcon(int index, RectTransform parent, Color col)
    {
        if (index == 0)        Img(parent, UISpriteFactory.Ring(48, 4f), col).preserveAspect = true;   // 원
        else if (index == 1) { Img(parent, RoundedFallback(2), col); parent.localRotation = Quaternion.Euler(0f, 0f, 45f); parent.localScale = Vector3.one * 0.72f; } // 다이아
        else if (index == 2)   Img(parent, UISpriteFactory.Disc(48), col).preserveAspect = true;       // 육각(폴백 디스크)
        else                   Img(parent, RoundedFallback(4), col);                                    // 네모(아이템)
    }

    // ── 리스트 + 메인 재빌드 ───────────────────────────────────────
    private void Refresh()
    {
        CloseSourcePopup();   // 뷰 재생성 시 떠 있던 출처 팝업 정리(스크롤/탭/네비게이션 등)
        HideTooltip();
        bool itemTab = _tab == 3;
        // 아이템 탭도 다른 탭과 동일하게 좌측 리스트 + 우측 메인 레이아웃 사용(통일).
        _listBox.gameObject.SetActive(true);
        if (_vLine != null) _vLine.gameObject.SetActive(true);
        _mainBox.gameObject.SetActive(true);
        if (itemTab)
        {
            if (_portrait != null) _portrait.ClearInstance();   // 몬스터 프리뷰/영상 정리
            if (_video != null) _video.Stop();
            BuildItemCatList();   // 좌측 = 7분류 리스트
            BuildItemMain();      // 우측 = 선택 분류의 아이템 그리드
        }
        else
        {
            BuildList();
            BuildMain();
        }
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
        Txt(label, "목록 - LIST", 14f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);

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
            Img(row, RoundedFallback(10), new Color32(95, 196, 255, 74));   // 선택 = 뚜렷한 강조(톤다운 패널서도 또렷하게 알파 보강)
            Line(row, new Color32(95, 196, 255, 130));
            var bar = Make("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(4f, -8f));
            Img(bar, RoundedFallback(3), Accent);
        }
        else
        {
            // 비선택 헤더도 기본 카드로 어느정도 강조(엔필처럼). 미발견은 더 흐리게.
            byte a = e.state == St.Public ? (byte)46 : (byte)24;   // 톤다운 패널서 카드 묻히지 않게 알파 보강
            Img(row, RoundedFallback(10), new Color32(120, 140, 160, a));
            if (e.state == St.Public) Line(row, new Color32(120, 140, 160, 42));
        }

        if (e.state == St.Public) Btn(row, () => { _sel = index; _highlightOutputItemId = 0; Refresh(); });

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

    // ── 아이템 도감 (4번째 탭) ──────────────────────────────────────
    // 전체폭: 상단 7분류 헤더(창고와 동일) + 아래 아이템 그리드. 미획득=어두운 실루엣, 획득=풀+클릭 상세(출처팝업).
    static readonly string[] ItemCatNames = { "전체", "원재료", "1차 가공품", "2차 가공품", "전술 소모품", "핵심 강화", "특수" };
    static readonly ItemCategory?[] ItemCatMap =
    {
        null, ItemCategory.RawMaterial, ItemCategory.ProcessedTier1, ItemCategory.ProcessedTier2,
        ItemCategory.TacticalConsumable, ItemCategory.CoreUpgrade, ItemCategory.Special
    };

    // 좌측 = 7분류 리스트(설비/몬스터 탭의 목록과 같은 좌우 레이아웃). 선택 시 우측 그리드 갱신.
    private void BuildItemCatList()
    {
        ClearChildren(_listBox);

        var label = Make("Label", _listBox, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -28f), new Vector2(-14f, -4f));
        Txt(label, "분류 - CATEGORY", 14f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);

        var viewport = Make("Viewport", _listBox, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(-12f, -32f));
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var rows = Make("Rows", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rows.pivot = new Vector2(0.5f, 1f);
        var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f; vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < ItemCatNames.Length; i++) BuildItemCatRow(rows, i);

        var scroll = _listBox.gameObject.GetComponent<ScrollRect>();
        if (scroll == null) scroll = _listBox.gameObject.AddComponent<ScrollRect>();
        scroll.content = rows; scroll.viewport = viewport;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
    }

    // 분류 한 줄(설비/몬스터 목록 행과 같은 카드 룩). 전 분류 항상 선택 가능.
    private void BuildItemCatRow(RectTransform parent, int index)
    {
        bool selected = index == _itemCatIndex;
        var row = NewChild("cat_" + index, parent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 76f; le.preferredHeight = 76f;

        if (selected)
        {
            Img(row, RoundedFallback(10), new Color32(95, 196, 255, 74));
            Line(row, new Color32(95, 196, 255, 130));
            var bar = Make("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 8f), new Vector2(4f, -8f));
            Img(bar, RoundedFallback(3), Accent);
        }
        else
        {
            Img(row, RoundedFallback(10), new Color32(120, 140, 160, 46));
            Line(row, new Color32(120, 140, 160, 42));
        }

        int idx = index;
        Btn(row, () => { _itemCatIndex = idx; Refresh(); });

        var nameRt = Make("Name", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 0f), new Vector2(-12f, 0f));
        Txt(nameRt, ItemCatNames[index], 20f, selected ? FontStyles.Bold : FontStyles.Normal, selected ? TxtMain : C32(60, 68, 76), TextAlignmentOptions.Left);
        if (!selected) Hatch(row);
    }

    // 우측 메인 = 선택 분류의 아이템 그리드(미획득=어두운 실루엣, 획득=풀+클릭 상세).
    private void BuildItemMain()
    {
        ClearChildren(_mainBox);
        string catName = ItemCatNames[Mathf.Clamp(_itemCatIndex, 0, ItemCatNames.Length - 1)];
        var body = MainHeader("아이템 - ITEM", catName, TxtMain, true);

        // 그리드 영역(스크롤)
        var vp = Make("ItemViewport", body, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true;
        vp.gameObject.AddComponent<RectMask2D>();
        _itemGrid = Make("ItemGrid", vp, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        _itemGrid.pivot = new Vector2(0.5f, 1f);
        var grid = _itemGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(118f, 146f); grid.spacing = new Vector2(12f, 12f);
        grid.padding = new RectOffset(2, 12, 4, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft; grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft; grid.constraint = GridLayoutGroup.Constraint.Flexible;
        _itemGrid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.content = _itemGrid; scroll.viewport = vp;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36f;

        // 카드(분류 필터 적용)
        var cat = ItemCatMap[Mathf.Clamp(_itemCatIndex, 0, ItemCatMap.Length - 1)];
        int shown = 0;
        foreach (var it in GameDataHolder.I.ItemData.All)
        {
            if (it == null) continue;
            if (cat != null && it.itemCategory != cat.Value) continue;
            BuildItemCard(_itemGrid, it);
            shown++;
        }
        if (shown == 0)
            Txt(NewChild("none", _itemGrid), "표시할 아이템이 없습니다.", 14f, FontStyles.Normal, TxtDim, TextAlignmentOptions.Left);
    }

    private void BuildItemCard(RectTransform parent, ItemDataSheetData it)
    {
        int id = ItemIdOf(it);
        bool got = _devUnlockAll || ItemDiscovery.IsObtained(id);
        var card = NewChild("item_" + id, parent);

        // 아이콘 슬롯(위)
        var slot = Make("slot", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(0f, 0f));
        SlotBg(slot);
        if (got) GradeAurora(slot, (int)it.itemGrade, 26f);
        var iconRt = Make("ic", slot, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        Sprite isp = ItemDatabase.GetIcon(it.iconKey);
        if (isp != null)
        {
            // 미획득 = 어두운 실루엣(공장 결과물 슬롯의 흐림 느낌 차용, 더 어둡게).
            var im = Img(iconRt, isp, got ? Color.white : new Color(0.12f, 0.14f, 0.18f, 0.55f));
            im.preserveAspect = true; im.raycastTarget = false;
        }
        else if (!got)
            Txt(iconRt, "?", 30f, FontStyles.Bold, new Color(0.4f, 0.45f, 0.5f, 0.5f), TextAlignmentOptions.Center);

        // 이름(아래) — 미획득은 ???
        var nameRt = Make("nm", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(0f, 28f));
        Txt(nameRt, got ? it.itemName : "???", 14f, got ? FontStyles.Bold : FontStyles.Normal, got ? TxtMain : TxtDim, TextAlignmentOptions.Center);

        // 획득품만 클릭 -> 상세(출처 팝업 재사용: 이름/등급/획득처)
        if (got) HoverButton(slot, () => ShowSourcePopup(id));
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
        return null;   // 공개(Public) 엔트리가 없으면 선택 없음(미발견 데이터 누출 방지)
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
            // 실제 시청: 인게임서 그 영상 봤으면(CodexDiscovery) Public, 아니면 Hidden(미시청).
            if (_devUnlockAll || (p != null && CodexDiscovery.IsTutorialWatched(p.title))) { e.state = St.Public; e.status = null; }
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
            // 실제 해금(FacilityUnlockManager) 또는 개발자 F9. 미해금이면 Silhouette("미해금") -> 선택 불가 = 레시피 가려짐.
            if (FacilityUnlocked(id)) { e.state = St.Public; e.status = null; }
            list.Add(e);
        }
        return list.Count > 0 ? list.ToArray() : CloneDev(Facilities);
    }

    // 설비 해금 여부. 도감은 정지 UI라 열려있는 동안 해금이 안 일어나니 열 때마다 평가로 충분(라이브 구독 불필요).
    private bool FacilityUnlocked(int id)
        => _devUnlockAll || (FacilityUnlockManager.Instance != null && FacilityUnlockManager.Instance.IsUnlocked(id));

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
        // 리스트 길이를 config(CodexPreviewConfig) 개수 기준으로. 그래야 placeholder 배열(8개)
        // 너머에 들어간 Wyvern/보스/엘리트 엔트리도 전부 뜬다. config 없으면 placeholder로 폴백.
        bool hasCfg = _cfg != null && _cfg.monsters != null && _cfg.monsters.Count > 0;
        int count = hasCfg ? _cfg.monsters.Count : Monsters.Length;
        var list = new Entry[count];
        for (int i = 0; i < count; i++)
        {
            // placeholder 이름은 인덱스가 placeholder 범위 안일 때만 기본값으로
            string baseName = i < Monsters.Length ? Monsters[i].name : null;
            var e = new Entry { name = baseName, state = St.Hidden, status = "미발견" };
            if (hasCfg && i < _cfg.monsters.Count && _cfg.monsters[i] != null)
            {
                var c = _cfg.monsters[i];
                if (!string.IsNullOrEmpty(c.displayName)) e.name = c.displayName;
                if (c.prefab != null)
                {
                    e.preview = c;
                    e.monsterSourceId = c.prefab.GetComponentInChildren<EnemyDropOnDeath>(true)?.SourceId;
                }
            }
            // 실제 발견: 처치(CodexDiscovery)했으면 Public, 아니면 Hidden(???). config RevealState(mock)는 무시.
            if (_devUnlockAll || CodexDiscovery.IsMonsterDiscovered(e.monsterSourceId)) { e.state = St.Public; e.status = null; }
            if (e.state == St.Public && string.IsNullOrEmpty(e.name)) e.name = "몬스터 " + (i + 1);
            list[i] = e;
        }
        return SortMonsters(list);
    }

    // 도감 몬스터 정렬: 베이스+엘리트 묶음(베이스 먼저), 늑대인간/와이번은 맨 뒤(와이번 최후미).
    // sourceId 기준 - 엘리트는 "..._Elite", 묶음 키(family)는 _Elite 떼낸 베이스 id.
    private static Entry[] SortMonsters(Entry[] src)
    {
        var famFirst = new Dictionary<string, int>();
        for (int i = 0; i < src.Length; i++)
        {
            string fam = MonsterFamily(src[i]);
            if (!famFirst.ContainsKey(fam)) famFirst[fam] = i;
        }
        var order = new int[src.Length];
        for (int i = 0; i < src.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) =>
        {
            long ka = MonsterSortKey(src[a], famFirst), kb = MonsterSortKey(src[b], famFirst);
            if (ka != kb) return ka.CompareTo(kb);
            return a.CompareTo(b);   // 동률은 원래 순서 유지(안정)
        });
        var outp = new Entry[src.Length];
        for (int i = 0; i < src.Length; i++) outp[i] = src[order[i]];
        return outp;
    }

    private static string MonsterFamily(Entry e)
    {
        string sid = e != null && e.monsterSourceId != null ? e.monsterSourceId : "";
        return sid.ToLowerInvariant().EndsWith("_elite") ? sid.Substring(0, sid.Length - 6) : sid;
    }

    private static long MonsterSortKey(Entry e, Dictionary<string, int> famFirst)
    {
        string sid = e != null && e.monsterSourceId != null ? e.monsterSourceId : "";
        string low = sid.ToLowerInvariant();
        bool isElite = low.EndsWith("_elite");
        string fam = isElite ? sid.Substring(0, sid.Length - 6) : sid;
        int endRank = low.Contains("wyvern") ? 2 : low.Contains("werewolf") ? 1 : 0;  // 와이번 최후미, 늑대인간 그 앞
        int famOrder = famFirst.TryGetValue(fam, out int fo) ? fo : 9999;
        int eliteRank = isElite ? 1 : 0;   // 같은 묶음서 베이스 먼저, 엘리트 뒤
        return (long)endRank * 1000000L + (long)famOrder * 100L + eliteRank;
    }

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
        Txt(Make("l", hdr, new Vector2(0f, 0f), new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero), monoLabel, 14f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);
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

    // 처치 수 기반 단계 공개 임계값 (실제 정의는 CodexDiscovery - 킬수 소유자, 드리프트 방지).
    const int KillsForStats = CodexDiscovery.KillsForStats;        // 스탯 공개
    const int KillsForDropRate = CodexDiscovery.KillsForDropRate;  // 드롭 확률 공개 (= 풀 해금, X/10 카운터 기준)

    // 이 몬스터 처치 수(개발자 전부해금이면 풀로 간주).
    private int MonsterKillCount(Entry e)
        => _devUnlockAll ? KillsForDropRate : CodexDiscovery.MonsterKills(e?.monsterSourceId);

    // 이름 옆 처치 진행도 배지 ("처치 X / 10").
    private void AddKillBadge(RectTransform row, int kills)
    {
        bool full = kills >= KillsForDropRate;
        // 풀해금(10) 전엔 진행도 "X / 10", 이후엔 누적 처치수만("처치 137" 같은 재미요소)
        string label = full ? ("처치 " + kills) : ("처치 " + kills + " / " + KillsForDropRate);
        var pill = NewChild("kc", row);
        var le = pill.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 128f; le.minWidth = 128f; le.preferredHeight = 28f; le.minHeight = 28f;
        var c = full ? Accent : C32(120, 140, 160);
        Img(pill, RoundedFallback(8), new Color(c.r, c.g, c.b, 0.16f));
        Txt(pill, label, 14f, FontStyles.Bold, full ? C32(40, 110, 150) : C32(92, 100, 112), TextAlignmentOptions.Center);
    }

    private void BuildMonsterMain(string selName)
    {
        var selE = SelectedEntry();
        var data = MonsterData(selE);   // 프리팹의 MeleeEnemyData (실제 스탯)
        string srcId = selE != null ? selE.monsterSourceId : null;
        int kills = MonsterKillCount(selE);
        // 자동 공개가 아니라 "도감서 직접 활성화". 조건(킬수) 충족하면 빨강 버튼 -> 클릭하면 공개.
        bool statsOn = _devUnlockAll || CodexDiscovery.IsStatsActivated(srcId);
        bool ratesOn = _devUnlockAll || CodexDiscovery.IsRatesActivated(srcId);

        int tier = ThreatTier(data);
        Color tierCol = ThreatColor(tier);
        var thr = Make("ThreatBadge", _mainBox, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-184f, -34f), new Vector2(-16f, -12f));
        var body = MainHeader("몬스터 - THREAT", "", C32(154, 162, 170), false);
        BuildThreatBadge(thr, tier);

        var face = Make("Face", body, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        face.sizeDelta = new Vector2(176f, 176f); face.anchoredPosition = new Vector2(88f, -106f);   // 큼직하게(가독성)
        Img(face, RoundedFallback(10), C32(208, 218, 230));   // 렌더 배경과 동일 톤(밝은 쿨 글래스)
        // 위험도 등급 색 테두리 - 엘리트/보스는 크기 외 식별점 없으니 테두리 '색'으로 구분.
        // 두께는 전 몬스터 통일(중간값). 위험도 표현은 색만, 두께 차등 없음.
        const float bw = 4f;
        Line(face, new Color(tierCol.r, tierCol.g, tierCol.b, 1f), bw);
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
        var nameRow = Make("nmrow", statArea, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(0f, 0f));
        var nhlg = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        nhlg.spacing = 12f; nhlg.childAlignment = TextAnchor.LowerLeft;
        nhlg.childControlWidth = true; nhlg.childControlHeight = true;
        nhlg.childForceExpandWidth = false; nhlg.childForceExpandHeight = false;
        Txt(NewChild("nm", nameRow), selName, 28f, FontStyles.Bold, TxtMain, TextAlignmentOptions.BottomLeft);
        AddKillBadge(nameRow, kills);
        if (statsOn)
        {
            var statRow = Make("StatRow", statArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -50f));
            var hlg = statRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            // 실제 스탯(MeleeEnemyData). 값 없으면 "?".
            string atkSpd = data != null && data.attackCooldown > 0.01f ? (1f / data.attackCooldown).ToString("0.0") : "?";
            AddStat(statRow, "체력", data != null ? Mathf.RoundToInt(data.maxHP).ToString("N0") : "?", "");
            AddStat(statRow, "공격력", data != null ? Mathf.RoundToInt(data.attackDamage).ToString() : "?", "");
            AddStat(statRow, "공격속도", atkSpd, "/s");
            AddStat(statRow, "이동속도", data != null ? data.moveSpeed.ToString("0.0") : "?", "m/s");
            AddStat(statRow, "사거리", data != null ? data.attackRange.ToString("0.0") : "?", "m");
            AddStat(statRow, "시야", data != null ? Mathf.RoundToInt(data.visionRange).ToString() : "?", "m");
        }
        else if (kills >= KillsForStats)
        {
            // 5마리 처치 완료 -> 빨강 [스탯 활성화] 버튼(클릭해 직접 해금)
            var pill = Make("stact", statArea, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            pill.sizeDelta = new Vector2(240f, 46f); pill.anchoredPosition = new Vector2(120f, -74f);
            RedActivateButton(pill, "스탯 활성화 (" + KillsForStats + "마리 처치 완료)",
                () => { CodexDiscovery.ActivateStats(srcId); ToastManager.Success("스탯 공개"); Refresh(); });
        }
        else
        {
            // 5마리 미만 -> 잠금 + 진행도
            Txt(Make("stlock", statArea, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -84f), new Vector2(0f, -52f)),
                "스탯 잠김 - " + KillsForStats + "마리 처치 시 활성화  (" + Mathf.Min(kills, KillsForStats) + " / " + KillsForStats + ")",
                17f, FontStyles.Bold, C32(150, 158, 168), TextAlignmentOptions.Left);
        }

        var line = Make("MidLine", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -204f), new Vector2(0f, -203f));
        Img(line, null, Divider);

        Txt(Make("dl", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -236f), new Vector2(0f, -212f)), "드랍 아이템 - DROPS", 14f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);
        if (!ratesOn)
        {
            if (kills >= KillsForDropRate)
            {
                // 10마리 처치 완료 -> 빨강 [드롭 확률 활성화]
                var pill = Make("ratact", body, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                pill.sizeDelta = new Vector2(196f, 40f); pill.anchoredPosition = new Vector2(282f, -225f);
                RedActivateButton(pill, "드롭 확률 활성화",
                    () => { CodexDiscovery.ActivateRates(srcId); ToastManager.Success("드롭 확률 공개"); Refresh(); });
            }
            else
                Txt(Make("drlock", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(178f, -237f), new Vector2(0f, -211f)),
                    "확률 잠김 - " + KillsForDropRate + "마리 처치 시 활성화  (" + Mathf.Min(kills, KillsForDropRate) + " / " + KillsForDropRate + ")",
                    14f, FontStyles.Bold, C32(108, 120, 136), TextAlignmentOptions.Left);
        }
        var grid = Make("DropGrid", body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -244f));
        var glg = grid.gameObject.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(540f, 104f);   // 한 줄에 2개, 큼직하게
        glg.spacing = new Vector2(22f, 16f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        // 실제 드롭 + 확률(가중치 비율, 처음부터 표시). 흔한 것부터.
        var drops = BuildMonsterDrops(selE);
        if (drops.Count > 0)
            foreach (var d in drops) BuildDropCard(grid, d, ratesOn);
        else
            Txt(Make("nd", body, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, -244f)), "드롭 정보가 없습니다.", 14f, FontStyles.Normal, TxtDim, TextAlignmentOptions.TopLeft);
    }

    // 프리팹 -> MeleeEnemyData(스탯)
    private static MeleeEnemyData MonsterData(Entry e)
    {
        var pf = e?.preview != null ? e.preview.prefab : null;
        if (pf == null) return null;
        var brain = pf.GetComponentInChildren<EnemyBrain>(true);
        if (brain != null && brain.Data != null) return brain.Data;
        var boss = pf.GetComponentInChildren<WyvernBossController>(true);   // 보스는 EnemyBrain 없이 전용 컨트롤러
        return boss != null ? boss.Data : null;
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

        rows.Sort((a, b) => b.chance.CompareTo(a.chance));
        int totalW = 0;
        foreach (var r in rows) totalW += Mathf.Max(0, r.chance);

        foreach (var r in rows)
        {
            var item = GameDataUtility.GetItem(r.itemId);
            if (item == null) continue;
            int gi = (int)item.itemGrade;
            float pct = totalW > 0 ? r.chance * 100f / totalW : 0f;   // 한 종 뽑기(가중치) -> 정규화 %
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

    // 위험도: 여러 스탯 가중합 -> 점수 -> 1~5 등급. 일반/엘리트/보스가 자연스럽게 분리되도록 임계 설정.
    // (일반 HP50~200/공15, 엘리트 HP x2.5/공x1.5, 보스 HP800/공30 기준. 임계는 조절 가능.)
    private static int ThreatTier(MeleeEnemyData d)
    {
        if (d == null) return 1;
        // 실제 스탯 기반: 체력(생존) + 초당데미지(화력) + 이동속도(추격). 실측 SO 값으로 임계 보정.
        // 일반 HP50~200/공15(쿨1.5,일부0.5) / 엘리트 HP x2.5,공22 / 보스 HP800,공30,쿨2.5.
        float dps = d.attackCooldown > 0.01f ? d.attackDamage / d.attackCooldown : d.attackDamage;
        float score = d.maxHP + dps * 5f + d.moveSpeed * 8f;
        if (score >= 500f) return 5;   // 치명: 보스(~888)
        if (score >= 330f) return 4;   // 위험: 강한 엘리트(EvilWatcher/Skeleton/FantasyWolf 정예)
        if (score >= 240f) return 3;   // 주의: 약한 엘리트 / 강한 일반(Werewolf, FantasyWolf)
        if (score >= 150f) return 2;   // 보통: 일반 잡몹
        return 1;                       // 낮음: 약체(OakTree/Mummy/Undead)
    }

    private static readonly string[] ThreatLabels = { "낮음", "보통", "주의", "위험", "치명" };

    // 위험도 등급색(액자 테두리/뱃지 공용). 1=연두 -> 5=빨강.
    private static Color ThreatColor(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 5))
        {
            case 5:  return new Color32(236, 40, 40, 255);    // 강렬한 빨강(치명/최종보스)
            case 4:  return new Color32(240, 138, 40, 255);   // 주황(위험)
            case 3:  return new Color32(236, 198, 46, 255);   // 노랑(주의)
            case 2:  return new Color32(150, 200, 70, 255);   // 라임(보통)
            default: return new Color32(92, 182, 120, 255);   // 초록(낮음)
        }
    }

    // 위험도 표시: "위험도" 라벨 + 뚫린 칸 5개(테두리). 등급만큼 등급색으로 채움(치명=5칸, 낮음=1칸).
    // 명시적 좌표 배치(HorizontalLayoutGroup이 한 덩어리로 뭉치던 버그 회피).
    private void BuildThreatBadge(RectTransform parent, int tier)
    {
        tier = Mathf.Clamp(tier, 1, 5);

        var lbl = Make("tl", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(52f, 0f));
        Txt(lbl, "위험도", 12f, FontStyles.Bold, C32(120, 130, 142), TextAlignmentOptions.Right);

        // 칸은 전 몬스터 동일 색(통일). 위험도 '단계'는 채운 칸 수로만, '색' 구분은 액자 테두리가 담당.
        Color fillCol = Accent;
        const float cw = 16f, gap = 4f, x0 = 60f;
        for (int i = 0; i < 5; i++)
        {
            var cell = Make("c" + i, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            cell.sizeDelta = new Vector2(cw, 16f);
            cell.anchoredPosition = new Vector2(x0 + i * (cw + gap) + cw * 0.5f, 0f);
            bool fill = i < tier;
            Img(cell, RoundedFallback(3), fill ? fillCol : new Color(fillCol.r, fillCol.g, fillCol.b, 0.06f));
            Line(cell, new Color32(120, 140, 160, (byte)(fill ? 205 : 90)), 1.5f);
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

    private void BuildDropCard(RectTransform parent, Drop d, bool showRate)
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
        Txt(pct, showRate ? d.pct : "??%", showRate ? 26f : 24f, FontStyles.Bold, showRate ? C32(60, 68, 76) : C32(120, 128, 140), TextAlignmentOptions.Right);
    }

    private void BuildFacilityMain(string selName)
    {
        var body = MainHeader("설비 - FACILITY", selName, TxtMain, true);
        Txt(Make("rl", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, -6f)), "제작 가능 레시피 - RECIPES", 14f, FontStyles.Normal, TxtSub, TextAlignmentOptions.Left);
        // 재료가 클릭 가능하다는 상시 힌트(호버 발광/툴팁과 함께 발견성 확보). RECIPES 라벨 바로 옆.
        Txt(Make("rlhint", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(212f, -30f), new Vector2(0f, -6f)), "·  재료를 누르면 출처 보기", 14f, FontStyles.Bold, Accent, TextAlignmentOptions.Left);
        // 잭팟 설명(우측). 마스터한 레시피의 보너스 = 직관적 안내.
        Txt(Make("jl", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(-12f, -6f)),
            "레시피 10회 제작 후 활성화 시 -> 제작마다 10% 확률로 2배 생산!", 13f, FontStyles.Normal, C32(165, 130, 60), TextAlignmentOptions.Right);

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
        // 해금된(Public) 설비만 레시피 공개. 전부 미해금이면 폴백이 잠긴 설비를 줄 수 있어 가드(레시피 누출 방지).
        var recipes = (selE != null && selE.state == St.Public && selE.facilityId > 0) ? GameDataUtility.GetRecipesByFacilityId(selE.facilityId) : null;
        if (recipes != null && recipes.Count > 0)
        {
            // 설비 내 최대 재료수 -> 재료 영역 폭을 행마다 동일하게(= 와 결과물 열 정렬)
            int maxIn = 1;
            foreach (var r in recipes) maxIn = Mathf.Max(maxIn, GameDataUtility.GetRecipeInputs(r.SheetId).Count);
            string targetIdStr = _highlightOutputItemId > 0 ? _highlightOutputItemId.ToString() : null;
            int hlIndex = -1;
            for (int k = 0; k < recipes.Count; k++)
            {
                if (targetIdStr != null && (string)recipes[k].outputItemId == targetIdStr) hlIndex = k;
                BuildRecipeRowReal(rows, recipes[k], maxIn);
            }
            BuildRewardBox(rows, selE.facilityId);   // 맨 아래 보상 박스(전 레시피 마스터 시 코어키트)
            if (hlIndex >= 0)   // 강조 레시피가 스크롤 밖이면 보이게 맞춤(86px 균일행 기준)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
                float maxY = Mathf.Max(0f, rows.rect.height - vp.rect.height);
                var ap = rows.anchoredPosition; ap.y = Mathf.Clamp(hlIndex * 86f - 16f, 0f, maxY); rows.anchoredPosition = ap;
            }
        }
        else
            Txt(NewChild("none", rows), "표시할 레시피가 없습니다.", 14f, FontStyles.Normal, TxtDim, TextAlignmentOptions.Left);
    }

    // 레시피 제작수/마스터 (F9 전부해금이면 마스터로 간주 - 테스트용)
    private int RecipeCrafts(string recipeId)
        => _devUnlockAll ? RecipeProgress.CraftsToMaster : RecipeProgress.Crafts(recipeId);
    private bool RecipeMastered(string recipeId)
        => _devUnlockAll || RecipeProgress.IsMastered(recipeId);

    // 조건 달성 시 뜨는 "빨강 활성화 버튼"을 host(미리 배치된 RectTransform)에 만든다.
    private void RedActivateButton(RectTransform host, string label, UnityEngine.Events.UnityAction onClick)
    {
        Img(host, RoundedFallback(9), C32(224, 96, 96));
        Txt(host, label, 15f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Btn(host, onClick);
    }

    // 레시피 제작 진행도(행 우측). 회색 N/10 -> (마스터)빨강[잭팟 활성화] -> (클릭)초록 잭팟 ON.
    private void BuildRecipeProgress(RectTransform row, string recipeId)
    {
        int crafts = RecipeCrafts(recipeId);
        // 잭팟은 보상느낌 -> F9(전부해금)여도 자동 ON 안 함. 마스터(10회/ F9) 후 직접 클릭해야 활성.
        bool jackpotOn = RecipeProgress.IsJackpotActive(recipeId);
        var box = NewChild("prog", row);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 172f; le.minWidth = 172f; le.preferredHeight = 56f;

        if (jackpotOn)
            Txt(box, crafts + " / " + RecipeProgress.CraftsToMaster + "  <color=#3FB46B>잭팟 ON</color>", 15f, FontStyles.Bold, C32(60, 68, 76), TextAlignmentOptions.Right);
        else if (crafts >= RecipeProgress.CraftsToMaster)
        {
            var pill = Make("jp", box, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            pill.pivot = new Vector2(1f, 0.5f);   // 우측 정렬 -> 박스 안에 들어와 안 잘림(기본 0.5피벗이면 오른쪽 절반이 밖으로 삐져 클리핑)
            pill.sizeDelta = new Vector2(138f, 40f); pill.anchoredPosition = new Vector2(-6f, 0f);
            string rid = recipeId;
            RedActivateButton(pill, "잭팟 활성화", () => { RecipeProgress.ActivateJackpot(rid); ToastManager.Success("잭팟 활성화! 제작마다 10% 확률로 2배 생산"); Refresh(); });
        }
        else
            Txt(box, Mathf.Min(crafts, RecipeProgress.CraftsToMaster) + " / " + RecipeProgress.CraftsToMaster, 16f, FontStyles.Bold, C32(120, 128, 140), TextAlignmentOptions.Right);
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

        // 출처점프로 도착한 "그 결과물을 만드는 레시피"면 지속 강조(화면 전환 전까지 유지, 반짝 아님).
        if (_highlightOutputItemId > 0 && (string)r.outputItemId == _highlightOutputItemId.ToString())
        {
            var hl = Make("rowhl", row, Vector2.zero, Vector2.one, new Vector2(1f, 2f), new Vector2(-1f, -1f));
            hl.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 배경 강조는 레이아웃서 제외
            Img(hl, RoundedFallback(8), new Color32(95, 196, 255, 56)).raycastTarget = false;
            Line(hl, new Color32(95, 196, 255, 150));
            var bar = Make("hlbar", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 5f), new Vector2(6f, -3f));
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Img(bar, RoundedFallback(2), Accent).raycastTarget = false;
        }

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

        // 우측으로 밀어내는 스페이서 + 제작 진행도(N/10, 마스터면 잭팟)
        var spacer = NewChild("sp", row);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        BuildRecipeProgress(row, (string)r.SheetId);
    }

    // 이 설비의 (마스터한 레시피 수, 전체 레시피 수)
    private (int mastered, int total) FacilityRecipeMastery(int facilityId)
    {
        var recipes = GameDataUtility.GetRecipesByFacilityId(facilityId);
        int total = recipes != null ? recipes.Count : 0, m = 0;
        if (recipes != null)
            foreach (var r in recipes) if (RecipeMastered((string)r.SheetId)) m++;
        return (m, total);
    }

    // 보상 지급: 가방 -> 창고 폴백(퀘스트 보상과 동일). 전부 들어가면 true.
    private bool GrantReward(int itemId, int count)
    {
        if (itemId <= 0 || count <= 0) return true;
        int leftover = count;
        if (InventoryManager.Instance != null)
            leftover = InventoryManager.Instance.TryAddItemFromLoot(itemId, leftover);
        int afterBag = leftover;
        if (leftover > 0 && InventoryManager.StorageInstance != null)
            leftover = InventoryManager.StorageInstance.TryAddItemFromLoot(itemId, leftover);
        if (afterBag > 0 && leftover < afterBag) ToastManager.Info("인벤토리가 가득 차 창고로 이동했습니다");
        if (leftover > 0) { ToastManager.Error("보상을 받지 못했습니다. 인벤토리를 비워주세요"); return false; }
        return true;
    }

    private void ClaimFacilityReward(int facilityId, int itemId, int count)
    {
        if (RecipeProgress.IsFacilityRewardClaimed(facilityId)) return;
        var (m, total) = FacilityRecipeMastery(facilityId);
        if (!(total > 0 && m >= total)) return;   // 안전: 100% 아니면 무시
        if (GrantReward(itemId, count))
        {
            RecipeProgress.MarkFacilityRewardClaimed(facilityId);
            ToastManager.Success("보상 수령 - 코어 키트 " + count + "개");
            Refresh();
        }
    }

    // 설비별 보상 박스(레시피 목록 맨 아래). 100% 마스터 전엔 회색/미달성, 100%면 수령 버튼.
    private void BuildRewardBox(RectTransform parent, int facilityId)
    {
        int rewardCount = _rewardCfg != null ? _rewardCfg.RewardCountFor(facilityId) : 0;
        if (rewardCount <= 0) return;   // 보상 미설정 설비는 박스 안 띄움
        int rewardItemId = _rewardCfg != null ? _rewardCfg.rewardItemId : 6101;
        var (mastered, total) = FacilityRecipeMastery(facilityId);
        bool complete = total > 0 && mastered >= total;
        bool claimed = RecipeProgress.IsFacilityRewardClaimed(facilityId);
        bool active = complete && !claimed;

        var box = NewChild("rewardbox", parent);
        var le = box.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 124f; le.preferredHeight = 124f;
        var pad = Make("rbpad", box, Vector2.zero, Vector2.one, new Vector2(2f, 16f), new Vector2(-2f, -8f));   // 위 여백(레시피와 분리)
        Color tint = claimed ? new Color(0.31f, 0.70f, 0.43f, 0.12f) : (active ? new Color(0.88f, 0.38f, 0.38f, 0.12f) : CardBg);
        Color edge = claimed ? new Color32(80, 180, 110, 120) : (active ? new Color32(224, 96, 96, 150) : CardBd);
        Img(pad, RoundedFallback(12), tint);
        Line(pad, edge);

        // 보상 아이템 슬롯(왼쪽, 세로 가운데)
        var item = GameDataUtility.GetItem(rewardItemId);
        var slot = Make("rbslot", pad, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        slot.sizeDelta = new Vector2(66f, 66f); slot.anchoredPosition = new Vector2(54f, 0f);
        SlotBg(slot);
        if (item != null) GradeAurora(slot, (int)item.itemGrade, 30f);
        Sprite isp = item != null ? ItemDatabase.GetIcon(item.iconKey) : null;
        if (isp != null) { var ic = Make("ic", slot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f)); var im = Img(ic, isp, Color.white); im.preserveAspect = true; im.raycastTarget = false; }

        // 텍스트 컬럼(슬롯 오른쪽 ~ 버튼 왼쪽). 캡션/이름/진행도를 세로로 분리해 안 겹치게.
        Txt(Make("rblab", pad, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(100f, -28f), new Vector2(-196f, -8f)),
            "전 레시피 마스터 보상", 12f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
        string iname = item != null ? item.itemName : ("아이템 " + rewardItemId);
        Txt(Make("rbnm", pad, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(100f, -66f), new Vector2(-196f, -32f)),
            iname + "  x" + rewardCount, 21f, FontStyles.Bold, complete ? TxtMain : TxtSub, TextAlignmentOptions.Left);
        Txt(Make("rbpg", pad, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(100f, -94f), new Vector2(-196f, -70f)),
            "레시피 " + mastered + " / " + total + " 마스터", 14f, FontStyles.Bold, complete ? C32(40, 110, 150) : C32(124, 132, 142), TextAlignmentOptions.Left);

        // 수령 버튼
        var btn = Make("rbbtn", pad, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        btn.sizeDelta = new Vector2(158f, 54f); btn.anchoredPosition = new Vector2(-96f, 0f);
        if (claimed)
        {
            Img(btn, RoundedFallback(9), new Color(0.31f, 0.70f, 0.43f, 0.20f));
            Txt(btn, "수령 완료", 16f, FontStyles.Bold, C32(60, 160, 95), TextAlignmentOptions.Center);
        }
        else if (complete)
        {
            int fid = facilityId, iid = rewardItemId, cnt = rewardCount;
            RedActivateButton(btn, "보상 수령", () => ClaimFacilityReward(fid, iid, cnt));
        }
        else
        {
            Img(btn, RoundedFallback(9), new Color(0.5f, 0.55f, 0.6f, 0.14f));
            Txt(btn, "미달성", 16f, FontStyles.Bold, C32(150, 156, 164), TextAlignmentOptions.Center);
        }
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

        // [효과] 소모품이면 효과 한 줄(ConsumableEffect 데이터). 재료 등 효과 없는 아이템은 생략.
        string effectDesc = ConsumableEffectApplier.Describe(itemId.ToString());
        if (!string.IsNullOrEmpty(effectDesc))
        {
            Txt(NewChild("efflbl", card), "효과", 13f, FontStyles.Normal, TxtMono, TextAlignmentOptions.Left);
            var effRow = NewChild("eff", card);
            effRow.gameObject.AddComponent<LayoutElement>().minHeight = 28f;
            Txt(effRow, effectDesc, 16f, FontStyles.Bold, C32(56, 140, 92), TextAlignmentOptions.Left);
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
            if (FacilityUnlocked(fid))
            {
                var fac = GameDataUtility.GetFacility(fid);
                string fname = fac != null ? fac.facilityName : ("설비 " + fid);
                AddSourceRow(card, "제작", Accent, fname + " 에서 제작", "이동", () => JumpToFacility(fid, itemId));
            }
            else   // 미해금 설비는 스포 방지 -> ???
            {
                AddSourceRow(card, "제작", C32(150, 156, 164), "??? (미해금 설비)", null, null);
            }
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

    private void JumpToFacility(int facilityId, int outputItemId)
    {
        CloseSourcePopup();
        var list = _facilities ?? Facilities;
        for (int i = 0; i < list.Length; i++)
            if (list[i].facilityId == facilityId && list[i].state == St.Public)
            {
                _tab = 2; _sel = i;
                _highlightOutputItemId = outputItemId;   // 그 설비서 "이 아이템을 만드는 레시피"를 지속 강조
                RebuildTabs(); Refresh();
                return;
            }
    }

    private void JumpToMonster(int monsterIndex)
    {
        CloseSourcePopup();
        var list = _monsters ?? Monsters;
        if (monsterIndex < 0 || monsterIndex >= list.Length || list[monsterIndex].state != St.Public) return;
        _tab = 1; _sel = monsterIndex; _highlightOutputItemId = 0; RebuildTabs(); Refresh();
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

    static void Btn(RectTransform rt, UnityEngine.Events.UnityAction onClick, SfxId clickSfx = SfxId.CodexClick)
    {
        var img = rt.gameObject.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => GameSfx.Play(clickSfx));   // 기본 CodexClick(좌측 목록/활성화·보상수령). 상단 탭 위젯만 CodexTabClick 전달. 그리드 셀은 HoverButton서 별도.
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
        btn.onClick.AddListener(() => GameSfx.Play(SfxId.CodexClick));   // 이미지/항목 클릭음
        btn.onClick.AddListener(onClick);

        // 호버 툴팁("자세히 보기 / 좌클릭") - 마우스 올리면 셀 위에 표시
        var et = cell.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener(e => { ShowTooltip(cell); GameSfx.Play(SfxId.CodexHover); });   // 이미지/항목 호버음
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
