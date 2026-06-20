// SettingsPanelRebuilder.cs
// Tools/UI/Rebuild Settings Panel
// 기존 설정창(Canvas/Panels/SettingsPanel/Option/BG/Settings)의 평평하게 나열된 임시 row들을
// 정리하고, 엔드필드 스타일(상단 아이콘 탭바 + 탭별 스크롤 콘텐츠)로 재구성한다.
// 탭 아이콘은 기존 SettingsPanel_Icon_*.png를 임시로 재사용 — 추후 교체 예정.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SettingsPanelRebuilder
{
    private const string MenuPath = "Tools/UI/Rebuild Settings Panel";
    private const string IconDir  = "Assets/Resources/Image/UI_Icon/Setting/";

    private static Sprite Load(string fileName) => AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + fileName);

    [MenuItem(MenuPath)]
    static void Rebuild()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[SettingsRebuilder] 'Canvas'를 찾을 수 없습니다."); return; }

        Transform settingsPanelT = canvasGO.transform.Find("Panels/SettingsPanel");
        if (settingsPanelT == null) { Debug.LogError("[SettingsRebuilder] 'Panels/SettingsPanel'을 찾을 수 없습니다."); return; }

        var settingsMgr = settingsPanelT.GetComponent<GlobalSettingsManager>();
        if (settingsMgr == null) { Debug.LogError("[SettingsRebuilder] GlobalSettingsManager 컴포넌트가 없습니다."); return; }

        Transform settingsBG = settingsPanelT.Find("Option/BG/Settings");
        if (settingsBG == null) { Debug.LogError("[SettingsRebuilder] 'Option/BG/Settings'를 찾을 수 없습니다."); return; }

        Undo.SetCurrentGroupName("Rebuild Settings Panel");
        int undoGroup = Undo.GetCurrentGroup();

        // 설정창은 평소 비활성(닫힌 상태)이라 Slider 등 활성 상태에서만 갱신되는 비주얼
        // (Fill/Handle 위치)이 빌드 시점에 갱신되지 않는다. 작업 중엔 강제로 활성화해두고 끝나면 복원.
        var activeChain = new List<(GameObject go, bool wasActive)>();
        for (var t = settingsBG; t != null; t = t.parent)
        {
            activeChain.Add((t.gameObject, t.gameObject.activeSelf));
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        }

        // ── 1. 기존 자식 중 살릴 것만 보존, 나머지 전부 삭제 ──────────────────
        // Btn_MainMenu는 더 이상 쓰지 않음 — 사용자가 풋터에서 제거 요청
        Transform title = settingsBG.Find("Title");
        Transform titleEn = settingsBG.Find("Title_en");

        var toDelete = new List<GameObject>();
        for (int i = 0; i < settingsBG.childCount; i++)
        {
            var child = settingsBG.GetChild(i).gameObject;
            if (child == title?.gameObject || child == titleEn?.gameObject)
                continue;
            toDelete.Add(child);
        }
        foreach (var go in toDelete) Undo.DestroyObjectImmediate(go);

        // ── 1b. 패널 전체화면화 + 배경 어둡게(거의 불투명) ────────────────────
        var bgRect = settingsBG.GetComponent<RectTransform>();
        Undo.RecordObject(bgRect, "Resize Settings Panel Fullscreen");
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImage = settingsBG.GetComponent<Image>();
        if (bgImage != null)
        {
            Undo.RecordObject(bgImage, "Darken Settings Panel Background");
            bgImage.sprite = null; // 작은 텍스처를 전체화면으로 늘리면 흐려지므로 단색으로 대체
            bgImage.color = new Color(0.04f, 0.05f, 0.07f, 0.96f);
        }

        // Title / Title_en 좌상단으로 재배치 (전체화면 기준 절대 위치)
        if (title != null)
        {
            var tr = title.GetComponent<RectTransform>();
            Undo.RecordObject(tr, "Reposition Title");
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot     = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(60f, -50f);
        }
        if (titleEn != null)
        {
            var tr = titleEn.GetComponent<RectTransform>();
            Undo.RecordObject(tr, "Reposition TitleEn");
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot     = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(170f, -50f);
        }

        // ── 2. 탭바 ──────────────────────────────────────────────────────────
        var tabBar = CreateUIObject("TabBar", settingsBG);
        var tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0.5f, 1f);
        tabBarRect.anchorMax = new Vector2(0.5f, 1f);
        tabBarRect.pivot     = new Vector2(0.5f, 1f);
        tabBarRect.anchoredPosition = new Vector2(0f, -130f);
        tabBarRect.sizeDelta = new Vector2(900f, 80f);
        var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.spacing = 30f;
        tabHlg.childControlWidth  = true;  // false였던 게 버그 — LayoutElement 크기가 무시됨
        tabHlg.childControlHeight = true;
        tabHlg.childForceExpandWidth  = false;
        tabHlg.childForceExpandHeight = false;
        Undo.RegisterCreatedObjectUndo(tabBar, "Create TabBar");

        var tabNames  = new[] { "그래픽", "오디오", "조작" };
        var tabIcons  = new[] { Load("SettingsPanel_Icon_Fullscreen.png"), Load("SettingsPanel_Icon_BGM.png"), Load("SettingsPanel_Icon_Mouse.png") };
        var tabButtons = new Button[3];
        var tabHighlights = new GameObject[3];

        for (int i = 0; i < 3; i++)
        {
            var (btn, highlight) = CreateTabButton(tabBar.transform, tabNames[i], tabIcons[i]);
            tabButtons[i] = btn;
            tabHighlights[i] = highlight;
        }

        // ── 3. 탭별 스크롤 콘텐츠 ─────────────────────────────────────────────
        var tabContents = new GameObject[3];

        // 그래픽
        var (graphicsRoot, graphicsContent) = CreateScrollTab(settingsBG, "GraphicsTab");
        CreateSectionHeader(graphicsContent, "성능 및 화면");
        TMP_Dropdown qualityDropdown        = CreateDropdownRow(graphicsContent, "화면 품질");
        var (fullscreenOnBg, fullscreenOffBg, fullscreenOnLabel, fullscreenOffLabel) = CreateSegmentedRow(graphicsContent, "표시 모드", "전체 화면", "창 모드",
            settingsMgr.SetFullscreenOn, settingsMgr.SetFullscreenOff);
        TMP_Dropdown resolutionDropdown      = CreateDropdownRow(graphicsContent, "해상도");
        TMP_Dropdown shadowQualityDropdown   = CreateDropdownRow(graphicsContent, "그림자 품질");
        TMP_Dropdown textureQualityDropdown  = CreateDropdownRow(graphicsContent, "텍스처 품질");
        tabContents[0] = graphicsRoot;

        // 오디오
        var (audioRoot, audioContent) = CreateScrollTab(settingsBG, "AudioTab");
        CreateSectionHeader(audioContent, "오디오 설정");
        Slider masterSlider = CreateSliderRow(audioContent, "마스터 볼륨", null);
        Slider bgmSlider = CreateSliderRow(audioContent, "배경음(BGM)", Load("SettingsPanel_Icon_BGM.png"));
        Slider sfxSlider = CreateSliderRow(audioContent, "효과음(SFX)", Load("SettingsPanel_Icon_SFX.png"));
        tabContents[1] = audioRoot;

        // 조작
        var (controlsRoot, controlsContent) = CreateScrollTab(settingsBG, "ControlsTab");
        CreateSectionHeader(controlsContent, "조작 설정");
        Slider sensitivitySlider = CreateSliderRow(controlsContent, "마우스 감도", Load("SettingsPanel_Icon_Mouse.png"));
        var rebindSlots = new List<GlobalSettingsManager.RebindSlot>
        {
            CreateRebindRow(controlsContent, "기본 공격", "Attack"),
            CreateRebindRow(controlsContent, "대시",     "Dash"),
            CreateRebindRow(controlsContent, "점프",     "Jump"),
            CreateRebindRow(controlsContent, "스킬 1",   "Skill1"),
            CreateRebindRow(controlsContent, "스킬 2",   "Skill2"),
            CreateRebindRow(controlsContent, "스킬 3",   "Skill3"),
            CreateRebindRow(controlsContent, "상호작용", "Interact"),
            CreateRebindRow(controlsContent, "즉시완료", "Instant"),
            CreateRebindRow(controlsContent, "퀵슬롯",   "QuickSlot"),
            CreateRebindRow(controlsContent, "인벤토리", "Inventory"),
            CreateRebindRow(controlsContent, "스탯창",   "Stat"),
            CreateRebindRow(controlsContent, "도감",     "Codex"),
        };
        tabContents[2] = controlsRoot;

        // 첫 탭만 보이게
        graphicsRoot.SetActive(true);
        audioRoot.SetActive(false);
        controlsRoot.SetActive(false);

        // ── 4. 닫기 버튼 (우상단) ────────────────────────────────────────────
        var closeBtn = CreateCloseButton(settingsBG, settingsMgr);

        // ── 5. 하단 고정 풋터 — 안내 문구(좌) + 설정 초기화/설정 적용(우) — 스크롤 영향 없음 ──
        CreateFooter(settingsBG, settingsMgr);

        // ── 5b. 키 리바인딩 입력 모달 (항상 최상단, 기본 비활성) ───────────────
        var (rebindModal, rebindActionLabel, rebindKeyDisplay) = CreateRebindModal(settingsBG);

        // ── 6. GlobalSettingsManager 필드 연결 ───────────────────────────────
        Undo.RecordObject(settingsMgr, "Wire GlobalSettingsManager Fields");
        settingsMgr.resolutionDropdown      = resolutionDropdown;
        settingsMgr.fullscreenOnBg          = fullscreenOnBg;
        settingsMgr.fullscreenOffBg         = fullscreenOffBg;
        settingsMgr.fullscreenOnLabel       = fullscreenOnLabel;
        settingsMgr.fullscreenOffLabel      = fullscreenOffLabel;
        settingsMgr.qualityDropdown         = qualityDropdown;
        settingsMgr.shadowQualityDropdown   = shadowQualityDropdown;
        settingsMgr.textureQualityDropdown  = textureQualityDropdown;
        settingsMgr.masterSlider            = masterSlider;
        settingsMgr.bgmSlider               = bgmSlider;
        settingsMgr.sfxSlider               = sfxSlider;
        settingsMgr.sensitivitySlider       = sensitivitySlider;
        settingsMgr.tabButtons              = tabButtons;
        settingsMgr.tabContents             = tabContents;
        settingsMgr.tabHighlights           = tabHighlights;
        settingsMgr.rebindSlots             = rebindSlots;
        settingsMgr.rebindModal             = rebindModal;
        settingsMgr.rebindModalActionLabel  = rebindActionLabel;
        settingsMgr.rebindModalKeyDisplay   = rebindKeyDisplay;
        EditorUtility.SetDirty(settingsMgr);

        // 임시로 활성화했던 체인 복원 (자식 → 부모 순서로 처리했으니 그대로 되돌림)
        foreach (var (go, wasActive) in activeChain)
            if (go.activeSelf != wasActive) go.SetActive(wasActive);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(settingsBG.gameObject.scene);

        Debug.Log("[SettingsRebuilder] 설정창 재구성 완료 — 그래픽/오디오/조작 탭 3개, 컨트롤 " +
                  "필드 GlobalSettingsManager에 연결됨. 인스펙터에서 확인하세요.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    // ── 공통 헬퍼 ────────────────────────────────────────────────────────────

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateUIElementViaMenu(string menuPath, Transform parent)
    {
        var prevSelection = Selection.activeGameObject;
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem(menuPath);
        GameObject created = Selection.activeGameObject;
        // ExecuteMenuItem이 Selection을 무시하고 씬의 다른 Canvas 밑에 생성하는 경우가 있어
        // 항상 의도한 parent로 강제 재배치한다.
        if (created != null && parent != null && created.transform.parent != parent)
            created.transform.SetParent(parent, false);
        Selection.activeGameObject = prevSelection;
        return created;
    }

    private static (Button, GameObject) CreateTabButton(Transform parent, string label, Sprite icon)
    {
        var btnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", parent);
        btnGO.name = "Btn_Tab_" + label;
        var rt = btnGO.GetComponent<RectTransform>();
        var le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth  = 300f;
        le.preferredHeight = 80f;

        var img = btnGO.GetComponent<Image>();
        if (img != null) img.color = new Color(0.14f, 0.16f, 0.20f, 0.9f);

        var tmp = btnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
        }

        // 임시 아이콘 (재사용 아트, 추후 교체 예정)
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(btnGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot     = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(14f, 0f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
        }

        // 선택 강조 밑줄 (노란선)
        var highlight = new GameObject("Highlight", typeof(RectTransform));
        highlight.transform.SetParent(btnGO.transform, false);
        var hImg = highlight.AddComponent<Image>();
        hImg.color = new Color(1f, 0.82f, 0.1f, 1f);
        var hRect = highlight.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 0f);
        hRect.anchorMax = new Vector2(1f, 0f);
        hRect.pivot     = new Vector2(0.5f, 0f);
        hRect.anchoredPosition = new Vector2(0f, 0f);
        hRect.sizeDelta = new Vector2(0f, 4f);
        highlight.SetActive(false);

        return (btnGO.GetComponent<Button>(), highlight);
    }

    private static (GameObject root, Transform content) CreateScrollTab(Transform parent, string name)
    {
        var scrollGO = CreateUIElementViaMenu("GameObject/UI/Scroll View", parent);
        scrollGO.name = name;
        var rect = scrollGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(90f, 110f);   // 좌, 하 여백
        rect.offsetMax = new Vector2(-90f, -220f); // 우, 상(탭바 아래부터) 여백

        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var rootImg = scrollGO.GetComponent<Image>();
        if (rootImg == null) rootImg = scrollGO.AddComponent<Image>();
        rootImg.sprite = null;
        rootImg.color = PanelBg;

        var viewport = scrollGO.transform.Find("Viewport");
        var content  = viewport != null ? viewport.Find("Content") : null;
        if (content == null)
        {
            Debug.LogWarning("[SettingsRebuilder] ScrollView 기본 구조를 찾지 못함: " + name);
            return (scrollGO, scrollGO.transform);
        }

        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return (scrollGO, content);
    }

    private static GameObject CreateRow(Transform content, string label, out Transform controlSlot)
    {
        var row = CreateUIObject(label + "_Row", content);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(28, 28, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 24f;
        hlg.childControlWidth  = true;  // false였던 게 버그 — LayoutElement 너비가 무시되어 컨트롤이 찌그러짐
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 64f;
        rowLE.preferredHeight = 64f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26f;
        tmp.color = new Color(0.9f, 0.93f, 0.96f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 380f;
        labelLE.minWidth = 380f;
        labelLE.flexibleWidth = 0f;

        var slot = new GameObject("Control", typeof(RectTransform));
        slot.transform.SetParent(row.transform, false);
        var slotLE = slot.AddComponent<LayoutElement>();
        slotLE.minWidth = 200f;
        slotLE.flexibleWidth = 1f;

        controlSlot = slot.transform;
        return row;
    }

    // 컨트롤을 슬롯 "오른쪽 끝"에 고정폭으로 붙인다 — 라벨과 컨트롤 사이는 비워둠(엔드필드 스타일)
    private static void FillSlot(GameObject element, Transform slot, float width, float height)
    {
        element.transform.SetParent(slot, false);
        var rect = element.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot     = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    private const float ControlWidth = 480f; // 드롭다운/세그먼트 컨트롤 표준 폭
    private const float ControlHeight = 56f; // 드롭다운/세그먼트 컨트롤 표준 높이

    // 엔드필드 팔레트 — 다크 네이비 + 노란 액센트
    private static readonly Color PanelBg       = new Color(0.063f, 0.078f, 0.102f, 1f);
    private static readonly Color ControlBg     = new Color(0.106f, 0.125f, 0.153f, 0.95f);
    private static readonly Color AccentYellow  = new Color(1f, 0.82f, 0.10f, 1f);
    private static readonly Color TextPrimary   = new Color(0.929f, 0.937f, 0.949f, 1f);
    private static readonly Color TextMuted     = new Color(0.604f, 0.631f, 0.671f, 1f);
    private static readonly Color SliderLineGray = new Color(0.29f, 0.30f, 0.32f, 1f); // 얇은 트랙 라인 색

    private static TMP_Dropdown CreateDropdownRow(Transform content, string label)
    {
        CreateRow(content, label, out var slot);
        var ddGO = CreateUIElementViaMenu("GameObject/UI/Dropdown - TextMeshPro", slot);
        FillSlot(ddGO, slot, ControlWidth, ControlHeight);
        var dd = ddGO.GetComponent<TMP_Dropdown>();
        StyleDropdown(dd);
        return dd;
    }

    // 드롭다운 박스 + 펼침 목록을 다크 네이비 팔레트로 재도색
    private static void StyleDropdown(TMP_Dropdown dd)
    {
        var rootImg = dd.GetComponent<Image>();
        if (rootImg != null) rootImg.color = ControlBg;

        var label = dd.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null) { label.color = TextPrimary; label.fontSize = 24f; }

        var arrowRt = dd.transform.Find("Arrow")?.GetComponent<RectTransform>();
        if (arrowRt != null) arrowRt.sizeDelta = new Vector2(24f, 24f);
        var arrow = dd.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow != null) arrow.color = TextMuted;

        var template = dd.transform.Find("Template");
        if (template == null) return;
        var templateImg = template.GetComponent<Image>();
        if (templateImg != null) templateImg.color = ControlBg;

        var itemBg = template.Find("Viewport/Content/Item/Item Background")?.GetComponent<Image>();
        if (itemBg != null) itemBg.color = PanelBg;

        var itemLabel = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<TextMeshProUGUI>();
        if (itemLabel != null) { itemLabel.color = TextPrimary; itemLabel.fontSize = 24f; }

        // 항목 행 높이가 기본값(20)에 묶여 있어 24pt 폰트가 한 줄을 다 못 채우고
        // 다음 행과 겹쳐 보임 — 행 높이를 키워 글자가 자연스럽게 들어가게 한다.
        // (TMP_Dropdown은 표시 시점에 이 Item의 sizeDelta.y를 행 간격으로 그대로 사용한다.)
        var item = template.Find("Viewport/Content/Item") as RectTransform;
        if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, 44f);

        var checkmark = template.Find("Viewport/Content/Item/Item Checkmark") as RectTransform;
        if (checkmark != null) checkmark.sizeDelta = new Vector2(22f, 22f);
    }

    // 엔드필드 스타일 섹션 헤더: ■ 노란 막대 + 굵은 노란 글씨 + 우측으로 뻗는 얇은 선
    private static void CreateSectionHeader(Transform content, string text)
    {
        var header = CreateUIObject(text + "_Header", content);
        var hlg = header.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 12f;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false; // true(기본값)면 flexibleWidth=0인 자식도 강제로 늘어남
        hlg.childForceExpandHeight = false;
        var le = header.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(header.transform, false);
        var barImg = bar.AddComponent<Image>();
        barImg.color = new Color(1f, 0.82f, 0.1f, 1f);
        var barLE = bar.AddComponent<LayoutElement>();
        barLE.preferredWidth = 6f; barLE.minWidth = 6f; barLE.flexibleWidth = 0f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(header.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.82f, 0.1f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 220f; labelLE.minWidth = 220f; labelLE.flexibleWidth = 0f;

        var line = new GameObject("Line", typeof(RectTransform));
        line.transform.SetParent(header.transform, false);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.15f);
        var lineLE = line.AddComponent<LayoutElement>();
        lineLE.flexibleWidth = 1f; lineLE.minHeight = 2f; lineLE.preferredHeight = 2f;
    }

    // 표시 모드 같은 2지선다 행: [전체 화면 | 창 모드] 버튼 한 쌍, 선택된 쪽만 노란색
    private static (Image onBg, Image offBg, TMP_Text onLabel, TMP_Text offLabel) CreateSegmentedRow(Transform content, string label,
        string onText, string offText, UnityEngine.Events.UnityAction onClickOn, UnityEngine.Events.UnityAction onClickOff)
    {
        CreateRow(content, label, out var slot);

        var segGO = new GameObject("Segments", typeof(RectTransform));
        segGO.transform.SetParent(slot, false);
        var segRect = segGO.GetComponent<RectTransform>();
        segRect.anchorMin = new Vector2(1f, 0.5f);
        segRect.anchorMax = new Vector2(1f, 0.5f);
        segRect.pivot     = new Vector2(1f, 0.5f);
        segRect.anchoredPosition = Vector2.zero;
        segRect.sizeDelta = new Vector2(ControlWidth, ControlHeight);
        var segHlg = segGO.AddComponent<HorizontalLayoutGroup>();
        segHlg.childControlWidth = true;
        segHlg.childControlHeight = true;
        segHlg.childForceExpandWidth  = false;
        segHlg.childForceExpandHeight = false;
        segHlg.spacing = 4f;

        var onBtnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", segGO.transform);
        onBtnGO.name = "Btn_On";
        var onLE = onBtnGO.AddComponent<LayoutElement>();
        onLE.flexibleWidth = 1f; onLE.preferredHeight = ControlHeight; onLE.minHeight = ControlHeight;
        var onTmp = onBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (onTmp != null) { onTmp.text = onText; onTmp.fontSize = 24f; }
        var onImg = onBtnGO.GetComponent<Image>();
        if (onImg != null) onImg.color = ControlBg;
        var onBtn = onBtnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(onBtn.onClick, onClickOn);

        var offBtnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", segGO.transform);
        offBtnGO.name = "Btn_Off";
        var offLE = offBtnGO.AddComponent<LayoutElement>();
        offLE.flexibleWidth = 1f; offLE.preferredHeight = ControlHeight; offLE.minHeight = ControlHeight;
        var offTmp = offBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (offTmp != null) { offTmp.text = offText; offTmp.fontSize = 24f; }
        var offImg = offBtnGO.GetComponent<Image>();
        if (offImg != null) offImg.color = ControlBg;
        var offBtn = offBtnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(offBtn.onClick, onClickOff);

        return (onImg, offImg, onTmp, offTmp);
    }

    // 하단 고정 풋터: 좌측 안내 문구 + 우측 설정 초기화/설정 적용 알약 버튼 — 탭 스크롤과 무관하게 항상 보임
    private static void CreateFooter(Transform parent, GlobalSettingsManager mgr)
    {
        const float btnWidth = 260f, btnHeight = 64f, spacing = 20f, y = 50f, edgePad = 40f;

        // 좌측 안내 문구 (레퍼런스의 "변경하고자 하는 키를 눌러서 선택해 주세요." 위치)
        var hintGO = new GameObject("Hint", typeof(RectTransform));
        hintGO.transform.SetParent(parent, false);
        var hintRt = hintGO.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot     = new Vector2(0f, 0f);
        hintRt.anchoredPosition = new Vector2(edgePad, y);
        hintRt.sizeDelta = new Vector2(700f, btnHeight);
        var hintTmp = hintGO.AddComponent<TextMeshProUGUI>();
        hintTmp.text = "변경하고자 하는 키를 눌러서 선택해 주세요.";
        hintTmp.fontSize = 20f;
        hintTmp.color = TextMuted;
        hintTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // 우측 알약 버튼 2개 (안쪽이 적용, 바깥쪽이 초기화)
        CreateFooterButton(parent, "Btn_Apply", "설정 적용", "✓",
            -edgePad, y, btnWidth, btnHeight, mgr.ApplySettings);
        CreateFooterButton(parent, "Btn_Reset", "설정 초기화", "↻",
            -edgePad - btnWidth - spacing, y, btnWidth, btnHeight, mgr.ResetGraphicsToDefault);
    }

    private static readonly Color PillBg       = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly Color PillText     = new Color(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Color PillCircleBg = new Color(0.16f, 0.17f, 0.19f, 0.95f);

    // 알약형 버튼: 가운데 텍스트 + 오른쪽 안쪽에 들어간 원형 아이콘(rightGlyph로 버튼마다
    // 구분: 새로고침/체크 등). 좌측 아이콘은 제거 — 흰 버튼 밖으로 삐져나오지 않게 전부 안쪽에 배치.
    private static void CreateFooterButton(Transform parent, string name, string text, string rightGlyph,
        float x, float y, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = CreateUIObject(name, parent);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, y);

        var pillImg = btnGO.AddComponent<Image>();
        pillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        pillImg.type = Image.Type.Sliced;
        pillImg.color = PillBg;

        var btn = btnGO.AddComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, onClick);

        float circleSize = height * 0.7f;
        const float circlePad = 8f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = PillText;
        tmp.alignment = TextAlignmentOptions.Center;
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = new Vector2(-(circleSize + circlePad * 2f), 0f);

        // 오른쪽 원형 아이콘 — 버튼 안쪽에 완전히 들어가도록 중심을 안으로 당김(삐져나오지 않음)
        CreateGlyphCircle(btnGO.transform, rightGlyph, new Vector2(1f, 0.5f),
            new Vector2(-(circleSize * 0.5f + circlePad), 0f), circleSize);
    }

    private static void CreateGlyphCircle(Transform parent, string glyph, Vector2 anchor, Vector2 anchoredPos, float size)
    {
        var circleGO = new GameObject("Circle", typeof(RectTransform));
        circleGO.transform.SetParent(parent, false);
        var rt = circleGO.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var img = circleGO.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = PillCircleBg;

        var glyphGO = new GameObject("Glyph", typeof(RectTransform));
        glyphGO.transform.SetParent(circleGO.transform, false);
        var grt = glyphGO.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = new Vector2(3f, 3f);
        grt.offsetMax = new Vector2(-3f, -3f);
        var gtmp = glyphGO.AddComponent<TextMeshProUGUI>();
        gtmp.text = glyph;
        gtmp.enableAutoSizing = true;
        gtmp.fontSizeMin = 10f;
        gtmp.fontSizeMax = size * 0.75f;
        gtmp.fontStyle = FontStyles.Bold;
        gtmp.color = TextPrimary;
        gtmp.alignment = TextAlignmentOptions.Center;
    }


    private const float SliderWidth = 480f; // 슬라이더 트랙 표준 폭(아이콘 제외)

    private const float ValueLabelWidth = 34f;

    // OS 볼륨 슬라이더 스타일: 얇은 회색 라인 트랙 + 흰 캡슐형 핸들 + 우측 숫자 값(0-10)
    private static Slider CreateSliderRow(Transform content, string label, Sprite icon)
    {
        CreateRow(content, label, out var slot);

        // 아이콘 + 슬라이더 + 숫자 라벨을 한 묶음으로 묶어 슬롯 "오른쪽 끝"에 고정폭으로 붙인다
        var groupGO = new GameObject("SliderGroup", typeof(RectTransform));
        groupGO.transform.SetParent(slot, false);
        var groupRect = groupGO.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(1f, 0.5f);
        groupRect.anchorMax = new Vector2(1f, 0.5f);
        groupRect.pivot     = new Vector2(1f, 0.5f);
        groupRect.anchoredPosition = Vector2.zero;
        float groupWidth = SliderWidth + ValueLabelWidth + 12f + (icon != null ? 60f : 0f);
        groupRect.sizeDelta = new Vector2(groupWidth, 40f);
        var groupHlg = groupGO.AddComponent<HorizontalLayoutGroup>();
        groupHlg.childAlignment = TextAnchor.MiddleLeft;
        groupHlg.spacing = 12f;
        groupHlg.childControlWidth  = true;
        groupHlg.childControlHeight = true;
        groupHlg.childForceExpandWidth  = false;
        groupHlg.childForceExpandHeight = false;

        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(groupGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 48f; iconLE.minWidth = 48f; iconLE.flexibleWidth = 0f;
            iconLE.preferredHeight = 48f;
        }

        var sliderGO = CreateUIElementViaMenu("GameObject/UI/Slider", groupGO.transform);
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.flexibleWidth = 1f;
        sliderLE.preferredHeight = 40f; sliderLE.minHeight = 40f;
        var rect = sliderGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // 트랙: 얇은 회색 라인 (sprite=null, 단색만 사용)
        const float trackThickness = 3f;
        var bgRt = sliderGO.transform.Find("Background")?.GetComponent<RectTransform>();
        if (bgRt != null)
        {
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, trackThickness);
        }
        var bgImg = bgRt != null ? bgRt.GetComponent<Image>() : null;
        if (bgImg != null) { bgImg.sprite = null; bgImg.color = SliderLineGray; }

        // 필 영역: 트랙과 같은 색으로 채워서 "끊긴 라인" 없이 하나의 얇은 선처럼 보이게 함
        var fillAreaRt = sliderGO.transform.Find("Fill Area")?.GetComponent<RectTransform>();
        if (fillAreaRt != null)
        {
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.sizeDelta = new Vector2(fillAreaRt.sizeDelta.x, trackThickness);
        }
        var fillImg = sliderGO.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fillImg != null) { fillImg.sprite = null; fillImg.color = SliderLineGray; }

        // 핸들: 작은 원형 대신 가로로 긴 흰색 캡슐(둥근 모서리 스프라이트를 늘려서 표현)
        var handleRt = sliderGO.transform.Find("Handle Slide Area/Handle")?.GetComponent<RectTransform>();
        if (handleRt != null) handleRt.sizeDelta = new Vector2(16f, 9f);
        var handleImg = handleRt != null ? handleRt.GetComponent<Image>() : null;
        if (handleImg != null)
        {
            handleImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            handleImg.type = Image.Type.Sliced;
            handleImg.color = TextPrimary;
        }

        // 우측 숫자 값 라벨 (0~10 스케일로 표시)
        var valueGO = new GameObject("Value", typeof(RectTransform));
        valueGO.transform.SetParent(groupGO.transform, false);
        var valueTmp = valueGO.AddComponent<TextMeshProUGUI>();
        valueTmp.text = "10";
        valueTmp.fontSize = 20f;
        valueTmp.color = TextMuted;
        valueTmp.alignment = TextAlignmentOptions.MidlineRight;
        var valueLE = valueGO.AddComponent<LayoutElement>();
        valueLE.preferredWidth = ValueLabelWidth; valueLE.minWidth = ValueLabelWidth; valueLE.flexibleWidth = 0f;
        slider.onValueChanged.AddListener(v => valueTmp.text = Mathf.RoundToInt(v * 10f).ToString());

        return slider;
    }

    // 키 바인딩 칩: 다크 네이비 박스 + 얇은 노란 외곽선(Outline 컴포넌트로 테두리 흉내) + 볼드 키 라벨
    private static GlobalSettingsManager.RebindSlot CreateRebindRow(Transform content, string label, string actionId)
    {
        CreateRow(content, label, out var slot);

        var btnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", slot);
        FillSlot(btnGO, slot, 140f, 48f);

        var img = btnGO.GetComponent<Image>();
        if (img != null) { img.sprite = null; img.color = ControlBg; }

        var outline = btnGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = AccentYellow;
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;

        var tmp = btnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = "Space";
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = TextPrimary;
        }

        return new GlobalSettingsManager.RebindSlot
        {
            actionId    = actionId,
            displayName = label,
            button      = btnGO.GetComponent<Button>(),
            keyLabel    = tmp
        };
    }

    private static Button CreateCloseButton(Transform parent, GlobalSettingsManager mgr)
    {
        var btnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", parent);
        btnGO.name = "Btn_Close";
        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-45f, -45f);
        rect.sizeDelta = new Vector2(70f, 70f);

        var img = btnGO.GetComponent<Image>();
        var closeBg = Load("SettingsPanel_Button_Close_BG.png");
        if (img != null && closeBg != null) img.sprite = closeBg;

        var tmp = btnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = "X"; tmp.fontSize = 28f; }

        var btn = btnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, mgr.CloseSettings);
        return btn;
    }

    // 키 리바인딩 중 표시되는 중앙 모달: 안내 문구 + 동작 이름 + 키 입력 박스 + ESC 취소 안내.
    // 기본 비활성, GlobalSettingsManager.BeginRebind/CancelRebind/CompleteRebind에서 토글.
    private static (GameObject modal, TMP_Text actionLabel, TMP_Text keyDisplay) CreateRebindModal(Transform parent)
    {
        var modal = CreateUIObject("RebindModal", parent);
        var modalRect = modal.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.offsetMin = Vector2.zero;
        modalRect.offsetMax = Vector2.zero;
        var backdrop = modal.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(modal.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot     = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(640f, 300f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        panelImg.color = PillBg;

        var title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(panel.transform, false);
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "변경하고자 하는 키를 입력해 주세요.";
        titleTmp.fontSize = 26f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = PillText;
        titleTmp.alignment = TextAlignmentOptions.Center;
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(0f, 40f);

        var actionLabelGO = new GameObject("ActionLabel", typeof(RectTransform));
        actionLabelGO.transform.SetParent(panel.transform, false);
        var actionTmp = actionLabelGO.AddComponent<TextMeshProUGUI>();
        actionTmp.text = "";
        actionTmp.fontSize = 20f;
        actionTmp.color = TextMuted;
        actionTmp.alignment = TextAlignmentOptions.Center;
        var actionRect = actionLabelGO.GetComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(0f, 1f);
        actionRect.anchorMax = new Vector2(1f, 1f);
        actionRect.pivot     = new Vector2(0.5f, 1f);
        actionRect.anchoredPosition = new Vector2(0f, -82f);
        actionRect.sizeDelta = new Vector2(0f, 30f);

        var keyBox = new GameObject("KeyBox", typeof(RectTransform));
        keyBox.transform.SetParent(panel.transform, false);
        var keyBoxRect = keyBox.GetComponent<RectTransform>();
        keyBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        keyBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        keyBoxRect.pivot     = new Vector2(0.5f, 0.5f);
        keyBoxRect.anchoredPosition = new Vector2(0f, -8f);
        keyBoxRect.sizeDelta = new Vector2(280f, 64f);
        var keyBoxImg = keyBox.AddComponent<Image>();
        keyBoxImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        keyBoxImg.type = Image.Type.Sliced;
        keyBoxImg.color = SliderLineGray;

        var keyDisplayGO = new GameObject("KeyDisplay", typeof(RectTransform));
        keyDisplayGO.transform.SetParent(keyBox.transform, false);
        var keyTmp = keyDisplayGO.AddComponent<TextMeshProUGUI>();
        keyTmp.text = "";
        keyTmp.fontSize = 24f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.color = PillText;
        keyTmp.alignment = TextAlignmentOptions.Center;
        var keyDisplayRect = keyDisplayGO.GetComponent<RectTransform>();
        keyDisplayRect.anchorMin = Vector2.zero;
        keyDisplayRect.anchorMax = Vector2.one;
        keyDisplayRect.offsetMin = Vector2.zero;
        keyDisplayRect.offsetMax = Vector2.zero;

        // ESC 취소 안내 (작은 키 배지 + 텍스트)
        var cancelRow = new GameObject("CancelRow", typeof(RectTransform));
        cancelRow.transform.SetParent(panel.transform, false);
        var cancelRowRect = cancelRow.GetComponent<RectTransform>();
        cancelRowRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRowRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRowRect.pivot     = new Vector2(0.5f, 0f);
        cancelRowRect.anchoredPosition = new Vector2(0f, 30f);
        cancelRowRect.sizeDelta = new Vector2(160f, 32f);
        var cancelHlg = cancelRow.AddComponent<HorizontalLayoutGroup>();
        cancelHlg.childAlignment = TextAnchor.MiddleCenter;
        cancelHlg.spacing = 8f;
        cancelHlg.childControlWidth  = true;
        cancelHlg.childControlHeight = true;
        cancelHlg.childForceExpandWidth  = false;
        cancelHlg.childForceExpandHeight = false;

        var escBadge = new GameObject("EscBadge", typeof(RectTransform));
        escBadge.transform.SetParent(cancelRow.transform, false);
        var escImg = escBadge.AddComponent<Image>();
        escImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        escImg.type = Image.Type.Sliced;
        escImg.color = ControlBg;
        var escLE = escBadge.AddComponent<LayoutElement>();
        escLE.preferredWidth = 48f; escLE.minWidth = 48f; escLE.flexibleWidth = 0f;
        escLE.preferredHeight = 28f; escLE.minHeight = 28f;
        var escTextGO = new GameObject("Text", typeof(RectTransform));
        escTextGO.transform.SetParent(escBadge.transform, false);
        var escTmp = escTextGO.AddComponent<TextMeshProUGUI>();
        escTmp.text = "ESC";
        escTmp.fontSize = 14f;
        escTmp.color = TextPrimary;
        escTmp.alignment = TextAlignmentOptions.Center;
        var escTextRect = escTextGO.GetComponent<RectTransform>();
        escTextRect.anchorMin = Vector2.zero; escTextRect.anchorMax = Vector2.one;
        escTextRect.offsetMin = Vector2.zero; escTextRect.offsetMax = Vector2.zero;

        var cancelLabelGO = new GameObject("Label", typeof(RectTransform));
        cancelLabelGO.transform.SetParent(cancelRow.transform, false);
        var cancelTmp = cancelLabelGO.AddComponent<TextMeshProUGUI>();
        cancelTmp.text = "설정 취소";
        cancelTmp.fontSize = 18f;
        cancelTmp.color = PillText;
        cancelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        var cancelLE = cancelLabelGO.AddComponent<LayoutElement>();
        cancelLE.preferredWidth = 90f; cancelLE.flexibleWidth = 0f;

        modal.SetActive(false);
        return (modal, actionTmp, keyTmp);
    }
}
#endif
