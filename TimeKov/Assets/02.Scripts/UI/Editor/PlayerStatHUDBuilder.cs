// =====================================================================
// PlayerStatHUDBuilder.cs  (Editor Only)
// Tools/TIMEKOV/플레이어 스탯 UI 생성
//   → 좌하단 HUD(playerInfoRoot) 안에 새 STATUS 패널을 생성하고
//     PlayerStatHUD 필드 + GameUIController.statPanel + 오픈 애니메이션을 자동 연결한다.
//   기존 Character_stat(구 스탯창)은 비활성화 + 이름 변경으로 보존(원하면 직접 삭제).
//
// 디자인: 목업(soft steel-blue sci-fi) 재해석 — 코너 브래킷 / 헤더 / MAX TIME·STAMINA
//   슬라이더 / ATK·DEF 박스. 둥근 모서리·아이콘은 단색 플레이스홀더(스프라이트 후속 교체).
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PlayerStatHUDBuilder
{
    // 패널 크기 (좌하단 기준)
    const float W = 360f, H = 270f;
    static readonly float HW = W * 0.5f, HH = H * 0.5f;
    const float Margin = 24f;   // 화면 좌·하단 여백

    // 실제 아이콘 스프라이트(SilentOutbreak UIKIT) — 없으면 빈 칸 없이 단색 사각으로 표시.
    const string IconKitPath = "Assets/SilentOutbreak_UIKIT/PNG/Icons/";
    static string IconPath(string fileName) => IconKitPath + fileName + ".png";

    [MenuItem("Tools/TIMEKOV/플레이어 스탯 UI 생성")]
    public static void Build()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 Canvas가 없습니다.", "확인");
            return;
        }

        var gameUI = Object.FindAnyObjectByType<GameUIController>();
        SerializedObject gameUISO = gameUI != null ? new SerializedObject(gameUI) : null;

        // 부모 = 좌하단 플레이어 정보 그룹(있으면). 설정/건설 모드에서 같이 숨겨지도록.
        Transform parent = gameUI != null && gameUI.playerInfoRoot != null
            ? gameUI.playerInfoRoot.transform
            : canvas.transform;

        // ── 기존 스탯창 처리(교체) ──────────────────────────────────────
        GameObject oldPanel = gameUI != null ? gameUI.statPanel : null;
        if (oldPanel != null)
        {
            bool replace = EditorUtility.DisplayDialog("기존 스탯창 교체",
                $"기존 스탯창 '{oldPanel.name}' 이(가) 연결돼 있습니다.\n" +
                "비활성화하고 새 STATUS 패널로 교체할까요?\n(기존 것은 삭제하지 않고 숨겨둡니다)",
                "교체", "취소");
            if (!replace) return;
            oldPanel.name += " (old)";
            oldPanel.SetActive(false);
        }

        // ── 패널 루트 = 토글 대상 + PlayerStatHUD + 애니메이션 호스트 ───────
        var root = new GameObject("Character_stat", typeof(RectTransform), typeof(CanvasGroup));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(parent, false);
        // playerInfoRoot는 화면을 stretch하는 컨테이너가 아니라 화면 중앙에 고정된 "점" 앵커이므로
        // (기존 Character_stat도 anchorMin/Max(0.5,0.5) + 중앙 기준 절대 오프셋으로 배치돼 있었음),
        // 같은 방식으로 화면 절반 크기(1920x1080 기준 960x540)를 기준 삼아 좌하단 코너 위치를 계산한다.
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot     = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(W, H);
        const float ScreenHalfW = 960f, ScreenHalfH = 540f;
        rootRt.anchoredPosition = new Vector2(-ScreenHalfW + Margin + HW, -ScreenHalfH + Margin + HH);

        var hud = root.AddComponent<PlayerStatHUD>();
        var hudSO = new SerializedObject(hud);
        root.AddComponent<StatPanelRevealEffect>();   // 열릴 때 솟아오르는 애니메이션(OnEnable 자동 재생)

        // ── 배경(StatBG) — 튜토 스포트라이트 타깃 이름 유지 ────────────────
        // 테두리 림 = 배경보다 2px 큰 사각을 뒤에 깔아 가장자리 1px 스틸블루 선만 비치게.
        var border = MakeImage("Border", root.transform, new Vector2(W + 2f, H + 2f), Vector2.zero, Hex("8FB6D6", 70));
        var bg = MakeImage("StatBG", root.transform, new Vector2(W, H), Vector2.zero, Hex("0D1522", 242));
        border.transform.SetSiblingIndex(0);
        bg.transform.SetSiblingIndex(1);

        // ── 코너 브래킷 (4모서리 L자) ──────────────────────────────────
        AddCornerBrackets(root.transform);

        // ── 헤더 ────────────────────────────────────────────────────────
        MakeIconSlot("HeaderIcon", root.transform, 26f, new Vector2(-HW + 32f, HH - 30f));
        var titleTmp = MakeTMP("Title", root.transform, new Vector2(220, 28), new Vector2(-HW + 120f, HH - 30f),
            "STATUS", 17, Hex("CFE0EE"), TextAlignmentOptions.Left);
        titleTmp.characterSpacing = 8f;
        // 헤더 우측 다이아몬드 데코 (라인 + 작은 마름모 + 점 마름모)
        MakeImage("HdrLine", root.transform, new Vector2(18, 1), new Vector2(HW - 70f, HH - 30f), Hex("8FB6D6", 150));
        MakeDiamond("HdrDiaSmall", root.transform, 8f, new Vector2(HW - 50f, HH - 30f), Hex("8FB6D6", 110), false);
        MakeDiamond("HdrDiaBig",   root.transform, 13f, new Vector2(HW - 32f, HH - 30f), Hex("8FB6D6", 255), true);

        // 헤더 구분선
        MakeImage("HdrDivider", root.transform, new Vector2(W - 36f, 1f), new Vector2(0f, HH - 52f), Hex("8FB6D6", 120));

        // ── MAX TIME ────────────────────────────────────────────────────
        MakeIconSlot("MaxTimeIcon", root.transform, 16f, new Vector2(-HW + 28f, 62f), IconPath("T_icon_stopwatch"));
        MakeTMP("MaxTimeLabel", root.transform, new Vector2(150, 20), new Vector2(-HW + 105f, 62f),
            "MAX TIME", 12, Hex("9FB0C2"), TextAlignmentOptions.Left).characterSpacing = 2f;
        var hpText = MakeTMP("MaxTimeValue", root.transform, new Vector2(150, 20), new Vector2(HW - 90f, 62f),
            "0 / 0", 13, Hex("DCE6F0"), TextAlignmentOptions.Right);
        var hpSlider = MakeBar("MaxTimeBar", root.transform, new Vector2(W - 36f, 8f), new Vector2(0f, 44f),
            Hex("8FB6D6", 30), Hex("8FB6D6", 200));

        // ── STAMINA ──────────────────────────────────────────────────────
        MakeIconSlot("StaminaIcon", root.transform, 16f, new Vector2(-HW + 28f, 16f), IconPath("T_icon_energy"));
        MakeTMP("StaminaLabel", root.transform, new Vector2(150, 20), new Vector2(-HW + 105f, 16f),
            "STAMINA", 12, Hex("9FB0C2"), TextAlignmentOptions.Left).characterSpacing = 2f;
        var stamText = MakeTMP("StaminaValue", root.transform, new Vector2(150, 20), new Vector2(HW - 90f, 16f),
            "0 / 0", 13, Hex("DCE6F0"), TextAlignmentOptions.Right);
        var stamSlider = MakeBar("StaminaBar", root.transform, new Vector2(W - 36f, 8f), new Vector2(0f, -2f),
            Hex("8FB6D6", 30), Hex("7AB096", 200));

        // ── ATK / DEF 박스 (가운데 정렬) ──────────────────────────────────
        var atkText = MakeStatBox(root.transform, new Vector2(-84f, -74f), "ATK", Hex("C89678"), IconPath("T_icon_missile"));
        var defText = MakeStatBox(root.transform, new Vector2(+84f, -74f), "DEF", Hex("88B0CF"), IconPath("T_icon_defense"));

        // ── 필드 연결 ────────────────────────────────────────────────────
        SetRef(hudSO, "hpText",       hpText);
        SetRef(hudSO, "staminaText",  stamText);
        SetRef(hudSO, "hpSlider",     hpSlider);
        SetRef(hudSO, "staminaSlider", stamSlider);
        SetRef(hudSO, "atkText",      atkText);
        SetRef(hudSO, "defText",      defText);
        hudSO.ApplyModifiedProperties();

        // GameUIController.statPanel 재연결
        if (gameUISO != null)
        {
            SetRef(gameUISO, "statPanel", root);
            gameUISO.ApplyModifiedProperties();
        }

        // 런타임에서 C키로 열기 전까진 숨김(시작 시 CloseAll이 어차피 끔 → 깜빡임 방지).
        // 에디터에서 디자인을 다듬을 땐 하이어라키에서 다시 활성화해 보면 된다.
        root.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[TIMEKOV] 플레이어 스탯 UI 생성 완료 — PlayerStatHUD/statPanel/애니메이션 연결됨.");
        EditorUtility.DisplayDialog("완료",
            "STATUS 패널 생성 완료!\n\n" +
            "- 좌하단에 배치됨 (Margin으로 위치 조정)\n" +
            "- C키로 열면 솟아오르는 애니메이션 재생\n" +
            "- 패널은 숨김 상태로 생성됨 → 디자인 확인하려면 하이어라키에서 잠깐 켜기\n" +
            "- 아이콘 슬롯/모서리 둥근맛은 스프라이트로 후속 교체 가능\n" +
            "- Ctrl+S로 씬 저장", "확인");
    }

    // ── 구성 헬퍼 ──────────────────────────────────────────────────────

    // 4모서리 L자 브래킷
    static void AddCornerBrackets(Transform parent)
    {
        const float len = 14f, th = 2f, inset = 10f;
        Color c = Hex("8FB6D6", 210);
        int[] sx = { -1, -1, 1, 1 };
        int[] sy = { -1, 1, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            float cx = sx[i] * (HW - inset);
            float cy = sy[i] * (HH - inset);
            // 가로 세그먼트(코너에서 안쪽으로)
            MakeImage($"Bracket{i}_H", parent, new Vector2(len, th),
                new Vector2(cx - sx[i] * (len * 0.5f), cy), c);
            // 세로 세그먼트
            MakeImage($"Bracket{i}_V", parent, new Vector2(th, len),
                new Vector2(cx, cy - sy[i] * (len * 0.5f)), c);
        }
    }

    // 아이콘 자리. spritePath를 주면 실제 아이콘으로, 못 찾으면 단색 채움 사각으로 표시.
    static GameObject MakeIconSlot(string name, Transform parent, float size, Vector2 pos, string spritePath = null)
    {
        var go = MakeImage(name, parent, new Vector2(size, size), pos, Hex("8FB6D6", 140));
        ApplyIconSprite(go, spritePath);
        return go;
    }

    static void ApplyIconSprite(GameObject iconGo, string spritePath)
    {
        if (string.IsNullOrEmpty(spritePath)) return;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[StatHUDBuilder] 아이콘 스프라이트를 찾지 못함: {spritePath}");
            return;
        }
        var img = iconGo.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    // 마름모(45도 회전 사각). filled=가운데 점 포함
    static GameObject MakeDiamond(string name, Transform parent, float size, Vector2 pos, Color color, bool withDot)
    {
        var go = MakeImage(name, parent, new Vector2(size, size), pos, color);
        go.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        // 테두리 느낌: 안쪽을 패널색으로 파서 외곽선만 남김
        var inner = MakeImage(name + "_Cut", go.transform, new Vector2(size - 3f, size - 3f), Vector2.zero, Hex("0D1522", 255));
        if (withDot)
            MakeImage(name + "_Dot", inner.transform, new Vector2(size * 0.28f, size * 0.28f), Vector2.zero, color);
        return go;
    }

    // ATK/DEF 박스 — 라벨(아이콘+이름) 중앙, 값 중앙. 값 TMP 반환.
    static TextMeshProUGUI MakeStatBox(Transform parent, Vector2 pos, string label, Color labelColor, string iconPath = null)
    {
        var box = MakeImage($"{label}Box", parent, new Vector2(156, 78), pos, Hex("8FB6D6", 18));
        MakeImage($"{label}Box_Border", box.transform, new Vector2(156, 78), Vector2.zero, Hex("8FB6D6", 55))
            .transform.SetSiblingIndex(0);
        MakeImage($"{label}Box_Fill", box.transform, new Vector2(152, 74), Vector2.zero, Hex("0E1826", 255))
            .transform.SetSiblingIndex(1);

        // 라벨 행(아이콘 + 텍스트) — 박스 중앙 정렬
        MakeIconSlot($"{label}Icon", box.transform, 14f, new Vector2(-22f, 16f), iconPath);
        MakeTMP($"{label}Label", box.transform, new Vector2(70, 18), new Vector2(8f, 16f),
            label, 11, labelColor, TextAlignmentOptions.Left).characterSpacing = 2f;

        // 값 — 박스 중앙
        return MakeTMP($"{label}Value", box.transform, new Vector2(140, 30), new Vector2(0f, -14f),
            "0", 20, Hex("EEF3F8"), TextAlignmentOptions.Center);
    }

    // 게이지 바 = Slider(채움 Image=Filled). PlayerStatHUD가 min/max/value를 매 프레임 세팅.
    static Slider MakeBar(string name, Transform parent, Vector2 size, Vector2 pos, Color trough, Color fill)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        // 트로프(바닥)
        var bgGo = MakeImage("Background", go.transform, size, Vector2.zero, trough);
        StretchFull(bgGo.GetComponent<RectTransform>());

        // 채움 — Image.Type.Simple(기본). Slider가 값에 맞춰 fillRect의 anchor를 리사이즈한다.
        // (Type.Filled + 스프라이트 없음 조합은 fillAmount가 시각적으로 안 먹어 바가 안 움직임)
        var fillGo = MakeImage("Fill", go.transform, size, Vector2.zero, fill);
        var fillRt = fillGo.GetComponent<RectTransform>();
        StretchFull(fillRt);
        var fillImg = fillGo.GetComponent<Image>();

        var slider = go.GetComponent<Slider>();
        slider.transition   = Selectable.Transition.None;
        slider.interactable = false;
        slider.direction    = Slider.Direction.LeftToRight;
        slider.fillRect     = fillRt;     // Filled 아님 → Slider가 앵커로 fill 폭을 조절
        slider.handleRect   = null;
        slider.targetGraphic = fillImg;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        return slider;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 기본 헬퍼 (CoreUpgradeUIBuilder 와 동일 패턴) ─────────────────────

    static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // 스탯창은 표시 전용 — 입력 막지 않게
        return go;
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent,
        Vector2 size, Vector2 pos, string text, float fontSize,
        Color color, TextAlignmentOptions align)
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
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRef(SerializedObject so, string fieldName, Object obj)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null) prop.objectReferenceValue = obj;
        else Debug.LogWarning($"[StatHUDBuilder] 필드 없음: '{fieldName}'");
    }

    static Color Hex(string hex, int alpha = 255)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
        {
            c.a = alpha / 255f;
            return c;
        }
        return Color.white;
    }
}
