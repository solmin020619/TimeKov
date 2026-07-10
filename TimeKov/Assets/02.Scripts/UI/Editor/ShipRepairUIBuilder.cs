// =====================================================================
// ShipRepairUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/우주선 수리 UI 생성 -> Canvas 안에 ShipRepairPanel 생성 + ref 연결.
// 풀스크린 대형 + 공장풍 프로스티드(BlurredImage) 콘솔 + 중앙 우주선 홀로그램(링게이지).
// 레벨 pip / 부품 행은 런타임에 ShipRepairUI 가 채운다(레벨 수 가변).
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;

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

        // 어두운 격납고 배경(우주 아님) + 뒤 클릭 차단
        var bg = MakeImage("Backdrop", rootRt, Vector2.zero, Vector2.zero, Color.white);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = null; bgImg.raycastTarget = true;
        var bgGrad = bg.AddComponent<UIFrostGradient>();
        bgGrad.topColor    = RGBA(14, 20, 30, 0.72f);
        bgGrad.bottomColor = RGBA(5, 8, 13, 0.90f);

        // ── 콘솔 카드 (중앙 대형) ──
        var card = MakeImage("Console", rootRt, new Vector2(1120, 680), Vector2.zero, RGBA(26, 36, 50, 0.30f));
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        var cardImg = card.GetComponent<Image>();
        cardImg.sprite = UISpriteFactory.RoundedRect(96, 26);
        cardImg.type   = Image.Type.Sliced;
        var cardMask = card.AddComponent<Mask>();
        cardMask.showMaskGraphic = true;

        AddFrostedBlur(crt);

        // 카드 내부 어두운 그라데이션(가독성)
        var inner = MakeImage("InnerDark", crt, Vector2.zero, Vector2.zero, Color.white);
        Stretch(inner.GetComponent<RectTransform>());
        inner.GetComponent<Image>().raycastTarget = false;
        var innerGrad = inner.AddComponent<UIFrostGradient>();
        innerGrad.topColor    = RGBA(20, 28, 40, 0.42f);
        innerGrad.bottomColor = RGBA(9, 13, 20, 0.60f);

        // ── 헤더 ──
        var eyebrow = MakeTMP("Eyebrow", crt, Vector2.zero, Vector2.zero, "폐선체 복원 관제", 14, RGBA(106, 212, 255, 0.9f), TextAlignmentOptions.Left);
        AnchorTop(eyebrow.rectTransform, 36, -52, -30);
        eyebrow.characterSpacing = 6f;

        var title = MakeTMP("Title", crt, Vector2.zero, Vector2.zero, "우주선 수리", 30, Hex("EAF3FB"), TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        AnchorTop(title.rectTransform, 36, -96, -56);

        var closeBtn = MakeButton("CloseButton", crt, new Vector2(46, 46), Vector2.zero, "X", 22, new Color(1f, 1f, 1f, 0f));
        var clrt = closeBtn.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(1, 1); clrt.pivot = new Vector2(1, 1);
        clrt.anchoredPosition = new Vector2(-22, -22);
        var clBtn = closeBtn.GetComponent<Button>();
        clBtn.transition = Selectable.Transition.ColorTint;
        var ccb = clBtn.colors;
        ccb.normalColor = new Color(1f, 1f, 1f, 0f);
        ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);
        ccb.pressedColor = new Color(0.20f, 0.24f, 0.34f, 0.36f);
        ccb.selectedColor = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor = new Color(1f, 1f, 1f, 0f);
        ccb.colorMultiplier = 1f; ccb.fadeDuration = 0.1f;
        clBtn.colors = ccb;
        SetRef(so, "closeButton", clBtn);

        var hdiv = MakeImage("HeaderDivider", crt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.5f));
        AnchorTopStretch(hdiv.GetComponent<RectTransform>(), 28, 28, -110, -108);
        hdiv.GetComponent<Image>().raycastTarget = false;

        // ── 레벨 사다리 ──
        var levelText = MakeTMP("LevelText", crt, Vector2.zero, Vector2.zero, "수리 단계  Lv.1 / 5", 16, Hex("CDD8E5"), TextAlignmentOptions.Left);
        levelText.fontStyle = FontStyles.Bold;
        AnchorTop(levelText.rectTransform, 36, -150, -122);
        SetRef(so, "levelText", levelText);

        var pipGo = new GameObject("PipContainer", typeof(RectTransform));
        pipGo.transform.SetParent(crt, false);
        var pipRt = pipGo.GetComponent<RectTransform>();
        pipRt.anchorMin = new Vector2(0, 1); pipRt.anchorMax = new Vector2(1, 1); pipRt.pivot = new Vector2(0.5f, 1);
        pipRt.offsetMin = new Vector2(300, -152); pipRt.offsetMax = new Vector2(-36, -120);
        var hlg = pipGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        SetRef(so, "pipContainer", pipRt);

        // ── 좌측 홀로그램 (복원도 링) ──
        var hero = MakeImage("HoloStage", crt, Vector2.zero, Vector2.zero, RGBA(12, 18, 28, 0.5f));
        var heroRt = hero.GetComponent<RectTransform>();
        heroRt.anchorMin = new Vector2(0, 0); heroRt.anchorMax = new Vector2(0.5f, 1);
        heroRt.offsetMin = new Vector2(36, 64); heroRt.offsetMax = new Vector2(-16, -170);
        var heroImg = hero.GetComponent<Image>();
        heroImg.sprite = UISpriteFactory.RoundedRect(64, 18); heroImg.type = Image.Type.Sliced;
        heroImg.raycastTarget = false;

        // 우주선 아트 슬롯(나중에 스프라이트 연결) — 지금은 비활성 placeholder
        var shipSlot = MakeImage("ShipHologramSlot", heroRt, new Vector2(300, 200), new Vector2(0, 12), new Color(1, 1, 1, 1));
        shipSlot.GetComponent<Image>().raycastTarget = false;
        shipSlot.GetComponent<Image>().enabled = false;   // 스프라이트 넣으면 켜기

        // 복원도 링 (트랙 + 게이지)
        var track = MakeImage("RingTrack", heroRt, new Vector2(300, 300), new Vector2(0, 12), RGBA(70, 96, 128, 0.35f));
        var trackImg = track.GetComponent<Image>();
        trackImg.sprite = UISpriteFactory.Ring(256, 9f);
        trackImg.raycastTarget = false;

        var gauge = MakeImage("RingGauge", heroRt, new Vector2(300, 300), new Vector2(0, 12), RGBA(106, 212, 255, 1f));
        var gaugeImg = gauge.GetComponent<Image>();
        gaugeImg.sprite = UISpriteFactory.Ring(256, 9f);
        gaugeImg.raycastTarget = false;
        gaugeImg.type = Image.Type.Filled;
        gaugeImg.fillMethod = Image.FillMethod.Radial360;
        gaugeImg.fillOrigin = (int)Image.Origin360.Top;
        gaugeImg.fillClockwise = true;
        gaugeImg.fillAmount = 0f;
        SetRef(so, "ringGauge", gaugeImg);

        var capTop = MakeTMP("HoloCaption", heroRt, new Vector2(240, 24), new Vector2(0, 60), "선체 복원도", 14, RGBA(122, 135, 151, 1f), TextAlignmentOptions.Center);
        capTop.characterSpacing = 4f;

        var pct = MakeTMP("RestorePercent", heroRt, new Vector2(240, 70), new Vector2(0, 6), "0%", 46, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Center);
        pct.fontStyle = FontStyles.Bold;
        SetRef(so, "restorePercentText", pct);

        // ── 우측 컬럼 ──
        var col = new GameObject("RightCol", typeof(RectTransform));
        col.transform.SetParent(crt, false);
        var colRt = col.GetComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0.5f, 0); colRt.anchorMax = new Vector2(1, 1);
        colRt.offsetMin = new Vector2(16, 64); colRt.offsetMax = new Vector2(-40, -170);

        var nextHeader = MakeTMP("NextHeader", colRt, Vector2.zero, Vector2.zero, "다음 수리   Lv.1 -> Lv.2", 15, RGBA(122, 135, 151, 1f), TextAlignmentOptions.Left);
        nextHeader.fontStyle = FontStyles.Bold;
        AnchorTopStretch(nextHeader.rectTransform, 4, 4, -34, -6);
        SetRef(so, "nextHeaderText", nextHeader);

        // 스탯 3행
        var statVals = new Object[3];
        statVals[0] = MakeStatRow(colRt, -50, "건축 범위");
        statVals[1] = MakeStatRow(colRt, -98, "설비 연료");
        statVals[2] = MakeStatRow(colRt, -146, "공장 가동속도");
        SetRefArray(so, "statValueTexts", statVals);

        var sdiv = MakeImage("StatDivider", colRt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.4f));
        AnchorTopStretch(sdiv.GetComponent<RectTransform>(), 4, 4, -204, -203);
        sdiv.GetComponent<Image>().raycastTarget = false;

        var partsLabel = MakeTMP("PartsLabel", colRt, Vector2.zero, Vector2.zero, "수리 부품", 15, Hex("CDD8E5"), TextAlignmentOptions.Left);
        partsLabel.fontStyle = FontStyles.Bold;
        AnchorTopStretch(partsLabel.rectTransform, 4, 120, -234, -210);

        var partsCount = MakeTMP("PartsCount", colRt, Vector2.zero, Vector2.zero, "회수  0 / 4", 13, RGBA(122, 135, 151, 1f), TextAlignmentOptions.Right);
        AnchorTopStretch(partsCount.rectTransform, 120, 4, -234, -210);
        SetRef(so, "partsCountText", partsCount);

        var partsGo = new GameObject("PartsContent", typeof(RectTransform));
        partsGo.transform.SetParent(colRt, false);
        var partsRt = partsGo.GetComponent<RectTransform>();
        partsRt.anchorMin = new Vector2(0, 0); partsRt.anchorMax = new Vector2(1, 1);
        partsRt.offsetMin = new Vector2(0, 72); partsRt.offsetMax = new Vector2(0, -244);
        var pvlg = partsGo.AddComponent<VerticalLayoutGroup>();
        pvlg.spacing = 6f; pvlg.childAlignment = TextAnchor.UpperCenter;
        pvlg.childControlWidth = true; pvlg.childControlHeight = false;
        pvlg.childForceExpandWidth = true; pvlg.childForceExpandHeight = false;
        SetRef(so, "partsContent", partsRt);

        // 수리 버튼
        var repair = MakeButton("RepairButton", colRt, Vector2.zero, Vector2.zero, "수리 실행", 20, new Color(0.20f, 0.66f, 0.95f, 1f));
        var repRt = repair.GetComponent<RectTransform>();
        repRt.anchorMin = new Vector2(0, 0); repRt.anchorMax = new Vector2(1, 0); repRt.pivot = new Vector2(0.5f, 0);
        repRt.offsetMin = new Vector2(0, 6); repRt.offsetMax = new Vector2(0, 60);
        SetRef(so, "repairButton", repair.GetComponent<Button>());
        SetRef(so, "repairButtonText", repair.GetComponentInChildren<TextMeshProUGUI>());

        // ── 푸터 ──
        var footer = MakeTMP("Footer", crt, Vector2.zero, Vector2.zero,
            "Lv.5 완료 시 - 격납 펜스 해제 / 탈출 시도 가능", 13, RGBA(110, 125, 141, 0.9f), TextAlignmentOptions.Center);
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

    // 스탯 1행: 라벨(좌) + 값(우). 값 TMP 반환.
    static TextMeshProUGUI MakeStatRow(Transform parent, float topY, string label)
    {
        var row = new GameObject("StatRow", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(0, topY - 40); rt.offsetMax = new Vector2(0, topY);
        var img = row.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(40, 10); img.type = Image.Type.Sliced;
        img.color = RGBA(20, 28, 40, 0.5f); img.raycastTarget = false;

        var nm = MakeTMP("Name", rt, Vector2.zero, Vector2.zero, label, 14, Hex("CDD8E5"), TextAlignmentOptions.Left);
        var nrt = nm.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(0.5f, 1);
        nrt.offsetMin = new Vector2(14, 0); nrt.offsetMax = new Vector2(0, 0);

        var val = MakeTMP("Value", rt, Vector2.zero, Vector2.zero, "-", 14, RGBA(106, 212, 255, 1f), TextAlignmentOptions.Right);
        var vrt = val.rectTransform;
        vrt.anchorMin = new Vector2(0.4f, 0); vrt.anchorMax = new Vector2(1, 1);
        vrt.offsetMin = new Vector2(0, 0); vrt.offsetMax = new Vector2(-14, 0);
        return val;
    }

    // ── 프로스티드 블러 (BaseUpgradeUIBuilder 와 동일 레시피) ──
    static void AddFrostedBlur(RectTransform parent)
    {
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform));
        blurGo.transform.SetParent(parent, false);
        Stretch(blurGo.GetComponent<RectTransform>());
        var blur = blurGo.AddComponent<BlurredImage>();
        blur.sprite = UISpriteFactory.RoundedRect(96, 26);
        blur.type   = Image.Type.Sliced;
        blur.color  = Color.white;
        blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = PickBuildCamera();
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            if (bs.blurSections != null)
                foreach (var sec in bs.blurSections) { sec.iterations = 5; sec.sampleDistance = 1.5f; }
            bs.vibrancy = 0f; bs.brightness = 0.02f; bs.contrast = 0f; bs.referenceResolution = 1080;
        }
        blur.Common.ValidateBlur();
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

    static Camera PickBuildCamera()
    {
        var main = Camera.main;
        if (main != null && main.targetTexture == null) return main;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null) return c;
        return main;
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
