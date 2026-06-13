// =====================================================================
// InventoryUIBuilder.cs (Editor Only)
// Tools/TIMEKOV/인벤토리 UI 생성 (가방)
// HANDOFF.md (Assets/11.UI/Inventory) 수치 그대로 가방 패널을 새로 생성 +
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
    const string SprDir = "Assets/11.UI/Inventory/sprites/";

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
    static Color TxtMain  => Hex("f1f7fd");
    static Color TxtSub   => Hex("b9cadb");

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

        // ── 가방 패널 560 x 600 (카테고리 없는 간소화), 화면 오른쪽(BagRightX). 배경 = 반투명 Image (블러는 뒤 UIBlur) ──
        var panel = MakeRounded("BagPanel", rootGo.transform, new Vector2(560, 600), new Vector2(BagRightX, 0), Panel);
        AddOutline(panel, ChromeHi, new Vector2(1f, -1f));
        AddCornerBrackets(panel.transform, new Vector2(560, 600), RGBA(95, 196, 255, 0.9f));

        var prt = panel.GetComponent<RectTransform>();

        // ── 헤더 바 (상단 스트레치, 높이 52, 좌우패딩 22, 상패딩 18) ──
        var header = MakeRounded("HeaderBar", prt, Vector2.zero, Vector2.zero, BarBg);
        StretchTop(header.GetComponent<RectTransform>(), 52, 18, 22);

        var title = MakeTMP("Title", header.transform, "가방", 27, TxtMain, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 16, 220, 36);
        title.fontStyle = FontStyles.Bold;

        // 용량 게이지 (우측): 숫자 + 트랙/fill
        // 닫기 X - 또렷한 버튼으로 우상단 끝
        var closeBtn = MakeIconButton("CloseButton", header.transform, "ic_close", 32, RGBA(30, 48, 74, 0.55f));
        AnchorRight(closeBtn.GetComponent<RectTransform>(), 14, 32, 32);
        SetRef(so, "bagCloseBtn", closeBtn.GetComponent<Button>());

        // 용량 숫자 + 게이지 (X 왼쪽)
        var cap = MakeTMP("CapacityText", header.transform, "0 / 35", 22, TxtMain, TextAlignmentOptions.Right);
        cap.fontStyle = FontStyles.Bold;
        AnchorRight(cap.rectTransform, 60, 140, 28);
        cap.rectTransform.anchoredPosition = new Vector2(cap.rectTransform.anchoredPosition.x, 7);
        SetRef(so, "capacityText", cap);
        var gTrough = MakeRounded("GaugeTrough", header.transform, new Vector2(150, 5), Vector2.zero, RGBA(6, 13, 24, 0.7f));
        var gtrt = gTrough.GetComponent<RectTransform>();
        AnchorRight(gtrt, 60, 150, 5);
        gtrt.anchoredPosition = new Vector2(gtrt.anchoredPosition.x, -13);
        var gFill = MakeRounded("GaugeFill", gTrough.transform, new Vector2(150, 5), Vector2.zero, Cyan);
        Stretch(gFill.GetComponent<RectTransform>());
        var gFillImg = gFill.GetComponent<Image>();
        gFillImg.type = Image.Type.Filled; gFillImg.fillMethod = Image.FillMethod.Horizontal; gFillImg.fillOrigin = 0; gFillImg.fillAmount = 0.3f;
        SetRef(so, "bagCapacityGaugeFill", gFillImg);

        // ── 카테고리 탭: 단독 가방에선 제거(엔드필드 단독가방처럼 간소화). 카테고리는 창고(듀얼)에만. ──
        SetRef(so, "bagFilterUI", null);

        // ── 슬롯 그리드 (스크롤). 위 = 헤더 아래(18+52+13=83), 아래 = 하단 아이콘 위(52). 패널 600이라 ~5행 보임 ──
        var scrollGo = MakeEmpty("SlotScroll", prt, Vector2.zero, Vector2.zero);
        StretchMiddle(scrollGo.GetComponent<RectTransform>(), 83, 52, 22);
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = SlotTone;
        scrollGo.AddComponent<RectMask2D>();
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var content = MakeEmpty("Content", scrollGo.transform, Vector2.zero, Vector2.zero);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
        crt.offsetMin = new Vector2(0, 0); crt.offsetMax = new Vector2(-8, 0);   // 우측 스크롤 거터 8
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90, 90); grid.spacing = new Vector2(11, 11);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = scrollGo.GetComponent<RectTransform>();
        scroll.content = crt;

        var gridUI = scrollGo.AddComponent<InventoryGridUI>();
        var gso = new SerializedObject(gridUI);
        gso.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        gso.FindProperty("slotGrid").objectReferenceValue = content.transform;
        gso.ApplyModifiedProperties();
        SetRef(so, "bagGridUI", gridUI);

        // ── 하단: 정리 아이콘 하나만 우하단. 엔드필드 단독가방처럼 극단순화 (획득순/방향 제거) ──
        var compactBtn = MakeIconButton("Compact", prt, "ic_compact", 34, RGBA(30, 48, 74, 0.5f));
        AnchorRightBottom(compactBtn.GetComponent<RectTransform>(), 14, 12, 34);

        // 가방 정렬바 — 정리(분류순 자동정렬)만 연결
        var bagSort = panel.AddComponent<SortBarUI>();
        var bso = new SerializedObject(bagSort);
        bso.FindProperty("organizeBtn")?.SetValueObj(compactBtn.GetComponent<Button>());
        bso.ApplyModifiedProperties();
        SetRef(so, "bagSortBarUI", bagSort);

        // ── 블러 캔버스 (Screen Space-Camera + UIBlur) - 데모 검증 방식. 패널 뒤에서 게임을 블러 ──
        var blurCanvas = MakeBlurCanvas(new Vector2(552, 592));
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

        // 루트 = 둥근 슬롯 프레임 + 네이비 오버레이(0.52) #0e1a2c (패널 0.38보다 진해 타일이 떠 보이게)
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null) { rootImg.sprite = RoundedSprite(); rootImg.type = Image.Type.Sliced; rootImg.pixelsPerUnitMultiplier = 0.6f; rootImg.color = RGBA(24, 29, 38, 0.52f); }
        var ol = root.GetComponent<UnityEngine.UI.Outline>();
        if (ol == null) ol = root.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = RGBA(160, 190, 218, 0.34f); ol.effectDistance = new Vector2(1f, -1f);   // 차분한 푸른빛 테두리(HANDOFF)
        // 외부 그림자 - 슬롯이 패널 위에 떠 보이게 (이게 없으면 평평함). Outline은 Shadow 파생이라 구분해 순수 Shadow만 찾음.
        UnityEngine.UI.Shadow rsh = null;
        foreach (var sh in root.GetComponents<UnityEngine.UI.Shadow>())
            if (!(sh is UnityEngine.UI.Outline)) { rsh = sh; break; }
        if (rsh == null) rsh = root.AddComponent<UnityEngine.UI.Shadow>();
        rsh.effectColor = RGBA(0, 0, 0, 0.26f); rsh.effectDistance = new Vector2(0f, -3f);

        var t = root.transform;

        // SlotInner(상태색 오버레이)도 둥글게
        var si = t.Find("SlotInner") as RectTransform;
        if (si != null) { var sii = si.GetComponent<Image>(); if (sii != null) { sii.sprite = RoundedSprite(); sii.type = Image.Type.Sliced; sii.pixelsPerUnitMultiplier = 0.6f; } }

        // 슬롯 윗면 서리 sheen (위 흰빛 -> 아래로 투명. 칸 위 60%만 살짝 반짝 = 맑은 유리. 패널 전체막 아님)
        var hl = (t.Find("SlotSheen") ?? t.Find("TopHighlight")) as RectTransform;
        if (hl == null)
        {
            var hlGo = new GameObject("SlotSheen", typeof(RectTransform), typeof(Image));
            hl = hlGo.GetComponent<RectTransform>(); hl.SetParent(t, false);
        }
        hl.gameObject.name = "SlotSheen";
        hl.anchorMin = new Vector2(0, 1); hl.anchorMax = new Vector2(1, 1); hl.pivot = new Vector2(0.5f, 1);
        hl.sizeDelta = new Vector2(-6, 56); hl.anchoredPosition = new Vector2(0, -3);   // 위에서 약 60%만 덮음
        var hlImg = hl.GetComponent<Image>(); hlImg.sprite = null; hlImg.color = Color.white; hlImg.raycastTarget = false;
        var hlGrad = hl.GetComponent<UIFrostGradient>(); if (hlGrad == null) hlGrad = hl.gameObject.AddComponent<UIFrostGradient>();
        hlGrad.topColor = RGBA(190, 214, 240, 0.14f); hlGrad.bottomColor = RGBA(190, 214, 240, 0f);   // 상단 하이라이트 (유리 두께감)

        // GradeBorder -> 하단 등급 언더라인 바 (칸 하단 가득, 높이 6, 바닥 밀착)
        var gb = t.Find("GradeBorder") as RectTransform;
        if (gb != null)
        {
            gb.anchorMin = new Vector2(0, 0); gb.anchorMax = new Vector2(1, 0); gb.pivot = new Vector2(0.5f, 0);
            gb.sizeDelta = new Vector2(0, 6); gb.anchoredPosition = new Vector2(0, 0);
            var gbi = gb.GetComponent<Image>(); if (gbi != null) { gbi.sprite = null; gbi.type = Image.Type.Simple; }
            if (si != null) gb.SetSiblingIndex(si.GetSiblingIndex() + 1);   // SlotInner 위로 (호버색에 안 묻히게)
        }

        // ItemIcon -> 중앙 80 (슬롯 90의 ~89%, 엔드필드처럼 칸을 채워 아이템 가독성↑)
        var ic = t.Find("ItemIcon") as RectTransform;
        if (ic != null)
        {
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f);
            ic.sizeDelta = new Vector2(80, 80); ic.anchoredPosition = Vector2.zero;
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
                chipImg.color = RGBA(6, 12, 22, 0.82f); chipImg.raycastTarget = false;
                chip.SetSiblingIndex(at.GetSiblingIndex());   // AmountText 아래로 -> 숫자가 칩 위에 보이게
                chipGo = chip.gameObject;
            }
        }

        // 상태색 (bgImage=SlotInner): 평소 투명(루트 프레임 보임), 호버 시안. + 수량칩 참조 배선.
        var slotUI = root.GetComponent<InventorySlotUI>();
        if (slotUI != null)
        {
            var sso = new SerializedObject(slotUI);
            sso.FindProperty("normalColor").colorValue   = RGBA(24, 29, 38, 0f);   // SlotInner는 투명(루트 색이 보임)
            var hp = sso.FindProperty("hoverColor"); if (hp != null) hp.colorValue = RGBA(95, 196, 255, 0.26f);
            var cc = sso.FindProperty("countChip"); if (cc != null && chipGo != null) cc.objectReferenceValue = chipGo;
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

        // 아이콘 그림자 (backing 대신 대비 확보 - 유리 위에서 윤곽 살게)
        if (ic != null)
        {
            var sh = ic.GetComponent<UnityEngine.UI.Shadow>();
            if (sh == null) sh = ic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = RGBA(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(0, -2);
        }

        // 그리기 순서 재배치 (뒤->앞): 서리/블러는 배경에만, 아이템/개수는 그 위에 또렷하게
        // SlotInner(호버) < SlotSheen(서리) < ItemIcon < AmountChip < AmountText(숫자) < GradeBorder < NEW
        var newBadge = t.Find("NewBedge");
        if (si != null) si.SetAsLastSibling();
        if (hl != null) hl.SetAsLastSibling();
        if (ic != null) ic.SetAsLastSibling();
        if (chipGo != null) chipGo.transform.SetAsLastSibling();
        if (at != null) at.SetAsLastSibling();
        if (gb != null) gb.SetAsLastSibling();
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
        if (s != null) { s.blurAdditionalDistancePerIteration = 11f; s.vibrancy = 0.2f; s.brightness = 0.04f; }
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
