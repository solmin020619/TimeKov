// =====================================================================
// ShipRepairUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/우주선 수리 UI 생성 -> Canvas 안에 ShipRepairPanel 생성 + ref 연결.
// 코어 UI 방식(불투명 풀스크린 + 중앙 1560x900 콘텐츠) + 격납고 관제 무드:
//   배경 = 도면 그리드(별밭 아님) / 좌 = sci-fi 프레임 + 복원도 링(눈금/후광) / 우 = 스탯/부품/버튼.
// 코어 에셋(Btn_Close_Sci, Panel_Frame_Sci)과 인벤 에셋(pill/panel_ash/panel_steel) 재사용 = 톤 통일.
// 레벨 pip / 부품 행은 런타임에 ShipRepairUI 가 채운다(레벨 수 가변).
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
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.", "확인");
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

        // 어두운 격납고 배경(우주 아님) + 뒤 클릭 차단.
        // 디자인 배경 PNG(Resources/ShipRepair/bg_hangar_blueprint.png)가 있으면 그걸 쓰고,
        // 없으면 절차 그라데이션 + 도면 그리드로 폴백.
        var bg = MakeImage("Backdrop", rootRt, Vector2.zero, Vector2.zero, Color.white);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.GetComponent<Image>();
        bgImg.raycastTarget = true;
        var bgSpr = LoadSprAt("Assets/Resources/ShipRepair/bg_hangar_blueprint.png", 0, quiet: true);
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
        bgGlow.GetComponent<Image>().sprite = UISpriteFactory.Disc(256);
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

        // ── 좌측 홀로그램 (복원도 링) ──
        var hero = MakeImage("HoloStage", crt, Vector2.zero, Vector2.zero, Color.white);
        var heroRt = hero.GetComponent<RectTransform>();
        heroRt.anchorMin = new Vector2(0, 0); heroRt.anchorMax = new Vector2(0.5f, 1);
        heroRt.offsetMin = new Vector2(36, 64); heroRt.offsetMax = new Vector2(-16, -170);
        // sci-fi 프레임 PNG (코어 효과패널과 동일 에셋 = 톤 통일, 테두리/코너 포함)
        SetSpr(hero, LoadSprAt("Assets/Resources/CoreUI/sprites/Panel_Frame_Sci.png", 44), Image.Type.Sliced);
        hero.GetComponent<Image>().raycastTarget = false;

        // 링 뒤 은은한 시안 후광
        var holoGlow = MakeImage("HoloGlow", heroRt, new Vector2(560, 560), new Vector2(0, 12), new Color(0.42f, 0.83f, 1f, 0.07f));
        holoGlow.GetComponent<Image>().sprite = UISpriteFactory.Disc(256);
        holoGlow.GetComponent<Image>().raycastTarget = false;

        // 우주선 아트 슬롯(나중에 스프라이트 연결) — 지금은 비활성 placeholder
        var shipSlot = MakeImage("ShipHologramSlot", heroRt, new Vector2(420, 280), new Vector2(0, 12), new Color(1, 1, 1, 1));
        shipSlot.GetComponent<Image>().raycastTarget = false;
        shipSlot.GetComponent<Image>().enabled = false;   // 스프라이트 넣으면 켜기

        // 복원도 링 (트랙 + 게이지)
        var track = MakeImage("RingTrack", heroRt, new Vector2(440, 440), new Vector2(0, 12), RGBA(70, 96, 128, 0.35f));
        var trackImg = track.GetComponent<Image>();
        trackImg.sprite = UISpriteFactory.Ring(400, 13f);
        trackImg.raycastTarget = false;

        var gauge = MakeImage("RingGauge", heroRt, new Vector2(440, 440), new Vector2(0, 12), RGBA(106, 212, 255, 1f));
        var gaugeImg = gauge.GetComponent<Image>();
        gaugeImg.sprite = UISpriteFactory.Ring(400, 13f);
        gaugeImg.raycastTarget = false;
        gaugeImg.type = Image.Type.Filled;
        gaugeImg.fillMethod = Image.FillMethod.Radial360;
        gaugeImg.fillOrigin = (int)Image.Origin360.Top;
        gaugeImg.fillClockwise = true;
        gaugeImg.fillAmount = 0f;
        SetRef(so, "ringGauge", gaugeImg);

        // 링 둘레 눈금 (관제 계기판 느낌 - 18도 간격, 90도마다 굵게)
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

        // 스캔라인 (위아래로 흐르는 홀로그램 주사선 - 런타임 ShipRepairUI 가 구동)
        var scan = MakeImage("ScanLine", heroRt, Vector2.zero, Vector2.zero, Color.white);
        var scanRt = scan.GetComponent<RectTransform>();
        scanRt.anchorMin = new Vector2(0, 0.5f); scanRt.anchorMax = new Vector2(1, 0.5f); scanRt.pivot = new Vector2(0.5f, 0.5f);
        scanRt.offsetMin = new Vector2(26, -24); scanRt.offsetMax = new Vector2(-26, 24);
        var scanImg = scan.GetComponent<Image>();
        scanImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(106, 212, 255, 0), new Color32(106, 212, 255, 50), 64, 0);
        scanImg.type = Image.Type.Simple;   // 세로 그라데이션 유지
        scanImg.raycastTarget = false;
        var scanEdge = MakeImage("Edge", scanRt, Vector2.zero, Vector2.zero, RGBA(106, 212, 255, 0.45f));
        var seRt = scanEdge.GetComponent<RectTransform>();
        seRt.anchorMin = new Vector2(0, 0); seRt.anchorMax = new Vector2(1, 0); seRt.pivot = new Vector2(0.5f, 0);
        seRt.offsetMin = new Vector2(0, 0); seRt.offsetMax = new Vector2(0, 2);
        scanEdge.GetComponent<Image>().raycastTarget = false;
        SetRef(so, "scanLine", scanRt);

        var capTop = MakeTMP("HoloCaption", heroRt, new Vector2(260, 28), new Vector2(0, 96), "선체 복원도", 16, RGBA(150, 178, 204, 1f), TextAlignmentOptions.Center);
        capTop.characterSpacing = 4f;

        var pct = MakeTMP("RestorePercent", heroRt, new Vector2(320, 88), new Vector2(0, 8), "0%", 58, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Center);
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
        nhImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(28, 40, 56, 235), new Color32(12, 18, 28, 235), 64, 18);
        nhImg.type = Image.Type.Sliced;
        nhImg.raycastTarget = false;
        AnchorTopStretch((RectTransform)nhBg.transform, 0, 400, -42, -2);

        var nextHeader = MakeTMP("NextHeader", colRt, Vector2.zero, Vector2.zero, "다음 수리   Lv.1 -> Lv.2", 17, Hex("AEE3FF"), TextAlignmentOptions.Left);
        nextHeader.fontStyle = FontStyles.Bold;
        AnchorTopStretch(nextHeader.rectTransform, 20, 4, -38, -6);
        SetRef(so, "nextHeaderText", nextHeader);

        // 스탯 3행
        var statVals = new Object[3];
        statVals[0] = MakeStatRow(colRt, -58, "건축 범위");
        statVals[1] = MakeStatRow(colRt, -120, "설비 연료");
        statVals[2] = MakeStatRow(colRt, -182, "공장 가동속도");
        SetRefArray(so, "statValueTexts", statVals);

        var sdiv = MakeImage("StatDivider", colRt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.4f));
        AnchorTopStretch(sdiv.GetComponent<RectTransform>(), 4, 4, -244, -243);
        sdiv.GetComponent<Image>().raycastTarget = false;

        var partsLabel = MakeTMP("PartsLabel", colRt, Vector2.zero, Vector2.zero, "수리 부품", 17, Hex("EAF3FB"), TextAlignmentOptions.Left);
        partsLabel.fontStyle = FontStyles.Bold;
        AnchorTopStretch(partsLabel.rectTransform, 4, 120, -274, -250);

        var partsCount = MakeTMP("PartsCount", colRt, Vector2.zero, Vector2.zero, "회수  0 / 4", 14, RGBA(150, 178, 204, 1f), TextAlignmentOptions.Right);
        AnchorTopStretch(partsCount.rectTransform, 120, 4, -274, -250);
        SetRef(so, "partsCountText", partsCount);

        var partsGo = new GameObject("PartsContent", typeof(RectTransform));
        partsGo.transform.SetParent(colRt, false);
        var partsRt = partsGo.GetComponent<RectTransform>();
        partsRt.anchorMin = new Vector2(0, 0); partsRt.anchorMax = new Vector2(1, 1);
        partsRt.offsetMin = new Vector2(0, 104); partsRt.offsetMax = new Vector2(0, -284);
        var pvlg = partsGo.AddComponent<VerticalLayoutGroup>();
        pvlg.spacing = 10f; pvlg.childAlignment = TextAnchor.UpperCenter;
        pvlg.childControlWidth = true; pvlg.childControlHeight = true;
        pvlg.childForceExpandWidth = true; pvlg.childForceExpandHeight = false;
        SetRef(so, "partsContent", partsRt);

        // 부품 목록과 버튼 사이 안내줄 (빈 공간 메움)
        var partsHint = MakeTMP("PartsHint", colRt, Vector2.zero, Vector2.zero,
            "부품은 맵 곳곳의 이전 탐사대 시설에서 회수합니다", 13, RGBA(110, 125, 141, 0.9f), TextAlignmentOptions.Center);
        var phRt = partsHint.rectTransform;
        phRt.anchorMin = new Vector2(0, 0); phRt.anchorMax = new Vector2(1, 0); phRt.pivot = new Vector2(0.5f, 0);
        phRt.offsetMin = new Vector2(0, 76); phRt.offsetMax = new Vector2(0, 100);

        // 수리 버튼 (흑백 그라데이션 스프라이트 + 상태색 틴트 - 런타임 BtnReady/BtnLocked 가 색을 바꿈)
        var repair = MakeButton("RepairButton", colRt, Vector2.zero, Vector2.zero, "수리 실행", 22, new Color(0.20f, 0.66f, 0.95f, 1f));
        var repImg = repair.GetComponent<Image>();
        repImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(255, 255, 255, 255), new Color32(185, 205, 222, 255), 64, 16);
        repImg.type = Image.Type.Sliced;
        repImg.color = new Color(0.20f, 0.66f, 0.95f, 1f);
        var repRt = repair.GetComponent<RectTransform>();
        repRt.anchorMin = new Vector2(0, 0); repRt.anchorMax = new Vector2(1, 0); repRt.pivot = new Vector2(0.5f, 0);
        repRt.offsetMin = new Vector2(0, 6); repRt.offsetMax = new Vector2(0, 68);
        SetRef(so, "repairButton", repair.GetComponent<Button>());
        SetRef(so, "repairButtonText", repair.GetComponentInChildren<TextMeshProUGUI>());

        // ── 푸터 ──
        var footer = MakeTMP("Footer", crt, Vector2.zero, Vector2.zero,
            "Lv.5 완료 시 - 격납 펜스 해제 / 탈출 시도 가능", 14, RGBA(130, 148, 168, 0.95f), TextAlignmentOptions.Center);
        AnchorBottomStretch(footer.rectTransform, 36, 36, 18, 42);

        so.ApplyModifiedProperties();

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
            "- 우주선 아트가 생기면 HoloStage/ShipHologramSlot 에 연결\n" +
            "- Ctrl+S 로 씬 저장", "확인");
    }

    // 스탯 1행: 애쉬 패널 PNG + 좌측 시안 액센트 바 + 라벨(좌)/값(우). 값 TMP 반환.
    static TextMeshProUGUI MakeStatRow(Transform parent, float topY, string label)
    {
        var row = new GameObject("StatRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(0, topY - 48); rt.offsetMax = new Vector2(0, topY);
        // 절차 그라데이션 (PNG 9-slice 를 가로로 늘리면 생기는 세로 이음매 없음)
        var rowImg = row.GetComponent<Image>();
        rowImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(34, 46, 64, 215), new Color32(16, 22, 32, 235), 64, 14);
        rowImg.type = Image.Type.Sliced;
        rowImg.raycastTarget = false;

        // 좌측 시안 액센트 바
        var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(rt, false);
        var art = (RectTransform)accent.transform;
        art.anchorMin = new Vector2(0, 0.5f); art.anchorMax = new Vector2(0, 0.5f); art.pivot = new Vector2(0, 0.5f);
        art.sizeDelta = new Vector2(4, 26); art.anchoredPosition = new Vector2(8, 0);
        accent.GetComponent<Image>().color = RGBA(106, 212, 255, 0.85f);
        accent.GetComponent<Image>().raycastTarget = false;

        var nm = MakeTMP("Name", rt, Vector2.zero, Vector2.zero, label, 16, Hex("CDD8E5"), TextAlignmentOptions.Left);
        var nrt = nm.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(0.5f, 1);
        nrt.offsetMin = new Vector2(22, 0); nrt.offsetMax = new Vector2(0, 0);

        var val = MakeTMP("Value", rt, Vector2.zero, Vector2.zero, "-", 16, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Right);
        var vrt = val.rectTransform;
        vrt.anchorMin = new Vector2(0.4f, 0); vrt.anchorMax = new Vector2(1, 1);
        vrt.offsetMin = new Vector2(0, 0); vrt.offsetMax = new Vector2(-16, 0);
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

        // 상/하 비네트 (위아래를 어둡게 눌러 중앙 집중)
        var vigTop = MakeImage("VignetteTop", parent, Vector2.zero, Vector2.zero, Color.white);
        var vtRt = vigTop.GetComponent<RectTransform>();
        vtRt.anchorMin = new Vector2(0, 1); vtRt.anchorMax = new Vector2(1, 1); vtRt.pivot = new Vector2(0.5f, 1);
        vtRt.offsetMin = new Vector2(0, -240); vtRt.offsetMax = new Vector2(0, 0);
        var vtImg = vigTop.GetComponent<Image>();
        vtImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(2, 4, 8, 160), new Color32(0, 0, 0, 0), 64, 0);
        vtImg.type = Image.Type.Simple;
        vtImg.raycastTarget = false;

        var vigBot = MakeImage("VignetteBottom", parent, Vector2.zero, Vector2.zero, Color.white);
        var vbRt = vigBot.GetComponent<RectTransform>();
        vbRt.anchorMin = new Vector2(0, 0); vbRt.anchorMax = new Vector2(1, 0); vbRt.pivot = new Vector2(0.5f, 0);
        vbRt.offsetMin = new Vector2(0, 0); vbRt.offsetMax = new Vector2(0, 240);
        var vbImg = vigBot.GetComponent<Image>();
        vbImg.sprite = UISpriteFactory.RoundedRectVGrad(new Color32(0, 0, 0, 0), new Color32(2, 4, 8, 160), 64, 0);
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
    // quiet=true: 없어도 경고 없이 null (배경 PNG처럼 '있으면 쓰는' 선택 에셋용)
    static Sprite LoadSprAt(string path, int border = 0, bool quiet = false)
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
            if (border > 0)
            {
                var b = new Vector4(border, border, border, border);
                if (imp.spriteBorder != b) { imp.spriteBorder = b; dirty = true; }
            }
            if (dirty) imp.SaveAndReimport();
        }
        else if (!quiet)
        {
            Debug.LogWarning($"[ShipRepairUIBuilder] 스프라이트 없음: {path}");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
