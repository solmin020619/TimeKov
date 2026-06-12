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
    static Color Panel    => RGBA(13, 26, 44, 0.57f);   // 본체 (반투명, 2단계서 블러)
    static Color BarBg    => RGBA(25, 44, 72, 0.47f);   // 헤더/푸터 바 (더 불투명)
    static Color SlotTone => RGBA(28, 48, 74, 0.10f);   // 가방 본문 톤 (밝게)
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

        // ── 가방 패널 560 x 724 (HANDOFF §2-2). 배경 = BlurredImage(간유리) + 네이비 틴트 + 튜너 자동 ──
        var panel = MakeBlurPanel("BagPanel", rootGo.transform, new Vector2(560, 724));
        AddOutline(panel, ChromeHi, new Vector2(1f, -1f));
        AddCornerBrackets(panel.transform, new Vector2(560, 724), RGBA(95, 196, 255, 0.9f));

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
        var cap = MakeTMP("CapacityText", header.transform, "0 / 35", 19, TxtSub, TextAlignmentOptions.Right);
        AnchorRight(cap.rectTransform, 60, 120, 24);
        cap.rectTransform.anchoredPosition = new Vector2(cap.rectTransform.anchoredPosition.x, 7);
        SetRef(so, "capacityText", cap);
        var gTrough = MakeRounded("GaugeTrough", header.transform, new Vector2(150, 5), Vector2.zero, RGBA(6, 13, 24, 0.7f));
        var gtrt = gTrough.GetComponent<RectTransform>();
        AnchorRight(gtrt, 60, 150, 5);
        gtrt.anchoredPosition = new Vector2(gtrt.anchoredPosition.x, -12);
        var gFill = MakeRounded("GaugeFill", gTrough.transform, new Vector2(150, 5), Vector2.zero, Cyan);
        Stretch(gFill.GetComponent<RectTransform>());
        var gFillImg = gFill.GetComponent<Image>();
        gFillImg.type = Image.Type.Filled; gFillImg.fillMethod = Image.FillMethod.Horizontal; gFillImg.fillOrigin = 0; gFillImg.fillAmount = 0.3f;

        // ── 카테고리 행 (헤더 아래, 높이 50) ──
        var catRow = MakeEmpty("CategoryRow", prt, Vector2.zero, Vector2.zero);
        StretchTop(catRow.GetComponent<RectTransform>(), 50, 18 + 52 + 13, 22);
        var hlg = catRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
        hlg.spacing = 8;
        var filterUI = catRow.AddComponent<CategoryFilterUI>();
        var fso = new SerializedObject(filterUI);
        fso.FindProperty("selectedColor").colorValue = RGBA(95, 196, 255, 0.18f);
        fso.FindProperty("normalColor").colorValue   = CatBtn;
        var btnArr = fso.FindProperty("filterButtons");
        btnArr.arraySize = 7;
        for (int i = 0; i < 7; i++)
            btnArr.GetArrayElementAtIndex(i).objectReferenceValue = MakeCatTab(catRow.transform, CatIcons[i]);
        fso.ApplyModifiedProperties();
        SetRef(so, "bagFilterUI", filterUI);

        // ── 슬롯 그리드 (스크롤, 가운데 채움) ──
        // 위 = 헤더+카테고리 아래(18+52+13+50+13=146), 아래 = 하단바 위(18+52+13=83)
        var scrollGo = MakeEmpty("SlotScroll", prt, Vector2.zero, Vector2.zero);
        StretchMiddle(scrollGo.GetComponent<RectTransform>(), 146, 83, 22);
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

        // ── 하단 바 (정렬 드롭다운 + 오름내림 + 정리) HANDOFF §2-5 ──
        var footer = MakeRounded("BottomBar", prt, Vector2.zero, Vector2.zero, BarBg);
        StretchBottom(footer.GetComponent<RectTransform>(), 52, 18, 22);

        var sortBtn = MakeButton("SortDropdown", footer.transform, new Vector2(132, 36), new Vector2(80, 0), "획득순", 16, RGBA(30, 48, 74, 0.5f));
        AnchorLeftMid(sortBtn.GetComponent<RectTransform>(), 14, 132, 36);
        var dirBtn = MakeIconButton("SortDir", footer.transform, "ic_sort_dir", 38, RGBA(30, 48, 74, 0.5f));
        AnchorLeftMid(dirBtn.GetComponent<RectTransform>(), 14 + 132 + 8, 38, 38);
        var compactBtn = MakeIconButton("Compact", footer.transform, "ic_compact", 38, RGBA(30, 48, 74, 0.5f));
        AnchorLeftMid(compactBtn.GetComponent<RectTransform>(), 14 + 132 + 8 + 38 + 8, 38, 38);

        // 가방 정렬바 — 컨트롤러가 Instance(가방)에 바인딩하도록 bagSortBarUI 필드에 연결
        var bagSort = footer.AddComponent<SortBarUI>();
        var bso = new SerializedObject(bagSort);
        bso.FindProperty("sortDropdown")?.SetValueObj(null);   // 드롭다운은 커스텀 버튼이라 미사용(추후 메뉴 연결)
        bso.FindProperty("orderToggleBtn")?.SetValueObj(dirBtn.GetComponent<Button>());
        bso.FindProperty("orderBtnText")?.SetValueObj(dirBtn.GetComponentInChildren<TextMeshProUGUI>());
        bso.FindProperty("organizeBtn")?.SetValueObj(compactBtn.GetComponent<Button>());
        bso.ApplyModifiedProperties();
        SetRef(so, "bagSortBarUI", bagSort);

        SetRef(so, "bagPanel", panel);
        so.ApplyModifiedProperties();

        if (!wasActive) rootGo.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료", "가방 패널 생성 (1단계: 구조).\nPlay -> TAB 확인 후 Ctrl+S.\n블러/슬롯/효과는 다음 단계.", "확인");
    }

    // ── 슬롯 프리팹 리스타일 (둥근 프레임 + 크롬 테두리 + 하단 등급바 + 우상단 수량칩) ──
    [MenuItem("Tools/TIMEKOV/인벤토리 슬롯 리스타일")]
    public static void RestyleSlot()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        if (root == null) { EditorUtility.DisplayDialog("오류", "슬롯 프리팹 못 찾음: " + SlotPrefabPath, "확인"); return; }

        // 루트 = 둥근 슬롯 프레임 + 반투명 네이비 + 크롬 테두리 (칸이 또렷하게 보이게)
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null) { rootImg.sprite = RoundedSprite(); rootImg.type = Image.Type.Sliced; rootImg.color = RGBA(28, 48, 74, 0.22f); }
        var ol = root.GetComponent<UnityEngine.UI.Outline>();
        if (ol == null) ol = root.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = RGBA(160, 190, 218, 0.34f); ol.effectDistance = new Vector2(1f, -1f);

        var t = root.transform;

        // SlotInner(상태색 오버레이)도 둥글게
        var si = t.Find("SlotInner") as RectTransform;
        if (si != null) { var sii = si.GetComponent<Image>(); if (sii != null) { sii.sprite = RoundedSprite(); sii.type = Image.Type.Sliced; } }

        // GradeBorder -> 하단 등급 언더라인 바 (좌우 인셋, 높이 5)
        var gb = t.Find("GradeBorder") as RectTransform;
        if (gb != null)
        {
            gb.anchorMin = new Vector2(0, 0); gb.anchorMax = new Vector2(1, 0); gb.pivot = new Vector2(0.5f, 0);
            gb.sizeDelta = new Vector2(-12, 5); gb.anchoredPosition = new Vector2(0, 5);
            var gbi = gb.GetComponent<Image>(); if (gbi != null) { gbi.sprite = null; gbi.type = Image.Type.Simple; }
        }

        // AmountText -> 우상단 수량 칩
        var at = t.Find("AmountText") as RectTransform;
        if (at != null)
        {
            at.anchorMin = at.anchorMax = new Vector2(1, 1); at.pivot = new Vector2(1, 1);
            at.sizeDelta = new Vector2(46, 22); at.anchoredPosition = new Vector2(-6, -6);
            var tmp = at.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.fontSize = 16; tmp.alignment = TextAlignmentOptions.TopRight; }
        }

        // ItemIcon -> 중앙 56
        var ic = t.Find("ItemIcon") as RectTransform;
        if (ic != null)
        {
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f); ic.pivot = new Vector2(0.5f, 0.5f);
            ic.sizeDelta = new Vector2(56, 56); ic.anchoredPosition = Vector2.zero;
            var ii = ic.GetComponent<Image>(); if (ii != null) ii.preserveAspect = true;
        }

        // 상태색 (bgImage=SlotInner): 평소 투명(루트 프레임 보임), 선택/호버 시안
        var slotUI = root.GetComponent<InventorySlotUI>();
        if (slotUI != null)
        {
            var sso = new SerializedObject(slotUI);
            sso.FindProperty("normalColor").colorValue   = RGBA(28, 48, 74, 0f);
            sso.FindProperty("selectedColor").colorValue = RGBA(95, 196, 255, 0.45f);
            var hp = sso.FindProperty("hoverColor"); if (hp != null) hp.colorValue = RGBA(95, 196, 255, 0.26f);
            sso.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        EditorUtility.DisplayDialog("완료", "슬롯 리스타일 완료.\n(런타임 슬롯이 프리팹 따르니 가방 빌더 재실행 불필요.)\nPlay로 확인.", "확인");
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
    // 간유리 패널: BlurredImage(뒤 게임 블러) + 네이비 틴트 + 실시간 튜너 자동 부착.
    static GameObject MakeBlurPanel(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(BlurredImage));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        var bi = go.GetComponent<BlurredImage>();
        bi.sprite = RoundedSprite(); bi.type = Image.Type.Sliced;
        bi.color = new Color(0.043f, 0.086f, 0.153f, 0.78f);   // 네이비 틴트 (InventoryBlurTuner가 실시간 덮어씀)
        go.AddComponent<InventoryBlurTuner>();
        return go;
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
