using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EnemyWorldUI prefab 자동 생성 + 활성 씬의 적 GameObject 자식으로 부착.
/// 메뉴: Tools > Enemy > Build World HP Bar
///       Tools > Enemy > Attach HP Bar To Selected
/// </summary>
public static class EnemyWorldUIBuilder
{
    const string PrefabPath = "Assets/05.Prefabs/Enemy/HP_Bar_World.prefab";

    // [HIDDEN] [MenuItem("Tools/TIMEKOV/적/월드 체력바 프리팹 생성")]
    public static void BuildPrefab()
    {
        EnsureFolder("Assets/05.Prefabs", "Enemy");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            bool ok = EditorUtility.DisplayDialog(
                "HP Bar prefab 덮어쓰기",
                $"{PrefabPath}이 이미 있습니다.\n덮어쓰면 인스펙터 조정값 사라집니다.\n계속?",
                "덮어쓰기", "취소");
            if (!ok) return;
        }

        // Root: WorldSpace Canvas
        var root = new GameObject("HP_Bar_World", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        var cg = root.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var rootRT = root.GetComponent<RectTransform>();
        // WorldSpace Canvas: sizeDelta는 픽셀 단위, scale로 World 크기 변환.
        // 200x60 * 0.01 = World 2m x 0.6m
        rootRT.sizeDelta = new Vector2(200f, 60f);
        root.transform.localScale = Vector3.one * 0.01f;

        // Name Text (위쪽 절반)
        var nameGO = MakeChild("Name", root.transform);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.5f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = "Enemy";
        nameText.fontSize = 22f;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        var korFont = TryGetKoreanFont();
        if (korFont != null) nameText.font = korFont;

        // HP Slider (아래쪽 절반)
        var sliderGO = MakeChild("HP_Slider", root.transform);
        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0f, 0f);
        sliderRT.anchorMax = new Vector2(1f, 0.5f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 100f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        // Slider Background (MilitaryUI Black_bar sprite + sliced 9-slice)
        var bgGO = MakeChild("Background", sliderGO.transform);
        SetStretch(bgGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.white;   // sprite 원본 색 유지
        bgImg.raycastTarget = false;
        var bgSprite = LoadSprite("Assets/MilitaryUI/Artworks/UI_parts/Black_bar1.png");
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.type = Image.Type.Sliced;
        }
        else
        {
            bgImg.color = new Color(0f, 0f, 0f, 0.7f);   // fallback 검정 단색
        }

        // Slider Fill Area + Fill (좌우 2px padding)
        var fillAreaGO = MakeChild("Fill Area", sliderGO.transform);
        SetStretch(fillAreaGO.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);

        var fillGO = MakeChild("Fill", fillAreaGO.transform);
        SetStretch(fillGO.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.85f, 0.1f, 0.15f, 1f);
        fillImg.raycastTarget = false;

        slider.fillRect = fillGO.GetComponent<RectTransform>();
        slider.targetGraphic = fillImg;
        slider.direction = Slider.Direction.LeftToRight;

        // EnemyWorldUI 컴포넌트
        var wui = root.AddComponent<EnemyWorldUI>();
        SetField(wui, "canvasGroup", cg);
        SetField(wui, "hpSlider", slider);
        SetField(wui, "nameText", nameText);

        // Save prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[EnemyWorldUI] HP Bar prefab 생성: {PrefabPath}\n" +
            $"다음 단계: Tools > Enemy > Attach HP Bar To Selected 메뉴로 적 GameObject 자식에 자동 부착\n" +
            $"또는 prefab을 적 GameObject 자식으로 드래그 (EnemyHealth.enemyWorldUI 슬롯에 자동 연결됨)");

        if (prefab != null)
            EditorGUIUtility.PingObject(prefab);
    }

    // [HIDDEN] [MenuItem("Tools/TIMEKOV/적/체력바 붙이기 (선택 항목)")]
    public static void AttachToSelected()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[EnemyWorldUI] HP Bar prefab 없음. 먼저 Build World HP Bar Prefab 메뉴 실행.");
            return;
        }

        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("[EnemyWorldUI] 선택된 GameObject 없음. Hierarchy에서 적 GameObject 선택 후 다시 실행.");
            return;
        }

        int attached = 0;
        foreach (var t in targets)
        {
            var health = t.GetComponent<EnemyHealth>();
            if (health == null)
            {
                Debug.LogWarning($"[EnemyWorldUI] {t.name}에 EnemyHealth 없음. 건너뜀.");
                continue;
            }

            // 기존 자식 EnemyWorldUI 있으면 건너뜀
            if (t.GetComponentInChildren<EnemyWorldUI>(true) != null)
            {
                Debug.Log($"[EnemyWorldUI] {t.name}에 이미 HP Bar 있음. 건너뜀.");
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t.transform);
            instance.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            instance.transform.localRotation = Quaternion.identity;

            // EnemyHealth.enemyWorldUI 슬롯에 연결 (private SerializeField라 SerializedObject 사용)
            var so = new SerializedObject(health);
            var prop = so.FindProperty("enemyWorldUI");
            if (prop != null)
            {
                prop.objectReferenceValue = instance.GetComponent<EnemyWorldUI>();
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(t);
            attached++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[EnemyWorldUI] HP Bar 부착 완료. {attached}개 적에 부착.");
    }

    // ===== helpers =====

    static GameObject MakeChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static void SetField(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null)
        {
            Debug.LogError($"[EnemyWorldUI] Field 못찾음: {obj.GetType().Name}.{fieldName}");
            return;
        }
        f.SetValue(obj, value);
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(parent, name);
    }

    static Sprite LoadSprite(string path)
    {
        // textureType이 Default여도 Sprite로 자동 변환 후 로드
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

    static TMP_FontAsset TryGetKoreanFont()
    {
        string[] candidates =
        {
            "Assets/11.Font/Pretendard-ExtraBold SDF.asset",
            "Assets/11.Font/남양주고딕Light (OTF) SDF.asset",
            "Assets/11.Font/GabiaMaeumgyeol SDF.asset",
        };
        foreach (var path in candidates)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f != null) return f;
        }
        return TMP_Settings.defaultFontAsset;
    }
}
