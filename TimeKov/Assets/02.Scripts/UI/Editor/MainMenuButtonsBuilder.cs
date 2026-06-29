// =====================================================================
// MainMenuButtonsBuilder.cs  (Editor Only)
// Tools/UI/Build MainMenu Buttons
// "아무 키나 눌러 시작" PressPrompt를 명확한 세로 메뉴 리스트(게임 시작 / 옵션 / 제작진 /
// 게임 종료)로 교체한다. _mockups/MAINMENU_HANDOFF.md 스펙을 그대로 따른다.
// WorldSelectUIBuilder/SettingsPanelRebuilder와 동일한 컨벤션: 자체 완결된 정적 빌더 +
// 자체 MakeXxx 헬퍼(다른 빌더 스크립트와 공유하지 않음).
// 옵션/제작진 패널은 MainMenuSettingsPanelBuilder/MainMenuCreditsPanelBuilder가 각자
// 책임지고, 여긴 그 결과물(컴포넌트)을 받아 메뉴 항목 클릭에 연결만 한다.
// =====================================================================

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuButtonsBuilder
{
    private const string MenuPath = "Tools/UI/Build MainMenu Buttons";
    private const string CanvasName = "MainMenu_Cinematic";

    private static readonly Color NormalColor = new Color(0.949f, 0.961f, 0.973f, 1f); // #F2F5F8
    private static readonly Color HoverColor  = new Color(0.498f, 0.816f, 1f, 1f);     // #7FD0FF
    private static readonly Color HoverBgColor = new Color(1f, 1f, 1f, 15f / 255f);

    // ── 게임 종료 확인 모달 색상 (WorldSelectUIBuilder와 동일 톤 — 프로젝트 전반 다크 네이비 통일) ──
    private static readonly Color ModalBoxColor = Hex("0A1018", 255);
    private static readonly Color ModalLineColor = Hex("FFFFFF", 40);
    private static readonly Color ModalTickColor = Hex("FFFFFF", 120);
    private static readonly Color FlatBtnBg = Hex("4A5562", 200);
    private static readonly Color FlatBtnBorder = Hex("FFFFFF", 70);
    private static readonly Color FlatBtnText = Hex("F2F5F8", 255);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/남양주고딕Light (OTF) SDF.asset");

    [MenuItem(MenuPath)]
    static void Build()
    {
        var canvasGO = GameObject.Find(CanvasName);
        if (canvasGO == null)
        {
            Debug.LogError($"[MainMenuButtonsBuilder] '{CanvasName}' 캔버스를 찾을 수 없습니다. MainMenu 씬에서 실행하세요.");
            return;
        }

        Undo.SetCurrentGroupName("Build MainMenu Buttons");
        int undoGroup = Undo.GetCurrentGroup();

        // ── PressPrompt 삭제 ─────────────────────────────────────────────
        var pressPrompt = canvasGO.transform.Find("PressPrompt");
        if (pressPrompt != null)
            Undo.DestroyObjectImmediate(pressPrompt.gameObject);

        // ── 기존 Btn_Quit 삭제(메뉴 리스트로 흡수, 깨끗한 재생성) ──────────
        var oldQuit = canvasGO.transform.Find("Btn_Quit");
        if (oldQuit != null)
            Undo.DestroyObjectImmediate(oldQuit.gameObject);

        // ── 기존 MenuList 재생성(반복 실행해도 항상 깨끗하게) ──────────────
        var existingMenu = canvasGO.transform.Find("MenuList");
        if (existingMenu != null)
            Undo.DestroyObjectImmediate(existingMenu.gameObject);

        // ── Scrim 알파 보정 ──────────────────────────────────────────────
        var scrim = canvasGO.transform.Find("Scrim")?.GetComponent<Image>();
        if (scrim != null)
        {
            Undo.RecordObject(scrim, "Set Scrim Alpha");
            Color c = scrim.color;
            c.a = 0.45f;
            scrim.color = c;
        }

        // ── MenuList 컨테이너 (하단 중앙, anchoredPosition.y = 140) ───────
        var menuListGo = new GameObject("MenuList", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(menuListGo, "Create MenuList");
        menuListGo.transform.SetParent(canvasGO.transform, false);

        var menuRt = menuListGo.GetComponent<RectTransform>();
        menuRt.anchorMin = new Vector2(0.5f, 0f);
        menuRt.anchorMax = new Vector2(0.5f, 0f);
        menuRt.pivot = new Vector2(0.5f, 0f);
        menuRt.anchoredPosition = new Vector2(0f, 140f);
        menuRt.sizeDelta = new Vector2(600f, 0f);

        var vlg = menuListGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 22f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var csf = menuListGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 메뉴 아이템 4개 ─────────────────────────────────────────────
        GameObject startItem   = MakeMenuItem("MenuItem_Start",   menuListGo.transform, "게임 시작");
        GameObject optionItem  = MakeMenuItem("MenuItem_Option",  menuListGo.transform, "옵션");
        GameObject creditsItem = MakeMenuItem("MenuItem_Credits", menuListGo.transform, "제작진");
        GameObject quitItem    = MakeMenuItem("MenuItem_Quit",    menuListGo.transform, "게임 종료");

        // ── 클릭 연결: 게임 시작 → TitleManager.OnClickStart() ───────────
        var titleManager = Object.FindAnyObjectByType<TitleManager>();
        var startBtn = startItem.GetComponent<Button>();
        if (titleManager != null)
            UnityEventTools.AddPersistentListener(startBtn.onClick, titleManager.OnClickStart);
        else
            Debug.LogWarning("[MainMenuButtonsBuilder] 씬에 TitleManager가 없어 '게임 시작' 연결을 건너뜀.");

        // ── 클릭 연결: 옵션 → World.unity의 설정 패널을 복제해온 GlobalSettingsManager ─
        var settingsMgr = MainMenuSettingsPanelBuilder.BuildSettingsPanel(canvasGO);
        var optionBtn = optionItem.GetComponent<Button>();
        if (settingsMgr != null)
            UnityEventTools.AddPersistentListener(optionBtn.onClick, settingsMgr.OpenSettings);
        else
            Debug.LogWarning("[MainMenuButtonsBuilder] 설정 패널 복제 실패로 '옵션' 연결을 건너뜀.");

        // ── 클릭 연결: 제작진 → 신규 CreditsPanel ────────────────────────
        var creditsCtrl = MainMenuCreditsPanelBuilder.BuildCreditsPanel(canvasGO);
        var creditsBtn = creditsItem.GetComponent<Button>();
        if (creditsCtrl != null)
            UnityEventTools.AddPersistentListener(creditsBtn.onClick, creditsCtrl.OpenCredits);
        else
            Debug.LogWarning("[MainMenuButtonsBuilder] 제작진 패널 생성 실패로 '제작진' 연결을 건너뜀.");

        // ── 클릭 연결: 게임 종료 → 확인 모달을 띄우는 MainMenuQuitButton 컴포넌트 ─
        var quitComp = quitItem.AddComponent<MainMenuQuitButton>();
        BuildQuitConfirmModal(canvasGO.transform, quitComp);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = menuListGo;

        Debug.Log("[MainMenuButtonsBuilder] MainMenu 메뉴 리스트 생성 완료. Ctrl+S로 씬 저장하세요.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    // ── 메뉴 아이템 (Button + TMP, 호버 시 색/배경 전환) ───────────────────

    static GameObject MakeMenuItem(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f); // 기본 투명, 호버 시 MenuItemHoverFx가 전환

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 30f * 1.2f + 12f; // 폰트크기*1.2 + 상하 패딩(6+6)

        var tmp = MakeTMP("Text", go.transform, label);
        tmp.fontSize = 30f;
        tmp.characterSpacing = 8f; // CSS letter-spacing 4px 근사값 — 눈으로 보고 6~10 사이 미세조정 가능
        tmp.color = NormalColor;

        // LayoutElement가 minHeight만 지정하고 폭은 안 줘서(preferredWidth 기본값 -1) VerticalLayoutGroup이
        // (childControlWidth=true, childForceExpandWidth=false) 폭을 0으로 계산해버리는 문제 —
        // 버튼이 클릭 영역 없이 세로선 한 줄로 줄어들어 클릭이 전혀 안 먹혔다. 텍스트 폭 + 좌우 패딩(28*2)으로
        // 명시적으로 채워준다.
        tmp.ForceMeshUpdate();
        le.preferredWidth = tmp.preferredWidth + 56f;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white; // 색 전환은 MenuItemHoverFx가 텍스트/배경에 직접 적용
        btn.colors = colors;

        var hoverFx = go.AddComponent<MenuItemHoverFx>();
        hoverFx.Setup(tmp, bg, NormalColor, HoverColor, HoverBgColor);

        return go;
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(28f, 6f);
        rt.offsetMax = new Vector2(-28f, -6f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyFont(tmp);
        return tmp;
    }

    static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
    }

    // ── 게임 종료 확인 모달 (팰월드 "게임을 종료하시겠습니까?" 팝업 참고 — 코너 틱 + 가운데
    // 메시지 + 구분선 + 예/아니요) — MainMenuQuitButton이 transform.root.Find로 이름 찾아
    // 쓰므로 캔버스 바로 아래 "QuitConfirmModal"이라는 이름으로 둔다(인스펙터 와이어링 불필요). ──
    static void BuildQuitConfirmModal(Transform canvasTransform, MainMenuQuitButton quitComp)
    {
        var existing = canvasTransform.Find("QuitConfirmModal");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var modalRoot = new GameObject("QuitConfirmModal", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(modalRoot, "Create QuitConfirmModal");
        modalRoot.transform.SetParent(canvasTransform, false);
        StretchFull(modalRoot.GetComponent<RectTransform>());

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(modalRoot.transform, false);
        StretchFull(backdropGo.GetComponent<RectTransform>());
        backdropGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var backdropBtn = backdropGo.GetComponent<Button>();
        backdropBtn.targetGraphic = backdropGo.GetComponent<Image>();
        UnityEventTools.AddPersistentListener(backdropBtn.onClick, quitComp.CancelQuit);

        const float boxW = 700f, boxH = 300f;
        var box = MakeRect("Box", modalRoot.transform, new Vector2(boxW, boxH), Vector2.zero, ModalBoxColor);

        var msg = MakeLabel("Message", box.transform, new Vector2(boxW - 80f, 50f), new Vector2(0f, 55f),
            "게임을 종료하시겠습니까?", 26f, NormalColor, TextAlignmentOptions.Center);
        ApplyFont(msg);

        MakeRect("Sep", box.transform, new Vector2(boxW - 80f, 1f), new Vector2(0f, -12f), ModalLineColor);

        var yesBtn = MakeFlatDialogButton("Btn_Yes", box.transform, new Vector2(260f, 56f), new Vector2(-150f, -90f), "예");
        UnityEventTools.AddPersistentListener(yesBtn.GetComponent<Button>().onClick, quitComp.ConfirmQuit);

        var noBtn = MakeFlatDialogButton("Btn_No", box.transform, new Vector2(260f, 56f), new Vector2(150f, -90f), "아니요");
        UnityEventTools.AddPersistentListener(noBtn.GetComponent<Button>().onClick, quitComp.CancelQuit);

        // 모서리 틱(팰월드 미니멀 코너 브래킷 근사 — 단순 대시 4개, WorldSelectUIBuilder 모달과 동일 스타일)
        float hx = boxW * 0.5f, hy = boxH * 0.5f;
        MakeRect("TickTL", box.transform, new Vector2(14f, 2f), new Vector2(-hx, hy), ModalTickColor);
        MakeRect("TickTR", box.transform, new Vector2(14f, 2f), new Vector2(hx, hy), ModalTickColor);
        MakeRect("TickBL", box.transform, new Vector2(14f, 2f), new Vector2(-hx, -hy), ModalTickColor);
        MakeRect("TickBR", box.transform, new Vector2(14f, 2f), new Vector2(hx, -hy), ModalTickColor);

        modalRoot.SetActive(false);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

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

    static TextMeshProUGUI MakeLabel(string name, Transform parent, Vector2 size, Vector2 pos,
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

    // 예/아니요 버튼 — WorldSelectUIBuilder 하단 액션 바와 동일한 무채색 평면 + 얇은 외곽선 스타일.
    static GameObject MakeFlatDialogButton(string name, Transform parent, Vector2 size, Vector2 pos, string label)
    {
        const float borderThickness = 1.5f;
        var border = MakeRect(name + "_Border", parent, size + Vector2.one * (borderThickness * 2f), pos, FlatBtnBorder);

        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = FlatBtnBg;
        go.GetComponent<Button>().targetGraphic = go.GetComponent<Image>();

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.color = FlatBtnText;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(tmp);

        return go;
    }

    static Color Hex(string hex, int alpha = 255)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = alpha / 255f;
        return c;
    }
}
#endif
