// =====================================================================
// CoreUpgradeUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/코어 강화 UI 생성 실행 시 World 씬 Canvas 안에
// CoreUpgradePanel 전체 계층을 자동으로 만들고 레퍼런스 연결까지 처리
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
        // ── Canvas 찾기 ───────────────────────────────
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.\nCanvas를 먼저 만들어 주세요.", "확인");
            return;
        }

        // ── 기존 패널 삭제 ────────────────────────────
        Transform existing = canvas.transform.Find("CoreUpgradePanel");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                "CoreUpgradePanel이 이미 존재합니다.\n기존 패널을 삭제하고 새로 만들까요?",
                "새로 만들기", "취소");
            if (!replace) return;
            Object.DestroyImmediate(existing.gameObject);
        }

        // ── 루트 패널 ─────────────────────────────────
        GameObject root = MakeImage("CoreUpgradePanel", canvas.transform,
            size: new Vector2(1100, 700), pos: Vector2.zero, color: Hex("1A2035", 240));
        root.SetActive(false); // 처음엔 숨김 상태

        CoreUpgradeUI ui = root.AddComponent<CoreUpgradeUI>();
        SerializedObject so = new SerializedObject(ui);
        SetRef(so, "panelRoot", root);

        // ── 타이틀 ────────────────────────────────────
        MakeTMP("TitleText", root.transform,
            size: new Vector2(400, 50), pos: new Vector2(0, 310),
            text: "코어 강화", fontSize: 28,
            color: Hex("7DD4FC"), align: TextAlignmentOptions.Center);

        // ── 레벨 뱃지 ─────────────────────────────────
        GameObject badge = MakeImage("LevelBadge", root.transform,
            size: new Vector2(130, 130), pos: new Vector2(430, 275),
            color: Hex("0D1929", 230));
        TextMeshProUGUI levelTxt = MakeTMP("LevelText", badge.transform,
            size: Vector2.zero, pos: Vector2.zero,
            text: "Lv.0\n/ 10", fontSize: 22,
            color: Color.white, align: TextAlignmentOptions.Center, stretch: true);
        SetRef(so, "levelText", levelTxt);

        // ── 코어 이미지 (플레이스홀더) ───────────────
        GameObject coreGo = MakeImage("CoreImage", root.transform,
            size: new Vector2(220, 260), pos: new Vector2(0, 30),
            color: Hex("4A9EFF", 100));
        SetRef(so, "coreImage", coreGo.GetComponent<Image>());

        // ── 현재 스탯 패널 (왼쪽) ────────────────────
        GameObject leftPanel = MakeImage("LeftPanel", root.transform,
            size: new Vector2(280, 300), pos: new Vector2(-385, 30),
            color: Hex("0D1929", 200));
        MakeTMP("LeftTitle", leftPanel.transform,
            size: new Vector2(240, 35), pos: new Vector2(0, 120),
            text: "현재 스탯", fontSize: 18,
            color: Hex("7DD4FC"), align: TextAlignmentOptions.Center);

        TextMeshProUGUI curTime    = MakeTMP("CurrentTimeText",    leftPanel.transform, new Vector2(240, 35), new Vector2(0,  65), "Time:  0s",   18, Hex("CCDDFF"));
        TextMeshProUGUI curStamina = MakeTMP("CurrentStaminaText", leftPanel.transform, new Vector2(240, 35), new Vector2(0,  18), "Stamina:  0", 18, Hex("CCDDFF"));
        TextMeshProUGUI curAtk     = MakeTMP("CurrentAtkText",     leftPanel.transform, new Vector2(240, 35), new Vector2(0, -29), "ATK:  0",     18, Hex("CCDDFF"));
        TextMeshProUGUI curDef     = MakeTMP("CurrentDefText",     leftPanel.transform, new Vector2(240, 35), new Vector2(0, -76), "DEF:  0",     18, Hex("CCDDFF"));

        SetRef(so, "currentTimeText",    curTime);
        SetRef(so, "currentStaminaText", curStamina);
        SetRef(so, "currentAtkText",     curAtk);
        SetRef(so, "currentDefText",     curDef);

        // ── 강화 후 스탯 패널 (오른쪽) ───────────────
        GameObject rightPanel = MakeImage("RightPanel", root.transform,
            size: new Vector2(280, 300), pos: new Vector2(385, 30),
            color: Hex("0D1929", 200));
        MakeTMP("RightTitle", rightPanel.transform,
            size: new Vector2(240, 35), pos: new Vector2(0, 120),
            text: "강화 후 스탯", fontSize: 18,
            color: Hex("7DD4FC"), align: TextAlignmentOptions.Center);

        TextMeshProUGUI nxtTime    = MakeTMP("NextTimeText",    rightPanel.transform, new Vector2(155, 35), new Vector2(-45,  65), "Time:  0s",   18, Hex("CCDDFF"));
        TextMeshProUGUI nxtStamina = MakeTMP("NextStaminaText", rightPanel.transform, new Vector2(155, 35), new Vector2(-45,  18), "Stamina:  0", 18, Hex("CCDDFF"));
        TextMeshProUGUI nxtAtk     = MakeTMP("NextAtkText",     rightPanel.transform, new Vector2(155, 35), new Vector2(-45, -29), "ATK:  0",     18, Hex("CCDDFF"));
        TextMeshProUGUI nxtDef     = MakeTMP("NextDefText",     rightPanel.transform, new Vector2(155, 35), new Vector2(-45, -76), "DEF:  0",     18, Hex("CCDDFF"));

        TextMeshProUGUI dltTime    = MakeTMP("DeltaTimeText",    rightPanel.transform, new Vector2(85, 35), new Vector2(105,  65), "+0s ↑", 15, Hex("33CC66"));
        TextMeshProUGUI dltStamina = MakeTMP("DeltaStaminaText", rightPanel.transform, new Vector2(85, 35), new Vector2(105,  18), "+0 ↑",  15, Hex("33CC66"));
        TextMeshProUGUI dltAtk     = MakeTMP("DeltaAtkText",     rightPanel.transform, new Vector2(85, 35), new Vector2(105, -29), "+0 ↑",  15, Hex("33CC66"));
        TextMeshProUGUI dltDef     = MakeTMP("DeltaDefText",     rightPanel.transform, new Vector2(85, 35), new Vector2(105, -76), "+0 ↑",  15, Hex("33CC66"));

        SetRef(so, "nextTimeText",     nxtTime);
        SetRef(so, "nextStaminaText",  nxtStamina);
        SetRef(so, "nextAtkText",      nxtAtk);
        SetRef(so, "nextDefText",      nxtDef);
        SetRef(so, "deltaTimeText",    dltTime);
        SetRef(so, "deltaStaminaText", dltStamina);
        SetRef(so, "deltaAtkText",     dltAtk);
        SetRef(so, "deltaDefText",     dltDef);

        // ── UpgradeInfoGroup ──────────────────────────
        GameObject infoGroup = MakeEmpty("UpgradeInfoGroup", root.transform);
        SetRef(so, "upgradeInfoGroup", infoGroup);

        // ── 재료 패널 ─────────────────────────────────
        GameObject kitPanel = MakeImage("KitPanel", infoGroup.transform,
            size: new Vector2(650, 115), pos: new Vector2(-50, -210),
            color: Hex("0D2040", 220));

        GameObject kitIconGo = MakeImage("KitIcon", kitPanel.transform,
            size: new Vector2(70, 70), pos: new Vector2(-255, 0),
            color: Hex("4A6080", 200));
        SetRef(so, "kitIconImage", kitIconGo.GetComponent<Image>());

        TextMeshProUGUI kitName  = MakeTMP("KitNameText",     kitPanel.transform, new Vector2(390, 32), new Vector2(55,  35), "필요: 내장 코어 보강 키트 I", 15, Color.white);
        TextMeshProUGUI kitCount = MakeTMP("KitCountText",    kitPanel.transform, new Vector2(190, 28), new Vector2(-15,  0), "보유:  0 / 1",              14, Color.white);
        TextMeshProUGUI kitShort = MakeTMP("KitShortageText", kitPanel.transform, new Vector2(150, 28), new Vector2(165,  0), "← 1개 부족",               13, Hex("FF4444"));
        TextMeshProUGUI succRate = MakeTMP("SuccessRateText", kitPanel.transform, new Vector2(280, 28), new Vector2(25, -35), "성공 확률:  100%",          15, Hex("4DCFFF"));

        SetRef(so, "kitNameText",     kitName);
        SetRef(so, "kitCountText",    kitCount);
        SetRef(so, "kitShortageText", kitShort);
        SetRef(so, "successRateText", succRate);

        // ── 강화 버튼 ─────────────────────────────────
        GameObject upgradeBtn = MakeButton("UpgradeButton", infoGroup.transform,
            size: new Vector2(390, 65), pos: new Vector2(-65, -305),
            label: "강화", fontSize: 24, bgColor: Hex("1A4080"));
        SetRef(so, "upgradeButton",     upgradeBtn.GetComponent<Button>());
        SetRef(so, "upgradeButtonText", upgradeBtn.GetComponentInChildren<TextMeshProUGUI>());

        // ── 닫기 버튼 ─────────────────────────────────
        GameObject closeBtn = MakeButton("CloseButton", root.transform,
            size: new Vector2(120, 65), pos: new Vector2(430, -305),
            label: "닫기", fontSize: 20, bgColor: Hex("2A2A3A"));
        SetRef(so, "closeButton", closeBtn.GetComponent<Button>());

        // ── MaxLevelGroup ─────────────────────────────
        GameObject maxGroup = MakeEmpty("MaxLevelGroup", root.transform);
        MakeTMP("MaxText", maxGroup.transform,
            size: new Vector2(500, 50), pos: new Vector2(0, -230),
            text: "★ 최대 단계 달성", fontSize: 24,
            color: Hex("FFD700"), align: TextAlignmentOptions.Center);
        maxGroup.SetActive(false);
        SetRef(so, "maxLevelGroup", maxGroup);

        // ── 피드백 텍스트 ──────────────────────────────
        TextMeshProUGUI feedback = MakeTMP("FeedbackText", root.transform,
            size: new Vector2(700, 40), pos: new Vector2(0, -265),
            text: "", fontSize: 18,
            color: Color.white, align: TextAlignmentOptions.Center);
        feedback.gameObject.SetActive(false);
        SetRef(so, "feedbackText", feedback);

        // ── 타임 캐치 UI ───────────────────────────────
        BuildTimeCatch(root, so);

        // ── 레퍼런스 적용 ─────────────────────────────
        so.ApplyModifiedProperties();

        // ── 씬 저장 필요 표시 ─────────────────────────
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log($"[TIMEKOV] ✅ 코어 강화 UI 생성 완료! Canvas: '{canvas.name}' 하위에 CoreUpgradePanel이 추가됐습니다.");
        EditorUtility.DisplayDialog("완료", "코어 강화 UI가 생성됐습니다!\n\n하이어라키에서 CoreUpgradePanel을 확인하세요.\n씬을 저장(Ctrl+S)하는 것을 잊지 마세요.", "확인");
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
        // 항상 활성. 화면 정중앙 오버레이 팝업
        var host = new GameObject("TimeCatchHost", typeof(RectTransform));
        host.transform.SetParent(root.transform, false);
        var hostRt = host.GetComponent<RectTransform>();
        hostRt.anchorMin = hostRt.anchorMax = hostRt.pivot = new Vector2(0.5f, 0.5f);
        hostRt.sizeDelta        = Vector2.zero;
        hostRt.anchoredPosition = Vector2.zero;

        var tc  = host.AddComponent<TimeCatchUI>();
        var tso = new SerializedObject(tc);

        // ── 팝업 패널 (처음엔 숨김) ──────────────────────
        // 전체 화면을 살짝 가리는 반투명 딤 레이어 + 팝업 창
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

        // 파란 외곽 테두리 효과 (살짝 큰 배경)
        var border = MakeImage("Border", popup.transform,
            new Vector2(380, 500), Vector2.zero, Hex("1A4060", 180));
        border.transform.SetAsFirstSibling();

        // ── 타이틀 ────────────────────────────────────────
        MakeTMP("Title", popup.transform,
            new Vector2(340, 45), new Vector2(0f, 210f),
            "TIME  CATCH", 26f, Hex("7DD4FC"),
            TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        // 타이틀 구분선
        MakeImage("TitleLine", popup.transform,
            new Vector2(320, 1), new Vector2(0f, 185f), Hex("1A4060", 200));

        // ── 시계 영역 컨테이너 ────────────────────────────
        var clock = new GameObject("ClockArea", typeof(RectTransform));
        clock.transform.SetParent(popup.transform, false);
        var clockRt = clock.GetComponent<RectTransform>();
        clockRt.anchorMin = clockRt.anchorMax = clockRt.pivot = new Vector2(0.5f, 0.5f);
        clockRt.sizeDelta        = new Vector2(260, 260);
        clockRt.anchoredPosition = new Vector2(0f, 40f);

        // 시계 어두운 배경 원
        MakeImage("ClockFace", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("030A12", 255));

        // 외곽 링 (Radial360 full, 얇은 청록 링)
        var outerRingGo = MakeImage("OuterRing", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("7DD4FC", 90));
        var outerRingImg = outerRingGo.GetComponent<Image>();
        outerRingImg.type       = Image.Type.Filled;
        outerRingImg.fillMethod = Image.FillMethod.Radial360;
        outerRingImg.fillAmount = 1f;
        SetRef(tso, "trackRingImage", outerRingImg);

        // 성공 구간 아크 (Radial360, 초록, 12시 중앙)
        var zoneGo  = MakeImage("SuccessZone", clock.transform,
            new Vector2(260, 260), Vector2.zero, Hex("00E676", 220));
        var zoneImg = zoneGo.GetComponent<Image>();
        zoneImg.type       = Image.Type.Filled;
        zoneImg.fillMethod = Image.FillMethod.Radial360;
        zoneImg.fillOrigin = 2;           // Top
        zoneImg.fillAmount = 60f / 360f;  // 기본 60°
        zoneGo.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        SetRef(tso, "successZoneImage", zoneImg);

        // 내부 마스크 (도넛 효과)
        MakeImage("InnerMask", clock.transform,
            new Vector2(210, 210), Vector2.zero, Hex("030A12", 255));

        // 12개 눈금
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

        // 12시 성공 구간 강조 마커
        MakeImage("TopMarker", clock.transform,
            new Vector2(5f, 18f), new Vector2(0f, 108f), Hex("00E676", 255));

        // 바늘 (pivot 하단 중앙 → 중심에서 위로 뻗음)
        var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        needleGo.transform.SetParent(clock.transform, false);
        var needleRt = needleGo.GetComponent<RectTransform>();
        needleRt.anchorMin = needleRt.anchorMax = new Vector2(0.5f, 0.5f);
        needleRt.pivot     = new Vector2(0.5f, 0f);  // 하단 = 회전축
        needleRt.sizeDelta        = new Vector2(3f, 90f);
        needleRt.anchoredPosition = Vector2.zero;
        needleGo.GetComponent<Image>().color = Color.white;
        SetRef(tso, "needle", needleRt);

        // 중심 핀
        MakeImage("CenterPin", clock.transform,
            new Vector2(10f, 10f), Vector2.zero, Hex("7DD4FC", 255));

        // ── 확률 표시 ─────────────────────────────────────
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

        // ── 하단 안내 ─────────────────────────────────────
        MakeImage("GuideLine", popup.transform,
            new Vector2(320, 1), new Vector2(0f, -175f), Hex("1A4060", 200));

        var guide = MakeTMP("GuideText", popup.transform,
            new Vector2(340, 36), new Vector2(0f, -205f),
            "SPACE", 22f, Hex("7DD4FC"),
            TextAlignmentOptions.Center);
        guide.fontStyle = FontStyles.Bold;
        SetRef(tso, "guideText", guide);

        // ── 결과 텍스트 ───────────────────────────────────
        var result = MakeTMP("ResultText", popup.transform,
            new Vector2(340, 40), new Vector2(0f, -205f),
            "", 22f, Hex("33CC66"), TextAlignmentOptions.Center);
        result.fontStyle = FontStyles.Bold;
        result.gameObject.SetActive(false);
        SetRef(tso, "resultText", result);

        tso.ApplyModifiedProperties();

        // CoreUpgradeUI 연결
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

        // TMP 텍스트 자식
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
}
