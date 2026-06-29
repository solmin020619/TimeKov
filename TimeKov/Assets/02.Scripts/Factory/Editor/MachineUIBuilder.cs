// =====================================================================
// MachineUIBuilder.cs (Editor Only)
// Tools/TIMEKOV/공장 UI 생성
// 공장(설비) UI를 엔드필드식 간유리 톤으로 빌더 신설. 인벤 빌더(InventoryUIBuilder)의
// 블러/패널/헬퍼를 복제 이식(인벤 빌더는 안 건드림). 로직은 MachineUI 그대로, 레이아웃만 새로.
//
// 단계 1: 단일 간유리 패널 + 블러(PanelBlur 한 겹 + 프로스트 3겹) + 헤더(아이콘/제목/닫기).
//   - 단일 표면 원칙: 패널 하나에 전부 얹음. 중앙에 별도 배경패널 금지(블러 죽음).
// 단계 2: 좌측 가방(스크롤 그리드 + 드롭존) — 런타임이 InventorySlot 프리팹을 채움.
// 단계 3: 중앙 생산부(무채색 도면판 + 재료/연료/게이지/출력 위젯). 중앙 파랑 금지.
// 단계 4: 하단 액션(진행바 + 모두받기 노란버튼 + 재료회수) + 레시피 네비.
//   - 위젯 4종(RecipeDropSlot/FuelDropSlot/MachineSlotWidget/ProcessingGauge)은 프리팹이
//     없어 매 재생성마다 코드로 새로 만들고 SerializedObject 로 내부 필드까지 배선한다.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;
using TIMEKOV.Factory;

public static class MachineUIBuilder
{
    // ── 경로/상수 (인벤 빌더와 동일 자산 재사용) ──
    const string PartDir = "Assets/11.UI/New";                       // clggdesign 간유리 부품 PNG
    const string PanelSpritePath = PartDir + "/panel_ash_a78.png";   // 간유리 패널 표면(9-slice)
    const int PanelSlice = 56;
    const string SprDir = "Assets/11.UI/Inventory UI/sprites/";      // 아이콘 PNG 폴더

    // ── 색 (인벤 팔레트 복제 + 공장 노란 액센트) ──
    static Color BaseDark   => RGBA(230, 223, 211, 0.38f);   // PNG 폴백색(정상시 패널이 PNG라 안쓰임)
    static Color TxtMain    => Hex("242a31");                // 어두운 슬레이트(밝은 표면 위)
    static Color TxtSub     => Hex("4c545d");
    static Color Chrome     => RGBA(150, 178, 205, 0.26f);
    static Color HeaderHair => RGBA(84, 98, 122, 0.50f);     // 헤더 밑 구분선(쿨 슬레이트)
    // 공장 노란 액센트(버튼/진행바/입력화살표) - 단계 4에서 사용
    static Color Yellow     => Hex("e6c24a");
    static Color YellowBd   => Hex("b89a2e");
    static Color YellowTx   => Hex("4a3c0a");
    static Color SlotBody   => RGBA(26, 34, 46, 0.36f);     // 함몰 슬롯 몸체(쿨)

    // ── 레이아웃 상수 (패널 = 스트레치 near-fullscreen, 영역은 패널 가장자리 기준 앵커) ──
    const float HeaderH = 64f, FooterH = 90f;   // 푸터=엔필식 액션바(공식 스트립+버튼+얇은 진행선)
    const float SidePad = 26f;       // 패널 안쪽 여백
    const float Gap = 20f;           // 영역 간 간격
    const float BagWidth = 540f;     // 좌 가방 칼럼 고정폭(나머지는 생산부가 채움)

    // ── 위젯 슬롯 프레임 / 가방 슬롯 프리팹 ──
    const string SlotPrefabPath = "Assets/05.Prefabs/Inventory/InventorySlot.prefab";
    const string SlotFramePath = PartDir + "/hl_slot_frame@2x.png";
    const int SlotFrameSlice = 48;

    [MenuItem("Tools/TIMEKOV/공장 UI 생성")]
    public static void BuildMachineUI()
    {
        var ui = Object.FindAnyObjectByType<MachineUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("오류", "씬에 MachineUI가 없습니다.", "확인"); return; }

        var so = new SerializedObject(ui);

        // ★MachineUI 컴포넌트는 절대 삭제하지 않는다(삭제하면 SerializedObject 타겟이 죽어 에러).
        // uiPanel이 MachineUI를 포함하면(같은 오브젝트/조상) 그 오브젝트를 재활용(자식만 정리),
        // 무관한 별도 패널일 때만 삭제 후 MachineUI 자식으로 새로 만든다.
        var oldPanel = so.FindProperty("uiPanel").objectReferenceValue as GameObject;
        bool reuseInPlace = oldPanel != null &&
            (oldPanel == ui.gameObject || ui.transform.IsChildOf(oldPanel.transform));

        if (oldPanel != null &&
            !EditorUtility.DisplayDialog("경고",
                "설비 UI 패널을 새로 만듭니다.\n가방/재료/연료/출력 슬롯·위젯이 코드로 새로 생성·배선됩니다.",
                "새로 만들기", "취소")) return;

        GameObject panel;
        if (reuseInPlace)
        {
            // uiPanel == MachineUI(또는 그 조상) -> 그 오브젝트 재활용. MachineUI가 들어있는 가지만 보존.
            panel = oldPanel;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
            {
                var ch = panel.transform.GetChild(i);
                if (ui.transform == ch || ui.transform.IsChildOf(ch)) continue;   // MachineUI 보존
                Object.DestroyImmediate(ch.gameObject);
            }
        }
        else
        {
            // 무관한 별도 패널 -> 삭제하고 MachineUI 자식으로 새 패널 생성
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);
            panel = new GameObject("MachinePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(ui.transform, false);
        }

        // ── 패널 (간유리 PNG + 둥근 코너 Mask). ──
        // ★중요1: 부모("Panels")가 100x100 중앙 앵커 컨테이너라 절대 stretch 금지
        //   (stretch 하면 100-여백 = 음수폭 -> 패널 0크기 invisible = "F 안켜짐"의 진범).
        //   sizeDelta(절대값) center 앵커라야 부모크기 무관하게 렌더됨.
        // ★중요2: 패널 localScale 이 0.5625(=1080/1920 stale) 로 박혀있어 sizeDelta 만 키워도
        //   실제론 56%로 줄어 보였음 -> localScale = 1 로 리셋해야 제 크기로 뜸.
        //   CanvasScaler ref 1920x1080(match=width). 1700x830 = 레퍼런스(사진2~5)급 가로형, HP바 안 덮음.
        var prt = panel.GetComponent<RectTransform>();
        if (prt == null) prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.localScale = Vector3.one;
        prt.sizeDelta = new Vector2(1700f, 830f);
        prt.anchoredPosition = Vector2.zero;

        var pimg = panel.GetComponent<Image>();
        if (pimg == null) pimg = panel.AddComponent<Image>();
        var panelSprite = LoadPanelSprite();
        if (panelSprite != null)
        {
            pimg.sprite = panelSprite; pimg.type = Image.Type.Sliced;
            pimg.color = new Color(1f, 1f, 1f, 0.12f);   // ash 표면 아주 옅게 = 칸이 블러 통과
            pimg.pixelsPerUnitMultiplier = 1f;
        }
        else { pimg.sprite = RoundedSprite(); pimg.type = Image.Type.Sliced; pimg.color = BaseDark; }
        var mask = panel.GetComponent<UnityEngine.UI.Mask>();
        if (mask == null) mask = panel.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        // ── 블러 = 단일 표면(패널 자식 BlurredImage 한 겹) + 프로스트 3겹. 인벤 레시피 그대로. ──
        BuildFrost(prt, panelSprite);

        // ── 헤더 (아이콘 / 제목 / 닫기 / 구분선) ──
        const float headerH = 64f;
        var header = MakeImage("HeaderBand", prt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        StretchTop(header.GetComponent<RectTransform>(), headerH, 0, 0);
        header.GetComponent<Image>().raycastTarget = false;

        // 설비 아이콘 (좌). 런타임 OpenFor 가 FacilityIconDatabase 로 sprite 세팅(없으면 숨김).
        var icon = MakeImage("TitleIcon", header.transform, new Vector2(36, 36), Vector2.zero, Color.white);
        var icRt = icon.GetComponent<RectTransform>();
        icRt.anchorMin = icRt.anchorMax = new Vector2(0, 0.5f); icRt.pivot = new Vector2(0, 0.5f);
        icRt.anchoredPosition = new Vector2(22, 0);
        var icImg = icon.GetComponent<Image>(); icImg.preserveAspect = true; icImg.raycastTarget = false;
        icImg.enabled = false;                 // sprite 세팅 전엔 숨김(흰 박스 방지)
        SetRef(so, "headerIconImage", icImg);

        // 제목(설비 이름) - SetRef. 런타임 OpenFor(title)에서 채움.
        var title = MakeTMP("Title", header.transform, "설비", 24, TxtMain, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 66, 380, 40);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(0.86f, 0.90f, 0.96f, 0.5f), new Vector2(1f, -1f));
        SetRef(so, "machineTitleText", title);

        // 닫기 버튼 (우상단, ic_close + 호버 ColorTint)
        var closeBtnGo = MakeIconButton("CloseButton", header.transform, "ic_close", 48, Color.clear);
        AnchorRight(closeBtnGo.GetComponent<RectTransform>(), 12, 48, 48);
        TintIcon(closeBtnGo, TxtMain);
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtnGo.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null && closeSpr != null) closeIconImg.sprite = closeSpr;
        var closeBg = closeBtnGo.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced; closeBg.color = Color.white;
        var closeButton = closeBtnGo.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint; closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor      = new Color(1f, 1f, 1f, 0f);
        ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);
        ccb.pressedColor     = new Color(0.20f, 0.24f, 0.34f, 0.36f);
        ccb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor    = new Color(1f, 1f, 1f, 0f);
        ccb.colorMultiplier  = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "closeBtn", closeButton);

        // 헤더 밑 구분선 (밝은 표면 위라 또렷하게)
        var hair = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, HeaderHair);
        var hairRt = hair.GetComponent<RectTransform>();
        hairRt.anchorMin = new Vector2(0, 1); hairRt.anchorMax = new Vector2(1, 1); hairRt.pivot = new Vector2(0.5f, 1);
        hairRt.offsetMin = new Vector2(3, -headerH - 2); hairRt.offsetMax = new Vector2(-3, -headerH);
        hair.GetComponent<Image>().raycastTarget = false;

        // ── 단계 2~4: 좌 가방 / 중앙 생산부 / 하단 액션 ──
        BuildBag(prt, so);
        BuildProduction(prt, so);
        BuildFooter(prt, so);

        // 가방 칼럼과 생산부 사이 세로 구분선(같은 면 위 선 한 줄 = 큰 박스 아님).
        var vdiv = MakeImage("BagProdDivider", prt, Vector2.zero, Vector2.zero, HeaderHair);
        var vdRt = vdiv.GetComponent<RectTransform>();
        vdRt.anchorMin = new Vector2(0, 0); vdRt.anchorMax = new Vector2(0, 1); vdRt.pivot = new Vector2(0.5f, 0.5f);
        float vx = SidePad + BagWidth + Gap * 0.5f;
        vdRt.offsetMin = new Vector2(vx - 0.75f, FooterH + 8);
        vdRt.offsetMax = new Vector2(vx + 0.75f, -(HeaderH + 8));
        vdiv.GetComponent<Image>().raycastTarget = false;

        // 이전에 잘못 추가한 FactoryScreenDim 정리(이제 안 씀)
        var cv = panel.GetComponentInParent<Canvas>();
        if (cv != null) { var od = cv.transform.Find("FactoryScreenDim"); if (od != null) Object.DestroyImmediate(od.gameObject); }

        // ── uiPanel 배선 (재활용이면 자기 자신, 신규면 자식 패널) ──
        SetRef(so, "uiPanel", panel);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료",
            "공장 UI 전체(골격+블러+헤더 / 가방 / 생산부 / 하단 액션) 생성 완료.\n\n" +
            "Play 에서 설비 열어 동작 확인.\n" +
            "(중앙 설비 도면 PNG 는 추후 7종 연결 - 지금은 placeholder)\n" +
            "확인 후 Ctrl+S.", "확인");
    }

    // ── 블러 = 단일 표면(패널 자식 BlurredImage 한 겹) + 프로스트 3겹. 인벤 빌더 레시피 그대로 복제. ──
    static void BuildFrost(RectTransform prt, Sprite panelSprite)
    {
        if (panelSprite == null) return;
        const float inset = 3f, footerH = 60f, titleH = 64f;

        // 통합 블러 = 패널 자식 (별도 Screen Space-Camera 캔버스 없음). 둥근 패널 스프라이트 + Mask로 코너 일치.
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform));
        blurGo.transform.SetParent(prt, false);
        var blRt = blurGo.GetComponent<RectTransform>();
        blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one; blRt.offsetMin = Vector2.zero; blRt.offsetMax = Vector2.zero;
        var blur = blurGo.AddComponent<BlurredImage>();
        blur.sprite = panelSprite; blur.type = Image.Type.Sliced; blur.pixelsPerUnitMultiplier = 1f;
        blur.color = Color.white; blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = PickBuildCamera();
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            if (bs.blurSections != null) foreach (var sec in bs.blurSections) { sec.iterations = 5; sec.sampleDistance = 1.5f; }
            bs.vibrancy = 0f; bs.brightness = 0.02f; bs.contrast = 0f; bs.referenceResolution = 1080;
        }
        blur.Common.ValidateBlur();

        // 어두운 배경 = 풀사이즈(Mask 둥근 코너). 가장자리/코너로 비쳐 그림자 착시.
        var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
        var bgrt = bgGo.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
        var bgGrad = bgGo.AddComponent<UIFrostGradient>();
        // 위아래 거의 균일(바닥만 살짝) = 하나의 면. 옛날 바닥 0.52 는 아래가 어두운 별도 패널처럼 보였음.
        bgGrad.topColor = RGBA(22, 28, 40, 0.26f); bgGrad.bottomColor = RGBA(20, 26, 38, 0.32f);

        // 본문 밝은 표면 = 헤더 아래 ~ 바닥까지 한 면(엔필처럼 하나의 패널). 푸터를 따로 안 비움.
        // (옛날엔 offsetMin.y=footerH 라 바닥 60px 가 BgDark 만 남아 어두운 띠=별도 패널처럼 보였음.
        //  하단 액션바는 같은 면 위 divider 선으로만 구분한다.)
        var cardGo = MakeImage("BodyFrost", prt, Vector2.zero, Vector2.zero, Color.white);
        var crt = cardGo.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(inset, inset); crt.offsetMax = new Vector2(-inset, -titleH);
        var cImg = cardGo.GetComponent<Image>(); cImg.sprite = null; cImg.type = Image.Type.Simple; cImg.raycastTarget = false;
        var cGrad = cardGo.AddComponent<UIFrostGradient>();
        cGrad.topColor = RGBA(216, 224, 237, 0.34f); cGrad.bottomColor = RGBA(199, 209, 223, 0.26f);

        // 헤더 밝은 표면 = 상단(거의 흰색, 엔필처럼).
        var hbGo = MakeImage("HeaderFrost", prt, Vector2.zero, Vector2.zero, Color.white);
        var hbrt = hbGo.GetComponent<RectTransform>();
        hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1); hbrt.pivot = new Vector2(0.5f, 1);
        hbrt.offsetMin = new Vector2(inset, -titleH); hbrt.offsetMax = new Vector2(-inset, -inset);
        var hbImg = hbGo.GetComponent<Image>(); hbImg.sprite = null; hbImg.type = Image.Type.Simple; hbImg.raycastTarget = false;
        var hbGrad = hbGo.AddComponent<UIFrostGradient>();
        hbGrad.topColor = RGBA(245, 248, 253, 0.62f); hbGrad.bottomColor = RGBA(237, 243, 251, 0.56f);
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼 (인벤 빌더 InventoryUIBuilder 에서 복제 - 자기완결)
    // ─────────────────────────────────────────────────────────────────

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

    static GameObject MakeRounded(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = MakeImage(name, parent, size, pos, color);
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
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
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);   // 기본 중앙 앵커(호출측 anchoredPosition 기준 일관)
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.raycastTarget = false;
        return tmp;
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

    static void TintIcon(GameObject btn, Color c)
    {
        var icon = btn.transform.Find("Icon");
        if (icon != null) { var img = icon.GetComponent<Image>(); if (img != null) img.color = c; }
    }

    static void StretchTop(RectTransform rt, float h, float top, float side)
    { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.offsetMin = new Vector2(side, -top - h); rt.offsetMax = new Vector2(-side, -top); }

    static void StretchBottom(RectTransform rt, float h, float bottom, float side)
    { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.offsetMin = new Vector2(side, bottom); rt.offsetMax = new Vector2(-side, bottom + h); }

    static void AnchorLeft(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0); }

    static void AnchorRight(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(-x, 0); }

    // ── 스프라이트 로드 (인벤 빌더와 동일 임포트 교정) ──
    static Sprite LoadPanelSprite() => ConfigurePanelSprite(PanelSpritePath);
    static Sprite ConfigurePanelSprite(string path) => LoadPartSprite(path, new Vector4(PanelSlice, PanelSlice, PanelSlice, PanelSlice));

    static Sprite LoadPartSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[MachineUIBuilder] PNG 못 찾음: " + path); return null; }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

        var s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        if (s.spriteBorder != border || s.spriteMeshType != SpriteMeshType.FullRect)
        {
            s.spriteBorder = border; s.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(s); changed = true;
        }
        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadSpr(string n)
    {
        string p = SprDir + n + ".png";
        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
        if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    static Camera PickBuildCamera()
    {
        var main = Camera.main;
        if (main != null && main.targetTexture == null) return main;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null) return c;
        return main;
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = obj;
        else Debug.LogWarning("[MachineUIBuilder] 필드 없음: " + field);
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

    // ═════════════════════════════════════════════════════════════════
    // 단계 2 : 좌측 가방 (스크롤 그리드 + 드롭존)
    //   런타임 MachineUI.BuildInventorySlots 가 InventorySlot 프리팹을 content 에 채운다.
    // ═════════════════════════════════════════════════════════════════
    static void BuildBag(RectTransform prt, SerializedObject so)
    {
        // 좌 가방 칼럼: 좌측 고정폭(BagWidth), 헤더~푸터 세로 스트레치.
        var col = Region("BagColumn", prt, new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(SidePad, FooterH + Gap), new Vector2(SidePad + BagWidth, -(HeaderH + Gap)));
        var ct = col.transform;

        // ── 가방/창고 탭 + 용량 (칼럼 상단). 런타임 MachineUI 가 활성탭 알파 토글. ──
        var bagTab = MakeTextButton("BagTab", ct, "가방", Vector2.zero, new Vector2(96, 34), RGBA(150, 178, 205, 0.22f), Chrome, TxtMain, 16);
        var btRt = bagTab.GetComponent<RectTransform>();
        btRt.anchorMin = btRt.anchorMax = new Vector2(0, 1); btRt.pivot = new Vector2(0, 1); btRt.anchoredPosition = new Vector2(4, -4);
        SetRef(so, "bagTabBtn", bagTab);

        var stoTab = MakeTextButton("StorageTab", ct, "창고", Vector2.zero, new Vector2(96, 34), RGBA(150, 178, 205, 0.22f), Chrome, TxtMain, 16);
        var stoRt = stoTab.GetComponent<RectTransform>();
        stoRt.anchorMin = stoRt.anchorMax = new Vector2(0, 1); stoRt.pivot = new Vector2(0, 1); stoRt.anchoredPosition = new Vector2(106, -4);
        SetRef(so, "storageTabBtn", stoTab);

        // 활성 탭 노란 밑줄(런타임 SetTabActive 가 on/off). 기본 off.
        AddTabUnderline(bagTab.transform);
        AddTabUnderline(stoTab.transform);

        // 두 탭 사이 얇은 세로 divider
        var tabDiv = MakeImage("TabDivider", ct, new Vector2(1.5f, 22), new Vector2(101, -21), Chrome);
        var tdRt = tabDiv.GetComponent<RectTransform>();
        tdRt.anchorMin = tdRt.anchorMax = new Vector2(0, 1); tdRt.pivot = new Vector2(0.5f, 1);
        tabDiv.GetComponent<Image>().raycastTarget = false;

        // 용량 = 탭 행 우측 끝에 세로 중앙 정렬(탭과 한 줄).
        var cap = MakeTMP("BagCapacity", ct, "용량 0 / 35", 14, TxtSub, TextAlignmentOptions.Right);
        var capRt = cap.rectTransform; capRt.anchorMin = capRt.anchorMax = new Vector2(1, 1); capRt.pivot = new Vector2(1, 0.5f);
        capRt.sizeDelta = new Vector2(160, 22); capRt.anchoredPosition = new Vector2(-6, -21);
        SetRef(so, "bagCapacityText", cap);

        // ★큰 함몰박스(BagWell) 제거 = 슬롯이 한 면 위에 "구멍 뚫린" 것처럼 보이게(엔필식).
        //   탭/그리드 아래 얇은 divider 한 줄로만 구분(선 기반).
        var bagDiv = MakeImage("BagHeaderDivider", ct, Vector2.zero, Vector2.zero, HeaderHair);
        var bdRt = bagDiv.GetComponent<RectTransform>();
        bdRt.anchorMin = new Vector2(0, 1); bdRt.anchorMax = new Vector2(1, 1); bdRt.pivot = new Vector2(0.5f, 1);
        bdRt.offsetMin = new Vector2(2, -46); bdRt.offsetMax = new Vector2(-2, -45);
        bagDiv.GetComponent<Image>().raycastTarget = false;

        // 뷰포트 (RectMask2D + ScrollRect + 드롭존)
        var vpGo = new GameObject("BagViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        vpGo.transform.SetParent(ct, false);
        var vpRt = vpGo.GetComponent<RectTransform>();
        vpRt.anchorMin = new Vector2(0, 0); vpRt.anchorMax = new Vector2(1, 1);
        vpRt.offsetMin = new Vector2(8, 30); vpRt.offsetMax = new Vector2(-8, -52);   // 하단 30 = 드롭 힌트 자리
        var vpImg = vpGo.GetComponent<Image>(); vpImg.color = new Color(1, 1, 1, 0f); vpImg.raycastTarget = true;  // 드롭 raycast 캐치

        // 드롭존 (출력/재료/연료 슬롯 -> 가방 반환). highlightImage = 뷰포트 자신.
        var dz = vpGo.AddComponent<InventoryPanelDropZone>();
        var dzso = new SerializedObject(dz);
        SetRef(dzso, "highlightImage", vpImg);
        dzso.ApplyModifiedProperties();
        SetRef(so, "inventoryDropZone", dz);

        // Content (그리드). 칼럼 폭 고정이라 셀 크기 빌드시점 계산 가능.
        int cols = 4;
        Vector2 spacing = new Vector2(8, 8);
        float innerW = BagWidth - 16f;   // 뷰포트 좌우 offset 8+8
        float cell = Mathf.Floor((innerW - spacing.x * (cols - 1)) / cols);

        var contentGo = new GameObject("BagContent", typeof(RectTransform));
        var content = contentGo.GetComponent<RectTransform>();
        content.SetParent(vpRt, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero; content.anchoredPosition = Vector2.zero;

        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell); grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = cols;
        grid.childAlignment = TextAnchor.UpperCenter; grid.padding = new RectOffset(2, 2, 2, 2);

        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = vpGo.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f; scroll.viewport = vpRt; scroll.content = content;

        // 빈 가방 안내(아이템 0개일 때만 런타임이 표시). 뷰포트 위에 얹어 그리드 위로.
        var empty = MakeTMP("BagEmpty", vpGo.transform, "비어있음", 16, TxtSub, TextAlignmentOptions.Center);
        FillRect(empty.rectTransform);
        var ecol = empty.color; ecol.a = 0.42f; empty.color = ecol;
        empty.gameObject.SetActive(false);
        SetRef(so, "bagEmptyText", empty);

        // 하단 드롭 힌트(상시 흐리게) = 결과물 슬롯을 여기로 끌어 회수.
        var hint = MakeTMP("DropHint", ct, "결과물을 여기로 드래그해 회수", 13, TxtSub, TextAlignmentOptions.Center);
        var hRt = hint.rectTransform;
        hRt.anchorMin = new Vector2(0, 0); hRt.anchorMax = new Vector2(1, 0); hRt.pivot = new Vector2(0.5f, 0);
        hRt.offsetMin = new Vector2(8, 6); hRt.offsetMax = new Vector2(-8, 28);
        var hcol = hint.color; hcol.a = 0.5f; hint.color = hcol;

        SetRef(so, "inventorySlotParent", content);
        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        if (slotPrefab != null) SetRef(so, "inventorySlotPrefab", slotPrefab);
        else Debug.LogWarning("[MachineUIBuilder] InventorySlot 프리팹 못 찾음: " + SlotPrefabPath);
    }

    // ═════════════════════════════════════════════════════════════════
    // 단계 3 : 중앙 생산부 (무채색 도면판 + 재료/연료/게이지/출력 + 레시피 네비)
    // ═════════════════════════════════════════════════════════════════
    static void BuildProduction(RectTransform prt, SerializedObject so)
    {
        // 생산부: 가방 칼럼 오른쪽 ~ 패널 우측, 헤더~푸터 채움(스트레치).
        var pt = Region("Production", prt, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(SidePad + BagWidth + Gap, FooterH + Gap), new Vector2(-SidePad, -(HeaderH + Gap)));

        // ★큰 도면판(Blueprint) 제거 = 기계/슬롯이 한 면 위에 직접 얹힘(엔필식 "큰 박스로 안 덮음").
        //   투명 처리만(오브젝트는 남김). 중앙 비주얼은 추후 설비 도면 PNG 가 채운다.
        var plate = MakeImage("Blueprint", pt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        var plRt = plate.GetComponent<RectTransform>();
        plRt.anchorMin = Vector2.zero; plRt.anchorMax = Vector2.one; plRt.offsetMin = new Vector2(6, 6); plRt.offsetMax = new Vector2(-6, -6);
        plate.GetComponent<Image>().raycastTarget = false;

        // 가동 글로우 = 가공 중 기계 뒤 노란빛(런타임 알파 펄스). 기본 알파 0. 기계보다 먼저 생성 = 뒤.
        var glow = MakeImage("MachineGlow", pt, new Vector2(520, 520), new Vector2(0, 55f), new Color(Yellow.r, Yellow.g, Yellow.b, 0f));
        var glowImg = glow.GetComponent<Image>(); glowImg.sprite = RoundedSprite(); glowImg.type = Image.Type.Sliced; glowImg.raycastTarget = false;
        SetRef(so, "machineGlow", glowImg);

        // 설비 도면 = 퀵슬롯 설비 모델 렌더(500x500 투명, facilityId 1~7) 재사용.
        // 런타임 OpenFor 가 FacilityIconDatabase 로 sprite 세팅 -> enabled.
        var fac = CenterIcon("FacilityImage", pt, 440f);
        var facRt = fac.rectTransform; facRt.anchorMin = facRt.anchorMax = new Vector2(0.5f, 0.5f); facRt.anchoredPosition = new Vector2(0, 55f);
        fac.color = Color.white; fac.enabled = false;   // sprite 세팅 전엔 숨김(흰 박스 방지)
        SetRef(so, "facilityImage", fac);

        var slotFrame = LoadSlotFrame();

        // ── 레시피 네비 (상단 중앙) ──
        var nav = MakeEmpty("RecipeNav", pt, new Vector2(560, 40), Vector2.zero);
        var navRt = nav.GetComponent<RectTransform>();
        navRt.anchorMin = navRt.anchorMax = new Vector2(0.5f, 1); navRt.pivot = new Vector2(0.5f, 1); navRt.anchoredPosition = new Vector2(0, -6);
        var prevBtn = MakeMiniButton("RecipePrev", nav.transform, "<", new Vector2(-220, 0), 40f);
        SetRef(so, "recipePrevBtn", prevBtn);
        var idx = MakeTMP("RecipeIndex", nav.transform, "", 18, TxtMain, TextAlignmentOptions.Center);
        idx.fontStyle = FontStyles.Bold; idx.rectTransform.sizeDelta = new Vector2(90, 30); idx.rectTransform.anchoredPosition = new Vector2(-150, 0);
        SetRef(so, "recipeIndexText", idx);
        var nm = MakeTMP("RecipeName", nav.transform, "", 18, TxtMain, TextAlignmentOptions.Left);
        nm.rectTransform.sizeDelta = new Vector2(240, 30); nm.rectTransform.anchoredPosition = new Vector2(40, 0);
        SetRef(so, "recipeNameText", nm);
        var nextBtn = MakeMiniButton("RecipeNext", nav.transform, ">", new Vector2(230, 0), 40f);
        SetRef(so, "recipeNextBtn", nextBtn);

        // ── 재료 슬롯 — 기계 왼쪽 가까이 세로 스택(클러스터) ──
        var capMat = MakeTMP("CapMaterial", pt, "재료", 15, TxtSub, TextAlignmentOptions.Center);
        var cmRt = capMat.rectTransform; cmRt.anchorMin = cmRt.anchorMax = new Vector2(0.5f, 0.5f); cmRt.pivot = new Vector2(0.5f, 0.5f);
        cmRt.sizeDelta = new Vector2(110, 22); cmRt.anchoredPosition = new Vector2(-330, 305);
        CaptionBadge(pt, cmRt);
        var inputArea = MakeEmpty("InputArea", pt, new Vector2(110, 400), Vector2.zero);
        var iaRt = inputArea.GetComponent<RectTransform>();
        iaRt.anchorMin = iaRt.anchorMax = new Vector2(0.5f, 0.5f); iaRt.pivot = new Vector2(0.5f, 0.5f); iaRt.anchoredPosition = new Vector2(-330, 90);
        var vlg = inputArea.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = false; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        vlg.spacing = 12; vlg.childAlignment = TextAnchor.UpperCenter;

        var recipeSlots = new System.Collections.Generic.List<RecipeDropSlot>();
        for (int i = 0; i < 4; i++)
            recipeSlots.Add(MakeRecipeSlot(inputArea.transform, 88f, slotFrame));
        var arr = so.FindProperty("recipeDropSlots");
        if (arr != null)
        {
            arr.arraySize = recipeSlots.Count;
            for (int i = 0; i < recipeSlots.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = recipeSlots[i];
        }

        // 입력 -> 기계 흐름 표시(노란 셰브론). 입력 클러스터 우측, 기계 입구 방향.
        var inFlow = MakeTMP("InputFlow", pt, ">", 30, Yellow, TextAlignmentOptions.Center);
        inFlow.fontStyle = FontStyles.Bold;
        var ifRt = inFlow.rectTransform; ifRt.anchorMin = ifRt.anchorMax = new Vector2(0.5f, 0.5f); ifRt.pivot = new Vector2(0.5f, 0.5f);
        ifRt.sizeDelta = new Vector2(40, 40); ifRt.anchoredPosition = new Vector2(-250, 55);

        // ── 연료 슬롯 (재료 스택 아래) ──
        var capFuel = MakeTMP("CapFuel", pt, "연료", 15, TxtSub, TextAlignmentOptions.Center);
        var cfRt = capFuel.rectTransform; cfRt.anchorMin = cfRt.anchorMax = new Vector2(0.5f, 0.5f); cfRt.pivot = new Vector2(0.5f, 0.5f);
        cfRt.sizeDelta = new Vector2(110, 22); cfRt.anchoredPosition = new Vector2(-330, -130);
        CaptionBadge(pt, cfRt);
        var fuel = MakeFuelSlot(pt, 88f, slotFrame);
        var fRt = fuel.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = new Vector2(0.5f, 0.5f); fRt.pivot = new Vector2(0.5f, 0.5f); fRt.anchoredPosition = new Vector2(-330, -200);
        SetRef(so, "fuelDropSlot", fuel);

        // ── 진행 게이지 (중앙 가로 바) ──
        var gauge = MakeGauge(pt, new Vector2(380, 28), Vector2.zero);
        var gRt = gauge.GetComponent<RectTransform>();
        gRt.anchorMin = gRt.anchorMax = new Vector2(0.5f, 0.5f); gRt.pivot = new Vector2(0.5f, 0.5f); gRt.anchoredPosition = new Vector2(0, -205);
        SetRef(so, "processingGauge", gauge);

        // ── 상태 텍스트 (연료 부족/제작시간 클론 부모). 런타임이 (-14,98) 에 클론 띄움. ──
        var status = MakeTMP("StatusText", pt, "", 18, Color.white, TextAlignmentOptions.Center);
        status.fontStyle = FontStyles.Bold;
        var stRt = status.rectTransform; stRt.anchorMin = stRt.anchorMax = new Vector2(0.5f, 0.5f); stRt.pivot = new Vector2(0.5f, 0.5f);
        stRt.sizeDelta = new Vector2(360, 30); stRt.anchoredPosition = new Vector2(0, -255);
        SetRef(so, "statusText", status);

        // ── 출력 슬롯 (기계 오른쪽 가까이, 추가 출력은 런타임이 같은 부모에 stack) ──
        var capOut = MakeTMP("CapOutput", pt, "결과", 15, TxtSub, TextAlignmentOptions.Center);
        var coRt = capOut.rectTransform; coRt.anchorMin = coRt.anchorMax = new Vector2(0.5f, 0.5f); coRt.pivot = new Vector2(0.5f, 0.5f);
        coRt.sizeDelta = new Vector2(150, 22); coRt.anchoredPosition = new Vector2(345, 178);
        CaptionBadge(pt, coRt);
        var outputArea = MakeEmpty("OutputArea", pt, new Vector2(190, 180), Vector2.zero);
        var oaRt = outputArea.GetComponent<RectTransform>();
        oaRt.anchorMin = oaRt.anchorMax = new Vector2(0.5f, 0.5f); oaRt.pivot = new Vector2(0.5f, 0.5f); oaRt.anchoredPosition = new Vector2(345, 70);
        var hlg = outputArea.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter;
        var output = MakeOutputSlot(outputArea.transform, new Vector2(150, 170), slotFrame);
        SetRef(so, "outputSlot", output);
    }

    // ═════════════════════════════════════════════════════════════════
    // 단계 4 : 하단 액션 = 엔필식 액션바
    //   [재료회수 작게] [현재 생산 공식 스트립] ...... [모두받기 크게] + 맨아래 얇은 진행선
    // ═════════════════════════════════════════════════════════════════
    static void BuildFooter(RectTransform prt, SerializedObject so)
    {
        float rowY = 52f;   // 액션 행 중심(하단 가장자리 기준)

        // 하단 액션바 구분 = 같은 면 위 divider 한 줄(별도 패널 아님).
        var footDiv = MakeImage("FooterDivider", prt, Vector2.zero, Vector2.zero, HeaderHair);
        var fdRt = footDiv.GetComponent<RectTransform>();
        fdRt.anchorMin = new Vector2(0, 0); fdRt.anchorMax = new Vector2(1, 0); fdRt.pivot = new Vector2(0.5f, 0);
        fdRt.offsetMin = new Vector2(3, FooterH); fdRt.offsetMax = new Vector2(-3, FooterH + 1.5f);
        footDiv.GetComponent<Image>().raycastTarget = false;

        // 진행 바 = 맨 아래 얇은 전폭 선(노란 fill + 노브).
        var bar = MakeProgressBar(prt, Vector2.zero, new Vector2(480, 10));
        var brRt = bar.GetComponent<RectTransform>();
        brRt.anchorMin = new Vector2(0, 0); brRt.anchorMax = new Vector2(1, 0); brRt.pivot = new Vector2(0.5f, 0);
        brRt.offsetMin = new Vector2(SidePad, 12); brRt.offsetMax = new Vector2(-SidePad, 22);
        SetRef(so, "progressBar", bar);
        SetRef(so, "progressKnob", bar.transform.Find("Knob") as RectTransform);

        // 재료 회수 (하단 좌, 보조 작게)
        var takeIn = MakeTextButton("TakeInputsBtn", prt, "재료 회수", Vector2.zero, new Vector2(124, 38), RGBA(44, 56, 72, 0.55f), Chrome, Hex("dfe7f0"), 15);
        var tiRt = takeIn.GetComponent<RectTransform>();
        tiRt.anchorMin = tiRt.anchorMax = new Vector2(0, 0); tiRt.pivot = new Vector2(0, 0.5f); tiRt.anchoredPosition = new Vector2(SidePad, rowY);
        SetRef(so, "takeInputsBtn", takeIn);

        // 현재 생산 공식 스트립 (재료회수 우측, 딱 그 크기 개별 패널)
        BuildRecipeFormula(prt, so, new Vector2(SidePad + 124 + 12, rowY));

        // 모두 받기 (하단 우, 노랑 크게)
        var takeOut = MakeTextButton("TakeOutputBtn", prt, "모두 받기", Vector2.zero, new Vector2(240, 52), Yellow, YellowBd, YellowTx, 21);
        var toRt = takeOut.GetComponent<RectTransform>();
        toRt.anchorMin = toRt.anchorMax = new Vector2(1, 0); toRt.pivot = new Vector2(1, 0.5f); toRt.anchoredPosition = new Vector2(-SidePad, rowY);
        SetRef(so, "takeOutputBtn", takeOut);
    }

    // 현재 생산 공식 = 작은 개별 패널. [상태 라벨][재료아이콘 > 결과아이콘] 을 런타임이 채운다.
    static void BuildRecipeFormula(RectTransform prt, SerializedObject so, Vector2 pos)
    {
        var panel = MakeRounded("RecipeFormula", prt, new Vector2(560, 52), Vector2.zero, RGBA(20, 28, 40, 0.34f));
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0, 0); pr.pivot = new Vector2(0, 0.5f); pr.anchoredPosition = pos;
        AddOutline(panel, Chrome, new Vector2(1f, -1f));
        panel.GetComponent<Image>().raycastTarget = false;

        // 상태 라벨(좌) = 런타임이 생산중/대기중 토글.
        var st = MakeTMP("FormulaStatus", panel.transform, "대기 중", 13, TxtSub, TextAlignmentOptions.Left);
        var stRt = st.rectTransform; stRt.anchorMin = stRt.anchorMax = new Vector2(0, 0.5f); stRt.pivot = new Vector2(0, 0.5f);
        stRt.sizeDelta = new Vector2(58, 40); stRt.anchoredPosition = new Vector2(14, 0);
        st.fontStyle = FontStyles.Bold;
        SetRef(so, "formulaStatusText", st);

        // 공식 내용 컨테이너(상태 라벨 우측) = 런타임이 아이콘 엔트리 채움(HLG 좌측정렬).
        var content = MakeEmpty("FormulaContent", panel.transform, Vector2.zero, Vector2.zero);
        var cr = content.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 0); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(0, 0.5f);
        cr.offsetMin = new Vector2(78, 4); cr.offsetMax = new Vector2(-12, -4);
        var hlg = content.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 6; hlg.childAlignment = TextAnchor.MiddleLeft;
        SetRef(so, "formulaContent", content.transform);
    }

    // ─────────────────────────────────────────────────────────────────
    // 위젯 팩토리 (프리팹 없음 -> 코드 생성 + SerializedObject 내부 배선)
    // ─────────────────────────────────────────────────────────────────

    static RecipeDropSlot MakeRecipeSlot(Transform parent, float size, Sprite frame)
    {
        var go = MakeEmpty("RecipeSlot", parent, new Vector2(size, size), Vector2.zero);
        var rds = go.AddComponent<RecipeDropSlot>();          // RequireComponent(Image) -> 몸체
        var body = go.GetComponent<Image>();
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = SlotBody; body.raycastTarget = true;

        var rarity = FillImage("Rarity", go.transform, frame, Image.Type.Sliced, new Color(1, 1, 1, 0f));
        var icon = CenterIcon("Icon", go.transform, size * 0.62f);
        var glow = FillImage("Glow", go.transform, frame, Image.Type.Sliced, new Color(0.47f, 0.78f, 1f, 0f));
        var amount = MakeTMP("Amount", go.transform, "0/0", 16, Color.white, TextAlignmentOptions.BottomRight);
        BadgeRectBottom(amount.rectTransform); amount.fontStyle = FontStyles.Bold;
        var label = MakeTMP("Label", go.transform, "", 14, Color.white, TextAlignmentOptions.Center);
        FillRect(label.rectTransform);

        var wso = new SerializedObject(rds);
        SetRef(wso, "iconImage", icon); SetRef(wso, "borderImage", glow); SetRef(wso, "rarityBorder", rarity);
        SetRef(wso, "amountText", amount); SetRef(wso, "labelText", label);
        wso.ApplyModifiedProperties();
        return rds;
    }

    static FuelDropSlot MakeFuelSlot(Transform parent, float size, Sprite frame)
    {
        var go = MakeEmpty("FuelSlot", parent, new Vector2(size, size), Vector2.zero);
        var fds = go.AddComponent<FuelDropSlot>();            // RequireComponent(Image) -> 몸체
        var body = go.GetComponent<Image>();
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = SlotBody; body.raycastTarget = true;

        var border = FillImage("Border", go.transform, frame, Image.Type.Sliced, new Color(1, 1, 1, 0f));
        var icon = CenterIcon("Icon", go.transform, size * 0.58f);
        var amount = MakeTMP("Amount", go.transform, "", 16, Color.white, TextAlignmentOptions.TopRight);
        BadgeRectTop(amount.rectTransform); amount.fontStyle = FontStyles.Bold;
        var time = MakeTMP("Time", go.transform, "", 14, Hex("e6c24a"), TextAlignmentOptions.Bottom);
        var trt = time.rectTransform;
        trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 0); trt.pivot = new Vector2(0.5f, 0);
        trt.offsetMin = new Vector2(0, 12); trt.offsetMax = new Vector2(0, 30);   // 게이지 위로 올림

        // 연료 게이지(#41) = 슬롯 하단 얇은 노란 fill 바. 런타임이 fillAmount 갱신.
        var gaugeGo = new GameObject("Gauge", typeof(RectTransform), typeof(Image));
        gaugeGo.transform.SetParent(go.transform, false);
        var grt = gaugeGo.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0, 0); grt.anchorMax = new Vector2(1, 0); grt.pivot = new Vector2(0, 0);
        grt.offsetMin = new Vector2(6, 5); grt.offsetMax = new Vector2(-6, 10);
        var gimg = gaugeGo.GetComponent<Image>();
        gimg.sprite = RoundedSprite(); gimg.type = Image.Type.Filled;
        gimg.fillMethod = Image.FillMethod.Horizontal; gimg.fillOrigin = (int)Image.OriginHorizontal.Left;
        gimg.fillAmount = 0f; gimg.color = Yellow; gimg.raycastTarget = false; gimg.enabled = false;

        var label = MakeTMP("Label", go.transform, "", 13, Color.white, TextAlignmentOptions.Center);
        FillRect(label.rectTransform);

        var wso = new SerializedObject(fds);
        SetRef(wso, "iconImage", icon); SetRef(wso, "borderImage", border);
        SetRef(wso, "amountText", amount); SetRef(wso, "timeText", time); SetRef(wso, "labelText", label);
        SetRef(wso, "fuelGauge", gimg);
        // 빈 슬롯 실루엣을 순흑 -> 부드러운 반투명 슬레이트(밝은 UI에서 "낙서"처럼 튀던 것 완화).
        var silhProp = wso.FindProperty("emptySilhouetteColor");
        if (silhProp != null) silhProp.colorValue = RGBA(40, 52, 68, 0.5f);
        wso.ApplyModifiedProperties();
        return fds;
    }

    static MachineSlotWidget MakeOutputSlot(Transform parent, Vector2 size, Sprite frame)
    {
        var go = MakeEmpty("OutputSlot", parent, size, Vector2.zero);
        var msw = go.AddComponent<MachineSlotWidget>();       // RequireComponent 없음 -> Image 수동
        var body = go.AddComponent<Image>();
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = RGBA(30, 40, 54, 0.42f); body.raycastTarget = true;

        var rarity = FillImage("Rarity", go.transform, frame, Image.Type.Sliced, new Color(1, 1, 1, 0f));
        var icon = CenterIcon("Icon", go.transform, size.x * 0.56f);
        icon.rectTransform.anchoredPosition = new Vector2(0, 14f);
        var nameTx = MakeTMP("Name", go.transform, "", 15, Color.white, TextAlignmentOptions.Center);
        var nrt = nameTx.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
        nrt.offsetMin = new Vector2(2, 4); nrt.offsetMax = new Vector2(-2, 30);
        var amount = MakeTMP("Amount", go.transform, "", 16, Color.white, TextAlignmentOptions.TopRight);
        BadgeRectTop(amount.rectTransform); amount.fontStyle = FontStyles.Bold;

        var wso = new SerializedObject(msw);
        SetRef(wso, "iconImage", icon); SetRef(wso, "rarityBorder", rarity);
        SetRef(wso, "itemNameText", nameTx); SetRef(wso, "amountText", amount);
        wso.ApplyModifiedProperties();
        return msw;
    }

    static ProcessingGauge MakeGauge(Transform parent, Vector2 size, Vector2 pos)
    {
        var go = MakeEmpty("ProcessingGauge", parent, size, pos);
        var gauge = go.AddComponent<ProcessingGauge>();
        FillImage("ArrowTrack", go.transform, RoundedSprite(), Image.Type.Sliced, RGBA(16, 22, 32, 0.6f));   // index 0
        var fill = FillImage("ArrowFill", go.transform, RoundedSprite(), Image.Type.Sliced, Hex("e6c24a"), 2f); // index 1
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0f;

        var wso = new SerializedObject(gauge);
        SetRef(wso, "arrowFill", fill);
        wso.ApplyModifiedProperties();
        return gauge;
    }

    static Button MakeMiniButton(string name, Transform parent, string label, Vector2 pos, float size)
    {
        var go = MakeRounded(name, parent, new Vector2(size, size), pos, RGBA(44, 56, 72, 0.55f));
        AddOutline(go, Chrome, new Vector2(1f, -1f));
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        ApplyHover(btn);
        var t = MakeTMP("Text", go.transform, label, 22, Hex("dfe7f0"), TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold; FillRect(t.rectTransform);
        return btn;
    }

    static Button MakeTextButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color bg, Color bd, Color tx, float fontSize)
    {
        var go = MakeRounded(name, parent, size, pos, bg);
        AddOutline(go, bd, new Vector2(1f, -1f));
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        ApplyHover(btn);
        var t = MakeTMP("Text", go.transform, label, fontSize, tx, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold; FillRect(t.rectTransform);
        return btn;
    }

    // 활성 탭 강조용 노란 밑줄(런타임 SetTabActive 가 on/off). 기본 off, 버튼 하단 3px.
    static void AddTabUnderline(Transform tab)
    {
        var go = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(tab, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(6, 0); rt.offsetMax = new Vector2(-6, 3);
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        img.color = Yellow; img.raycastTarget = false;
        go.SetActive(false);
    }

    // 구역 캡션(재료/연료/결과) 뒤 얇은 알약 배경 = 작은 개별 패널(큰 박스 아님).
    static void CaptionBadge(Transform parent, RectTransform capRt)
    {
        var go = MakeRounded("CapBadge", parent, capRt.sizeDelta + new Vector2(20, 8), capRt.anchoredPosition, RGBA(20, 28, 40, 0.32f));
        go.GetComponent<Image>().raycastTarget = false;
        AddOutline(go, Chrome, new Vector2(1f, -1f));
        go.transform.SetAsFirstSibling();      // 캡션 글자 뒤로
    }

    // 호버/프레스 ColorTint(베이스색에 곱연산: 호버 살짝 밝게, 프레스 어둡게).
    static void ApplyHover(Button btn)
    {
        var c = btn.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        c.pressedColor     = new Color(0.88f, 0.88f, 0.88f, 1f);
        c.selectedColor    = Color.white;
        c.disabledColor    = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        c.colorMultiplier  = 1f; c.fadeDuration = 0.08f;
        btn.colors = c;
    }

    static Slider MakeProgressBar(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("ProgressBar", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;

        var bg = FillImage("Background", go.transform, RoundedSprite(), Image.Type.Sliced, RGBA(16, 22, 32, 0.62f));
        var fillArea = MakeEmpty("Fill Area", go.transform, Vector2.zero, Vector2.zero);
        FillRect(fillArea.GetComponent<RectTransform>(), 2f);
        var fill = FillImage("Fill", fillArea.transform, RoundedSprite(), Image.Type.Sliced, Hex("e6c24a"));
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0f;

        // 진행 노브 = fill 끝점 도트(런타임이 value 로 x 이동). 좌측 앵커 기준, 기본 off.
        var knob = MakeImage("Knob", go.transform, new Vector2(20, 20), Vector2.zero, Yellow);
        var kImg = knob.GetComponent<Image>(); kImg.sprite = RoundedSprite(); kImg.type = Image.Type.Sliced; kImg.raycastTarget = false;
        var kRt = knob.GetComponent<RectTransform>();
        kRt.anchorMin = kRt.anchorMax = new Vector2(0, 0.5f); kRt.pivot = new Vector2(0.5f, 0.5f);
        kRt.anchoredPosition = Vector2.zero;
        AddOutline(knob, YellowBd, new Vector2(1f, -1f));
        knob.SetActive(false);

        var slider = go.GetComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = null; slider.targetGraphic = bg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0f;
        slider.interactable = false;
        return slider;
    }

    // ── 위젯 공통 소품 ──

    // 패널 가장자리 기준 앵커 영역(빈 RectTransform). offMin=좌하, offMax=우상 오프셋.
    static RectTransform Region(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        return rt;
    }

    static Image FillImage(string name, Transform parent, Sprite spr, Image.Type type, Color col, float inset = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        var img = go.GetComponent<Image>();
        img.sprite = spr; img.type = type; img.color = col; img.raycastTarget = false;
        if (spr != null) img.pixelsPerUnitMultiplier = 1f;
        return img;
    }

    static Image CenterIcon(string name, Transform parent, float size)
    {
        var go = MakeImage(name, parent, new Vector2(size, size), Vector2.zero, Color.white);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false; img.preserveAspect = true;
        return img;
    }

    static void FillRect(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void BadgeRectBottom(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
        rt.offsetMin = new Vector2(0, 2); rt.offsetMax = new Vector2(-5, 26);
    }

    static void BadgeRectTop(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, -26); rt.offsetMax = new Vector2(-5, -2);
    }

    static Sprite LoadSlotFrame()
        => LoadPartSprite(SlotFramePath, new Vector4(SlotFrameSlice, SlotFrameSlice, SlotFrameSlice, SlotFrameSlice));
}
