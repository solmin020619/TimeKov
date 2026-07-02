// =====================================================================
// WorldSelectUIBuilder.cs  (Editor Only)
// Tools/UI/Build World Select UI
// 타이틀 화면(MainMenu_Cinematic) 위에 월드 선택 패널을 생성한다.
// 팰월드 WORLD SELECT / WORLD SETTING 레퍼런스 참고: 중앙 카드 팝업이 아니라
// 전체화면(기존 배경이 그대로 비치는 풀블리드) + 좌상단 큰 타이틀 + 테이블形 목록.
// SettingsPanelRebuilder/MainMenuButtonsBuilder와 동일 컨벤션: 자체 완결된 정적 빌더 +
// 자체 헬퍼(다른 빌더 스크립트와 공유하지 않음), 확인창 없이 항상 재생성.
// =====================================================================

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorldSelectUIBuilder
{
    private const string MenuPath = "Tools/UI/Build World Select UI";
    private const string CanvasName = "MainMenu_Cinematic";
    private const string RowPrefabPath = "Assets/05.Prefabs/UI/WorldSelectRow.prefab";

    // 레이아웃 기준값 (1920x1080 캔버스)
    private const float LeftMargin = 90f;
    private const float ContentWidth = 1650f; // 90 ~ 1740
    private const float RowHeight = 84f;
    private const float RowSpacing = 10f;
    private const float LevelColWidth = 160f;

    // 팰월드 레퍼런스 톤: 거의 무채색 슬레이트 네이비 + 시안 액센트(레벨/강조값만).
    // 배경이 밝고 복잡한 산/하늘 그림이라 처음엔 행 배경이 거의 투명(alpha 80~130)하고
    // 글자색도 채도가 낮아 글자가 배경에 묻혀 안 보이는 문제가 있었음 — 전부 대비를 크게 올림.
    private static readonly Color DimOverlayColor = Hex("050B12", 150); // 화면 전체를 깔아 글자 대비 확보
    private static readonly Color TitleColor   = Hex("F2F5F8", 255);
    private static readonly Color LineColor    = Hex("FFFFFF", 60);
    private static readonly Color HeaderColor  = Hex("C7D2DC", 255);
    private static readonly Color RowBgColor   = Hex("0A1018", 235);
    private static readonly Color CreateBgColor = Hex("0A1018", 190);
    private static readonly Color NameColor    = Hex("FFFFFF", 255);
    private static readonly Color DateColor    = Hex("A9BBCB", 255);
    private static readonly Color AccentCyan   = Hex("8FD8FF", 255);
    private static readonly Color ModalBg      = Hex("0A1018", 255);
    private static readonly Color ModalLabelBg = Hex("5A5A5A", 255);
    private static readonly Color ModalLabelTx = Hex("C8D8E4", 255);
    private static readonly Color InputBg      = Hex("0D1820", 235);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/Pretendard-SemiBold SDF.asset");

    [MenuItem(MenuPath)]
    static void Build()
    {
        var canvasGO = GameObject.Find(CanvasName);
        if (canvasGO == null)
        {
            Debug.LogError($"[WorldSelectUIBuilder] '{CanvasName}' 캔버스를 찾을 수 없습니다. MainMenu 씬에서 실행하세요.");
            return;
        }

        var existing = canvasGO.transform.Find("WorldSelectPanel");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.SetCurrentGroupName("Build World Select UI");
        int undoGroup = Undo.GetCurrentGroup();

        // ── 루트 — 풀스크린이지만 배경 채움 없음(기존 MainMenu_Cinematic의 산/Scrim이 그대로 비침) ──
        var root = new GameObject("WorldSelectPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create WorldSelectPanel");
        root.transform.SetParent(canvasGO.transform, false);
        Stretch(root.GetComponent<RectTransform>());

        var ui = root.AddComponent<WorldSelectUI>();
        var so = new SerializedObject(ui);
        SetRef(so, "panelRoot", root);

        // ── 딤 오버레이 — 배경(산/하늘) 위에 어둡게 깔아 글자 대비를 확보 ──────────
        // 풀블리드 배경이 밝고 복잡해서 이게 없으면 타이틀/헤더/행 글자가 묻혀 안 보였다.
        var dimOverlay = MakeRect("DimOverlay", root.transform, Vector2.zero, Vector2.zero, DimOverlayColor);
        Stretch(dimOverlay.GetComponent<RectTransform>());
        dimOverlay.GetComponent<Image>().raycastTarget = false;

        // ── 타이틀 (좌상단, 팰월드 "WORLD SELECT" 스타일) ───────────────────
        var title = MakeTMP("Title", root.transform, new Vector2(900, 60),
            new Vector2(LeftMargin, -70f), "WORLD SELECT", 46f, TitleColor, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 4f;
        AnchorTopLeft(title.rectTransform);
        ApplyFont(title);

        var titleLine = MakeRect("TitleLine", root.transform, new Vector2(360, 2), new Vector2(LeftMargin, -120f), LineColor);
        AnchorTopLeft(titleLine);
        var titleTick = MakeRect("TitleTick", root.transform, new Vector2(3, 12), new Vector2(LeftMargin, -116f), Hex("FFFFFF", 140));
        AnchorTopLeft(titleTick);

        // ── 헤더 행 (데이터 행과 동일한 HorizontalLayoutGroup 폭 배분으로 컬럼 정렬 보장) ──
        const float HeaderTop = -150f;
        const float HeaderRowH = 44f;   // 레퍼런스 기준 헤더는 데이터 행보다 얇게
        const float HeaderGap = 12f;    // 헤더 줄과 목록 사이 여백
        float headerBottom = HeaderTop - HeaderRowH;

        var headerRow = BuildRowSkeleton("HeaderRow", root.transform, new Vector2(LeftMargin, HeaderTop), withBg: false);
        // BuildRowSkeleton은 RowHeight(84)로 sizeDelta를 설정하므로 헤더 전용으로 덮어씀
        headerRow.GetComponent<RectTransform>().sizeDelta = new Vector2(ContentWidth, HeaderRowH);
        var headerName = MakeColumnTMP(headerRow, "월드명", HeaderColor, TextAlignmentOptions.MidlineLeft, flexible: true);
        headerName.fontStyle = FontStyles.Bold;
        var headerLevel = MakeColumnTMP(headerRow, "코어 레벨", HeaderColor, TextAlignmentOptions.MidlineRight, flexible: false, fixedWidth: LevelColWidth);
        headerLevel.fontStyle = FontStyles.Bold;

        var headerLine = MakeRect("HeaderLine", root.transform, new Vector2(ContentWidth, 1f), new Vector2(LeftMargin, headerBottom), Hex("FFFFFF", 25));
        AnchorTopLeft(headerLine);

        // 헤더 구분선 위 그라데이션 — 구분선 쪽이 짙고 위로 갈수록 투명해짐
        // RawImage + Texture2D 에셋 방식(씬 저장 시 참조 유지)
        var gradTex = BuildHeaderGradTex();
        if (gradTex != null)
        {
            const float GradH = 30f;
            var gradGo = new GameObject("HeaderGradient", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            gradGo.transform.SetParent(root.transform, false);
            var gradRt = gradGo.GetComponent<RectTransform>();
            gradRt.anchorMin = gradRt.anchorMax = gradRt.pivot = new Vector2(0f, 1f);
            gradRt.anchoredPosition = new Vector2(LeftMargin, headerBottom + GradH);
            gradRt.sizeDelta  = new Vector2(ContentWidth, GradH);
            var rawImg = gradGo.GetComponent<UnityEngine.UI.RawImage>();
            rawImg.texture = gradTex;
            rawImg.raycastTarget = false;
            // DimOverlay(index 0) 바로 위에 렌더 — 텍스트/행보다 먼저 그려짐
            gradGo.transform.SetSiblingIndex(1);
        }

        // ── 목록 컨테이너 (스크롤 없이 VerticalLayoutGroup — 슬롯이 적어 스크롤 불필요) ──
        var listGo = new GameObject("RowContainer", typeof(RectTransform));
        listGo.transform.SetParent(root.transform, false);
        var listRt = listGo.GetComponent<RectTransform>();
        AnchorTopLeft(listRt);
        listRt.anchoredPosition = new Vector2(LeftMargin, headerBottom - HeaderGap);
        listRt.sizeDelta = new Vector2(ContentWidth, 0f);
        var listVlg = listGo.AddComponent<VerticalLayoutGroup>();
        listVlg.spacing = RowSpacing;
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = true;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;
        var listCsf = listGo.AddComponent<ContentSizeFitter>();
        listCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SetRef(so, "rowContainer", listGo.transform);

        // ── "+ 신규 월드 생성하기" 행 — rowContainer의 자식으로 둬서 Refresh() 때 항상 맨 아래로 ──
        var createRow = BuildRowSkeleton("CreateRow", listGo.transform, Vector2.zero, withBg: true, bgColor: CreateBgColor);
        var createLabel = MakeTMP("Label", createRow.transform, new Vector2(0, RowHeight), Vector2.zero,
            "+  신규 월드 생성하기", 22f, NameColor, TextAlignmentOptions.Center);
        StretchFull(createLabel.rectTransform);
        ApplyFont(createLabel);
        // createRow의 HorizontalLayoutGroup이 자식을 컨텐츠 크기로 줄여버려(StretchFull 무시) 텍스트가
        // 가운데가 아니라 왼쪽에 몰려 보이는 문제 — 레이아웃 계산에서 빼서 직접 지정한 풀스트레치를 살린다.
        var createLabelLe = createLabel.gameObject.AddComponent<LayoutElement>();
        createLabelLe.ignoreLayout = true;
        var createBtn = createRow.AddComponent<Button>();
        createBtn.targetGraphic = createRow.GetComponent<Image>();
        SetRef(so, "createRowButton", createBtn);

        // ── 하단 우측 액션 바: 삭제 / 게임 시작 (우측부터 정렬) ──────────────────
        // 행을 클릭하면 선택(하이라이트)만 되고, 실제 입장/삭제는 여기 버튼이 현재
        // 선택된 슬롯을 대상으로 처리한다(선택 전엔 게임 시작/삭제 비활성).
        const float actBtnW = 220f, actBtnH = 52f, actSpacing = 16f, actBottom = 50f, actRight = 90f;

        var deleteActionBtn = MakeBottomRightButton("Btn_Delete", root.transform, new Vector2(actBtnW, actBtnH),
            actRight + actBtnW + actSpacing, actBottom, "삭제");
        SetRef(so, "deleteButton", deleteActionBtn.GetComponent<Button>());

        var enterBtn = MakeBottomRightButton("Btn_Enter", root.transform, new Vector2(actBtnW, actBtnH),
            actRight, actBottom, "게임 시작", isPrimary: true);
        SetRef(so, "enterButton", enterBtn.GetComponent<Button>());

        // 선택된 슬롯이 없는 평소 상태 — 인터랙터블 토글은 WorldSelectUI.SelectRow()가 담당.
        enterBtn.GetComponent<Button>().interactable = false;
        deleteActionBtn.GetComponent<Button>().interactable = false;

        // 액션 바 상단 구분선 (레퍼런스의 버튼 위 흰색 라인)
        var actLine = MakeRect("ActionLine", root.transform,
            new Vector2(ContentWidth, 2f), Vector2.zero, new Color(1f, 1f, 1f, 0.30f));
        var actLineRt = actLine.GetComponent<RectTransform>();
        actLineRt.anchorMin = actLineRt.anchorMax = actLineRt.pivot = new Vector2(0f, 0f);
        actLineRt.anchoredPosition = new Vector2(LeftMargin, actBottom + actBtnH + 55f);
        actLine.GetComponent<Image>().raycastTarget = false;

        // ── 이름 입력 모달 (팰월드 WORLD SETTING의 "월드명" 변경 팝업 참고) ──
        BuildCreateModal(root.transform, so);

        // ── WorldSelectRow 프리팹 ────────────────────────────────────────
        WorldSelectRow rowComp = BuildRowPrefab();
        if (rowComp != null) SetRef(so, "rowPrefab", rowComp);

        // ── 메인메뉴 버튼 목록 / 장식 요소 자동 연결 ──────────────────────
        var menuList = canvasGO.transform.Find("MenuList");
        if (menuList != null) SetRef(so, "mainMenuList", menuList.gameObject);

        var hideNames = new[] { "Logo_Timekov", "Logo_Underline", "Tagline" };
        var hideGos = new System.Collections.Generic.List<GameObject>();
        foreach (var n in hideNames)
        {
            var t = canvasGO.transform.Find(n);
            if (t != null) hideGos.Add(t.gameObject);
        }
        SetRefArray(so, "hideWhileOpen", hideGos);

        so.ApplyModifiedProperties();

        // ── TitleManager 자동 연결 ────────────────────────────────────────
        var titleManager = Object.FindAnyObjectByType<TitleManager>();
        if (titleManager != null)
        {
            var tmSO = new SerializedObject(titleManager);
            SetRef(tmSO, "worldSelectUI", ui);
            tmSO.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[WorldSelectUIBuilder] 씬에 TitleManager가 없어 자동 연결을 건너뜀.");
        }

        root.SetActive(false); // 평소엔 숨김 — TitleManager.worldSelectUI.Show()가 활성화

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[WorldSelectUIBuilder] 월드 선택 UI(풀스크린, 팰월드 스타일) 생성 완료. Ctrl+S로 씬 저장하세요.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    // ── 행 골격 (헤더/데이터/생성행이 동일한 컬럼 폭 배분을 공유 — 정렬 보장) ──────
    static GameObject BuildRowSkeleton(string name, Transform parent, Vector2 pos, bool withBg, Color bgColor = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (parent.GetComponent<VerticalLayoutGroup>() == null)
        {
            AnchorTopLeft(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(ContentWidth, RowHeight);
        }

        if (withBg)
        {
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
        }

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(24, 16, 8, 8);
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = RowHeight;
        le.preferredHeight = RowHeight;

        return go;
    }

    static TextMeshProUGUI MakeColumnTMP(GameObject row, string text, Color color, TextAlignmentOptions align, bool flexible, float fixedWidth = 0f)
    {
        var go = new GameObject("Col_" + text, typeof(RectTransform));
        go.transform.SetParent(row.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFontStatic(tmp);

        var le = go.AddComponent<LayoutElement>();
        if (flexible) le.flexibleWidth = 1f;
        else { le.preferredWidth = fixedWidth; le.minWidth = fixedWidth; le.flexibleWidth = 0f; }
        return tmp;
    }

    // ── 이름 입력 모달 ────────────────────────────────────────────────────
    static void BuildCreateModal(Transform parent, SerializedObject so)
    {
        var modalRoot = new GameObject("CreateModal", typeof(RectTransform));
        modalRoot.transform.SetParent(parent, false);
        Stretch(modalRoot.GetComponent<RectTransform>());
        modalRoot.SetActive(false);
        SetRef(so, "createModal", modalRoot);

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(modalRoot.transform, false);
        Stretch(backdropGo.GetComponent<RectTransform>());
        backdropGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var backdropBtn = backdropGo.GetComponent<Button>();
        backdropBtn.targetGraphic = backdropGo.GetComponent<Image>();
        SetRef(so, "modalBackdropButton", backdropBtn);

        const float boxW = 1000f, boxH = 360f;
        var box = MakeRect("Box", modalRoot.transform, new Vector2(boxW, boxH), Vector2.zero, ModalBg);

        // 라벨 스트립 — 팰월드 참고: 라벨이 박스 최상단에 바로 붙음
        // 배경 전용 오브젝트 + 자식 텍스트 구조 (같은 GO 안 컴포넌트 순서 문제 회피)
        // 팰월드 비율: 컨텐츠 폭=박스의 67%, 상단 90px / 하단 35px 여백
        // 컨텐츠 블록이 박스 중앙에 부유 — label center y = 180-90-36 = 54
        // 레퍼런스: label top = box top에서 86px 아래, center = 74
        const float innerW = 460f;
        const float labelH = 41f;
        const float labelW = 560f;
        var labelStrip = MakeRect("LabelStrip", box.transform, new Vector2(labelW, labelH),
            new Vector2(0f, 74f), ModalLabelBg);
        var label = MakeTMP("Text", labelStrip.transform, Vector2.zero, Vector2.zero,
            "월드명", 20f, ModalLabelTx, TextAlignmentOptions.Center);
        StretchFull(label.rectTransform);
        ApplyFont(label);

        // 인풋 테두리 (인풋 뒤에 렌더, 얇은 흰색 아웃라인)
        // boxH=360: label bottom=126, gap=7 → input top=119 → input center=91
        // label bottom=18, gap=5 → input top=13, center=13-33=-20, height=66
        // label bottom=53, gap=4 → input top=49, center=28, height=43
        // label bottom=53.5, gap=12 → input top=41.5, center=20
        MakeRect("InputBorder", box.transform, new Vector2(innerW + 2f, 47f), new Vector2(0f, 20f), new Color(1f, 1f, 1f, 0.25f));

        // 입력 필드 — 높이 43px
        var inputGo = CreateUIElementViaMenu("GameObject/UI/Input Field - TextMeshPro", box.transform);
        inputGo.name = "Input_WorldName";
        var inputRt = inputGo.GetComponent<RectTransform>();
        inputRt.anchorMin = inputRt.anchorMax = inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta = new Vector2(innerW, 43f);
        inputRt.anchoredPosition = new Vector2(0f, 20f);
        var inputImg = inputGo.GetComponent<Image>();
        if (inputImg != null) inputImg.color = InputBg;
        var input = inputGo.GetComponent<TMP_InputField>();
        input.characterLimit = 24;
        // "GameObject/UI/Input Field - TextMeshPro" 메뉴로 생성한 기본 템플릿은 폰트 크기가
        // 작고(14) 텍스트 색이 어두운 기본값이라, 이 모달의 어두운 InputBg 배경 위에서 거의
        // 안 보였다 — 박스 안 다른 텍스트(22)와 맞추고 색도 밝게 올린다.
        var placeholder = inputGo.transform.Find("Text Area/Placeholder")?.GetComponent<TextMeshProUGUI>();
        if (placeholder != null)
        {
            placeholder.text = "새 월드 이름";
            placeholder.fontSize = 22f;
            placeholder.color = Hex("A9BBCB", 160);
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.alignment = TextAlignmentOptions.Center;
            ApplyFont(placeholder);
        }
        var inputText = inputGo.transform.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
        if (inputText != null)
        {
            inputText.fontSize = 22f;
            inputText.color = Hex("FFFFFF", 255);
            inputText.alignment = TextAlignmentOptions.Center;
        }
        ApplyFont(inputText);
        SetRef(so, "newWorldNameInput", input);

        // 구분선 — input bottom=91-28=63, gap=20 → sep y=43
        // input bottom=7, gap=54 → sep y=-47
        MakeRect("Sep", box.transform, new Vector2(innerW, 1f), new Vector2(0f, -47f), Hex("FFFFFF", 50));

        // sep=-47, gap=29 → button top=-76, center=-97, height=41
        MakeRect("Btn_Confirm_Border", box.transform, new Vector2(322f, 43f), new Vector2(0f, -97f), new Color(1f, 1f, 1f, 0.30f));
        var confirmBtn = MakeButton("Btn_Confirm", box.transform, new Vector2(320f, 41f),
            new Vector2(0f, -97f), "결정", 20f, Hex("5A5A5A"), Hex("E8ECEF"));
        SetRef(so, "confirmCreateButton", confirmBtn.GetComponent<Button>());

        // 모서리 틱 — center pivot 기준으로 박스 안쪽 경계에 정렬 (H+V L자)
        float hx = boxW * 0.5f, hy = boxH * 0.5f;
        const float tw = 12f, th = 2f; // 가로 틱 크기
        Color tickCol = Hex("FFFFFF", 120);
        // TL
        MakeRect("TickTL_H", box.transform, new Vector2(tw, th), new Vector2(-hx + tw * 0.5f,  hy - th * 0.5f), tickCol);
        MakeRect("TickTL_V", box.transform, new Vector2(th, tw), new Vector2(-hx + th * 0.5f,  hy - tw * 0.5f), tickCol);
        // TR
        MakeRect("TickTR_H", box.transform, new Vector2(tw, th), new Vector2( hx - tw * 0.5f,  hy - th * 0.5f), tickCol);
        MakeRect("TickTR_V", box.transform, new Vector2(th, tw), new Vector2( hx - th * 0.5f,  hy - tw * 0.5f), tickCol);
        // BL
        MakeRect("TickBL_H", box.transform, new Vector2(tw, th), new Vector2(-hx + tw * 0.5f, -hy + th * 0.5f), tickCol);
        MakeRect("TickBL_V", box.transform, new Vector2(th, tw), new Vector2(-hx + th * 0.5f, -hy + tw * 0.5f), tickCol);
        // BR
        MakeRect("TickBR_H", box.transform, new Vector2(tw, th), new Vector2( hx - tw * 0.5f, -hy + th * 0.5f), tickCol);
        MakeRect("TickBR_V", box.transform, new Vector2(th, tw), new Vector2( hx - th * 0.5f, -hy + tw * 0.5f), tickCol);
    }

    static GameObject CreateUIElementViaMenu(string menuPath, Transform parent)
    {
        var prevSelection = Selection.activeGameObject;
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem(menuPath);
        GameObject created = Selection.activeGameObject;
        if (created != null && parent != null && created.transform.parent != parent)
            created.transform.SetParent(parent, false);
        Selection.activeGameObject = prevSelection;
        return created;
    }

    // ── WorldSelectRow 프리팹 (항상 재생성 — 다른 빌더와 동일 컨벤션) ───────────
    static WorldSelectRow BuildRowPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/05.Prefabs/UI");

        // BuildRowSkeleton은 parent.GetComponent<VerticalLayoutGroup>()를 호출해 parent가
        // 필요하므로(프리팹 루트는 부모 없는 임시 오브젝트), 여기서는 그 헬퍼를 쓰지 않고
        // 행 골격을 직접 만든다(헤더/데이터 행과 동일한 HorizontalLayoutGroup 설정만 맞춤).
        var rowGo = new GameObject("WorldSelectRow", typeof(RectTransform), typeof(Image));
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(ContentWidth, RowHeight);
        rowGo.GetComponent<Image>().color = RowBgColor;

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(24, 16, 8, 8);
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var rowLe = rowGo.AddComponent<LayoutElement>();
        rowLe.minHeight = RowHeight;
        rowLe.preferredHeight = RowHeight;

        // 이름 + 날짜 (세로로 쌓는 텍스트 컬럼, 가변폭)
        var textColGo = new GameObject("TextColumn", typeof(RectTransform));
        textColGo.transform.SetParent(rowGo.transform, false);
        var textColLe = textColGo.AddComponent<LayoutElement>();
        textColLe.flexibleWidth = 1f;
        var textVlg = textColGo.AddComponent<VerticalLayoutGroup>();
        textVlg.childControlWidth = true;
        textVlg.childControlHeight = true;
        textVlg.childForceExpandWidth = true;
        textVlg.childForceExpandHeight = false;
        textVlg.spacing = 4f;
        textVlg.childAlignment = TextAnchor.MiddleLeft;

        var nameTMP = MakeTMP("NameText", textColGo.transform, new Vector2(0, 28), Vector2.zero,
            "이름없는 월드", 22f, NameColor, TextAlignmentOptions.MidlineLeft);
        nameTMP.fontStyle = FontStyles.Bold;
        ApplyFont(nameTMP);

        var dateTMP = MakeTMP("DateText", textColGo.transform, new Vector2(0, 20), Vector2.zero,
            "-", 14f, DateColor, TextAlignmentOptions.MidlineLeft);
        ApplyFont(dateTMP);

        // 레벨 (고정폭, 시안 액센트 — 헤더 "강화 Lv."와 정렬)
        var levelTMP = MakeColumnTMP(rowGo, "Lv.0", AccentCyan, TextAlignmentOptions.MidlineRight, flexible: false, fixedWidth: LevelColWidth);
        levelTMP.name = "LevelText";
        levelTMP.fontStyle = FontStyles.Bold;

        // 호버 아웃라인 — 평소 투명, 마우스를 올리면 시안 테두리
        var outline = rowGo.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.clear;
        outline.effectDistance = new Vector2(2.5f, 2.5f);
        outline.useGraphicAlpha = false;

        // 선택 버튼(행 전체) — 클릭하면 선택(하이라이트)만 됨. 입장/삭제는 하단 액션 바가 처리.
        // Button의 내장 색 전환(ColorBlock)을 끄고 WorldSelectRow가 직접 시각 상태를 관리한다.
        var selectBtn = rowGo.AddComponent<Button>();
        selectBtn.targetGraphic = rowGo.GetComponent<Image>();
        selectBtn.transition = Selectable.Transition.None;

        var rowComp = rowGo.AddComponent<WorldSelectRow>();
        var rowSO = new SerializedObject(rowComp);
        SetRef(rowSO, "background", rowGo.GetComponent<Image>());
        SetRef(rowSO, "hoverOutline", outline);
        SetRef(rowSO, "nameText", nameTMP);
        SetRef(rowSO, "dateText", dateTMP);
        SetRef(rowSO, "levelText", levelTMP);
        SetRef(rowSO, "selectButton", selectBtn);
        rowSO.ApplyModifiedProperties();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rowGo, RowPrefabPath);
        Object.DestroyImmediate(rowGo);

        AssetDatabase.Refresh();
        Debug.Log($"[WorldSelectUIBuilder] WorldSelectRow 프리팹 재생성됨: {RowPrefabPath}");

        return prefabAsset?.GetComponent<WorldSelectRow>();
    }

    // ── 헬퍼 (이 빌더 전용 — 다른 Builder 스크립트와 공유하지 않음) ───────

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void AnchorTopLeft(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
    }

    static void AnchorTopLeft(GameObject go) => AnchorTopLeft(go.GetComponent<RectTransform>());

    static GameObject MakeRect(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
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

    static TextMeshProUGUI MakeTMP(string name, Transform parent, Vector2 size, Vector2 pos,
        string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
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

    static GameObject MakeButton(string name, Transform parent, Vector2 size, Vector2 pos,
        string label, float fontSize, Color bgColor, Color? textColor = null)
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
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = textColor ?? Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        ApplyFont(tmp);
        return go;
    }

    // 화면 우하단 모서리 기준 고정 배치 버튼.
    // 흰색 반투명 테두리 + 각 모서리에 L-브래킷 장식(팰월드 "축소" 버튼 스타일).
    static GameObject MakeBottomRightButton(string name, Transform parent, Vector2 size,
        float xFromRight, float yFromBottom, string label, bool isPrimary = false)
    {
        var anchoredPos = new Vector2(-xFromRight - size.x * 0.5f, yFromBottom + size.y * 0.5f);

        // 보더 rect — 흰색 반투명 테두리 (primary는 살짝 더 밝게)
        var border = MakeRect(name + "_Border", parent, size + Vector2.one * 2f, Vector2.zero,
            new Color(1f, 1f, 1f, isPrimary ? 0.50f : 0.35f));
        var borderRt = border.GetComponent<RectTransform>();
        borderRt.anchorMin = borderRt.anchorMax = borderRt.pivot = new Vector2(1f, 0f);
        borderRt.anchoredPosition = anchoredPos;

        // 버튼 본체
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var bg = go.GetComponent<Image>();
        bg.color = new Color32(0x62, 0x66, 0x6C, 0xFF);

        // 모서리 L-브래킷 (4개 × H+V 두 획 = 8개 rect, 흰색 반투명)
        float hw = size.x * 0.5f, hh = size.y * 0.5f;
        const float tLen = 10f, tThk = 2f;
        var tc = new Color(1f, 1f, 1f, 0.80f);
        int[] cxs = { -1,  1, -1,  1 };
        int[] cys = {  1,  1, -1, -1 };
        for (int i = 0; i < 4; i++)
        {
            float cx = cxs[i], cy = cys[i];
            // 수평 획
            AddChildImage(go.transform, new Vector2(tLen, tThk),
                new Vector2(cx * (hw - tLen * 0.5f), cy * (hh - tThk * 0.5f)), tc);
            // 수직 획
            AddChildImage(go.transform, new Vector2(tThk, tLen),
                new Vector2(cx * (hw - tThk * 0.5f), cy * (hh - tLen * 0.5f)), tc);
        }

        // 텍스트
        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20f;
        tmp.color     = isPrimary
            ? new Color32(0xD8, 0xE2, 0xEC, 0xFF)
            : new Color32(0xBB, 0xC4, 0xCE, 0xFF);
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(tmp);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition    = Selectable.Transition.None;

        var fx = go.AddComponent<ButtonAccentHoverFx>();
        var fxSO = new SerializedObject(fx);
        SetRef(fxSO, "label", tmp);
        var primProp = fxSO.FindProperty("isPrimary");
        if (primProp != null) primProp.boolValue = isPrimary;
        fxSO.ApplyModifiedProperties();

        return go;
    }

    // 버튼 내부 장식용 — raycastTarget 꺼서 버튼 클릭 방해하지 않음
    static void AddChildImage(Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject("_tick", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
    }

    static void ApplyFontStatic(TMP_Text t) => ApplyFont(t);

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var prop = so.FindProperty(field);
        if (prop != null) prop.objectReferenceValue = obj;
        else Debug.LogWarning($"[WorldSelectUIBuilder] 필드 없음: '{field}'");
    }

    static void SetRefArray(SerializedObject so, string field, System.Collections.Generic.List<GameObject> items)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[WorldSelectUIBuilder] 필드 없음: '{field}'"); return; }
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static Color Hex(string hex, int alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha / 255f;
        return c;
    }

    // 헤더 그라데이션 텍스처 생성/로드 (1×64 — 아래=짙은 네이비, 위=투명)
    static Texture2D BuildHeaderGradTex()
    {
        const string path = "Assets/05.Prefabs/UI/WorldSelectHeaderGrad.asset";
        System.IO.Directory.CreateDirectory("Assets/05.Prefabs/UI");

        // 이미 있으면 재생성(색 변경 시 반영되도록 항상 덮어씀)
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float t = (float)y / 63f;           // 0=bottom(구분선쪽), 1=top(투명쪽)
            float a = (1f - t) * (1f - t) * 0.12f; // 이차곡선: 구분선 바로 위만 밝고 빠르게 페이드
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        AssetDatabase.CreateAsset(tex, path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
