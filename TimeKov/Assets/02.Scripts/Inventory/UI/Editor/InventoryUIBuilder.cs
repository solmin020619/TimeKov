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
    const string WarehouseDir = "Assets/11.UI/Warehouse/sprites/";   // 창고 전용 부품 PNG (ic_warehouse/ic_trash/ic_deposit_all/cat_*/tab_selected)

    // 카테고리 7 아이콘 (HANDOFF §10 순서 = 전체/원재료/1차/2차/전술/코어/특수)
    static readonly string[] CatIcons =
        { "cat_all", "cat_raw", "cat_primary", "cat_secondary", "cat_tactical", "cat_core", "cat_special" };

    // HANDOFF §3 색
    const string BlurCanvasName = "InventoryBlurCanvas";
    const float BagRightX = 500f;   // 가방을 화면 중앙에서 오른쪽으로. 패널/블러 같은 값으로 정렬 (값 키울수록 더 오른쪽)
    // clggdesign 따뜻한 간유리 패널 표면 (9-slice). 톤을 배경무관 고정 + 블러는 뒤에서 깊이만.
    // 알파 강화 원하면 a82/a88 파일로 경로만 바꾸면 됨.
    const string PartDir = "Assets/11.UI/New";   // clggdesign 부품 PNG 폴더 (패널 변형 + header_bar/grade_bar/divider/scrollbar/icons)
    const string PanelSpritePath = PartDir + "/panel_ash_a78.png";   // 기본 = 밝은 중성 쿨(ash). F9로 steel/cool 비교
    const int PanelSlice = 56;
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
    static Color BaseDark   => RGBA(230, 223, 211, 0.38f);  // PNG 못 찾을때 폴백색. 정상시엔 패널이 PNG라 안쓰임.
    static Color BandHead   => RGBA(150, 165, 185, 0.18f);  // 헤더/푸터 밴드 = 쿨 라이트(프레임 또렷). 인셋26으로 코너 회피.
    static Color BandBody   => RGBA(232, 225, 213, 0.00f);  // 본문 투명 (PNG가 표면 담당, 이중 틴트 방지)
    static Color Hairline   => RGBA(20, 24, 30, 0.50f);     // 헤더 밑 1px 선 (밝은 위라 어둡게)
    static Color BtnLight   => RGBA(255, 255, 255, 0.40f);  // 하단 버튼(밝은 무채색)
    static Color BtnLightBd => RGBA(255, 255, 255, 0.50f);
    static Color SlotFill   => RGBA(12, 16, 24, 0.10f);    // 칸 = 살짝만 어둡게(마진보다 약간 진해 칸 음각) + 블러 통과. 정의는 4변 테두리가 담당.
    static Color SlotEmptyC => RGBA(12, 16, 24, 0.22f);     // 빈 칸 더 어둡게
    static Color SlotBorder => RGBA(8, 10, 14, 0.85f);      // 슬롯 테두리 기본
    // 칸 음각용 비대칭 엣지 + 크롬 (쿨)
    static Color SlotEdgeDark  => RGBA(12, 16, 24, 0.22f);  // (구) 비대칭 음각 - 밝은 패널선 안 보여 폐기
    static Color SlotEdgeLight => RGBA(150, 165, 188, 0.22f);
    static Color SlotLine      => RGBA(40, 54, 74, 0.45f);  // 칸 4변 균일 테두리(쿨 슬레이트). 밝은 패널 위에서 격자 또렷.
    static Color HeaderHair   => RGBA(170, 190, 212, 0.28f); // 헤더/푸터 구분선 (쿨 라이트)
    static Color PillDark     => RGBA(16, 20, 28, 0.55f);   // 닫기/액션 pill 다크 배경
    static Color ScrollHandle => RGBA(150, 178, 205, 0.40f); // 스크롤 핸들 쿨실버

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
        const float pw = 560f, ph = 600f;   // 슬롯-푸터 간격 타이트하게 줄여서 패널 다시 줄임(4줄 유지)
        var panel = MakeRounded("BagPanel", rootGo.transform, new Vector2(pw, ph), new Vector2(BagRightX, 0), BaseDark);
        var prt = panel.GetComponent<RectTransform>();
        // 따뜻한 간유리 표면 PNG(9-slice)로 베이스 교체. 없으면 위 RoundedSprite+BaseDark 폴백.
        var panelSprite = LoadPanelSprite();
        if (panelSprite != null)
        {
            var pimg = panel.GetComponent<Image>();
            pimg.sprite = panelSprite;
            pimg.type = Image.Type.Sliced;
            pimg.color = new Color(1f, 1f, 1f, 0.12f);   // ash 표면 아주 옅게 = 슬롯칸이 블러 통과(엔필 빈칸=배경 비침). 코너 Mask는 0.12여도 동작.
            pimg.pixelsPerUnitMultiplier = 1f;   // 코너 크기 안 맞으면 이 값 조절
        }
        // ★패널 둥근 알파로 자식 클리핑(Mask) = 헤더/푸터 풀폭이어도 코너 밖으로 안 삐짐. showMaskGraphic=패널 자체는 그대로 보임.
        var panelMask = panel.GetComponent<UnityEngine.UI.Mask>();
        if (panelMask == null) panelMask = panel.AddComponent<UnityEngine.UI.Mask>();
        panelMask.showMaskGraphic = true;

        // 개발용 스킨 스왑 (Play 중 F9=다음/F10=이전 으로 panel_* PNG 갈아끼며 톤 비교). 톤 확정후 제거.
        var skinSwap = panel.GetComponent<InventoryPanelSkinSwapper>();
        if (skinSwap == null) skinSwap = panel.AddComponent<InventoryPanelSkinSwapper>();
        skinSwap.skins = LoadAllPanelSkins();
        skinSwap.alpha = 0.12f;   // 패널 pimg.color 알파와 동일해야 함(skinSwap Update가 매프레임 덮음)
        skinSwap.index = 0;

        // ── ★엔필 구조 (별도 블러캔버스 폐기): 통합블러 + 어두운배경(풀) + 둥근 밝은카드(inset) ──
        //   블러 = 패널 자식 BlurredImage(둥근 스프라이트라 코너 일치, 겹침 없음). 배경 BgDark가 가장자리/코너로 비쳐 그림자 착시.
        //   푸터 = 배경 아래쪽 노출. 칸 = 배경색(어두움)이라 구멍처럼. 카드도 둥글어 배경 코너와 일치(패널 두개처럼 안 보임).
        if (panelSprite != null)
        {
            const float inset   = 3f;     // 위/좌우는 아주 얇게 = 배경 은은히만 비침(2중패널처럼 안 보이게)
            const float footerH = 60f;    // 아래 배경 노출 = 푸터(여긴 도드라져도 됨)
            const float titleH  = 62f;    // 제목바(가방 아이콘+제목)만 밝게. 용량바는 슬롯영역과 한 표면으로 합쳐 색 끊김 제거

            // 통합 블러 = 패널 자식 (별도 Screen Space-Camera 캔버스 X). 둥근 패널 스프라이트 + Mask로 코너 일치.
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

            // 어두운 배경 = 풀사이즈(Mask 둥근코너). 칸/푸터/그림자 = 이 색. 블러 위에 어둡게.
            var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
            var bgrt = bgGo.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
            var bgGrad = bgGo.AddComponent<UIFrostGradient>();
            bgGrad.topColor    = RGBA(22, 28, 40, 0.26f);   // 위 = 옅게(콘텐츠 뒤 회색 덜 입힘 -> 흰색느낌). 위/좌우 3px 그림자도 은은.
            bgGrad.bottomColor = RGBA(10, 14, 22, 0.52f);   // 아래 = 진하게(푸터 또렷)

            // 슬롯영역 = 풀폭 틴트(스프라이트 X) -> 패널 Mask가 코너를 패널과 똑같이 둥글림(사다리꼴/2중패널 X). inset 3 = 좌우 배경 은은히만.
            var cardGo = MakeImage("SlotFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var crt2 = cardGo.GetComponent<RectTransform>();
            crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one;
            crt2.offsetMin = new Vector2(inset, footerH); crt2.offsetMax = new Vector2(-inset, -titleH);   // 위로 용량바까지 덮어 용량바+슬롯영역을 한 표면으로 (색 단차 제거)
            var cImg = cardGo.GetComponent<Image>(); cImg.sprite = null; cImg.type = Image.Type.Simple; cImg.raycastTarget = false;
            var cGrad = cardGo.AddComponent<UIFrostGradient>();
            cGrad.topColor    = RGBA(216, 224, 237, 0.34f);   // 용량바쪽(위) 살짝 더 밝게 - 부드러운 위->아래 그라데만, 단차 X
            cGrad.bottomColor = RGBA(199, 209, 223, 0.26f);   // 슬롯 아래로 갈수록 살짝 투명 = 배경 더 비침(스크롤 깊이감)

            // 헤더+용량바 = 풀폭 틴트(스프라이트 X, Mask로 둥근 윗코너). 거의 흰색. inset 3 = 위/좌우 배경 은은.
            var hbGo = MakeImage("HeaderFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var hbrt = hbGo.GetComponent<RectTransform>();
            hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1); hbrt.pivot = new Vector2(0.5f, 1);
            hbrt.offsetMin = new Vector2(inset, -titleH); hbrt.offsetMax = new Vector2(-inset, -inset);
            var hbImg = hbGo.GetComponent<Image>(); hbImg.sprite = null; hbImg.type = Image.Type.Simple; hbImg.raycastTarget = false;
            var hbGrad = hbGo.AddComponent<UIFrostGradient>();
            hbGrad.topColor    = RGBA(245, 248, 253, 0.62f);   // 거의 흰색(엔필처럼)
            hbGrad.bottomColor = RGBA(237, 243, 251, 0.56f);

            // 헤더(제목) / 용량바 사이 구분선 (엔필처럼). 흰 헤더 위라 또렷하게 (진하게+2px).
            var hdiv = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
            var hdrt = hdiv.GetComponent<RectTransform>();
            hdrt.anchorMin = new Vector2(0, 1); hdrt.anchorMax = new Vector2(1, 1); hdrt.pivot = new Vector2(0.5f, 1);
            hdrt.offsetMin = new Vector2(inset, -63); hdrt.offsetMax = new Vector2(-inset, -61);   // 제목 아래 ~ 용량바 위, 2px, 헤더 폭에 꽉 차게(좌우 여백 제거 = inset 3)
            hdiv.GetComponent<Image>().raycastTarget = false;

            // 가방 아이콘을 정사각형 칸으로 감싼다. 아래=가로 헤더선, 좌/상=패널 가장자리(둥근코너), 우=이 세로선.
            // 4변 길이를 같게(=62) 맞춰야 칸이 정사각형으로 보인다(엔필처럼). 가로선이 위에서 62px라 세로선도 x=62, 높이 62.
            var vdiv = MakeImage("HeaderIconDivider", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
            var vdrt = vdiv.GetComponent<RectTransform>();
            vdrt.anchorMin = new Vector2(0, 1); vdrt.anchorMax = new Vector2(0, 1); vdrt.pivot = new Vector2(0.5f, 1);
            vdrt.sizeDelta = new Vector2(2, 62); vdrt.anchoredPosition = new Vector2(62, 0);   // 위 끝=패널 상단, 아래 끝=가로 헤더선과 만남
            vdiv.GetComponent<Image>().raycastTarget = false;
        }

        // ── 헤더 = 제목/아이콘/닫기 위치 컨테이너만 (밝기는 위 TopFrost가 담당, 투명). ──
        var header = MakeImage("HeaderBand", prt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        StretchTop(header.GetComponent<RectTransform>(), 56, 0, 0);
        header.GetComponent<Image>().raycastTarget = false;

        // 헤더 가방 아이콘 (좌, 어두운 틴트 = 밝은 표면 위)
        var bagIcon = MakeImage("TitleIcon", header.transform, new Vector2(36, 36), Vector2.zero, TxtMain);
        var biRt = bagIcon.GetComponent<RectTransform>();
        biRt.anchorMin = biRt.anchorMax = new Vector2(0, 0.5f); biRt.pivot = new Vector2(0, 0.5f);
        biRt.anchoredPosition = new Vector2(13, -3);   // 정사각형 칸(62x62) 정중앙
        var biImg = bagIcon.GetComponent<Image>(); biImg.raycastTarget = false; biImg.preserveAspect = true;
        var bagSpr = LoadPartSprite(PartDir + "/ic_bag.png", Vector4.zero);
        if (bagSpr != null) biImg.sprite = bagSpr; else bagIcon.SetActive(false);

        var title = MakeTMP("Title", header.transform, "가방", 26, TxtMain, TextAlignmentOptions.Left);   // 밝은 표면 위 어두운 글자
        AnchorLeft(title.rectTransform, bagSpr != null ? 74 : 22, 240, 40);   // 아이콘 정사각형 칸(우변 x=62) 뒤로 제목 시작
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(0.86f, 0.90f, 0.96f, 0.5f), new Vector2(1f, -1f));   // 옅은 라이트 외곽선 = 배경 어두워도 글자 살게

        // 닫기 버튼 (New 폴더 ic_close + 호버/클릭 하이라이트)
        var closeBtn = MakeIconButton("CloseButton", header.transform, "ic_close", 54, Color.clear);
        AnchorRight(closeBtn.GetComponent<RectTransform>(), 12, 54, 54);   // 헤더 높이(56)에 꽉 차게
        TintIcon(closeBtn, TxtMain);   // 밝은 표면 위라 어두운 글리프
        // 아이콘을 New 폴더의 ic_close 로 교체 (LoadSpr은 sprites/ 폴더라 New를 직접 로드) + 헤더에 꽉 차게 크게
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtn.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null)
        {
            if (closeSpr != null) closeIconImg.sprite = closeSpr;
            closeIconImg.rectTransform.sizeDelta = new Vector2(48, 48);   // 위아래 여백 거의 없이
        }
        // 호버/클릭 인터랙션: 루트에 둥근 배경(평소 투명) + ColorTint 로 강조
        var closeBg = closeBtn.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced;
        closeBg.color = Color.white;   // ColorTint state 색이 그대로 보이게 base = white
        var closeButton = closeBtn.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint;
        closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor      = new Color(1f, 1f, 1f, 0f);              // 평소 = 투명
        ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);  // 호버 = 은은한 쿨 슬레이트
        ccb.pressedColor     = new Color(0.20f, 0.24f, 0.34f, 0.36f);  // 누름 = 더 진하게
        ccb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor    = new Color(1f, 1f, 1f, 0f);
        ccb.colorMultiplier  = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "bagCloseBtn", closeButton);

        // ── 본문 밴드 (밝음, 블러 대상). 위=용량 텍스트, 아래=슬롯 그리드 ──
        var body = MakeRounded("BodyBand", prt, Vector2.zero, Vector2.zero, BandBody);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(1, 60); bodyRt.offsetMax = new Vector2(-1, -60);   // 헤더 간격 살짝 줄임(60), 푸터쪽은 타이트(60=푸터높이와 동일), 좌우 1

        // 용량 = 텍스트만 ("용량 0 / 35"), 상태색은 컨트롤러가 갱신. (최종 디자인 = 게이지 바 없음)
        var cap = MakeTMP("CapacityText", body.transform, "용량 0/35", 22, TxtSub, TextAlignmentOptions.Left);
        cap.fontStyle = FontStyles.Bold;
        AddOutline(cap.gameObject, new Color(0.90f, 0.93f, 0.97f, 0.55f), new Vector2(1f, -1f));   // 투명 슬롯영역 위라 옅은 외곽선으로 대비 확보
        var caprt = cap.rectTransform;
        caprt.anchorMin = caprt.anchorMax = new Vector2(0, 1); caprt.pivot = new Vector2(0, 1);
        caprt.sizeDelta = new Vector2(320, 30); caprt.anchoredPosition = new Vector2(22, -16);
        SetRef(so, "capacityText", cap);
        SetRef(so, "bagCapacityGaugeFill", null);

        // 카테고리 탭: 단독 가방엔 없음 (카테고리는 창고/듀얼에만)
        SetRef(so, "bagFilterUI", null);

        // ── 슬롯 그리드 (스크롤). 본문 밴드 안, 용량(50) 아래 ~ 바닥(8) 위. 4행 보이고 세로 스크롤 ──
        var scrollGo = MakeEmpty("SlotScroll", body.transform, Vector2.zero, Vector2.zero);
        StretchMiddle(scrollGo.GetComponent<RectTransform>(), 76, 8, 14);   // top76=용량과 간격 유지, bottom8=슬롯이 푸터에 거의 붙게(엔필 타이트)
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
        grid.cellSize = new Vector2(90, 90); grid.spacing = new Vector2(9, 9);   // 엔필처럼 타이트하게 (14는 너무 벌어져 답답)
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;   // 좌상단부터 채우기(순서 유지). 좌우대칭은 콘텐츠를 블록 폭으로 가운데 둬서 처리.
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
        var sbTrack = sbGo.GetComponent<Image>();
        var trackSpr = LoadPartSprite(PartDir + "/scrollbar_track.png", new Vector4(0, 8, 0, 8));
        if (trackSpr != null) { sbTrack.sprite = trackSpr; sbTrack.type = Image.Type.Sliced; sbTrack.color = Color.white; }
        else { sbTrack.sprite = RoundedSprite(); sbTrack.type = Image.Type.Sliced; sbTrack.color = RGBA(40, 46, 54, 0.18f); }
        var sb = sbGo.GetComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop;

        var slideArea = new GameObject("Sliding Area", typeof(RectTransform));
        slideArea.transform.SetParent(sbGo.transform, false);
        Stretch(slideArea.GetComponent<RectTransform>());

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slideArea.transform, false);
        var hRt = handle.GetComponent<RectTransform>(); Stretch(hRt);
        var hImg = handle.GetComponent<Image>();
        var handleSpr = LoadPartSprite(PartDir + "/scrollbar_handle.png", new Vector4(0, 8, 0, 8));
        if (handleSpr != null) { hImg.sprite = handleSpr; hImg.type = Image.Type.Sliced; hImg.color = Color.white; }
        else { hImg.sprite = RoundedSprite(); hImg.type = Image.Type.Sliced; hImg.color = ScrollHandle; }

        sb.targetGraphic = hImg; sb.handleRect = hRt;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var gridUI = scrollGo.AddComponent<InventoryGridUI>();
        var gso = new SerializedObject(gridUI);
        gso.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        gso.FindProperty("dropFrameSprite").objectReferenceValue = LoadPartSprite(PartDir + "/hl_slot_frame@2x.png", new Vector4(44, 44, 44, 44));
        gso.FindProperty("slotGrid").objectReferenceValue = content.transform;
        gso.ApplyModifiedProperties();
        SetRef(so, "bagGridUI", gridUI);

        // ── 푸터 = 따로 없음. BgDark(어두운 배경) 아래쪽 footerH 노출이 곧 푸터(엔필 방식). 정렬/액션 버튼만 그 위에 올림. ──

        // ── 푸터 정렬 = 엔필식 작은 글리프 아이콘 (어두운 푸터밴드 위, pill/글자 X). 기능은 동일(정리). ──
        var compactBtn = MakeImage("Compact", prt, new Vector2(34, 34), Vector2.zero, new Color(0, 0, 0, 0));   // 투명 히트영역
        var compactRt = compactBtn.GetComponent<RectTransform>();
        compactRt.anchorMin = compactRt.anchorMax = new Vector2(1, 0); compactRt.pivot = new Vector2(1, 0);
        compactRt.sizeDelta = new Vector2(52, 52); compactRt.anchoredPosition = new Vector2(-16, 4);   // 푸터(60) 높이에 꽉 차게
        var compactBtnComp = compactBtn.AddComponent<Button>();
        // 호버/클릭 강조: 루트에 둥근 배경(평소 투명) + ColorTint (어두운 푸터 위라 밝은 하이라이트)
        var compactBg = compactBtn.GetComponent<Image>();
        compactBg.sprite = RoundedSprite(); compactBg.type = Image.Type.Sliced;
        compactBg.color = Color.white;   // ColorTint state 색이 그대로 보이게 base = white
        compactBtnComp.transition = Selectable.Transition.ColorTint;
        compactBtnComp.targetGraphic = compactBg;
        var scb = compactBtnComp.colors;
        scb.normalColor      = new Color(1f, 1f, 1f, 0f);     // 평소 = 투명
        scb.highlightedColor = new Color(1f, 1f, 1f, 0.16f);  // 호버 = 은은한 밝은 강조(어두운 푸터 위)
        scb.pressedColor     = new Color(1f, 1f, 1f, 0.30f);  // 누름 = 더 밝게
        scb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        scb.disabledColor    = new Color(1f, 1f, 1f, 0f);
        scb.colorMultiplier  = 1f; scb.fadeDuration = 0.1f;
        compactBtnComp.colors = scb;
        var sortIcon = MakeImage("Icon", compactBtn.transform, new Vector2(46, 46), Vector2.zero, RGBA(214, 224, 238, 1f));   // 밝은 글리프(어두운 푸터 위), 크게
        var siImg = sortIcon.GetComponent<Image>(); siImg.raycastTarget = false; siImg.preserveAspect = true;
        var sortSpr = LoadPartSprite(PartDir + "/ic_sort.png", Vector4.zero);
        if (sortSpr != null) siImg.sprite = sortSpr;

        // 가방 정렬바 — 정리(분류순 자동정렬)만 연결
        var bagSort = panel.AddComponent<SortBarUI>();
        var bso = new SerializedObject(bagSort);
        bso.FindProperty("organizeBtn")?.SetValueObj(compactBtn.GetComponent<Button>());
        bso.ApplyModifiedProperties();
        SetRef(so, "bagSortBarUI", bagSort);

        // ── 유리 림 = 패널 형태 그대로 1px 라이트 보더 (글래스모피즘: 눈이 패널 경계를 잡게). 맨 위에 올려 또렷하게 ──
        if (panelSprite != null)
        {
            var rim = MakeImage("PanelRim", prt, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));   // 채움 투명, 림만
            var rimRt = rim.GetComponent<RectTransform>();
            rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one; rimRt.offsetMin = Vector2.zero; rimRt.offsetMax = Vector2.zero;
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = panelSprite; rimImg.type = Image.Type.Sliced; rimImg.raycastTarget = false;
            AddOutline(rim, RGBA(238, 246, 255, 0.32f), new Vector2(1f, -1f));   // 안쪽 1px 라이트 림
            rim.transform.SetAsLastSibling();
        }

        // ── 블러 = 위에서 만든 통합 BlurredImage(PanelBlur)가 담당. 별도 캔버스 폐기 -> bagBlurCanvas 비움(컨트롤러 null-safe로 스킵). ──
        SetRef(so, "bagBlurCanvas", null);

        SetRef(so, "bagPanel", panel);

        // ── 통합 패널 (창고+가방 한 표면, 결계 안에서만 표시. 결계 밖이면 위 단독 가방). ──
        BuildDualPanel(so, rootGo, panel);

        // ── 파밍 상자 패널 (결계 밖/상자 파밍 시 화면 왼쪽 단독). ──
        BuildChestPanel(so, rootGo);
        so.ApplyModifiedProperties();

        if (!wasActive) rootGo.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료", "가방 패널 + 슬롯 리스타일 한 번에 생성 완료.\nPlay -> TAB 확인 후 Ctrl+S.\n블러 = 패널 자식 BlurredImage(PanelBlur) 통합방식(별도 캔버스 없음).", "확인");
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

        // ── 4변 균일 테두리 (가로/세로 다 보이는 격자). 밝은 패널 위라 어두운 쿨선으로 통일(비대칭 음각은 밝은쪽 묻혀서 폐기) ──
        var edgeT = MakeSlotEdge(t, "BorderTop",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 2), SlotLine);
        var edgeB = MakeSlotEdge(t, "BorderBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 2), SlotLine);
        var edgeL = MakeSlotEdge(t, "BorderLeft",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(2, 0), SlotLine);
        var edgeR = MakeSlotEdge(t, "BorderRight",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(2, 0), SlotLine);

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
            gb.sizeDelta = new Vector2(0, 8); gb.anchoredPosition = new Vector2(0, 0);   // 등급 밑줄(grade_bar PNG, 8px)
            var gbi = gb.GetComponent<Image>();
            if (gbi != null)
            {
                var gradeBarSpr = LoadPartSprite(PartDir + "/grade_bar.png", new Vector4(12, 0, 12, 0));   // 흰+알파, 런타임 등급색 틴트
                if (gradeBarSpr != null) { gbi.sprite = gradeBarSpr; gbi.type = Image.Type.Sliced; }
                else { gbi.sprite = null; gbi.type = Image.Type.Simple; }
            }
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
        aurora.sizeDelta = new Vector2(0, 28); aurora.anchoredPosition = new Vector2(0, 6);   // 밑줄(6px) 위에서 글로우 시작 = 바 또렷 + 은은한 번짐
        var auImg = aurora.GetComponent<Image>(); auImg.sprite = null; auImg.color = new Color(1, 1, 1, 0f); auImg.raycastTarget = false;
        var auGrad = aurora.GetComponent<UIFrostGradient>(); if (auGrad == null) auGrad = aurora.gameObject.AddComponent<UIFrostGradient>();
        auGrad.topColor = new Color(1, 1, 1, 0f); auGrad.bottomColor = new Color(1, 1, 1, 1f);

        // 소모품 표시 배지 (좌상단). 소모품 아이템일 때만 InventorySlotUI가 런타임에 켠다. item_marker = clggdesign.
        var consumable = t.Find("ConsumableBadge") as RectTransform;
        if (consumable == null)
        {
            var cbGo = new GameObject("ConsumableBadge", typeof(RectTransform), typeof(Image));
            consumable = cbGo.GetComponent<RectTransform>(); consumable.SetParent(t, false);
        }
        consumable.anchorMin = consumable.anchorMax = new Vector2(0, 1); consumable.pivot = new Vector2(0, 1);
        consumable.anchoredPosition = new Vector2(4, -4); consumable.sizeDelta = new Vector2(26, 26);
        var cbImg = consumable.GetComponent<Image>();
        var markerSpr = LoadPartSprite(PartDir + "/item_marker.png", Vector4.zero);
        cbImg.sprite = markerSpr; cbImg.type = Image.Type.Simple; cbImg.color = Color.white;
        cbImg.raycastTarget = false; cbImg.preserveAspect = true;
        cbImg.enabled = false;   // 기본 숨김 - 소모품일 때만 런타임에 켜짐

        // ItemIcon -> 칸 채우기(stretch+여백6). 90칸이면 78로 동일, 큰 칸(공장 등)이면 그만큼 커짐. preserveAspect로 찌그러짐 방지.
        var ic = t.Find("ItemIcon") as RectTransform;
        if (ic != null)
        {
            ic.anchorMin = Vector2.zero; ic.anchorMax = Vector2.one; ic.pivot = new Vector2(0.5f, 0.5f);
            ic.offsetMin = new Vector2(6, 6); ic.offsetMax = new Vector2(-6, -6); ic.anchoredPosition = Vector2.zero;
            var ii = ic.GetComponent<Image>(); if (ii != null) ii.preserveAspect = true;
        }

        // AmountText -> 우상단 + 그 뒤에 검은 반투명 수량 칩
        var at = t.Find("AmountText") as RectTransform;
        GameObject chipGo = null;
        if (at != null)
        {
            at.anchorMin = new Vector2(0.56f, 0.72f); at.anchorMax = new Vector2(0.96f, 0.96f); at.pivot = new Vector2(0.5f, 0.5f);
            at.offsetMin = Vector2.zero; at.offsetMax = Vector2.zero;
            var tmp = at.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.enableAutoSizing = true; tmp.fontSizeMin = 12; tmp.fontSizeMax = 30; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.color = RGBA(232, 238, 245, 1f); }

            // 칩은 새 GameObject 대신 안 쓰는 기존 SelectedOverlay를 재활용
            // (LoadPrefabContents에서 new GameObject가 프리팹에 저장 안 되는 경우 대비 - 확실한 방식)
            var chip = (t.Find("AmountChip") ?? t.Find("SelectedOverlay")) as RectTransform;
            if (chip != null)
            {
                chip.gameObject.name = "AmountChip";
                chip.gameObject.SetActive(true);
                chip.anchorMin = new Vector2(0.56f, 0.72f); chip.anchorMax = new Vector2(0.96f, 0.96f); chip.pivot = new Vector2(0.5f, 0.5f);
                chip.offsetMin = Vector2.zero; chip.offsetMax = Vector2.zero;
                var chipImg = chip.GetComponent<Image>();
                if (chipImg == null) chipImg = chip.gameObject.AddComponent<Image>();
                chipImg.sprite = RoundedSprite(); chipImg.type = Image.Type.Sliced;
                chipImg.color = RGBA(6, 9, 14, 0.70f); chipImg.raycastTarget = false;
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
            var cb = sso.FindProperty("consumableBadge"); if (cb != null) cb.objectReferenceValue = cbImg;
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
        var newBadge = t.Find("NewBedge") as RectTransform;
        if (newBadge != null)
        {
            // NEW 배지 = 좌상단 코너 (소모품 마커와 같은 자리). 둘은 상호배타라 안 겹침: NEW 먼저, 사라지면 마커.
            newBadge.anchorMin = newBadge.anchorMax = new Vector2(0, 1); newBadge.pivot = new Vector2(0, 1);
            newBadge.anchoredPosition = new Vector2(4, -4); newBadge.sizeDelta = new Vector2(32, 16);
        }
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
        if (consumable != null) consumable.SetAsLastSibling();   // 소모품 배지 = 최상단(좌상단 코너)

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
        if (s != null) { s.blurAdditionalDistancePerIteration = 6f; s.vibrancy = 0f; s.brightness = 0.02f; }   // 채도=0(정석 글래스모피즘은 오히려 saturate. -0.55는 회색죽 만들던 원인). 밝기는 흰막이 담당. Tuner 기본값과 동일하게
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

    // 간유리 패널 PNG 로드 + 임포트 설정 자동(Sprite/Single/9-slice 보더/무압축). 종욱이 Sprite Editor 안 건드려도 됨.
    static Sprite LoadPanelSprite() => ConfigurePanelSprite(PanelSpritePath);

    static Sprite ConfigurePanelSprite(string path) => LoadPartSprite(path, new Vector4(PanelSlice, PanelSlice, PanelSlice, PanelSlice));

    // 부품 PNG 로드 + 임포트 자동(Sprite/Single/9-slice 보더/무압축). border = 9-slice 보더(아이콘 등 통짜면 Vector4.zero).
    static Sprite LoadPartSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[InventoryUIBuilder] PNG 못 찾음: " + path); return null; }

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
            s.spriteBorder = border;
            s.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(s);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // sprites 폴더의 panel_* 전부 로드(각각 9-slice 설정). 프로스트(기본) 먼저. 스킨 스왑용.
    static Sprite[] LoadAllPanelSkins()
    {
        var list = new System.Collections.Generic.List<Sprite>();
        var first = ConfigurePanelSprite(PanelSpritePath);
        if (first != null) list.Add(first);

        var guids = AssetDatabase.FindAssets("panel t:Texture2D", new[] { PartDir, "Assets/11.UI/Inventory UI/sprites" });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path == PanelSpritePath) continue;
            var fn = path.Substring(path.LastIndexOf('/') + 1);
            if (!fn.StartsWith("panel_")) continue;
            var spr = ConfigurePanelSprite(path);
            if (spr != null) list.Add(spr);
        }
        return list.ToArray();
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

    // 창고 부품 PNG 로드 (11.UI/Warehouse/sprites/). 통짜 아이콘=Vector4.zero, tab_selected=9slice 보더.
    static Sprite LoadWarehouseSprite(string n, Vector4 border) => LoadPartSprite(WarehouseDir + n + ".png", border);

    // ─────────────────────────────────────────────────────────────────────────
    // 통합 패널 = 창고(좌) + 가방(우) 를 한 9-slice 표면으로 (바깥 4모서리만 둥글고 안쪽 평평 = seam 없음).
    // 결계 안에서만 InventoryUIController 가 표시(이때 단독 가방은 숨김). 가운데 세로 seam선으로만 구역 구분.
    // 새 스프라이트 불필요(9-slice가 어떤 너비든 처리). 단독 가방(BuildBag)은 그대로 = 결계 밖 전용.
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildDualPanel(SerializedObject so, GameObject rootGo, GameObject bagPanel)
    {
        var oldWh = so.FindProperty("warehousePanel")?.objectReferenceValue as GameObject;
        if (oldWh != null) Object.DestroyImmediate(oldWh);

        const float whW = 860f, bagW = 560f, ph = 600f;
        const float dualW = whW + bagW;          // 1420
        const float whFrac = whW / dualW;        // 창고/가방 경계 비율(앵커용)
        var bagRt0 = bagPanel.GetComponent<RectTransform>();
        float bagRight = bagRt0.anchoredPosition.x + bagRt0.sizeDelta.x / 2f;   // 가방 우측 끝 = 통합패널 우측 끝(가방 위치 고정)

        var panelSprite = LoadPanelSprite();
        var panel = MakeRounded("WarehousePanel", rootGo.transform, new Vector2(dualW, ph), Vector2.zero, BaseDark);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(bagRight, 0);
        if (panelSprite != null)
        {
            var pimg = panel.GetComponent<Image>();
            pimg.sprite = panelSprite; pimg.type = Image.Type.Sliced;
            pimg.color = new Color(1f, 1f, 1f, 0.12f); pimg.pixelsPerUnitMultiplier = 1f;
        }
        var panelMask = panel.GetComponent<UnityEngine.UI.Mask>();
        if (panelMask == null) panelMask = panel.AddComponent<UnityEngine.UI.Mask>();
        panelMask.showMaskGraphic = true;

        // ── 프로스트 표면 (full-width, 가방과 동일 값 = 한 표면) ──
        if (panelSprite != null)
        {
            const float inset = 3f, footerH = 60f, titleH = 62f;

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

            var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
            var bgrt = bgGo.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
            var bgGrad = bgGo.AddComponent<UIFrostGradient>();
            bgGrad.topColor = RGBA(22, 28, 40, 0.26f);
            bgGrad.bottomColor = RGBA(10, 14, 22, 0.52f);

            var cardGo = MakeImage("SlotFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var crt2 = cardGo.GetComponent<RectTransform>();
            crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one;
            crt2.offsetMin = new Vector2(inset, footerH); crt2.offsetMax = new Vector2(-inset, -titleH);
            var cImg = cardGo.GetComponent<Image>(); cImg.sprite = null; cImg.type = Image.Type.Simple; cImg.raycastTarget = false;
            var cGrad = cardGo.AddComponent<UIFrostGradient>();
            cGrad.topColor = RGBA(216, 224, 237, 0.34f);
            cGrad.bottomColor = RGBA(199, 209, 223, 0.26f);

            var hbGo = MakeImage("HeaderFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var hbrt = hbGo.GetComponent<RectTransform>();
            hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1); hbrt.pivot = new Vector2(0.5f, 1);
            hbrt.offsetMin = new Vector2(inset, -titleH); hbrt.offsetMax = new Vector2(-inset, -inset);
            var hbImg = hbGo.GetComponent<Image>(); hbImg.sprite = null; hbImg.type = Image.Type.Simple; hbImg.raycastTarget = false;
            var hbGrad = hbGo.AddComponent<UIFrostGradient>();
            hbGrad.topColor = RGBA(245, 248, 253, 0.62f);
            hbGrad.bottomColor = RGBA(237, 243, 251, 0.56f);

            var hdiv = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
            var hdrt = hdiv.GetComponent<RectTransform>();
            hdrt.anchorMin = new Vector2(0, 1); hdrt.anchorMax = new Vector2(1, 1); hdrt.pivot = new Vector2(0.5f, 1);
            hdrt.offsetMin = new Vector2(inset, -63); hdrt.offsetMax = new Vector2(-inset, -61);
            hdiv.GetComponent<Image>().raycastTarget = false;

            // 가운데 세로 seam = 3분절(헤더/본문/푸터). 밴드별로 끊겨 엔필처럼 선 3개로 보임.
            // 1) 헤더 구간 (밝은 헤더 위, 어두운 슬레이트 — 위에 헤더선과 톤 통일)
            var s1 = MakeImage("SeamHeader", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
            var s1rt = s1.GetComponent<RectTransform>();
            s1rt.anchorMin = new Vector2(whFrac, 1); s1rt.anchorMax = new Vector2(whFrac, 1); s1rt.pivot = new Vector2(0.5f, 1);
            s1rt.sizeDelta = new Vector2(2, 56); s1rt.anchoredPosition = new Vector2(0, -4);   // 제목 구간 = 공백 없이 헤더선까지 solid
            s1.GetComponent<Image>().raycastTarget = false;
            // 2) 본문 구간 (슬롯영역, 어두운 슬레이트)
            var s2 = MakeImage("SeamBody", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.55f));
            var s2rt = s2.GetComponent<RectTransform>();
            s2rt.anchorMin = new Vector2(whFrac, 0); s2rt.anchorMax = new Vector2(whFrac, 1); s2rt.pivot = new Vector2(0.5f, 0.5f);
            s2rt.offsetMin = new Vector2(-1, 100); s2rt.offsetMax = new Vector2(1, -110);   // 본문 (위아래 공백, 적당히)
            s2.GetComponent<Image>().raycastTarget = false;
            // 3) 푸터 구간 (어두운 푸터 위라 밝은 선으로 — 푸터 색감에 맞춤)
            var s3 = MakeImage("SeamFooter", prt, Vector2.zero, Vector2.zero, RGBA(160, 178, 205, 0.50f));
            var s3rt = s3.GetComponent<RectTransform>();
            s3rt.anchorMin = new Vector2(whFrac, 0); s3rt.anchorMax = new Vector2(whFrac, 0); s3rt.pivot = new Vector2(0.5f, 0);
            s3rt.sizeDelta = new Vector2(2, 40); s3rt.anchoredPosition = new Vector2(0, 10);   // 푸터 (위로 공백, 적당히)
            s3.GetComponent<Image>().raycastTarget = false;

            // 아이콘 칸 세로선: 창고(좌, x0) + 가방(가방구역 시작 whFrac)
            MakeIconCellDivider(prt, 0f);
            MakeIconCellDivider(prt, whFrac);
        }

        // ── 헤더 (full-width 컨테이너): 창고 제목(좌) + 가방 제목(가방구역) + 닫기(우) ──
        var header = MakeImage("HeaderBand", prt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        StretchTop(header.GetComponent<RectTransform>(), 56, 0, 0);
        header.GetComponent<Image>().raycastTarget = false;
        BuildSectionTitle(header.transform, 0f, LoadWarehouseSprite("ic_warehouse", Vector4.zero), "창고");
        BuildSectionTitle(header.transform, whFrac, LoadPartSprite(PartDir + "/ic_bag.png", Vector4.zero), "가방");

        // 닫기 버튼 (가방과 동일 — 우상단, ic_close + 호버/클릭 강조)
        var closeBtn = MakeIconButton("CloseButton", header.transform, "ic_close", 54, Color.clear);
        AnchorRight(closeBtn.GetComponent<RectTransform>(), 12, 54, 54);
        TintIcon(closeBtn, TxtMain);
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtn.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null) { if (closeSpr != null) closeIconImg.sprite = closeSpr; closeIconImg.rectTransform.sizeDelta = new Vector2(48, 48); }
        var closeBg = closeBtn.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced; closeBg.color = Color.white;
        var closeButton = closeBtn.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint; closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor = new Color(1f, 1f, 1f, 0f); ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);
        ccb.pressedColor = new Color(0.20f, 0.24f, 0.34f, 0.36f); ccb.selectedColor = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor = new Color(1f, 1f, 1f, 0f); ccb.colorMultiplier = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "warehouseDualCloseBtn", closeButton);

        // ── 본문 (full-width 컨테이너) ──
        var body = MakeRounded("BodyBand", prt, Vector2.zero, Vector2.zero, BandBody);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(1, 60); bodyRt.offsetMax = new Vector2(-1, -60);

        // 가방 용량 텍스트 (가방 구역 좌상단)
        var cap = MakeTMP("CapacityText", body.transform, "용량 0/35", 22, TxtSub, TextAlignmentOptions.Left);
        cap.fontStyle = FontStyles.Bold;
        AddOutline(cap.gameObject, new Color(0.90f, 0.93f, 0.97f, 0.55f), new Vector2(1f, -1f));
        var caprt = cap.rectTransform;
        caprt.anchorMin = new Vector2(whFrac, 1); caprt.anchorMax = new Vector2(whFrac, 1); caprt.pivot = new Vector2(0, 1);
        caprt.sizeDelta = new Vector2(320, 30); caprt.anchoredPosition = new Vector2(22, -16);
        SetRef(so, "warehouseBagCapacityText", cap);

        // 창고 그리드(좌, 8열) / 가방 그리드(우, 5열)
        var whGrid = BuildScrollGrid(body, "WarehouseScroll", new Vector2(0f, 0f), new Vector2(whFrac, 1f), 8, 30f);   // 스크롤바를 seam에서 더 떼기
        // 창고만 동적 칸(아이템 수만큼, 빈칸 없음 = 무제한 느낌)
        var whGridSo = new SerializedObject(whGrid);
        whGridSo.FindProperty("dynamicSlots").boolValue = true;
        whGridSo.ApplyModifiedProperties();
        SetRef(so, "warehouseGridUI", whGrid);

        // 창고 영역 드롭존 (가방->창고 입고) + 영역강조 + 매칭강조 + 힌트. ScrollRect 같은 GO라 드래그중 스크롤 유지.
        SetupDropZone(body, whGrid, true, 0f, whFrac, "드래그하여 창고에 넣기");
        var bagGrid = BuildScrollGrid(body, "BagScroll", new Vector2(whFrac, 0f), new Vector2(1f, 1f), 5);
        SetRef(so, "warehouseBagGridUI", bagGrid);

        // 가방 영역 드롭존 (창고->가방 회수) + 영역강조 + 매칭강조 + 힌트
        SetupDropZone(body, bagGrid, false, whFrac, 1f, "드래그하여 가방에 넣기");

        // 창고 카테고리 분류 탭 (헤더선 아래 줄, 창고 구역) — cat_* 7개 + tab_selected
        var whFilter = BuildCategoryTabs(prt, whFrac);
        SetRef(so, "warehouseFilterUI", whFilter);

        // 푸터: 창고정렬(좌하) / 이음선 좌우 = 일괄회수 + 가방정렬 / 일괄보관(우하)
        var whSortBtn = BuildFooterGlyph(prt, "WarehouseSort", LoadPartSprite(PartDir + "/ic_sort.png", Vector4.zero), new Vector2(0f, 0f), 16f);
        var depositBtn = BuildFooterGlyph(prt, "BagDeposit", LoadWarehouseSprite("ic_deposit_all", Vector4.zero), new Vector2(1f, 0f), -16f);
        SetRef(so, "moveAllBtn", depositBtn);
        var bagSortBtn = BuildFooterGlyph(prt, "BagSort", LoadPartSprite(PartDir + "/ic_sort.png", Vector4.zero), new Vector2(whFrac, 0f), 36f, 0.5f);

        // 정렬바 컴포넌트 (각 정렬버튼 GO에 부착 = 같은 타입 중복 회피). 창고=StorageInstance, 가방=Instance는 컨트롤러가 바인딩.
        var whSort = whSortBtn.gameObject.AddComponent<SortBarUI>();
        var whso = new SerializedObject(whSort);
        whso.FindProperty("organizeBtn")?.SetValueObj(whSortBtn);
        whso.ApplyModifiedProperties();
        SetRef(so, "sortBarUI", whSort);
        var bagSortC = bagSortBtn.gameObject.AddComponent<SortBarUI>();
        var bsso = new SerializedObject(bagSortC);
        bsso.FindProperty("organizeBtn")?.SetValueObj(bagSortBtn);
        bsso.ApplyModifiedProperties();
        SetRef(so, "warehouseBagSortBarUI", bagSortC);

        // 일괄회수 (창고->가방, 이음선 왼쪽). OnClickTakeAll = 창고 카테고리 필터 적용(보이는 것만 회수).
        var retrieveBtn = BuildFooterGlyph(prt, "BagRetrieve", LoadWarehouseSprite("ic_deposit_all", Vector4.zero), new Vector2(whFrac, 0f), -36f, 0.5f);
        var rIcon = retrieveBtn.transform.Find("Icon");
        if (rIcon != null) rIcon.localScale = new Vector3(1f, -1f, 1f);   // 아이콘 세로 뒤집어 '회수(위로)' (보관=아래). 전용 아이콘 받으면 교체
        SetRef(so, "takeAllBtn", retrieveBtn);

        // 유리 림
        if (panelSprite != null)
        {
            var rim = MakeImage("PanelRim", prt, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
            var rimRt = rim.GetComponent<RectTransform>();
            rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one; rimRt.offsetMin = Vector2.zero; rimRt.offsetMax = Vector2.zero;
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = panelSprite; rimImg.type = Image.Type.Sliced; rimImg.raycastTarget = false;
            AddOutline(rim, RGBA(238, 246, 255, 0.32f), new Vector2(1f, -1f));
            rim.transform.SetAsLastSibling();
        }

        SetRef(so, "warehousePanel", panel);
    }

    // 파밍 상자 패널 = 가방과 같은 간유리 표면, 화면 왼쪽 단독(결계 밖/상자 파밍). 그리드는 ChestInstance 바인딩.
    static void BuildChestPanel(SerializedObject so, GameObject rootGo)
    {
        // 기존 상자 패널 제거 (스테일 클론/중복 방지)
        var oldChest = so.FindProperty("chestPanel")?.objectReferenceValue as GameObject;
        if (oldChest != null) Object.DestroyImmediate(oldChest);
        var byName = rootGo.transform.Find("ChestPanel");
        if (byName != null) Object.DestroyImmediate(byName.gameObject);

        const float pw = 520f, ph = 300f;   // 컴팩트 - 5칸 한 줄만(스크롤 없음). ChestInstance.maxSlots=5 가정.
        const float chestX = -480f;          // 화면 왼쪽(가방 거울). 런타임은 컨트롤러 chestLeftX가 덮음.
        var panel = MakeRounded("ChestPanel", rootGo.transform, new Vector2(pw, ph), new Vector2(chestX, 0), BaseDark);
        var prt = panel.GetComponent<RectTransform>();

        var panelSprite = LoadPanelSprite();
        if (panelSprite != null)
        {
            var pimg = panel.GetComponent<Image>();
            pimg.sprite = panelSprite; pimg.type = Image.Type.Sliced;
            pimg.color = new Color(1f, 1f, 1f, 0.12f); pimg.pixelsPerUnitMultiplier = 1f;
        }
        var mask = panel.GetComponent<UnityEngine.UI.Mask>();
        if (mask == null) mask = panel.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        // 간유리 레이어 (가방과 동일 레시피: 통합블러 + 어두운배경 + 밝은 inset 카드/헤더)
        if (panelSprite != null)
        {
            const float inset = 3f, footerH = 60f, titleH = 62f;

            var blurGo = new GameObject("PanelBlur", typeof(RectTransform));
            blurGo.transform.SetParent(prt, false);
            var blRt = blurGo.GetComponent<RectTransform>();
            blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one; blRt.offsetMin = Vector2.zero; blRt.offsetMax = Vector2.zero;
            var blur = blurGo.AddComponent<BlurredImage>();
            blur.sprite = panelSprite; blur.type = Image.Type.Sliced; blur.pixelsPerUnitMultiplier = 1f;
            blur.color = Color.white; blur.raycastTarget = false;
            blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
            blur.Common.cameraReference = PickBuildCamera();
            blur.Common.featureNumber = 0; blur.Common.unrankedLayer = 1;
            var bs = blur.Common.blurInstanceSettings;
            if (bs != null)
            {
                if (bs.blurSections != null) foreach (var sec in bs.blurSections) { sec.iterations = 5; sec.sampleDistance = 1.5f; }
                bs.vibrancy = 0f; bs.brightness = 0.02f; bs.contrast = 0f; bs.referenceResolution = 1080;
            }
            blur.Common.ValidateBlur();

            var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
            var bgrt = bgGo.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
            var bgGrad = bgGo.AddComponent<UIFrostGradient>();
            bgGrad.topColor = RGBA(22, 28, 40, 0.26f); bgGrad.bottomColor = RGBA(10, 14, 22, 0.52f);

            var cardGo = MakeImage("SlotFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var crt2 = cardGo.GetComponent<RectTransform>();
            crt2.anchorMin = Vector2.zero; crt2.anchorMax = Vector2.one;
            crt2.offsetMin = new Vector2(inset, footerH); crt2.offsetMax = new Vector2(-inset, -titleH);
            var cImg = cardGo.GetComponent<Image>(); cImg.sprite = null; cImg.type = Image.Type.Simple; cImg.raycastTarget = false;
            var cGrad = cardGo.AddComponent<UIFrostGradient>();
            cGrad.topColor = RGBA(216, 224, 237, 0.34f); cGrad.bottomColor = RGBA(199, 209, 223, 0.26f);

            var hbGo = MakeImage("HeaderFrost", prt, Vector2.zero, Vector2.zero, Color.white);
            var hbrt = hbGo.GetComponent<RectTransform>();
            hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1); hbrt.pivot = new Vector2(0.5f, 1);
            hbrt.offsetMin = new Vector2(inset, -titleH); hbrt.offsetMax = new Vector2(-inset, -inset);
            var hbImg = hbGo.GetComponent<Image>(); hbImg.sprite = null; hbImg.type = Image.Type.Simple; hbImg.raycastTarget = false;
            var hbGrad = hbGo.AddComponent<UIFrostGradient>();
            hbGrad.topColor = RGBA(245, 248, 253, 0.62f); hbGrad.bottomColor = RGBA(237, 243, 251, 0.56f);

            var hdiv = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
            var hdrt = hdiv.GetComponent<RectTransform>();
            hdrt.anchorMin = new Vector2(0, 1); hdrt.anchorMax = new Vector2(1, 1); hdrt.pivot = new Vector2(0.5f, 1);
            hdrt.offsetMin = new Vector2(inset, -63); hdrt.offsetMax = new Vector2(-inset, -61);
            hdiv.GetComponent<Image>().raycastTarget = false;
        }

        // 헤더 = 제목 "파밍 상자" + 닫기 (투명 컨테이너, 밝기는 HeaderFrost가)
        var header = MakeImage("HeaderBand", prt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        StretchTop(header.GetComponent<RectTransform>(), 56, 0, 0);
        header.GetComponent<Image>().raycastTarget = false;

        var title = MakeTMP("Title", header.transform, "파밍 상자", 26, TxtMain, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 22, 240, 40);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(0.86f, 0.90f, 0.96f, 0.5f), new Vector2(1f, -1f));

        var closeBtn = MakeIconButton("CloseButton", header.transform, "ic_close", 54, Color.clear);
        AnchorRight(closeBtn.GetComponent<RectTransform>(), 12, 54, 54);
        TintIcon(closeBtn, TxtMain);
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtn.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null) { if (closeSpr != null) closeIconImg.sprite = closeSpr; closeIconImg.rectTransform.sizeDelta = new Vector2(48, 48); }
        var closeBg = closeBtn.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced; closeBg.color = Color.white;
        var closeButton = closeBtn.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint; closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor = new Color(1f, 1f, 1f, 0f); ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);
        ccb.pressedColor = new Color(0.20f, 0.24f, 0.34f, 0.36f); ccb.selectedColor = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor = new Color(1f, 1f, 1f, 0f); ccb.colorMultiplier = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "chestCloseBtn", closeButton);

        // 본문 = 카운터 "아이템 N/M" + 그리드
        var body = MakeRounded("BodyBand", prt, Vector2.zero, Vector2.zero, BandBody);
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(1, 60); bodyRt.offsetMax = new Vector2(-1, -60);

        var cap = MakeTMP("ChestCapacityText", body.transform, "아이템 0/5", 22, TxtSub, TextAlignmentOptions.Left);
        cap.fontStyle = FontStyles.Bold;
        AddOutline(cap.gameObject, new Color(0.90f, 0.93f, 0.97f, 0.55f), new Vector2(1f, -1f));
        var caprt = cap.rectTransform;
        caprt.anchorMin = caprt.anchorMax = new Vector2(0, 1); caprt.pivot = new Vector2(0, 1);
        caprt.sizeDelta = new Vector2(320, 30); caprt.anchoredPosition = new Vector2(22, -16);
        SetRef(so, "chestCapacityText", cap);

        var chestGrid = BuildScrollGrid(body, "ChestScroll", new Vector2(0f, 0f), new Vector2(1f, 1f), 5, 14f, false);   // 한 줄 5칸, 스크롤 없음
        SetRef(so, "chestGridUI", chestGrid);

        // 푸터 = 모두 가져오기(상자->가방). 아이콘 위로 뒤집어 '가져오기'.
        var takeAll = BuildFooterGlyph(prt, "ChestTakeAll", LoadWarehouseSprite("ic_deposit_all", Vector4.zero), new Vector2(1f, 0f), -16f);
        var taIcon = takeAll.transform.Find("Icon");
        if (taIcon != null) taIcon.localScale = new Vector3(1f, -1f, 1f);
        SetRef(so, "takeAllFromChestBtn", takeAll);

        // 유리 림
        if (panelSprite != null)
        {
            var rim = MakeImage("PanelRim", prt, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
            var rimRt = rim.GetComponent<RectTransform>();
            rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one; rimRt.offsetMin = Vector2.zero; rimRt.offsetMax = Vector2.zero;
            var rimImg = rim.GetComponent<Image>();
            rimImg.sprite = panelSprite; rimImg.type = Image.Type.Sliced; rimImg.raycastTarget = false;
            AddOutline(rim, RGBA(238, 246, 255, 0.32f), new Vector2(1f, -1f));
            rim.transform.SetAsLastSibling();
        }

        panel.SetActive(false);
        SetRef(so, "chestPanel", panel);
    }

    // 아이콘 칸 세로 구분선 (헤더 구역 시작 fraction 에서 우측 62px, 높이 62 = 정사각형 칸)
    static void MakeIconCellDivider(RectTransform prt, float xFrac)
    {
        var vdiv = MakeImage("IconCellDivider", prt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.60f));
        var vdrt = vdiv.GetComponent<RectTransform>();
        vdrt.anchorMin = new Vector2(xFrac, 1); vdrt.anchorMax = new Vector2(xFrac, 1); vdrt.pivot = new Vector2(0.5f, 1);
        vdrt.sizeDelta = new Vector2(2, 62); vdrt.anchoredPosition = new Vector2(62, 0);
        vdiv.GetComponent<Image>().raycastTarget = false;
    }

    // 구역 제목(아이콘+텍스트) — header 안, fraction 기준 그 구역 좌측에 배치
    static void BuildSectionTitle(Transform header, float xFrac, Sprite iconSpr, string label)
    {
        var icon = MakeImage("TitleIcon", header, new Vector2(36, 36), Vector2.zero, TxtMain);
        var iRt = icon.GetComponent<RectTransform>();
        iRt.anchorMin = iRt.anchorMax = new Vector2(xFrac, 0.5f); iRt.pivot = new Vector2(0, 0.5f);
        iRt.anchoredPosition = new Vector2(13, -3);
        var iImg = icon.GetComponent<Image>(); iImg.raycastTarget = false; iImg.preserveAspect = true;
        if (iconSpr != null) iImg.sprite = iconSpr; else icon.SetActive(false);
        var title = MakeTMP("Title", header, label, 26, TxtMain, TextAlignmentOptions.Left);
        var tRt = title.rectTransform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(xFrac, 0.5f); tRt.pivot = new Vector2(0, 0.5f);
        tRt.sizeDelta = new Vector2(240, 40); tRt.anchoredPosition = new Vector2(74, 0);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(0.86f, 0.90f, 0.96f, 0.5f), new Vector2(1f, -1f));
    }

    // 푸터 글리프 버튼 (어두운 푸터 위, 둥근 배경 호버/클릭 강조 + 밝은 아이콘). anchor=코너(0,0 좌하 / 1,0 우하), xOff=코너에서 가로 오프셋.
    static Button BuildFooterGlyph(RectTransform prt, string name, Sprite iconSpr, Vector2 anchor, float xOff, float pivotX = -1f)
    {
        var go = MakeImage(name, prt, new Vector2(52, 52), Vector2.zero, Color.white);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(pivotX < 0f ? anchor.x : pivotX, anchor.y);   // 이음선 배치용: pivotX 지정 시 가로 기준점 분리
        rt.anchoredPosition = new Vector2(xOff, 4);
        var bg = go.GetComponent<Image>(); bg.sprite = RoundedSprite(); bg.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.ColorTint; btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.16f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.30f); cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0f); cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;
        var icon = MakeImage("Icon", go.transform, new Vector2(42, 42), Vector2.zero, RGBA(214, 224, 238, 1f));
        var iImg = icon.GetComponent<Image>(); iImg.raycastTarget = false; iImg.preserveAspect = true;
        if (iconSpr != null) iImg.sprite = iconSpr;
        return btn;
    }

    // 창고 카테고리 분류 탭 (전체 + ItemCategory 6종). CategoryFilterUI 반환(컨트롤러가 warehouseGridUI.SetFilter에 연결).
    static CategoryFilterUI BuildCategoryTabs(RectTransform prt, float whFrac)
    {
        var bar = MakeEmpty("FilterBar", prt, Vector2.zero, Vector2.zero);
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(whFrac, 1); brt.pivot = new Vector2(0, 1);
        brt.offsetMin = new Vector2(0, -128); brt.offsetMax = new Vector2(0, -66);   // 헤더선(62) 아래 줄, 높이 62, 좌우 풀(탭은 가운데 정렬)

        // 인덱스 순서 = CategoryFilterUI.IndexToCategory (0 전체 / 1~6 분류)
        string[] cats = { "cat_all", "cat_raw", "cat_proc1", "cat_proc2", "cat_consumable", "cat_core", "cat_special" };
        const float tabSize = 58f, gap = 8f;
        var tabBg = LoadWarehouseSprite("tab_selected", new Vector4(20, 20, 20, 20));
        var buttons = new Button[cats.Length];
        for (int i = 0; i < cats.Length; i++)
        {
            var t = MakeImage("Tab" + i, bar.transform, new Vector2(tabSize, tabSize), Vector2.zero, Color.white);
            var trt = t.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);   // 바 가운데 기준
            trt.anchoredPosition = new Vector2((i - (cats.Length - 1) / 2f) * (tabSize + gap), 0);   // 가운데에서 양옆으로 퍼지게
            var tImg = t.GetComponent<Image>();
            if (tabBg != null) { tImg.sprite = tabBg; tImg.type = Image.Type.Sliced; }   // 선택 시 보이는 다크 pill(시안 림). 평소엔 CategoryFilterUI가 alpha 0으로 숨김
            var b = t.AddComponent<Button>(); b.targetGraphic = tImg; b.transition = Selectable.Transition.None;   // 색은 CategoryFilterUI가 직접 관리
            var ci = MakeImage("CatIcon", t.transform, new Vector2(38, 38), Vector2.zero, new Color(0.76f, 0.83f, 0.90f, 1f));
            var cirt = ci.GetComponent<RectTransform>();
            cirt.anchorMin = cirt.anchorMax = new Vector2(0f, 0.5f); cirt.pivot = new Vector2(0f, 0.5f);
            cirt.anchoredPosition = new Vector2(10f, 0f);   // 좌측 고정(펼쳐도 아이콘 왼쪽). 접힘(58)일 땐 가운데처럼 보임
            var ciImg = ci.GetComponent<Image>(); ciImg.raycastTarget = false; ciImg.preserveAspect = true;
            var cs = LoadWarehouseSprite(cats[i], Vector4.zero);
            if (cs != null) ciImg.sprite = cs;

            // 카테고리 이름 (선택 시 펼쳐 보임). 평소 alpha 0. 폭/배치/페이드는 CategoryFilterUI가 런타임 처리.
            var lbl = MakeTMP("CatLabel", t.transform, "", 20, new Color(0.82f, 0.92f, 1f, 0f), TextAlignmentOptions.Left);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f); lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(56f, 0f); lrt.sizeDelta = new Vector2(140f, 30f);   // 아이콘(10+38)+간격8 우측
            lbl.raycastTarget = false; lbl.enableWordWrapping = false; lbl.fontStyle = FontStyles.Bold;
            buttons[i] = b;
        }

        var filter = bar.AddComponent<CategoryFilterUI>();
        var cfso = new SerializedObject(filter);
        var arr = cfso.FindProperty("filterButtons");
        if (arr != null)
        {
            arr.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        }
        cfso.FindProperty("selectedColor")?.SetColor(new Color(1f, 1f, 1f, 1f));        // 선택 = tab_selected pill 그대로 보임
        cfso.FindProperty("normalColor")?.SetColor(new Color(1f, 1f, 1f, 0f));          // 비선택 = pill 숨김(아이콘만)
        cfso.FindProperty("selectedIconColor")?.SetColor(new Color(0.68f, 0.89f, 1f, 1f));
        cfso.FindProperty("normalIconColor")?.SetColor(new Color(0.76f, 0.83f, 0.90f, 1f));
        cfso.ApplyModifiedProperties();
        return filter;
    }

    // 드롭존 셋업: 그리드 GO에 InventoryDropZone 부착 + 영역강조 프레임 + 힌트 pill 생성/연결.
    // toStorage=true: 가방->창고 입고 / false: 창고->가방 회수. [aMinX,aMaxX]=body 내 이 구역 가로 비율.
    static void SetupDropZone(GameObject body, InventoryGridUI grid, bool toStorage, float aMinX, float aMaxX, string hintText)
    {
        var dz = grid.gameObject.AddComponent<InventoryDropZone>();

        // 영역 강조 = 흰 sci-fi 프레임(위/좌/우 3변, 바닥 열림) 9-slice. 집으면 켜짐. 로드 실패 시 4변 라인 폴백.
        // 프레임을 '가로 중앙정렬된 슬롯 그리드' 폭에 딱 맞춰 중앙 배치 -> 슬롯이 패널보다 좁아도 좌우대칭으로 감쌈.
        var gridRt = grid.GetComponent<RectTransform>();
        var glg = grid.GetComponentInChildren<GridLayoutGroup>(true);
        int cols = glg != null ? glg.constraintCount : 5;
        float cell = glg != null ? glg.cellSize.x : 90f;
        float sp = glg != null ? glg.spacing.x : 9f;
        const float gutter = 8f, sideGap = 16f, topUp = -4f;   // topUp 음수 = 프레임 윗변을 탭 아래로(탭에 선 안 생기게)
        float frameW = cols * cell + (cols - 1) * sp + 2f * sideGap;            // 그리드 폭 + 좌우 여백
        float cxAnchor = (gridRt.anchorMin.x + gridRt.anchorMax.x) * 0.5f;
        float cxPos = (gridRt.offsetMin.x + gridRt.offsetMax.x) * 0.5f - gutter * 0.5f;   // 슬롯(거터 보정) 가로 중심
        var region = MakeEmpty("DropRegion", body.transform, Vector2.zero, Vector2.zero);
        var rr = region.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(cxAnchor, gridRt.anchorMin.y);
        rr.anchorMax = new Vector2(cxAnchor, gridRt.anchorMax.y);
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.offsetMin = new Vector2(cxPos - frameW * 0.5f, gridRt.offsetMin.y - sideGap);
        rr.offsetMax = new Vector2(cxPos + frameW * 0.5f, gridRt.offsetMax.y + topUp);
        var regionSpr = LoadPartSprite(PartDir + "/hl_region_frame_open@2x.png", new Vector4(52, 52, 52, 52));
        if (regionSpr != null)
        {
            // 두 겹: 안쪽 굵은 선 = 고정 / 바깥 얇은 선 = 물결처럼 안으로 반복 수렴(RegionFrameRipple).
            // ★물결 조절값 3개 (바꾼 뒤 이 메뉴 재실행해야 반영):
            const float rippleDepth = 1f;       // 안쪽 고정선 위치(작을수록 슬롯에서 멀어짐 = 겹침↓)
            const float rippleStartOut = 14f;   // 물결 시작 거리 = 프레임 밖 이만큼서 옴(클수록 멀리서)
            const float ripplePeriod = 0.8f;    // 물결 주기(초, 작을수록 빠름)
            MakeRegionFrameLayer(region.transform, regionSpr, 2f, rippleDepth);    // 안쪽 고정(굵음)
            var outer = MakeRegionFrameLayer(region.transform, regionSpr, 3f, 0f); // 바깥(얇음)
            var ripple = outer.gameObject.AddComponent<RegionFrameRipple>();
            ripple.rippleDepth = rippleDepth; ripple.startOut = rippleStartOut; ripple.period = ripplePeriod;
        }
        else
        {
            var edge = RGBA(130, 214, 230, 0.95f);
            MakeRegionEdge(region.transform, edge, 3f, 0);
            MakeRegionEdge(region.transform, edge, 3f, 1);
            MakeRegionEdge(region.transform, edge, 3f, 2);
            MakeRegionEdge(region.transform, edge, 3f, 3);
        }
        region.transform.SetAsLastSibling();
        region.SetActive(false);

        // 힌트 pill (포인터가 영역 안일 때만)
        var hint = MakeRounded("DropHint", body.transform, new Vector2(360, 52), Vector2.zero, RGBA(10, 14, 22, 0.82f));
        var hrt = hint.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2((aMinX + aMaxX) * 0.5f, 0.5f); hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero;
        hint.GetComponent<Image>().raycastTarget = false;
        var ht = MakeTMP("Text", hint.transform, hintText, 22, RGBA(235, 242, 250, 1f), TextAlignmentOptions.Center);
        var htt = ht.rectTransform; htt.anchorMin = Vector2.zero; htt.anchorMax = Vector2.one; htt.offsetMin = Vector2.zero; htt.offsetMax = Vector2.zero;
        ht.raycastTarget = false; ht.fontStyle = FontStyles.Bold;
        hint.transform.SetAsLastSibling();
        hint.SetActive(false);

        var dzso = new SerializedObject(dz);
        dzso.FindProperty("depositToStorage").boolValue = toStorage;
        dzso.FindProperty("regionHighlight").objectReferenceValue = region;
        dzso.FindProperty("dragHint").objectReferenceValue = hint;
        dzso.ApplyModifiedProperties();
    }

    // 영역 강조 테두리 한 변. side: 0=위 1=아래 2=좌 3=우.
    static void MakeRegionEdge(Transform parent, Color c, float th, int side)
    {
        var go = MakeImage("Edge" + side, parent, Vector2.zero, Vector2.zero, c);
        var rt = go.GetComponent<RectTransform>();
        go.GetComponent<Image>().raycastTarget = false;
        switch (side)
        {
            case 0: rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, th); break;
            case 1: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(0, th); break;
            case 2: rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(th, 0); break;
            default: rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(th, 0); break;
        }
        rt.anchoredPosition = Vector2.zero;
    }

    // 범위 강조 한 겹 라인(9-slice). ppuMult 클수록 라인 얇아짐. inset px만큼 사방 안으로(0=region 경계). 흰색. Image 반환.
    static Image MakeRegionFrameLayer(Transform parent, Sprite spr, float ppuMult, float inset)
    {
        var go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        var img = go.GetComponent<Image>();
        img.sprite = spr; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppuMult;
        img.color = Color.white; img.raycastTarget = false;
        return img;
    }

    // 스크롤+그리드 빌드 (body 안 fraction 영역, columns열). InventoryGridUI 반환. 창고/가방 공용.
    // withScroll=false 면 스크롤바/세로스크롤 없음(파밍상자처럼 한 줄 고정용).
    static InventoryGridUI BuildScrollGrid(GameObject body, string name, Vector2 aMin, Vector2 aMax, int columns, float rightInset = 14f, bool withScroll = true)
    {
        var scrollGo = MakeEmpty(name, body.transform, Vector2.zero, Vector2.zero);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = aMin; srt.anchorMax = aMax;
        srt.offsetMin = new Vector2(14, 8); srt.offsetMax = new Vector2(-rightInset, -76);   // 우측 인셋 클수록 스크롤바가 seam에서 멀어짐
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = Color.clear;
        var scrollMask = scrollGo.AddComponent<RectMask2D>();
        // 마스크 클립 영역을 살짝 넓힌다(음수 padding=확장) -> 가장자리(맨윗줄/좌/우) 칸의 강조 프레임+팝이 안 잘리게.
        scrollMask.padding = new Vector4(-12f, -12f, -12f, -12f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = withScroll; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var content = MakeEmpty("Content", scrollGo.transform, Vector2.zero, Vector2.zero);
        var crt = content.GetComponent<RectTransform>();
        float blockW = columns * 90f + (columns - 1) * 9f;        // 그리드 블록 폭(열 수 기준)
        float gutter = withScroll ? 8f : 0f;                      // 우측 스크롤바 거터
        crt.anchorMin = new Vector2(0.5f, 1f); crt.anchorMax = new Vector2(0.5f, 1f); crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(blockW, 0f);                  // 가로 고정폭=블록 -> 슬롯 그리드가 패널 가운데 대칭. 세로는 ContentSizeFitter.
        crt.anchoredPosition = new Vector2(-gutter * 0.5f, 0f);   // 거터/2만큼 좌로 -> 우측에 스크롤바 자리
        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90, 90); grid.spacing = new Vector2(9, 9);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;   // 좌상단부터 채우기(순서 유지). 좌우대칭은 콘텐츠를 블록 폭으로 가운데 둬서 처리.
        grid.padding = new RectOffset(0, 0, 20, 0);   // 윗줄을 내려 범위강조 프레임 상단 코너 + 호버 커짐이 첫 칸과 안 겹치게
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = srt;
        scroll.content = crt;

        if (withScroll)
        {
            var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(scrollGo.transform, false);
            var sbRt = sbGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 0); sbRt.anchorMax = new Vector2(1, 1); sbRt.pivot = new Vector2(1, 1);
            sbRt.sizeDelta = new Vector2(6, 0); sbRt.anchoredPosition = new Vector2(-1, 0);
            var sbTrack = sbGo.GetComponent<Image>();
            var trackSpr = LoadPartSprite(PartDir + "/scrollbar_track.png", new Vector4(0, 8, 0, 8));
            if (trackSpr != null) { sbTrack.sprite = trackSpr; sbTrack.type = Image.Type.Sliced; sbTrack.color = Color.white; }
            else { sbTrack.sprite = RoundedSprite(); sbTrack.type = Image.Type.Sliced; sbTrack.color = RGBA(40, 46, 54, 0.18f); }
            var sb = sbGo.GetComponent<Scrollbar>(); sb.direction = Scrollbar.Direction.BottomToTop;
            var slideArea = new GameObject("Sliding Area", typeof(RectTransform));
            slideArea.transform.SetParent(sbGo.transform, false);
            Stretch(slideArea.GetComponent<RectTransform>());
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slideArea.transform, false);
            var hRt = handle.GetComponent<RectTransform>(); Stretch(hRt);
            var hImg = handle.GetComponent<Image>();
            var handleSpr = LoadPartSprite(PartDir + "/scrollbar_handle.png", new Vector4(0, 8, 0, 8));
            if (handleSpr != null) { hImg.sprite = handleSpr; hImg.type = Image.Type.Sliced; hImg.color = Color.white; }
            else { hImg.sprite = RoundedSprite(); hImg.type = Image.Type.Sliced; hImg.color = ScrollHandle; }
            sb.targetGraphic = hImg; sb.handleRect = hRt;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        var gridUI = scrollGo.AddComponent<InventoryGridUI>();
        var gso = new SerializedObject(gridUI);
        gso.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        gso.FindProperty("dropFrameSprite").objectReferenceValue = LoadPartSprite(PartDir + "/hl_slot_frame@2x.png", new Vector4(44, 44, 44, 44));
        gso.FindProperty("slotGrid").objectReferenceValue = content.transform;
        gso.ApplyModifiedProperties();
        return gridUI;
    }

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
    public static void SetColor(this SerializedProperty p, Color c) { if (p != null) p.colorValue = c; }
}
