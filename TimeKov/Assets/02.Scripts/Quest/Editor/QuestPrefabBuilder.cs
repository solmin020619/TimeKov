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

    [MenuItem("Tools/Quest/Consolidate BarFlash Assets")]
    public static void ConsolidateBarFlashAssets()
    {
        const string newFolder = "Assets/12.VFX/UI Effect/FX_UI_BarFlashO";
        const string oldRoot = "Assets/Game VFX - UI & Word Effect Collection";

        if (!AssetDatabase.IsValidFolder(newFolder))
            AssetDatabase.CreateFolder("Assets/UI Effect", "FX_UI_BarFlashO");

        // BarFlashO prefab + 3 material + 1 shader + 2 texture = 7개 의존성
        // (grad2b_windowlight는 UI Effect에 이미 있음 - skip)
        string[] paths = {
            $"{oldRoot}/Prefabs/FX_UI_BarFlashO.prefab",
            $"{oldRoot}/Materials/starli.mat",
            $"{oldRoot}/Materials/gradglow_0.mat",
            $"{oldRoot}/Materials/glow_2.mat",
            $"{oldRoot}/Shader/Additive.shader",
            $"{oldRoot}/Textures/starli.png",
            $"{oldRoot}/Textures/glow_00000.png",
        };

        int moved = 0;
        int skipped = 0;
        foreach (var src in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(src) == null)
            {
                skipped++;
                continue;
            }
            var filename = System.IO.Path.GetFileName(src);
            var dst = $"{newFolder}/{filename}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst) != null)
            {
                skipped++;
                continue;
            }
            var err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogWarning($"[BarFlash] 이동 실패: {src} -> {dst}: {err}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[QuestPrefabBuilder] BarFlashO 의존성 정리 완료.\n" +
            $"  - 이동: {moved}개 -> {newFolder}\n" +
            $"  - 건너뜀: {skipped}개 (이미 이동됨 또는 없음)\n" +
            $"  이제 '{oldRoot}' 폴더 안의 나머지 파일은 안전하게 삭제 가능 (BarGlow 폴더도 있으면 같이).\n" +
            $"  Project 뷰에서 폴더 우클릭 → Delete.");
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
        // IconAlert(!!!) 우선순위: quest_marker_1 > quest_marker_256 > shockmark
        // IconNormal(?) 우선순위: marker_2 > question
        // EnsureSpriteImport는 textureType이 Default여도 Sprite로 자동 변환 후 로드
        var marker1 = EnsureSpriteImport("Assets/12.VFX/UI Effect/quest_marker_1.png");
        var marker256 = marker1 == null ? EnsureSpriteImport("Assets/12.VFX/UI Effect/quest_marker_256.png") : null;
        var shockmark = (marker1 == null && marker256 == null) ? LoadVfxSprite("shockmark.png") : null;
        var marker2 = EnsureSpriteImport("Assets/12.VFX/UI Effect/target_marker_256.png");
        var gradWindow = LoadVfxSprite("grad2b_windowlight.png");
        var question = LoadVfxSprite("question.png");

        var iconAlertSprite = marker1 ?? marker256 ?? shockmark;
        var iconNormalSprite = marker2 ?? question;
        if (iconAlertSprite == null || gradWindow == null)
        {
            Debug.LogError($"[QuestPrefabBuilder] 필수 VFX 텍스처 못 찾음. iconAlert={iconAlertSprite}, gradWindow={gradWindow}");
            return;
        }
        if (question == null)
            Debug.LogWarning("[QuestPrefabBuilder] question.png 못 찾음. IconNormal은 비워둠");

        int updated = 0;

        // QuestEntry: IconAlert <- marker_1 (등), IconNormal <- marker_2 (또는 question)
        var qePath = $"{PrefabFolder}/QuestEntry.prefab";
        var qe = PrefabUtility.LoadPrefabContents(qePath);
        if (qe != null)
        {
            updated += SetImageSprite(qe.transform, "IconAlert", iconAlertSprite);
            if (iconNormalSprite != null)
                updated += SetImageSprite(qe.transform, "IconNormal", iconNormalSprite);
            PrefabUtility.SaveAsPrefabAsset(qe, qePath);
            PrefabUtility.UnloadPrefabContents(qe);
        }

        // ObjectiveLine: YellowSweep + CheckBoxEmpty(square_marker_256) + CheckBoxFilled(check_marker_256)
        var checkBoxSprite = EnsureSpriteImport("Assets/12.VFX/UI Effect/square_marker_512.png");
        var checkFilledSprite = EnsureSpriteImport("Assets/12.VFX/UI Effect/check_marker_256 (1).png");

        var olPath = $"{PrefabFolder}/ObjectiveLine.prefab";
        var ol = PrefabUtility.LoadPrefabContents(olPath);
        if (ol != null)
        {
            // YellowSweep sprite: grad2b는 어두운 픽셀 있어서 노란 tint 안 먹힘.
            // _SweepGlow (자동 생성, 흰색 + sin alpha 곡선) → 가운데 진하고 양쪽 fade, 노란 tint 잘 받음.
            var sweepSprite = GetOrCreateSweepGlowSprite();
            if (sweepSprite != null)
                updated += SetImageSprite(ol.transform, "YellowSweep", sweepSprite);
            else
                updated += SetImageSprite(ol.transform, "YellowSweep", gradWindow);
            if (checkBoxSprite != null)
                updated += SetImageSprite(ol.transform, "CheckBoxEmpty", checkBoxSprite);
            if (checkFilledSprite != null)
                updated += SetImageSprite(ol.transform, "CheckBoxFilled", checkFilledSprite);

            // 체크박스 크기 키움 + Filled 색을 sprite 원본 그대로 (흰색)
            SetCheckBoxSize(ol.transform, "CheckBoxEmpty", 32f);
            SetCheckBoxSize(ol.transform, "CheckBoxFilled", 36f);   // Empty보다 큼
            SetImageColor(ol.transform, "CheckBoxFilled", Color.white);

            // 체크박스 X = 0 (ObjectiveList VLG padding-left이 QuestIconX로 정렬 처리)
            SetIconX(ol.transform, "CheckBoxEmpty", 0f);
            SetIconX(ol.transform, "CheckBoxFilled", 0f);

            // Label leftPad = Title text X와 정렬
            float labelLeftPad = QuestTitleLeftPad - QuestIconX;
            SetStretchLeftPad(ol.transform, "Label", labelLeftPad);

            // YellowSweep leftPad = 0 → 체크박스부터 line 전체 덮음 (사용자 요구)
            SetStretchLeftPad(ol.transform, "YellowSweep", 0f);

            // 기존 prefab의 sweep timing이 빠른 값 (0.25)일 수 있으니 새 디폴트로 덮어쓰기
            var olComponent = ol.GetComponent<ObjectiveLine>();
            if (olComponent != null)
            {
                var so = new SerializedObject(olComponent);
                var sd = so.FindProperty("sweepDuration");
                if (sd != null) sd.floatValue = 0.6f;
                var sfd = so.FindProperty("sweepFadeDuration");
                if (sfd != null) sfd.floatValue = 0.4f;
                var psh = so.FindProperty("postSweepHold");
                if (psh != null) psh.floatValue = 0.3f;
                var cd = so.FindProperty("collapseDuration");
                if (cd != null) cd.floatValue = 0.5f;
                var spa = so.FindProperty("sweepPeakAlpha");
                if (spa != null) spa.floatValue = 1.0f;
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(ol, olPath);
            PrefabUtility.UnloadPrefabContents(ol);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string iconSource = marker1 != null ? "quest_marker_1.png (사용자 디자인 최신)"
                          : marker256 != null ? "quest_marker_256.png (사용자 디자인)"
                          : "shockmark.png (fallback)";
        Debug.Log(
            $"[QuestPrefabBuilder] VFX sprite 적용 완료. 슬롯 {updated}개 업데이트.\n" +
            $"  - IconAlert <- {iconSource}\n" +
            (marker2 != null ? "  - IconNormal <- target_marker_256.png (사용자 디자인)\n"
             : question != null ? "  - IconNormal <- question.png (fallback)\n"
             : "  - IconNormal: 비움 (사용자 드래그)\n") +
            "  - YellowSweep <- grad2b_windowlight.png (sweep)\n" +
            (checkBoxSprite != null ? "  - CheckBoxEmpty <- square_marker_512.png (사용자 디자인)\n" : "  - CheckBoxEmpty: 비움 (사용자 드래그)\n") +
            (checkFilledSprite != null ? "  - CheckBoxFilled <- check_marker_256 (1).png (사용자 디자인)\n" : "  - CheckBoxFilled: 비움 (사용자 드래그)\n") +
            "  Toast Icon은 사용자가 직접 인스펙터에서 드래그.");
    }

    /// <summary>VFX 텍스처 로드. Cleanup 전/후 경로 둘 다 시도</summary>
    static Sprite LoadVfxSprite(string filename)
    {
        // EnsureSpriteImport로 textureType=Default 자동 변환 후 로드
        var path1 = $"Assets/12.VFX/UI Effect/{filename}";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path1) != null)
            return EnsureSpriteImport(path1);

        var path2 = $"Assets/Game VFX - UI & Word Effect Collection/Textures/{filename}";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path2) != null)
            return EnsureSpriteImport(path2);

        return null;
    }

    /// <summary>자식 GameObject 이름으로 찾아서 Image.sprite 설정. 못 찾으면 0 반환.</summary>
    static void SetIconX(Transform root, string childName, float x)
    {
        var found = FindRecursive(root, childName);
        if (found == null) return;
        var rt = found.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
    }

    static void SetStretchLeftPad(Transform root, string childName, float leftPad)
    {
        var found = FindRecursive(root, childName);
        if (found == null) return;
        var rt = found.GetComponent<RectTransform>();
        if (rt != null) rt.offsetMin = new Vector2(leftPad, rt.offsetMin.y);
    }

    static void SetCheckBoxSize(Transform root, string childName, float size)
    {
        var found = FindRecursive(root, childName);
        if (found == null) return;
        var rt = found.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(size, size);
    }

    static void SetImageColor(Transform root, string childName, Color color)
    {
        var found = FindRecursive(root, childName);
        if (found == null) return;
        var img = found.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.color = color;
    }

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

    [MenuItem("Tools/Quest/Apply Yellow Toast Style")]
    public static void ApplyYellowToastStyle()
    {
        string toastPath = $"{PrefabFolder}/ToastNotification.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(toastPath);
        if (prefab == null)
        {
            Debug.LogError($"[QuestPrefabBuilder] 토스트 prefab 못 찾음: {toastPath}");
            return;
        }

        ApplyToastStyleToObject(prefab);
        PrefabUtility.SaveAsPrefabAsset(prefab, toastPath);
        PrefabUtility.UnloadPrefabContents(prefab);

        // 씬 인스턴스도 같이 업데이트 (override 덮어쓰기)
        int sceneInstancesUpdated = 0;
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var rootGO in activeScene.GetRootGameObjects())
        {
            var instances = rootGO.GetComponentsInChildren<ToastNotification>(true);
            foreach (var inst in instances)
            {
                ApplyToastStyleToObject(inst.gameObject);
                EditorUtility.SetDirty(inst.gameObject);
                sceneInstancesUpdated++;
            }
        }
        if (sceneInstancesUpdated > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[QuestPrefabBuilder] 토스트 노란 스타일 적용 완료.\n" +
            "  - Background: 골든 옐로우 (#F5C842), 불투명\n" +
            "  - Label: 검정 + Bold, 13pt, 좌측 22px (Icon 자리 확보)\n" +
            "  - 박스 크기: 200x24 (얇고 길게, 엔드필드 패턴)\n" +
            "  - Icon: 14x14 좌측 4px\n" +
            $"  - 씬 인스턴스 {sceneInstancesUpdated}개 업데이트");
    }

    /// <summary>
    /// 토스트 prefab/인스턴스 둘 다에 동일 스타일 적용. 이름으로 자식 찾기.
    /// Icon은 박스 안 좌측 (분리된 GameObject지만 시각적으로 박스 안에 위치).
    /// Background는 각진 직사각형 (PPU multiplier로 9-slice 모서리 줄임).
    /// Box는 root 전체 stretch (Icon 자리 분리 X, Icon이 박스 위에 겹쳐 그려짐).
    /// </summary>
    static void ApplyToastStyleToObject(GameObject root)
    {
        if (root == null) return;

        var rootRT = root.GetComponent<RectTransform>();
        if (rootRT != null) rootRT.sizeDelta = ToastSize;

        // Box: root 전체 stretch (Icon이 박스 위에 겹쳐 그려지도록)
        var box = FindRecursive(root.transform, "Box");
        if (box != null)
        {
            var boxRT = box.GetComponent<RectTransform>();
            if (boxRT != null)
            {
                boxRT.anchorMin = new Vector2(0f, 0f);
                boxRT.anchorMax = new Vector2(1f, 1f);
                boxRT.pivot = new Vector2(0.5f, 0.5f);
                boxRT.offsetMin = Vector2.zero;
                boxRT.offsetMax = Vector2.zero;
            }
        }

        // Background: 노란 + 좌→우 알파 그라데이션 sprite (우측 페이드 효과)
        var bg = FindRecursive(root.transform, "Background");
        if (bg != null)
        {
            var img = bg.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.color = ToastYellow;
                var fadeSprite = GetOrCreateRightFadeSprite();
                if (fadeSprite != null)
                {
                    img.sprite = fadeSprite;
                    img.type = UnityEngine.UI.Image.Type.Simple;
                }
            }
        }

        // Icon: 14x14, 박스 안 좌측 (시각적으로 박스 안). 분리 구조 유지.
        var icon = FindRecursive(root.transform, "Icon");
        if (icon != null)
        {
            var iconRT = icon.GetComponent<RectTransform>();
            if (iconRT != null)
            {
                iconRT.anchorMin = new Vector2(0f, 0.5f);
                iconRT.anchorMax = new Vector2(0f, 0.5f);
                iconRT.pivot = new Vector2(0f, 0.5f);
                iconRT.anchoredPosition = new Vector2(ToastIconX, 0f);
                iconRT.sizeDelta = new Vector2(14f, 14f);
            }
            // 렌더 순서: Icon이 Box보다 위에 그려지도록 sibling 마지막으로
            icon.SetAsLastSibling();
        }

        // Label: 검정 Bold 17pt, Box 안 좌측 padding 22 (Icon 자리 확보)
        var label = FindRecursive(root.transform, "Label");
        if (label != null)
        {
            var tmp = label.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = Color.black;
                tmp.fontStyle |= TMPro.FontStyles.Bold;
                tmp.fontSize = 17;
            }

            var labelRT = label.GetComponent<RectTransform>();
            if (labelRT != null)
            {
                labelRT.anchorMin = new Vector2(0f, 0f);
                labelRT.anchorMax = new Vector2(1f, 1f);
                labelRT.pivot = new Vector2(0.5f, 0.5f);
                labelRT.offsetMin = new Vector2(22f, 0f);
                labelRT.offsetMax = new Vector2(-8f, 0f);
            }
        }
    }

    const float QuestTitleHeight = 44f;   // 토스트(26)보다 세로 크게, 위아래 공백 생김
    const float QuestIconSize = 30f;      // IconAlert(!!!) 크기 - 박스 안에 들어가도록
    const float QuestNormalIconSize = 40f; // IconNormal(?) 크기 - 따로 조정 가능
    const float QuestTitleFontSize = 24f; // 내용 텍스트(18)보다 큼
    const float ObjectiveLineHeight = 32f;   // 체크박스 줄 높이
    const float ObjectiveLineFontSize = 18f; // 체크박스 줄 텍스트

    [MenuItem("Tools/Quest/Apply Quest Title Style")]
    public static void ApplyQuestTitleStyle()
    {
        // 1. QuestEntry prefab 업데이트
        string qePath = $"{PrefabFolder}/QuestEntry.prefab";
        var qePrefab = PrefabUtility.LoadPrefabContents(qePath);
        if (qePrefab == null)
        {
            Debug.LogError($"[QuestPrefabBuilder] QuestEntry prefab 못 찾음: {qePath}");
            return;
        }
        ApplyQuestEntryStyle(qePrefab);
        PrefabUtility.SaveAsPrefabAsset(qePrefab, qePath);
        PrefabUtility.UnloadPrefabContents(qePrefab);

        // 2a. ObjectiveLine prefab: 줄 높이 + 텍스트 크기 키움
        string olPath = $"{PrefabFolder}/ObjectiveLine.prefab";
        var olPrefab = PrefabUtility.LoadPrefabContents(olPath);
        if (olPrefab != null)
        {
            var olRT = olPrefab.GetComponent<RectTransform>();
            if (olRT != null) olRT.sizeDelta = new Vector2(olRT.sizeDelta.x, ObjectiveLineHeight);

            var olLE = olPrefab.GetComponent<UnityEngine.UI.LayoutElement>();
            if (olLE == null) olLE = olPrefab.AddComponent<UnityEngine.UI.LayoutElement>();
            olLE.preferredHeight = ObjectiveLineHeight;

            var olLabel = FindRecursive(olPrefab.transform, "Label");
            if (olLabel != null)
            {
                var tmp = olLabel.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.fontSize = ObjectiveLineFontSize;
            }

            PrefabUtility.SaveAsPrefabAsset(olPrefab, olPath);
            PrefabUtility.UnloadPrefabContents(olPrefab);
        }

        // 2b. QuestPanelUI prefab: 좌측 X=0 + CategoryRoot 좌측 padding 0 (왼쪽 공백 제거)
        string qpPath = $"{PrefabFolder}/QuestPanelUI.prefab";
        var qpPrefab = PrefabUtility.LoadPrefabContents(qpPath);
        if (qpPrefab != null)
        {
            var qpRT = qpPrefab.GetComponent<RectTransform>();
            if (qpRT != null)
            {
                var pos = qpRT.anchoredPosition;
                qpRT.anchoredPosition = new Vector2(0f, pos.y);
            }
            ApplyQuestPanelPadding(qpPrefab);
            PrefabUtility.SaveAsPrefabAsset(qpPrefab, qpPath);
            PrefabUtility.UnloadPrefabContents(qpPrefab);
        }

        // 3. 씬 인스턴스: QuestPanelUI 인스턴스의 X 위치 0 + CategoryRoot padding 처리
        int sceneUpdated = 0;
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var rootGO in activeScene.GetRootGameObjects())
        {
            var panels = rootGO.GetComponentsInChildren<QuestPanelUI>(true);
            foreach (var p in panels)
            {
                // QuestPanelUI 자체 X 위치 0 (화면 좌측 끝까지)
                var prt = p.GetComponent<RectTransform>();
                if (prt != null)
                {
                    var pos = prt.anchoredPosition;
                    prt.anchoredPosition = new Vector2(0f, pos.y);
                }
                ApplyQuestPanelPadding(p.gameObject);
                EditorUtility.SetDirty(p.gameObject);
                sceneUpdated++;
            }
        }
        if (sceneUpdated > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[QuestPrefabBuilder] 퀘스트 제목 스타일 적용 완료.\n" +
            $"  - Background: 검정 alpha 0.7 + 좌→우 페이드 sprite, 세로 {QuestTitleHeight}px (토스트보다 큼)\n" +
            "  - Title: 세로 가운데 정렬, 위아래 공백 자동\n" +
            $"  - IconAlert: quest_marker_256.png 적용, 박스 가운데 정렬, {QuestIconSize}x{QuestIconSize}\n" +
            "  - QuestPanelUI.CategoryRoot 좌측 padding 0 (왼쪽 공백 제거)\n" +
            $"  - 씬 QuestPanelUI 인스턴스 {sceneUpdated}개 업데이트");
    }

    static void ApplyQuestEntryStyle(GameObject root)
    {
        // root: 부모 VLG 무시 (ignoreLayout) + 좌상단 stretch로 절대 위치 고정.
        // 자식 수에 무관하게 Title 화면 위치 유지.
        var rootLE = root.GetComponent<UnityEngine.UI.LayoutElement>();
        if (rootLE == null) rootLE = root.AddComponent<UnityEngine.UI.LayoutElement>();
        rootLE.ignoreLayout = true;

        var rootRT = root.GetComponent<RectTransform>();
        if (rootRT != null)
        {
            rootRT.anchorMin = new Vector2(0f, 1f);
            rootRT.anchorMax = new Vector2(1f, 1f);
            rootRT.pivot = new Vector2(0f, 1f);
            rootRT.anchoredPosition = Vector2.zero;
        }

        // Background: 검정 alpha 0.7 + fade sprite + 세로 28px (토스트보다 큰 박스)
        var bg = FindRecursive(root.transform, "Background");
        if (bg != null)
        {
            var img = bg.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.color = new Color(0f, 0f, 0f, 0.7f);
                var fadeSprite = GetOrCreateRightFadeSprite();
                if (fadeSprite != null)
                {
                    img.sprite = fadeSprite;
                    img.type = UnityEngine.UI.Image.Type.Simple;
                }
            }
            var bgRT = bg.GetComponent<RectTransform>();
            if (bgRT != null)
            {
                bgRT.anchorMin = new Vector2(0f, 1f);
                bgRT.anchorMax = new Vector2(1f, 1f);
                bgRT.pivot = new Vector2(0f, 1f);
                bgRT.anchoredPosition = Vector2.zero;
                bgRT.sizeDelta = new Vector2(0f, QuestTitleHeight);
            }
        }

        // Title: 폰트 크게 + ignoreLayout으로 절대 위치 고정 (ObjectiveList 항목 수와 무관)
        var title = FindRecursive(root.transform, "Title");
        if (title != null)
        {
            var tmp = title.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                tmp.fontSize = QuestTitleFontSize;
                tmp.fontStyle |= TMPro.FontStyles.Bold;
                tmp.margin = new Vector4(QuestTitleLeftPad, 0f, 4f, 0f);   // Icon 자리 + 여백
            }

            var titleLE = title.GetComponent<UnityEngine.UI.LayoutElement>();
            if (titleLE == null) titleLE = title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            titleLE.ignoreLayout = true;

            var titleRT = title.GetComponent<RectTransform>();
            if (titleRT != null)
            {
                titleRT.anchorMin = new Vector2(0f, 1f);
                titleRT.anchorMax = new Vector2(1f, 1f);
                titleRT.pivot = new Vector2(0f, 1f);
                titleRT.anchoredPosition = Vector2.zero;
                titleRT.sizeDelta = new Vector2(0f, QuestTitleHeight);
            }
        }

        // TextContent VLG padding-top: Title 자리(ignoreLayout) + 제목↔내용 spacing
        var textContent = FindRecursive(root.transform, "TextContent");
        if (textContent != null)
        {
            var vlg = textContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg != null)
            {
                var p = vlg.padding;
                vlg.padding = new RectOffset(p.left, p.right, (int)(QuestTitleHeight + TitleToContentSpacing), p.bottom);
            }
        }

        // ObjectiveList padding-left: 체크박스 X를 Title Icon X(QuestIconX)와 정렬
        var objectiveList = FindRecursive(root.transform, "ObjectiveList");
        if (objectiveList != null)
        {
            var vlg = objectiveList.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg != null)
            {
                var p = vlg.padding;
                vlg.padding = new RectOffset((int)QuestIconX, p.right, p.top, p.bottom);
            }
        }

        // IconNormal: 박스 위에 sibling 마지막, 박스 세로 가운데 정렬 + marker_2.png 적용
        var iconNormal = FindRecursive(root.transform, "IconNormal");
        if (iconNormal != null)
        {
            iconNormal.SetAsLastSibling();
            SetIconToBoxCenter(iconNormal, QuestNormalIconSize);

            var normalSprite = EnsureSpriteImport("Assets/12.VFX/UI Effect/target_marker_256.png");
            if (normalSprite != null)
            {
                var img = iconNormal.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.sprite = normalSprite;
                    img.preserveAspect = true;
                }
            }
        }

        // IconAlert: 동일 위치 + quest_marker sprite 적용 (1 우선, 256 fallback)
        var iconAlert = FindRecursive(root.transform, "IconAlert");
        if (iconAlert != null)
        {
            iconAlert.SetAsLastSibling();
            SetIconToBoxCenter(iconAlert, QuestIconSize);

            // quest_marker_1 우선 (textureType=Default여도 Sprite로 자동 변환)
            var marker = EnsureSpriteImport("Assets/12.VFX/UI Effect/quest_marker_1.png");
            if (marker == null) marker = EnsureSpriteImport("Assets/12.VFX/UI Effect/quest_marker_256.png");

            if (marker != null)
            {
                var img = iconAlert.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.sprite = marker;
                    img.preserveAspect = true;
                }
            }
        }
    }

    const float QuestIconX = 50f;       // 박스 좌측 공백 (Icon 시작 X) = 체크박스 X 정렬 기준
    const float QuestTitleLeftPad = 100f; // Title 텍스트 좌측 padding = label 텍스트 X 정렬 기준
    const float TitleToContentSpacing = 18f; // 제목 박스 ↔ 퀘스트 내용 사이 간격

    /// <summary>Icon을 박스 세로 가운데에 정렬. pivot (0, 0.5) 좌중앙 + Y = 박스 height/2.</summary>
    static void SetIconToBoxCenter(Transform icon, float size)
    {
        var iconRT = icon.GetComponent<RectTransform>();
        if (iconRT == null) return;
        iconRT.anchorMin = new Vector2(0f, 1f);
        iconRT.anchorMax = new Vector2(0f, 1f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(QuestIconX, -QuestTitleHeight / 2f);
        iconRT.sizeDelta = new Vector2(size, size);
    }

    /// <summary>
    /// 텍스처 파일의 import type을 Sprite로 강제 변경 후 Sprite 로드.
    /// Unity 디폴트 import가 Default texture이면 Sprite 로드 실패하니까 자동 변환.
    /// </summary>
    static Sprite EnsureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ApplyQuestPanelPadding(GameObject root)
    {
        var crRoot = FindRecursive(root.transform, "CategoryRoot");
        if (crRoot != null)
        {
            var vlg = crRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg != null) vlg.padding = new RectOffset(0, 8, 8, 8);
        }
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

    // QuestIconX는 위쪽 ApplyQuestTitleStyle 섹션에서 정의 (값 10f)

    static string BuildQuestEntry(string objectiveLinePath)
    {
        GameObject root = MakeUI("QuestEntry");
        var rt = root.GetComponent<RectTransform>();
        // root 자체를 부모 좌상단 stretch로 박음 + ignoreLayout으로 부모 VLG 무시.
        // ObjectiveList 항목 수에 따라 root height가 변동해도 Title 화면 위치 고정.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 40f);

        var rootLE = root.AddComponent<LayoutElement>();
        rootLE.ignoreLayout = true;

        // VLG + CSF: TextContent wrapper만 자식. Background/Icon은 ignoreLayout으로 별도 배치.
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

        const float titleLineHeight = 22f;

        // Background: Title 라인 검정 박스. 좌측 끝부터 우측까지 풀 width. ignoreLayout.
        // 가장 먼저 추가되어 sibling 최상위 = 가장 뒤에 그려짐. Icon들은 그 위에 겹쳐 그려짐.
        // 토스트와 동일한 좌→우 페이드 sprite 적용 (검정 색).
        var bgGO = MakeUI("Background", root.transform);
        var bgLE = bgGO.AddComponent<LayoutElement>();
        bgLE.ignoreLayout = true;
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 1f);
        bgRT.anchorMax = new Vector2(1f, 1f);
        bgRT.pivot = new Vector2(0f, 1f);
        bgRT.anchoredPosition = Vector2.zero;
        bgRT.sizeDelta = new Vector2(0f, titleLineHeight);   // 좌우 stretch, 높이 22
        var bgImg = AddImage(bgGO, new Color(0f, 0f, 0f, 0.7f));
        var qeFadeSprite = GetOrCreateRightFadeSprite();
        if (qeFadeSprite != null)
        {
            bgImg.sprite = qeFadeSprite;
            bgImg.type = Image.Type.Simple;
        }

        // TextContent: Title + ObjectiveList wrapper. CanvasGroup으로 fade in 단위. VLG 자식.
        // padding-top = Title height + spacing (Title이 ignoreLayout이라 VLG가 무시하므로 자리 확보용)
        var textGO = MakeUI("TextContent", root.transform);
        var textCG = textGO.AddComponent<CanvasGroup>();
        var textVLG = textGO.AddComponent<VerticalLayoutGroup>();
        textVLG.padding = new RectOffset(0, 0, (int)(QuestTitleHeight + 4f), 0);  // top = Title 자리
        textVLG.spacing = 4f;
        textVLG.childControlWidth = true;
        textVLG.childControlHeight = true;
        textVLG.childForceExpandWidth = true;
        textVLG.childForceExpandHeight = false;
        var textCSF = textGO.AddComponent<ContentSizeFitter>();
        textCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title (TextContent 자식, **ignoreLayout으로 절대 위치 고정**).
        // ObjectiveList 항목 수에 무관하게 Title 화면 위치 고정.
        var titleGO = MakeUI("Title", textGO.transform);
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.ignoreLayout = true;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0f, 1f);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta = new Vector2(0f, QuestTitleHeight);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(title, "Quest Title", QuestTitleFontSize, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        title.margin = new Vector4(22f, 0f, 4f, 0f);

        // ObjectiveList (TextContent 자식). 체크박스 들여쓰기.
        var listGO = MakeUI("ObjectiveList", textGO.transform);
        var listVLG = listGO.AddComponent<VerticalLayoutGroup>();
        listVLG.padding = new RectOffset(12, 2, 0, 0);
        listVLG.spacing = 2f;
        listVLG.childControlWidth = true;
        listVLG.childControlHeight = true;
        listVLG.childForceExpandWidth = true;
        listVLG.childForceExpandHeight = false;
        var listCSF = listGO.AddComponent<ContentSizeFitter>();
        listCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // IconNormal: 박스 안 좌측 (분리된 GameObject지만 시각적으로 박스 안). 14x14.
        // root 자식, sibling 순서상 Background 뒤에 추가 = Background 위에 겹쳐 그려짐.
        var iconNormalGO = MakeUI("IconNormal", root.transform);
        var iconNormalLE = iconNormalGO.AddComponent<LayoutElement>();
        iconNormalLE.ignoreLayout = true;
        SetAnchor(iconNormalGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(QuestIconX, -4f), new Vector2(14f, 14f));
        AddImage(iconNormalGO, Color.white);
        iconNormalGO.AddComponent<CanvasGroup>();

        // IconAlert (!!!): Normal과 동일 위치, 평상시 비활성.
        var iconAlertGO = MakeUI("IconAlert", root.transform);
        var iconAlertLE = iconAlertGO.AddComponent<LayoutElement>();
        iconAlertLE.ignoreLayout = true;
        SetAnchor(iconAlertGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(QuestIconX, -4f), new Vector2(14f, 14f));
        AddImage(iconAlertGO, Color.white);
        iconAlertGO.AddComponent<CanvasGroup>();
        iconAlertGO.SetActive(false);

        // ----- 컴포넌트 + 슬롯 -----
        var qe = root.AddComponent<QuestEntry>();
        SetField(qe, "title", title);
        SetField(qe, "objectiveList", listGO.transform);
        SetField(qe, "iconNormal", iconNormalGO);
        SetField(qe, "iconAlert", iconAlertGO);
        SetField(qe, "audioSource", audio);
        SetField(qe, "backgroundBox", bgRT);
        SetField(qe, "textGroup", textCG);
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

    // 엔드필드 패턴: 노란 골든 배경 + 검정 굵은 텍스트, 얇고 길게.
    static readonly Color ToastYellow = new Color(245f / 255f, 200f / 255f, 66f / 255f, 1f);
    static readonly Vector2 ToastSize = new Vector2(260f, 26f);   // 30% 키움

    /// <summary>
    /// sweep용 글로우 sprite 자동 생성. 양쪽 fade + 가운데 진함 (sin curve).
    /// windowlight 효과 비슷하지만 흰색 + alpha만 (검정 픽셀 없음 → tint 잘 받음).
    /// </summary>
    static Sprite GetOrCreateSweepGlowSprite()
    {
        string path = "Assets/05.Prefabs/Quest/_SweepGlow.png";

        const int W = 256;
        const int H = 16;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var pixels = new Color[W * H];
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);
            // sin curve: 0 → 1 → 0 (가운데 진함, 양쪽 fade out)
            float a = Mathf.Sin(t * Mathf.PI);
            for (int y = 0; y < H; y++) pixels[y * W + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// 좌→우 알파 그라데이션 sprite 자동 생성. 박스 우측 페이드 효과용.
    /// 매번 재생성 (곡선 변경 시 자동 반영). 끝쪽만 약하게 페이드 (1 - t^8).
    /// </summary>
    static Sprite GetOrCreateRightFadeSprite()
    {
        string path = "Assets/05.Prefabs/Quest/_RightFade.png";

        // 텍스처 생성 + PNG 저장 (매번 새로)
        const int W = 256;
        const int H = 8;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var pixels = new Color[W * H];
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);
            // 끝쪽만 약하게 페이드. t<0.7 거의 1 (균일), t>0.85부터 빠르게 0.
            float a = 1f - Mathf.Pow(t, 8f);
            for (int y = 0; y < H; y++) pixels[y * W + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // 동기 import (안 그러면 LoadAssetAtPath가 null 반환)
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    const float ToastIconX = 4f;   // 박스 안 좌측 Icon 위치

    static string BuildToastNotification()
    {
        GameObject root = MakeUI("ToastNotification");
        var rt = root.GetComponent<RectTransform>();
        // 좌상단 앵커, 패널(8,-120) 위쪽에 위치. 얇고 길게.
        SetAnchor(rt,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -90f), ToastSize);

        var cg = root.AddComponent<CanvasGroup>();

        // Box: root 전체 stretch. 노란 단색 배경.
        // (먼저 추가되어 sibling 0 = 가장 뒤에 그려짐 = Icon이 위에 겹쳐 그려짐)
        var boxGO = MakeUI("Box", root.transform);
        var boxRT = boxGO.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0f, 0f);
        boxRT.anchorMax = new Vector2(1f, 1f);
        boxRT.pivot = new Vector2(0.5f, 0.5f);
        boxRT.offsetMin = Vector2.zero;
        boxRT.offsetMax = Vector2.zero;

        // Background: Box 안 stretch, 노란 + 우측 페이드 그라데이션 sprite.
        var bgGO = MakeUI("Background", boxGO.transform);
        SetStretch(bgGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var bgImg = AddImage(bgGO, ToastYellow);
        var fadeSprite = GetOrCreateRightFadeSprite();
        if (fadeSprite != null)
        {
            bgImg.sprite = fadeSprite;
            bgImg.type = Image.Type.Simple;
        }

        // Label: Box 안, 좌측 padding 22 (Icon 자리 확보: 4 + 14 + 4 = 22)
        var labelGO = MakeUI("Label", boxGO.transform);
        SetStretch(labelGO.GetComponent<RectTransform>(), 22f, 8f, 0f, 0f);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        ConfigureTMP(label, "업데이트 완료", 13, FontStyles.Bold, TextAlignmentOptions.Left);
        label.color = Color.black;

        // Icon: 박스 안 좌측 (분리된 GameObject지만 시각적으로 박스 안에 위치).
        // root 자식으로 sibling 마지막 = Box 위에 그려짐.
        var iconGO = MakeUI("Icon", root.transform);
        SetAnchor(iconGO.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(ToastIconX, 0f), new Vector2(14f, 14f));
        var iconImg = AddImage(iconGO, Color.white);

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
