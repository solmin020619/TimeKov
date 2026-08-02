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
    const string PartDir = "Assets/15.UI/New";                       // clggdesign 간유리 부품 PNG
    const string PanelSpritePath = PartDir + "/panel_ash_a78.png";   // 간유리 패널 표면(9-slice)
    const int PanelSlice = 56;
    const string SprDir = "Assets/15.UI/Inventory UI/sprites/";      // 아이콘 PNG 폴더

    // ── 색 (인벤 팔레트 복제 + 공장 노란 액센트) ──
    //   [07-02 교훈] 웜(베이지) 틴트 전면 교체는 오답이었음(안 어울림). 엔필의 톤 = 무색 유리 + 밝은 투과.
    //   팔레트는 인벤과 동일 유지, 밝기는 BgDark 그라데이션 bottom 알파(0.85 -> 0.62)로만 조절한다.
    static Color BaseDark   => RGBA(230, 223, 211, 0.38f);   // PNG 폴백색(정상시 패널이 PNG라 안쓰임)
    static Color TxtMain    => Hex("e9eef5");                // 밝은 텍스트(그래디언트 어두운 하단 위)
    static Color TxtSub     => Hex("9aabbf");                // 밝은 보조 텍스트
    static Color TxtDark    => Hex("232a33");                // 어두운 텍스트(그래디언트 밝은 상단용: 헤더/레시피명/용량)
    static Color Chrome     => RGBA(150, 178, 205, 0.26f);
    static Color HeaderHair => RGBA(120, 140, 170, 0.45f);   // 구분선(어두운 글라스 위 - 밝은 쿨 라인)
    // 공장 노란 액센트(버튼/진행바/입력화살표) - 단계 4에서 사용
    static Color Yellow     => Hex("e6c24a");
    static Color YellowBd   => Hex("b89a2e");
    static Color YellowTx   => Hex("4a3c0a");
    static Color SlotBody   => RGBA(26, 34, 46, 0.36f);     // 함몰 슬롯 몸체(쿨)
    static Color SlotBodySolid => RGBA(20, 26, 38, 0.9f);   // 불투명 슬롯 몸체 = 빈 칸도 또렷한 박스로 보임(인벤처럼). 연료 안보이던 문제 해결.
    static Color SlotEdge   => RGBA(150, 178, 205, 0.5f);   // 슬롯 중립 테두리(등급테두리 없는 연료용)
    static Color RailDim    => RGBA(170, 148, 82, 0.8f);    // 흐름 레일 비활성(엔필 idle 벨트처럼 또렷한 머스타드 골드). 활성은 더 밝은 Yellow.

    // ── 레이아웃 상수 (패널 = 스트레치 near-fullscreen, 영역은 패널 가장자리 기준 앵커) ──
    const float HeaderH = 64f, FooterH = 150f;  // 푸터=생산영역 하단 밴드(연료 140 슬롯 + 액션버튼 + 얇은 진행선). 가방 아래엔 안 깔림(가방이 풀높이).
    const float SidePad = 26f;       // 패널 안쪽 여백
    const float Gap = 20f;           // 영역 간 간격
    const float BagWidth = 620f;     // 좌 가방 칼럼 고정폭(엔필 비례로 확대. 셀 135 x 4열). MachineUI.SEC_ColW 와 동기 필수.

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
        var title = MakeTMP("Title", header.transform, "설비", 24, TxtDark, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 66, 380, 40);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(1f, 1f, 1f, 0.4f), new Vector2(1f, -1f));   // 밝은 헤일로(밝은 상단 위 어두운 글자)
        SetRef(so, "machineTitleText", title);

        // 닫기 버튼 (우상단, ic_close + 호버 ColorTint)
        var closeBtnGo = MakeIconButton("CloseButton", header.transform, "ic_close", 48, Color.clear);
        AnchorRight(closeBtnGo.GetComponent<RectTransform>(), 12, 48, 48);
        TintIcon(closeBtnGo, TxtDark);
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtnGo.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null && closeSpr != null) closeIconImg.sprite = closeSpr;
        var closeBg = closeBtnGo.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced; closeBg.color = Color.white;
        var closeButton = closeBtnGo.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint; closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor      = new Color(1f, 1f, 1f, 0f);
        ccb.highlightedColor = new Color(0f, 0f, 0f, 0.10f);    // 밝은 상단 = 어둡게 호버
        ccb.pressedColor     = new Color(0f, 0f, 0f, 0.18f);
        ccb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor    = new Color(1f, 1f, 1f, 0f);
        ccb.colorMultiplier  = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "closeBtn", closeButton);

        // 좌측 레일 탭 아이콘(클로드디자인 PNG, 흰색 = 런타임 틴트). 없으면 런타임 절차 도형 폴백.
        SetRef(so, "railBagSprite", LoadPartSprite(PartDir + "/tab_bag.png", Vector4.zero));
        SetRef(so, "railStorageSprite", LoadPartSprite(PartDir + "/tab_storage.png", Vector4.zero));
        // 드래그 대상 강조 프레임(통합 인벤과 동일 hl_region_frame) - 접힘 박스 물결 강조용.
        SetRef(so, "regionFrameSprite", LoadPartSprite(PartDir + "/hl_region_frame_open@2x.png", new Vector4(52, 52, 52, 52)));

        // 헤더 밑 구분선 (그래디언트 밝은 상단 위라 어두운 선으로 또렷하게)
        var hair = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, RGBA(70, 84, 104, 0.5f));
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
        vdRt.offsetMin = new Vector2(vx - 0.75f, 30);   // 가방 풀높이라 divider 도 하단까지(푸터 밴드는 우측 생산영역에만)
        vdRt.offsetMax = new Vector2(vx + 0.75f, -(HeaderH + 8));
        vdiv.GetComponent<Image>().raycastTarget = false;

        // 이전에 잘못 추가한 FactoryScreenDim 정리(이제 안 씀)
        var cv = panel.GetComponentInParent<Canvas>();
        if (cv != null) { var od = cv.transform.Find("FactoryScreenDim"); if (od != null) Object.DestroyImmediate(od.gameObject); }

        // ── uiPanel 배선 (재활용이면 자기 자신, 신규면 자식 패널) ──
        SetRef(so, "uiPanel", panel);

        // 절차 생성 스프라이트(게이지 점 등)는 에셋이 아니라 메모리 생성물이라 유니티 재시작 때 사라진다.
        // 재생성 키를 심어두면 실행 시 스스로 되살아난다.
        UIBuilderUtil.AttachGeneratedSpriteKeys(panel);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료",
            "공장 UI 전체(골격+블러+헤더 / 가방 / 생산부 / 하단 액션) 생성 완료.\n\n" +
            "Play 에서 설비 열어 동작 확인.\n" +
            "(중앙 설비 도면 PNG 는 추후 7종 연결 - 지금은 placeholder)\n" +
            "확인 후 Ctrl+S.", "확인");
    }

    // ── 블러 = 패널 자식 UIBlur 한 겹(프레임버퍼 직접 블러) + BgDark 그래디언트 한 겹. ──
    static void BuildFrost(RectTransform prt, Sprite panelSprite)
    {
        // 블러 = UIBlur 컴포넌트(데모 "Diverse Blurs" Tile3 = 종욱이 고른 룩, World 게임씬에 그대로 떨궈서 완벽 작동 검증).
        //   [진짜 원인] 우리가 쓰던 BlurredImage 는 blur RT 를 materialForRendering 으로 바인딩하는데 우리 빌드에선
        //   그게 폴백남(DefaultBlurMaterial = 그냥 반투명) -> 설정을 뭘 바꿔도 무반응 = "똑같은데"의 정체.
        //   UIBlur 는 Image 가 아니라 프레임버퍼 영역을 직접 흐려서 그 폴백 경로 자체가 없음 = 확실히 작동.
        //   Camera=null -> Camera.main (World 씬서 작동한 Tile3 와 동일, PickBuildCamera 는 폴백 위험이라 안 씀).
        //   코너는 사각(둥근 코너는 추후 별도 처리). 설정값은 World 씬서 검증된 Tile3 그대로.
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIBlur));
        blurGo.transform.SetParent(prt, false);
        var blRt = blurGo.GetComponent<RectTransform>();
        // [07-02] 인셋 16 -> 6: 블러는 직각이고 패널은 라운드라 코너가 삐져나오지 않게만 "티 안 나게" 살짝 인셋.
        //   16 이던 시절엔 그래디언트 bottom 0.85 가 테두리를 가려줬지만 0.62 로 밝히자 무블러 띠가 드러나
        //   "패널 두 개"처럼 보였음. 0 이면 모서리 사각 블러가 코너 밖으로 삐짐. 6 = 절충 확정.
        const float blurInset = 6f;
        blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one;
        blRt.offsetMin = new Vector2(blurInset, blurInset); blRt.offsetMax = new Vector2(-blurInset, -blurInset);
        var blur = blurGo.GetComponent<UIBlur>();
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = null;
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 0;
        blur.Common.blurStrength = 1f;
        // 데모 Tile3 검증값: downscale 5-Tap Star iter2 / blur Gaussian(3+3 Taps) iter4 / refRes 1080 / dither 0.25 / vibrancy 1.
        //   더 세게/약하게는 blurSections.iterations(4) / sampleDistance(1.5) 만 조절(downscale 은 그대로).
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            if (bs.downscaleSections != null) foreach (var sec in bs.downscaleSections) { sec.SetAlgorithm(BlurAlgorithm.Tap5Star); sec.iterations = 2; sec.sampleDistance = 1.5f; }
            if (bs.blurSections != null) foreach (var sec in bs.blurSections) { sec.SetAlgorithm(BlurAlgorithm.Gaussian); sec.horizontalSamplesPerSide = 1; sec.verticalSamplesPerSide = 1; sec.iterations = 4; sec.sampleDistance = 1.5f; }
            bs.blurAdditionalDistancePerIteration = 1f;
            bs.referenceResolution = 1080;
            bs.hqResample = false;
            bs.ditherStrength = 0.25f;
            bs.vibrancy = 1f; bs.brightness = 0f; bs.contrast = 0f;
        }
        blur.Common.ValidateBlur();

        // ★엔필식 = 단일 패널에 세로 그래디언트(위 밝게 -> 아래 어둡게) 한 겹. 블러+밝은/어두운 층 짜깁기 아님.
        //   배경 때문이 아니라 그래디언트가 본질이라 "어딜 가도 비슷". 구역은 divider 선으로만 나눈다.
        //   ★상단이 밝아서 헤더/레시피명 글자는 어둡게(TxtDark), 아래 어두운 데는 글자 밝게(TxtMain).
        var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
        var bgrt = bgGo.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
        var bgGrad = bgGo.AddComponent<UIFrostGradient>();
        // ★알파 확 낮춤 = 이게 높으면(0.6/0.72) 블러를 회색으로 덮어 죽인다(블러 안보임의 진범).
        //   블러가 비치게 낮은 톤만. (인벤도 0.26/0.52). 톤 부족하면 살짝 올림.
        // [07-02] top 순백 0.9 유지 / bottom 알파 0.85 -> 0.62 로 밝힘 = 인벤(0.52)처럼 블러 배경이 톤을 지배.
        //   설비 UI 가 어두워 보이던 진범이 bottom 0.85. 웜 RGB 틴트 실험은 오답이라 되돌림(무색 유리가 정답).
        bgGrad.topColor = RGBA(255, 255, 255, 0.9f); bgGrad.bottomColor = RGBA(18, 20, 26, 0.62f);
        bgGrad.topBias = 3f;   // 밝음 상단 ~30%로 퍼뜨림(높을수록 위로 쏠림).

        // (옛 밝은 BodyFrost/HeaderFrost 층 제거 = 어두운 글라스로 전환. 헤더/푸터는 divider 선으로만 구분.)
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
        // 좌 가방 칼럼: 좌측 고정폭(BagWidth). 헤더~하단 가까이까지 풀높이(엔필식: 가방은 끝까지, 푸터 밴드는 우측 생산영역 아래에만).
        var col = Region("BagColumn", prt, new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(SidePad, 28), new Vector2(SidePad + BagWidth, -(HeaderH + Gap)));
        var ct = col.transform;

        // ── 가방/창고 탭 + 용량 (칼럼 상단). 런타임 MachineUI 가 활성탭 알파 토글. ──
        var bagTab = MakeTextButton("BagTab", ct, "가방", Vector2.zero, new Vector2(96, 34), RGBA(236, 241, 248, 0.5f), Chrome, TxtDark, 16);
        var btRt = bagTab.GetComponent<RectTransform>();
        btRt.anchorMin = btRt.anchorMax = new Vector2(0, 1); btRt.pivot = new Vector2(0, 1); btRt.anchoredPosition = new Vector2(4, -4);
        SetRef(so, "bagTabBtn", bagTab);

        var stoTab = MakeTextButton("StorageTab", ct, "창고", Vector2.zero, new Vector2(96, 34), RGBA(236, 241, 248, 0.5f), Chrome, TxtDark, 16);
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
        var cap = MakeTMP("BagCapacity", ct, "용량 0 / 35", 14, TxtDark, TextAlignmentOptions.Right);
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

        // (하단 "결과물을 여기로 드래그해 회수" 힌트 = 이중 섹션으로 바뀌며 자리가 접힘 박스로 넘어갔다.
        //  런타임이 계속 끄고 있던 것을 제거함.)

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

        // (가동 글로우 = 패널을 노랗게 덮어 거슬린다는 판정으로 런타임이 알파 0 고정이었다. 제거함.)

        // 설비 도면 = 퀵슬롯 설비 모델 렌더(500x500 투명, facilityId 1~7) 재사용.
        // 런타임 OpenFor 가 FacilityIconDatabase 로 sprite 세팅 -> enabled.
        var fac = CenterIcon("FacilityImage", pt, 720f);   // 기계 PNG 크게(생산영역 채우게). 슬롯/레일은 별도 앵커라 PNG 키워도 안 움직임.
        var facRt = fac.rectTransform; facRt.anchorMin = facRt.anchorMax = new Vector2(0.5f, 0.5f); facRt.anchoredPosition = new Vector2(0, 0f);
        fac.color = Color.white; fac.enabled = false;   // sprite 세팅 전엔 숨김(흰 박스 방지)
        SetRef(so, "facilityImage", fac);

        // ── 공정 흐름 레일 컨테이너 (런타임 BuildFlowRails 가 설비 포트수/레시피에 맞춰 채움). ──
        //   기계 PNG 뒤가 아니라 앞·슬롯 뒤(여기 생성 순서). 0크기 = 생산영역 중심 기준 좌표.
        var flowRails = MakeEmpty("FlowRails", pt, Vector2.zero, Vector2.zero);
        var frRt = flowRails.GetComponent<RectTransform>();
        frRt.anchorMin = frRt.anchorMax = frRt.pivot = new Vector2(0.5f, 0.5f);
        frRt.sizeDelta = Vector2.zero; frRt.anchoredPosition = Vector2.zero;
        SetRef(so, "flowRailsRoot", frRt);

        var slotFrame = LoadSlotFrame();

        // ── 레시피 네비 (상단 중앙). 화살표 좌우 대칭 + index/name 은 HLG 로 정중앙 정렬(이름 길이 무관). ──
        var nav = MakeEmpty("RecipeNav", pt, new Vector2(600, 40), Vector2.zero);
        var navRt = nav.GetComponent<RectTransform>();
        navRt.anchorMin = navRt.anchorMax = new Vector2(0.5f, 1); navRt.pivot = new Vector2(0.5f, 1); navRt.anchoredPosition = new Vector2(0, -6);
        var prevBtn = MakeMiniButton("RecipePrev", nav.transform, "<", new Vector2(-250, 0), 40f);
        SetRef(so, "recipePrevBtn", prevBtn);
        var nextBtn = MakeMiniButton("RecipeNext", nav.transform, ">", new Vector2(250, 0), 40f);
        SetRef(so, "recipeNextBtn", nextBtn);

        // index + name 을 가운데 컨테이너(HLG)로 묶어 화살표 사이 정중앙. childControl + name 의 ContentSizeFitter 라 이름 길이 달라도 중앙 유지.
        var navCenter = MakeEmpty("RecipeNavCenter", nav.transform, new Vector2(420, 36), Vector2.zero);
        var ncHlg = navCenter.AddComponent<HorizontalLayoutGroup>();
        ncHlg.childControlWidth = true; ncHlg.childControlHeight = true;
        ncHlg.childForceExpandWidth = false; ncHlg.childForceExpandHeight = false;
        ncHlg.spacing = 14; ncHlg.childAlignment = TextAnchor.MiddleCenter;
        var idx = MakeTMP("RecipeIndex", navCenter.transform, "", 18, TxtDark, TextAlignmentOptions.Center);
        idx.fontStyle = FontStyles.Bold;
        var idxLE = idx.gameObject.AddComponent<LayoutElement>(); idxLE.minWidth = 46f; idxLE.preferredHeight = 30f;
        SetRef(so, "recipeIndexText", idx);
        var nm = MakeTMP("RecipeName", navCenter.transform, "", 18, TxtDark, TextAlignmentOptions.Left);
        var nmFit = nm.gameObject.AddComponent<ContentSizeFitter>();
        nmFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; nmFit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        var nmLE = nm.gameObject.AddComponent<LayoutElement>(); nmLE.preferredHeight = 30f;
        SetRef(so, "recipeNameText", nm);

        // ── 재료 슬롯 — 기계 왼쪽 가까이 세로 스택(클러스터) ──
        // (재료/결과 라벨 제거 - 레일로 흐름 보이면 자명. 종욱 지시.)
        var inputArea = MakeEmpty("InputArea", pt, new Vector2(140, 400), Vector2.zero);
        var iaRt = inputArea.GetComponent<RectTransform>();
        iaRt.anchorMin = iaRt.anchorMax = new Vector2(0.5f, 0.5f); iaRt.pivot = new Vector2(0.5f, 0.5f); iaRt.anchoredPosition = new Vector2(-155, 0);   // 기계 왼쪽에 얹음(PNG 위)
        var vlg = inputArea.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = false; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        vlg.spacing = 12; vlg.childAlignment = TextAnchor.MiddleCenter;   // 기계 높이에 가운데정렬

        var recipeSlots = new System.Collections.Generic.List<RecipeDropSlot>();
        for (int i = 0; i < 5; i++)   // 설비 입력 포트 최대 5개(5x5). 런타임이 inputSlotCount 만큼만 활성.
            recipeSlots.Add(MakeRecipeSlot(inputArea.transform, 140f, slotFrame));
        var arr = so.FindProperty("recipeDropSlots");
        if (arr != null)
        {
            arr.arraySize = recipeSlots.Count;
            for (int i = 0; i < recipeSlots.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = recipeSlots[i];
        }

        // (중앙 화살표 + 입력/출력 레일 = 런타임 BuildFlowRails 가 그림. 빌더는 컨테이너만.)

        // (연료 슬롯은 BuildFooter 로 이동 = 옛 "현재 생산 공식 스트립" 자리.)

        // ── 깔끔 게이지 (도면 아래 얇은 선 + 양끝 점 + 좌->우 채움 + 그 위 "N초") ──
        // 옛 노란 트레이 게이지(ProcessingGauge)/하단 슬라이더를 대체한 최종안. 자리는 그대로 물려받는다.
        BuildCleanGauge(pt, so, new Vector2(0, -205));

        // ── 상태 텍스트 (연료 부족 전용. 제작 시간은 게이지 위 ProcessTimeText 가 담당). ──
        var status = MakeTMP("StatusText", pt, "", 18, Color.white, TextAlignmentOptions.Center);
        status.fontStyle = FontStyles.Bold;
        var stRt = status.rectTransform; stRt.anchorMin = stRt.anchorMax = new Vector2(0.5f, 0.5f); stRt.pivot = new Vector2(0.5f, 0.5f);
        stRt.sizeDelta = new Vector2(360, 30); stRt.anchoredPosition = new Vector2(0, -255);
        SetRef(so, "statusText", status);

        // ── 출력 슬롯 (기계 오른쪽 가까이, 추가 출력은 런타임이 같은 부모에 stack) ──
        // (결과 라벨 제거)
        var outputArea = MakeEmpty("OutputArea", pt, new Vector2(190, 180), Vector2.zero);
        var oaRt = outputArea.GetComponent<RectTransform>();
        oaRt.anchorMin = oaRt.anchorMax = new Vector2(0.5f, 0.5f); oaRt.pivot = new Vector2(0.5f, 0.5f); oaRt.anchoredPosition = new Vector2(155, 0);   // 기계 오른쪽에 얹음
        var hlg = outputArea.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 8; hlg.childAlignment = TextAnchor.MiddleCenter;
        var output = MakeOutputSlot(outputArea.transform, new Vector2(140, 140), slotFrame);   // 입력칸과 동일 크기(엔필: 칸 크기 전부 같음)
        SetRef(so, "outputSlot", output);

        // (출력 흐름 레일 = 런타임 BuildFlowRails 가 그림.)
    }

    // ═════════════════════════════════════════════════════════════════
    // 단계 4 : 하단 액션 = 엔필식 액션바
    //   [재료회수 작게] [현재 생산 공식 스트립] ...... [모두받기 크게] + 맨아래 얇은 진행선
    // ═════════════════════════════════════════════════════════════════
    static void BuildFooter(RectTransform prt, SerializedObject so)
    {
        // 푸터 밴드 = 생산영역(우측) 하단에만. 가방칼럼 아래엔 안 깔림(가방이 풀높이 = 빈칸 없음).
        float prodLeft = SidePad + BagWidth + Gap;   // 생산영역 좌측 시작 x(패널 좌측 기준)

        // (맨 아래 얇은 진행 슬라이더 + 노브 = 깔끔 게이지로 대체돼 런타임이 항상 껐다. 제거함.)

        // 연료 슬롯 (하단 좌 = 생산영역 시작점). 재료칸과 동일 140 크기/모양 = 복붙, 로직만 연료.
        var fuelFrame = LoadSlotFrame();
        var fuel = MakeFuelSlot(prt, 140f, fuelFrame);
        var fRt = fuel.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = new Vector2(0, 0); fRt.pivot = new Vector2(0, 0);
        fRt.anchoredPosition = new Vector2(prodLeft + 6f, 28f);
        SetRef(so, "fuelDropSlot", fuel);

        // "연료" 캡션 = 슬롯 위 작은 라벨.
        var capFuel = MakeTMP("CapFuel", prt, "연료", 14, TxtSub, TextAlignmentOptions.Center);
        var cfRt = capFuel.rectTransform; cfRt.anchorMin = cfRt.anchorMax = new Vector2(0, 0); cfRt.pivot = new Vector2(0, 0);
        cfRt.sizeDelta = new Vector2(140, 20); cfRt.anchoredPosition = new Vector2(prodLeft + 6f, 170f);

        // 액션 버튼 = 우측 한 줄, 바닥선은 연료 슬롯과 동일(28). 위가 밝은 그라데이션으로 입체감.
        // 주버튼(모두 받기) = 노란색 금지(종욱) -> 벨트 시안 계열 + 어두운 글자. 보조(재료 회수) = 밝은 간유리.
        var takeOut = MakeTextButton("TakeOutputBtn", prt, "모두 받기", Vector2.zero, new Vector2(280, 64), Yellow, YellowBd, YellowTx, 22);
        StyleActionButton(takeOut, new Vector2(280, 64), new Vector2(-26f, 28f),
            new Color(0.30f, 0.72f, 0.95f, 1f), new Color(0.03f, 0.10f, 0.16f, 1f), 22);
        SetRef(so, "takeOutputBtn", takeOut);

        var takeIn = MakeTextButton("TakeInputsBtn", prt, "재료 회수", Vector2.zero, new Vector2(170, 64), RGBA(44, 56, 72, 0.55f), Chrome, Hex("dfe7f0"), 18);
        StyleActionButton(takeIn, new Vector2(170, 64), new Vector2(-(26f + 280f + 12f), 28f),
            new Color(0.92f, 0.95f, 0.98f, 0.16f), new Color(0.92f, 0.95f, 0.98f, 0.92f), 18);
        SetRef(so, "takeInputsBtn", takeIn);
    }

    // 하단 액션 버튼 최종 스타일. 예전엔 런타임 Awake 가 이걸 덮어써서 인스펙터 값이 실제 값이 아니었다.
    static void StyleActionButton(Button b, Vector2 size, Vector2 pos, Color bg, Color txt, float fontSize)
    {
        if (b == null) return;
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        if (b.image != null)
        {
            b.image.color = bg;
            var g = b.image.GetComponent<UIFrostGradient>();
            if (g == null) g = b.image.gameObject.AddComponent<UIFrostGradient>();
            g.topColor = new Color(1f, 1f, 1f, 1f);
            g.bottomColor = new Color(0.58f, 0.58f, 0.58f, 1f);   // 아래로 가라앉는 셰이딩 = 입체감
        }
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.color = txt; tmp.fontSize = fontSize; }
    }

    // 깔끔 게이지: 트랙(흐린 선) + 채움(밝은 선) + 양끝 점 + 그 위 "N초" 텍스트.
    static void BuildCleanGauge(Transform parent, SerializedObject so, Vector2 pos)
    {
        const float GA_W = 300f, GA_H = 3f;
        var root = MakeEmpty("CleanGauge", parent, new Vector2(GA_W, 22f), pos);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(GA_W, 22f); rrt.anchoredPosition = pos;
        SetRef(so, "_gaugeRoot", rrt);

        GaugePart("Track", rrt, new Vector2(GA_W, GA_H), Vector2.zero, new Color(1f, 1f, 1f, 0.28f), null);
        var fill = GaugePart("Fill", rrt, new Vector2(0f, GA_H), new Vector2(-GA_W * 0.5f, 0f), new Color(1f, 1f, 1f, 0.95f), null);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);   // 좌->우로 자람
        SetRef(so, "_gaugeFill", fill);
        GaugePart("DotL", rrt, new Vector2(8f, 8f), new Vector2(-GA_W * 0.5f, 0f), new Color(1f, 1f, 1f, 0.9f), CircleSprite());
        GaugePart("DotR", rrt, new Vector2(8f, 8f), new Vector2(GA_W * 0.5f, 0f), new Color(1f, 1f, 1f, 0.9f), CircleSprite());

        // 제작 시간 "N초" = 게이지 바로 위 중앙. 예전엔 StatusText 를 런타임 복제해 썼다.
        var t = MakeTMP("ProcessTimeText", rrt, "", 18, Color.white, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        var trt = t.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(220f, 26f); trt.anchoredPosition = new Vector2(0f, 20f);
        SetRef(so, "_processTimeText", t);
    }

    static Image GaugePart(string name, Transform parent, Vector2 size, Vector2 pos, Color color, Sprite sprite)
    {
        var go = MakeEmpty(name, parent, size, pos);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    // 게이지 양끝 점(원). 런타임 MachineUI.CircleSprite 와 같은 모양.
    static Sprite _circle;
    static Sprite CircleSprite()
    {
        if (_circle == null) _circle = UISpriteFactory.Circle(64);
        return _circle;
    }

    // (옛 "현재 생산 공식 스트립" 패널 = 어느 시점부터 호출이 끊겨 씬에 아예 안 만들어졌고,
    //  런타임 BuildFormula 도 참조가 null 이라 항상 no-op 이었다. 그 자리는 연료 슬롯이 가져갔다. 제거함.)

    // ─────────────────────────────────────────────────────────────────
    // 위젯 팩토리 (프리팹 없음 -> 코드 생성 + SerializedObject 내부 배선)
    // ─────────────────────────────────────────────────────────────────

    static RecipeDropSlot MakeRecipeSlot(Transform parent, float size, Sprite frame)
    {
        var go = MakeEmpty("RecipeSlot", parent, new Vector2(size, size), Vector2.zero);
        var rds = go.AddComponent<RecipeDropSlot>();          // RequireComponent(Image) -> 몸체
        var body = go.GetComponent<Image>();
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = SlotBodySolid; body.raycastTarget = true;
        AddOutline(go, SlotEdge, new Vector2(1f, -1f));   // 깔끔한 얇은 테두리(옛 장식 프레임 hl_slot_frame 제거 = "더러움" 해결)

        var aurora = AddGradeAurora(go.transform, size * 0.34f);   // 하단 등급 오로라(인벤과 동일, 아이콘 뒤)
        var gradeBar = AddGradeBar(go.transform);                  // 바닥 솔리드 등급선(인벤과 통일)
        var glow = FillImage("Glow", go.transform, RoundedSprite(), Image.Type.Sliced, new Color(0.37f, 0.77f, 1f, 0f));   // 드래그 호버 글로우(라운드 오버레이, 아이콘 뒤)
        var icon = CenterIcon("Icon", go.transform, size * 0.85f);   // 인벤 비율과 통일(종욱: 슬롯의 85% 채우기)
        var amount = MakeTMP("Amount", go.transform, "0/0", 16, Color.white, TextAlignmentOptions.BottomRight);
        BadgeRectBottom(amount.rectTransform); amount.fontStyle = FontStyles.Bold;
        var label = MakeTMP("Label", go.transform, "", 14, Color.white, TextAlignmentOptions.Center);
        FillRect(label.rectTransform);

        // (슬롯별 레일 제거 = 포트/버스/레일 구조는 런타임 MachineUI.BuildFlowRails 가 별도로 그린다.)

        var wso = new SerializedObject(rds);
        SetRef(wso, "iconImage", icon); SetRef(wso, "borderImage", glow);
        SetRef(wso, "gradeAurora", aurora); SetRef(wso, "rarityBorder", gradeBar);
        SetRef(wso, "amountText", amount); SetRef(wso, "labelText", label);
        wso.ApplyModifiedProperties();
        return rds;
    }

    static FuelDropSlot MakeFuelSlot(Transform parent, float size, Sprite frame)
    {
        var go = MakeEmpty("FuelSlot", parent, new Vector2(size, size), Vector2.zero);
        var fds = go.AddComponent<FuelDropSlot>();            // RequireComponent(Image) -> 몸체
        var body = go.GetComponent<Image>();
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = SlotBodySolid; body.raycastTarget = true;
        AddOutline(go, SlotEdge, new Vector2(1f, -1f));   // 깔끔한 얇은 테두리(장식 프레임 제거 = "더러움" 해결). 등급/연료색은 하단 오로라가 담당.

        var aurora = AddGradeAurora(go.transform, size * 0.34f);   // 하단 연료색 오로라(브론즈)
        var border = FillImage("Border", go.transform, RoundedSprite(), Image.Type.Sliced, new Color(1, 1, 1, 0f));   // 호버 글로우(라운드 오버레이)
        var icon = CenterIcon("Icon", go.transform, size * 0.85f);   // 재료칸(MakeRecipeSlot)과 동일 비율 = 복붙 느낌
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
        // 게이지 = 연료 슬롯의 "바닥 선" 역할(등급선과 동일 지오메트리: 슬롯 폭 딱 맞게 h6, 맨 아래).
        grt.anchorMin = new Vector2(0, 0); grt.anchorMax = new Vector2(1, 0); grt.pivot = new Vector2(0, 0);
        grt.offsetMin = new Vector2(0, 0); grt.offsetMax = new Vector2(0, 6);
        var gimg = gaugeGo.GetComponent<Image>();
        gimg.sprite = RoundedSprite(); gimg.type = Image.Type.Filled;
        gimg.fillMethod = Image.FillMethod.Horizontal; gimg.fillOrigin = (int)Image.OriginHorizontal.Left;
        gimg.fillAmount = 0f; gimg.color = Yellow; gimg.raycastTarget = false; gimg.enabled = false;

        var label = MakeTMP("Label", go.transform, "", 13, Color.white, TextAlignmentOptions.Center);
        FillRect(label.rectTransform);

        var wso = new SerializedObject(fds);
        SetRef(wso, "iconImage", icon); SetRef(wso, "borderImage", border);
        SetRef(wso, "amountText", amount); SetRef(wso, "timeText", time); SetRef(wso, "labelText", label);
        SetRef(wso, "fuelGauge", gimg); SetRef(wso, "gradeAurora", aurora);
        SetRef(wso, "highlightFrameSprite", LoadPartSprite(PartDir + "/hl_region_frame_open@2x.png", new Vector4(52, 52, 52, 52)));
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
        body.sprite = RoundedSprite(); body.type = Image.Type.Sliced; body.color = SlotBodySolid; body.raycastTarget = true;
        AddOutline(go, SlotEdge, new Vector2(1f, -1f));   // 깔끔한 얇은 테두리(장식 프레임 제거)

        var aurora = AddGradeAurora(go.transform, size.x * 0.34f);   // 하단 등급 오로라(인벤과 동일)
        var gradeBar = AddGradeBar(go.transform);                    // 바닥 솔리드 등급선(인벤과 통일)
        var icon = CenterIcon("Icon", go.transform, size.x * 0.85f);   // 인벤 비율과 통일
        icon.rectTransform.anchoredPosition = new Vector2(0, 6f);     // 85% 아이콘이 칸 위로 안 삐지는 오프셋(하단 이름줄 살짝 겹침은 텍스트가 위층이라 OK)
        var nameTx = MakeTMP("Name", go.transform, "", 15, Color.white, TextAlignmentOptions.Center);
        var nrt = nameTx.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 0); nrt.pivot = new Vector2(0.5f, 0);
        nrt.offsetMin = new Vector2(2, 4); nrt.offsetMax = new Vector2(-2, 30);
        var amount = MakeTMP("Amount", go.transform, "", 16, Color.white, TextAlignmentOptions.TopRight);
        BadgeRectTop(amount.rectTransform); amount.fontStyle = FontStyles.Bold;

        var wso = new SerializedObject(msw);
        SetRef(wso, "iconImage", icon);
        SetRef(wso, "gradeAurora", aurora); SetRef(wso, "rarityBorder", gradeBar);
        SetRef(wso, "itemNameText", nameTx); SetRef(wso, "amountText", amount);
        wso.ApplyModifiedProperties();
        return msw;
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

    // 등급 오로라 = 인벤 슬롯과 동일(하단서 위로 번지는 그라데이션). 색은 런타임 위젯이 등급/연료색으로 Image.color 틴트.
    //   UIFrostGradient: 아래 흰 1 -> 위 투명 (Image.color 에 곱해짐). 칸 아래에 깔리는 "무지개" 글로우.
    static Image AddGradeAurora(Transform slot, float height)
    {
        var go = new GameObject("GradeAurora", typeof(RectTransform), typeof(Image), typeof(UIFrostGradient));
        go.transform.SetParent(slot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2(0, height); rt.anchoredPosition = new Vector2(0, 6f);
        var img = go.GetComponent<Image>(); img.sprite = null; img.color = new Color(1, 1, 1, 0f); img.raycastTarget = false;
        var grad = go.GetComponent<UIFrostGradient>(); grad.topColor = new Color(1, 1, 1, 0f); grad.bottomColor = new Color(1, 1, 1, 1f);
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

    // 등급 바닥 선 = 슬롯 폭에 딱 맞는 솔리드 직선(h6). 인벤 InventorySlotUI 와 동일 지오메트리로 전 슬롯 통일.
    //   색은 런타임 위젯(rarityBorder)이 등급색으로 칠하고, 빈 칸은 투명. 오로라(y6~)가 이 선 위에서 시작한다.
    static Image AddGradeBar(Transform slot)
    {
        var go = new GameObject("GradeBar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0, 6f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = false;
        return img;
    }
}
