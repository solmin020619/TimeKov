// =====================================================================
// BaseUpgradeUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/기지 업그레이드 UI 생성 -> Canvas 안에 BaseUpgradePanel 생성 + ref 연결.
// 인벤토리/창고와 동일 디자인: 프로스티드 블러(BlurredImage) + 라운드 + Mask + 슬라이드.
// 항목 행은 런타임에 BaseUpgradeUI 가 RowTemplate/GroupTemplate 를 복제해 채운다.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;

public static class BaseUpgradeUIBuilder
{
    [MenuItem("Tools/TIMEKOV/기지 업그레이드 UI 생성")]
    public static void Build()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.", "확인");
            return;
        }

        // 기존 패널 제거 (싱글톤 가로채기 방지)
        var existing = Object.FindObjectsByType<BaseUpgradeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 0)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                $"기존 기지 업그레이드 패널 {existing.Length}개가 있습니다.\n모두 삭제하고 새로 만들까요?",
                "새로 만들기", "취소");
            if (!replace) return;
            foreach (var u in existing) if (u != null) Object.DestroyImmediate(u.gameObject);
        }

        // 호스트(항상 active) — Awake 가 돌아 Instance 등록. 보이는 건 PanelRoot 카드만 토글.
        var host = new GameObject("BaseUpgradePanel", typeof(RectTransform));
        var hostRt = host.GetComponent<RectTransform>();
        hostRt.SetParent(canvas.transform, false);
        Stretch(hostRt);

        var ui = host.AddComponent<BaseUpgradeUI>();
        var so = new SerializedObject(ui);

        // ── 카드 (오른쪽 정렬, 라운드 + Mask) = panelRoot ──
        var card = MakeImage("PanelRoot", hostRt, new Vector2(560, 860), Vector2.zero, new Color(1f, 1f, 1f, 0.12f));
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
        crt.pivot     = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-40f, 0f);
        var cardImg = card.GetComponent<Image>();
        cardImg.sprite = UISpriteFactory.RoundedRect(96, 28);
        cardImg.type   = Image.Type.Sliced;
        var cardMask = card.AddComponent<Mask>();
        cardMask.showMaskGraphic = true;

        var slide = card.AddComponent<UISlideEffect>();
        slide.offsetX = 48f;
        SetRef(so, "panelRoot", card);
        SetRef(so, "slide", slide);

        // 프로스티드 블러 (인벤과 동일 레시피)
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform));
        blurGo.transform.SetParent(crt, false);
        Stretch(blurGo.GetComponent<RectTransform>());
        var blur = blurGo.AddComponent<BlurredImage>();
        blur.sprite = UISpriteFactory.RoundedRect(96, 28);
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

        // 어두운 배경 그라데이션
        var bgGo = MakeImage("BgDark", crt, Vector2.zero, Vector2.zero, Color.white);
        Stretch(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = null; bgImg.raycastTarget = true;   // 뒤 클릭 차단
        var bgGrad = bgGo.AddComponent<UIFrostGradient>();
        bgGrad.topColor    = RGBA(22, 28, 40, 0.55f);
        bgGrad.bottomColor = RGBA(8, 11, 18, 0.78f);

        // ── 헤더: 타이틀 + 닫기 ──
        var title = MakeTMP("Title", crt, new Vector2(440, 40), Vector2.zero,
            "통합 핵심 구역 내 업그레이드 가능 항목", 22, Hex("EAF3FB"), TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.offsetMin = new Vector2(28, -66); trt.offsetMax = new Vector2(-74, -22);

        var closeBtn = MakeButton("CloseButton", crt, new Vector2(44, 44), Vector2.zero, "✕", 24, new Color(1f, 1f, 1f, 0f));
        var clrt = closeBtn.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(1, 1); clrt.pivot = new Vector2(1, 1);
        clrt.anchoredPosition = new Vector2(-18, -18);
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

        // 헤더 구분선
        var hdiv = MakeImage("HeaderDivider", crt, Vector2.zero, Vector2.zero, RGBA(84, 98, 122, 0.55f));
        var hdrt = hdiv.GetComponent<RectTransform>();
        hdrt.anchorMin = new Vector2(0, 1); hdrt.anchorMax = new Vector2(1, 1); hdrt.pivot = new Vector2(0.5f, 1);
        hdrt.offsetMin = new Vector2(20, -78); hdrt.offsetMax = new Vector2(-20, -76);
        hdiv.GetComponent<Image>().raycastTarget = false;

        // ── 스크롤 영역 ──
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        scrollGo.transform.SetParent(crt, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 1);
        srt.offsetMin = new Vector2(18, 232); srt.offsetMax = new Vector2(-18, -86);   // 하단 재료/제출 영역 확보
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(srt, false);
        var cont = contentGo.GetComponent<RectTransform>();
        cont.anchorMin = new Vector2(0, 1); cont.anchorMax = new Vector2(1, 1); cont.pivot = new Vector2(0.5f, 1);
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cont;
        scroll.viewport = srt;
        SetRef(so, "content", cont);

        // ── 하단 힌트 ──
        var hint = MakeTMP("Hint", crt, new Vector2(440, 26), Vector2.zero,
            "업그레이드 항목을 선택하세요.", 15, RGBA(150, 178, 204, 0.85f), TextAlignmentOptions.Right);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(0, 0); hrt.anchorMax = new Vector2(1, 0); hrt.pivot = new Vector2(0.5f, 0);
        hrt.offsetMin = new Vector2(22, 16); hrt.offsetMax = new Vector2(-22, 46);

        // ── 하단 재료 슬롯 3칸 ──
        var slotComps = new Object[3];
        float[] slotX = { -100f, 0f, 100f };
        for (int i = 0; i < 3; i++)
            slotComps[i] = MakeMaterialSlot(crt, new Vector2(slotX[i], 126f));
        SetRefArray(so, "materialSlots", slotComps);

        // ── 제출 버튼 ──
        var submit = MakeButton("SubmitButton", crt, Vector2.zero, Vector2.zero, "제출", 20, new Color(0.20f, 0.66f, 0.95f, 1f));
        var subRt = submit.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0, 0); subRt.anchorMax = new Vector2(1, 0); subRt.pivot = new Vector2(0.5f, 0);
        subRt.offsetMin = new Vector2(28, 56); subRt.offsetMax = new Vector2(-28, 108);
        SetRef(so, "submitButton", submit.GetComponent<Button>());
        SetRef(so, "submitButtonText", submit.GetComponentInChildren<TextMeshProUGUI>());

        // ── 행 템플릿 (비활성, content 밖) ──
        SetRef(so, "rowTemplate", BuildRowTemplate(crt));
        SetRef(so, "groupTemplate", BuildGroupTemplate(crt));

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = host;

        EditorUtility.DisplayDialog("완료",
            "기지 업그레이드 UI 생성 완료!\n\n" +
            "- BaseUpgradePanel 은 active(켜짐)로 둘 것\n" +
            "- BaseUpgradeUI 의 zoneCamera 에 사선 카메라 연결\n" +
            "- 항목/재료는 BaseUpgradeManager 인스펙터에서 정의\n" +
            "- Ctrl+S 로 씬 저장", "확인");
    }

    // 행 템플릿: 라운드 배경 + Button + Name/Cost/CostIcon/Mark
    static GameObject BuildRowTemplate(Transform parent)
    {
        var go = MakeImage("RowTemplate", parent, new Vector2(500, 64), Vector2.zero, RGBA(216, 224, 237, 0.10f));
        var img = go.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(64, 16);
        img.type   = Image.Type.Sliced;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 46f;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var rrt = (RectTransform)go.transform;

        var name = MakeTMP("Name", rrt, Vector2.zero, Vector2.zero, "항목", 19, Hex("EAF3FB"), TextAlignmentOptions.Left);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0, 0.5f);
        nrt.offsetMin = new Vector2(48, 0); nrt.offsetMax = new Vector2(-52, 0);

        var markGo = MakeImage("Mark", rrt, new Vector2(28, 28), Vector2.zero, Color.white);
        markGo.GetComponent<Image>().raycastTarget = false;
        var mrt = markGo.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(1, 0.5f); mrt.pivot = new Vector2(1, 0.5f);
        mrt.anchoredPosition = new Vector2(-22, 0);
        markGo.SetActive(false);

        go.SetActive(false);
        return go;
    }

    static GameObject BuildGroupTemplate(Transform parent)
    {
        var go = new GameObject("GroupTemplate", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;

        var label = MakeTMP("Label", go.transform, Vector2.zero, Vector2.zero, "그룹", 16, RGBA(160, 184, 208, 1f), TextAlignmentOptions.Left);
        label.fontStyle = FontStyles.Bold;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(8, 0); lrt.offsetMax = new Vector2(-8, 0);

        go.SetActive(false);
        return go;
    }

    // 재료 칸 1개: 라운드 프레임(호버 받음) + 아이콘 + 현재/필요 텍스트 + BaseUpgradeMaterialSlot
    static BaseUpgradeMaterialSlot MakeMaterialSlot(Transform parent, Vector2 pos)
    {
        var slot = MakeImage("MatSlot", parent, new Vector2(80, 92), pos, RGBA(255, 255, 255, 0.06f));
        var srt = slot.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0); srt.pivot = new Vector2(0.5f, 0);
        srt.anchoredPosition = pos;
        var img = slot.GetComponent<Image>();
        img.sprite = UISpriteFactory.RoundedRect(48, 12); img.type = Image.Type.Sliced;
        img.raycastTarget = true;   // 호버 받기

        var comp = slot.AddComponent<BaseUpgradeMaterialSlot>();

        var iconGo = MakeImage("Icon", slot.transform, new Vector2(48, 48), new Vector2(0, 16), Color.white);
        var iimg = iconGo.GetComponent<Image>();
        iimg.raycastTarget = false; iimg.preserveAspect = true;

        var count = MakeTMP("Count", slot.transform, new Vector2(76, 22), new Vector2(0, -30), "0/0", 14, Hex("DCE6F2"), TextAlignmentOptions.Center);

        var cso = new SerializedObject(comp);
        var pi = cso.FindProperty("icon");      if (pi != null) pi.objectReferenceValue = iimg;
        var pc = cso.FindProperty("countText"); if (pc != null) pc.objectReferenceValue = count;
        cso.ApplyModifiedProperties();

        slot.SetActive(false);   // 선택 전엔 비어있음 (컨트롤러가 Set/Clear)
        return comp;
    }

    // ── 헬퍼 (CoreUpgradeUIBuilder 와 동일 계열) ──────────────────────

    static void SetRefArray(SerializedObject so, string field, Object[] objs)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[BaseUpgradeUIBuilder] 필드 없음: '{field}'"); return; }
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
        else Debug.LogWarning($"[BaseUpgradeUIBuilder] 필드 없음: '{field}' — BaseUpgradeUI.cs 확인");
    }

    static Color Hex(string hex, int alpha = 255)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) { c.a = alpha / 255f; return c; }
        return Color.white;
    }

    static Color RGBA(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
}
