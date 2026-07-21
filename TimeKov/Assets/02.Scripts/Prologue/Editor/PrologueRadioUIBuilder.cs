#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PrologueRadioUIBuilder
{
    // ── 색상 ──────────────────────────────────────────────────────────
    static readonly Color BgMain    = new Color(0.00f, 0.00f, 0.00f, 0.72f);
    static readonly Color Orange    = new Color(0.96f, 0.60f, 0.09f, 1.00f);
    static readonly Color OrangeHex = new Color(0.96f, 0.60f, 0.09f, 0.80f);
    static readonly Color White     = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    static readonly Color DivCol    = new Color(1.00f, 1.00f, 1.00f, 0.09f);
    static readonly Color ShadowCol = new Color(0.00f, 0.00f, 0.00f, 0.65f);
    static readonly Color PortBg    = new Color(0.04f, 0.04f, 0.07f, 1.00f);

    // ── 레이아웃 ──────────────────────────────────────────────────────
    const float PanelW   = 640f;
    const float PanelH   = 200f;
    const float PortColW = 118f;   // 초상화 컬럼 (오른쪽)
    const float NextW    = 120f;   // ▶ 버튼 너비
    const float PadV     = 8f;
    const float PadL     = 14f;
    const float PortSz   = 84f;
    const float BorderPx = 2.5f;
    const float HeaderH  = 22f;
    const float IconSz   = 18f;
    const float WaveH    = 16f;
    const float PadVLine = 35f;   // BtnLine 위아래 패딩 (Message 하단과 동일)

    // 오른쪽에서 각 요소 경계 (패널 우측 기준)
    // |  텍스트  | gap | BtnLine | gap | ▶ | VDiv | 초상화+파형 |
    //            ↑             ↑            ↑   ↑
    //         TextRX       BtnLine        BtnRX  VDivX
    const float GapW       = 10f;                            // BtnLine 양측 여백
    const float PortX      = PortColW;                        // 초상화 컬럼 왼쪽 경계
    const float VDivX      = PortColW + 1f;                   // 세로 구분선
    const float BtnRX      = PortColW + 1f + NextW;           // ▶ 버튼 왼쪽 경계 = 239
    const float BtnLineLX  = BtnRX + GapW + 1f;              // BtnLine 왼쪽 경계 (우측 기준) = 250
    const float BtnLineRX  = BtnRX + GapW;                   // BtnLine 오른쪽 경계 (우측 기준) = 249
    const float TextRX     = BtnRX + 2f * GapW + 1f;         // 텍스트 영역 오른쪽 경계 = 260

    [MenuItem("Tools/UI/Build Prologue Radio UI")]
    static void Build()
    {
        // 스프라이트 임포트 — char.png (초상화)
        const string portPath = "Assets/Resources/char.png";
        var importer = AssetImporter.GetAtPath(portPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        var portSprite = AssetDatabase.LoadAssetAtPath<Sprite>(portPath);
        if (portSprite == null)
            Debug.LogWarning("[Builder] char.png 스프라이트 로드 실패");

        // 스프라이트 임포트 — icon.png (버튼 아이콘)
        const string iconPath = "Assets/Resources/icon.png";
        var iconImporter = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (iconImporter != null && iconImporter.textureType != TextureImporterType.Sprite)
        {
            iconImporter.textureType         = TextureImporterType.Sprite;
            iconImporter.spriteImportMode    = SpriteImportMode.Single;
            iconImporter.alphaIsTransparency = true;
            iconImporter.SaveAndReimport();
        }
        var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (iconSprite == null)
            Debug.LogWarning("[Builder] icon.png 스프라이트 로드 실패");

        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.sortingOrder == 0) { canvas = c; break; }
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[Builder] Canvas 없음"); return; }

        Undo.SetCurrentGroupName("Build Prologue Radio UI");
        int ug = Undo.GetCurrentGroup();

        var old = canvas.transform.Find("RadioPanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // ── 루트 패널 ────────────────────────────────────────────────
        var panel = new GameObject("RadioPanel");
        Undo.RegisterCreatedObjectUndo(panel, "RadioPanel");
        panel.transform.SetParent(canvas.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-80f, -250f);
        rt.sizeDelta        = new Vector2(PanelW, PanelH);

        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

        // 블러 머티리얼 로드
        var blurMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/RadioBlurMat.mat");
        if (blurMat == null)
            Debug.LogWarning("[Builder] RadioBlurMat.mat 없음 — 셰이더 Assets/Shaders/UIBackgroundBlur.shader 확인 후 빌더 재실행");

        // 배경은 텍스트+버튼 영역만 덮음 — 초상화·파형 영역은 열어둠
        var textBg = Child(panel, "TextBg");
        var tbRt   = textBg.GetComponent<RectTransform>();
        tbRt.anchorMin = Vector2.zero;
        tbRt.anchorMax = Vector2.one;
        tbRt.offsetMin = Vector2.zero;
        tbRt.offsetMax = new Vector2(-TextRX, 0f);
        var tbImg = textBg.AddComponent<Image>();
        if (blurMat != null)
        {
            tbImg.material = blurMat;
            tbImg.color    = Color.white;   // CanvasGroup alpha 통과용
        }
        else
        {
            tbImg.color = BgMain;           // 폴백: 단색 배경
        }

        // ── 초상화 컬럼 (오른쪽) ─────────────────────────────────────
        var portCol = Child(panel, "PortraitCol");
        var pcRt = portCol.GetComponent<RectTransform>();
        pcRt.anchorMin = new Vector2(1f, 0f);
        pcRt.anchorMax = new Vector2(1f, 1f);
        pcRt.offsetMin = new Vector2(-PortColW, 0f);
        pcRt.offsetMax = new Vector2(0f, 0f);
        // PortraitCol은 배경 없음 — 씬 배경 그대로 노출 (텍스트 패널과 분리된 느낌)

        // 초상화 프레임 (테두리 없음)
        var portFrame = Child(portCol, "PortraitFrame");
        var pfRt = portFrame.GetComponent<RectTransform>();
        pfRt.anchorMin        = new Vector2(0.5f, 1f);
        pfRt.anchorMax        = new Vector2(0.5f, 1f);
        pfRt.pivot            = new Vector2(0.5f, 1f);
        pfRt.anchoredPosition = new Vector2(0f, -PadV);
        pfRt.sizeDelta        = new Vector2(PortSz, PortSz);

        var portImgGo = Child(portFrame, "Portrait");
        Stretch(portImgGo.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var portImg = portImgGo.AddComponent<Image>();
        portImg.sprite         = portSprite;
        portImg.preserveAspect = true;

        // 파형 바
        float waveTop = PadV + PortSz + 5f;
        var waveGo = Child(portCol, "WaveformBars");
        var wRt = waveGo.GetComponent<RectTransform>();
        wRt.anchorMin        = new Vector2(0.08f, 1f);
        wRt.anchorMax        = new Vector2(0.92f, 1f);
        wRt.pivot            = new Vector2(0.5f, 1f);
        wRt.anchoredPosition = new Vector2(0f, -waveTop);
        wRt.sizeDelta        = new Vector2(0f, WaveH);
        var wb = waveGo.AddComponent<WaveformBars>();
        wb.barCount  = 14; wb.barWidth = 3f; wb.gap = 3f;
        wb.minHeight = 3f; wb.maxHeight = WaveH;
        wb.barColor  = new Color(1f, 1f, 1f, 0.70f);

        // ── 텍스트 영역 (왼쪽) ───────────────────────────────────────
        float divY = PadV + HeaderH + 4f;

        var header = Child(panel, "Header");
        var hdRt   = header.GetComponent<RectTransform>();
        hdRt.anchorMin = new Vector2(0f, 1f);
        hdRt.anchorMax = new Vector2(1f, 1f);
        hdRt.offsetMin = new Vector2(PadL, -(PadV + HeaderH));
        hdRt.offsetMax = new Vector2(-TextRX, -PadV);

        var iconGo = Child(header, "Icon");
        var icRt   = iconGo.GetComponent<RectTransform>();
        icRt.anchorMin = new Vector2(0f, 0f);
        icRt.anchorMax = new Vector2(0f, 1f);
        icRt.offsetMin = Vector2.zero;
        icRt.offsetMax = new Vector2(IconSz, 0f);
        var icTmp = iconGo.AddComponent<TextMeshProUGUI>();
        icTmp.text      = "◎";
        icTmp.fontSize  = 13f;
        icTmp.color     = new Color(1f, 1f, 1f, 0.45f);
        icTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var spkGo = Child(header, "SpeakerName");
        var spRt   = spkGo.GetComponent<RectTransform>();
        spRt.anchorMin = new Vector2(0f, 0f);
        spRt.anchorMax = new Vector2(1f, 1f);
        spRt.offsetMin = new Vector2(IconSz + 5f, 0f);
        spRt.offsetMax = Vector2.zero;
        var speakerTmp = spkGo.AddComponent<TextMeshProUGUI>();
        speakerTmp.text      = "본부 관제";
        speakerTmp.fontSize  = 13f;
        speakerTmp.color     = White;
        speakerTmp.fontStyle = FontStyles.Bold;
        speakerTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var hDiv  = Child(panel, "HDivider");
        var hdvRt = hDiv.GetComponent<RectTransform>();
        hdvRt.anchorMin = new Vector2(0f, 1f);
        hdvRt.anchorMax = new Vector2(1f, 1f);
        hdvRt.offsetMin = new Vector2(PadL,         -(divY + 1f));
        hdvRt.offsetMax = new Vector2(-TextRX,       -divY);
        hDiv.AddComponent<Image>().color = DivCol;

        var msgGo = Child(panel, "Message");
        var msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.anchorMin = Vector2.zero;
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.offsetMin = new Vector2(PadL, 35f);  // BtnLine 하단 패딩과 맞춤
        msgRt.offsetMax = new Vector2(-TextRX, -(divY + 5f));
        var msgTmp = msgGo.AddComponent<TextMeshProUGUI>();
        msgTmp.text               = "";
        msgTmp.fontSize           = 18f;
        msgTmp.color              = White;
        msgTmp.enableWordWrapping = true;
        msgTmp.lineSpacing        = 2f;
        msgTmp.overflowMode       = TextOverflowModes.Truncate;

        // ── ▶ 버튼 (텍스트 위 렌더링) ────────────────────────────────
        var btnGo = Child(panel, "NextBtn");
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.offsetMin = new Vector2(-BtnRX, 0f);
        btnRt.offsetMax = new Vector2(-VDivX, 0f);
        btnGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);  // 배경 없음
        btnGo.AddComponent<Button>();

        var arGo = Child(btnGo, "Arrow");
        var arRt = arGo.GetComponent<RectTransform>();
        // 버튼 세로 중앙 고정, 가로 꽉 채움
        arRt.anchorMin        = new Vector2(0.05f, 0.05f);
        arRt.anchorMax        = new Vector2(0.95f, 0.95f);
        arRt.pivot            = new Vector2(0.5f, 0.5f);
        arRt.anchoredPosition = Vector2.zero;
        arRt.sizeDelta        = Vector2.zero;
        var arImg = arGo.AddComponent<Image>();
        arImg.sprite         = iconSprite;
        arImg.color          = Color.white;
        arImg.preserveAspect = true;

        // 텍스트/버튼 경계선
        var bLine = Child(panel, "BtnLine");
        var blRt  = bLine.GetComponent<RectTransform>();
        blRt.anchorMin = new Vector2(1f, 0f);
        blRt.anchorMax = new Vector2(1f, 1f);
        blRt.offsetMin = new Vector2(-BtnLineLX, 35f);
        blRt.offsetMax = new Vector2(-BtnLineRX, -35f);
        bLine.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);

        // ── PrologueRadioUI 연결 ─────────────────────────────────────
        var radioUI = panel.AddComponent<PrologueRadioUI>();
        var so      = new SerializedObject(radioUI);
        so.FindProperty("_canvasGroup").objectReferenceValue   = cg;
        so.FindProperty("_portraitImage").objectReferenceValue = portImg;
        so.FindProperty("_speakerText").objectReferenceValue   = speakerTmp;
        so.FindProperty("_messageText").objectReferenceValue   = msgTmp;
        so.FindProperty("_signalBar1").objectReferenceValue    = null;
        so.FindProperty("_signalBar2").objectReferenceValue    = null;
        so.FindProperty("_signalBar3").objectReferenceValue    = null;

        var entries = so.FindProperty("radioEntries");
        entries.arraySize = 4;
        // [3]은 quest=null — ShowByIndex(3)로만 수동 트리거 (시네마신 완료 후)
        string[] paths = {
            "Assets/06.ScriptableObjects/Quest/Quests/Prologue/quest_prolog_basics.asset",
            "Assets/06.ScriptableObjects/Quest/Quests/Prologue/quest_prolog_basics.asset",
            "Assets/06.ScriptableObjects/Quest/Quests/Prologue/quest_prolog_reach.asset",
            null,
        };
        int[]    triggers = { 0, 1, 1, 0 };
        string[] speakers = { "KAIROS 관제", "KAIROS 관제", "KAIROS 관제", "KAIROS 관제" };
        string[] messages = {
            "카이로스 관제예요. 살아있군요—다행이에요. ANANKE 진입 중 에너지장이 항법을 날렸어요. 지금 있는 곳은 1차 탐사대 기지예요. 기체 상태부터 봐요.",
            "이상 없어요. 기억하죠—지구 시간 에너지, 얼마 안 남았어요. ANANKE가 유일한 답이에요. 1차 탐사대 비상 장치가 조종석에 있어요. 가요.",
            "잘 왔어요. 중앙 콘솔이에요. 켜면 지구로 에너지 채굴 좌표가 전송돼요—여기서부터 진짜 임무예요. 코어가 시간이에요, 아끼면서 움직여요.",
            "빨간 덮개 아래예요. 한 번 켜면 돌이킬 수 없어요. 그래도—지구를 위한 거예요. 누르세요.",
        };

        for (int i = 0; i < 4; i++)
        {
            var e = entries.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("trigger").enumValueIndex        = triggers[i];
            e.FindPropertyRelative("quest").objectReferenceValue    =
                AssetDatabase.LoadAssetAtPath<QuestSO>(paths[i]);
            e.FindPropertyRelative("speakerName").stringValue       = speakers[i];
            e.FindPropertyRelative("message").stringValue           = messages[i];
            e.FindPropertyRelative("portrait").objectReferenceValue = portSprite;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.CollapseUndoOperations(ug);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = panel;
        Debug.Log("[PrologueRadioUIBuilder] 완료");
    }

    [MenuItem("Tools/UI/Build Prologue Radio UI", true)]
    static bool Validate() => !Application.isPlaying;

    static GameObject Child(GameObject p, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(p.transform, false);
        return go;
    }

    static void Stretch(RectTransform r, float l, float b, float ri, float t)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b);
        r.offsetMax = new Vector2(-ri, -t);
    }
}
#endif
