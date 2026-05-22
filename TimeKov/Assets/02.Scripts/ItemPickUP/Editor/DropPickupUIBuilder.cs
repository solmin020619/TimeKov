using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class DropPickupUIBuilder
{
    private const string RowPrefabPath = "Assets/05.Prefabs/UI/PickupRow.prefab";
    private const string BoxSpritePath = "Assets/Resources/Image/UI_PickUP/skill-frame-4x.png";
    private const string LineSpritePath = "Assets/Resources/Image/UI_PickUP/divider-white-square-4x.png";
    private const string FKeySpritePath = "Assets/Resources/Image/UI_PickUP/key-box-F-4x.png";

    [MenuItem("Tools/Drop Pickup UI/Build")]
    public static void Build()
    {
        // 기존 패널이 있으면 지우고 새로 만든다 (재실행 가능)
        var old = Object.FindAnyObjectByType<DropPickupPanel>();
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject rowPrefab = BuildRowPrefab();
        DropPickupPanel panel = BuildPanel(rowPrefab);

        // 씬에 LootBoxScanner 가 있으면 panel 자동 연결
        var scanner = Object.FindAnyObjectByType<LootBoxScanner>();
        if (scanner != null)
        {
            var sso = new SerializedObject(scanner);
            sso.FindProperty("panel").objectReferenceValue = panel;
            sso.ApplyModifiedProperties();
        }

        Debug.Log("[DropPickupUIBuilder] 생성 완료 — Canvas 밑 DropPickupPanel + " + RowPrefabPath);
    }

    private static Sprite LoadSprite(string assetPath)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.SaveAndReimport();
        }
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp == null) Debug.LogWarning("[DropPickupUIBuilder] 스프라이트 못 찾음: " + assetPath);
        return sp;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, string text, float size,
                                           TextAlignmentOptions align, bool bold)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static GameObject BuildRowPrefab()
    {
        RectTransform row = NewUI("PickupRow", null);
        row.sizeDelta = new Vector2(360, 64);

        // (1) 겉 박스 — skill-frame 스프라이트
        var bg = row.gameObject.AddComponent<Image>();
        Sprite boxSprite = LoadSprite(BoxSpritePath);
        if (boxSprite != null)
        {
            bg.sprite = boxSprite;
            bg.type = Image.Type.Sliced;
        }
        else bg.color = new Color(0.06f, 0.06f, 0.07f, 0.78f);

        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 20, 6, 6);
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // (3) 아이콘 — 슬롯만 (런타임에 아이템별 아이콘으로 채워짐)
        RectTransform icon = NewUI("Icon", row);
        icon.sizeDelta = new Vector2(52, 52);
        var iconImg = icon.gameObject.AddComponent<Image>();

        RectTransform count = NewUI("Count", icon);
        count.anchorMin = new Vector2(0f, 0f);
        count.anchorMax = new Vector2(1f, 0f);
        count.pivot = new Vector2(0.5f, 0f);
        count.sizeDelta = new Vector2(0, 22);
        count.anchoredPosition = new Vector2(0, 1);
        var countTmp = AddText(count, "1", 20, TextAlignmentOptions.Bottom, true);

        // (2) 가운데 선 — divider-white 스프라이트 (흰색, 런타임에 등급색으로 tint)
        RectTransform tier = NewUI("TierBar", row);
        tier.sizeDelta = new Vector2(8, 34);
        var tierImg = tier.gameObject.AddComponent<Image>();
        Sprite lineSprite = LoadSprite(LineSpritePath);
        if (lineSprite != null) tierImg.sprite = lineSprite;
        tierImg.color = Color.white;

        // (4) 아이템 이름
        RectTransform nameRt = NewUI("Name", row);
        nameRt.sizeDelta = new Vector2(230, 44);
        var nameTmp = AddText(nameRt, "아이템 이름", 26, TextAlignmentOptions.MidlineLeft, false);

        var rowComp = row.gameObject.AddComponent<DropPickupRow>();
        var so = new SerializedObject(rowComp);
        so.FindProperty("iconImage").objectReferenceValue = iconImg;
        so.FindProperty("countText").objectReferenceValue = countTmp;
        so.FindProperty("tierBar").objectReferenceValue = tierImg;
        so.FindProperty("nameText").objectReferenceValue = nameTmp;
        so.ApplyModifiedProperties();

        EnsureFolder(RowPrefabPath);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPrefabPath);
        Object.DestroyImmediate(row.gameObject);
        return prefab;
    }

    private static DropPickupPanel BuildPanel(GameObject rowPrefab)
    {
        Canvas canvas = GetOrCreateCanvas();

        RectTransform panel = NewUI("DropPickupPanel", canvas.transform);
        var panelComp = panel.gameObject.AddComponent<DropPickupPanel>();

        RectTransform panelRoot = NewUI("PanelRoot", panel);
        panelRoot.pivot = new Vector2(0f, 0.5f);            // 박스 오른쪽으로 펼쳐지도록
        panelRoot.localScale = new Vector3(0.5f, 0.5f, 1f); // 전체 크기 절반
        var prHlg = panelRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        prHlg.spacing = 12;
        prHlg.childAlignment = TextAnchor.MiddleLeft;
        prHlg.childControlWidth = false;
        prHlg.childControlHeight = false;
        prHlg.childForceExpandWidth = false;
        prHlg.childForceExpandHeight = false;
        var prFit = panelRoot.gameObject.AddComponent<ContentSizeFitter>();
        prFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        prFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform fp = NewUI("FPrompt", panelRoot);
        fp.sizeDelta = new Vector2(46, 46);
        var fpImg = fp.gameObject.AddComponent<Image>();
        Sprite fSprite = LoadSprite(FKeySpritePath);
        if (fSprite != null)
        {
            fpImg.sprite = fSprite;
        }
        else
        {
            fpImg.color = new Color(0.06f, 0.06f, 0.07f, 0.85f);
            RectTransform fLabel = NewUI("Label", fp);
            fLabel.anchorMin = Vector2.zero;
            fLabel.anchorMax = Vector2.one;
            fLabel.sizeDelta = Vector2.zero;
            AddText(fLabel, "F", 24, TextAlignmentOptions.Center, true);
        }

        RectTransform rc = NewUI("RowContainer", panelRoot);
        var vlg = rc.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        // PickupRow를 인스펙터에서 localScale 키울 수 있으니 scale을 정렬 계산에 반영
        vlg.childScaleWidth = true;
        vlg.childScaleHeight = true;
        var rcFit = rc.gameObject.AddComponent<ContentSizeFitter>();
        rcFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rcFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var so = new SerializedObject(panelComp);
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot.gameObject;
        so.FindProperty("rowContainer").objectReferenceValue = rc;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab.GetComponent<DropPickupRow>();
        SerializedProperty tc = so.FindProperty("tierColors");
        tc.arraySize = 5;
        tc.GetArrayElementAtIndex(0).colorValue = new Color(0.78f, 0.78f, 0.80f); // Common
        tc.GetArrayElementAtIndex(1).colorValue = new Color(0.35f, 0.80f, 0.40f); // Advanced
        tc.GetArrayElementAtIndex(2).colorValue = new Color(0.33f, 0.62f, 1f);    // Rare
        tc.GetArrayElementAtIndex(3).colorValue = new Color(0.70f, 0.45f, 0.95f); // Hero
        tc.GetArrayElementAtIndex(4).colorValue = new Color(1f, 0.66f, 0.25f);    // Legend
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panel.gameObject;
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        return panelComp;
    }

    private static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) return canvas;

        var canvasGo = new GameObject("Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private static void EnsureFolder(string assetPath)
    {
        string dir = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(dir)) return;

        string[] parts = dir.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
