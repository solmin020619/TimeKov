// =====================================================================
// MainMenuCreditsPanelBuilder.cs  (Editor Only)
// Tools/UI/Build MainMenu Credits Panel
// MainMenu_Cinematic 캔버스 밑에 "제작진" 패널(배경 딤 + 중앙 박스 + 팀원 목록 + 닫기 버튼)을
// 생성한다. WorldSelectUIBuilder/SettingsPanelRebuilder와 동일 컨벤션: 자체 완결된 정적
// 빌더 + 자체 헬퍼(다른 빌더 스크립트와 공유하지 않음), 확인창 없이 항상 재생성.
// =====================================================================

#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuCreditsPanelBuilder
{
    private const string MenuPath = "Tools/UI/Build MainMenu Credits Panel";
    private const string CanvasName = "MainMenu_Cinematic";

    private static readonly (string name, string role)[] Members =
    {
        ("김솔민", "대표, 클라이언트, 기획"),
        ("한종욱", "메인 클라이언트"),
        ("한재원", "QA 서브 클라이언트"),
        ("안승현", "메인 기획"),
        ("엄기영", "배경 모델러"),
    };

    private static readonly Color PanelBg    = new Color(0.067f, 0.071f, 0.078f, 0.96f);
    private static readonly Color BackdropBg = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextPrimary = new Color(0.949f, 0.961f, 0.973f, 1f); // #F2F5F8
    private static readonly Color TextMuted   = new Color(0.604f, 0.631f, 0.671f, 1f);
    private static readonly Color AccentColor = new Color(0.498f, 0.816f, 1f, 1f);     // #7FD0FF

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/남양주고딕Light (OTF) SDF.asset");

    private static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
    }

    private static Sprite _roundedPillSprite;
    private const string RoundedPillPath = "Assets/Resources/Image/UI_Icon/Setting/Generated_CreditsRoundedPill.png";

    private static Sprite RoundedPillSprite()
    {
        if (_roundedPillSprite != null) return _roundedPillSprite;

        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPillPath);
        if (existing != null) { _roundedPillSprite = existing; return existing; }

        const int size = 64;
        const int radius = 22;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var pngBytes = tex.EncodeToPNG();
        Directory.CreateDirectory(Path.GetDirectoryName(RoundedPillPath));
        File.WriteAllBytes(RoundedPillPath, pngBytes);
        AssetDatabase.ImportAsset(RoundedPillPath);

        var importer = AssetImporter.GetAtPath(RoundedPillPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(radius, radius, radius, radius);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        _roundedPillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPillPath);
        return _roundedPillSprite;
    }

    [MenuItem(MenuPath)]
    static void BuildMenuItem()
    {
        var canvasGO = GameObject.Find(CanvasName);
        if (canvasGO == null)
        {
            Debug.LogError($"[MainMenuCreditsPanelBuilder] '{CanvasName}' 캔버스를 찾을 수 없습니다. MainMenu 씬에서 실행하세요.");
            return;
        }
        BuildCreditsPanel(canvasGO);
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    public static CreditsPanelController BuildCreditsPanel(GameObject canvasGO)
    {
        Undo.SetCurrentGroupName("Build MainMenu Credits Panel");
        int undoGroup = Undo.GetCurrentGroup();

        var existing = canvasGO.transform.Find("CreditsPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var root = new GameObject("CreditsPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create CreditsPanel");
        root.transform.SetParent(canvasGO.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var backdrop = root.AddComponent<Image>();
        backdrop.color = BackdropBg;

        var controller = root.AddComponent<CreditsPanelController>();

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(640f, 560f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = RoundedPillSprite();
        panelImg.type = Image.Type.Sliced;
        panelImg.color = PanelBg;

        // 제목
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panel.transform, false);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "제작진";
        titleTmp.fontSize = 36f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = TextPrimary;
        titleTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(titleTmp);
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(0f, 50f);

        // 팀원 목록 (세로 정렬)
        var listGO = new GameObject("MemberList", typeof(RectTransform));
        listGO.transform.SetParent(panel.transform, false);
        var listRect = listGO.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(60f, 90f);
        listRect.offsetMax = new Vector2(-60f, -120f);
        var vlg = listGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 18f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        foreach (var (name, role) in Members)
            MakeMemberRow(listGO.transform, name, role);

        // 닫기 버튼 (우상단)
        var closeBtnGO = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(panel.transform, false);
        var closeRect = closeBtnGO.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-28f, -28f);
        closeRect.sizeDelta = new Vector2(44f, 44f);
        var closeImg = closeBtnGO.GetComponent<Image>();
        closeImg.sprite = RoundedPillSprite();
        closeImg.type = Image.Type.Sliced;
        closeImg.color = new Color(1f, 1f, 1f, 0.08f);

        var closeLabelGO = new GameObject("Text", typeof(RectTransform));
        closeLabelGO.transform.SetParent(closeBtnGO.transform, false);
        var closeTmp = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeTmp.text = "X";
        closeTmp.fontSize = 20f;
        closeTmp.color = TextPrimary;
        closeTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(closeTmp);
        var closeLabelRect = closeLabelGO.GetComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;

        var closeBtn = closeBtnGO.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(closeBtn.onClick, controller.CloseCredits);

        root.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[MainMenuCreditsPanelBuilder] 제작진 패널 생성 완료.");
        return controller;
    }

    private static void MakeMemberRow(Transform parent, string name, string role)
    {
        var row = new GameObject(name + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 36f;
        le.preferredHeight = 36f;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 12f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(row.transform, false);
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text = name;
        nameTmp.fontSize = 24f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = AccentColor;
        nameTmp.alignment = TextAlignmentOptions.MidlineRight;
        ApplyFont(nameTmp);
        var nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredWidth = 120f;
        nameLE.minWidth = 120f;

        var sep = new GameObject("Sep", typeof(RectTransform));
        sep.transform.SetParent(row.transform, false);
        var sepTmp = sep.AddComponent<TextMeshProUGUI>();
        sepTmp.text = ":";
        sepTmp.fontSize = 24f;
        sepTmp.color = TextMuted;
        sepTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(sepTmp);
        var sepLE = sep.AddComponent<LayoutElement>();
        sepLE.preferredWidth = 16f;
        sepLE.minWidth = 16f;

        var roleGO = new GameObject("Role", typeof(RectTransform));
        roleGO.transform.SetParent(row.transform, false);
        var roleTmp = roleGO.AddComponent<TextMeshProUGUI>();
        roleTmp.text = role;
        roleTmp.fontSize = 24f;
        roleTmp.color = TextPrimary;
        roleTmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(roleTmp);
        var roleLE = roleGO.AddComponent<LayoutElement>();
        roleLE.preferredWidth = 360f;
        roleLE.minWidth = 360f;
    }
}
#endif
