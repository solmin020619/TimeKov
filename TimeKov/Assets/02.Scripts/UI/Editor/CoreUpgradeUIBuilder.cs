// =====================================================================
// CoreUpgradeUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/코어 강화 UI 생성 → Canvas 안에 CoreUpgradePanel 계층 자동 생성 + 필드 연결.
// 레이아웃 = CoreUI/README.md [최종 화면] 스펙: 스탯은 "체력(시간)" 하나만.
// (스프라이트는 비워두고 색 플레이스홀더만 — core.png/reference PNG는 인스펙터에서 드래그)
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CoreUpgradeUIBuilder
{
    [MenuItem("Tools/TIMEKOV/코어 강화 UI 생성")]
    public static void Build()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.\nCanvas를 먼저 만들어 주세요.", "확인");
            return;
        }

        // 기존 패널 전수 제거 (비활성 포함). 옛 빌더 잔재가 다른 부모 밑에 숨어
        // 런타임 싱글톤(CoreUpgradeUI.Instance)을 가로채면 새 패널이 파괴돼 "변경이 안 먹힌다".
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

        // ── 루트 = 컴포넌트 호스트. 항상 active로 둬야 Awake가 돌아 Instance가 등록되고,
        //    트리거(CoreUpgradeUI.Instance.Open())가 패널을 열 수 있다.
        //    실제 보이는 딤/패널은 PanelRoot 자식으로 빼서 그것만 껐다 켠다. ──
        GameObject root = new GameObject("CoreUpgradePanel", typeof(RectTransform));
        var rootTr = root.GetComponent<RectTransform>();
        rootTr.SetParent(canvas.transform, false);
        rootTr.anchorMin = Vector2.zero; rootTr.anchorMax = Vector2.one;
        rootTr.offsetMin = rootTr.offsetMax = Vector2.zero;

        CoreUpgradeUI ui = root.AddComponent<CoreUpgradeUI>();
        SerializedObject so = new SerializedObject(ui);

        // ── PanelRoot = 전체화면 딤(scrim) + 패널 내용. 평소 false, Open()에서 true. ──
        GameObject panelRootGo = new GameObject("PanelRoot", typeof(RectTransform), typeof(Image));
        var panelRootTr = panelRootGo.GetComponent<RectTransform>();
        panelRootTr.SetParent(rootTr, false);
        panelRootTr.anchorMin = Vector2.zero; panelRootTr.anchorMax = Vector2.one;
        panelRootTr.offsetMin = panelRootTr.offsetMax = Vector2.zero;
        panelRootGo.GetComponent<Image>().color = Hex("070C16", 190);   // 은은한 딤
        SetRef(so, "panelRoot", panelRootGo);
        panelRootGo.SetActive(false);

        // ── 패널 (1280x720) = 투명 컨테이너. 불투명 베이스/프레임은 스프라이트 블록에서 얹음. ──
        GameObject panel = MakeImage("Panel", panelRootTr,
            size: new Vector2(1280, 720), pos: Vector2.zero, color: Hex("0E1A2C", 0));

        // ── 타이틀 ──
        var title = MakeTMP("TitleText", panel.transform,
            new Vector2(500, 44), new Vector2(0, 305),
            "코어 <color=#5FC4FF>강화</color>", 30, Hex("EAF3FB"), TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;

        // ── 레벨 뱃지 (우상단) ──
        GameObject badge = MakeImage("LevelBadge", panel.transform,
            new Vector2(140, 60), new Vector2(540, 300), Hex("0B1422", 230));
        var levelTxt = MakeTMP("LevelText", badge.transform, Vector2.zero, Vector2.zero,
            "Lv.0 / 10", 22, Hex("EAF3FB"), TextAlignmentOptions.Center, stretch: true);
        levelTxt.fontStyle = FontStyles.Bold;
        SetRef(so, "levelText", levelTxt);

        // ── 닫기 버튼 (좌상단) ──
        GameObject closeBtn = MakeButton("CloseButton", panel.transform,
            new Vector2(48, 48), new Vector2(-596, 300), "X", 22, Hex("16263C", 215));
        SetRef(so, "closeButton", closeBtn.GetComponent<Button>());

        // ── 코어 이미지 (중앙) ──
        GameObject coreGo = MakeImage("CoreImage", panel.transform,
            new Vector2(300, 300), new Vector2(0, 45), Hex("4A9EFF", 0));
        coreGo.GetComponent<Image>().preserveAspect = true;
        SetRef(so, "coreImage", coreGo.GetComponent<Image>());

        // ── 현재 카드 (좌, 항상 표시) ──
        GameObject leftCard = MakeImage("CurrentCard", panel.transform,
            new Vector2(280, 210), new Vector2(-430, 0), Hex("0B1422", 150));
        MakeTMP("CurHead", leftCard.transform, new Vector2(240, 30), new Vector2(0, 78), "현재", 20, Hex("AEBFD0"), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        MakeTMP("CurLbl",  leftCard.transform, new Vector2(200, 24), new Vector2(0, 30), "체력", 16, Hex("AEBFD0"), TextAlignmentOptions.Center);
        var curTime = MakeTMP("CurrentTimeText", leftCard.transform, new Vector2(260, 80), new Vector2(0, -28), "0s", 56, Hex("EAF3FB"), TextAlignmentOptions.Center);
        curTime.fontStyle = FontStyles.Bold;
        SetRef(so, "currentTimeText", curTime);

        // ── 강화 정보 그룹 (MAX면 숨김) ──
        GameObject infoGroup = MakeEmpty("UpgradeInfoGroup", panel.transform);
        SetRef(so, "upgradeInfoGroup", infoGroup);

        // 강화 후 카드 (우)
        GameObject rightCard = MakeImage("NextCard", infoGroup.transform,
            new Vector2(280, 210), new Vector2(430, 0), Hex("0B1422", 150));
        MakeTMP("NxtHead", rightCard.transform, new Vector2(240, 30), new Vector2(0, 78), "강화 후", 20, Hex("AEE3FF"), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        MakeTMP("NxtLbl",  rightCard.transform, new Vector2(200, 24), new Vector2(0, 30), "체력", 16, Hex("AEBFD0"), TextAlignmentOptions.Center);
        var nxtTime = MakeTMP("NextTimeText", rightCard.transform, new Vector2(260, 80), new Vector2(0, -18), "0s", 56, Hex("AEE3FF"), TextAlignmentOptions.Center);
        nxtTime.fontStyle = FontStyles.Bold;
        SetRef(so, "nextTimeText", nxtTime);
        var dltTime = MakeTMP("DeltaTimeText", rightCard.transform, new Vector2(220, 24), new Vector2(0, -72), "+0s ↑", 17, Hex("46E08A"), TextAlignmentOptions.Center);
        dltTime.fontStyle = FontStyles.Bold;
        SetRef(so, "deltaTimeText", dltTime);

        // 하단 바 (재료 + 성공확률 + 게이지) — 행 간격 충분히 (겹침 방지)
        GameObject bottomBar = MakeImage("BottomBar", infoGroup.transform,
            new Vector2(640, 110), new Vector2(0, -215), Hex("0B1422", 200));
        GameObject kitIconGo = MakeImage("KitIcon", bottomBar.transform,
            new Vector2(48, 48), new Vector2(-288, 24), Hex("0D2040", 220));
        SetRef(so, "kitIconImage", kitIconGo.GetComponent<Image>());
        var kitName  = MakeTMP("KitNameText",     bottomBar.transform, new Vector2(280, 24), new Vector2(-100, 30), "필요: 코어 키트", 16, Hex("EAF3FB"), TextAlignmentOptions.Left);
        var kitCount = MakeTMP("KitCountText",    bottomBar.transform, new Vector2(200, 22), new Vector2(-110, 4),  "보유:  0 / 3",   15, Hex("AEBFD0"), TextAlignmentOptions.Left);
        var kitShort = MakeTMP("KitShortageText", bottomBar.transform, new Vector2(130, 22), new Vector2(95, 4),    "",              14, Hex("FF6068"), TextAlignmentOptions.Left);
        var succRate = MakeTMP("SuccessRateText", bottomBar.transform, new Vector2(240, 26), new Vector2(170, 30),  "성공 확률:  50%", 18, Hex("AEE3FF"), TextAlignmentOptions.Right);
        succRate.fontStyle = FontStyles.Bold;
        SetRef(so, "kitNameText",     kitName);
        SetRef(so, "kitCountText",    kitCount);
        SetRef(so, "kitShortageText", kitShort);
        SetRef(so, "successRateText", succRate);

        // 성공 확률 게이지 (바닥)
        GameObject gaugeBg = MakeImage("GaugeBG", bottomBar.transform, new Vector2(600, 10), new Vector2(0, -34), Hex("05080D", 255));
        GameObject gaugeFillGo = MakeImage("GaugeFill", gaugeBg.transform, new Vector2(600, 10), Vector2.zero, Hex("6FD2FF", 255));
        var gaugeFillImg = gaugeFillGo.GetComponent<Image>();
        gaugeFillImg.type       = Image.Type.Filled;
        gaugeFillImg.fillMethod = Image.FillMethod.Horizontal;
        gaugeFillImg.fillOrigin = 0; // Left
        gaugeFillImg.fillAmount = 0.5f;
        SetRef(so, "gaugeFill", gaugeFillImg);

        // 강화 시작 버튼
        GameObject upgradeBtn = MakeButton("UpgradeButton", infoGroup.transform,
            new Vector2(360, 60), new Vector2(0, -312), "강화 시작", 20, Hex("2F9BE0"));
        SetRef(so, "upgradeButton",     upgradeBtn.GetComponent<Button>());
        SetRef(so, "upgradeButtonText", upgradeBtn.GetComponentInChildren<TextMeshProUGUI>());

        // ── MAX 그룹 ──
        GameObject maxGroup = MakeEmpty("MaxLevelGroup", panel.transform);
        MakeTMP("MaxText", maxGroup.transform, new Vector2(500, 50), new Vector2(0, -200), "최대 단계 달성", 24, Hex("FFD66B"), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        maxGroup.SetActive(false);
        SetRef(so, "maxLevelGroup", maxGroup);

        // ── 피드백 텍스트 ──
        var feedback = MakeTMP("FeedbackText", panel.transform, new Vector2(600, 40), new Vector2(0, -150), "", 22, Hex("EAF3FB"), TextAlignmentOptions.Center);
        feedback.fontStyle = FontStyles.Bold;
        feedback.gameObject.SetActive(false);
        SetRef(so, "feedbackText", feedback);

        // ── 스프라이트 자동 적용 (CoreUI/sprites) — Sprite 임포트 + 9-slice border 자동 ──
        SetSpr(badge,       LoadSpr("Bar_Frame", 36),          Image.Type.Sliced);   // 종욱이 LevelBadge->Bar_Frame 선택, 재생성 보존
        SetSpr(leftCard,    LoadSpr("Card_Frame", 48),         Image.Type.Sliced);
        SetSpr(rightCard,   LoadSpr("Card_Frame", 48),         Image.Type.Sliced);
        SetSpr(bottomBar,   LoadSpr("Bar_Frame", 36),          Image.Type.Sliced);
        SetSpr(kitIconGo,   LoadSpr("KitSlot"),                Image.Type.Simple);   // 육각이라 9-slice 금지(찌그러짐)
        SetSpr(gaugeBg,     LoadSpr("Gauge_Trough", 6),        Image.Type.Sliced);
        SetSpr(gaugeFillGo, LoadSpr("Gauge_Fill", 6),          Image.Type.Filled);
        gaugeFillGo.GetComponent<Image>().color = Hex("6FD2FF");   // 채움은 시안 (흰색 아님)
        SetSpr(upgradeBtn,  LoadSpr("Btn_Enhance_Normal", 24), Image.Type.Sliced);
        SetSpr(coreGo,      LoadSpr("core"), Image.Type.Simple);   // core.png는 sprites/ 안에 있음

        // 패널 배경 = Panel_BG 2겹(같은 크기/둥근 모양). 단색 직각 베이스를 쓰면 그 직선 변이
        // 반투명 프레임 뒤로 비쳐 상/우/하에 "선"이 생김 → 같은 스프라이트를 겹쳐 경계를 없애고
        // 불투명도도 보강(코너 삐짐 동시 해결).
        var panelBase = MakeImage("PanelBase", panel.transform, new Vector2(1280, 720), Vector2.zero, Color.white);
        SetSpr(panelBase, LoadSpr("Panel_BG"), Image.Type.Simple);
        panelBase.GetComponent<Image>().color = Hex("0E1A2C", 255);   // 뒤겹은 navy 틴트로 시안 테두리 죽임 → 테두리 1줄, 불투명 채움 유지
        panelBase.transform.SetSiblingIndex(0);
        var panelFrame = MakeImage("PanelFrame", panel.transform, new Vector2(1280, 720), Vector2.zero, Color.white);
        SetSpr(panelFrame, LoadSpr("Panel_BG"), Image.Type.Simple);
        panelFrame.transform.SetSiblingIndex(1);

        // 데코 링 + 코어 글로우 (프레임 위 · 코어 뒤, 살짝 작게)
        var dring = MakeImage("DecoRing", panel.transform, new Vector2(360, 360), new Vector2(0, 45), Color.white);
        SetSpr(dring, LoadSpr("Deco_Ring"), Image.Type.Simple);
        dring.transform.SetSiblingIndex(2);
        var glow = MakeImage("CoreGlow", panel.transform, new Vector2(380, 380), new Vector2(0, 45), Color.white);
        SetSpr(glow, LoadSpr("Core_Glow"), Image.Type.Simple);
        glow.transform.SetSiblingIndex(3);

        // 키트 아이콘 (슬롯 위에)
        var kitItemIcon = MakeImage("KitItemIcon", kitIconGo.transform, new Vector2(34, 34), Vector2.zero, Color.white);
        SetSpr(kitItemIcon, LoadSpr("KitIcon"), Image.Type.Simple);

        // ── 인라인 시계 (강화 시 코어→시계 전환) — Clock_* 스프라이트 사용 ──
        var clockGo = MakeEmpty("ClockGroup", panel.transform);
        var clockRt = clockGo.GetComponent<RectTransform>();
        clockRt.anchorMin = clockRt.anchorMax = clockRt.pivot = new Vector2(0.5f, 0.5f);
        clockRt.sizeDelta        = new Vector2(300, 300);
        clockRt.anchoredPosition = new Vector2(0, 45);
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
        var spinHintTmp = MakeTMP("SpinHint", panel.transform, new Vector2(540, 36), new Vector2(0, -120),
            "멈춰서 <color=#46E08A>성공존</color>에 맞추세요!", 18, Hex("AEE3FF"), TextAlignmentOptions.Center);
        spinHintTmp.fontStyle = FontStyles.Bold;
        spinHintTmp.gameObject.SetActive(false);
        SetRef(so, "spinHint", spinHintTmp.gameObject);

        var judgeChip = MakeTMP("JudgeChip", panel.transform, new Vector2(360, 40), new Vector2(0, 200),
            "", 26, Hex("FFD66B"), TextAlignmentOptions.Center);
        judgeChip.fontStyle = FontStyles.Bold;
        judgeChip.gameObject.SetActive(false);
        SetRef(so, "judgeChipText", judgeChip);

        // ── 적용 + 저장 ──
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[TIMEKOV] 코어 강화 UI 생성 완료 (README 스펙: 체력만).");
        EditorUtility.DisplayDialog("완료",
            "코어 강화 UI 생성 완료!\n\n- CoreUpgradePanel은 active(켜진 상태)로 둘 것\n  (안 보이는 처리는 자식 PanelRoot가 자동으로 함)\n- Ctrl+S로 씬 저장", "확인");
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
        tmp.enableWordWrapping = false;
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
            Debug.LogWarning($"[UIBuilder] SerializedProperty 없음: '{fieldName}' — CoreUpgradeUI.cs 필드명 확인 필요");
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
            // UI 스프라이트는 압축 금지: DXT/BC 블록압축이 그라데이션·얇은선을 뭉개
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
}
