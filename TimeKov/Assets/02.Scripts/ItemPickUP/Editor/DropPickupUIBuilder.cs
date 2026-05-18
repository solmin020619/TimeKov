using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class DropPickupUIBuilder
{
    private const string RowPrefabPath = "Assets/05.Prefabs/UI/PickupRow.prefab";

    [MenuItem("Tools/Drop Pickup UI/Build")]
    public static void Build()
    {
        if (Object.FindAnyObjectByType<DropPickupPanel>() != null)
        {
            Debug.LogWarning("[DropPickupUIBuilder] 씬에 DropPickupPanel이 이미 있습니다. 기존 것을 지우고 다시 실행하세요.");
            return;
        }

        GameObject rowPrefab = BuildRowPrefab();
        BuildPanel(rowPrefab);
        Debug.Log("[DropPickupUIBuilder] 생성 완료 — Canvas 밑 DropPickupPanel + " + RowPrefabPath);
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

        var bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.07f, 0.78f);

        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 20, 6, 6);
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

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

        RectTransform tier = NewUI("TierBar", row);
        tier.sizeDelta = new Vector2(6, 34);
        var tierImg = tier.gameObject.AddComponent<Image>();
        tierImg.color = new Color(0.61f, 0.42f, 0.88f);

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

    private static void BuildPanel(GameObject rowPrefab)
    {
        Canvas canvas = GetOrCreateCanvas();

        RectTransform panel = NewUI("DropPickupPanel", canvas.transform);
        var panelComp = panel.gameObject.AddComponent<DropPickupPanel>();

        RectTransform panelRoot = NewUI("PanelRoot", panel);
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
        fpImg.color = new Color(0.06f, 0.06f, 0.07f, 0.85f);
        RectTransform fLabel = NewUI("Label", fp);
        fLabel.anchorMin = Vector2.zero;
        fLabel.anchorMax = Vector2.one;
        fLabel.sizeDelta = Vector2.zero;
        AddText(fLabel, "F", 24, TextAlignmentOptions.Center, true);

        RectTransform rc = NewUI("RowContainer", panelRoot);
        var vlg = rc.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        var rcFit = rc.gameObject.AddComponent<ContentSizeFitter>();
        rcFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rcFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var so = new SerializedObject(panelComp);
        so.FindProperty("panelRoot").objectReferenceValue = panelRoot.gameObject;
        so.FindProperty("rowContainer").objectReferenceValue = rc;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab.GetComponent<DropPickupRow>();
        SerializedProperty tc = so.FindProperty("tierColors");
        tc.arraySize = 4;
        tc.GetArrayElementAtIndex(0).colorValue = Color.gray;
        tc.GetArrayElementAtIndex(1).colorValue = new Color(0.75f, 0.75f, 0.78f);
        tc.GetArrayElementAtIndex(2).colorValue = new Color(0.33f, 0.62f, 1f);
        tc.GetArrayElementAtIndex(3).colorValue = new Color(0.61f, 0.42f, 0.88f);
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panel.gameObject;
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
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
