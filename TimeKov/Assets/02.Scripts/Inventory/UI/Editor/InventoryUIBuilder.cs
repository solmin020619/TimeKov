// =====================================================================
// InventoryUIBuilder.cs (Editor Only)
// Tools/TIMEKOV/인벤토리 UI 생성 (가방)
// HANDOFF.md (Assets/11.UI/Inventory UI) 수치 그대로 가방 패널을 새로 생성 +
// InventoryUIController 자동 배선. 코어 빌더와 동일 방식.
// 1단계: 구조/색/탭/그리드/헤더/하단바 (블러는 2단계, 지금은 반투명 틴트).
// 로직/팝업/드래그/슬롯프리팹은 재사용.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;

public static class InventoryUIBuilder
{
    const string SlotPrefabPath = "Assets/05.Prefabs/Inventory/InventorySlot.prefab";
    const string SprDir = "Assets/11.UI/Inventory UI/sprites/";

    // 카테고리 7 아이콘 (HANDOFF §10 순서 = 전체/원재료/1차/2차/전술/코어/특수)
    static readonly string[] CatIcons =
        { "cat_all", "cat_raw", "cat_primary", "cat_secondary", "cat_tactical", "cat_core", "cat_special" };

    // HANDOFF §3 색
    const string BlurCanvasName = "InventoryBlurCanvas";
    const float BagRightX = 360f;   // 가방을 화면 중앙에서 오른쪽으로 (엔드필드처럼). 패널/블러 같은 값으로 정렬
    static Color Panel    => RGBA(26, 32, 42, 0.40f);   // 본체 (쿨 뉴트럴 - 엔드필드처럼 깔끔. 진한 파랑 빼서 블러색이 살게)
    static Color BarBg    => RGBA(34, 40, 52, 0.55f);   // 헤더 바 (쿨 뉴트럴, 약간 더 불투명)
    static Color SlotTone => RGBA(30, 36, 46, 0.10f);   // 본문 톤
    static Color CatBtn   => RGBA(40, 60, 88, 0.32f);
    static Color Chrome   => RGBA(150, 178, 205, 0.26f);
    static Color ChromeHi => RGBA(170, 196, 222, 0.42f);
    static Color Cyan     => Hex("5fc4ff");
    static Color CyanHi   => Hex("aee3ff");
    // 클로드디자인 확정: 밝은 간유리 콘텐츠 밴드 + 어두운 베이스(테두리/푸터) + 어두운 슬롯 셀 + 무채색 슬레이트 글자.
    // ("라이트 washes out"은 사실 InventoryRoot active 버그였고, 고쳐진 지금은 라이트가 정상. 컬러는 아이템/등급바에만.)
    static Color TxtMain  => Hex("242a31");                 // 어두운 슬레이트 (밝은 밴드 위)
    static Color TxtSub   => Hex("4c545d");
    static Color BaseDark   => RGBA(22, 26, 32, 0.20f);     // 패널 단일 필름(얇게). 블러가 배경 담당, 이건 통일 틴트만. (3겹중 본문겹 제거 -> 이거+헤더만)
    static Color BandHead   => RGBA(202, 207, 213, 0.30f);  // 헤더만 살짝 더 또렷(BagPanel 위 얇게 1겹 더). 본문은 필름 없음.
    static Color BandBody   => RGBA(218, 223, 228, 0.00f);  // 본문 밴드 제거(투명). 블러가 본문 배경 = 칸마다 블러 비침(엔필 단일 프로스트 방식)
    static Color Hairline   => RGBA(20, 24, 30, 0.50f);     // 헤더 밑 1px 선 (밝은 위라 어둡게)
    static Color BtnLight   => RGBA(255, 255, 255, 0.40f);  // 하단 버튼(밝은 무채색)
    static Color BtnLightBd => RGBA(255, 255, 255, 0.50f);
    static Color SlotFill   => RGBA(24, 28, 34, 0.06f);     // 칸 안 거의 비움(borders-only). 블러 그대로 비침. 칸 정의는 4변 테두리가.
    static Color SlotEmptyC => RGBA(20, 24, 30, 0.20f);     // 빈 칸 더 어둡게
    static Color SlotBorder => RGBA(8, 10, 14, 0.90f);      // 슬롯 진한 검정 2px 테두리 (연한 fill 위에서 또렷하게)

    [MenuItem("Tools/TIMEKOV/인벤토리 UI 생성 (가방)")]
    public static void BuildBag()
    {
        var ctrl = Object.FindAnyObjectByType<InventoryUIController>(FindObjectsInactive.Include);
        if (ctrl == null) { EditorUtility.DisplayDialog("오류", "씬에 InventoryUIController가 없습니다.", "확인"); return; }

        var so = new SerializedObject(ctrl);
        var rootGo = so.FindProperty("inventoryRoot").objectReferenceValue as GameObject;
        if (rootGo == null) { EditorUtility.DisplayDialog("오류", "InventoryUIController.inventoryRoot 미연결.", "확인"); return; }

        var oldBag = so.FindProperty("bagPanel").objectReferenceValue as GameObject;
        if (oldBag != null)
        {
            if (!EditorUtility.DisplayDialog("경고", "기존 가방 패널을 삭제하고 새로 만듭니다.", "새로 만들기", "취소")) return;
            Object.DestroyImmediate(oldBag);
        }

        bool wasActive = rootGo.activeSelf;
        if (!wasActive) rootGo.SetActive(true);

        RestyleSlotCore();   // 슬롯 프리팹도 항상 최신 스타일로 같이 갱신 (수동 메뉴 깜빡 방지)

        // 기존 블러 캔버스 제거 (재실행 중복 방지)
        var oldBlurP = so.FindProperty("bagBlurCanvas");
        if (oldBlurP != null && oldBlurP.objectReferenceValue is GameObject oldBlurCanvas)
            Object.DestroyImmediate(oldBlurCanvas);

        // 베이스 패널 = 어두운 0.30 (HANDOFF 0.5). 위에 밝은 밴드들이 얹히고 가장자리 3px + 푸터로 베이스가 노출됨.
        // 블러는 뒤 UIBlur 캔버스가 담당(채도 낮춰 맵색 중화). 화면 오른쪽(BagRightX).
        const float pw = 560f, ph = 588f;
        var panel = MakeRounded("BagPanel", rootGo.transform, new Vector2(pw, ph), new Vector2(BagRightX, 0), BaseDark);
        var prt = panel.GetComponent<RectTransform>();

        // ── 헤더 밴드 (밝음, 상단 3px 띄워 베이스 노출). 타이틀 + 닫기, 아래 hairline ──
        var header = MakeRounded("HeaderBand", prt, Vector2.zero, Vector2.zero, BandHead);
        StretchTop(header.GetComponent<RectTransform>(), 52, 1, 1);   // 베이스 가장자리 얇게(1px)
        AddTopSheen(header.transform, 0.18f);
        // 가로 구분선 없음 (clggdesign #3: 헤더/용량/그리드 한 표면, 푸터만 베이스로 구분)

        var title = MakeTMP("Title", header.transform, "가방", 27, TxtMain, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 20, 240, 40);
        title.fontStyle = FontStyles.Bold;

        // 닫기 X (박스 없는 SF 글리프, 어두운 글자색)
        var closeBtn = MakeIconButton("CloseButton", header.transform, "ic_close", 40, Color.clear);
        AnchorRight(closeBtn.GetComponent<RectTransform>(), 10, 44, 44);
        TintIcon(closeBtn, TxtMain);
        SetRef(so, "bagCloseBtn", closeBtn.GetComponent<Button>());

        // ── 본문 밴드 (밝음, 블러 대상). 위=용량 텍스트, 아래=슬롯 그리드 ──
        var body = MakeRounded("BodyBand", prt, Vector2.zero, Vector2.zero, BandBody);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(1, 63); bodyRt.offsetMax = new Vector2(-1, -53);   // 헤더(53) 바로 아래 ~ 푸터(63) 위, 좌우 1(얇은 베이스 가장자리)

        // 용량 = 텍스트만 ("용량 0 / 35"), 상태색은 컨트롤러가 갱신. (최종 디자인 = 게이지 바 없음)
        var cap = MakeTMP("CapacityText", body.transform, "용량 0/35", 22, TxtSub, TextAlignmentOptions.Left);
        cap.fontStyle = FontStyles.Bold;
        var caprt = cap.rectTransform;
        caprt.anchorMin = caprt.anchorMax = new Vector2(0, 1); caprt.pivot = new Vector2(0, 1);
        caprt.sizeDelta = new Vector2(320, 30); caprt.anchoredPosition = new Vector2(22, -16);
        SetRef(so, "capacityText", cap);
        SetRef(so, "bagCapacityGaugeFill", null);

        // 카테고리 탭: 단독 가방엔 없음 (카테고리는 창고/듀얼에만)
        SetRef(so, "bagFilterUI", null);

        // ── 슬롯 그리드 (스크롤). 본문 밴드 안, 용량(50) 아래 ~ 바닥(8) 위. 4행 보이고 세로 스크롤 ──
        var scrollGo = MakeEmpty("SlotScroll", body.transform, Vector2.zero, Vector2.zero);
        StretchMiddle(scrollGo.GetComponent<RectTransform>(), 50, 8, 14);
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = Color.clear;   // 투명(밴드가 표면), 스크롤휠 레이캐스트용
        scrollGo.AddComponent<RectMask2D>();
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var content = MakeEmpty("Content", scrollGo.transform, Vector2.zero, Vector2.zero);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
        crt.offsetMin = new Vector2(0, 0); crt.offsetMax = new Vector2(-8, 0);   // 우측 스크롤 거터 8
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90, 90); grid.spacing = new Vector2(14, 14);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperCenter;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();
        scroll.content = crt;

        // ── 세로 스크롤바 (그리드 우측 거터, 무채색) ──
        var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGo.transform.SetParent(scrollGo.transform, false);
        var sbRt = sbGo.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1, 0); sbRt.anchorMax = new Vector2(1, 1); sbRt.pivot = new Vector2(1, 1);
        sbRt.sizeDelta = new Vector2(6, 0); sbRt.anchoredPosition = new Vector2(-1, 0);
        var sbTrack = sbGo.GetComponent<Image>(); sbTrack.sprite = RoundedSprite(); sbTrack.type = Image.Type.Sliced; sbTrack.color = RGBA(40, 46, 54, 0.18f);
        var sb = sbGo.GetComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop;

        var slideArea = new GameObject("Sliding Area", typeof(RectTransform));
        slideArea.transform.SetParent(sbGo.transform, false);
        Stretch(slideArea.GetComponent<RectTransform>());

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slideArea.transform, false);
        var hRt = handle.GetComponent<RectTransform>(); Stretch(hRt);
        var hImg = handle.GetComponent<Image>(); hImg.sprite = RoundedSprite(); hImg.type = Image.Type.Sliced; hImg.color = RGBA(70, 78, 90, 0.55f);

        sb.targetGraphic = hImg; sb.handleRect = hRt;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var gridUI = scrollGo.AddComponent<InventoryGridUI>();
        var gso = new SerializedObject(gridUI);
        gso.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        gso.FindProperty("slotGrid").objectReferenceValue = content.transform;
        gso.ApplyModifiedProperties();
        SetRef(so, "bagGridUI", gridUI);

        // ── 푸터 (배경 없음 = 베이스 노출). 정렬 버튼만 우하단 = 아이콘 없이 "정렬" 텍스트 박스 ──
        var compactBtn = MakeRounded("Compact", prt, new Vector2(84, 40), Vector2.zero, BtnLight);
        AddOutline(compactBtn, BtnLightBd, new Vector2(1f, -1f));
        var compactRt = compactBtn.GetComponent<RectTransform>();
        compactRt.anchorMin = compactRt.anchorMax = new Vector2(1, 0); compactRt.pivot = new Vector2(1, 0);
        compactRt.sizeDelta = new Vector2(84, 40); compactRt.anchoredPosition = new Vector2(-16, 14);
        var compactBtnComp = compactBtn.AddComponent<Button>(); compactBtnComp.targetGraphic = compactBtn.GetComponent<Image>();
        var compactTxt = MakeTMP("Text", compactBtn.transform, "정렬", 16, TxtMain, TextAlignmentOptions.Center);
        Stretch(compactTxt.rectTransform); compactTxt.fontStyle = FontStyles.Bold;

        // 가방 정렬바 — 정리(분류순 자동정렬)만 연결
        var bagSort = panel.AddComponent<SortBarUI>();
        var bso = new SerializedObject(bagSort);
        bso.FindProperty("organizeBtn")?.SetValueObj(compactBtn.GetComponent<Button>());
        bso.ApplyModifiedProperties();
        SetRef(so, "bagSortBarUI", bagSort);

        // ── 블러 캔버스 (Screen Space-Camera + UIBlur), 채도 낮춰 맵색 중화 ──
        var blurCanvas = MakeBlurCanvas(new Vector2(pw - 6, ph - 6));
        blurCanvas.SetActive(false);   // 인벤 닫힘 상태 기본 (컨트롤러가 열 때 켬)
        SetRef(so, "bagBlurCanvas", blurCanvas);

        SetRef(so, "bagPanel", panel);
        so.ApplyModifiedProperties();

        if (!wasActive) rootGo.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료", "가방 패널 + 슬롯 리스타일 한 번에 생성 완료.\nPlay -> TAB 확인 후 Ctrl+S.\n블러는 BagPanel의 InventoryBlurTuner 슬라이더로 미세조정.", "확인");
    }

    // ── 슬롯 프리팹 리스타일 (둥근 프레임 + 크롬 테두리 + 하단 등급바 + 우상단 수량칩) ──
    [MenuItem("Tools/TIMEKOV/인벤토리 슬롯 리스타일")]
    public static void RestyleSlot()
    {
        if (RestyleSlotCore())
            EditorUtility.DisplayDialog("완료", "슬롯 리스타일 완료.\n(가방 UI 생성 시 자동으로도 적용됨.)\nPlay로 확인.", "확인");
    }

    // 슬롯 프리팹 리스타일 본체 (다이얼로그 없음. BuildBag에서도 자동 호출해서 항상 동기화).
    static bool RestyleSlotCore()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        if (root == null) { Debug.LogWarning("[InventoryUIBuilder] 슬롯 프리팹 못 찾음: " + SlotPrefabPath); return false; }

        // 루트 = 각진 어두운 셀 (HANDOFF 0.5: 밝은 밴드 위에 어두운 슬롯 셀). sprite 없음 = 각진 모서리.
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null) { rootImg.sprite = null; rootImg.type = Image.Type.Simple; rootImg.color = SlotFill; }
        var ol = root.GetComponent<UnityEngine.UI.Outline>();
        if (ol == null) ol = root.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = SlotBorder; ol.effectDistance = Vector2.zero;   // Outline 시각 off (알파가 fill에 곱해져 회색됨). 테두리는 4변 프레임이 담당.
        // 외부 그림자는 최종 디자인에 없음 (셀 자체가 어두워 대비됨). 기존 그림자 있으면 끔.
        foreach (var sh in root.GetComponents<UnityEngine.UI.Shadow>())
            if (!(sh is UnityEngine.UI.Outline)) sh.effectColor = new Color(0, 0, 0, 0f);

        var t = root.transform;

        // ── 진한 검정 2px 테두리 = 4변 독립 프레임 (Outline은 알파가 fill에 곱해져 회색됨 -> 폐기) ──
        var edgeT = MakeSlotEdge(t, "BorderTop",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 2), SlotBorder);
        var edgeB = MakeSlotEdge(t, "BorderBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 2), SlotBorder);
        var edgeL = MakeSlotEdge(t, "BorderLeft",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(2, 0), SlotBorder);
        var edgeR = MakeSlotEdge(t, "BorderRight",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(2, 0), SlotBorder);

        // SlotInner(호버 오버레이)도 각지게
        var si = t.Find("SlotInner") as RectTransform;
        if (si != null) { var sii = si.GetComponent<Image>(); if (sii != null) { sii.sprite = null; sii.type = Image.Type.Simple; } }

        // 어두운 셀에는 상단 서리 sheen 없음(최종 디자인). 이전에 만든 SlotSheen 있으면 끔.
        var hl = (t.Find("SlotSheen") ?? t.Find("TopHighlight")) as RectTransform;
        if (hl != null) hl.gameObject.SetActive(false);

        // GradeBorder -> 하단 등급 언더라인 바 (칸 하단 가득, 높이 4, 바닥 밀착)
        var gb = t.Find("GradeBorder") as RectTransform;
        if (gb != null)
        {
            gb.anchorMin = new Vector2(0, 0); gb.anchorMax = new Vector2(1, 0); gb.pivot = new Vector2(0.5f, 0);
            gb.sizeDelta = new Vector2(0, 4); gb.anchoredPosition = new Vector2(0, 0);
            var gbi = gb.GetComponent<Image>(); if (gbi != null) { gbi.sprite = null; gbi.type = Image.Type.Simple; }
        }

        // 등급 오로라: 하단서 위로 번지는 그라데이션(높이 46). 색은 런타임에 슬롯이 등급색으로 Image.color 틴트.
        // UIFrostGradient = 아래 흰 1 -> 위 투명 (Image.color에 곱해짐). 커먼은 슬롯이 alpha 0으로.
        var aurora = t.Find("GradeAurora") as RectTransform;
        if (aurora == null)
        {
            var ag = new GameObject("GradeAurora", typeof(RectTransform), typeof(Image), typeof(UIFrostGradient));
            aurora = ag.GetComponent<RectTransform>(); aurora.SetParent(t, false);
        }
        aurora.anchorMin = new Vector2(0, 0); aurora.anchorMax = new Vector2(1, 0); aurora.pivot = new Vector2(0.5f, 0);
        aurora.sizeDelta = new Vector2(0, 46); aurora.anchoredPosition = new Vector2(0, 0);
        var auImg = aurora.GetComponent<Image>(); auImg.sprite = null; auImg.color = new Color(1, 1, 1, 0f); auImg.raycastTarget = false;
        var auGrad = aurora.GetComponent<UIFrostGradient>(); if (auGrad == null) auGrad = aurora.gameObject.AddComponent<UIFrostGradient>();
        auGrad.topColor = new Color(1, 1, 1, 0f); auGrad.bottomColor = new Color(1, 1, 1, 1f);

        // ItemIcon -> 중앙 78 (슬롯 90의 ~87%, 꽉차게. 너무 크면 줄임)
        var ic = t.Find("ItemIcon") as RectTransform;
        if (ic != null)
        {
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f);
            ic.sizeDelta = new Vector2(78, 78); ic.anchoredPosition = Vector2.zero;
            var ii = ic.GetComponent<Image>(); if (ii != null) ii.preserveAspect = true;
        }

        // AmountText -> 우상단 + 그 뒤에 검은 반투명 수량 칩
        var at = t.Find("AmountText") as RectTransform;
        GameObject chipGo = null;
        if (at != null)
        {
            at.anchorMin = at.anchorMax = new Vector2(1, 1); at.pivot = new Vector2(1, 1);
            at.sizeDelta = new Vector2(34, 20); at.anchoredPosition = new Vector2(-5, -5);
            var tmp = at.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.fontSize = 14; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; }

            // 칩은 새 GameObject 대신 안 쓰는 기존 SelectedOverlay를 재활용
            // (LoadPrefabContents에서 new GameObject가 프리팹에 저장 안 되는 경우 대비 - 확실한 방식)
            var chip = (t.Find("AmountChip") ?? t.Find("SelectedOverlay")) as RectTransform;
            if (chip != null)
            {
                chip.gameObject.name = "AmountChip";
                chip.gameObject.SetActive(true);
                chip.anchorMin = chip.anchorMax = new Vector2(1, 1); chip.pivot = new Vector2(1, 1);
                chip.sizeDelta = new Vector2(34, 20); chip.anchoredPosition = new Vector2(-5, -5);
                var chipImg = chip.GetComponent<Image>();
                if (chipImg == null) chipImg = chip.gameObject.AddComponent<Image>();
                chipImg.sprite = RoundedSprite(); chipImg.type = Image.Type.Sliced;
                chipImg.color = RGBA(8, 10, 14, 0.62f); chipImg.raycastTarget = false;
                chip.SetSiblingIndex(at.GetSiblingIndex());   // AmountText 아래로 -> 숫자가 칩 위에 보이게
                chipGo = chip.gameObject;
            }
        }

        // 상태색 (bgImage=SlotInner): 평소 투명, 호버 시 살짝 밝아짐. 테두리 평소=진한검정/호버=흰색. 칩/오로라 배선.
        var slotUI = root.GetComponent<InventorySlotUI>();
        if (slotUI != null)
        {
            var sso = new SerializedObject(slotUI);
            sso.FindProperty("normalColor").colorValue   = new Color(1, 1, 1, 0f);    // SlotInner 평소 투명
            var hp = sso.FindProperty("hoverColor"); if (hp != null) hp.colorValue = RGBA(255, 255, 255, 0.10f);    // 호버 (무채색 살짝 밝게)
            var nb = sso.FindProperty("normalBorderColor"); if (nb != null) nb.colorValue = SlotBorder;             // 진한 검정
            var hb = sso.FindProperty("hoverBorderColor"); if (hb != null) hb.colorValue = RGBA(255, 255, 255, 0.5f); // 호버 흰 테두리
            var cc = sso.FindProperty("countChip"); if (cc != null && chipGo != null) cc.objectReferenceValue = chipGo;
            var ga = sso.FindProperty("gradeAurora"); if (ga != null) ga.objectReferenceValue = auImg;
            sso.ApplyModifiedProperties();
        }

        // 아이콘 뒤 backing 제거 (칸마다 어두운 동그라미로 보여 거슬림. 슬롯 네이비가 이미 대비 역할).
        var oldBacking = t.Find("IconBacking");
        if (oldBacking != null) Object.DestroyImmediate(oldBacking.gameObject);
        if (slotUI != null)
        {
            var sso2 = new SerializedObject(slotUI);
            var ib = sso2.FindProperty("iconBacking"); if (ib != null) ib.objectReferenceValue = null;
            sso2.ApplyModifiedProperties();
        }

        // 아이콘 그림자 (어두운 셀 위에서 윤곽 살게). CSS drop-shadow 0 1px 3px black 0.5.
        if (ic != null)
        {
            var sh = ic.GetComponent<UnityEngine.UI.Shadow>();
            if (sh == null) sh = ic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = RGBA(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(0, -1);
        }

        // 그리기 순서 (뒤->앞): SlotInner(호버) < GradeAurora < GradeBorder(언더라인) < ItemIcon < AmountChip < 숫자 < NEW
        var newBadge = t.Find("NewBedge");
        if (si != null) si.SetAsLastSibling();
        if (hl != null) hl.SetAsLastSibling();
        if (aurora != null) aurora.SetAsLastSibling();
        if (edgeT != null) edgeT.SetAsLastSibling();
        if (edgeB != null) edgeB.SetAsLastSibling();
        if (edgeL != null) edgeL.SetAsLastSibling();
        if (edgeR != null) edgeR.SetAsLastSibling();
        if (gb != null) gb.SetAsLastSibling();   // 등급 언더라인이 하단 테두리 위에
        if (ic != null) ic.SetAsLastSibling();
        if (chipGo != null) chipGo.transform.SetAsLastSibling();
        if (at != null) at.SetAsLastSibling();
        if (newBadge != null) newBadge.SetAsLastSibling();

        PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ── 카테고리 탭 (배경 버튼 + 가운데 아이콘) ──
    static GameObject MakeCatTab(Transform parent, string iconName)
    {
        var go = MakeRounded("CatTab_" + iconName, parent, new Vector2(64, 44), Vector2.zero, CatBtn);
        AddOutline(go, Chrome, new Vector2(1f, -1f));
        go.AddComponent<Button>().targetGraphic = go.GetComponent<Image>();
        var icon = MakeImage("CatIcon", go.transform, new Vector2(28, 28), Vector2.zero, Hex("c2d4e6"));
        var img = icon.GetComponent<Image>();
        img.raycastTarget = false; img.preserveAspect = true;
        var spr = LoadSpr(iconName);
        if (spr != null) img.sprite = spr;
        return go;
    }

    // ── 헬퍼 ──
    // 블러 캔버스: Screen Space-Camera 루트 캔버스 + UIBlur 영역(보이지 않음, 카메라 출력의 그 사각 영역을 블러).
    // 데모 "Single Camera UI Preserving Blur" 검증 방식. Overlay 인벤 UI가 그 위에 그려져 sharp 유지.
    static GameObject MakeBlurCanvas(Vector2 regionSize)
    {
        var cam = PickBuildCamera();

        var go = new GameObject(BlurCanvasName, typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 9;   // 인벤 캔버스(10)보다 아래
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var region = new GameObject("BagBlurRegion", typeof(RectTransform), typeof(CanvasRenderer));
        region.transform.SetParent(go.transform, false);
        var rt = region.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = regionSize; rt.anchoredPosition = new Vector2(BagRightX, 0);   // 패널과 같은 오른쪽 위치 (정렬)

        var uiBlur = region.AddComponent<UIBlur>();
        var s = uiBlur.Common.blurInstanceSettings;
        // 채도 낮춤(맵색 중화). 다크 표면이라 밝기는 0 (올리면 표면이 떠서 글자 대비 죽음).
        if (s != null) { s.blurAdditionalDistancePerIteration = 11f; s.vibrancy = -0.5f; s.brightness = 0f; }
        if (cam != null) uiBlur.Common.cameraReference = cam;
        region.AddComponent<InventoryBlurTuner>();
        return go;
    }

    static Camera PickBuildCamera()
    {
        var main = Camera.main;
        if (main != null && main.targetTexture == null) return main;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null) return c;
        return main;
    }

    static GameObject MakeRounded(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = MakeImage(name, parent, size, pos, color);
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite();
        img.type = Image.Type.Sliced;
        return go;
    }

    static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeEmpty(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
        tmp.enableWordWrapping = false; tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 size, Vector2 pos, string label, float fontSize, Color bg)
    {
        var go = MakeRounded(name, parent, size, pos, bg);
        AddOutline(go, Chrome, new Vector2(1f, -1f));
        go.GetComponent<Button>(); // ensure
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var txt = MakeTMP("Text", go.transform, label, fontSize, TxtSub, TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        return go;
    }

    static GameObject MakeIconButton(string name, Transform parent, string iconName, float size, Color bg)
    {
        GameObject go = bg.a > 0f ? MakeRounded(name, parent, new Vector2(size, size), Vector2.zero, bg)
                                  : MakeImage(name, parent, new Vector2(size, size), Vector2.zero, Color.clear);
        if (bg.a > 0f) AddOutline(go, Chrome, new Vector2(1f, -1f));
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var icon = MakeImage("Icon", go.transform, new Vector2(size * 0.6f, size * 0.6f), Vector2.zero, TxtSub);
        var img = icon.GetComponent<Image>(); img.raycastTarget = false; img.preserveAspect = true;
        var spr = LoadSpr(iconName);
        if (spr != null) img.sprite = spr;
        return go;
    }

    // 아이콘(좌) + 글자 버튼 (예: [정리])
    static GameObject MakeIconTextButton(string name, Transform parent, string iconName, string label, float w, float h, Color bg)
    {
        var go = MakeRounded(name, parent, new Vector2(w, h), Vector2.zero, bg);
        AddOutline(go, Chrome, new Vector2(1f, -1f));
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();

        var icon = MakeImage("Icon", go.transform, new Vector2(h * 0.5f, h * 0.5f), Vector2.zero, TxtSub);
        var irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
        irt.anchoredPosition = new Vector2(9, 0);
        var img = icon.GetComponent<Image>(); img.raycastTarget = false; img.preserveAspect = true;
        var spr = LoadSpr(iconName); if (spr != null) img.sprite = spr;

        var txt = MakeTMP("Text", go.transform, label, 15, TxtSub, TextAlignmentOptions.Center);
        var trt = txt.rectTransform;
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(9 + h * 0.5f, 0); trt.offsetMax = new Vector2(-6, 0);
        return go;
    }

    static void AddOutline(GameObject go, Color color, Vector2 dist)
    {
        var ol = go.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = color; ol.effectDistance = dist;
    }

    // 밴드 상단 흰 sheen (유리 두께감). 위 흰빛 -> 아래 투명, 높이 14.
    static void AddTopSheen(Transform parent, float a)
    {
        var go = new GameObject("Sheen", typeof(RectTransform), typeof(Image), typeof(UIFrostGradient));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, 14); rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>(); img.color = Color.white; img.raycastTarget = false;
        var g = go.GetComponent<UIFrostGradient>();
        g.topColor = RGBA(255, 255, 255, a); g.bottomColor = RGBA(255, 255, 255, 0f);
    }

    // 헤더 밑 1px hairline (바닥 밀착).
    static void AddBottomHairline(Transform parent, Color c)
    {
        var go = MakeImage("Hairline", parent, Vector2.zero, Vector2.zero, c);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, 1); rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().raycastTarget = false;
    }

    // 슬롯 4변 테두리 한 변 (독립 Image라 알파가 fill에 안 곱해짐 -> 연한 fill 위에서도 진한 검정 유지).
    static RectTransform MakeSlotEdge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 sz, Color c)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            t = go.GetComponent<RectTransform>(); t.SetParent(parent, false);
        }
        t.anchorMin = aMin; t.anchorMax = aMax; t.pivot = piv; t.sizeDelta = sz; t.anchoredPosition = Vector2.zero;
        var img = t.GetComponent<Image>(); img.sprite = null; img.type = Image.Type.Simple; img.color = c; img.raycastTarget = false;
        return t;
    }

    // 아이콘 버튼의 가운데 Icon 자식 색 지정 (밝은 밴드 위 = 어둡게).
    static void TintIcon(GameObject btn, Color c)
    {
        var icon = btn.transform.Find("Icon");
        if (icon != null) { var img = icon.GetComponent<Image>(); if (img != null) img.color = c; }
    }

    static void AddCornerBrackets(Transform panel, Vector2 sz, Color color)
    {
        float hx = sz.x / 2f - 8, hy = sz.y / 2f - 8, len = 26, th = 2;
        Vector2[] c = { new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(-hx, -hy), new Vector2(hx, -hy) };
        int[] sx = { 1, -1, 1, -1 }; int[] sy = { -1, -1, 1, 1 };
        for (int i = 0; i < 4; i++)
        {
            MakeImage("BrkH" + i, panel, new Vector2(len, th), c[i] + new Vector2(sx[i] * len / 2, 0), color).GetComponent<Image>().raycastTarget = false;
            MakeImage("BrkV" + i, panel, new Vector2(th, len), c[i] + new Vector2(0, sy[i] * len / 2), color).GetComponent<Image>().raycastTarget = false;
        }
    }

    // anchoring helpers
    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void StretchTop(RectTransform rt, float h, float top, float side)
    { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.offsetMin = new Vector2(side, -top - h); rt.offsetMax = new Vector2(-side, -top); }
    static void StretchBottom(RectTransform rt, float h, float bottom, float side)
    { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.offsetMin = new Vector2(side, bottom); rt.offsetMax = new Vector2(-side, bottom + h); }
    static void StretchMiddle(RectTransform rt, float top, float bottom, float side)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(side, bottom); rt.offsetMax = new Vector2(-side, -top); }
    static void AnchorLeft(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0); }
    static void AnchorLeftMid(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0); }
    static void AnchorRight(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(-x, 0); }
    static void AnchorRightBottom(RectTransform rt, float x, float y, float size)
    { rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0); rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = new Vector2(-x, y); }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = obj;
        else Debug.LogWarning("[InventoryUIBuilder] 필드 없음: " + field + " (InventoryUIController에 추가 필요)");
    }

    static Color Hex(string hex, int a = 255)
    { if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) { c.a = a / 255f; return c; } return Color.white; }
    static Color RGBA(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

    static Sprite _rounded;
    static Sprite RoundedSprite()
    {
        if (_rounded == null)
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        return _rounded;
    }

    static Sprite _knob;
    static Sprite KnobSprite()   // 원형 (아이콘 뒤 backing 용)
    {
        if (_knob == null)
            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        return _knob;
    }

    static Sprite LoadSpr(string n)
    {
        string p = SprDir + n + ".png";
        // 3D 프로젝트는 PNG가 Texture(Default)로 임포트됨 -> Sprite/Single로 교정해야 잡힘.
        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[InventoryUIBuilder] 파일/임포터 없음: " + p); return null; }
        if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        // LoadAssetAtPath가 null이면 서브에셋에서 Sprite 직접 탐색 (재임포트 타이밍 대비)
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (s == null)
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Sprite sp) { s = sp; break; }
        if (s == null) Debug.LogWarning("[InventoryUIBuilder] 스프라이트 로드 실패: " + p);
        return s;
    }
}

// SerializedProperty Object 세팅 확장 (이름 충돌 회피용)
static class _InvBuilderSPExt
{
    public static void SetValueObj(this SerializedProperty p, Object v) { if (p != null) p.objectReferenceValue = v; }
}
