// =====================================================================
// CoreUpgradeUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/코어 강화 UI 생성 -> Canvas 안에 CoreUpgradePanel 계층 자동 생성 + 필드 연결.
// 레이아웃 = 별자리(운명의 자리) 스타일: 풀스크린 우주배경 + 중앙 코어 + 10단계 노드 원형 배치.
//   1단계(골격, 지금): 우주배경/별 + 코어 + 10노드(전부 미점등).
//   다음 단계: 2 점등/연결선, 3 효과리스트(???/미리보기), 4 미니게임 연결, 5 연출/PNG.
// 미니게임 시계 / 재료바 / 버튼 / 매니저 로직은 기존 그대로 재활용(SetRef 연결만 유지).
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CoreUpgradeUIBuilder
{
    // 별자리 배치 상수
    const int   NodeCount  = 10;
    const float NodeRadius = 290f;   // 코어 중심에서 노드까지
    const float CoreY      = 50f;    // 코어/별자리 중심 Y (panel 기준)

    [MenuItem("Tools/TIMEKOV/코어 강화 UI 생성")]
    public static void Build()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.\nCanvas를 먼저 만들어 주세요.", "확인");
            return;
        }

        // 기존 패널 전수 제거 (비활성 포함). 옛 잔재가 싱글톤(CoreUpgradeUI.Instance)을 가로채면
        // 새 패널이 파괴돼 "변경이 안 먹힌다".
        var existingUis = Object.FindObjectsByType<CoreUpgradeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existingUis.Length > 0)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                $"기존 코어 강화 패널 {existingUis.Length}개가 있습니다.\n모두 삭제하고 새로 만들까요?",
                "새로 만들기", "취소");
            if (!replace) return;
            foreach (var u in existingUis)
                if (u != null) Object.DestroyImmediate(u.gameObject);
        }

        // 루트 = 컴포넌트 호스트. 항상 active(Awake가 돌아 Instance 등록). 보이는 건 PanelRoot 자식만 토글.
        GameObject root = new GameObject("CoreUpgradePanel", typeof(RectTransform));
        var rootTr = root.GetComponent<RectTransform>();
        rootTr.SetParent(canvas.transform, false);
        Stretch(rootTr);

        CoreUpgradeUI ui = root.AddComponent<CoreUpgradeUI>();
        SerializedObject so = new SerializedObject(ui);

        // -- PanelRoot = 풀스크린 우주 배경. 평소 off, Open()에서 on. --
        GameObject panelRootGo = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
        var panelRootTr = panelRootGo.GetComponent<RectTransform>();
        panelRootTr.SetParent(rootTr, false);
        Stretch(panelRootTr);
        panelRootGo.GetComponent<Image>().color = Hex("04060D", 252);   // 깊은 우주(거의 불투명, 뒤 클릭 차단)
        SetRef(so, "panelRoot", panelRootGo);
        panelRootGo.SetActive(false);

        // 배경 그라데이션 (위 네이비 -> 아래 거의 검정)
        var grad = MakeImage("SpaceGradient", panelRootTr, new Vector2(64, 64), Vector2.zero, Color.white);
        Stretch(grad.GetComponent<RectTransform>());
        var gradImg = grad.GetComponent<Image>();
        gradImg.sprite       = UISpriteFactory.RoundedRectVGrad(Hex("0A1730"), Hex("03060E"), 64, 0);
        gradImg.type         = Image.Type.Simple;
        gradImg.raycastTarget = false;

        // 별 (고정 시드 -> 재생성해도 동일 배치)
        BuildStars(panelRootTr);

        // -- panel = 1600x900 중앙 컨테이너 (투명). 코어/노드/UI 올림. --
        GameObject panel = MakeImage("Panel", panelRootTr, new Vector2(1600, 900), Vector2.zero, Hex("1A202A", 0));
        panel.GetComponent<Image>().raycastTarget = false;

        // 코어 글로우 (코어 뒤 은은한 빛)
        var glow = MakeImage("CoreGlow", panel.transform, new Vector2(560, 560), new Vector2(0, CoreY), Color.white);
        SetSpr(glow, LoadSpr("Core_Glow"), Image.Type.Simple);

        // 데코 링
        var dring = MakeImage("DecoRing", panel.transform, new Vector2(360, 360), new Vector2(0, CoreY), Color.white);
        SetSpr(dring, LoadSpr("Deco_Ring"), Image.Type.Simple);

        // 코어 본체 (중앙)
        GameObject coreGo = MakeImage("CoreImage", panel.transform, new Vector2(230, 230), new Vector2(0, CoreY), Color.white);
        SetSpr(coreGo, LoadSpr("core"), Image.Type.Simple);
        coreGo.GetComponent<Image>().preserveAspect = true;
        SetRef(so, "coreImage", coreGo.GetComponent<Image>());

        // -- 10단계 노드 원형 배치 (12시부터 시계방향). 1단계는 전부 미점등. --
        var nodeImgs = new Object[NodeCount];
        for (int i = 0; i < NodeCount; i++)
        {
            float angDeg = 90f - i * (360f / NodeCount);   // 12시(90도) 시작, 시계방향
            float rad    = angDeg * Mathf.Deg2Rad;
            Vector2 pos  = new Vector2(Mathf.Cos(rad) * NodeRadius, CoreY + Mathf.Sin(rad) * NodeRadius);
            nodeImgs[i]  = MakeNode(panel.transform, pos, i + 1);
        }
        SetRefArray(so, "constellationNodes", nodeImgs);

        // -- 타이틀 / 레벨 뱃지 / 닫기 --
        var title = MakeTMP("TitleText", panel.transform, new Vector2(500, 44), new Vector2(0, 420),
            "코어 <color=#5FC4FF>강화</color>", 30, Hex("EAF3FB"), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;

        GameObject badge = MakeImage("LevelBadge", panel.transform, new Vector2(140, 60), new Vector2(640, 416), Hex("0C1322", 230));
        SetSpr(badge, LoadSprAt("Assets/11.UI/New/pill_dark.png", 24), Image.Type.Sliced);
        var levelTxt = MakeTMP("LevelText", badge.transform, Vector2.zero, Vector2.zero, "Lv.0 / 10", 22, Hex("EAF3FB"), TextAlignmentOptions.Center, stretch: true);
        levelTxt.fontStyle = FontStyles.Bold;
        SetRef(so, "levelText", levelTxt);

        GameObject closeBtn = MakeButton("CloseButton", panel.transform, new Vector2(48, 48), new Vector2(-724, 416), "X", 22, Hex("0E1626", 215));
        SetSpr(closeBtn, LoadSprAt("Assets/11.UI/New/pill_dark.png", 16), Image.Type.Sliced);
        closeBtn.GetComponent<Image>().color = Hex("0E1626", 215);
        SetRef(so, "closeButton", closeBtn.GetComponent<Button>());

        // -- 효과 리스트 패널 (우측) = 코어 단계별 효과 + 다음 강화 미리보기(???/+N) --
        GameObject fxPanel = MakeImage("EffectPanel", panel.transform, new Vector2(388, 348), new Vector2(566, CoreY - 6f), Hex("0C1322", 205));
        SetSpr(fxPanel, LoadSprAt("Assets/11.UI/New/panel_ash_a78.png", 32), Image.Type.Sliced);
        MakeTMP("EffectTitle", fxPanel.transform, new Vector2(340, 30), new Vector2(0, 142), "코어 효과", 18, Hex("AEE3FF"), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        MakeImage("EffectDivider", fxPanel.transform, new Vector2(332, 2), new Vector2(0, 120), Hex("2A3C5A", 200));
        var fxRows = new Object[4];
        string[] fxSeed = { "최대 시간", "부활 체력", "우클릭 대쉬", "흡수율" };
        for (int i = 0; i < 4; i++)
        {
            var fxRow = MakeTMP($"EffectRow{i}", fxPanel.transform, new Vector2(330, 38), new Vector2(10, 84 - i * 62), fxSeed[i], 17, Hex("C7D6E6"), TextAlignmentOptions.Left);
            fxRows[i] = fxRow;
        }
        SetRefArray(so, "effectRows", fxRows);

        // -- 강화 정보 그룹 (MAX면 숨김) = 재료/확률 바 + 강화 버튼 --
        GameObject infoGroup = MakeEmpty("UpgradeInfoGroup", panel.transform);
        SetRef(so, "upgradeInfoGroup", infoGroup);

        GameObject bottomBar = MakeImage("BottomBar", infoGroup.transform, new Vector2(660, 104), new Vector2(0, -396), Hex("0C1322", 200));
        SetSpr(bottomBar, LoadSprAt("Assets/11.UI/New/panel_ash_a78.png", 32), Image.Type.Sliced);
        AddHighlight(bottomBar, "core_upgrade_materials");   // 튜토 코치마크 대상 (재료/성공확률)
        GameObject kitIconGo = MakeImage("KitIcon", bottomBar.transform, new Vector2(48, 48), new Vector2(-296, 22), Hex("1A202C", 220));
        SetSpr(kitIconGo, LoadSpr("KitSlot"), Image.Type.Simple);
        SetRef(so, "kitIconImage", kitIconGo.GetComponent<Image>());
        var kitItemIcon = MakeImage("KitItemIcon", kitIconGo.transform, new Vector2(34, 34), Vector2.zero, Color.white);
        SetSpr(kitItemIcon, LoadSpr("KitIcon"), Image.Type.Simple);
        var kitName  = MakeTMP("KitNameText",     bottomBar.transform, new Vector2(280, 24), new Vector2(-108, 28), "필요: 코어 키트", 16, Hex("EAF3FB"), TextAlignmentOptions.Left);
        var kitCount = MakeTMP("KitCountText",    bottomBar.transform, new Vector2(200, 22), new Vector2(-118, 2),  "보유:  0 / 3",   15, Hex("AEBFD0"), TextAlignmentOptions.Left);
        var kitShort = MakeTMP("KitShortageText", bottomBar.transform, new Vector2(130, 22), new Vector2(88, 2),    "",              14, Hex("FF6068"), TextAlignmentOptions.Left);
        var succRate = MakeTMP("SuccessRateText", bottomBar.transform, new Vector2(240, 26), new Vector2(170, 28),  "성공 확률:  50%", 18, Hex("AEE3FF"), TextAlignmentOptions.Right);
        succRate.fontStyle = FontStyles.Bold;
        SetRef(so, "kitNameText",     kitName);
        SetRef(so, "kitCountText",    kitCount);
        SetRef(so, "kitShortageText", kitShort);
        SetRef(so, "successRateText", succRate);

        // 성공 확률 게이지 (바닥)
        GameObject gaugeBg = MakeImage("GaugeBG", bottomBar.transform, new Vector2(610, 10), new Vector2(0, -34), Hex("05080D", 255));
        SetSpr(gaugeBg, LoadSpr("Gauge_Trough", 6), Image.Type.Sliced);
        GameObject gaugeFillGo = MakeImage("GaugeFill", gaugeBg.transform, new Vector2(610, 10), Vector2.zero, Hex("6FD2FF", 255));
        SetSpr(gaugeFillGo, LoadSpr("Gauge_Fill", 6), Image.Type.Filled);
        var gaugeFillImg = gaugeFillGo.GetComponent<Image>();
        gaugeFillImg.type       = Image.Type.Filled;
        gaugeFillImg.fillMethod = Image.FillMethod.Horizontal;
        gaugeFillImg.fillOrigin = 0; // Left
        gaugeFillImg.fillAmount = 0.5f;
        gaugeFillImg.color      = Hex("6FD2FF");   // 채움은 시안
        SetRef(so, "gaugeFill", gaugeFillImg);

        // 강화 시작 버튼
        GameObject upgradeBtn = MakeButton("UpgradeButton", infoGroup.transform, new Vector2(360, 60), new Vector2(0, -486), "강화", 20, Hex("2A4A66"));
        SetSpr(upgradeBtn, LoadSprAt("Assets/11.UI/New/panel_steel_a78.png", 24), Image.Type.Sliced);
        upgradeBtn.GetComponent<Image>().color = Hex("2A4A66");
        SetRef(so, "upgradeButton",     upgradeBtn.GetComponent<Button>());
        SetRef(so, "upgradeButtonText", upgradeBtn.GetComponentInChildren<TextMeshProUGUI>());
        AddHighlight(upgradeBtn, "core_upgrade_button");   // 튜토 코치마크 대상 (강화 버튼)

        // -- MAX 그룹 --
        GameObject maxGroup = MakeEmpty("MaxLevelGroup", panel.transform);
        MakeTMP("MaxText", maxGroup.transform, new Vector2(500, 50), new Vector2(0, -396), "최대 단계 달성", 24, Hex("FFD66B"), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        maxGroup.SetActive(false);
        SetRef(so, "maxLevelGroup", maxGroup);

        // -- 피드백 텍스트 --
        var feedback = MakeTMP("FeedbackText", panel.transform, new Vector2(600, 40), new Vector2(0, -170), "", 22, Hex("EAF3FB"), TextAlignmentOptions.Center);
        feedback.fontStyle = FontStyles.Bold;
        feedback.gameObject.SetActive(false);
        SetRef(so, "feedbackText", feedback);

        // -- 인라인 시계 (강화 시 코어->시계 전환). 미니게임 재활용. --
        BuildInlineClock(panel, so);

        // -- 적용 + 저장 --
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[TIMEKOV] 코어 강화 UI(별자리 1단계 골격) 생성 완료.");
        EditorUtility.DisplayDialog("완료",
            "코어 강화 UI(별자리 골격) 생성 완료!\n\n- CoreUpgradePanel은 active(켜진 상태)로 둘 것\n- Ctrl+S로 씬 저장", "확인");
    }

    // -- 인라인 시계 (코어 자리에서 코어<->시계 크로스페이드, 미니게임용) --
    static void BuildInlineClock(GameObject panel, SerializedObject so)
    {
        var clockGo = MakeEmpty("ClockGroup", panel.transform);
        var clockRt = clockGo.GetComponent<RectTransform>();
        clockRt.anchorMin = clockRt.anchorMax = clockRt.pivot = new Vector2(0.5f, 0.5f);
        clockRt.sizeDelta        = new Vector2(300, 300);
        clockRt.anchoredPosition = new Vector2(0, CoreY);
        var clockCg = clockGo.AddComponent<CanvasGroup>();
        clockCg.alpha = 0f;
        SetRef(so, "clockGroup", clockCg);

        var clockFace = MakeImage("ClockFace", clockGo.transform, new Vector2(300, 300), Vector2.zero, Color.white);
        SetSpr(clockFace, LoadSpr("Clock_Face"), Image.Type.Simple);

        // 성공존/퍼펙트존: 흰 링 스프라이트를 틴트 + Radial360 채움 (상단 중앙 기준)
        var succZone = MakeImage("SuccessZone", clockGo.transform, new Vector2(300, 300), Vector2.zero, Color.white);
        SetSpr(succZone, LoadSpr("Clock_RingWhite"), Image.Type.Filled);
        var succImg = succZone.GetComponent<Image>();
        succImg.color         = Hex("46E08A", 235);
        succImg.fillMethod    = Image.FillMethod.Radial360;
        succImg.fillOrigin    = (int)Image.Origin360.Top;
        succImg.fillClockwise = true;
        succImg.fillAmount    = 52f / 360f;
        SetRef(so, "successZoneImage", succImg);

        var perfZone = MakeImage("PerfectZone", clockGo.transform, new Vector2(300, 300), Vector2.zero, Color.white);
        SetSpr(perfZone, LoadSpr("Clock_RingWhite"), Image.Type.Filled);
        var perfImg = perfZone.GetComponent<Image>();
        perfImg.color         = Hex("FFD66B", 245);
        perfImg.fillMethod    = Image.FillMethod.Radial360;
        perfImg.fillOrigin    = (int)Image.Origin360.Top;
        perfImg.fillClockwise = true;
        perfImg.fillAmount    = 18f / 360f;
        SetRef(so, "perfectZoneImage", perfImg);

        // 바늘 (회전축을 하단쪽 = 시계 중앙으로)
        var needleGo = MakeImage("ClockNeedle", clockGo.transform, new Vector2(150, 150), Vector2.zero, Color.white);
        SetSpr(needleGo, LoadSpr("Clock_Needle"), Image.Type.Simple);
        var needleRt = needleGo.GetComponent<RectTransform>();
        needleRt.pivot            = new Vector2(0.5f, 0.18f);
        needleRt.anchoredPosition = Vector2.zero;
        SetRef(so, "clockNeedle", needleRt);

        // 중앙 허브
        var hubGo = MakeImage("ClockHub", clockGo.transform, new Vector2(46, 46), Vector2.zero, Color.white);
        SetSpr(hubGo, LoadSpr("Clock_Hub"), Image.Type.Simple);

        // 스핀 안내 (코어 아래) + 판정 칩 (코어 위)
        var spinHintTmp = MakeTMP("SpinHint", panel.transform, new Vector2(540, 36), new Vector2(0, CoreY - 170f),
            "멈춰서 <color=#46E08A>성공존</color>에 맞추세요!", 18, Hex("AEE3FF"), TextAlignmentOptions.Center);
        spinHintTmp.fontStyle = FontStyles.Bold;
        spinHintTmp.gameObject.SetActive(false);
        SetRef(so, "spinHint", spinHintTmp.gameObject);

        var judgeChip = MakeTMP("JudgeChip", panel.transform, new Vector2(360, 40), new Vector2(0, CoreY + 155f),
            "", 26, Hex("FFD66B"), TextAlignmentOptions.Center);
        judgeChip.fontStyle = FontStyles.Bold;
        judgeChip.gameObject.SetActive(false);
        SetRef(so, "judgeChipText", judgeChip);
    }

    // ── 타임 캐치 단독 추가 ───────────────────────────────────────────
    [MenuItem("Tools/TIMEKOV/코어 강화 타임캐치 UI 추가")]
    public static void AddTimeCatch()
    {
        var panelGo = Selection.activeGameObject;
        if (panelGo == null || panelGo.name != "CoreUpgradePanel")
        {
            EditorUtility.DisplayDialog("오류",
                "하이어라키에서 CoreUpgradePanel을 선택한 뒤 실행하세요.", "확인");
            return;
        }
        if (panelGo.transform.Find("TimeCatchHost") != null)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                "TimeCatchHost가 이미 존재합니다. 교체할까요?", "교체", "취소");
            if (!replace) return;
            Object.DestroyImmediate(panelGo.transform.Find("TimeCatchHost").gameObject);
        }
        var uiComp = panelGo.GetComponent<CoreUpgradeUI>();
        if (uiComp == null)
        {
            EditorUtility.DisplayDialog("오류",
                "CoreUpgradeUI 컴포넌트가 없습니다.", "확인");
            return;
        }
        var so = new SerializedObject(uiComp);
        BuildTimeCatch(panelGo, so);
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("완료",
            "TimeCatch UI가 추가됐습니다!\nCtrl+S로 씬을 저장하세요.", "확인");
    }

    // ── 타임 캐치 UI 빌드 ─────────────────────────────────────────────
    static void BuildTimeCatch(GameObject root, SerializedObject coreSO)
    {
        // ── TimeCatchHost ─────────────────────────────────
        var host = new GameObject("TimeCatchHost", typeof(RectTransform));
        host.transform.SetParent(root.transform, false);
        var hostRt = host.GetComponent<RectTransform>();
        hostRt.anchorMin = hostRt.anchorMax = hostRt.pivot = new Vector2(0.5f, 0.5f);
        hostRt.sizeDelta        = Vector2.zero;
        hostRt.anchoredPosition = Vector2.zero;

        var tc  = host.AddComponent<TimeCatchUI>();
        var tso = new SerializedObject(tc);

        // ── 팝업 패널 (처음엔 숨김) ──────────────────────
        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(host.transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        dimGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var popup = new GameObject("TimeCatchPanel", typeof(RectTransform), typeof(Image));
        popup.transform.SetParent(host.transform, false);
        var popupRt = popup.GetComponent<RectTransform>();
        popupRt.anchorMin = popupRt.anchorMax = popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.sizeDelta        = new Vector2(380, 500);
        popupRt.anchoredPosition = Vector2.zero;
        popup.GetComponent<Image>().color = Hex("07111E", 255);
        popup.SetActive(false);
        SetRef(tso, "timeCatchPanel", popup);

        var border = MakeImage("Border", popup.transform,
            new Vector2(380, 500), Vector2.zero, Hex("1A4060", 180));
        border.transform.SetAsFirstSibling();

        MakeTMP("Title", popup.transform,
            new Vector2(340, 45), new Vector2(0f, 210f),
            "TIME  CATCH", 26f, Hex("7DD4FC"),
            TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        MakeImage("TitleLine", popup.transform,
            new Vector2(320, 1), new Vector2(0f, 185f), Hex("1A4060", 200));

        var clock = new GameObject("ClockArea", typeof(RectTransform));
        clock.transform.SetParent(popup.transform, false);
        var clockRt = clock.GetComponent<RectTransform>();
        clockRt.anchorMin = clockRt.anchorMax = clockRt.pivot = new Vector2(0.5f, 0.5f);
        clockRt.sizeDelta        = new Vector2(260, 260);
        clockRt.anchoredPosition = new Vector2(0f, 40f);

        MakeImage("ClockFace", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("030A12", 255));

        var outerRingGo = MakeImage("OuterRing", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("7DD4FC", 90));
        var outerRingImg = outerRingGo.GetComponent<Image>();
        outerRingImg.type       = Image.Type.Filled;
        outerRingImg.fillMethod = Image.FillMethod.Radial360;
        outerRingImg.fillAmount = 1f;
        SetRef(tso, "trackRingImage", outerRingImg);

        var zoneGo  = MakeImage("SuccessZone", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("00E676", 220));
        var zoneImg = zoneGo.GetComponent<Image>();
        zoneImg.type       = Image.Type.Filled;
        zoneImg.fillMethod = Image.FillMethod.Radial360;
        zoneImg.fillOrigin = 2;           // Top
        zoneImg.fillAmount = 60f / 360f;
        zoneGo.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        SetRef(tso, "successZoneImage", zoneImg);

        MakeImage("InnerMask", clock.transform,
            new Vector2(210, 210), Vector2.zero, Hex("030A12", 255));

        for (int i = 0; i < 12; i++)
        {
            float  deg  = i * 30f;
            float  rad  = deg * Mathf.Deg2Rad;
            float  r    = 112f;
            bool   major = (i % 3 == 0);
            float  tw = major ? 3f : 2f;
            float  th = major ? 14f : 8f;
            Color  tc2 = major ? Hex("7DD4FC", 230) : Hex("FFFFFF", 120);

            var tick = MakeImage($"Tick{i}", clock.transform,
                new Vector2(tw, th),
                new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r),
                tc2);
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, -deg);
        }

        MakeImage("TopMarker", clock.transform,
            new Vector2(5f, 18f), new Vector2(0f, 108f), Hex("00E676", 255));

        var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        needleGo.transform.SetParent(clock.transform, false);
        var needleRt = needleGo.GetComponent<RectTransform>();
        needleRt.anchorMin = needleRt.anchorMax = new Vector2(0.5f, 0.5f);
        needleRt.pivot     = new Vector2(0.5f, 0f);
        needleRt.sizeDelta        = new Vector2(3f, 90f);
        needleRt.anchoredPosition = Vector2.zero;
        needleGo.GetComponent<Image>().color = Color.white;
        SetRef(tso, "needle", needleRt);

        MakeImage("CenterPin", clock.transform,
            new Vector2(10f, 10f), Vector2.zero, Hex("7DD4FC", 255));

        MakeImage("RateLine", popup.transform,
            new Vector2(320, 1), new Vector2(0f, -90f), Hex("1A4060", 200));

        var curRate = MakeTMP("CurrentRateText", popup.transform,
            new Vector2(340, 32), new Vector2(0f, -115f),
            "성공 확률  85%", 16f, Hex("AACCEE"),
            TextAlignmentOptions.Center);
        SetRef(tso, "currentRateText", curRate);

        var bonusRate = MakeTMP("BonusRateText", popup.transform,
            new Vector2(340, 32), new Vector2(0f, -145f),
            "캐치 성공  90%  (+5%)", 16f, Hex("33CC66"),
            TextAlignmentOptions.Center);
        bonusRate.fontStyle = FontStyles.Bold;
        SetRef(tso, "bonusRateText", bonusRate);

        MakeImage("GuideLine", popup.transform,
            new Vector2(320, 1), new Vector2(0f, -175f), Hex("1A4060", 200));

        var guide = MakeTMP("GuideText", popup.transform,
            new Vector2(340, 36), new Vector2(0f, -205f),
            "SPACE", 22f, Hex("7DD4FC"),
            TextAlignmentOptions.Center);
        guide.fontStyle = FontStyles.Bold;
        SetRef(tso, "guideText", guide);

        var result = MakeTMP("ResultText", popup.transform,
            new Vector2(340, 40), new Vector2(0f, -205f),
            "", 22f, Hex("33CC66"), TextAlignmentOptions.Center);
        result.fontStyle = FontStyles.Bold;
        result.gameObject.SetActive(false);
        SetRef(tso, "resultText", result);

        tso.ApplyModifiedProperties();

        SetRef(coreSO, "timeCatch", tc);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // 별 배경 (고정 시드라 재생성해도 같은 배치)
    static void BuildStars(Transform parent)
    {
        var rng = new System.Random(20260624);
        var starsRoot = new GameObject("Stars", typeof(RectTransform));
        starsRoot.transform.SetParent(parent, false);
        Stretch(starsRoot.GetComponent<RectTransform>());

        var starSpr = UISpriteFactory.Disc(16);
        for (int i = 0; i < 90; i++)
        {
            float x = (float)(rng.NextDouble() * 1920.0 - 960.0);
            float y = (float)(rng.NextDouble() * 1080.0 - 540.0);
            float s = 1.5f + (float)rng.NextDouble() * 2.5f;
            float a = 0.15f + (float)rng.NextDouble() * 0.55f;
            var star = MakeImage($"Star{i}", starsRoot.transform, new Vector2(s, s), new Vector2(x, y), new Color(1f, 1f, 1f, a));
            var img = star.GetComponent<Image>();
            img.sprite        = starSpr;
            img.raycastTarget = false;
        }
    }

    // 별자리 노드 1개 (미점등). 점등 대상인 안쪽 disc Image를 반환.
    static Image MakeNode(Transform parent, Vector2 pos, int number)
    {
        var node = MakeImage($"Node{number}", parent, new Vector2(58, 58), pos, Color.white);
        var img  = node.GetComponent<Image>();
        img.sprite = UISpriteFactory.Disc(64);
        img.type   = Image.Type.Simple;
        img.color  = Hex("121A2C", 240);   // 미점등 채움

        var ring  = MakeImage("Ring", node.transform, new Vector2(58, 58), Vector2.zero, Color.white);
        var rimg  = ring.GetComponent<Image>();
        rimg.sprite        = UISpriteFactory.Ring(64, 3f);
        rimg.color         = Hex("2A3C5A", 255);   // 미점등 테두리
        rimg.raycastTarget = false;

        var num = MakeTMP("Num", node.transform, new Vector2(58, 58), Vector2.zero, number.ToString(), 20, Hex("46597C"), TextAlignmentOptions.Center);
        num.raycastTarget = false;
        return img;
    }

    static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent,
        Vector2 size, Vector2 pos, string text, float fontSize,
        Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
        bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();

        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent,
        Vector2 size, Vector2 pos, string label, float fontSize, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = bgColor;
        go.GetComponent<Button>().targetGraphic = go.GetComponent<Image>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return go;
    }

    static GameObject MakeEmpty(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static void SetRef(SerializedObject so, string fieldName, Object obj)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = obj;
        else
            Debug.LogWarning($"[UIBuilder] SerializedProperty 없음: '{fieldName}' -- CoreUpgradeUI.cs 필드명 확인 필요");
    }

    static void SetRefArray(SerializedObject so, string fieldName, Object[] objs)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[UIBuilder] SerializedProperty 없음: '{fieldName}' -- CoreUpgradeUI.cs 필드명 확인 필요");
            return;
        }
        prop.arraySize = objs.Length;
        for (int i = 0; i < objs.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = objs[i];
    }

    static Color Hex(string hex, int alpha = 255)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
        {
            c.a = alpha / 255f;
            return c;
        }
        return Color.white;
    }

    // ── 스프라이트 로드 (Sprite 임포트 타입 + 9-slice border 자동 설정 후 로드) ──
    static Sprite LoadSpr(string name, int border = 0)
        => LoadSprAt("Assets/Resources/CoreUI/sprites/" + name + ".png", border);

    static Sprite LoadSprAt(string path, int border = 0)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
            // UI 스프라이트는 압축 금지: DXT/BC 블록압축이 그라데이션, 얇은선을 뭉개
            // 고해상도 원본이어도 엔진에선 "블러"처럼 보인다.
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (imp.filterMode != FilterMode.Bilinear) { imp.filterMode = FilterMode.Bilinear; dirty = true; }
            if (imp.maxTextureSize < 4096) { imp.maxTextureSize = 4096; dirty = true; }   // Panel_BG 2560 클램프 방지
            if (border > 0)
            {
                var b = new Vector4(border, border, border, border);
                if (imp.spriteBorder != b) { imp.spriteBorder = b; dirty = true; }
            }
            if (dirty) imp.SaveAndReimport();
        }
        else
        {
            Debug.LogWarning($"[UIBuilder] 스프라이트 없음: {path}");
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

    // 튜토리얼 코치마크(스포트라이트) 대상으로 등록되도록 TutorialHighlightTarget 부착 + id 설정
    static void AddHighlight(GameObject go, string id)
    {
        if (go == null) return;
        var h = go.GetComponent<TutorialHighlightTarget>();
        if (h == null) h = go.AddComponent<TutorialHighlightTarget>();
        var hso = new SerializedObject(h);
        var prop = hso.FindProperty("id");
        if (prop != null) { prop.stringValue = id; hso.ApplyModifiedProperties(); }
    }
}
