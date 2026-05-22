// MinimapBuilder.cs
// 미니맵 씬 세팅 자동화
// Menu: Tools/UI/Setup Minimap In Scene
// ─────────────────────────────────────────────────────────────────────
// 실행 시 자동으로 수행하는 작업:
//   1. RenderTexture (256×256) 에셋 생성   → Assets/05.Textures/MinimapRT.renderTexture
//   2. 원형 마스크·화살표 스프라이트 생성   → Assets/05.Textures/Minimap_*.png
//   3. MinimapCamera GameObject 생성       → 씬 루트 / MinimapController 자동 부착
//   4. Canvas(ScreenSpaceOverlay) 탐색/생성 후 미니맵 UI 계층 구성
//   5. MinimapController · MinimapUI 필드 자동 연결
// ─────────────────────────────────────────────────────────────────────

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MinimapBuilder
{
    // ── 경로 상수 ────────────────────────────────────────────────────
    const string kTextureFolder = "Assets/05.Textures";
    const string kCirclePath    = "Assets/05.Textures/Minimap_Circle.png";
    const string kArrowPath     = "Assets/05.Textures/Minimap_Arrow.png";
    const string kRtPath        = "Assets/05.Textures/MinimapRT.renderTexture";

    // MilitaryUI 에 있는 화살표 스프라이트 (있으면 플레이어 아이콘으로 우선 사용)
    const string kArrowFallback = "Assets/MilitaryUI/Artworks/Action_icons/Arrow1.png";

    // 커스텀 미니맵 프레임 스프라이트
    const string kFramePath = "Assets/14.Textures/MiniMap_Frame.png";

    const float kPanelSize = 200f;
    const float kPadding   = 10f;

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/UI/Apply Minimap Frame")]
    public static void ApplyFrame()
    {
        var frameSprite = LoadAsSprite(kFramePath);
        if (frameSprite == null)
        {
            Debug.LogError($"[MinimapBuilder] 프레임 스프라이트를 찾을 수 없음: {kFramePath}");
            return;
        }

        var rimGO = GameObject.Find("Minimap_Rim");
        if (rimGO == null)
        {
            Debug.LogError("[MinimapBuilder] Minimap_Rim 오브젝트가 씬에 없음. 먼저 Setup Minimap을 실행하세요.");
            return;
        }

        var img = rimGO.GetComponent<Image>();
        if (img == null) img = rimGO.AddComponent<Image>();

        Undo.RecordObject(img, "Apply Minimap Frame");
        img.sprite        = frameSprite;
        img.color         = Color.white;   // 원본 색상 그대로
        img.raycastTarget = false;
        img.maskable      = false;

        // 프레임이 미니맵보다 살짝 크게 (자연스러운 테두리 효과)
        var rt = rimGO.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Apply Minimap Frame Size");
        rt.sizeDelta = new Vector2(kPanelSize + 20f, kPanelSize + 20f);

        EditorUtility.SetDirty(rimGO);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorGUIUtility.PingObject(rimGO);
        Debug.Log("[MinimapBuilder] ✓ 미니맵 프레임 적용 완료! (Minimap_Rim)");
    }

    // ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/UI/Setup Minimap In Scene")]
    public static void SetupMinimap()
    {
        // 기존 오브젝트 중복 방지
        var existingPanel = GameObject.Find("Minimap_Panel");
        if (existingPanel != null)
        {
            bool ok = EditorUtility.DisplayDialog(
                "미니맵 이미 존재",
                "씬에 Minimap_Panel이 이미 있습니다.\n삭제하고 새로 생성할까요?",
                "재생성", "취소");
            if (!ok) return;
            Undo.DestroyObjectImmediate(existingPanel);
        }
        var existingCam = GameObject.Find("MinimapCamera");
        if (existingCam != null) Undo.DestroyObjectImmediate(existingCam);

        // ── 1. 에셋 준비 ──────────────────────────────────────────
        EnsureFolder(kTextureFolder);
        Sprite circleSprite = GetOrCreateCircleSprite();
        Sprite arrowSprite  = GetOrCreateArrowSprite();
        RenderTexture rt    = GetOrCreateRenderTexture();

        // ── 2. MinimapCamera ──────────────────────────────────────
        GameObject camObj = BuildMinimapCamera(rt);

        // ── 3. Canvas 탐색 / UI 구성 ──────────────────────────────
        Canvas canvas = FindOrCreateOverlayCanvas();
        var (panel, iconContainer, playerIconGO) =
            BuildMinimapPanel(canvas, circleSprite, arrowSprite, rt);

        // ── 4. 컴포넌트 필드 연결 ─────────────────────────────────
        var controller = camObj.GetComponent<MinimapController>();
        SetField(controller, "_minimapCamera", camObj.GetComponent<Camera>());
        SetField(controller, "_playerIcon",    playerIconGO.GetComponent<RectTransform>());

        var minimapUI = panel.GetComponent<MinimapUI>();
        SetField(minimapUI, "_minimapCamera", camObj.GetComponent<Camera>());
        SetField(minimapUI, "_iconContainer", iconContainer.GetComponent<RectTransform>());

        // ── 5. 씬 저장 처리 ───────────────────────────────────────
        Undo.RegisterCreatedObjectUndo(camObj, "Create MinimapCamera");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorGUIUtility.PingObject(panel);
        Selection.activeGameObject = panel;

        Debug.Log(
            "[MinimapBuilder] ✓ 미니맵 자동 세팅 완료!\n" +
            "  • MinimapCamera  — 씬 루트에 추가됨 (MinimapController 부착)\n" +
            "  • Minimap_Panel  — Canvas 하위에 추가됨 (MinimapUI 부착)\n" +
            "  • MinimapRT      — Assets/05.Textures/MinimapRT.renderTexture\n\n" +
            "다음 단계: Ctrl+S 로 씬 저장\n" +
            "적에게 MinimapMarker 컴포넌트 추가 시 자동으로 미니맵에 표시됩니다."
        );
    }

    // ═════════════════════════════════════════════════════════════════
    // MinimapCamera 생성
    // ═════════════════════════════════════════════════════════════════
    static GameObject BuildMinimapCamera(RenderTexture rt)
    {
        var go  = new GameObject("MinimapCamera");
        var cam = go.AddComponent<Camera>();

        cam.orthographic      = true;
        cam.orthographicSize  = 20f;
        cam.clearFlags        = CameraClearFlags.SolidColor;
        cam.backgroundColor   = new Color(0.08f, 0.1f, 0.12f, 1f); // 어두운 배경
        cam.cullingMask       = ~LayerMask.GetMask("UI");            // UI 레이어 제외
        cam.targetTexture     = rt;
        cam.depth             = -2;                                   // 메인 카메라보다 먼저 렌더링
        cam.nearClipPlane     = 0.1f;
        cam.farClipPlane      = 200f;

        // 플레이어 위에서 내려다보는 초기 위치/각도 (MinimapController가 매 프레임 갱신)
        go.transform.position = new Vector3(0f, 30f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // MinimapController 부착 (필드는 이후 연결)
        go.AddComponent<MinimapController>();

        return go;
    }

    // ═════════════════════════════════════════════════════════════════
    // 미니맵 UI 계층 생성
    // 반환: (panel, iconContainer, playerIconGO)
    // ═════════════════════════════════════════════════════════════════
    static (GameObject panel, GameObject iconContainer, GameObject playerIconGO)
        BuildMinimapPanel(Canvas canvas, Sprite circleSprite, Sprite arrowSprite, RenderTexture rt)
    {
        // ── MinimapPanel (마스크 루트) ─────────────────────────────
        //    왼쪽 상단 고정, 200×200
        var panel   = MakeChild("Minimap_Panel", canvas.transform);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 1f);  // 왼쪽 상단
        panelRT.anchorMax        = new Vector2(0f, 1f);
        panelRT.pivot            = new Vector2(0f, 1f);
        panelRT.anchoredPosition = new Vector2(kPadding, -kPadding);
        panelRT.sizeDelta        = new Vector2(kPanelSize, kPanelSize);

        // 원형 마스크: Image(circle) + Mask
        var maskImg = panel.AddComponent<Image>();
        maskImg.sprite        = circleSprite;
        maskImg.color         = Color.white;
        maskImg.raycastTarget = false;

        var mask = panel.AddComponent<Mask>();
        mask.showMaskGraphic = false;   // 마스크 Image 자체는 보이지 않음

        // MinimapUI 부착 (필드는 이후 연결)
        panel.AddComponent<MinimapUI>();

        // ── 배경 (진한 반투명 원) ─────────────────────────────────
        var bg    = MakeChild("Minimap_Background", panel.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite        = circleSprite;
        bgImg.color         = new Color(0.05f, 0.08f, 0.12f, 0.82f);
        bgImg.raycastTarget = false;
        SetStretch(bg.GetComponent<RectTransform>(), 0, 0, 0, 0);

        // ── RawImage (RenderTexture 표시, 3px 안쪽 = 테두리 효과) ─
        var rawGO   = MakeChild("Minimap_RawImage", panel.transform);
        var rawImg  = rawGO.AddComponent<RawImage>();
        rawImg.texture      = rt;
        rawImg.color        = Color.white;
        rawImg.raycastTarget = false;
        SetStretch(rawGO.GetComponent<RectTransform>(), 3, 3, 3, 3);

        // ── IconContainer (마커 아이콘 부모, raycast 차단 없음) ───
        var iconContGO  = MakeChild("Minimap_IconContainer", panel.transform);
        var iconContCG  = iconContGO.AddComponent<CanvasGroup>();
        iconContCG.blocksRaycasts = false;
        iconContCG.interactable   = false;
        SetStretch(iconContGO.GetComponent<RectTransform>(), 0, 0, 0, 0);

        // ── 플레이어 아이콘 (항상 중앙, MinimapController가 회전) ─
        var playerIconGO  = MakeChild("Minimap_PlayerIcon", iconContGO.transform);
        var playerIconRT  = playerIconGO.GetComponent<RectTransform>();
        playerIconRT.anchorMin        = new Vector2(0.5f, 0.5f);
        playerIconRT.anchorMax        = new Vector2(0.5f, 0.5f);
        playerIconRT.pivot            = new Vector2(0.5f, 0.5f);
        playerIconRT.anchoredPosition = Vector2.zero;
        playerIconRT.sizeDelta        = new Vector2(18f, 18f);

        var playerIconImg = playerIconGO.AddComponent<Image>();
        playerIconImg.sprite        = arrowSprite;
        playerIconImg.color         = new Color(0.25f, 0.85f, 1f, 1f);  // 하늘색
        playerIconImg.raycastTarget = false;

        // ── 테두리 링 (패널 외곽 컬러 링, 마스크 외부 — 패널의 형제) ─
        //    마스크 밖에 두어야 내용물을 가리지 않음
        var rimGO  = MakeChild("Minimap_Rim", canvas.transform);
        var rimRT  = rimGO.GetComponent<RectTransform>();
        // panel과 동일한 위치·크기
        rimRT.anchorMin        = panelRT.anchorMin;
        rimRT.anchorMax        = panelRT.anchorMax;
        rimRT.pivot            = panelRT.pivot;
        rimRT.anchoredPosition = panelRT.anchoredPosition;
        rimRT.sizeDelta        = panelRT.sizeDelta;

        var rimImg = rimGO.AddComponent<Image>();
        // 커스텀 프레임이 있으면 사용, 없으면 원형 스프라이트 fallback
        var frameSprite = LoadAsSprite(kFramePath);
        rimImg.sprite        = frameSprite != null ? frameSprite : circleSprite;
        rimImg.color         = frameSprite != null
                               ? Color.white
                               : new Color(0.35f, 0.6f, 0.85f, 0.55f);
        rimImg.raycastTarget = false;
        rimImg.maskable      = false;                                    // 마스크 영향 제외

        // 프레임이 있으면 미니맵보다 20px 크게 (테두리가 바깥으로 나오는 효과)
        if (frameSprite != null)
            rimRT.sizeDelta = new Vector2(kPanelSize + 20f, kPanelSize + 20f);

        // Rim은 panel 뒤에서 border 역할: panel 보다 먼저(아래 계층) 렌더링
        rimGO.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());

        return (panel, iconContGO, playerIconGO);
    }

    // ═════════════════════════════════════════════════════════════════
    // Canvas 탐색 / 생성
    // ═════════════════════════════════════════════════════════════════
    static Canvas FindOrCreateOverlayCanvas()
    {
        // 씬 내 Screen Space Overlay 캔버스(루트) 탐색
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay &&
                c.transform.parent == null)
                return c;
        }

        // 없으면 새 Canvas 생성
        var go     = new GameObject("Canvas_Minimap");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        Debug.LogWarning(
            "[MinimapBuilder] 씬에 ScreenSpaceOverlay Canvas가 없어 'Canvas_Minimap'을 새로 만들었습니다.\n" +
            "기존 Canvas가 있다면 Minimap_Panel을 드래그해서 해당 Canvas로 옮기세요.");
        return canvas;
    }

    // ═════════════════════════════════════════════════════════════════
    // 에셋 생성
    // ═════════════════════════════════════════════════════════════════

    // 원형 스프라이트 (마스크·배경·플레이어 아이콘 공용)
    static Sprite GetOrCreateCircleSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(kCirclePath);
        if (existing != null) return existing;

        const int size = 256;
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist  = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
            byte  alpha = (byte)(Mathf.Clamp01(radius - dist + 1.5f) * 255);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(kCirclePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(kCirclePath, ImportAssetOptions.ForceSynchronousImport);
        ApplySpriteImportSettings(kCirclePath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(kCirclePath);
    }

    // 위쪽 화살표 스프라이트 (플레이어 방향 아이콘)
    static Sprite GetOrCreateArrowSprite()
    {
        // MilitaryUI Arrow 우선 사용
        var fallback = LoadAsSprite(kArrowFallback);
        if (fallback != null) return fallback;

        // 없으면 프로그래밍 생성
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(kArrowPath);
        if (existing != null) return existing;

        const int size = 64;
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float px = x - cx;
            float py = y - cy;                        // +y = 위
            // 위쪽 뾰족한 삼각형: |px| <= halfW(py), py in [-h, h]
            float h       = size * 0.42f;
            float halfW   = size * 0.30f * ((py + h) / (2f * h));
            bool  inside  = py >= -h * 0.6f && py <= h && Mathf.Abs(px) <= halfW;
            pixels[y * size + x] = inside
                ? new Color32(255, 255, 255, 255)
                : new Color32(0, 0, 0, 0);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(kArrowPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(kArrowPath, ImportAssetOptions.ForceSynchronousImport);
        ApplySpriteImportSettings(kArrowPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(kArrowPath);
    }

    // RenderTexture (256×256)
    static RenderTexture GetOrCreateRenderTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(kRtPath);
        if (existing != null) return existing;

        var rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        rt.name         = "MinimapRT";
        rt.filterMode   = FilterMode.Bilinear;
        rt.antiAliasing = 1;
        rt.Create();

        AssetDatabase.CreateAsset(rt, kRtPath);
        AssetDatabase.SaveAssets();
        return rt;
    }

    // ═════════════════════════════════════════════════════════════════
    // 유틸 헬퍼
    // ═════════════════════════════════════════════════════════════════

    static GameObject MakeChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // offsetMin/offsetMax 기준 stretch (음수 = 확장, 양수 = 축소)
    static void SetStretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // private SerializeField 필드에 값 주입 (리플렉션)
    static void SetField(object obj, string fieldName, object value)
    {
        if (obj == null) return;
        var field = obj.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (field == null)
        {
            Debug.LogError($"[MinimapBuilder] 필드 없음: {obj.GetType().Name}.{fieldName}");
            return;
        }
        field.SetValue(obj, value);
        EditorUtility.SetDirty(obj as Object);
    }

    // 폴더 존재 보장 (중간 폴더 포함)
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts   = path.Split('/');
        string cur  = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // Sprite 임포트 설정 적용
    static void ApplySpriteImportSettings(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType         = TextureImporterType.Sprite;
        imp.spriteImportMode    = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.filterMode          = FilterMode.Bilinear;
        imp.mipmapEnabled       = false;
        imp.SaveAndReimport();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    // 기존 파일을 Sprite로 로드 (필요 시 텍스처 타입 변환)
    static Sprite LoadAsSprite(string path)
    {
        if (!File.Exists(path)) return null;
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.Sprite)
        {
            imp.textureType         = TextureImporterType.Sprite;
            imp.spriteImportMode    = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
