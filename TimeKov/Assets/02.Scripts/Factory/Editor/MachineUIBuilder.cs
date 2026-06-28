// =====================================================================
// MachineUIBuilder.cs (Editor Only)
// Tools/TIMEKOV/공장 UI 생성
// 공장(설비) UI를 엔드필드식 간유리 톤으로 빌더 신설. 인벤 빌더(InventoryUIBuilder)의
// 블러/패널/헬퍼를 복제 이식(인벤 빌더는 안 건드림). 로직은 MachineUI 그대로, 레이아웃만 새로.
//
// 단계 1: 단일 간유리 패널 + 블러(PanelBlur 한 겹 + 프로스트 3겹) + 헤더(아이콘/제목/닫기).
//   - 단일 표면 원칙: 패널 하나에 전부 얹음. 중앙에 별도 배경패널 금지(블러 죽음).
//   - 좌측 인벤/창고(2단계), 중앙 생산부(3단계), 하단 레시피/버튼(4단계)은 이후 누적.
// =====================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JeffGrawAssets.FlexibleUI;

public static class MachineUIBuilder
{
    // ── 경로/상수 (인벤 빌더와 동일 자산 재사용) ──
    const string PartDir = "Assets/11.UI/New";                       // clggdesign 간유리 부품 PNG
    const string PanelSpritePath = PartDir + "/panel_ash_a78.png";   // 간유리 패널 표면(9-slice)
    const int PanelSlice = 56;
    const string SprDir = "Assets/11.UI/Inventory UI/sprites/";      // 아이콘 PNG 폴더

    // ── 색 (인벤 팔레트 복제 + 공장 노란 액센트) ──
    static Color BaseDark   => RGBA(230, 223, 211, 0.38f);   // PNG 폴백색(정상시 패널이 PNG라 안쓰임)
    static Color TxtMain    => Hex("242a31");                // 어두운 슬레이트(밝은 표면 위)
    static Color TxtSub     => Hex("4c545d");
    static Color Chrome     => RGBA(150, 178, 205, 0.26f);
    static Color HeaderHair => RGBA(84, 98, 122, 0.50f);     // 헤더 밑 구분선(쿨 슬레이트)
    // 공장 노란 액센트(버튼/진행바/입력화살표) - 단계 4에서 사용
    static Color Yellow     => Hex("e6c24a");
    static Color YellowBd   => Hex("b89a2e");
    static Color YellowTx   => Hex("4a3c0a");

    [MenuItem("Tools/TIMEKOV/공장 UI 생성")]
    public static void BuildMachineUI()
    {
        var ui = Object.FindAnyObjectByType<MachineUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("오류", "씬에 MachineUI가 없습니다.", "확인"); return; }

        var so = new SerializedObject(ui);

        // ★MachineUI 컴포넌트는 절대 삭제하지 않는다(삭제하면 SerializedObject 타겟이 죽어 에러).
        // uiPanel이 MachineUI를 포함하면(같은 오브젝트/조상) 그 오브젝트를 재활용(자식만 정리),
        // 무관한 별도 패널일 때만 삭제 후 MachineUI 자식으로 새로 만든다.
        var oldPanel = so.FindProperty("uiPanel").objectReferenceValue as GameObject;
        bool reuseInPlace = oldPanel != null &&
            (oldPanel == ui.gameObject || ui.transform.IsChildOf(oldPanel.transform));

        if (oldPanel != null &&
            !EditorUtility.DisplayDialog("경고",
                "설비 UI 패널을 새로 만듭니다.\n슬롯/위젯은 이후 단계(2~4)에서 다시 생성됩니다.",
                "새로 만들기", "취소")) return;

        GameObject panel;
        if (reuseInPlace)
        {
            // uiPanel == MachineUI(또는 그 조상) -> 그 오브젝트 재활용. MachineUI가 들어있는 가지만 보존.
            panel = oldPanel;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
            {
                var ch = panel.transform.GetChild(i);
                if (ui.transform == ch || ui.transform.IsChildOf(ch)) continue;   // MachineUI 보존
                Object.DestroyImmediate(ch.gameObject);
            }
        }
        else
        {
            // 무관한 별도 패널 -> 삭제하고 MachineUI 자식으로 새 패널 생성
            if (oldPanel != null) Object.DestroyImmediate(oldPanel);
            panel = new GameObject("MachinePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(ui.transform, false);
        }

        // ── 패널 (간유리 PNG + 둥근 코너 Mask). 크기/위치 강제(중앙 1400x740). ──
        var prt = panel.GetComponent<RectTransform>();
        if (prt == null) prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1400f, 740f);
        prt.anchoredPosition = Vector2.zero;

        var pimg = panel.GetComponent<Image>();
        if (pimg == null) pimg = panel.AddComponent<Image>();
        var panelSprite = LoadPanelSprite();
        if (panelSprite != null)
        {
            pimg.sprite = panelSprite; pimg.type = Image.Type.Sliced;
            pimg.color = new Color(1f, 1f, 1f, 0.12f);   // ash 표면 아주 옅게 = 칸이 블러 통과
            pimg.pixelsPerUnitMultiplier = 1f;
        }
        else { pimg.sprite = RoundedSprite(); pimg.type = Image.Type.Sliced; pimg.color = BaseDark; }
        var mask = panel.GetComponent<UnityEngine.UI.Mask>();
        if (mask == null) mask = panel.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        // ── 블러 = 단일 표면(패널 자식 BlurredImage 한 겹) + 프로스트 3겹. 인벤 레시피 그대로. ──
        BuildFrost(prt, panelSprite);

        // ── 헤더 (아이콘 / 제목 / 닫기 / 구분선) ──
        const float headerH = 64f;
        var header = MakeImage("HeaderBand", prt, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        StretchTop(header.GetComponent<RectTransform>(), headerH, 0, 0);
        header.GetComponent<Image>().raycastTarget = false;

        // 설비 아이콘 (좌, 어두운 틴트 = 밝은 표면 위)
        var icon = MakeImage("TitleIcon", header.transform, new Vector2(34, 34), Vector2.zero, TxtMain);
        var icRt = icon.GetComponent<RectTransform>();
        icRt.anchorMin = icRt.anchorMax = new Vector2(0, 0.5f); icRt.pivot = new Vector2(0, 0.5f);
        icRt.anchoredPosition = new Vector2(22, 0);
        var icImg = icon.GetComponent<Image>(); icImg.preserveAspect = true; icImg.raycastTarget = false;

        // 제목(설비 이름) - SetRef. 런타임 OpenFor(title)에서 채움.
        var title = MakeTMP("Title", header.transform, "설비", 24, TxtMain, TextAlignmentOptions.Left);
        AnchorLeft(title.rectTransform, 66, 380, 40);
        title.fontStyle = FontStyles.Bold;
        AddOutline(title.gameObject, new Color(0.86f, 0.90f, 0.96f, 0.5f), new Vector2(1f, -1f));
        SetRef(so, "machineTitleText", title);

        // 닫기 버튼 (우상단, ic_close + 호버 ColorTint)
        var closeBtnGo = MakeIconButton("CloseButton", header.transform, "ic_close", 48, Color.clear);
        AnchorRight(closeBtnGo.GetComponent<RectTransform>(), 12, 48, 48);
        TintIcon(closeBtnGo, TxtMain);
        var closeSpr = LoadPartSprite(PartDir + "/ic_close.png", Vector4.zero);
        var closeIconImg = closeBtnGo.transform.Find("Icon")?.GetComponent<Image>();
        if (closeIconImg != null && closeSpr != null) closeIconImg.sprite = closeSpr;
        var closeBg = closeBtnGo.GetComponent<Image>();
        closeBg.sprite = RoundedSprite(); closeBg.type = Image.Type.Sliced; closeBg.color = Color.white;
        var closeButton = closeBtnGo.GetComponent<Button>();
        closeButton.transition = Selectable.Transition.ColorTint; closeButton.targetGraphic = closeBg;
        var ccb = closeButton.colors;
        ccb.normalColor      = new Color(1f, 1f, 1f, 0f);
        ccb.highlightedColor = new Color(0.24f, 0.29f, 0.39f, 0.20f);
        ccb.pressedColor     = new Color(0.20f, 0.24f, 0.34f, 0.36f);
        ccb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        ccb.disabledColor    = new Color(1f, 1f, 1f, 0f);
        ccb.colorMultiplier  = 1f; ccb.fadeDuration = 0.1f;
        closeButton.colors = ccb;
        SetRef(so, "closeBtn", closeButton);

        // 헤더 밑 구분선 (밝은 표면 위라 또렷하게)
        var hair = MakeImage("HeaderDivider", prt, Vector2.zero, Vector2.zero, HeaderHair);
        var hairRt = hair.GetComponent<RectTransform>();
        hairRt.anchorMin = new Vector2(0, 1); hairRt.anchorMax = new Vector2(1, 1); hairRt.pivot = new Vector2(0.5f, 1);
        hairRt.offsetMin = new Vector2(3, -headerH - 2); hairRt.offsetMax = new Vector2(-3, -headerH);
        hair.GetComponent<Image>().raycastTarget = false;

        // ── uiPanel 배선 (재활용이면 자기 자신, 신규면 자식 패널) ──
        SetRef(so, "uiPanel", panel);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog("완료",
            "공장 UI 단계 1(골격 + 블러 + 헤더) 생성 완료.\n\n" +
            "Play 에서 설비 패널 모양 / 블러를 확인하세요.\n" +
            "(좌측 인벤·창고 / 중앙 생산부 / 하단 버튼은 단계 2~4)\n" +
            "확인 후 Ctrl+S.", "확인");
    }

    // ── 블러 = 단일 표면(패널 자식 BlurredImage 한 겹) + 프로스트 3겹. 인벤 빌더 레시피 그대로 복제. ──
    static void BuildFrost(RectTransform prt, Sprite panelSprite)
    {
        if (panelSprite == null) return;
        const float inset = 3f, footerH = 60f, titleH = 64f;

        // 통합 블러 = 패널 자식 (별도 Screen Space-Camera 캔버스 없음). 둥근 패널 스프라이트 + Mask로 코너 일치.
        var blurGo = new GameObject("PanelBlur", typeof(RectTransform));
        blurGo.transform.SetParent(prt, false);
        var blRt = blurGo.GetComponent<RectTransform>();
        blRt.anchorMin = Vector2.zero; blRt.anchorMax = Vector2.one; blRt.offsetMin = Vector2.zero; blRt.offsetMax = Vector2.zero;
        var blur = blurGo.AddComponent<BlurredImage>();
        blur.sprite = panelSprite; blur.type = Image.Type.Sliced; blur.pixelsPerUnitMultiplier = 1f;
        blur.color = Color.white; blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.cameraReference = PickBuildCamera();
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            if (bs.blurSections != null) foreach (var sec in bs.blurSections) { sec.iterations = 5; sec.sampleDistance = 1.5f; }
            bs.vibrancy = 0f; bs.brightness = 0.02f; bs.contrast = 0f; bs.referenceResolution = 1080;
        }
        blur.Common.ValidateBlur();

        // 어두운 배경 = 풀사이즈(Mask 둥근 코너). 가장자리/코너로 비쳐 그림자 착시.
        var bgGo = MakeImage("BgDark", prt, Vector2.zero, Vector2.zero, Color.white);
        var bgrt = bgGo.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>(); bgImg.sprite = null; bgImg.type = Image.Type.Simple; bgImg.raycastTarget = false;
        var bgGrad = bgGo.AddComponent<UIFrostGradient>();
        bgGrad.topColor = RGBA(22, 28, 40, 0.26f); bgGrad.bottomColor = RGBA(10, 14, 22, 0.52f);

        // 본문 밝은 표면 = 풀폭 틴트(Mask가 코너 둥글림). inset 3 = 좌우 배경 은은히만 비침.
        var cardGo = MakeImage("BodyFrost", prt, Vector2.zero, Vector2.zero, Color.white);
        var crt = cardGo.GetComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(inset, footerH); crt.offsetMax = new Vector2(-inset, -titleH);
        var cImg = cardGo.GetComponent<Image>(); cImg.sprite = null; cImg.type = Image.Type.Simple; cImg.raycastTarget = false;
        var cGrad = cardGo.AddComponent<UIFrostGradient>();
        cGrad.topColor = RGBA(216, 224, 237, 0.34f); cGrad.bottomColor = RGBA(199, 209, 223, 0.26f);

        // 헤더 밝은 표면 = 상단(거의 흰색, 엔필처럼).
        var hbGo = MakeImage("HeaderFrost", prt, Vector2.zero, Vector2.zero, Color.white);
        var hbrt = hbGo.GetComponent<RectTransform>();
        hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1); hbrt.pivot = new Vector2(0.5f, 1);
        hbrt.offsetMin = new Vector2(inset, -titleH); hbrt.offsetMax = new Vector2(-inset, -inset);
        var hbImg = hbGo.GetComponent<Image>(); hbImg.sprite = null; hbImg.type = Image.Type.Simple; hbImg.raycastTarget = false;
        var hbGrad = hbGo.AddComponent<UIFrostGradient>();
        hbGrad.topColor = RGBA(245, 248, 253, 0.62f); hbGrad.bottomColor = RGBA(237, 243, 251, 0.56f);
    }

    // ─────────────────────────────────────────────────────────────────
    // 헬퍼 (인벤 빌더 InventoryUIBuilder 에서 복제 - 자기완결)
    // ─────────────────────────────────────────────────────────────────

    static GameObject MakeImage(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeRounded(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        var go = MakeImage(name, parent, size, pos, color);
        var img = go.GetComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced;
        return go;
    }

    static GameObject MakeEmpty(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject MakeIconButton(string name, Transform parent, string iconName, float size, Color bg)
    {
        GameObject go = bg.a > 0f ? MakeRounded(name, parent, new Vector2(size, size), Vector2.zero, bg)
                                  : MakeImage(name, parent, new Vector2(size, size), Vector2.zero, Color.clear);
        if (bg.a > 0f) AddOutline(go, Chrome, new Vector2(1f, -1f));
        var btn = go.AddComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var icon = MakeImage("Icon", go.transform, new Vector2(size * 0.6f, size * 0.6f), Vector2.zero, TxtSub);
        var img = icon.GetComponent<Image>(); img.raycastTarget = false; img.preserveAspect = true;
        var spr = LoadSpr(iconName);
        if (spr != null) img.sprite = spr;
        return go;
    }

    static void AddOutline(GameObject go, Color color, Vector2 dist)
    {
        var ol = go.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = color; ol.effectDistance = dist;
    }

    static void TintIcon(GameObject btn, Color c)
    {
        var icon = btn.transform.Find("Icon");
        if (icon != null) { var img = icon.GetComponent<Image>(); if (img != null) img.color = c; }
    }

    static void StretchTop(RectTransform rt, float h, float top, float side)
    { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.offsetMin = new Vector2(side, -top - h); rt.offsetMax = new Vector2(-side, -top); }

    static void StretchBottom(RectTransform rt, float h, float bottom, float side)
    { rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0); rt.offsetMin = new Vector2(side, bottom); rt.offsetMax = new Vector2(-side, bottom + h); }

    static void AnchorLeft(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, 0); }

    static void AnchorRight(RectTransform rt, float x, float w, float h)
    { rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(-x, 0); }

    // ── 스프라이트 로드 (인벤 빌더와 동일 임포트 교정) ──
    static Sprite LoadPanelSprite() => ConfigurePanelSprite(PanelSpritePath);
    static Sprite ConfigurePanelSprite(string path) => LoadPartSprite(path, new Vector4(PanelSlice, PanelSlice, PanelSlice, PanelSlice));

    static Sprite LoadPartSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[MachineUIBuilder] PNG 못 찾음: " + path); return null; }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }

        var s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        if (s.spriteBorder != border || s.spriteMeshType != SpriteMeshType.FullRect)
        {
            s.spriteBorder = border; s.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(s); changed = true;
        }
        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite LoadSpr(string n)
    {
        string p = SprDir + n + ".png";
        var imp = AssetImporter.GetAtPath(p) as TextureImporter;
        if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    static Camera PickBuildCamera()
    {
        var main = Camera.main;
        if (main != null && main.targetTexture == null) return main;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null) return c;
        return main;
    }

    static void SetRef(SerializedObject so, string field, Object obj)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = obj;
        else Debug.LogWarning("[MachineUIBuilder] 필드 없음: " + field);
    }

    static Color Hex(string hex, int a = 255)
    { if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) { c.a = a / 255f; return c; } return Color.white; }

    static Color RGBA(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

    static Sprite _rounded;
    static Sprite RoundedSprite()
    {
        if (_rounded == null)
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        return _rounded;
    }
}
