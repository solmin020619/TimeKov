using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quest UI 프리팹 4개 자동 생성 (ObjectiveLine, QuestEntry, CategoryWidget, QuestPanelUI).
/// 메뉴: Tools > Quest > Build UI Prefabs
/// 시각 디테일은 자동 생성 후 사용자가 인스펙터에서 조정.
/// </summary>
public static class QuestPrefabBuilder
{
    const string PrefabFolder = "Assets/05.Prefabs/Quest";

    // #00FF80
    static readonly Color GreenAccent = new Color(0f, 1f, 128f / 255f, 1f);
    static readonly Color GreenAccentTransparent = new Color(0f, 1f, 128f / 255f, 0f);

    // 한글 지원 TMP 폰트 후보 (우선순위 순). 첫 번째로 찾은 것 사용.
    static readonly string[] KoreanFontCandidates =
    {
        "Assets/11.Font/Pretendard-ExtraBold SDF.asset",
        "Assets/11.Font/남양주고딕Light (OTF) SDF.asset",
        "Assets/11.Font/GabiaMaeumgyeol SDF.asset",
        "Assets/Resources/Font/Maplestory Light SDF.asset",
        "Assets/TextMesh Pro/Fonts/DungGeunMo SDF.asset",
    };

    static TMP_FontAsset _cachedKoreanFont;

    static TMP_FontAsset GetKoreanFont()
    {
        if (_cachedKoreanFont != null) return _cachedKoreanFont;

        foreach (var path in KoreanFontCandidates)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f != null)
            {
                _cachedKoreanFont = f;
                return f;
            }
        }

        Debug.LogWarning("[QuestPrefabBuilder] 한글 지원 폰트 못 찾음 — TMP 기본 폰트 사용 (한글 깨짐). " +
                         "11.Font/ 폴더에 SDF 폰트가 있는지 확인하세요.");
        return TMP_Settings.defaultFontAsset;
    }

    [MenuItem("Tools/Quest/Verify Font Setup")]
    public static void VerifyFontSetup()
    {
        _cachedKoreanFont = null;
        var font = GetKoreanFont();
        if (font == null)
        {
            Debug.LogError("[Verify] 한글 폰트 못 찾음");
            return;
        }

        var so = new SerializedObject(font);
        int mode = so.FindProperty("m_AtlasPopulationMode").intValue;
        var sourceProp = so.FindProperty("m_SourceFontFile");
        var sourceFile = sourceProp?.objectReferenceValue;
        var guidProp = so.FindProperty("m_SourceFontFileGUID");
        string guidValue = guidProp != null ? guidProp.stringValue : "(none)";

        string modeStr = mode == 0 ? "Static ❌ (한글 깨짐 원인)"
                       : mode == 1 ? "Dynamic ✅"
                       : mode == 2 ? "Dynamic OS"
                       : $"Unknown({mode})";

        string sourceStr = sourceFile != null
            ? $"{sourceFile.name} ✅"
            : "❌ NULL — Dynamic이지만 source 없으면 글리프 못 추가";

        Debug.Log(
            $"[Verify] 폰트 진단\n" +
            $"  파일: {AssetDatabase.GetAssetPath(font)}\n" +
            $"  Atlas Mode: {modeStr}\n" +
            $"  Source Font: {sourceStr}\n" +
            $"  Source GUID: {guidValue}\n" +
            $"  CharacterTable 글리프 수: {font.characterTable.Count}\n" +
            $"  AtlasTexture: {(font.atlasTexture != null ? font.atlasTexture.name + $" ({font.atlasTexture.width}x{font.atlasTexture.height})" : "null")}\n" +
            "  → Mode가 Dynamic이고 Source가 ✅면 정상. 그래도 깨지면 Force Refresh 시도."
        );
    }

    [MenuItem("Tools/Quest/Clear Korean Font Atlas Data")]
    public static void ClearKoreanFontAtlasData()
    {
        _cachedKoreanFont = null;
        var font = GetKoreanFont();
        if (font == null) return;

        bool ok = EditorUtility.DisplayDialog(
            "Atlas 데이터 클리어",
            $"'{font.name}'의 atlas 글리프 + 텍스처 데이터를 모두 비웁니다.\n" +
            "Dynamic 모드면 런타임에 다시 채워집니다.\n\n" +
            "(파일 크기 비대 해소 / 깨끗하게 재시작용)",
            "클리어", "취소");
        if (!ok) return;

        font.ClearFontAssetData(true);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[QuestPrefabBuilder] '{font.name}' atlas 데이터 클리어 완료. " +
                  "Project 뷰에서 파일 크기 줄어든 것 확인 가능.");
    }

    [MenuItem("Tools/Quest/Pre-populate Korean Atlas")]
    public static void PrePopulateKoreanAtlas()
    {
        _cachedKoreanFont = null;
        var font = GetKoreanFont();
        if (font == null) return;

        // Multi-atlas 활성화 (한 atlas 꽉 차면 새 atlas page 추가)
        var soFont = new SerializedObject(font);
        var multi = soFont.FindProperty("m_IsMultiAtlasTexturesEnabled");
        bool multiEnabled = multi != null;
        if (multi != null && !multi.boolValue)
        {
            multi.boolValue = true;
            soFont.ApplyModifiedProperties();
        }

        // ASCII + 한글 음절 + General Punctuation (스마트따옴표/em-dash/ellipsis 등) + CJK 기호
        var sb = new System.Text.StringBuilder(11172 + 256);
        // ASCII printable 33~126
        for (int i = 33; i <= 126; i++) sb.Append((char)i);
        // General Punctuation (U+2000~U+206F) — “ ” ‘ ’ – — … 등
        for (int i = 0x2000; i <= 0x206F; i++) sb.Append((char)i);
        // CJK Symbols and Punctuation (U+3000~U+303F) — 　 ・ 「」 등
        for (int i = 0x3000; i <= 0x303F; i++) sb.Append((char)i);
        // Hangul Syllables (U+AC00~U+D7A3)
        for (int i = 0xAC00; i <= 0xD7A3; i++) sb.Append((char)i);
        string charset = sb.ToString();

        font.TryAddCharacters(charset, out string missing);
        int missingCount = missing != null ? missing.Length : 0;
        int addedCount = charset.Length - missingCount;

        Debug.Log(
            $"[QuestPrefabBuilder] 한글 atlas pre-populate 완료\n" +
            $"  시도: {charset.Length}자 (ASCII + Hangul Syllables)\n" +
            $"  성공: {addedCount}자\n" +
            $"  실패: {missingCount}자 (source font에 없는 것)\n" +
            $"  최종 글리프 수: {font.characterTable.Count}\n" +
            $"  Atlas 텍스처 수: {(font.atlasTextures != null ? font.atlasTextures.Length : 0)}\n" +
            $"  Multi-Atlas Enabled: {multiEnabled}"
        );

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Quest/Apply Korean Font To Prefabs")]
    public static void ApplyKoreanFont()
    {
        _cachedKoreanFont = null;  // 새로 검색
        var font = GetKoreanFont();
        if (font == null)
        {
            Debug.LogError("[QuestPrefabBuilder] 적용할 폰트 없음 — TMP_Settings.defaultFontAsset도 null");
            return;
        }

        string[] names = { "ObjectiveLine.prefab", "QuestEntry.prefab", "CategoryWidget.prefab", "QuestPanelUI.prefab" };
        int totalUpdated = 0;
        int prefabsUpdated = 0;

        foreach (var n in names)
        {
            string path = $"{PrefabFolder}/{n}";
            var prefabContents = PrefabUtility.LoadPrefabContents(path);
            if (prefabContents == null)
            {
                Debug.LogWarning($"[QuestPrefabBuilder] 프리팹 못 찾음: {path}");
                continue;
            }

            int updatedInThis = 0;
            var tmps = prefabContents.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmps)
            {
                tmp.font = font;
                updatedInThis++;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
            PrefabUtility.UnloadPrefabContents(prefabContents);

            totalUpdated += updatedInThis;
            if (updatedInThis > 0) prefabsUpdated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[QuestPrefabBuilder] 한글 폰트 적용 완료. 폰트={font.name}, " +
                  $"프리팹 {prefabsUpdated}개 / TMP 컴포넌트 {totalUpdated}개 업데이트.");
    }

    [MenuItem("Tools/Quest/Build UI Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/05.Prefabs", "Quest");

        if (AnyPrefabExists())
        {
            bool ok = EditorUtility.DisplayDialog(
                "Quest UI 프리팹 덮어쓰기",
                "Quest UI 프리팹이 이미 존재합니다.\n덮어쓰면 인스펙터에서 조정한 시각 설정이 사라집니다.\n계속하시겠습니까?",
                "덮어쓰기", "취소");
            if (!ok)
            {
                Debug.Log("[QuestPrefabBuilder] 사용자 취소.");
                return;
            }
        }

        string olPath = BuildObjectiveLine();
        string qePath = BuildQuestEntry(olPath);
        string cwPath = BuildCategoryWidget(qePath);
        string qpPath = BuildQuestPanelUI(cwPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[QuestPrefabBuilder] 4개 프리팹 생성 완료.\n" +
            "다음 단계:\n" +
            "1. 씬에 Canvas 만들고 그 안에 QuestPanelUI 프리팹 배치 (좌상단 앵커)\n" +
            "2. 빈 GameObject \"QuestSystem\" 만들고 QuestManager 컴포넌트 부착, tutorial 슬롯에 TutorialSO 드래그\n" +
            "3. 빈 GameObject \"PlayerWatcher\" 만들고 PlayerMovementWatcher 컴포넌트 부착\n" +
            "4. QuestEntry.prefab 열어서 completeSfx에 사운드 클립 드래그 (옵션)\n" +
            "5. 시각 조정: 폰트, 색상, 크기는 각 프리팹에서 인스펙터로 조정"
        );
    }

    static bool AnyPrefabExists()
    {
        string[] names = { "ObjectiveLine.prefab", "QuestEntry.prefab", "CategoryWidget.prefab", "QuestPanelUI.prefab" };
        foreach (var n in names)
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{n}") != null) return true;
        return false;
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, name);
    }

    // ── 1. ObjectiveLine ─────────────────────────────────────────

    static string BuildObjectiveLine()
    {
        GameObject root = MakeUI("ObjectiveLine");
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240f, 24f);

        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;

        // Label — 좌측 stretch, 우측에 체크마크 자리 24px 비움
        var labelGO = MakeUI("Label", root.transform);
        SetStretch(labelGO.GetComponent<RectTransform>(), 4f, 24f, 0f, 0f);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(label, "└ Objective label", 14, FontStyles.Normal, TextAlignmentOptions.Left);

        // Checkmark — 우측 16x16
        var checkGO = MakeUI("Checkmark", root.transform);
        SetAnchor(checkGO.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-2f, 0f), new Vector2(16f, 16f));
        var checkImg = AddImage(checkGO, GreenAccent);
        checkImg.enabled = false;

        var ol = root.AddComponent<ObjectiveLine>();
        SetField(ol, "labelText", label);
        SetField(ol, "checkmark", checkImg);

        return Save(root, "ObjectiveLine.prefab");
    }

    // ── 2. QuestEntry ───────────────────────────────────────────

    static string BuildQuestEntry(string objectiveLinePath)
    {
        GameObject root = MakeUI("QuestEntry");
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 40f);

        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;

        root.AddComponent<CanvasGroup>();
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // ── SingleRow ────────────────────────────────
        var singleRow = MakeUI("SingleRow", root.transform);
        SetStretch(singleRow.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        // SingleFlash (배경, 가장 먼저 그려져야 함)
        var singleFlashGO = MakeUI("SingleFlash", singleRow.transform);
        SetStretch(singleFlashGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var singleFlashImg = AddImage(singleFlashGO, GreenAccentTransparent);

        // SingleLabel
        var singleLabelGO = MakeUI("SingleLabel", singleRow.transform);
        SetStretch(singleLabelGO.GetComponent<RectTransform>(), 4f, 24f, 0f, 0f);
        var singleLabel = singleLabelGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(singleLabel, "Quest label", 14, FontStyles.Normal, TextAlignmentOptions.Left);

        // SingleStrike (취소선) — 좌측 시작, height 2, width 0
        var singleStrikeGO = MakeUI("SingleStrike", singleRow.transform);
        var singleStrikeRT = singleStrikeGO.GetComponent<RectTransform>();
        SetAnchor(singleStrikeRT,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(4f, 0f), new Vector2(0f, 2f));
        AddImage(singleStrikeGO, Color.white);

        // SingleCheck — 우측 16x16, scale 0
        var singleCheckGO = MakeUI("SingleCheck", singleRow.transform);
        var singleCheckRT = singleCheckGO.GetComponent<RectTransform>();
        SetAnchor(singleCheckRT,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-2f, 0f), new Vector2(16f, 16f));
        singleCheckRT.localScale = Vector3.zero;
        var singleCheckImg = AddImage(singleCheckGO, GreenAccent);

        // ── MultiRow (초기엔 비활성) ──────────────────
        var multiRow = MakeUI("MultiRow", root.transform);
        SetStretch(multiRow.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        multiRow.SetActive(false);

        // MultiFlash (배경)
        var multiFlashGO = MakeUI("MultiFlash", multiRow.transform);
        SetStretch(multiFlashGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var multiFlashImg = AddImage(multiFlashGO, GreenAccentTransparent);

        // MultiTitle — 상단 22px
        var multiTitleGO = MakeUI("MultiTitle", multiRow.transform);
        var multiTitleRT = multiTitleGO.GetComponent<RectTransform>();
        SetAnchor(multiTitleRT,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(0f, 22f));
        var multiTitle = multiTitleGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(multiTitle, "Quest Title", 14, FontStyles.Bold, TextAlignmentOptions.Left);

        // MultiObjectiveList — 제목 아래, 좌측 들여쓰기, VLG
        var multiListGO = MakeUI("MultiObjectiveList", multiRow.transform);
        var multiListRT = multiListGO.GetComponent<RectTransform>();
        multiListRT.anchorMin = new Vector2(0f, 0f);
        multiListRT.anchorMax = new Vector2(1f, 1f);
        multiListRT.pivot = new Vector2(0.5f, 1f);
        multiListRT.offsetMin = new Vector2(12f, 0f);    // 좌측 들여쓰기
        multiListRT.offsetMax = new Vector2(-2f, -22f);  // 상단 제목 자리 비움
        var multiVLG = multiListGO.AddComponent<VerticalLayoutGroup>();
        multiVLG.spacing = 2f;
        multiVLG.childControlWidth = true;
        multiVLG.childControlHeight = false;
        multiVLG.childForceExpandWidth = true;
        multiVLG.childForceExpandHeight = false;

        // MultiStrike — 제목 줄 위에 좌측 시작
        var multiStrikeGO = MakeUI("MultiStrike", multiRow.transform);
        var multiStrikeRT = multiStrikeGO.GetComponent<RectTransform>();
        SetAnchor(multiStrikeRT,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(4f, -11f), new Vector2(0f, 2f));
        AddImage(multiStrikeGO, Color.white);

        // MultiCheck — 우상단 16x16, scale 0
        var multiCheckGO = MakeUI("MultiCheck", multiRow.transform);
        var multiCheckRT = multiCheckGO.GetComponent<RectTransform>();
        SetAnchor(multiCheckRT,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-2f, -11f), new Vector2(16f, 16f));
        multiCheckRT.localScale = Vector3.zero;
        var multiCheckImg = AddImage(multiCheckGO, GreenAccent);

        // ── 컴포넌트 + 슬롯 ───────────────────────────
        var qe = root.AddComponent<QuestEntry>();
        SetField(qe, "singleRow", singleRow);
        SetField(qe, "singleLabel", singleLabel);
        SetField(qe, "singleStrike", singleStrikeRT);
        SetField(qe, "singleFlash", singleFlashImg);
        SetField(qe, "singleCheck", singleCheckImg);
        SetField(qe, "multiRow", multiRow);
        SetField(qe, "multiTitle", multiTitle);
        SetField(qe, "multiObjectiveList", multiListGO.transform);
        SetField(qe, "multiStrike", multiStrikeRT);
        SetField(qe, "multiFlash", multiFlashImg);
        SetField(qe, "multiCheck", multiCheckImg);
        SetField(qe, "audioSource", audio);
        SetField(qe, "defaultHeight", 40f);
        // completeSfx — 사용자가 직접 할당

        // ObjectiveLine prefab 참조
        var olPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(objectiveLinePath);
        var olComponent = olPrefabGO != null ? olPrefabGO.GetComponent<ObjectiveLine>() : null;
        if (olComponent != null)
            SetField(qe, "objectiveLinePrefab", olComponent);
        else
            Debug.LogError($"[QuestPrefabBuilder] ObjectiveLine prefab 참조 실패: {objectiveLinePath}");

        return Save(root, "QuestEntry.prefab");
    }

    // ── 3. CategoryWidget ────────────────────────────────────────

    static string BuildCategoryWidget(string questEntryPath)
    {
        GameObject root = MakeUI("CategoryWidget");
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 60f);

        var rootVLG = root.AddComponent<VerticalLayoutGroup>();
        rootVLG.spacing = 4f;
        rootVLG.padding = new RectOffset(0, 0, 0, 0);
        rootVLG.childControlWidth = true;
        rootVLG.childControlHeight = false;
        rootVLG.childForceExpandWidth = true;
        rootVLG.childForceExpandHeight = false;

        var rootCSF = root.AddComponent<ContentSizeFitter>();
        rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var cg = root.AddComponent<CanvasGroup>();

        // CategoryTitle
        var titleGO = MakeUI("CategoryTitle", root.transform);
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 28f;
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(title, "카테고리명", 18, FontStyles.Bold, TextAlignmentOptions.Left);

        // QuestSlot — VLG + CSF
        var slotGO = MakeUI("QuestSlot", root.transform);
        var slotVLG = slotGO.AddComponent<VerticalLayoutGroup>();
        slotVLG.spacing = 4f;
        slotVLG.childControlWidth = true;
        slotVLG.childControlHeight = false;
        slotVLG.childForceExpandWidth = true;
        slotVLG.childForceExpandHeight = false;
        var slotCSF = slotGO.AddComponent<ContentSizeFitter>();
        slotCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 컴포넌트
        var cw = root.AddComponent<CategoryWidget>();
        SetField(cw, "categoryTitle", title);
        SetField(cw, "questSlot", slotGO.transform);
        SetField(cw, "categoryGroup", cg);

        // QuestEntry prefab 참조
        var qePrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(questEntryPath);
        var qeComponent = qePrefabGO != null ? qePrefabGO.GetComponent<QuestEntry>() : null;
        if (qeComponent != null)
            SetField(cw, "questEntryPrefab", qeComponent);
        else
            Debug.LogError($"[QuestPrefabBuilder] QuestEntry prefab 참조 실패: {questEntryPath}");

        return Save(root, "CategoryWidget.prefab");
    }

    // ── 4. QuestPanelUI ──────────────────────────────────────────

    static string BuildQuestPanelUI(string categoryWidgetPath)
    {
        GameObject root = MakeUI("QuestPanelUI");
        var rt = root.GetComponent<RectTransform>();
        // 좌상단 앵커, width 300, height 600 (CSF가 줄여줌)
        SetAnchor(rt,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -8f), new Vector2(300f, 600f));

        var cg = root.AddComponent<CanvasGroup>();

        var rootVLG = root.AddComponent<VerticalLayoutGroup>();
        rootVLG.padding = new RectOffset(0, 0, 0, 0);
        rootVLG.childControlWidth = true;
        rootVLG.childControlHeight = false;
        rootVLG.childForceExpandWidth = true;
        rootVLG.childForceExpandHeight = false;

        var rootCSF = root.AddComponent<ContentSizeFitter>();
        rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // CategoryRoot — VLG spacing=8 padding=8
        var crGO = MakeUI("CategoryRoot", root.transform);
        var crVLG = crGO.AddComponent<VerticalLayoutGroup>();
        crVLG.spacing = 8f;
        crVLG.padding = new RectOffset(8, 8, 8, 8);
        crVLG.childControlWidth = true;
        crVLG.childControlHeight = false;
        crVLG.childForceExpandWidth = true;
        crVLG.childForceExpandHeight = false;
        var crCSF = crGO.AddComponent<ContentSizeFitter>();
        crCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 컴포넌트
        var qp = root.AddComponent<QuestPanelUI>();
        SetField(qp, "categoryRoot", crGO.transform);
        SetField(qp, "panelGroup", cg);
        SetField(qp, "toggleKey", KeyCode.Tab);
        SetField(qp, "completionTimeoutSec", 3f);
        SetField(qp, "toggleDuration", 0.2f);

        // CategoryWidget prefab 참조
        var cwPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(categoryWidgetPath);
        var cwComponent = cwPrefabGO != null ? cwPrefabGO.GetComponent<CategoryWidget>() : null;
        if (cwComponent != null)
            SetField(qp, "categoryWidgetPrefab", cwComponent);
        else
            Debug.LogError($"[QuestPrefabBuilder] CategoryWidget prefab 참조 실패: {categoryWidgetPath}");

        return Save(root, "QuestPanelUI.prefab");
    }

    // ── Helpers ─────────────────────────────────────────────────

    static GameObject MakeUI(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void SetStretch(RectTransform rt, float leftPad, float rightPad, float topPad, float bottomPad)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(leftPad, bottomPad);
        rt.offsetMax = new Vector2(-rightPad, -topPad);
    }

    static void SetAnchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                          Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static void ConfigureTMP(TextMeshProUGUI tmp, string text, float fontSize,
                             FontStyles style, TextAlignmentOptions align)
    {
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        var font = GetKoreanFont();
        if (font != null) tmp.font = font;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        // 빈 Image는 렌더링 안 되니까 Unity 내장 UISprite 할당
        var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (defaultSprite != null) img.sprite = defaultSprite;
        img.raycastTarget = false;  // UI 클릭 통과
        return img;
    }

    static string Save(GameObject root, string filename)
    {
        string path = $"{PrefabFolder}/{filename}";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (prefab == null)
            Debug.LogError($"[QuestPrefabBuilder] 프리팹 저장 실패: {path}");
        return path;
    }

    static void SetField(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null)
        {
            Debug.LogError($"[QuestPrefabBuilder] Field 못찾음: {obj.GetType().Name}.{fieldName}");
            return;
        }
        f.SetValue(obj, value);
    }
}
