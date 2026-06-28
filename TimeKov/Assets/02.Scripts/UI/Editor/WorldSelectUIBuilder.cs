// =====================================================================
// WorldSelectUIBuilder.cs  (Editor Only)
// Tools/UI/Build World Select UI
// 타이틀 화면(MainMenu_Cinematic) 위에 월드 선택 패널을 생성하고 레퍼런스를 연결한다.
// SettingsPanelRebuilder/ChestOpenUIBuilder와 동일한 컨벤션: 자체 완결된 정적 빌더 +
// 자체 MakeXxx 헬퍼(다른 빌더 스크립트와 공유하지 않음).
// =====================================================================

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorldSelectUIBuilder
{
    private const string MenuPath = "Tools/UI/Build World Select UI";
    private const string CanvasName = "MainMenu_Cinematic";
    private const string RowPrefabPath = "Assets/05.Prefabs/UI/WorldSelectRow.prefab";

    // 타이틀 화면 Btn_Quit의 하이라이트 색과 동일 계열의 골드 — SettingsPanelRebuilder.AccentColor와도 통일.
    private static readonly Color AccentColor = new Color(0.85f, 0.78f, 0.24f, 1f);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/남양주고딕Light (OTF) SDF.asset");

    [MenuItem(MenuPath)]
    static void Build()
    {
        var canvasGO = GameObject.Find(CanvasName);
        if (canvasGO == null)
        {
            Debug.LogError($"[WorldSelectUIBuilder] '{CanvasName}' 캔버스를 찾을 수 없습니다. MainMenu 씬에서 실행하세요.");
            return;
        }

        // 반복 실행해도 항상 깨끗하게 — SettingsPanelRebuilder와 동일하게 확인창 없이 바로 교체.
        var existing = canvasGO.transform.Find("WorldSelectPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.SetCurrentGroupName("Build World Select UI");
        int undoGroup = Undo.GetCurrentGroup();

        // ── 루트(전체화면 어둡게 깔기) ───────────────────────────────────
        GameObject root = MakeFullscreen("WorldSelectPanel", canvasGO.transform, Hex("050B12", 215));
        Undo.RegisterCreatedObjectUndo(root, "Create WorldSelectPanel");

        var ui = root.AddComponent<WorldSelectUI>();
        var so = new SerializedObject(ui);
        SetRef(so, "panelRoot", root);

        // ── 카드 ─────────────────────────────────────────────────────────
        GameObject card = MakeImage("Card", root.transform, new Vector2(900, 700), Vector2.zero, Hex("0E1B2A", 235));

        var title = MakeTMP("Title", card.transform, new Vector2(800, 56), new Vector2(0f, 306f),
            "월드 선택", 30f, AccentColor, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        ApplyFont(title);

        MakeImage("TitleLine", card.transform, new Vector2(820, 1f), new Vector2(0f, 272f), Hex("23394F", 220));

        // ── 슬롯 목록 (스크롤) ─────────────────────────────────────────
        Transform rowContainer = BuildScrollList(card.transform);
        SetRef(so, "rowContainer", rowContainer);

        MakeImage("BottomLine", card.transform, new Vector2(820, 1f), new Vector2(0f, -218f), Hex("23394F", 220));

        // ── 하단: 새 월드 이름 입력 + 생성 버튼 ──────────────────────────
        TMP_InputField nameInput = BuildNameInputField(card.transform);
        SetRef(so, "newWorldNameInput", nameInput);

        GameObject createBtn = MakeButton("Btn_CreateWorld", card.transform,
            new Vector2(220, 56), new Vector2(345f, -270f), "새 월드 만들기", 19f, AccentColor, Hex("1A1606"));
        SetRef(so, "newWorldButton", createBtn.GetComponent<Button>());

        GameObject backBtn = MakeButton("Btn_Back", card.transform,
            new Vector2(160, 48), new Vector2(0f, -330f), "취소", 18f, Hex("1A2A3C"), Color.white);
        SetRef(so, "backButton", backBtn.GetComponent<Button>());

        // ── WorldSelectRow 프리팹 생성/로드 ──────────────────────────────
        WorldSelectRow rowComp = BuildOrLoadRowPrefab();
        if (rowComp != null) SetRef(so, "rowPrefab", rowComp);

        so.ApplyModifiedProperties();

        // ── TitleManager 자동 연결 ────────────────────────────────────────
        var titleManager = Object.FindAnyObjectByType<TitleManager>();
        if (titleManager != null)
        {
            var tmSO = new SerializedObject(titleManager);
            SetRef(tmSO, "worldSelectUI", ui);
            tmSO.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[WorldSelectUIBuilder] 씬에 TitleManager가 없어 자동 연결을 건너뜀.");
        }

        root.SetActive(false); // 평소엔 숨김 — TitleManager.worldSelectUI.Show()가 활성화

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[WorldSelectUIBuilder] 월드 선택 UI 생성 완료. " +
            "행 프리팹: Assets/05.Prefabs/UI/WorldSelectRow.prefab — Ctrl+S로 씬 저장하세요.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    // ── 스크롤 목록 ──────────────────────────────────────────────────────

    static Transform BuildScrollList(Transform parent)
    {
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta = new Vector2(840, 460);
        scrollRt.anchoredPosition = new Vector2(0f, 30f);

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var viewportGo = MakeImage("Viewport", scrollGo.transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        var vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRt;

        var contentGo = new GameObject("RowContainer", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);
        contentRt.anchoredPosition = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        return contentGo.transform;
    }

    // ── 새 월드 이름 입력 필드 ────────────────────────────────────────────
    // TMP_InputField는 Text Area/Placeholder 등 내부 구조가 복잡해 수동 생성보다
    // 기본 메뉴 생성 결과를 재배치하는 쪽이 안전하다 (SettingsPanelRebuilder와 동일 방식).

    static TMP_InputField BuildNameInputField(Transform parent)
    {
        GameObject go = CreateUIElementViaMenu("GameObject/UI/Input Field - TextMeshPro", parent);
        go.name = "Input_WorldName";
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480, 56);
        rt.anchoredPosition = new Vector2(-100f, -270f);

        var input = go.GetComponent<TMP_InputField>();
        input.text = string.Empty;
        input.characterLimit = 24;

        var placeholder = go.transform.Find("Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>();
        if (placeholder != null)
        {
            placeholder.text = "새 월드 이름";
            ApplyFont(placeholder);
        }
        var textComp = go.transform.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
        ApplyFont(textComp);

        return input;
    }

    static GameObject CreateUIElementViaMenu(string menuPath, Transform parent)
    {
        var prevSelection = Selection.activeGameObject;
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem(menuPath);
        GameObject created = Selection.activeGameObject;
        if (created != null && parent != null && created.transform.parent != parent)
            created.transform.SetParent(parent, false);
        Selection.activeGameObject = prevSelection;
        return created;
    }

    // ── WorldSelectRow 프리팹 생성/로드 ───────────────────────────────────

    static WorldSelectRow BuildOrLoadRowPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (existing != null)
        {
            var comp = existing.GetComponent<WorldSelectRow>();
            if (comp != null) return comp;
        }

        System.IO.Directory.CreateDirectory("Assets/05.Prefabs/UI");

        var row = new GameObject("WorldSelectRow", typeof(RectTransform));
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(800, 84);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = Hex("13283B", 220);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(20, 14, 8, 8);
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 84f;
        le.preferredHeight = 84f;

        // 이름 + 정보 텍스트 (세로로 쌓는 영역)
        var textColGo = new GameObject("TextColumn", typeof(RectTransform));
        textColGo.transform.SetParent(row.transform, false);
        var textColLE = textColGo.AddComponent<LayoutElement>();
        textColLE.flexibleWidth = 1f;
        var vlg = textColGo.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.MiddleLeft;

        var nameTMP = MakeTMP("NameText", textColGo.transform, new Vector2(0, 28), Vector2.zero,
            "이름없는 월드", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
        nameTMP.fontStyle = FontStyles.Bold;
        ApplyFont(nameTMP);

        var infoTMP = MakeTMP("InfoText", textColGo.transform, new Vector2(0, 22), Vector2.zero,
            "강화 Lv.0", 15f, Hex("8FA6BC"), TextAlignmentOptions.MidlineLeft);
        ApplyFont(infoTMP);

        // 선택 버튼(행 전체를 누르면 진입)
        var selectBtn = row.AddComponent<Button>();
        selectBtn.targetGraphic = rowImg;

        // 삭제 버튼(우측 고정)
        var deleteGo = MakeButton("Btn_Delete", row.transform, new Vector2(72, 56), Vector2.zero, "삭제", 16f, Hex("3A1414"), Hex("FF8A8A"));
        var deleteLE = deleteGo.AddComponent<LayoutElement>();
        deleteLE.minWidth = 72f;
        deleteLE.preferredWidth = 72f;
        deleteLE.flexibleWidth = 0f;

        var rowComp = row.AddComponent<WorldSelectRow>();
        var rowSO = new SerializedObject(rowComp);
        SetRef(rowSO, "nameText", nameTMP);
        SetRef(rowSO, "infoText", infoTMP);
        SetRef(rowSO, "selectButton", selectBtn);
        SetRef(rowSO, "deleteButton", deleteGo.GetComponent<Button>());
        rowSO.ApplyModifiedProperties();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(row, RowPrefabPath);
        Object.DestroyImmediate(row);

        AssetDatabase.Refresh();
        Debug.Log($"[WorldSelectUIBuilder] WorldSelectRow 프리팹 생성됨: {RowPrefabPath}");

        return prefabAsset?.GetComponent<WorldSelectRow>();
    }

    // ── 헬퍼 (이 빌더 전용 — 다른 Builder 스크립트와 공유하지 않음) ───────

    static GameObject MakeFullscreen(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
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

    static TextMeshProUGUI MakeTMP(string name, Transform parent, Vector2 size, Vector2 pos,
        string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent, Vector2 size, Vector2 pos,
        string label, float fontSize, Color bgColor, Color? textColor = null)
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
        tmp.color = textColor ?? Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        ApplyFont(tmp);
        return go;
    }

    static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var prop = so.FindProperty(field);
        if (prop != null) prop.objectReferenceValue = obj;
        else Debug.LogWarning($"[WorldSelectUIBuilder] 필드 없음: '{field}'");
    }

    static Color Hex(string hex, int alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha / 255f;
        return c;
    }
}
#endif
