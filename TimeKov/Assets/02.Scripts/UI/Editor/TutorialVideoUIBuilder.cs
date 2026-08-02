using JeffGrawAssets.FlexibleUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// [08-02] 튜토리얼 영상 팝업(TutorialVideoUI) 실물 생성기.
//   예전엔 런타임이 이 계층을 통째로 만들고 스프라이트도 Resources.Load 로 집었다.
//   지금은 여기서 씬에 만들고 스프라이트도 직접 참조로 물린다(Resources 의존 제거).
//   레이아웃 숫자는 여기서 '한 번' 쓰이고, 그 뒤로는 씬의 RectTransform 값이 진짜다 - 인스펙터에서 바로 조정하면 된다.
//
//   저장이 불가능한 것만 런타임에 남겼다:
//     - RenderTexture (TutorialVideoUI.SetupVideo)
//     - 링/셰브론 스프라이트 (RuntimeGeneratedSprite 부품이 스스로 채움)
//     - 블러가 볼 카메라 (Camera.main 은 실행 중에만 잡힘)
public static class TutorialVideoUIBuilder
{
    private const string RootName = "TutorialVideoPopup";

    private const string FrameSpritePath = "Assets/Resources/tutorial/1.png";                 // sci-fi 프레임(확인 바 테두리 / 영상 테두리 폴백)
    private const string VideoFramePath = "Assets/Resources/TutorialVideo/vid_frame.png";     // 영상 테두리(인벤 영역 강조 프레임)
    private const string KeycapPath = "Assets/Resources/Image/UI_Icon/HUD/Keycap.png";        // 키캡(Q/E)

    // 배경 연출 - "기존 화면 위에 살짝 떠오르는" 느낌 목표 = 약한 블러 + 옅은 어둡기.
    // 만든 뒤에는 씬의 BlurredImage 인스펙터에서 조절하면 된다(인벤 패널 블러는 6).
    private const float BlurStrength = 1.5f;        // 블러 강도(낮을수록 배경 또렷)
    private const int BlurIterations = 3;           // 반복(낮을수록 덜 뭉갬)
    private const float BlurSampleDistance = 1.1f;  // 샘플 간격
    private const float DimAlpha = 0.28f;           // 블러 위 어둡기(낮을수록 기존 화면 느낌)

    private static readonly Color DimColor = new Color(0f, 0f, 0f, DimAlpha);
    private static readonly Color AccentColor = new Color(1f, 0.85f, 0.2f, 1f);      // 금색 액센트(페이지표시/확인바). 흰색은 영상 테두리에만.
    private static readonly Color ArrowColor = new Color(0.93f, 0.96f, 1f, 0.92f);   // 화살표
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.18f);          // divider 선

    [MenuItem("Tools/TIMEKOV/튜토리얼 영상 팝업 생성")]
    public static void Build()
    {
        var canvas = UIBuilderUtil.FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("오류", "씬에 루트 Canvas 가 없습니다.", "확인");
            return;
        }

        if (Object.FindAnyObjectByType<TutorialVideoUI>(FindObjectsInactive.Include) != null)
        {
            if (!EditorUtility.DisplayDialog("확인", "영상 팝업이 이미 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소")) return;
            UIBuilderUtil.RemoveExisting<TutorialVideoUI>();
        }

        var frameSprite = LoadSprite(FrameSpritePath);
        var videoFrameSprite = LoadSprite(VideoFramePath);
        var keycapSprite = LoadSprite(KeycapPath);

        Transform parent = UIBuilderUtil.EnsureGroup(canvas, "Overlays");

        // ── 루트 ──────────────────────────────────────────────────────
        var rootGo = new GameObject(RootName, typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(rootGo, "Build TutorialVideoUI");
        var root = (RectTransform)rootGo.transform;
        Stretch(root);

        var cv = rootGo.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 5000;   // 거의 최상단(다른 UI 위)
        var raycaster = rootGo.AddComponent<GraphicRaycaster>();
        var group = rootGo.AddComponent<CanvasGroup>();

        // 영상 재생기는 루트에. 출력 대상(RenderTexture)만 런타임이 물린다.
        var vp = rootGo.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.source = VideoSource.VideoClip;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.isLooping = true;
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = true;
        vp.audioOutputMode = VideoAudioOutputMode.None;   // 무음 데모

        // ── 1) 전체화면 블러 (게임 화면을 통째로 뭉갬 - 시선을 영상으로) ──
        // Graphic 은 오브젝트당 1개라 별도 GameObject 로 둔다.
        var blurGo = new GameObject("Blur", typeof(RectTransform));
        blurGo.transform.SetParent(root, false);
        Stretch((RectTransform)blurGo.transform);
        var blur = blurGo.AddComponent<BlurredImage>();
        ConfigureBlur(blur);

        // ── 2) 블러 위 어두운 틴트(시선 집중 + 텍스트 가독). 뒤 클릭 차단도 담당 ──
        var dim = NewImage("Dim", root, DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // ── 3) 콘텐츠 (투명 - 패널 박스 없음, 블러 위에 요소만) ──
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(root, false);
        var cr = (RectTransform)contentGo.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(960f, 820f);
        cr.anchoredPosition = Vector2.zero;

        // 영상 프레임 (상단 16:9)
        var videoFrame = NewImage("VideoFrame", cr, Color.black);
        var vf = videoFrame.rectTransform;
        vf.anchorMin = vf.anchorMax = new Vector2(0.5f, 1f);
        vf.pivot = new Vector2(0.5f, 1f);
        vf.sizeDelta = new Vector2(872f, 491f);
        vf.anchoredPosition = new Vector2(0f, -6f);

        var videoImage = NewRawImage("VideoImage", vf);
        SetInset(videoImage.rectTransform, 5f);

        // 영상 테두리 = 인벤 영역 강조 프레임(흰색 9-slice). 없으면 sci-fi 프레임으로 폴백.
        // 액자처럼 영상 가장자리에 딱 붙게: @2x 슬롯프레임 -> ppu 2(코너 22px) + 7px 바깥 outset (인벤 방식).
        var vborder = NewImage("VideoBorder", vf, Color.white);
        SetInset(vborder.rectTransform, -7f);   // 영상보다 7px 바깥
        if (videoFrameSprite != null)
        {
            vborder.sprite = videoFrameSprite;
            vborder.type = Image.Type.Sliced;
            vborder.pixelsPerUnitMultiplier = 2f;
        }
        else if (frameSprite != null)
        {
            vborder.sprite = frameSprite;
            vborder.type = Image.Type.Sliced;
            vborder.pixelsPerUnitMultiplier = 4f;
        }

        // 플레이스홀더 (clip 없는 페이지용)
        var placeholder = NewImage("Placeholder", vf, new Color(0.08f, 0.1f, 0.14f, 1f));
        SetInset(placeholder.rectTransform, 5f);
        var ph = NewText("PlaceholderText", placeholder.transform);
        Stretch(ph.rectTransform);
        ph.alignment = TextAlignmentOptions.Center;
        ph.fontSize = 26f;
        ph.color = new Color(1f, 1f, 1f, 0.5f);
        ph.text = "영상 준비 중";
        placeholder.gameObject.SetActive(false);

        // 페이지 표시 "1 / 2"
        var pageCount = NewText("PageCount", cr);
        TopCentered(pageCount.rectTransform, 380f, 28f, -515f);
        pageCount.alignment = TextAlignmentOptions.Center;
        pageCount.fontSize = 20f;
        pageCount.color = AccentColor;

        // 제목
        var title = NewText("Title", cr);
        TopCentered(title.rectTransform, 872f, 46f, -543f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = 32f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;

        // divider (제목 / 본문 구분)
        var div = NewImage("Divider", cr, LineColor);
        TopCentered(div.rectTransform, 760f, 2f, -594f);

        // 본문
        var body = NewText("Body", cr);
        TopCentered(body.rectTransform, 820f, 150f, -610f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSize = 22f;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.color = new Color(0.92f, 0.94f, 0.98f, 1f);

        // 하단 힌트 = [Q][E] 키캡 + 설명
        var hintRow = BuildHintRow(cr, keycapSprite);

        // "확인" 바 (마지막/단일 페이지 = 닫기 가능할 때만 표시)
        var confirmBar = BuildConfirmBar(cr, frameSprite);
        confirmBar.SetActive(false);

        // 화살표(셰브론) - 영상 좌우 바깥. 키캡 Q/E 함께.
        var chevL = BuildChevron(root, false, "Q", keycapSprite);
        var chevR = BuildChevron(root, true, "E", keycapSprite);

        // ── 컴포넌트 + 참조 연결 ──────────────────────────────────────
        var ui = rootGo.AddComponent<TutorialVideoUI>();
        var so = new SerializedObject(ui);
        SetRef(so, "canvasComp", cv);
        SetRef(so, "raycaster", raycaster);
        SetRef(so, "group", group);
        SetRef(so, "blur", blur);
        SetRef(so, "videoPlayer", vp);
        SetRef(so, "videoImage", videoImage);
        SetRef(so, "placeholder", placeholder.gameObject);
        SetRef(so, "pageCount", pageCount);
        SetRef(so, "titleText", title);
        SetRef(so, "bodyText", body);
        SetRef(so, "hintRow", hintRow);
        SetRef(so, "confirmBar", confirmBar);
        SetRef(so, "chevronLeft", chevL);
        SetRef(so, "chevronRight", chevR);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 절차 스프라이트(링/셰브론)를 지금 한 번 채워 에디터에서도 제 모양으로 보이게 한다.
        UIBuilderUtil.ApplyGeneratedSprites(rootGo);

        // 오브젝트는 항상 활성으로 두고 Canvas 만 끈다(런타임 LateUpdate 가 계속 돌아야 설정창 복귀를 감지한다).
        // 에디터에서도 꺼두는 이유: 켜두면 전체화면 블러+딤이 씬/게임 뷰를 통째로 덮어 다른 작업이 불가능하다.
        // 레이아웃을 눈으로 보며 조정하려면 이 Canvas 체크박스를 잠깐 켜라(실행하면 어차피 자동으로 꺼진다).
        cv.enabled = false;
        raycaster.enabled = false;

        Selection.activeGameObject = rootGo;
        EditorUtility.DisplayDialog("완료",
            "튜토리얼 영상 팝업을 만들었습니다.\n\n" +
            "위치: Canvas/Overlays/" + RootName + "\n\n" +
            "평소엔 Canvas 컴포넌트가 꺼져 있어 화면에 안 보입니다.\n" +
            "레이아웃을 보며 조정하려면 Canvas 체크를 잠깐 켜세요.\n\n" +
            "Ctrl+S 로 씬을 저장하세요.", "확인");
    }

    // ── 부품 ──────────────────────────────────────────────────────────

    // "확인" 바. 마지막(또는 단일) 페이지에서만 뜬다.
    private static GameObject BuildConfirmBar(RectTransform parent, Sprite frameSprite)
    {
        var go = new GameObject("ConfirmBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(320f, 56f);
        rt.anchoredPosition = new Vector2(0f, 6f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.10f);   // 옅은 흰 바
        bg.raycastTarget = false;

        if (frameSprite != null)
        {
            var border = NewImage("ConfirmFrame", rt, AccentColor);
            Stretch(border.rectTransform);
            border.sprite = frameSprite;
            border.type = Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = 4f;
        }

        var label = NewText("ConfirmLabel", rt);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.text = "확인";

        return go;
    }

    // 하단 힌트 행: [Q][E] 키캡 + 안내 문구. 가운데 정렬.
    private static GameObject BuildHintRow(RectTransform parent, Sprite keycapSprite)
    {
        var go = new GameObject("HintRow", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 6f);

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 7f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildKeycap(rt, "Q", 28f, keycapSprite);
        BuildKeycap(rt, "E", 28f, keycapSprite);

        var txt = NewText("HintText", rt);
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.fontSize = 17f;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.color = new Color(0.72f, 0.76f, 0.82f, 1f);
        txt.text = "를 누르거나 좌우 화살표를 눌러 페이지를 넘길 수 있으며, 모두 읽은 후에는 창을 닫을 수 있습니다.";

        return go;
    }

    // 화살표 = 원형 링 안에 셰브론, 그 바깥 옆에 키 알파벳(Q/E).
    // right=true 면 오른쪽(>, 다음/E), false 면 왼쪽(<, 이전/Q).
    private static CanvasGroup BuildChevron(RectTransform parent, bool right, string keyLabel, Sprite keycapSprite)
    {
        float s = right ? 1f : -1f;
        var go = new GameObject(right ? "ChevronRight" : "ChevronLeft", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        // 화면 가장자리가 아니라 영상 가장자리 바로 옆(중앙 기준). 영상 반폭 ~436 -> 558 이면 링이 충분히 떨어진다.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 120f);
        rt.anchoredPosition = new Vector2(s * 558f, 155f);
        var cg = go.AddComponent<CanvasGroup>();   // 갈 수 없는 방향을 흐리게 하는 용도

        float ringX = -s * 22f;   // 링 = 영상쪽(안쪽)
        float keyX = s * 54f;     // 키캡 = 바깥쪽 (링과 살짝 띄움)

        // 원형 링
        var ring = NewImage("Ring", rt, ArrowColor);
        CenterAt(ring.rectTransform, new Vector2(70f, 70f), new Vector2(ringX, 0f));
        var ringGen = ring.gameObject.AddComponent<RuntimeGeneratedSprite>();
        ringGen.shape = RuntimeGeneratedSprite.Shape.Ring;
        ringGen.texSize = 96;
        ringGen.ringThickness = 2.5f;

        // 셰브론 (링 안)
        var icon = NewImage("Icon", rt, ArrowColor);
        icon.preserveAspect = true;
        CenterAt(icon.rectTransform, new Vector2(28f, 40f), new Vector2(ringX, 0f));
        icon.rectTransform.localScale = new Vector3(s, 1f, 1f);   // 왼쪽이면 좌우반전
        var iconGen = icon.gameObject.AddComponent<RuntimeGeneratedSprite>();
        iconGen.shape = RuntimeGeneratedSprite.Shape.Chevron;
        iconGen.texSize = 64;
        iconGen.chevronThickness = 7f;
        iconGen.chevronAspect = 0.75f;

        // 키캡 (링 바깥쪽) - 하단 힌트와 동일 디자인
        var kc = BuildKeycap(rt, keyLabel, 46f, keycapSprite);
        CenterAt((RectTransform)kc.transform, new Vector2(46f, 46f), new Vector2(keyX, 0f));

        return cg;
    }

    // 키캡 = 기존 게임 키캡 스프라이트 + 글자. 스프라이트가 없으면 코드 박스로 폴백.
    // 화살표 옆(CenterAt 으로 배치) / 힌트(HorizontalLayoutGroup) 양쪽에서 동일하게 쓴다.
    private static GameObject BuildKeycap(Transform parent, string letter, float size, Sprite keycapSprite)
    {
        var go = new GameObject("Keycap_" + letter, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);

        var box = go.AddComponent<Image>();
        box.raycastTarget = false;
        if (keycapSprite != null)
        {
            box.sprite = keycapSprite;
            box.type = Image.Type.Simple;
            box.preserveAspect = true;
            box.color = Color.white;
        }
        else
        {
            box.color = new Color(0.85f, 0.9f, 1f, 0.9f);   // 폴백: 밝은 테두리
            var inner = NewImage("Inner", rt, new Color(0.10f, 0.12f, 0.16f, 0.95f));
            SetInset(inner.rectTransform, size * 0.06f);
        }

        var le = go.AddComponent<LayoutElement>();   // 힌트 HorizontalLayoutGroup 에서 크기 고정
        le.preferredWidth = size;
        le.preferredHeight = size;

        var t = NewText("Letter", rt);
        SetInset(t.rectTransform, size * 0.15f);     // 테두리 피해 가운데
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = size * 0.46f;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.text = letter;
        return go;
    }

    // ── 블러 설정 (인벤 InventoryUIBuilder 의 검증된 셋업 복제) ────────
    // 여기서 넣은 값은 씬에 저장된다. 카메라만 실행 중에 물린다(Camera.main).
    private static void ConfigureBlur(BlurredImage blur)
    {
        blur.color = Color.white;
        blur.raycastTarget = false;
        blur.Common.blurReferencesFrom = UIBlurCommon.BlurReferencesFrom.Self;
        blur.Common.featureNumber = 0;
        blur.Common.unrankedLayer = 1;
        var bs = blur.Common.blurInstanceSettings;
        if (bs != null)
        {
            bs.blurAdditionalDistancePerIteration = BlurStrength;   // 약하게 = 배경 알아볼 정도(화면 전환 느낌 방지)
            if (bs.blurSections != null)
                foreach (var sec in bs.blurSections) { sec.iterations = BlurIterations; sec.sampleDistance = BlurSampleDistance; }
            bs.vibrancy = 0f;
            bs.brightness = 0f;
            bs.contrast = 0f;
            bs.referenceResolution = 1080;
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning("[TutorialVideoUIBuilder] 스프라이트를 못 찾음: " + path + " -> 폴백으로 만든다(인스펙터에서 직접 물려도 됨).");
        return s;
    }

    private static Image NewImage(string name, Transform parent, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    private static RawImage NewRawImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetInset(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // 중앙(0.5,0.5) 앵커로 크기+오프셋 배치
    private static void CenterAt(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    // 콘텐츠(중앙) 기준 상단정렬 배치: 폭/높이 + 상단에서의 y오프셋(음수)
    private static void TopCentered(RectTransform rt, float w, float h, float yFromTop)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, yFromTop);
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[TutorialVideoUIBuilder] 필드를 못 찾음: " + field);
    }
}
