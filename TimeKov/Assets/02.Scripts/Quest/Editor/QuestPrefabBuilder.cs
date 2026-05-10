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

        Debug.LogWarning("[QuestPrefabBuilder] 한글 지원 폰트 못 찾음. TMP 기본 폰트 사용 (한글 깨짐). " +
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

        string modeStr = mode == 0 ? "Static (NG, 한글 깨짐 원인)"
                       : mode == 1 ? "Dynamic (OK)"
                       : mode == 2 ? "Dynamic OS"
                       : $"Unknown({mode})";

        string sourceStr = sourceFile != null
            ? $"{sourceFile.name} (OK)"
            : "(NG) NULL. Dynamic이지만 source 없으면 글리프 못 추가";

        Debug.Log(
            $"[Verify] 폰트 진단\n" +
            $"  파일: {AssetDatabase.GetAssetPath(font)}\n" +
            $"  Atlas Mode: {modeStr}\n" +
            $"  Source Font: {sourceStr}\n" +
            $"  Source GUID: {guidValue}\n" +
            $"  CharacterTable 글리프 수: {font.characterTable.Count}\n" +
            $"  AtlasTexture: {(font.atlasTexture != null ? font.atlasTexture.name + $" ({font.atlasTexture.width}x{font.atlasTexture.height})" : "null")}\n" +
            "  Mode가 Dynamic이고 Source가 OK면 정상. 그래도 깨지면 Force Refresh 시도."
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
        // General Punctuation (U+2000~U+206F): smart quotes, en/em dash, ellipsis 등
        for (int i = 0x2000; i <= 0x206F; i++) sb.Append((char)i);
        // CJK Symbols and Punctuation (U+3000~U+303F): ideographic space, middot, brackets 등
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

    [MenuItem("Tools/Quest/Cleanup VFX Package")]
    public static void CleanupVfxPackage()
    {
        const string oldFolder = "Assets/Game VFX - UI & Word Effect Collection";
        const string oldTextures = oldFolder + "/Textures";
        const string newFolder = "Assets/UI Effect";
        string[] keep = { "shockmark.png", "grad2b_windowlight.png", "question.png" };

        if (!AssetDatabase.IsValidFolder(oldFolder))
        {
            Debug.LogError($"[Cleanup] 패키지 폴더 못 찾음: {oldFolder} (이미 정리됐거나 경로 다름)");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "VFX 패키지 정리",
            $"패키지에서 사용 중인 텍스처 3개를 '{newFolder}'로 이동 후, '{oldFolder}' 폴더 통째 삭제.\n\n" +
            "이동 대상:\n  - " + string.Join("\n  - ", keep) + "\n\n" +
            "삭제: 머티리얼/프리팹/셰이더/씬/Readme 모두\n\n" +
            "[주의] 이 패키지의 다른 에셋을 다른 시스템에서 참조 중이면 missing reference 발생. " +
            "git 커밋 전이면 revert 가능.",
            "정리 실행", "취소");
        if (!ok) return;

        // 새 폴더 생성
        if (!AssetDatabase.IsValidFolder(newFolder))
            AssetDatabase.CreateFolder("Assets", "UI Effect");

        // 텍스처 이동. MoveAsset은 GUID 유지 + 참조 자동 갱신
        int moved = 0;
        foreach (var f in keep)
        {
            var src = $"{oldTextures}/{f}";
            var dst = $"{newFolder}/{f}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(src) == null)
            {
                Debug.LogWarning($"[Cleanup] 이미 이동됐거나 없음: {src}");
                continue;
            }
            var err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogError($"[Cleanup] 이동 실패: {src} -> {dst}: {err}");
        }

        // 패키지 폴더 통째 삭제
        bool deleted = AssetDatabase.DeleteAsset(oldFolder);
        if (!deleted) Debug.LogError($"[Cleanup] 폴더 삭제 실패: {oldFolder}");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Cleanup] 정리 완료.\n" +
            $"  - 이동: {moved}개 -> {newFolder}\n" +
            $"  - 삭제: {oldFolder} {(deleted ? "(OK)" : "(NG)")}\n" +
            "  - Console에 missing reference 에러 떠있는지 확인. 없으면 OK.");
    }

    [MenuItem("Tools/Quest/Apply VFX Sprites")]
    public static void ApplyVfxSprites()
    {
        // Cleanup 전(원래 패키지 경로) / 후(UI Effect 폴더) 둘 다 호환
        var shockmark = LoadVfxSprite("shockmark.png");
        var gradWindow = LoadVfxSprite("grad2b_windowlight.png");
        var question = LoadVfxSprite("question.png");

        if (shockmark == null || gradWindow == null)
        {
            Debug.LogError($"[QuestPrefabBuilder] 필수 VFX 텍스처 못 찾음. shockmark={shockmark}, gradWindow={gradWindow}");
            return;
        }
        if (question == null)
            Debug.LogWarning("[QuestPrefabBuilder] question.png 못 찾음. IconNormal은 비워둠");

        int updated = 0;

        // QuestEntry: IconAlert <- shockmark, IconNormal <- question (있으면)
        var qePath = $"{PrefabFolder}/QuestEntry.prefab";
        var qe = PrefabUtility.LoadPrefabContents(qePath);
        if (qe != null)
        {
            updated += SetImageSprite(qe.transform, "IconAlert", shockmark);
            if (question != null)
                updated += SetImageSprite(qe.transform, "IconNormal", question);
            PrefabUtility.SaveAsPrefabAsset(qe, qePath);
            PrefabUtility.UnloadPrefabContents(qe);
        }

        // ObjectiveLine: YellowSweep <- grad2b_windowlight (Image.type=Filled는 빌더가 이미 설정)
        var olPath = $"{PrefabFolder}/ObjectiveLine.prefab";
        var ol = PrefabUtility.LoadPrefabContents(olPath);
        if (ol != null)
        {
            updated += SetImageSprite(ol.transform, "YellowSweep", gradWindow);
            PrefabUtility.SaveAsPrefabAsset(ol, olPath);
            PrefabUtility.UnloadPrefabContents(ol);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[QuestPrefabBuilder] VFX sprite 적용 완료. 슬롯 {updated}개 업데이트.\n" +
            "  - IconAlert <- shockmark.png (!!!)\n" +
            (question != null ? "  - IconNormal <- question.png (?, 임시 동그라미 대용)\n" : "  - IconNormal: 비움 (사용자 드래그)\n") +
            "  - YellowSweep <- grad2b_windowlight.png (sweep)\n" +
            "  Toast Icon, CheckBoxEmpty/Filled는 사용자가 직접 인스펙터에서 드래그.");
    }

    /// <summary>VFX 텍스처 로드. Cleanup 전/후 경로 둘 다 시도</summary>
    static Sprite LoadVfxSprite(string filename)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI Effect/{filename}");
        if (sp == null)
            sp = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Game VFX - UI & Word Effect Collection/Textures/{filename}");
        return sp;
    }

    /// <summary>자식 GameObject 이름으로 찾아서 Image.sprite 설정. 못 찾으면 0 반환.</summary>
    static int SetImageSprite(Transform root, string childName, Sprite sprite)
    {
        var found = FindRecursive(root, childName);
        if (found == null)
        {
            Debug.LogWarning($"[QuestPrefabBuilder] '{childName}' 자식 못 찾음 (prefab 재빌드 필요할 수 있음)");
            return 0;
        }
        var img = found.GetComponent<UnityEngine.UI.Image>();
        if (img == null)
        {
            Debug.LogWarning($"[QuestPrefabBuilder] '{childName}'에 Image 컴포넌트 없음");
            return 0;
        }
        img.sprite = sprite;
        return 1;
    }

    static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var hit = FindRecursive(parent.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    [MenuItem("Tools/Quest/Apply Korean Font To Prefabs")]
    public static void ApplyKoreanFont()
    {
        _cachedKoreanFont = null;  // 새로 검색
        var font = GetKoreanFont();
        if (font == null)
        {
            Debug.LogError("[QuestPrefabBuilder] 적용할 폰트 없음. TMP_Settings.defaultFontAsset도 null");
            return;
        }

        string[] names = { "ObjectiveLine.prefab", "QuestEntry.prefab", "CategoryWidget.prefab", "QuestPanelUI.prefab", "ToastNotification.prefab" };
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
        string tnPath = BuildToastNotification();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[QuestPrefabBuilder] 5개 프리팹 생성 완료.\n" +
            "다음 단계:\n" +
            "1. 씬에 Canvas 만들고 그 안에 QuestPanelUI 프리팹 배치 (좌상단 앵커)\n" +
            "2. 같은 Canvas에 ToastNotification 프리팹을 sibling으로 배치 (패널 위쪽)\n" +
            "3. QuestPanelUI 인스턴스의 toast 슬롯에 ToastNotification 인스턴스 드래그\n" +
            "4. 빈 GameObject \"QuestSystem\" 만들고 QuestManager 컴포넌트 부착, tutorial 슬롯에 TutorialSO 드래그\n" +
            "5. 빈 GameObject \"PlayerWatcher\" 만들고 PlayerMovementWatcher 컴포넌트 부착\n" +
            "6. QuestEntry.prefab 열어서 completeSfx에 사운드 클립 드래그 (옵션)\n" +
            "7. 각 prefab의 Image 빈 sprite 슬롯(IconNormal/IconAlert/체크박스/토스트 아이콘/sweep VFX)에 사용자 에셋 드래그\n" +
            "8. 시각 조정: 폰트, 색상, 크기는 각 프리팹에서 인스펙터로 조정"
        );
    }

    static bool AnyPrefabExists()
    {
        string[] names = { "ObjectiveLine.prefab", "QuestEntry.prefab", "CategoryWidget.prefab", "QuestPanelUI.prefab", "ToastNotification.prefab" };
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

    // ===== 1. ObjectiveLine =====

    static string BuildObjectiveLine()
    {
        GameObject root = MakeUI("ObjectiveLine");
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240f, 24f);

        var le = root.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;

        // 자식 그리기 순서 (앞->뒤 = 추가 순서):
        //   CheckBoxEmpty(평상시 빈 박스), Label, YellowSweep, CheckBoxFilled(완료 시 체크된 박스)
        // Filled가 같은 위치 위에 덮어 그려져 등장 효과.

        // CheckBoxEmpty: 좌측 16x16, 평상시 활성. sprite는 인스펙터에서 사용자 드래그.
        var emptyGO = MakeUI("CheckBoxEmpty", root.transform);
        SetAnchor(emptyGO.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(2f, 0f), new Vector2(16f, 16f));
        var emptyImg = AddImage(emptyGO, Color.white);

        // Label: 좌측 22px(박스16 + 여백6) 들여쓰기, 우측 4px 여백
        var labelGO = MakeUI("Label", root.transform);
        SetStretch(labelGO.GetComponent<RectTransform>(), 22f, 4f, 0f, 0f);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(label, "Objective label", 14, FontStyles.Normal, TextAlignmentOptions.Left);

        // YellowSweep: Label 위 sweep. 박스 자리 피해서 텍스트 영역만 덮음.
        var sweepGO = MakeUI("YellowSweep", root.transform);
        SetStretch(sweepGO.GetComponent<RectTransform>(), 22f, 4f, 0f, 0f);
        var sweepImg = sweepGO.AddComponent<Image>();
        sweepImg.color = new Color(1f, 0.85f, 0f, 0f);  // 골든 옐로우, alpha 0
        sweepImg.type = Image.Type.Filled;
        sweepImg.fillMethod = Image.FillMethod.Horizontal;
        sweepImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        sweepImg.fillAmount = 0f;
        sweepImg.raycastTarget = false;
        var sweepSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sweepSprite != null) sweepImg.sprite = sweepSprite;

        // CheckBoxFilled: Empty와 동일 위치/크기. 평상시 비활성, 완료 시 scale pop으로 등장.
        var filledGO = MakeUI("CheckBoxFilled", root.transform);
        SetAnchor(filledGO.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(2f, 0f), new Vector2(16f, 16f));
        filledGO.transform.localScale = Vector3.zero;
        var filledImg = AddImage(filledGO, GreenAccent);
        filledImg.enabled = false;

        var ol = root.AddComponent<ObjectiveLine>();
        SetField(ol, "labelText", label);
        SetField(ol, "checkBoxEmpty", emptyImg);
        SetField(ol, "checkBoxFilled", filledImg);
        SetField(ol, "yellowSweep", sweepImg);

        return Save(root, "ObjectiveLine.prefab");
    }

    // ===== 2. QuestEntry =====

    static string BuildQuestEntry(string objectiveLinePath)
    {
        GameObject root = MakeUI("QuestEntry");
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 40f);

        // VLG + CSF: Title + ObjectiveList 자동 stack, 헤드 아이콘은 ignoreLayout
        var rootVLG = root.AddComponent<VerticalLayoutGroup>();
        rootVLG.padding = new RectOffset(0, 0, 0, 0);
        rootVLG.spacing = 4f;
        rootVLG.childControlWidth = true;
        rootVLG.childControlHeight = true;
        rootVLG.childForceExpandWidth = true;
        rootVLG.childForceExpandHeight = false;
        var rootCSF = root.AddComponent<ContentSizeFitter>();
        rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        root.AddComponent<CanvasGroup>();
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // Title: 큰 제목. VLG 첫 번째 자식. 좌측 22px 들여쓰기로 헤드 아이콘 자리 확보.
        var titleGO = MakeUI("Title", root.transform);
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 22f;
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(title, "Quest Title", 14, FontStyles.Bold, TextAlignmentOptions.Left);
        title.margin = new Vector4(22f, 0f, 0f, 0f);

        // IconNormal (평상시 동그라미): VLG 무시(절대 위치). Title 좌측 14x14.
        var iconNormalGO = MakeUI("IconNormal", root.transform);
        var iconNormalLE = iconNormalGO.AddComponent<LayoutElement>();
        iconNormalLE.ignoreLayout = true;
        SetAnchor(iconNormalGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f, -4f), new Vector2(14f, 14f));
        AddImage(iconNormalGO, Color.white);

        // IconAlert (!!!): Normal과 동일 위치, 평상시 비활성. 새 퀘스트 등장 순간만 표시.
        var iconAlertGO = MakeUI("IconAlert", root.transform);
        var iconAlertLE = iconAlertGO.AddComponent<LayoutElement>();
        iconAlertLE.ignoreLayout = true;
        SetAnchor(iconAlertGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f, -4f), new Vector2(14f, 14f));
        AddImage(iconAlertGO, Color.white);
        iconAlertGO.SetActive(false);

        // ObjectiveList: VLG 두 번째 자식. 자체도 VLG + CSF (ObjectiveLine들 stack)
        var listGO = MakeUI("ObjectiveList", root.transform);
        var listVLG = listGO.AddComponent<VerticalLayoutGroup>();
        listVLG.padding = new RectOffset(12, 2, 0, 0);  // 좌측 들여쓰기 12
        listVLG.spacing = 2f;
        listVLG.childControlWidth = true;
        listVLG.childControlHeight = true;   // VLG가 LE.preferredHeight 읽어 height 설정. vanish 애니에 필요
        listVLG.childForceExpandWidth = true;
        listVLG.childForceExpandHeight = false;
        var listCSF = listGO.AddComponent<ContentSizeFitter>();
        listCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ----- 컴포넌트 + 슬롯 -----
        var qe = root.AddComponent<QuestEntry>();
        SetField(qe, "title", title);
        SetField(qe, "objectiveList", listGO.transform);
        SetField(qe, "iconNormal", iconNormalGO);
        SetField(qe, "iconAlert", iconAlertGO);
        SetField(qe, "audioSource", audio);
        // completeSfx: 사용자가 직접 할당

        // ObjectiveLine prefab 참조
        var olPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(objectiveLinePath);
        var olComponent = olPrefabGO != null ? olPrefabGO.GetComponent<ObjectiveLine>() : null;
        if (olComponent != null)
            SetField(qe, "objectiveLinePrefab", olComponent);
        else
            Debug.LogError($"[QuestPrefabBuilder] ObjectiveLine prefab 참조 실패: {objectiveLinePath}");

        return Save(root, "QuestEntry.prefab");
    }

    // ===== 3. CategoryWidget =====

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

        // QuestSlot: VLG + CSF
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

    // ===== 4. QuestPanelUI =====

    static string BuildQuestPanelUI(string categoryWidgetPath)
    {
        GameObject root = MakeUI("QuestPanelUI");
        var rt = root.GetComponent<RectTransform>();
        // 좌상단 앵커, width 300, height 600 (CSF가 줄여줌). 위에서 120px 내려서 HUD와 공간 확보.
        SetAnchor(rt,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -120f), new Vector2(300f, 600f));

        var cg = root.AddComponent<CanvasGroup>();

        var rootVLG = root.AddComponent<VerticalLayoutGroup>();
        rootVLG.padding = new RectOffset(0, 0, 0, 0);
        rootVLG.childControlWidth = true;
        rootVLG.childControlHeight = false;
        rootVLG.childForceExpandWidth = true;
        rootVLG.childForceExpandHeight = false;

        var rootCSF = root.AddComponent<ContentSizeFitter>();
        rootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // CategoryRoot: VLG spacing=8 padding=8
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

    // ===== 5. ToastNotification =====

    static string BuildToastNotification()
    {
        GameObject root = MakeUI("ToastNotification");
        var rt = root.GetComponent<RectTransform>();
        // 좌상단 앵커, 패널(8,-120) 위쪽에 위치. 사용자가 씬에서 미세조정.
        SetAnchor(rt,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -90f), new Vector2(220f, 36f));

        var cg = root.AddComponent<CanvasGroup>();

        // Background: 풀 stretch, 어두운 반투명 디폴트
        var bgGO = MakeUI("Background", root.transform);
        SetStretch(bgGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        AddImage(bgGO, new Color(0f, 0f, 0f, 0.7f));

        // Icon: 좌측 24x24, sprite 빈 슬롯 (사용자가 드래그)
        var iconGO = MakeUI("Icon", root.transform);
        SetAnchor(iconGO.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(24f, 24f));
        var iconImg = AddImage(iconGO, Color.white);

        // Label: 아이콘 우측부터 끝까지 stretch
        var labelGO = MakeUI("Label", root.transform);
        SetStretch(labelGO.GetComponent<RectTransform>(), 40f, 8f, 0f, 0f);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(label, "업데이트 완료", 14, FontStyles.Bold, TextAlignmentOptions.Left);

        var tn = root.AddComponent<ToastNotification>();
        SetField(tn, "canvasGroup", cg);
        SetField(tn, "root", rt);
        SetField(tn, "labelText", label);
        SetField(tn, "iconImage", iconImg);

        return Save(root, "ToastNotification.prefab");
    }

    // ===== Helpers =====

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
