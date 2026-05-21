using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Claude 디자인 "Cinematic Minimal" 메인 메뉴 자동 셋업.
/// - bg-mountain 풀스크린 배경 + Scrim + 위/아래 Letterbox
/// - 화면 하단에서 위로 천천히 떠오르는 먼지 ParticleSystem
/// - TIMEKOV 로고 (Cinzel + VertexGradient) + ECHOES OF THE DRIFT (Rajdhani)
/// - "‹ 화면을 클릭하여 시작 ›" (Rajdhani + Pretendard fallback) + 펄스
///
/// 메뉴: Tools > UI > Build MainMenu (Cinematic)
///
/// 사전 조건:
///   - Resources/Image/Main2/assets/bg-mountain.png (Texture Type 은 빌더가 자동 보정)
///   - Assets/11.Font/Cinzel-Variable.ttf
///   - Assets/11.Font/Rajdhani-SemiBold.ttf, Rajdhani-Regular.ttf
///   - Assets/11.Font/Pretendard-ExtraBold SDF.asset (한글 fallback)
///   - 씬에 Main Camera
/// </summary>
public static class MainMenuBuilder
{
    const string BgAssetPath = "Assets/Resources/Image/Main2/assets/bg-mountain.png";
    const string BgResourcePath = "Image/Main2/assets/bg-mountain";
    const string RootName = "MainMenu_Cinematic";
    const string FontFolder = "Assets/11.Font";
    const string CinzelTtf = FontFolder + "/Cinzel-Variable.ttf";
    const string RajdhaniSemiTtf = FontFolder + "/Rajdhani-SemiBold.ttf";
    const string RajdhaniRegTtf = FontFolder + "/Rajdhani-Regular.ttf";
    const string PretendardSdf = FontFolder + "/Pretendard-ExtraBold SDF.asset";
    const string CinzelSdfPath = FontFolder + "/Cinzel SDF.asset";
    const string RajdhaniSemiSdfPath = FontFolder + "/Rajdhani-SemiBold SDF.asset";
    const string RajdhaniRegSdfPath = FontFolder + "/Rajdhani-Regular SDF.asset";

    [MenuItem("Tools/UI/Build MainMenu (Cinematic)")]
    public static void Build()
    {
        // 1. 자산 로드 / 폰트 SDF 자동 생성
        EnsureBgIsSprite();  // textureType이 Default면 Sprite로 자동 변경
        var sprite = Resources.Load<Sprite>(BgResourcePath);
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("자산 없음",
                $"Sprite 로드 실패: Resources/{BgResourcePath}\n" +
                $"{BgAssetPath} 존재 여부 확인.", "OK");
            return;
        }

        var cinzelSdf = GetOrCreateSdf(CinzelTtf, CinzelSdfPath);
        var rajdhaniSemiSdf = GetOrCreateSdf(RajdhaniSemiTtf, RajdhaniSemiSdfPath);
        var rajdhaniRegSdf = GetOrCreateSdf(RajdhaniRegTtf, RajdhaniRegSdfPath);
        var pretendardSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardSdf);
        // LiberationSans (Unity TMP 기본) — ◂ ▸ 같은 기하 글리프 fallback 용
        var liberationSans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        // Rajdhani fallback: Pretendard(한글) + LiberationSans(◂▸ 등 기하 문자)
        if (rajdhaniRegSdf != null)
        {
            if (pretendardSdf != null) AddFallback(rajdhaniRegSdf, pretendardSdf);
            if (liberationSans != null) AddFallback(rajdhaniRegSdf, liberationSans);
        }

        var cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("카메라 없음",
                "씬에 Main Camera 없음. 추가 후 다시 실행.", "OK");
            return;
        }

        // 2. 기존 같은 이름 root 삭제
        var existing = GameObject.Find($"/{RootName}");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("덮어쓰기",
                $"기존 '{RootName}' 발견. 삭제하고 새로 생성?",
                "덮어쓰기", "취소")) return;
            Object.DestroyImmediate(existing);
        }

        // 3. Canvas (Screen Space Camera)
        var canvasGo = new GameObject(RootName,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10f;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 4. BG (산 풍경, 화면 채움)
        var bg = CreateUIChild(canvasGo.transform, "BG_Mountain");
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.sprite = sprite;
        bgImg.preserveAspect = false;
        StretchFull(bg);

        // 5. Scrim (어두운 비넷)
        var scrim = CreateUIChild(canvasGo.transform, "Scrim");
        var scrimImg = scrim.gameObject.AddComponent<Image>();
        scrimImg.color = new Color(0.03f, 0.05f, 0.08f, 0.35f);
        StretchFull(scrim);

        // 6. (Letterbox 제거 — 디자인 의도상 풀스크린 BG 유지)

        // 7. Particles (Canvas UI Image 18개 — 디자인이 권장한 방식, CSS와 1:1 매핑)
        SetupParticles(canvasGo.transform);

        // ── 디자인 캔버스 750×416 → 1920×1080 환산 배율 (X 2.56, Y ≈ 2.6) ──
        // SPEC.md 기준 픽셀 값에 이 배율을 곱해 Unity 값으로 변환.

        // 8. TIMEKOV 로고 (Cinzel + 세로 그라데이션 + 입체 머티리얼)
        //    SPEC: Cinzel SemiBold 62px → 159, letter-spacing 5px → 13
        var logo = CreateUIChild(canvasGo.transform, "Logo_Timekov");
        var logoTmp = logo.gameObject.AddComponent<TextMeshProUGUI>();
        logoTmp.text = "TIMEKOV";
        logoTmp.fontSize = 159;
        logoTmp.alignment = TextAlignmentOptions.Center;
        logoTmp.color = Color.white;
        logoTmp.characterSpacing = 13;
        logoTmp.fontStyle = FontStyles.Bold;
        if (cinzelSdf != null) logoTmp.font = cinzelSdf;

        // 세로 그라데이션 (SPEC: 흰색→#bfe4ff→#7ea8c4→흰색 4-stop).
        // TMP VertexGradient는 4-corner만 지원 → 핵심 색감(상단 흰색, 하단 청회색)으로 단순화.
        logoTmp.enableVertexGradient = true;
        logoTmp.colorGradient = new VertexGradient(
            Color.white,                  // TL
            Color.white,                  // TR
            HexColor("#7EA8C4"),          // BL
            HexColor("#7EA8C4")           // BR
        );

        logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.5f);
        logo.pivot = new Vector2(0.5f, 0.5f);
        // 로고+underline+tagline 그룹의 시각 중심이 화면 중앙에 오도록 wordmark는 위로 살짝
        logo.anchoredPosition = new Vector2(0, 60);
        logo.sizeDelta = new Vector2(1400, 220);

        // 머티리얼: Outline + Underlay + Glow + Bevel + Lighting (조각된 얼음 느낌)
        ApplyCinzelMaterial(logoTmp);

        // 9. Underline accent (장식선)
        //    SPEC: 273×11 SVG (다이아몬드 + 원). 여기는 단순 라인. 다이아몬드/원은 PNG sprite 필요 시 별도 작업.
        var underline = CreateUIChild(canvasGo.transform, "Logo_Underline");
        var underImg = underline.gameObject.AddComponent<Image>();
        underImg.color = new Color(0.749f, 0.894f, 1f, 0.85f);
        underline.anchorMin = underline.anchorMax = new Vector2(0.5f, 0.5f);
        underline.pivot = new Vector2(0.5f, 0.5f);
        underline.anchoredPosition = new Vector2(0, -50);
        underline.sizeDelta = new Vector2(700, 2f);

        // 10. Tagline (Rajdhani)
        //     SPEC: Rajdhani 400, 11.2px → 29, letter-spacing 5px → 13
        var tagline = CreateUIChild(canvasGo.transform, "Tagline");
        var taglineTmp = tagline.gameObject.AddComponent<TextMeshProUGUI>();
        taglineTmp.text = "ECHOES OF THE DRIFT";
        taglineTmp.fontSize = 29;
        taglineTmp.alignment = TextAlignmentOptions.Center;
        taglineTmp.color = new Color(0.86f, 0.92f, 1f, 0.65f);
        taglineTmp.characterSpacing = 13;
        if (rajdhaniRegSdf != null) taglineTmp.font = rajdhaniRegSdf;
        tagline.anchorMin = tagline.anchorMax = new Vector2(0.5f, 0.5f);
        tagline.pivot = new Vector2(0.5f, 0.5f);
        tagline.anchoredPosition = new Vector2(0, -85);
        tagline.sizeDelta = new Vector2(900, 40);

        // 11. Press Prompt (◂ + 화면을 클릭하여 시작 + ▸)
        //     SPEC: bottom 28 → 73, 본문 13px → 33, chevron 11px → 28, letter-spacing 5px → 13
        var prompt = CreateUIChild(canvasGo.transform, "PressPrompt");
        prompt.anchorMin = new Vector2(0, 0);
        prompt.anchorMax = new Vector2(1, 0);
        prompt.pivot = new Vector2(0.5f, 0);
        prompt.anchoredPosition = new Vector2(0, 73);
        prompt.sizeDelta = new Vector2(0, 60);

        // 가로 레이아웃: ChevronLeft  Text  ChevronRight
        var hLayout = prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.spacing = 36;  // SPEC gap 14px → 36
        hLayout.childControlHeight = true;
        hLayout.childControlWidth = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        // 그룹 전체 펄스 → CanvasGroup + MainMenuPulse
        prompt.gameObject.AddComponent<CanvasGroup>();
        prompt.gameObject.AddComponent<MainMenuPulse>();

        // 좌측 chevron ‹ (U+2039 — 라틴 확장. ◂ U+25C2 는 대부분 폰트에 없어서 □ 로 깨짐)
        BuildChevron(prompt, "ChevronLeft", "‹", rajdhaniRegSdf);
        // 중앙 한글 텍스트
        var promptText = CreateUIChild(prompt, "Text");
        var promptTmp = promptText.gameObject.AddComponent<TextMeshProUGUI>();
        promptTmp.text = "화면을 클릭하여 시작";
        promptTmp.fontSize = 33;
        promptTmp.alignment = TextAlignmentOptions.Center;
        promptTmp.color = new Color(0.9f, 0.94f, 1f, 0.92f);
        promptTmp.characterSpacing = 13;
        promptTmp.fontStyle = FontStyles.UpperCase;
        if (rajdhaniRegSdf != null) promptTmp.font = rajdhaniRegSdf;
        var promptFitter = promptText.gameObject.AddComponent<ContentSizeFitter>();
        promptFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        promptFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // 우측 chevron › (U+203A)
        BuildChevron(prompt, "ChevronRight", "›", rajdhaniRegSdf);

        // 12. Version chip (top-left)
        //     SPEC: top 18 → 47, left 22 → 56, fontSize 10px → 26, letter-spacing 3px → 8
        var version = CreateUIChild(canvasGo.transform, "VersionChip");
        var versionTmp = version.gameObject.AddComponent<TextMeshProUGUI>();
        versionTmp.text = "V 0.7.4 — CLOSED BETA\nBUILD 2024.11.18";
        versionTmp.fontSize = 18;
        versionTmp.alignment = TextAlignmentOptions.TopLeft;
        versionTmp.color = new Color(0.86f, 0.92f, 1f, 0.55f);
        versionTmp.characterSpacing = 8;
        versionTmp.lineSpacing = 6;
        if (rajdhaniRegSdf != null) versionTmp.font = rajdhaniRegSdf;
        version.anchorMin = new Vector2(0, 1);
        version.anchorMax = new Vector2(0, 1);
        version.pivot = new Vector2(0, 1);
        version.anchoredPosition = new Vector2(56, -47);
        version.sizeDelta = new Vector2(400, 60);

        Selection.activeGameObject = canvasGo;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log($"[MainMenuBuilder] '{RootName}' 생성 완료. Ctrl+S로 씬 저장.");
    }

    // ─── bg-mountain 텍스처 임포트 자동 보정 ───
    // Default Texture로 들어오면 Resources.Load<Sprite>가 null. Sprite로 강제 변환.
    static void EnsureBgIsSprite()
    {
        var importer = AssetImporter.GetAtPath(BgAssetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MainMenuBuilder] 텍스처 임포터 없음: {BgAssetPath}");
            return;
        }

        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (dirty)
        {
            importer.SaveAndReimport();
            Debug.Log($"[MainMenuBuilder] bg-mountain Texture Type → Sprite로 자동 변경");
        }
    }

    // ─── 폰트 SDF 자동 생성 ───
    //  atlas Texture + material 을 sub-asset 으로 명시 등록.
    //  안 그러면 도메인 리로드 후 atlas / material 휘발 → "Atlas Texture missing" 에러.
    //  기존 asset 의 atlas 또는 material 이 null 이면 깨진 것 → 삭제 후 재생성.
    static TMP_FontAsset GetOrCreateSdf(string ttfPath, string sdfPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
        if (existing != null)
        {
            if (existing.atlasTexture != null && existing.material != null)
                return existing;

            Debug.LogWarning($"[MainMenuBuilder] 기존 SDF 손상 ({sdfPath}) → 재생성");
            AssetDatabase.DeleteAsset(sdfPath);
        }

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null)
        {
            Debug.LogWarning($"[MainMenuBuilder] TTF 못 찾음: {ttfPath} (기본 TMP 폰트 사용)");
            return null;
        }

        // Dynamic SDF (런타임에 글자 자동 추가)
        var sdf = TMP_FontAsset.CreateFontAsset(ttf);
        var sdfName = System.IO.Path.GetFileNameWithoutExtension(sdfPath);
        sdf.name = sdfName;

        AssetDatabase.CreateAsset(sdf, sdfPath);

        // atlas Texture / Material 을 sub-asset 으로 박아 영구 저장
        if (sdf.atlasTexture != null)
        {
            sdf.atlasTexture.name = sdfName + " Atlas";
            AssetDatabase.AddObjectToAsset(sdf.atlasTexture, sdf);
        }
        if (sdf.material != null)
        {
            sdf.material.name = sdfName + " Material";
            AssetDatabase.AddObjectToAsset(sdf.material, sdf);
        }

        EditorUtility.SetDirty(sdf);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(sdfPath);
        Debug.Log($"[MainMenuBuilder] SDF 생성: {sdfPath}");
        return sdf;
    }

    // ─── 로고 "조각된 얼음" 머티리얼 ───
    // Outline + Underlay + Glow + Bevel + Lighting (디자인 답변 가이드대로)
    static void ApplyCinzelMaterial(TextMeshProUGUI tmp)
    {
        if (tmp.fontSharedMaterial == null) return;

        // sharedMaterial 안 건드리고 인스턴스 복사
        var mat = new Material(tmp.fontSharedMaterial) { name = "Logo_Timekov_Mat" };

        // Bevel/Lighting 은 SDF Surface 셰이더에서만 동작. 기본 SDF 면 강제 교체.
        var surface = Shader.Find("TextMeshPro/Distance Field Surface");
        if (surface != null) mat.shader = surface;

        // Outline (얼음 가장자리 어두운 청록)
        mat.SetColor("_OutlineColor", HexColor("#1A3850"));
        mat.SetFloat("_OutlineWidth", 0.1f);

        // Underlay (drop shadow)
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 1f));
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", -1f);
        mat.SetFloat("_UnderlayDilate", 0.3f);
        mat.SetFloat("_UnderlaySoftness", 0.8f);

        // Glow (시안 빛)
        mat.SetColor("_GlowColor", HexColor("#7EC8E8"));
        mat.SetFloat("_GlowOffset", 0f);
        mat.SetFloat("_GlowInner", 0.3f);
        mat.SetFloat("_GlowOuter", 0.4f);
        mat.SetFloat("_GlowPower", 1f);

        // Bevel (Outer — 입체감)
        mat.SetFloat("_Bevel", 0.5f);
        mat.SetFloat("_BevelOffset", 0f);
        mat.SetFloat("_BevelWidth", 0.3f);
        mat.SetFloat("_BevelRoundness", 0.3f);
        mat.SetFloat("_BevelClamp", 0.5f);

        // Lighting (베벨 비추는 광원)
        mat.SetFloat("_LightAngle", 3.1416f);  // 위에서 비추는 빛
        mat.SetColor("_SpecularColor", Color.white);
        mat.SetFloat("_SpecularPower", 2f);
        mat.SetFloat("_Reflectivity", 10f);
        mat.SetFloat("_Diffuse", 0.5f);
        mat.SetFloat("_Ambient", 0.5f);

        // 키워드 활성 — TMP SDF Surface 셰이더가 이 키워드로 효과 활성
        mat.EnableKeyword("OUTLINE_ON");
        mat.EnableKeyword("UNDERLAY_ON");
        mat.EnableKeyword("GLOW_ON");
        mat.EnableKeyword("BEVEL_ON");

        tmp.fontMaterial = mat;
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    static void AddFallback(TMP_FontAsset main, TMP_FontAsset fallback)
    {
        if (main.fallbackFontAssetTable == null)
            main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!main.fallbackFontAssetTable.Contains(fallback))
        {
            main.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(main);
        }
    }

    static RectTransform CreateUIChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void BuildChevron(Transform parent, string name, string glyph, TMP_FontAsset font)
    {
        var rt = CreateUIChild(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = glyph;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.745f, 0.863f, 1f, 0.7f);  // SPEC: rgba(190,220,255,0.7)
        tmp.characterSpacing = 10;
        if (font != null) tmp.font = font;
        var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void CreateLetterbox(Transform parent, string name, bool top, float height)
    {
        var rect = CreateUIChild(parent, name);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = Color.black;
        rect.anchorMin = top ? new Vector2(0, 1) : new Vector2(0, 0);
        rect.anchorMax = top ? new Vector2(1, 1) : new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, top ? 1 : 0);
        rect.sizeDelta = new Vector2(0, height);
        rect.anchoredPosition = Vector2.zero;
    }

    static void SetupParticles(Transform canvasTransform)
    {
        // Particles 컨테이너 (RectMask2D로 화면 밖 입자 자동 클리핑)
        var particles = CreateUIChild(canvasTransform, "Particles");
        StretchFull(particles);
        particles.gameObject.AddComponent<RectMask2D>();
        // Image 안 박고 RectMask2D만으로 마스킹 (TMP Pre-render 안전)
        var canvasGroup = particles.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // ParticlePrefab (비활성 GameObject — TimekovParticles가 Instantiate)
        var prefab = new GameObject("ParticlePrefab",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        prefab.transform.SetParent(canvasTransform, false);
        prefab.SetActive(false);

        var pRect = prefab.GetComponent<RectTransform>();
        pRect.anchorMin = pRect.anchorMax = new Vector2(0, 0);
        pRect.pivot = new Vector2(0.5f, 0.5f);
        pRect.sizeDelta = new Vector2(2, 2);

        var pImg = prefab.GetComponent<Image>();
        pImg.color = new Color(0.78f, 0.88f, 1f, 1f);  // RGBA(200,225,255,1)
        pImg.raycastTarget = false;
        // sprite = null → 흰 사각형. 1~2.5px이라 점처럼 보임

        // TimekovParticles 컴포넌트 부착 + slot 바인딩
        var tp = particles.gameObject.AddComponent<TimekovParticles>();
        var so = new SerializedObject(tp);
        so.FindProperty("frame").objectReferenceValue = particles;
        so.FindProperty("particlePrefab").objectReferenceValue = prefab;
        so.FindProperty("count").intValue = 18;
        so.ApplyModifiedProperties();
    }

    static void ConfigureDustParticles_Unused(ParticleSystem ps)
    {
        // 원본 tk-rise 키프레임 (index.html):
        //   0%  → opacity 0
        //   10% → opacity 0.7    (빠른 fade in)
        //   90% → opacity 0.5
        //   100% → translateY(-460px) translateX(20px), opacity 0
        //   duration 14~24s linear
        // Unity: Canvas referenceResolution 1080 기준 460px ≈ 5.3 world unit (대략)
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 24f);
        main.startSpeed = 0f;  // velocityOverLifetime에서 직접 제어
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new Color(0.78f, 0.88f, 1f, 1f);  // 알파는 colorOverLifetime에서
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = 30;  // 원본 18개 + 여유

        var emission = ps.emission;
        emission.rateOverTime = 1.3f;  // 19초 평균 lifetime 동안 ~25개 → 동시 18~22개

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(7f, 0.1f, 0.1f);  // Canvas 너비 cover
        shape.position = Vector3.zero;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        // 14~24초 동안 위로 ~5유닛 → 평균 0.27/초. 오른쪽 +20px ≈ 0.23유닛 → 0.012/초
        vel.x = new ParticleSystem.MinMaxCurve(0.005f, 0.015f);
        vel.y = new ParticleSystem.MinMaxCurve(0.20f, 0.35f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 원본 tk-rise alpha 키프레임 그대로
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f, 0f),     // 0% — fade in 시작
                new GradientAlphaKey(0.7f, 0.1f), // 10% — peak
                new GradientAlphaKey(0.5f, 0.9f), // 90% — fade out 시작
                new GradientAlphaKey(0f, 1f)      // 100% — 사라짐
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        // Canvas Screen Space Camera 안에서 ParticleSystem이 Canvas 다른 UI 위에 그려지도록
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 200;  // Canvas sortingOrder(100)보다 위

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Particles/Unlit")
                  ?? Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.material = new Material(shader) { name = "DustParticle_Mat" };
    }
}
