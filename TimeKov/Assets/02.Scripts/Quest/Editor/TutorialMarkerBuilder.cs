using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Endfield 스타일 위치 마커 자동 셋업.
/// - 노란 원 sprite 자동 생성 (없으면 .png로 저장)
/// - Canvas Screen Space Overlay 안에 Marker UI 생성
/// - TutorialMarker 컴포넌트 부착 + 슬롯 자동 연결
///
/// 메뉴: Tools > Quest > Build Tutorial Marker
/// </summary>
public static class TutorialMarkerBuilder
{
    const string SpriteFolder = "Assets/Resources/Image/UI_Quest";
    const string CirclePath = SpriteFolder + "/yellow-marker-ring.png";  // v2 — ring 형태
    const string ArrowPath = SpriteFolder + "/yellow-marker-arrow.png";
    const string ManagerName = "TutorialMarker";

    [MenuItem("Tools/Quest/Build Tutorial Marker")]
    public static void Build()
    {
        // 1. 자산 생성
        EnsureFolder(SpriteFolder);
        Sprite circleSprite = EnsureCircleSprite();
        Sprite arrowSprite = EnsureArrowSprite();

        // 2. 기존 TutorialMarker 삭제 (재실행 가능)
        var existing = GameObject.Find($"/{ManagerName}");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("덮어쓰기",
                $"기존 '{ManagerName}' 발견. 새로 생성?", "덮어쓰기", "취소")) return;
            Object.DestroyImmediate(existing);
        }

        // 3. Canvas 찾기 또는 생성 (Screen Space Overlay, 최상위)
        Canvas canvas = FindOrCreateCanvas();

        // 4. TutorialMarker 루트 (Canvas 자식)
        var markerGo = new GameObject(ManagerName, typeof(RectTransform));
        markerGo.transform.SetParent(canvas.transform, false);
        var markerRoot = markerGo.GetComponent<RectTransform>();
        markerRoot.anchorMin = markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        markerRoot.pivot = new Vector2(0.5f, 0.5f);
        markerRoot.sizeDelta = new Vector2(80, 100);  // Icon 64 + Text 30

        // 4-1. Yellow Circle (마커 본체)
        var circle = CreateUIChild(markerRoot, "Circle");
        var circleImg = circle.gameObject.AddComponent<Image>();
        circleImg.sprite = circleSprite;
        circleImg.color = new Color(1f, 0.85f, 0.1f, 1f);  // 노란색
        circleImg.raycastTarget = false;
        circle.anchorMin = circle.anchorMax = new Vector2(0.5f, 0.5f);
        circle.pivot = new Vector2(0.5f, 0.5f);
        circle.sizeDelta = new Vector2(56, 56);
        circle.anchoredPosition = new Vector2(0, 10);

        // 4-2. Inner Dot (중심 점 — Endfield 원본은 노란 작은 점)
        // Ring sprite 재사용하면 dot도 ring 모양이 되므로 별도 solid 처리: 흰 sprite + 작은 사이즈
        var dot = CreateUIChild(circle, "Dot");
        var dotImg = dot.gameObject.AddComponent<Image>();
        dotImg.color = new Color(1f, 0.85f, 0.1f, 1f);  // 노란색
        dotImg.raycastTarget = false;
        dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(8, 8);  // 작은 점

        // 4-3. Distance Text
        var distRect = CreateUIChild(markerRoot, "Distance");
        var distTmp = distRect.gameObject.AddComponent<TextMeshProUGUI>();
        distTmp.text = "0 m";
        distTmp.fontSize = 20;
        distTmp.alignment = TextAlignmentOptions.Center;
        distTmp.color = Color.white;
        distTmp.fontStyle = FontStyles.Bold;
        distTmp.raycastTarget = false;
        distRect.anchorMin = distRect.anchorMax = new Vector2(0.5f, 0.5f);
        distRect.pivot = new Vector2(0.5f, 1f);
        distRect.sizeDelta = new Vector2(120, 28);
        distRect.anchoredPosition = new Vector2(0, -28);

        // 4-4. Offscreen Arrow (시야 밖일 때 회전)
        var arrow = CreateUIChild(markerRoot, "Arrow");
        var arrowImg = arrow.gameObject.AddComponent<Image>();
        arrowImg.sprite = arrowSprite;
        arrowImg.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        arrowImg.raycastTarget = false;
        arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.sizeDelta = new Vector2(30, 30);
        arrow.anchoredPosition = new Vector2(0, 50);
        arrow.gameObject.SetActive(false);

        // 5. TutorialMarker 컴포넌트 부착 + slot 연결
        var tm = markerGo.AddComponent<TutorialMarker>();
        var so = new SerializedObject(tm);
        so.FindProperty("markerRoot").objectReferenceValue = markerRoot;
        so.FindProperty("distanceText").objectReferenceValue = distTmp;
        so.FindProperty("offscreenArrow").objectReferenceValue = arrow;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = markerGo;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialMarkerBuilder] '{ManagerName}' 생성 완료. Canvas 자식. Ctrl+S로 씬 저장.");
    }

    static Canvas FindOrCreateCanvas()
    {
        // 기존 Screen Space Overlay Canvas 우선
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        // 없으면 새로 생성
        var go = new GameObject("Canvas_Marker", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c2 = go.GetComponent<Canvas>();
        c2.renderMode = RenderMode.ScreenSpaceOverlay;
        c2.sortingOrder = 50;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        return c2;
    }

    static RectTransform CreateUIChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Sprite EnsureCircleSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        if (existing != null) return existing;

        // Ring 형태 (안쪽 비우고 외곽선만)
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;  // 0 = center, 1 = edge

                // ring 두께: d 0.78~0.92 → alpha 1, 위/아래 6%는 부드러운 fade
                float a;
                if (d < 0.72f) a = 0f;
                else if (d < 0.78f) a = Mathf.SmoothStep(0f, 1f, (d - 0.72f) / 0.06f);
                else if (d < 0.92f) a = 1f;
                else if (d < 0.98f) a = Mathf.SmoothStep(1f, 0f, (d - 0.92f) / 0.06f);
                else a = 0f;

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(CirclePath);

        ConvertToSprite(CirclePath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
    }

    static Sprite EnsureArrowSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
        if (existing != null) return existing;

        // 삼각형 (위쪽 향함)
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 위쪽 삼각형: y 비율 t에 따라 x 범위 [center - half, center + half]
                float t = (float)y / size;
                float center = size * 0.5f;
                float halfWidth = (1f - t) * size * 0.5f;
                bool inside = x >= center - halfWidth && x <= center + halfWidth && t > 0.1f;
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(ArrowPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(ArrowPath);

        ConvertToSprite(ArrowPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
    }

    static void ConvertToSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        var parts = fullPath.Split('/');
        string curr = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{curr}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(curr, parts[i]);
            curr = next;
        }
    }
}
