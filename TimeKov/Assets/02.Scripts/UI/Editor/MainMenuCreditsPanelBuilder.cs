// =====================================================================
// MainMenuCreditsPanelBuilder.cs  (Editor Only)
// Tools/UI/Build MainMenu Credits Panel
// MainMenu_Cinematic 캔버스 밑에 "제작진" 패널을 생성한다. 팰월드 엔드크레딧 참고:
// 작은 중앙 팝업이 아니라 풀스크린 단색 배경 + 중앙 워드마크 + 역할(영문, 시안)/이름
// 2열 목록. WorldSelectUIBuilder/SettingsPanelRebuilder와 동일 컨벤션: 자체 완결된
// 정적 빌더 + 자체 헬퍼(다른 빌더 스크립트와 공유하지 않음), 확인창 없이 항상 재생성.
// =====================================================================

#if UNITY_EDITOR
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

    // role은 팰월드 크레딧처럼 영문, name은 실제 인명(고유명사)이라 번역하지 않고 그대로 둔다.
    private static readonly (string role, string name)[] Members =
    {
        ("CEO, Client Programmer, Planner", "김솔민"),
        ("Lead Client Programmer",          "한종욱"),
        ("QA & Sub Client Programmer",      "한재원"),
        ("Lead Game Designer",              "안승현"),
        ("Environment Artist",              "엄기영"),
    };

    private static readonly Color BgColor    = Hex("0A0E13", 255);
    private static readonly Color TitleColor = Hex("F2F5F8", 255);
    private static readonly Color SubColor   = Hex("C7D2DC", 255);
    private static readonly Color LineColor  = Hex("FFFFFF", 90);
    private static readonly Color RoleColor  = Hex("8FD8FF", 255);
    private static readonly Color NameColor  = Hex("FFFFFF", 255);
    private static readonly Color CloseBg    = Hex("FFFFFF", 20);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/남양주고딕Light (OTF) SDF.asset");

    private static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
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

        // ── 루트 — 풀스크린 단색 배경(팰월드 크레딧처럼 메인메뉴 배경을 완전히 덮는다) ──
        var root = new GameObject("CreditsPanel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create CreditsPanel");
        root.transform.SetParent(canvasGO.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = BgColor;

        var controller = root.AddComponent<CreditsPanelController>();

        // ── 워드마크 타이틀 + 구분 장식(짧은 선 - 다이아몬드 - 짧은 선) ──────────
        var title = MakeTMP("Title", root.transform, new Vector2(900f, 70f), new Vector2(0f, 320f),
            "TIMEKOV", 48f, TitleColor, TextAlignmentOptions.Center);
        title.characterSpacing = 6f;
        ApplyFont(title);

        MakeRect("LineL", root.transform, new Vector2(140f, 1f), new Vector2(-110f, 264f), LineColor);
        var diamond = MakeRect("Diamond", root.transform, new Vector2(9f, 9f), new Vector2(0f, 264f), LineColor);
        diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        MakeRect("LineR", root.transform, new Vector2(140f, 1f), new Vector2(110f, 264f), LineColor);

        var studio = MakeTMP("Studio", root.transform, new Vector2(700f, 40f), new Vector2(0f, 218f),
            "Development Team", 24f, SubColor, TextAlignmentOptions.Center);
        studio.fontStyle = FontStyles.Bold;
        ApplyFont(studio);

        // ── 역할(영문)/이름 목록 — 화면 중앙에서 아래로 흐르는 2열 ──────────────
        var listGo = new GameObject("MemberList", typeof(RectTransform));
        listGo.transform.SetParent(root.transform, false);
        var listRt = listGo.GetComponent<RectTransform>();
        listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
        listRt.pivot = new Vector2(0.5f, 1f);
        listRt.anchoredPosition = new Vector2(0f, 140f);
        listRt.sizeDelta = new Vector2(620f, 0f);
        var vlg = listGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 22f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = listGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var (role, name) in Members)
            MakeMemberRow(listGo.transform, role, name);

        // ── 닫기 버튼 (우상단, ESC로도 닫힘 — CreditsPanelController.Update) ──────
        var closeBtnGO = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(root.transform, false);
        var closeRect = closeBtnGO.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-40f, -40f);
        closeRect.sizeDelta = new Vector2(44f, 44f);
        closeBtnGO.GetComponent<Image>().color = CloseBg;

        var closeLabelGO = new GameObject("Text", typeof(RectTransform));
        closeLabelGO.transform.SetParent(closeBtnGO.transform, false);
        var closeTmp = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeTmp.text = "X";
        closeTmp.fontSize = 20f;
        closeTmp.color = TitleColor;
        closeTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(closeTmp);
        var closeLabelRect = closeLabelGO.GetComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;

        var closeBtn = closeBtnGO.GetComponent<Button>();
        closeBtn.targetGraphic = closeBtnGO.GetComponent<Image>();
        UnityEventTools.AddPersistentListener(closeBtn.onClick, controller.CloseCredits);

        root.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[MainMenuCreditsPanelBuilder] 제작진 패널(풀스크린, 팰월드 크레딧 스타일) 생성 완료.");
        return controller;
    }

    // ── 역할/이름 행 (역할: 우측 정렬 + 시안, 이름: 좌측 정렬 + 흰색) ───────────
    private static void MakeMemberRow(Transform parent, string role, string name)
    {
        var row = new GameObject(name + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 30f;
        le.preferredHeight = 30f;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 24f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var roleGO = new GameObject("Role", typeof(RectTransform));
        roleGO.transform.SetParent(row.transform, false);
        var roleTmp = roleGO.AddComponent<TextMeshProUGUI>();
        roleTmp.text = role;
        roleTmp.fontSize = 20f;
        roleTmp.fontStyle = FontStyles.Bold;
        roleTmp.color = RoleColor;
        roleTmp.alignment = TextAlignmentOptions.MidlineRight;
        roleTmp.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFont(roleTmp);
        var roleLE = roleGO.AddComponent<LayoutElement>();
        roleLE.preferredWidth = 340f;
        roleLE.minWidth = 340f;

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(row.transform, false);
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text = name;
        nameTmp.fontSize = 20f;
        nameTmp.color = NameColor;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFont(nameTmp);
        var nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredWidth = 220f;
        nameLE.minWidth = 220f;
    }

    // ── 헬퍼 (이 빌더 전용 — 다른 Builder 스크립트와 공유하지 않음) ───────────

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static GameObject MakeRect(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
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

    private static TextMeshProUGUI MakeTMP(string name, Transform parent, Vector2 size, Vector2 pos,
        string text, float fontSize, Color color, TextAlignmentOptions align)
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

    private static Color Hex(string hex, int alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha / 255f;
        return c;
    }
}
#endif
