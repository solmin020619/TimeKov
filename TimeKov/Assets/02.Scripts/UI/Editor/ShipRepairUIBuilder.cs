// =====================================================================
// ShipRepairUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/우주선 수리 UI 생성 -> Canvas 안에 ShipRepairPanel 생성 + ref 연결.
// 코어 UI 방식(불투명 풀스크린 + 중앙 1560x900 콘텐츠) + 격납고 관제 무드:
//   배경 = 도면 그리드(별밭 아님) / 좌 = sci-fi 프레임 + 복원도 링(눈금/후광) / 우 = 스탯/부품/버튼.
// 디자인 PNG 세트 = Resources/ShipRepair/1~7 (1우주선홀로 2배경 3행패널 4버튼 5링 6노드 7부품아이콘).
//   전부 '있으면 사용, 없으면 절차 스프라이트 폴백'. 코어 에셋(Btn_Close_Sci, Panel_Frame_Sci)도 재사용.
//
// [08-02] 부품 전시대 / 특수부품 칩 / 호버 툴팁도 여기서 만든다(예전엔 런타임 생성이라 에디터에 없었다).
//   개수가 데이터에 따라 변하는 것(레벨 pip / 특수부품 칩)은 '템플릿' 1개만 만들어 꺼두고,
//   런타임(ShipRepairUI)이 그걸 복제한다. 그래서 런타임 코드에는 계층 생성이 남아 있지 않다.
//   저장이 안 되는 절차 스프라이트에는 RuntimeGeneratedSprite 를 붙여 실행 시 스스로 채우게 한다.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ShipRepairUIBuilder
{
    [MenuItem("Tools/TIMEKOV/우주선 수리 UI 생성")]
    public static void Build()
    {
        // 루트 캔버스로만 찾는다(중첩 Canvas 를 잡으면 UI 안에 UI 가 파묻힌다 - 실제로 겪은 사고).
        Canvas canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 루트 Canvas가 없습니다.", "확인");
            return;
        }

        var existing = Object.FindObjectsByType<ShipRepairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 0)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                $"기존 우주선 수리 패널 {existing.Length}개가 있습니다.\n모두 삭제하고 새로 만들까요?",
                "새로 만들기", "취소");
            if (!replace) return;
            foreach (var u in existing) if (u != null) Object.DestroyImmediate(u.gameObject);
        }

        // 호스트(항상 active) — Awake 가 돌아 Instance 등록. 보이는 건 Root 만 토글.
        var host = new GameObject("ShipRepairPanel", typeof(RectTransform));
        var hostRt = host.GetComponent<RectTransform>();
        hostRt.SetParent(canvas.transform, false);
        Stretch(hostRt);

        var ui = host.AddComponent<ShipRepairUI>();
        var so = new SerializedObject(ui);

        // ── panelRoot = 풀스크린 오버레이 ──
        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(hostRt, false);
        var rootRt = root.GetComponent<RectTransform>();
        Stretch(rootRt);
        SetRef(so, "panelRoot", root);

        // 런타임 Resources.Load 로 쓰는 디자인 PNG 임포트 세팅 보장 (스프라이트/비압축 + 행패널 9-slice 보더).
        // 2/배경, 4/버튼, 5/링은 아래 사용처에서 LoadSprAt 로 세팅된다.
        for (int lv = 1; lv <= 5; lv++)
            LoadSprAt($"Assets/Resources/ShipRepair/1/ship_holo_lv{lv}.png", 0, quiet: true);
        LoadSprAt("Assets/Resources/ShipRepair/3/row_panel.png",    RowPanelBorder, quiet: true);
        LoadSprAt("Assets/Resources/ShipRepair/3/row_panel_hl.png", RowPanelBorder, quiet: true);
        LoadSprAt("Assets/Resources/ShipRepair/6/node_off.png", 0, quiet: true);
        LoadSprAt("Assets/Resources/ShipRepair/6/node_on.png",  0, quiet: true);
        LoadSprAt("Assets/Resources/ShipRepair/7/icon_part.png", 0, quiet: true);

        // 어두운 격납고 배경(우주 아님) + 뒤 클릭 차단.
        // 디자인 배경 PNG(Resources/ShipRepair/2/bg_hangar_blueprint.png)가 있으면 그걸 쓰고,
        // 없으면 절차 그라데이션 + 도면 그리드로 폴백.
        var bg = MakeImage("Backdrop", rootRt, Vector2.zero, Vector2.zero, Color.white);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.GetComponent<Image>();
        bgImg.raycastTarget = true;
        var bgSpr = LoadSprAt("Assets/Resources/ShipRepair/2/bg_hangar_blueprint.png", 0, quiet: true);
        if (bgSpr != null)
        {
            bgImg.sprite = bgSpr;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;   // 풀스크린 스트레치
        }
        else
        {
            bgImg.sprite = null;
            var bgGrad = bg.AddComponent<UIFrostGradient>();
            bgGrad.topColor    = RGBA(15, 21, 32, 1f);   // 불투명 = 게임 화면 완전히 가림(코어 방식)
            bgGrad.bottomColor = RGBA(6, 9, 14, 1f);

            // 도면(블루프린트) 그리드 + 레이더 링/비네트 = PNG 없을 때만(절차 폴백)
            BuildBlueprintGrid(rootRt);
            BuildBackdropDeco(rootRt);
        }

        // 홀로그램 영역 뒤 대형 후광 (배경 레이어)
        var bgGlow = MakeImage("HoloBackGlow", rootRt, new Vector2(860, 860), new Vector2(-390, 20), new Color(0.42f, 0.83f, 1f, 0.05f));
        Procedural(bgGlow, RuntimeGeneratedSprite.Shape.Disc, 256);
        bgGlow.GetComponent<Image>().raycastTarget = false;

        // ── 콘텐츠 컨테이너 (투명, 중앙 1560x900) ──
        // 코어 UI 방식: 불투명 풀스크린 배경 위에 콘텐츠만 띄운다(별도 카드 프레임/블러 박스 없음).
        var card = MakeImage("Content", rootRt, new Vector2(1560, 900), Vector2.zero, new Color(1f, 1f, 1f, 0f));
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().raycastTarget = false;

        // 4코너 브래킷 (sci-fi 관제 프레임 액센트)
        BuildCornerBrackets(crt, new Vector2(1560, 900));

        // ── 헤더 ──
        var eyebrow = MakeTMP("Eyebrow", crt, Vector2.zero, Vector2.zero, "폐선체 복원 관제", 14, RGBA(106, 212, 255, 0.9f), TextAlignmentOptions.Left);
        AnchorTop(eyebrow.rectTransform, 36, -52, -30);
        eyebrow.characterSpacing = 6f;

        var title = MakeTMP("Title", crt, Vector2.zero, Vector2.zero, "우주선 수리", 34, Hex("EAF3FB"), TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        AnchorTop(title.rectTransform, 36, -96, -56);

        // 닫기 = 코어와 동일한 sci-fi 육각 버튼 PNG (X는 그림에 포함, 어두운 배경서 또렷)
        var closeBtn = MakeButton("CloseButton", crt, new Vector2(60, 60), Vector2.zero, "", 22, Color.white);
        var clrt = closeBtn.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(1, 1); clrt.pivot = new Vector2(1, 1);
        clrt.anchoredPosition = new Vector2(-18, -14);
        SetSpr(closeBtn, LoadSprAt("Assets/Resources/CoreUI/sprites/Btn_Close_Sci.png"), Image.Type.Simple);
        var closeLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (closeLabel != null) closeLabel.gameObject.SetActive(false);   // X 글자는 PNG에 그려져 있어 중복 제거
        var clBtn = closeBtn.GetComponent<Button>();
        clBtn.transition = Selectable.Transition.ColorTint;
        var ccb = clBtn.colors;
        ccb.normalColor      = Color.white;
        ccb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        ccb.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        ccb.selectedColor    = Color.white;
        ccb.colorMultiplier  = 1f; ccb.fadeDuration = 0.1f;
        clBtn.colors = ccb;
        HoverFx(closeBtn, 1.08f, scaleOnly: true);   // 밝기는 ColorTint 담당이라 확대만
        SetRef(so, "closeButton", clBtn);

        var hdiv = MakeImage("HeaderDivider", crt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.5f));
        AnchorTopStretch(hdiv.GetComponent<RectTransform>(), 28, 28, -110, -108);
        hdiv.GetComponent<Image>().raycastTarget = false;

        // 구분선 왼쪽 밝은 시안 액센트 세그먼트
        var hacc = MakeImage("HeaderAccent", crt, Vector2.zero, Vector2.zero, RGBA(106, 212, 255, 0.9f));
        AnchorTopStretch(hacc.GetComponent<RectTransform>(), 28, 1330, -112, -108);
        hacc.GetComponent<Image>().raycastTarget = false;

        // ── 레벨 사다리 ──
        var levelText = MakeTMP("LevelText", crt, Vector2.zero, Vector2.zero, "수리 단계  Lv.1 / 5", 19, Hex("EAF3FB"), TextAlignmentOptions.Left);
        levelText.fontStyle = FontStyles.Bold;
        AnchorTop(levelText.rectTransform, 36, -150, -122);
        SetRef(so, "levelText", levelText);

        var pipGo = new GameObject("PipContainer", typeof(RectTransform));
        pipGo.transform.SetParent(crt, false);
        var pipRt = pipGo.GetComponent<RectTransform>();
        pipRt.anchorMin = new Vector2(0, 1); pipRt.anchorMax = new Vector2(1, 1); pipRt.pivot = new Vector2(0.5f, 1);
        pipRt.offsetMin = new Vector2(240, -154); pipRt.offsetMax = new Vector2(-36, -118);
        var hlg = pipGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        SetRef(so, "pipContainer", pipRt);

        // pip 원본 1개. 레벨 수는 데이터(ShipRepairManager.MaxLevel)라 런타임이 이걸 복제한다.
        var nodeOffSpr = LoadSprAt("Assets/Resources/ShipRepair/6/node_off.png", 0, quiet: true);
        var nodeOnSpr  = LoadSprAt("Assets/Resources/ShipRepair/6/node_on.png",  0, quiet: true);
        bool pipArt = nodeOffSpr != null && nodeOnSpr != null;

        var pipTpl = MakeImage("PipTemplate", pipRt, new Vector2(36, 36), Vector2.zero, pipArt ? Color.white : RGBA(77, 92, 112, 1f));
        var pipTplLe = pipTpl.AddComponent<LayoutElement>();
        pipTplLe.preferredWidth = pipArt ? 36f : 30f;
        pipTplLe.preferredHeight = pipArt ? 36f : 30f;
        var pipTplImg = pipTpl.GetComponent<Image>();
        pipTplImg.sprite = nodeOffSpr;                 // 없으면 색만으로 표시(런타임이 상태색을 넣는다)
        pipTplImg.raycastTarget = false;
        pipTpl.SetActive(false);                       // 원본은 꺼둔다
        SetRef(so, "pipTemplate", pipTplImg);
        SetRef(so, "pipOnSprite", nodeOnSpr);
        SetRef(so, "pipOffSprite", nodeOffSpr);

        // 레벨별 우주선 홀로그램 아트 (Lv.1 ~ Lv.5). 예전엔 런타임 Resources.Load 였다.
        var shipArt = new Object[5];
        for (int lv = 1; lv <= 5; lv++)
            shipArt[lv - 1] = LoadSprAt($"Assets/Resources/ShipRepair/1/ship_holo_lv{lv}.png", 0, quiet: true);
        SetRefArray(so, "shipHologramByLevel", shipArt);

        // ── 좌측 홀로그램 (복원도 링) ──
        var hero = MakeImage("HoloStage", crt, Vector2.zero, Vector2.zero, Color.white);
        var heroRt = hero.GetComponent<RectTransform>();
        heroRt.anchorMin = new Vector2(0, 0); heroRt.anchorMax = new Vector2(0.5f, 1);
        heroRt.offsetMin = new Vector2(36, 64); heroRt.offsetMax = new Vector2(-16, -170);
        // sci-fi 프레임 PNG (코어 효과패널과 동일 에셋 = 톤 통일, 테두리/코너 포함)
        SetSpr(hero, LoadSprAt("Assets/Resources/CoreUI/sprites/Panel_Frame_Sci.png", 44), Image.Type.Sliced);
        hero.GetComponent<Image>().raycastTarget = false;

        // 링 뒤 은은한 시안 후광 (런타임에 알파가 맥동)
        var holoGlow = MakeImage("HoloGlow", heroRt, new Vector2(560, 560), new Vector2(0, 12), new Color(0.42f, 0.83f, 1f, 0.07f));
        Procedural(holoGlow, RuntimeGeneratedSprite.Shape.Disc, 256);
        holoGlow.GetComponent<Image>().raycastTarget = false;
        SetRef(so, "holoGlow", holoGlow.GetComponent<Image>());

        // 우주선 아트 슬롯 — 런타임이 레벨에 맞는 스프라이트를 넣고 켠다
        var shipSlot = MakeImage("ShipHologramSlot", heroRt, new Vector2(420, 280), new Vector2(0, 12), new Color(1, 1, 1, 1));
        shipSlot.GetComponent<Image>().raycastTarget = false;
        shipSlot.GetComponent<Image>().enabled = false;
        SetRef(so, "shipHologramSlot", shipSlot.GetComponent<Image>());

        // 복원도 링 (트랙 + 게이지) - 디자인 PNG(5/) 우선, 없으면 절차 링 폴백.
        // 절차 링과 PNG 링은 굵기/반경이 달라 둘을 섞으면 어긋남 -> 반드시 짝으로만 사용.
        var trackSpr = LoadSprAt("Assets/Resources/ShipRepair/5/ring_track.png", 0, quiet: true);
        var fillSpr  = LoadSprAt("Assets/Resources/ShipRepair/5/ring_fill.png",  0, quiet: true);
        bool ringArt = trackSpr != null && fillSpr != null;

        var track = MakeImage("RingTrack", heroRt, new Vector2(440, 440), new Vector2(0, 12),
            ringArt ? Color.white : RGBA(70, 96, 128, 0.35f));
        var trackImg = track.GetComponent<Image>();
        trackImg.sprite = trackSpr;
        trackImg.raycastTarget = false;
        if (!ringArt) Procedural(track, RuntimeGeneratedSprite.Shape.Ring, 400).ringThickness = 13f;

        var gauge = MakeImage("RingGauge", heroRt, new Vector2(440, 440), new Vector2(0, 12), RGBA(106, 212, 255, 1f));
        var gaugeImg = gauge.GetComponent<Image>();
        gaugeImg.sprite = fillSpr;   // 채움 링 PNG=흰색(시안 틴트용)
        gaugeImg.raycastTarget = false;
        if (!ringArt) Procedural(gauge, RuntimeGeneratedSprite.Shape.Ring, 400).ringThickness = 13f;
        gaugeImg.type = Image.Type.Filled;
        gaugeImg.fillMethod = Image.FillMethod.Radial360;
        gaugeImg.fillOrigin = (int)Image.Origin360.Top;
        gaugeImg.fillClockwise = true;
        gaugeImg.fillAmount = 0f;
        SetRef(so, "ringGauge", gaugeImg);

        // 링 둘레 눈금 - 트랙 PNG 에는 눈금이 그려져 있어 절차 눈금은 폴백일 때만
        if (!ringArt)
        {
            for (int i = 0; i < 20; i++)
            {
                float deg = i * 18f;
                bool majorTick = (i % 5 == 0);
                float rad = deg * Mathf.Deg2Rad;
                const float tickR = 250f;
                var tick = MakeImage($"Tick{i}", heroRt,
                    new Vector2(majorTick ? 3f : 2f, majorTick ? 16f : 9f),
                    new Vector2(Mathf.Sin(rad) * tickR, 12f + Mathf.Cos(rad) * tickR),
                    majorTick ? RGBA(106, 212, 255, 0.55f) : RGBA(122, 150, 180, 0.30f));
                tick.transform.localRotation = Quaternion.Euler(0, 0, -deg);
                tick.GetComponent<Image>().raycastTarget = false;
            }
        }

        // 스캔라인 (위아래로 흐르는 홀로그램 주사선 - 런타임 ShipRepairUI 가 구동)
        var scan = MakeImage("ScanLine", heroRt, Vector2.zero, Vector2.zero, RGBA(106, 212, 255, 1f));
        var scanRt = scan.GetComponent<RectTransform>();
        scanRt.anchorMin = new Vector2(0, 0.5f); scanRt.anchorMax = new Vector2(1, 0.5f); scanRt.pivot = new Vector2(0.5f, 0.5f);
        scanRt.offsetMin = new Vector2(26, -16); scanRt.offsetMax = new Vector2(-26, 16);
        var scanImg = scan.GetComponent<Image>();
        scanImg.type = Image.Type.Simple;
        scanImg.raycastTarget = false;
        // 위 투명 -> 아래 발광 페이드 (VGrad 는 알파를 못 살림). 알파만 쓰는 모양이라 색은 Image.color 가 정한다.
        var scanGen = Procedural(scan, RuntimeGeneratedSprite.Shape.VFade, 64);
        scanGen.topColor = new Color(1f, 1f, 1f, 0f);
        scanGen.bottomColor = new Color(1f, 1f, 1f, 40f / 255f);
        var scanEdge = MakeImage("Edge", scanRt, Vector2.zero, Vector2.zero, RGBA(106, 212, 255, 0.35f));
        var seRt = scanEdge.GetComponent<RectTransform>();
        seRt.anchorMin = new Vector2(0, 0); seRt.anchorMax = new Vector2(1, 0); seRt.pivot = new Vector2(0.5f, 0);
        seRt.offsetMin = new Vector2(0, 0); seRt.offsetMax = new Vector2(0, 2);
        scanEdge.GetComponent<Image>().raycastTarget = false;
        SetRef(so, "scanLine", scanRt);

        // 링 중앙은 우주선 홀로그램 자리 - 라벨+퍼센트를 링 아래에 계기판식으로 쌓는다
        // (위쪽 캡션은 프레임 상단 장식선과 겹쳐서 폐기)
        var capTop = MakeTMP("HoloCaption", heroRt, new Vector2(260, 24), new Vector2(0, -212), "선체 복원도", 15, RGBA(150, 178, 204, 1f), TextAlignmentOptions.Center);
        capTop.characterSpacing = 4f;

        var pct = MakeTMP("RestorePercent", heroRt, new Vector2(320, 64), new Vector2(0, -262), "0%", 46, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Center);
        pct.fontStyle = FontStyles.Bold;
        SetRef(so, "restorePercentText", pct);

        // ── 우측 컬럼 ──
        var col = new GameObject("RightCol", typeof(RectTransform));
        col.transform.SetParent(crt, false);
        var colRt = col.GetComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0.5f, 0); colRt.anchorMax = new Vector2(1, 1);
        colRt.offsetMin = new Vector2(16, 64); colRt.offsetMax = new Vector2(-40, -170);

        // "다음 수리" 헤더 배지 (절차 그라데이션 - PNG 늘림 이음매 없음)
        var nhBg = MakeImage("NextHeaderBg", colRt, Vector2.zero, Vector2.zero, Color.white);
        var nhImg = nhBg.GetComponent<Image>();
        nhImg.type = Image.Type.Sliced;
        nhImg.raycastTarget = false;
        var nhGen = Procedural(nhBg, RuntimeGeneratedSprite.Shape.RoundedRectVGrad, 64);
        nhGen.radius = 18;
        nhGen.topColor = RGBA(28, 40, 56, 235f / 255f);
        nhGen.bottomColor = RGBA(12, 18, 28, 235f / 255f);
        AnchorTopStretch((RectTransform)nhBg.transform, 0, 400, -42, -2);

        var nextHeader = MakeTMP("NextHeader", colRt, Vector2.zero, Vector2.zero, "다음 수리   Lv.1 -> Lv.2", 17, Hex("AEE3FF"), TextAlignmentOptions.Left);
        nextHeader.fontStyle = FontStyles.Bold;
        AnchorTopStretch(nextHeader.rectTransform, 20, 4, -38, -6);
        SetRef(so, "nextHeaderText", nextHeader);

        // 스탯 3행
        string[] statLabels = { "건축 범위", "설비 연료", "공장 가동속도" };
        float[] statTops = { -58f, -120f, -182f };
        var statVals = new Object[3];
        var statNames = new Object[3];
        var statRows = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            statVals[i] = MakeStatRow(colRt, statTops[i], statLabels[i], out var rowImg, out var nameT);
            statNames[i] = nameT;
            statRows[i] = rowImg;
        }
        SetRefArray(so, "statValueTexts", statVals);
        SetRefArray(so, "statNameTexts", statNames);
        SetRefArray(so, "statRowImages", statRows);
        SetRef(so, "statRowSprite", LoadSprAt("Assets/Resources/ShipRepair/3/row_panel.png", RowPanelBorder, quiet: true));
        SetRef(so, "statRowHighlightSprite", LoadSprAt("Assets/Resources/ShipRepair/3/row_panel_hl.png", RowPanelBorder, quiet: true));

        var sdiv = MakeImage("StatDivider", colRt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.4f));
        AnchorTopStretch(sdiv.GetComponent<RectTransform>(), 4, 4, -244, -243);
        sdiv.GetComponent<Image>().raycastTarget = false;

        // 부품 영역 = 전시대 카드 + 특수부품 칩. 아래 여백은 76(버튼 위까지) - 칩이 늘어나도 링과 안 겹친다.
        var partsGo = new GameObject("PartsContent", typeof(RectTransform));
        partsGo.transform.SetParent(colRt, false);
        var partsRt = partsGo.GetComponent<RectTransform>();
        partsRt.anchorMin = new Vector2(0, 0); partsRt.anchorMax = new Vector2(1, 1);
        partsRt.offsetMin = new Vector2(0, 76); partsRt.offsetMax = new Vector2(0, -254);
        var pvlg = partsGo.AddComponent<VerticalLayoutGroup>();
        pvlg.spacing = 10f; pvlg.childAlignment = TextAnchor.MiddleCenter;
        pvlg.childControlWidth = true; pvlg.childControlHeight = true;
        pvlg.childForceExpandWidth = true; pvlg.childForceExpandHeight = false;

        BuildPartDisplay(partsRt, so);
        BuildExtraParts(partsRt, so);

        // 수리 버튼 (흑백 버튼 PNG + 상태색 틴트 - 런타임 BtnReady/BtnLocked 가 색을 바꿈)
        var repair = MakeButton("RepairButton", colRt, Vector2.zero, Vector2.zero, "수리 실행", 22, new Color(0.20f, 0.66f, 0.95f, 1f));
        var repImg = repair.GetComponent<Image>();
        var btnSpr = LoadSprAt("Assets/Resources/ShipRepair/4/btn_repair.png", new Vector4(30, 24, 30, 24), quiet: true);
        repImg.sprite = btnSpr;
        repImg.type = Image.Type.Sliced;
        repImg.color = new Color(0.20f, 0.66f, 0.95f, 1f);
        if (btnSpr == null)
        {
            var repGen = Procedural(repair, RuntimeGeneratedSprite.Shape.RoundedRectVGrad, 64);
            repGen.radius = 16;
            repGen.topColor = Color.white;
            repGen.bottomColor = RGBA(185, 205, 222, 1f);
        }
        var repRt = repair.GetComponent<RectTransform>();
        repRt.anchorMin = new Vector2(0, 0); repRt.anchorMax = new Vector2(1, 0); repRt.pivot = new Vector2(0.5f, 0);
        repRt.offsetMin = new Vector2(0, 6); repRt.offsetMax = new Vector2(0, 68);

        // 유니티 기본 틴트는 호버 변화가 거의 안 보임 -> 닫기 버튼과 같은 명시 틴트.
        // 잠금(부품 부족) 어둡기는 이미지색이 담당하므로 disabled 틴트는 중립(이중 어둡힘 방지).
        var repBtn = repair.GetComponent<Button>();
        repBtn.transition = Selectable.Transition.ColorTint;
        var rcb = repBtn.colors;
        rcb.normalColor      = Color.white;
        rcb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        rcb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
        rcb.selectedColor    = Color.white;
        rcb.disabledColor    = Color.white;
        rcb.colorMultiplier  = 1f; rcb.fadeDuration = 0.1f;
        repBtn.colors = rcb;
        HoverFx(repair, 1.02f, scaleOnly: true);   // 밝기는 ColorTint 담당이라 확대만

        SetRef(so, "repairButton", repBtn);
        SetRef(so, "repairButtonText", repair.GetComponentInChildren<TextMeshProUGUI>());

        // ── 푸터 ──
        var footer = MakeTMP("Footer", crt, Vector2.zero, Vector2.zero,
            "Lv.5 완료 시 - 격납 펜스 해제 / 탈출 시도 가능", 14, RGBA(130, 148, 168, 0.95f), TextAlignmentOptions.Center);
        AnchorBottomStretch(footer.rectTransform, 36, 36, 18, 42);

        // ── 특수부품 호버 툴팁 (칩 위에 뜬다. 항상 맨 위로 오게 마지막에 만든다) ──
        BuildChipTooltip(rootRt, so);

        so.ApplyModifiedProperties();

        // 절차 스프라이트를 지금 한 번 채워 에디터에서도 제 모양으로 보이게 한다.
        UIBuilderUtil.ApplyGeneratedSprites(host);

        // 패널을 기본 비활성으로 둔다 → 에디터/플레이 시작 시 화면에 안 보임(깔끔).
        // 런타임엔 우주선 터미널 F 가 ShipRepairUI.EnsureInstance() 로 이 오브젝트를 찾아
        // 활성화한 뒤 Open() 한다. 그래서 호스트를 꺼둬도 정상 작동.
        root.SetActive(false);
        host.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = host;

        EditorUtility.DisplayDialog("완료",
            "우주선 수리 UI 생성 완료!\n\n" +
            "- ShipRepairPanel 은 기본 비활성(꺼둠) — 그대로 두면 됨\n" +
            "- 런타임에 우주선 F 로 자동 활성화되어 열림\n" +
            "- 레벨/부품은 ShipRepairManager 인스펙터에서 정의\n" +
            "- 디자인 PNG(Resources/ShipRepair/1~7)는 자동 연결(없으면 폴백)\n" +
            "- 레벨 pip / 특수부품 칩은 PipTemplate, ChipTemplate 을 런타임이 복제한다\n" +
            "  (원본은 꺼져 있는 게 정상 - 지우지 말 것)\n" +
            "- Ctrl+S 로 씬 저장", "확인");
    }

    // ── 부품 전시대 ──────────────────────────────────────────────────
    //   [보유 N] --- ((대형 아이콘 + 후광 + 링 2겹 회전)) --- [필요 M]
    //                          수리 부품
    // 링 회전/후광 맥동은 런타임(ShipRepairUI.DriveAmbient)이 구동한다.
    static void BuildPartDisplay(RectTransform parent, SerializedObject so)
    {
        var go = new GameObject("PartDisplay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 230f;
        var root = (RectTransform)go.transform;

        Vector2 iconC = new Vector2(0f, 34f);   // 아이콘 중심 (위쪽에 배치, 아래로 이름)

        // 후광 (맥동)
        var glow = MakeImage("IconGlow", root, new Vector2(230, 230), iconC, new Color(0.42f, 0.83f, 1f, 0.10f));
        Procedural(glow, RuntimeGeneratedSprite.Shape.Disc, 256);
        glow.GetComponent<Image>().raycastTarget = false;
        SetRef(so, "partIconGlow", glow.GetComponent<Image>());

        // 아이콘 둘레 링 = 복원도 링과 같은 아트(시각 언어 통일) + 이 링이 곧 부품 게이지.
        var ringTrackSpr = LoadSprAt("Assets/Resources/ShipRepair/5/ring_track.png", 0, quiet: true);
        var ringFillSpr  = LoadSprAt("Assets/Resources/ShipRepair/5/ring_fill.png",  0, quiet: true);
        bool ringArt = ringTrackSpr != null && ringFillSpr != null;

        var trackGo = MakeImage("RingTrack", root, new Vector2(200, 200), iconC,
            ringArt ? Color.white : RGBA(70, 96, 128, 0.35f));
        var trackImg = trackGo.GetComponent<Image>();
        trackImg.sprite = ringTrackSpr;
        trackImg.raycastTarget = false;
        if (!ringArt) Procedural(trackGo, RuntimeGeneratedSprite.Shape.Ring, 200).ringThickness = 4f;
        SetRef(so, "partRingTrack", (RectTransform)trackGo.transform);

        var gaugeGo = MakeImage("RingGauge", root, new Vector2(200, 200), iconC, new Color(0.42f, 0.83f, 1f, 1f));
        var gaugeImg = gaugeGo.GetComponent<Image>();
        gaugeImg.sprite = ringFillSpr;
        gaugeImg.type = Image.Type.Filled;
        gaugeImg.fillMethod = Image.FillMethod.Radial360;
        gaugeImg.fillOrigin = (int)Image.Origin360.Top;
        gaugeImg.fillClockwise = true;
        gaugeImg.fillAmount = 0f;
        gaugeImg.raycastTarget = false;
        if (!ringArt) Procedural(gaugeGo, RuntimeGeneratedSprite.Shape.Ring, 200).ringThickness = 4f;
        SetRef(so, "partGauge", gaugeImg);

        // 아이콘 (크게)
        var iconSpr = LoadSprAt("Assets/Resources/ShipRepair/7/icon_part.png", 0, quiet: true);
        var iconGo = MakeImage("Icon", root, new Vector2(140, 140), iconC, Color.white);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = iconSpr;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = iconSpr != null;   // 아트가 없으면 흰 사각형이 뜨므로 아예 끈다

        // 아이콘 양옆 연결 라인 (전시대 느낌)
        var lineCol = new Color(0.42f, 0.83f, 1f, 0.30f);
        MakeImage("LineL", root, new Vector2(96, 2), iconC + new Vector2(-160, 0), lineCol)
            .GetComponent<Image>().raycastTarget = false;
        MakeImage("LineR", root, new Vector2(96, 2), iconC + new Vector2(160, 0), lineCol)
            .GetComponent<Image>().raycastTarget = false;

        // 좌 = 보유 / 우 = 필요 (라벨 + 큰 숫자)
        SetRef(so, "partOwnedText", MakeBigNumber(root, "보유", -252f, iconC.y));
        SetRef(so, "partRequiredText", MakeBigNumber(root, "필요", 252f, iconC.y));

        // 아이콘 아래 부품 이름 (내용은 데이터에서 온다)
        var nameT = MakeTMP("PartName", root, new Vector2(400, 30), new Vector2(0, -84),
            "", 20, new Color(0.82f, 0.89f, 0.96f, 1f), TextAlignmentOptions.Center);
        nameT.fontStyle = FontStyles.Bold;
        nameT.textWrappingMode = TextWrappingModes.Normal;   // 긴 이름(다른 언어)도 상자 안에 들어오게
        SetRef(so, "partNameText", nameT);
    }

    // "보유"/"필요" 라벨 + 그 아래 큰 숫자. 숫자 TMP 를 돌려준다(런타임이 값을 넣는다).
    static TextMeshProUGUI MakeBigNumber(RectTransform parent, string label, float x, float iconY)
    {
        var lbl = MakeTMP($"{label}Label", parent, new Vector2(140, 22), new Vector2(x, iconY + 34f),
            label, 14, new Color(0.44f, 0.51f, 0.60f, 1f), TextAlignmentOptions.Center);
        lbl.characterSpacing = 6f;

        var num = MakeTMP($"{label}Number", parent, new Vector2(160, 52), new Vector2(x, iconY - 12f),
            "0", 42, new Color(0.82f, 0.89f, 0.96f, 1f), TextAlignmentOptions.Center);
        num.fontStyle = FontStyles.Bold;
        return num;
    }

    // ── 특수 부품(전송 보상) 칩 ──────────────────────────────────────
    // 개수는 데이터(레벨별 특수부품 정의)라 여기서는 '템플릿 1개'만 만들고 런타임이 복제한다.
    static void BuildExtraParts(RectTransform parent, SerializedObject so)
    {
        // ★섹션 = 높이만 잡는 빈 컨테이너(순수 LayoutElement). HLG 를 섹션에 직접 붙이면
        //   childForceExpandHeight 가 flexibleHeight(>=1)를 만들어서, 부모 VerticalLayoutGroup 이
        //   남는 세로 공간만큼 섹션을 늘려버린다 = preferredHeight 가 무시됨(칩이 뚱뚱해지던 진짜 원인).
        //   그래서 HLG 는 '채우기 앵커' 자식(Chips)에 두고, 섹션은 LayoutElement 로 높이만 고정한다.
        var secGo = new GameObject("ExtraParts", typeof(RectTransform));
        secGo.transform.SetParent(parent, false);
        secGo.AddComponent<LayoutElement>().preferredHeight = 58f;   // ★칩 세로 높이 = 여기 하나만 바꾸면 됨
        var sec = (RectTransform)secGo.transform;
        SetRef(so, "extraChipSection", secGo);

        var rowGo = new GameObject("Chips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(sec, false);
        var rrt = (RectTransform)rowGo.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.offsetMin = new Vector2(6f, 1f); rrt.offsetMax = new Vector2(-6f, -1f);
        var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        SetRef(so, "extraChipContainer", rrt);

        // 칩 원본
        var chipGo = new GameObject("ChipTemplate", typeof(RectTransform), typeof(Image));
        chipGo.transform.SetParent(rrt, false);
        var chipImg = chipGo.GetComponent<Image>();
        var rowSpr = LoadSprAt("Assets/Resources/ShipRepair/3/row_panel.png", RowPanelBorder, quiet: true);
        chipImg.sprite = rowSpr;
        chipImg.type = Image.Type.Sliced;
        chipImg.raycastTarget = true;   // 호버 툴팁을 받으려면 레이캐스트 대상이어야 함
        if (rowSpr == null)
        {
            var chipGen = Procedural(chipGo, RuntimeGeneratedSprite.Shape.RoundedRectVGrad, 64);
            chipGen.radius = 12;
            chipGen.topColor = RGBA(34, 46, 64, 220f / 255f);
            chipGen.bottomColor = RGBA(16, 22, 32, 235f / 255f);
        }

        // 이름만 (보유/미보유는 색으로 구분)
        var nameT = MakeTMP("Name", chipGo.transform, Vector2.zero, Vector2.zero,
            "", 18, new Color(0.82f, 0.89f, 0.96f, 1f), TextAlignmentOptions.Center);
        var nrt = nameT.rectTransform;
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
        nrt.offsetMin = new Vector2(4f, 0f); nrt.offsetMax = new Vector2(-4f, 0f);
        nameT.fontStyle = FontStyles.Bold;
        nameT.enableAutoSizing = true; nameT.fontSizeMin = 12f; nameT.fontSizeMax = 18f;   // 칩 폭이 좁아 긴 이름은 줄여 넣는다

        var chip = chipGo.AddComponent<ShipExtraPartChip>();
        var chipSo = new SerializedObject(chip);
        SetRef(chipSo, "background", chipImg);
        SetRef(chipSo, "nameText", nameT);
        chipSo.ApplyModifiedPropertiesWithoutUndo();

        chipGo.SetActive(false);   // 원본은 꺼둔다
        SetRef(so, "extraChipTemplate", chip);
    }

    // ── 특수부품 호버 툴팁 ───────────────────────────────────────────
    // 시간에너지 UI(TransmissionComputerUI)의 툴팁을 따라한 간이 버전.
    //   칩에 마우스를 올리면 "이게 뭐지?" 하다가도 "몇 %에서 얻는 거구나" 하고 이해되게 한다.
    static void BuildChipTooltip(RectTransform panelRoot, SerializedObject so)
    {
        var go = new GameObject("ChipTooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panelRoot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);   // 아래 기준 = 칩 위에 뜸
        rt.sizeDelta = new Vector2(240, 84);

        var box = go.GetComponent<Image>();
        box.type = Image.Type.Sliced;
        box.color = new Color(0.06f, 0.10f, 0.18f, 0.98f);
        box.raycastTarget = false;
        Procedural(go, RuntimeGeneratedSprite.Shape.RoundedRect, 48).radius = 12;

        // ★풀네임 필수: 18.외부에셋 의 3D Outline(전역 네임스페이스)이 UnityEngine.UI.Outline 을 가린다.
        var outline = go.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.42f, 0.83f, 1f, 0.5f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var title = TooltipLine(rt, "Title", 12f, 24f, 17, new Color(0.42f, 0.83f, 1f, 1f), FontStyles.Bold);
        var body  = TooltipLine(rt, "Body",  38f, 20f, 14, new Color(0.82f, 0.89f, 0.96f, 1f), FontStyles.Normal);
        var state = TooltipLine(rt, "State", 60f, 18f, 12, new Color(0.44f, 0.51f, 0.60f, 1f), FontStyles.Normal);

        SetRef(so, "chipTooltip", go);
        SetRef(so, "chipTooltipTitle", title);
        SetRef(so, "chipTooltipBody", body);
        SetRef(so, "chipTooltipState", state);
        SetRef(so, "chipTooltipOutline", outline);

        go.SetActive(false);
    }

    // 툴팁 한 줄: 위에서 topY 만큼 내려온 높이 h 짜리 좌측정렬 텍스트.
    static TextMeshProUGUI TooltipLine(RectTransform parent, string name, float topY, float h, float fontSize, Color col, FontStyles style)
    {
        var t = MakeTMP(name, parent, Vector2.zero, Vector2.zero, "", fontSize, col, TextAlignmentOptions.Left);
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(14f, -(topY + h)); rt.offsetMax = new Vector2(-14f, -topY);
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;   // 본문이 상자를 넘어가지 않게
        return t;
    }

    // 스탯 1행: 디자인 행패널 PNG(코너 액센트 포함) + 라벨(좌)/값(우). 값 TMP 반환.
    // PNG 없으면 절차 그라데이션 + 좌측 시안 액센트 바 폴백.
    static TextMeshProUGUI MakeStatRow(Transform parent, float topY, string label, out Image rowImage, out TextMeshProUGUI nameText)
    {
        var row = new GameObject("StatRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(0, topY - 48); rt.offsetMax = new Vector2(0, topY);
        var rowSpr = LoadSprAt("Assets/Resources/ShipRepair/3/row_panel.png", RowPanelBorder, quiet: true);
        var rowImg = row.GetComponent<Image>();
        rowImg.sprite = rowSpr;
        rowImg.type = Image.Type.Sliced;
        rowImg.raycastTarget = true;   // 호버 확대/밝아짐을 받으려면 레이캐스트 대상이어야 함
        rowImage = rowImg;
        HoverFx(row, 1.015f, scaleOnly: false);

        if (rowSpr == null)
        {
            var rowGen = Procedural(row, RuntimeGeneratedSprite.Shape.RoundedRectVGrad, 64);
            rowGen.radius = 14;
            rowGen.topColor = RGBA(34, 46, 64, 215f / 255f);
            rowGen.bottomColor = RGBA(16, 22, 32, 235f / 255f);

            // 좌측 시안 액센트 바 (절차 폴백 전용 - 디자인판은 패널 자체에 액센트가 있음)
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(rt, false);
            var art = (RectTransform)accent.transform;
            art.anchorMin = new Vector2(0, 0.5f); art.anchorMax = new Vector2(0, 0.5f); art.pivot = new Vector2(0, 0.5f);
            art.sizeDelta = new Vector2(4, 26); art.anchoredPosition = new Vector2(8, 0);
            accent.GetComponent<Image>().color = RGBA(106, 212, 255, 0.85f);
            accent.GetComponent<Image>().raycastTarget = false;
        }

        // 디자인 패널은 양끝에 코너 회로 장식이 있어 텍스트를 넉넉히 안쪽으로 (붙으면 답답해 보임)
        float inset = rowSpr != null ? 52f : 22f;
        var nm = MakeTMP("Name", rt, Vector2.zero, Vector2.zero, label, 16, Hex("CDD8E5"), TextAlignmentOptions.Left);
        var nrt = nm.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(0.5f, 1);
        nrt.offsetMin = new Vector2(inset, 0); nrt.offsetMax = new Vector2(0, 0);
        nameText = nm;

        var val = MakeTMP("Value", rt, Vector2.zero, Vector2.zero, "-", 16, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Right);
        var vrt = val.rectTransform;
        vrt.anchorMin = new Vector2(0.4f, 0); vrt.anchorMax = new Vector2(1, 1);
        vrt.offsetMin = new Vector2(0, 0); vrt.offsetMax = new Vector2(rowSpr != null ? -52f : -16f, 0);
        return val;
    }

    // ── 배경 도면 그리드 (격납고 관제 무드, 에셋 불필요 - 얇은 선 Image 다수) ──
    static void BuildBlueprintGrid(Transform parent)
    {
        var gridRoot = new GameObject("BlueprintGrid", typeof(RectTransform));
        gridRoot.transform.SetParent(parent, false);
        Stretch(gridRoot.GetComponent<RectTransform>());

        const float step = 90f;
        for (int i = -11; i <= 11; i++)
        {
            bool majorLine = (i % 4 == 0);
            var v = new GameObject($"GV{i}", typeof(RectTransform), typeof(Image));
            v.transform.SetParent(gridRoot.transform, false);
            var rt = (RectTransform)v.transform;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(majorLine ? 2f : 1f, 0f);
            rt.anchoredPosition = new Vector2(i * step, 0f);
            var vImg = v.GetComponent<Image>();
            vImg.color = RGBA(106, 212, 255, majorLine ? 0.055f : 0.028f);
            vImg.raycastTarget = false;
        }
        for (int i = -6; i <= 6; i++)
        {
            bool majorLine = (i % 4 == 0);
            var h = new GameObject($"GH{i}", typeof(RectTransform), typeof(Image));
            h.transform.SetParent(gridRoot.transform, false);
            var rt = (RectTransform)h.transform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, majorLine ? 2f : 1f);
            rt.anchoredPosition = new Vector2(0f, i * step);
            var hImg = h.GetComponent<Image>();
            hImg.color = RGBA(106, 212, 255, majorLine ? 0.055f : 0.028f);
            hImg.raycastTarget = false;
        }

        // 주요 교차점 노드 점 (도면의 측점 마커 느낌)
        for (int gx = -8; gx <= 8; gx += 4)
            for (int gy = -4; gy <= 4; gy += 4)
            {
                var dot = new GameObject($"Node_{gx}_{gy}", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(gridRoot.transform, false);
                var drt = (RectTransform)dot.transform;
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(5f, 5f);
                drt.anchoredPosition = new Vector2(gx * step, gy * step);
                var dImg = dot.GetComponent<Image>();
                dImg.sprite = UISpriteFactory.Disc(16);
                dImg.color = RGBA(106, 212, 255, 0.14f);
                dImg.raycastTarget = false;
            }

        // 은은한 하이라이트 셀 몇 개 (고정 시드 = 재생성해도 동일 배치)
        var rng = new System.Random(20260710);
        for (int c = 0; c < 9; c++)
        {
            int cx = rng.Next(-10, 10);
            int cy = rng.Next(-6, 5);
            var cell = new GameObject($"Cell{c}", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(gridRoot.transform, false);
            var cellRt = (RectTransform)cell.transform;
            cellRt.anchorMin = cellRt.anchorMax = cellRt.pivot = new Vector2(0.5f, 0.5f);
            cellRt.sizeDelta = new Vector2(step, step);
            cellRt.anchoredPosition = new Vector2(cx * step + step * 0.5f, cy * step + step * 0.5f);
            var cImg = cell.GetComponent<Image>();
            cImg.color = RGBA(106, 212, 255, 0.035f);
            cImg.raycastTarget = false;
        }
    }

    // ── 레이더 링 + 상하 비네트 (배경 깊이감) ──
    static void BuildBackdropDeco(Transform parent)
    {
        // 홀로그램 뒤 대형 레이더 링 2겹
        float[] ringSizes = { 700f, 1000f };
        float[] ringAlpha = { 0.07f, 0.045f };
        for (int i = 0; i < 2; i++)
        {
            var ring = MakeImage($"RadarRing{i}", parent, new Vector2(ringSizes[i], ringSizes[i]), new Vector2(-390, 20), new Color(0.42f, 0.83f, 1f, ringAlpha[i]));
            var rImg = ring.GetComponent<Image>();
            rImg.sprite = UISpriteFactory.Ring(400, 2.5f);
            rImg.raycastTarget = false;
        }

        // 상/하 비네트 (위아래를 어둡게 눌러 중앙 집중) - 알파 페이드 스프라이트 + 암색 틴트
        var vigTop = MakeImage("VignetteTop", parent, Vector2.zero, Vector2.zero, RGBA(2, 4, 8, 1f));
        var vtRt = vigTop.GetComponent<RectTransform>();
        vtRt.anchorMin = new Vector2(0, 1); vtRt.anchorMax = new Vector2(1, 1); vtRt.pivot = new Vector2(0.5f, 1);
        vtRt.offsetMin = new Vector2(0, -240); vtRt.offsetMax = new Vector2(0, 0);
        var vtImg = vigTop.GetComponent<Image>();
        vtImg.sprite = UISpriteFactory.VFade(160, 0);
        vtImg.type = Image.Type.Simple;
        vtImg.raycastTarget = false;

        var vigBot = MakeImage("VignetteBottom", parent, Vector2.zero, Vector2.zero, RGBA(2, 4, 8, 1f));
        var vbRt = vigBot.GetComponent<RectTransform>();
        vbRt.anchorMin = new Vector2(0, 0); vbRt.anchorMax = new Vector2(1, 0); vbRt.pivot = new Vector2(0.5f, 0);
        vbRt.offsetMin = new Vector2(0, 0); vbRt.offsetMax = new Vector2(0, 240);
        var vbImg = vigBot.GetComponent<Image>();
        vbImg.sprite = UISpriteFactory.VFade(0, 160);
        vbImg.type = Image.Type.Simple;
        vbImg.raycastTarget = false;
    }

    // ── 콘텐츠 4코너 브래킷 (sci-fi 관제 프레임 액센트) ──
    static void BuildCornerBrackets(RectTransform content, Vector2 size)
    {
        float hw = size.x * 0.5f;
        float hh = size.y * 0.5f;
        Vector2[] corners = { new Vector2(-hw, hh), new Vector2(hw, hh), new Vector2(-hw, -hh), new Vector2(hw, -hh) };
        for (int i = 0; i < 4; i++)
        {
            float sx = corners[i].x < 0 ? 1f : -1f;   // 안쪽 방향
            float sy = corners[i].y < 0 ? 1f : -1f;
            var hbar = MakeImage($"CornerH{i}", content, new Vector2(36, 3), corners[i] + new Vector2(sx * 18f, 0f), RGBA(106, 212, 255, 0.55f));
            hbar.GetComponent<Image>().raycastTarget = false;
            var vbar = MakeImage($"CornerV{i}", content, new Vector2(3, 36), corners[i] + new Vector2(0f, sy * 18f), RGBA(106, 212, 255, 0.55f));
            vbar.GetComponent<Image>().raycastTarget = false;
        }
    }

    // ── 스프라이트 로드/적용 (CoreUpgradeUIBuilder 계열) ──
    // 행패널 PNG(512x96) 9-slice 보더: 좌우 28 / 상하 20 (스탯 행 높이 48에서도 코너 안 뭉개짐)
    static readonly Vector4 RowPanelBorder = new Vector4(28, 20, 28, 20);

    // quiet=true: 없어도 경고 없이 null (배경 PNG처럼 '있으면 쓰는' 선택 에셋용)
    static Sprite LoadSprAt(string path, int border = 0, bool quiet = false)
        => LoadSprAt(path, new Vector4(border, border, border, border), quiet);

    // border = (좌, 하, 우, 상) — TextureImporter.spriteBorder 순서
    static Sprite LoadSprAt(string path, Vector4 border, bool quiet)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
            // 어두운 그라데이션 UI는 블록압축(DXT) 밴딩이 잘 보임 - 비압축 강제 (코어 빌더와 동일)
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (imp.maxTextureSize < 2048) { imp.maxTextureSize = 2048; dirty = true; }
            if (border != Vector4.zero && imp.spriteBorder != border)
            {
                imp.spriteBorder = border;
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();
        }
        else if (!quiet)
        {
            Debug.LogWarning($"[ShipRepairUIBuilder] 스프라이트 없음: {path}");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // UISpriteFactory 스프라이트는 에셋이 아니라 씬에 저장되지 않는다(유니티 재시작 시 소실).
    // 그래서 '모양 값만' 저장해두고 실행 시 컴포넌트가 스스로 채우게 한다.
    static RuntimeGeneratedSprite Procedural(GameObject go, RuntimeGeneratedSprite.Shape shape, int texSize)
    {
        var gen = go.GetComponent<RuntimeGeneratedSprite>();
        if (gen == null) gen = go.AddComponent<RuntimeGeneratedSprite>();
        gen.shape = shape;
        gen.texSize = texSize;
        return gen;
    }

    // 호버 확대/밝아짐. 예전엔 런타임 AddComponent 였는데 인스펙터에서 안 보여 조절이 불가능했다.
    static void HoverFx(GameObject go, float scale, bool scaleOnly)
    {
        if (go == null) return;
        var fx = go.GetComponent<ShipUIHoverFx>();
        if (fx == null) fx = go.AddComponent<ShipUIHoverFx>();
        fx.hoverScale = scale;
        fx.scaleOnly = scaleOnly;
    }

    static void SetSpr(GameObject go, Sprite spr, Image.Type type)
    {
        if (go == null || spr == null) return;
        var img = go.GetComponent<Image>();
        if (img == null) return;
        img.sprite = spr;
        img.type   = type;
        img.color  = Color.white;
        if (type == Image.Type.Simple) img.preserveAspect = true;
    }

    // ── 앵커 헬퍼 ──
    // 좌상단 기준 top-anchored 고정폭 요소 (x 왼쪽부터, y 위에서부터 음수).
    static void AnchorTop(RectTransform rt, float left, float bottomY, float topY)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(left, topY);
        rt.sizeDelta = new Vector2(400, topY - bottomY);
    }

    // top-anchored 가로 스트레치 (left/right 여백, y 범위).
    static void AnchorTopStretch(RectTransform rt, float left, float right, float bottomY, float topY)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(left, bottomY);
        rt.offsetMax = new Vector2(-right, topY);
    }

    static void AnchorBottomStretch(RectTransform rt, float left, float right, float bottomY, float topY)
    {
        rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(left, bottomY);
        rt.offsetMax = new Vector2(-right, topY);
    }

    // ── 공통 헬퍼 (BaseUpgradeUIBuilder 계열) ──
    static void SetRefArray(SerializedObject so, string field, Object[] objs)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[ShipRepairUIBuilder] 필드 없음: '{field}'"); return; }
        p.arraySize = objs.Length;
        for (int i = 0; i < objs.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = objs[i];
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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

    static TextMeshProUGUI MakeTMP(string name, Transform parent, Vector2 size, Vector2 pos,
        string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 size, Vector2 pos,
        string label, float fontSize, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.sprite = UISpriteFactory.RoundedRect(48, 12);
        img.type = Image.Type.Sliced;
        go.GetComponent<Button>().targetGraphic = img;

        var t = MakeTMP("Label", go.transform, Vector2.zero, Vector2.zero, label, fontSize, Hex("EAF3FB"), TextAlignmentOptions.Center);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        return go;
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = obj;
        else Debug.LogWarning($"[ShipRepairUIBuilder] 필드 없음: '{field}' — ShipRepairUI.cs 확인");
    }

    static Color Hex(string hex, int alpha = 255)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) { c.a = alpha / 255f; return c; }
        return Color.white;
    }

    static Color RGBA(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
}
