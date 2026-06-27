// =====================================================================
// ChestOpenUIBuilder.cs  (Editor Only)
// Tools/TIMEKOV/상자 오픈 UI 생성
// Canvas 안에 ChestOpenPanel 계층을 자동 생성하고 레퍼런스 연결
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ChestOpenUIBuilder
{
    // [MenuItem("Tools/TIMEKOV/상자 오픈 UI 생성")]   // 메뉴 정리: 숨김(필요시 주석 해제)
    public static void Build()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.", "확인");
            return;
        }

        // 기존 삭제
        var existing = canvas.transform.Find("ChestOpenPanel");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("경고",
                "ChestOpenPanel이 이미 존재합니다. 교체할까요?", "교체", "취소");
            if (!replace) return;
            Object.DestroyImmediate(existing.gameObject);
        }

        // ── 루트 패널 ─────────────────────────────────────────────────
        GameObject root = MakeImage("ChestOpenPanel", canvas.transform,
            new Vector2(420, 520), Vector2.zero, Hex("07111E", 255));
        root.SetActive(false);

        var ui  = root.AddComponent<ChestOpenUI>();
        var so  = new SerializedObject(ui);
        SetRef(so, "panelRoot", root);

        // 외곽 테두리
        var border = MakeImage("Border", root.transform,
            new Vector2(420, 520), Vector2.zero, Hex("1A4060", 160));
        border.transform.SetAsFirstSibling();

        // ── 타이틀 ────────────────────────────────────────────────────
        MakeTMP("Title", root.transform,
            new Vector2(380, 48), new Vector2(0f, 225f),
            "획득 아이템", 24f, Hex("7DD4FC"),
            TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        MakeImage("TitleLine", root.transform,
            new Vector2(380, 1), new Vector2(0f, 198f), Hex("1A4060", 200));

        // ── 스크롤 뷰 (아이템 목록) ───────────────────────────────────
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(root.transform, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
        scrollRt.sizeDelta        = new Vector2(400, 330);
        scrollRt.anchoredPosition = new Vector2(0f, 10f);

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;

        // Viewport
        var viewportGo = MakeImage("Viewport", scrollGo.transform,
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        var vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin  = Vector2.zero;
        vpRt.anchorMax  = Vector2.one;
        vpRt.offsetMin  = vpRt.offsetMax = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRt;

        // Content (RowContainer)
        var contentGo = new GameObject("RowContainer", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot     = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);
        contentRt.anchoredPosition = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 6f;
        vlg.padding            = new RectOffset(10, 10, 8, 8);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        SetRef(so, "rowContainer", contentGo.transform);

        // ── 구분선 ────────────────────────────────────────────────────
        MakeImage("BottomLine", root.transform,
            new Vector2(380, 1), new Vector2(0f, -162f), Hex("1A4060", 200));

        // ── 닫기 버튼 ─────────────────────────────────────────────────
        var closeBtn = MakeButton("CloseButton", root.transform,
            new Vector2(380, 55), new Vector2(0f, -220f),
            "확인", 20f, Hex("1A4080"));
        SetRef(so, "closeButton", closeBtn.GetComponent<Button>());

        // ── ChestItemRow 프리팹 생성 또는 기존 연결 ─────────────────
        ChestItemRow rowPrefabComp = BuildOrLoadChestItemRowPrefab(canvas);
        if (rowPrefabComp != null)
            SetRef(so, "rowPrefab", rowPrefabComp);

        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("완료",
            "상자 오픈 UI가 생성됐습니다!\n\n" +
            "이미지 교체 위치:\n" +
            "Assets/05.Prefabs/UI/ChestItemRow.prefab\n" +
            " - IconImage: 아이템 아이콘\n" +
            " - GradeBar: 등급 색 이미지 (선택)\n\n" +
            "Ctrl+S로 씬 저장하세요.", "확인");
    }

    // ── ChestItemRow 프리팹 생성 / 로드 ──────────────────────────────

    const string ROW_PREFAB_PATH = "Assets/05.Prefabs/UI/ChestItemRow.prefab";

    static ChestItemRow BuildOrLoadChestItemRowPrefab(Canvas canvas)
    {
        // 이미 있으면 재사용
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ROW_PREFAB_PATH);
        if (existing != null)
        {
            var comp = existing.GetComponent<ChestItemRow>();
            if (comp != null) return comp;
        }

        // 경로 확보
        System.IO.Directory.CreateDirectory("Assets/05.Prefabs/UI");

        // ── 행 오브젝트 만들기 (씬에 임시 생성 후 프리팹으로 저장) ──

        // 루트: 가로 레이아웃
        var row = new GameObject("ChestItemRow", typeof(RectTransform));
        row.transform.SetParent(canvas.transform, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(380f, 64f);

        var rowImg = row.AddComponent<Image>();
        rowImg.color = new Color(0.08f, 0.16f, 0.25f, 0.85f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 10f;
        hlg.padding               = new RectOffset(8, 12, 6, 6);
        hlg.childControlHeight    = true;
        hlg.childControlWidth     = false;
        hlg.childForceExpandWidth = false;
        hlg.childAlignment        = TextAnchor.MiddleLeft;

        var le = row.AddComponent<LayoutElement>();
        le.minHeight      = 64f;
        le.preferredHeight = 64f;

        // 등급 바 (좌측 얇은 색 줄)
        var gradeBar = new GameObject("GradeBar", typeof(RectTransform), typeof(Image));
        gradeBar.transform.SetParent(row.transform, false);
        gradeBar.GetComponent<Image>().color = Hex("7DD4FC");
        var gradeLE = gradeBar.AddComponent<LayoutElement>();
        gradeLE.minWidth      = 5f;
        gradeLE.preferredWidth = 5f;
        gradeLE.flexibleWidth  = 0f;

        // 아이콘 (교체 가능)
        var iconGo = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        iconGo.GetComponent<Image>().color = new Color(0.3f, 0.4f, 0.5f, 0.6f);
        var iconLE = iconGo.AddComponent<LayoutElement>();
        iconLE.minWidth      = 52f;
        iconLE.preferredWidth = 52f;
        iconLE.flexibleWidth  = 0f;

        // 이름 텍스트
        var nameGo = new GameObject("NameText", typeof(RectTransform));
        nameGo.transform.SetParent(row.transform, false);
        var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
        nameTMP.text      = "아이템명";
        nameTMP.fontSize  = 17f;
        nameTMP.color     = Color.white;
        nameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        var nameLE = nameGo.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1f;

        // 수량 텍스트 (우측 고정)
        var countGo = new GameObject("CountText", typeof(RectTransform));
        countGo.transform.SetParent(row.transform, false);
        var countTMP = countGo.AddComponent<TextMeshProUGUI>();
        countTMP.text      = "x1";
        countTMP.fontSize  = 17f;
        countTMP.color     = Hex("7DD4FC");
        countTMP.alignment = TextAlignmentOptions.MidlineRight;
        countTMP.fontStyle = FontStyles.Bold;
        countTMP.textWrappingMode = TextWrappingModes.NoWrap;
        var countLE = countGo.AddComponent<LayoutElement>();
        countLE.minWidth      = 55f;
        countLE.preferredWidth = 55f;
        countLE.flexibleWidth  = 0f;

        // ChestItemRow 컴포넌트 연결
        var rowComp = row.AddComponent<ChestItemRow>();
        var rowSO   = new SerializedObject(rowComp);
        SetRef(rowSO, "iconImage",  iconGo.GetComponent<Image>());
        SetRef(rowSO, "nameText",   nameTMP);
        SetRef(rowSO, "countText",  countTMP);
        SetRef(rowSO, "gradeBar",   gradeBar.GetComponent<Image>());
        rowSO.ApplyModifiedProperties();

        // 프리팹으로 저장
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(row, ROW_PREFAB_PATH);
        Object.DestroyImmediate(row);

        AssetDatabase.Refresh();
        Debug.Log($"[ChestUIBuilder] ChestItemRow 프리팹 생성됨: {ROW_PREFAB_PATH}");

        return prefabAsset?.GetComponent<ChestItemRow>();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
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

    static TextMeshProUGUI MakeTMP(string name, Transform parent,
        Vector2 size, Vector2 pos, string text, float fontSize,
        Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text  = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent,
        Vector2 size, Vector2 pos, string label, float fontSize, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = bgColor;
        go.GetComponent<Button>().targetGraphic = go.GetComponent<Image>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        return go;
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var prop = so.FindProperty(field);
        if (prop != null) prop.objectReferenceValue = obj;
        else Debug.LogWarning($"[ChestUIBuilder] 필드 없음: '{field}'");
    }

    static Color Hex(string hex, int alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha / 255f;
        return c;
    }
}
