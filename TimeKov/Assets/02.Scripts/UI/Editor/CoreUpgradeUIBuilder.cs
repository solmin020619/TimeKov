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

        TextMeshProUGUI nxtTime    = MakeTMP("NextTimeText",    rightPanel.transform, new Vector2(110, 35), new Vector2(-55,  65), "0s",  18, Hex("CCDDFF"));
        TextMeshProUGUI nxtStamina = MakeTMP("NextStaminaText", rightPanel.transform, new Vector2(110, 35), new Vector2(-55,  18), "0",   18, Hex("CCDDFF"));
        TextMeshProUGUI nxtAtk     = MakeTMP("NextAtkText",     rightPanel.transform, new Vector2(110, 35), new Vector2(-55, -29), "0",   18, Hex("CCDDFF"));
        TextMeshProUGUI nxtDef     = MakeTMP("NextDefText",     rightPanel.transform, new Vector2(110, 35), new Vector2(-55, -76), "0",   18, Hex("CCDDFF"));

        TextMeshProUGUI dltTime    = MakeTMP("DeltaTimeText",    rightPanel.transform, new Vector2(105, 35), new Vector2(80,  65), "+0s ↑", 15, Hex("33CC66"));
        TextMeshProUGUI dltStamina = MakeTMP("DeltaStaminaText", rightPanel.transform, new Vector2(105, 35), new Vector2(80,  18), "+0 ↑",  15, Hex("33CC66"));
        TextMeshProUGUI dltAtk     = MakeTMP("DeltaAtkText",     rightPanel.transform, new Vector2(105, 35), new Vector2(80, -29), "+0 ↑",  15, Hex("33CC66"));
        TextMeshProUGUI dltDef     = MakeTMP("DeltaDefText",     rightPanel.transform, new Vector2(105, 35), new Vector2(80, -76), "+0 ↑",  15, Hex("33CC66"));

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

        // ── 레퍼런스 적용 ─────────────────────────────
        so.ApplyModifiedProperties();

        // ── 씬 저장 필요 표시 ─────────────────────────
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log($"[TIMEKOV] ✅ 코어 강화 UI 생성 완료! Canvas: '{canvas.name}' 하위에 CoreUpgradePanel이 추가됐습니다.");
        EditorUtility.DisplayDialog("완료", "코어 강화 UI가 생성됐습니다!\n\n하이어라키에서 CoreUpgradePanel을 확인하세요.\n씬을 저장(Ctrl+S)하는 것을 잊지 마세요.", "확인");
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
