// SettingsPanelRebuilder.cs
// Tools/UI/Rebuild Settings Panel
// 기존 설정창(Canvas/Panels/SettingsPanel/Option/BG/Settings)의 평평하게 나열된 임시 row들을
// 정리하고, 엔드필드 스타일(상단 아이콘 탭바 + 탭별 스크롤 콘텐츠)로 재구성한다.
// 탭 아이콘은 기존 SettingsPanel_Icon_*.png를 임시로 재사용 — 추후 교체 예정.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using JeffGrawAssets.FlexibleUI;

public static class SettingsPanelRebuilder
{
    private const string MenuPath = "Tools/UI/Rebuild Settings Panel";
    private const string IconDir  = "Assets/Resources/Image/UI_Icon/Setting/";

    private static Sprite Load(string fileName) => AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + fileName);

    // 흰 실루엣 PNG 아이콘 세트(체크/새로고침/닫기) — TMP 유니코드 글리프(✓,↻)는 프로젝트의
    // 모든 폰트 에셋이 Static 아틀라스라 해당 글자가 없으면 그냥 깨져버려서(□) 아이콘 대신
    // 실제 스프라이트를 쓴다. 폰트 교체로는 못 고치는 문제.
    private const string IconKitDir = "Assets/SilentOutbreak_UIKIT/PNG/Icons/";
    private static Sprite LoadKitIcon(string fileName) => AssetDatabase.LoadAssetAtPath<Sprite>(IconKitDir + fileName);

    // 기본 TMP 폰트(LiberationSans SDF)는 Static 아틀라스라 이 패널에서 처음 쓰는 한글
    // 글자(그래픽/오디오/볼륨 등)가 깨져 나온다(□). 한글이 전부 포함된 폰트로 통일.
    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset KoreanFont =>
        _koreanFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/11.Font/Pretendard-SemiBold SDF.asset");

    // 버튼을 엔드필드처럼 확실하게 둥글게 만드는 전용 스프라이트.
    // Unity 기본 제공 UI/Skin/UISprite.psd는 모서리가 텍스처 10px / PPU 200이라
    // 화면에선 5px밖에 안 나와서 우리 컨트롤 높이(48~64)에서는 거의 안 보였다 —
    // 반경을 충분히 크게 준 라운드 사각형 텍스처를 직접 만들어 9-slice로 쓴다.
    private static Sprite _roundedPillSprite;
    private const string RoundedPillPath = "Assets/Resources/Image/UI_Icon/Setting/Generated_RoundedPill.png";

    // 드롭다운 열렸을 때 버튼용: 위쪽만 둥근 모서리, 아래는 직사각
    private static Sprite _roundedTopSprite;
    private const string RoundedTopPath = "Assets/Resources/Image/UI_Icon/Setting/Generated_RoundedTop.png";

    private static Sprite RoundedTopSprite()
    {
        if (_roundedTopSprite != null) return _roundedTopSprite;
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedTopPath);
        if (existing != null) { _roundedTopSprite = existing; return existing; }

        const int size = 64;
        const int radius = 28;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha;
                if (y < size - radius) // 하단부(y < 36): 완전 불투명 사각형
                {
                    alpha = 1f;
                }
                else // 상단부(y >= 36): 위쪽 모서리만 둥글게
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(y - (size - radius - 0.5f), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Clamp01(radius - dist + 0.5f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var pngBytes = tex.EncodeToPNG();
        Directory.CreateDirectory(Path.GetDirectoryName(RoundedTopPath));
        File.WriteAllBytes(RoundedTopPath, pngBytes);
        AssetDatabase.ImportAsset(RoundedTopPath);

        var importer = AssetImporter.GetAtPath(RoundedTopPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(radius, 0, radius, radius); // L,B=0,R,T: 하단 보더 없음
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        _roundedTopSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedTopPath);
        return _roundedTopSprite;
    }

    // 드롭다운 펼침 목록 전용: 위쪽은 완전 불투명 사각형, 아래쪽만 둥근 모서리
    // 버튼 위로 28px 겹쳐서 버튼 라벨이 비치지 않도록 상단을 불투명하게 유지
    private static Sprite _roundedBottomSprite;
    private const string RoundedBottomPath = "Assets/Resources/Image/UI_Icon/Setting/Generated_RoundedBottom.png";

    private static Sprite RoundedBottomSprite()
    {
        if (_roundedBottomSprite != null) return _roundedBottomSprite;
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedBottomPath);
        if (existing != null) { _roundedBottomSprite = existing; return existing; }

        const int size = 64;
        const int radius = 28;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha;
                if (y >= radius) // 상단부(y=radius 이상): 완전 불투명 사각형
                {
                    alpha = 1f;
                }
                else // 하단부(y < radius): 좌우 아래 모서리만 둥글게
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max((radius - 0.5f) - y, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Clamp01(radius - dist + 0.5f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var pngBytes = tex.EncodeToPNG();
        Directory.CreateDirectory(Path.GetDirectoryName(RoundedBottomPath));
        File.WriteAllBytes(RoundedBottomPath, pngBytes);
        AssetDatabase.ImportAsset(RoundedBottomPath);

        var importer = AssetImporter.GetAtPath(RoundedBottomPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(radius, radius, radius, 0); // L,B,R,T: top=0(상단 둥근모서리 없음)
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        _roundedBottomSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedBottomPath);
        return _roundedBottomSprite;
    }

    private static Sprite RoundedPillSprite()
    {
        if (_roundedPillSprite != null) return _roundedPillSprite;

        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPillPath);
        if (existing != null) { _roundedPillSprite = existing; return existing; }

        const int size = 64;
        const int radius = 28;
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

    private static void ApplyFont(TMP_Text t)
    {
        if (t != null && KoreanFont != null) t.font = KoreanFont;
    }

    [MenuItem(MenuPath)]
    static void Rebuild()
    {
        // Canvas 이름은 씬마다 다를 수 있어(MainMenu_Cinematic 등) 이름 대신
        // GlobalSettingsManager 컴포넌트를 통해 SettingsPanel을 직접 찾는다.
        var settingsMgr = Object.FindAnyObjectByType<GlobalSettingsManager>(FindObjectsInactive.Include);
        if (settingsMgr == null) { Debug.LogError("[SettingsRebuilder] GlobalSettingsManager를 찾을 수 없습니다."); return; }

        Transform settingsPanelT = settingsMgr.transform.parent;
        while (settingsPanelT != null && settingsPanelT.name != "SettingsPanel")
            settingsPanelT = settingsPanelT.parent;
        if (settingsPanelT == null)
        {
            // 폴백: 이름으로 직접 찾기
            var found = GameObject.Find("SettingsPanel");
            if (found != null) settingsPanelT = found.transform;
        }
        if (settingsPanelT == null) { Debug.LogError("[SettingsRebuilder] 'SettingsPanel'을 찾을 수 없습니다."); return; }

        Transform settingsBG = settingsPanelT.Find("Option/BG/Settings");
        if (settingsBG == null) { Debug.LogError("[SettingsRebuilder] 'Option/BG/Settings'를 찾을 수 없습니다."); return; }

        Undo.SetCurrentGroupName("Rebuild Settings Panel");
        int undoGroup = Undo.GetCurrentGroup();

        // 설정창은 평소 비활성(닫힌 상태)이라 Slider 등 활성 상태에서만 갱신되는 비주얼
        // (Fill/Handle 위치)이 빌드 시점에 갱신되지 않는다. 작업 중엔 강제로 활성화해두고 끝나면 복원.
        var activeChain = new List<(GameObject go, bool wasActive)>();
        for (var t = settingsBG; t != null; t = t.parent)
        {
            activeChain.Add((t.gameObject, t.gameObject.activeSelf));
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        }

        // ── 1. 기존 자식 중 살릴 것만 보존, 나머지 전부 삭제 ──────────────────
        // Btn_MainMenu는 더 이상 쓰지 않음 — 사용자가 풋터에서 제거 요청
        Transform title = settingsBG.Find("Title");
        Transform titleEn = settingsBG.Find("Title_en");

        var toDelete = new List<GameObject>();
        for (int i = 0; i < settingsBG.childCount; i++)
        {
            var child = settingsBG.GetChild(i).gameObject;
            if (child == title?.gameObject || child == titleEn?.gameObject)
                continue;
            toDelete.Add(child);
        }
        foreach (var go in toDelete) Undo.DestroyObjectImmediate(go);

        // ── 1b. 패널 전체화면화 + 배경 어둡게(거의 불투명) ────────────────────
        var bgRect = settingsBG.GetComponent<RectTransform>();
        Undo.RecordObject(bgRect, "Resize Settings Panel Fullscreen");
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 엔드필드 레퍼런스처럼 뒤쪽 게임 화면이 블러로 비치게 — 프로젝트에 이미 설치된
        // FlexibleUI 블러(URP Renderer Feature 등록까지 끝나있음)의 BlurredImage로 교체.
        // BlurredImage 셰이더는 SourceImageFade=0일 때 항상 "블러 원본 그대로(불투명)"만 출력하고
        // color/AlphaBlend는 그 경로에서 전혀 안 쓰임 — 즉 이 컴포넌트 자체로는 블러를 어둡게
        // 틴트할 수 없다(둘 중 하나: 순수 블러 또는 순수 단색, 섞이지 않음). 그래서 블러는
        // 그대로 보여주고, 그 위에 반투명 단색 DimOverlay를 따로 한 장 더 깔아서 어둡게 만든다.
        var bgImage = settingsBG.GetComponent<Image>();
        if (bgImage != null) Undo.DestroyObjectImmediate(bgImage);
        var blurredBg = Undo.AddComponent<BlurredImage>(settingsBG.gameObject);
        blurredBg.sprite = null;
        blurredBg.color = Color.white;
        blurredBg.Common.blurStrength = 1f;

        var dimOverlay = CreateUIObject("DimOverlay", settingsBG);
        var dimRect = dimOverlay.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImg = dimOverlay.AddComponent<Image>();
        dimImg.sprite = null;
        dimImg.color = new Color(0.05f, 0.055f, 0.045f, 0.92f); // 레퍼런스만큼 어두운 올리브 틴트
        dimImg.raycastTarget = false;
        Undo.RegisterCreatedObjectUndo(dimOverlay, "Create DimOverlay");

        // Title / Title_en 좌상단으로 재배치 (전체화면 기준 절대 위치)
        if (title != null)
        {
            var tr = title.GetComponent<RectTransform>();
            Undo.RecordObject(tr, "Reposition Title");
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot     = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(60f, -50f);
        }
        if (titleEn != null)
        {
            var tr = titleEn.GetComponent<RectTransform>();
            Undo.RecordObject(tr, "Reposition TitleEn");
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot     = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(170f, -50f);
        }

        // ── 2. 탭바 ──────────────────────────────────────────────────────────
        var tabBar = CreateUIObject("TabBar", settingsBG);
        var tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0.5f, 1f);
        tabBarRect.anchorMax = new Vector2(0.5f, 1f);
        tabBarRect.pivot     = new Vector2(0.5f, 1f);
        tabBarRect.anchoredPosition = new Vector2(0f, -50f);
        tabBarRect.sizeDelta = new Vector2(340f, 80f);
        var tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHlg.childAlignment = TextAnchor.MiddleCenter;
        tabHlg.spacing = 18f;
        tabHlg.childControlWidth  = true;  // false였던 게 버그 — LayoutElement 크기가 무시됨
        tabHlg.childControlHeight = true;
        tabHlg.childForceExpandWidth  = false;
        tabHlg.childForceExpandHeight = false;
        Undo.RegisterCreatedObjectUndo(tabBar, "Create TabBar");

        var tabNames  = new[] { "그래픽", "오디오", "조작" };
        var tabIcons  = new[] { Load("SettingsPanel_Icon_Fullscreen.png"), Load("SettingsPanel_Icon_BGM.png"), Load("SettingsPanel_Icon_Mouse.png") };
        var tabButtons = new Button[3];
        var tabHighlights = new GameObject[3];
        var tabIconImages = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            var (btn, highlight, iconImg) = CreateTabButton(tabBar.transform, tabNames[i], tabIcons[i]);
            tabButtons[i] = btn;
            tabHighlights[i] = highlight;
            tabIconImages[i] = iconImg;
        }

        // ── 3. 탭별 스크롤 콘텐츠 ─────────────────────────────────────────────
        var tabContents = new GameObject[3];

        // 그래픽
        var (graphicsRoot, graphicsContent) = CreateScrollTab(settingsBG, "GraphicsTab");
        CreateSectionHeader(graphicsContent, "성능 및 화면");
        TMP_Dropdown qualityDropdown        = CreateDropdownRow(graphicsContent, "화면 품질");
        var (fullscreenOnBg, fullscreenOffBg, fullscreenOnLabel, fullscreenOffLabel) = CreateSegmentedRow(graphicsContent, "표시 모드", "전체 화면", "창 모드",
            settingsMgr.SetFullscreenOn, settingsMgr.SetFullscreenOff);
        TMP_Dropdown resolutionDropdown      = CreateDropdownRow(graphicsContent, "해상도");
        TMP_Dropdown shadowQualityDropdown   = CreateDropdownRow(graphicsContent, "그림자 품질");
        TMP_Dropdown textureQualityDropdown  = CreateDropdownRow(graphicsContent, "텍스처 품질");
        tabContents[0] = graphicsRoot;

        // 오디오
        var (audioRoot, audioContent) = CreateScrollTab(settingsBG, "AudioTab");
        CreateSectionHeader(audioContent, "오디오 설정");
        // 오디오 3종은 아이콘을 눌러 음소거 토글이 가능해야 해서 동일한 볼륨 켬/끔 아이콘 쌍으로 통일
        // (BGM/SFX 전용 아이콘은 꺼짐 상태 그림이 없어 토글 시 보여줄 짝이 없었음).
        var volumeOnIcon  = LoadKitIcon("T_icon_volume_on.png");
        var volumeMuteIcon = LoadKitIcon("T_icon_volume_mute.png");
        Slider masterSlider = CreateSliderRow(audioContent, "마스터 볼륨", volumeOnIcon, volumeMuteIcon);
        Slider bgmSlider = CreateSliderRow(audioContent, "배경음(BGM)", volumeOnIcon, volumeMuteIcon);
        Slider sfxSlider = CreateSliderRow(audioContent, "효과음(SFX)", volumeOnIcon, volumeMuteIcon);
        tabContents[1] = audioRoot;

        // 조작
        var (controlsRoot, controlsContent) = CreateScrollTab(settingsBG, "ControlsTab");
        CreateSectionHeader(controlsContent, "조작 설정");
        Slider sensitivitySlider = CreateSliderRow(controlsContent, "마우스 감도", Load("SettingsPanel_Icon_Mouse.png"));
        // 볼륨류 슬라이더(0~1, 1=최대)와 달리 감도는 1.0이 "기존 카메라 SensitivityX/Y 그대로"인
        // 중간값이라 0~1로는 기본보다 빠르게 갈 방법이 없었다 — 0~2로 넓혀 1.0을 중간 기준으로 둠.
        sensitivitySlider.minValue = 0f;
        sensitivitySlider.maxValue = 2f;
        sensitivitySlider.value = 1f;

        // 감도는 볼륨처럼 "%"가 아니라 "배율" 개념이라 100/150/200보다 1.00x/1.50x/2.00x가 더 직관적.
        var sensValueInput = sensitivitySlider.transform.parent.GetComponentInChildren<SliderValueInput>(true);
        if (sensValueInput != null)
        {
            sensValueInput.scale = 1f;
            sensValueInput.decimals = 2;
            sensValueInput.suffix = "x";
            if (sensValueInput.input != null)
            {
                // DecimalNumber 콘텐츠 타입은 'x' 문자를 입력 중 걸러내버려서 Standard로 둔다 —
                // 파싱(SliderValueInput.OnSubmit)이 숫자만 직접 떼어내 처리하므로 문제 없음.
                sensValueInput.input.contentType = TMP_InputField.ContentType.Standard;
                sensValueInput.input.characterLimit = 6;
                sensValueInput.input.text = "1.00x";
            }
        }
        var rebindSlots = new List<GlobalSettingsManager.RebindSlot>
        {
            CreateRebindRow(controlsContent, "기본 공격", "Attack"),
            CreateRebindRow(controlsContent, "대시",     "Dash"),
            CreateRebindRow(controlsContent, "점프",     "Jump"),
            CreateRebindRow(controlsContent, "스킬 1",   "Skill1"),
            CreateRebindRow(controlsContent, "스킬 2",   "Skill2"),
            CreateRebindRow(controlsContent, "스킬 3",   "Skill3"),
            CreateRebindRow(controlsContent, "상호작용", "Interact"),
            CreateRebindRow(controlsContent, "즉시완료", "Instant"),
            CreateRebindRow(controlsContent, "퀵슬롯",   "QuickSlot"),
            CreateRebindRow(controlsContent, "인벤토리", "Inventory"),
            CreateRebindRow(controlsContent, "스탯창",   "Stat"),
            CreateRebindRow(controlsContent, "도감",     "Codex"),
        };
        tabContents[2] = controlsRoot;

        // 첫 탭만 보이게
        graphicsRoot.SetActive(true);
        audioRoot.SetActive(false);
        controlsRoot.SetActive(false);

        // ── 4. 닫기 버튼 (우상단) ────────────────────────────────────────────
        var closeBtn = CreateCloseButton(settingsBG, settingsMgr);

        // ── 5. 하단 고정 풋터 — 안내 문구(좌) + 설정 초기화/설정 적용(우) — 스크롤 영향 없음 ──
        CreateFooter(settingsBG, settingsMgr);

        // ── 5b. 키 리바인딩 입력 모달 (항상 최상단, 기본 비활성) ───────────────
        var (rebindModal, rebindActionLabel, rebindKeyDisplay) = CreateRebindModal(settingsBG);

        // ── 5c. 적용 안 한 변경사항 경고 팝업 (항상 최상단, 기본 비활성) ─────────
        var applyWarningModal = CreateApplyWarningModal(settingsBG, settingsMgr);

        // ── 6. GlobalSettingsManager 필드 연결 ───────────────────────────────
        Undo.RecordObject(settingsMgr, "Wire GlobalSettingsManager Fields");
        settingsMgr.resolutionDropdown      = resolutionDropdown;
        settingsMgr.fullscreenOnBg          = fullscreenOnBg;
        settingsMgr.fullscreenOffBg         = fullscreenOffBg;
        settingsMgr.fullscreenOnLabel       = fullscreenOnLabel;
        settingsMgr.fullscreenOffLabel      = fullscreenOffLabel;
        settingsMgr.qualityDropdown         = qualityDropdown;
        settingsMgr.shadowQualityDropdown   = shadowQualityDropdown;
        settingsMgr.textureQualityDropdown  = textureQualityDropdown;
        settingsMgr.masterSlider            = masterSlider;
        settingsMgr.bgmSlider               = bgmSlider;
        settingsMgr.sfxSlider               = sfxSlider;
        settingsMgr.sensitivitySlider       = sensitivitySlider;
        settingsMgr.tabButtons              = tabButtons;
        settingsMgr.tabContents             = tabContents;
        settingsMgr.tabHighlights           = tabHighlights;
        settingsMgr.tabIconImages           = tabIconImages;
        settingsMgr.rebindSlots             = rebindSlots;
        settingsMgr.rebindModal             = rebindModal;
        settingsMgr.rebindModalActionLabel  = rebindActionLabel;
        settingsMgr.rebindModalKeyDisplay   = rebindKeyDisplay;
        settingsMgr.applyWarningModal       = applyWarningModal;
        EditorUtility.SetDirty(settingsMgr);

        // 새로 만든 Image/Text들의 CanvasRenderer가 색상을 한 번도 못 반영한 채로 비활성화되면
        // (특히 Play 진입으로 도메인 리로드가 끼면) 생성 시점 기본값(흰색 불투명)으로 굳어버린다 —
        // 체인을 다시 끄기 전에 강제로 한 번 갱신시켜서 실제 지정한 색이 반영되게 한다.
        Canvas.ForceUpdateCanvases();

        // 임시로 활성화했던 체인 복원 (자식 → 부모 순서로 처리했으니 그대로 되돌림)
        foreach (var (go, wasActive) in activeChain)
            if (go.activeSelf != wasActive) go.SetActive(wasActive);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(settingsBG.gameObject.scene);

        Debug.Log("[SettingsRebuilder] 설정창 재구성 완료 — 그래픽/오디오/조작 탭 3개, 컨트롤 " +
                  "필드 GlobalSettingsManager에 연결됨. 인스펙터에서 확인하세요.");
    }

    [MenuItem(MenuPath, true)]
    static bool Validate() => !Application.isPlaying;

    // ── 공통 헬퍼 ────────────────────────────────────────────────────────────

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateUIElementViaMenu(string menuPath, Transform parent)
    {
        var prevSelection = Selection.activeGameObject;
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem(menuPath);
        GameObject created = Selection.activeGameObject;
        // ExecuteMenuItem이 Selection을 무시하고 씬의 다른 Canvas 밑에 생성하는 경우가 있어
        // 항상 의도한 parent로 강제 재배치한다.
        if (created != null && parent != null && created.transform.parent != parent)
            created.transform.SetParent(parent, false);
        Selection.activeGameObject = prevSelection;
        return created;
    }

    // 엔드필드 레퍼런스처럼 텍스트 없이 아이콘만 + 선택된 탭은 아이콘 뒤에 노란 둥근 사각 하이라이트.
    private static (Button, GameObject highlight, Image iconImg) CreateTabButton(Transform parent, string label, Sprite icon)
    {
        var btnGO = CreateUIObject("Btn_Tab_" + label, parent);
        Undo.RegisterCreatedObjectUndo(btnGO, "Create Tab Button");
        var le = btnGO.AddComponent<LayoutElement>();
        le.preferredWidth  = 72f;
        le.preferredHeight = 72f;

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f); // 클릭 히트박스만 — 비주얼은 아래 Highlight가 담당
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // 선택 강조 — 아이콘 뒤에 깔리는 둥근 사각 하이라이트 (선택 안 됐을 때는 비활성)
        var highlight = new GameObject("Highlight", typeof(RectTransform));
        highlight.transform.SetParent(btnGO.transform, false);
        var hImg = highlight.AddComponent<Image>();
        hImg.sprite = RoundedPillSprite();
        hImg.type = Image.Type.Sliced;
        hImg.color = AccentColor;
        var hRect = highlight.GetComponent<RectTransform>();
        hRect.anchorMin = Vector2.zero;
        hRect.anchorMax = Vector2.one;
        hRect.offsetMin = Vector2.zero;
        hRect.offsetMax = Vector2.zero;
        highlight.SetActive(false);

        Image iconImg = null;
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(btnGO.transform, false);
            iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white; // 미선택 기본값 — 선택 시 GlobalSettingsManager.ShowTab이 어둡게 바꿈
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot     = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(36f, 36f);
        }

        return (btn, highlight, iconImg);
    }

    private static (GameObject root, Transform content) CreateScrollTab(Transform parent, string name)
    {
        var scrollGO = CreateUIElementViaMenu("GameObject/UI/Scroll View", parent);
        scrollGO.name = name;
        var rect = scrollGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(90f, 110f);   // 좌, 하 여백
        rect.offsetMax = new Vector2(-90f, -140f); // 우, 상(탭바 아래부터) 여백 — 탭바가 타이틀과 같은 줄로 올라가서 줄임

        var scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // 패널 전체에 깔린 BlurredImage(블러+올리브 틴트)가 그대로 비치도록 투명하게 — raycast 차단용으로만 둠.
        // (기존엔 PanelBg로 완전 불투명하게 채워서 탭 콘텐츠 영역에서는 블러가 전혀 안 보였음)
        var rootImg = scrollGO.GetComponent<Image>();
        if (rootImg == null) rootImg = scrollGO.AddComponent<Image>();
        rootImg.sprite = null;
        rootImg.color = new Color(0f, 0f, 0f, 0f);

        var viewport = scrollGO.transform.Find("Viewport");
        var content  = viewport != null ? viewport.Find("Content") : null;
        if (content == null)
        {
            Debug.LogWarning("[SettingsRebuilder] ScrollView 기본 구조를 찾지 못함: " + name);
            return (scrollGO, scrollGO.transform);
        }

        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return (scrollGO, content);
    }

    private static GameObject CreateRow(Transform content, string label, out Transform controlSlot)
    {
        var row = CreateUIObject(label + "_Row", content);

        // 엔드필드 레퍼런스의 "전체 너비 옅은 행 배경" — 모든 줄(드롭다운/슬라이더/세그먼트/키 리바인딩)에 동일하게 적용.
        var rowBg = row.AddComponent<Image>();
        rowBg.sprite = RoundedPillSprite();
        rowBg.type = Image.Type.Sliced;
        rowBg.color = new Color(1f, 1f, 1f, 0.005f);
        rowBg.raycastTarget = false;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(28, 28, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 24f;
        hlg.childControlWidth  = true;  // false였던 게 버그 — LayoutElement 너비가 무시되어 컨트롤이 찌그러짐
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 64f;
        rowLE.preferredHeight = 64f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26f;
        tmp.color = new Color(0.76f, 0.78f, 0.81f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(tmp);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 380f;
        labelLE.minWidth = 380f;
        labelLE.flexibleWidth = 0f;

        var slot = new GameObject("Control", typeof(RectTransform));
        slot.transform.SetParent(row.transform, false);
        var slotLE = slot.AddComponent<LayoutElement>();
        slotLE.minWidth = 200f;
        slotLE.flexibleWidth = 1f;

        controlSlot = slot.transform;
        return row;
    }

    // 컨트롤을 슬롯 "오른쪽 끝"에 고정폭으로 붙인다 — 라벨과 컨트롤 사이는 비워둠(엔드필드 스타일)
    private static void FillSlot(GameObject element, Transform slot, float width, float height)
    {
        element.transform.SetParent(slot, false);
        var rect = element.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot     = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    private const float ControlWidth = 520f; // 드롭다운/세그먼트 컨트롤 표준 폭
    private const float ControlHeight = 64f; // 드롭다운/세그먼트 컨트롤 표준 높이

    // 엔드필드 레퍼런스 팔레트 — 다크 올리브 + 옐로우 액센트
    // (이전 값은 B>G>R인 네이비톤이라 올리브가 아니라 청록빛 다크였음 — R>=G>B로 수정)
    private static readonly Color ControlBg     = new Color(0.165f, 0.158f, 0.125f, 0.95f);
    private static readonly Color AccentColor  = new Color(0.76f, 0.70f, 0.20f, 1f);
    private static readonly Color TextPrimary   = new Color(0.78f, 0.79f, 0.81f, 1f);
    private static readonly Color TextMuted     = new Color(0.55f, 0.57f, 0.60f, 1f);
    private static readonly Color SliderLineGray = new Color(0.29f, 0.30f, 0.32f, 1f); // 얇은 트랙 라인 색

    private static TMP_Dropdown CreateDropdownRow(Transform content, string label)
    {
        CreateRow(content, label, out var slot);
        var ddGO = CreateUIElementViaMenu("GameObject/UI/Dropdown - TextMeshPro", slot);
        FillSlot(ddGO, slot, ControlWidth, ControlHeight);
        var dd = ddGO.GetComponent<TMP_Dropdown>();
        StyleDropdown(dd);
        return dd;
    }

    // 드롭다운 — 팰월드 레퍼런스 라이트+라운드 스타일
    // 닫힌 박스: 라운드 흰 박스 / 열린 목록: flat 직사각형(닫힌 박스 위에 겹쳐 사각형처럼 보임)
    private static void StyleDropdown(TMP_Dropdown dd)
    {
        // 닫힌 박스: 라이트 배경 + 라운드
        var rootImg = dd.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = RoundedPillSprite();
            rootImg.type   = Image.Type.Sliced;
            rootImg.color  = PillBg;
        }

        var label = dd.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null) { label.color = PillText; label.fontSize = 24f; label.fontStyle = FontStyles.Normal; label.alignment = TextAlignmentOptions.MidlineLeft; ApplyFont(label); }
        var labelRect = label?.GetComponent<RectTransform>();
        if (labelRect != null)
        {
            labelRect.offsetMin = new Vector2(20f, 0f);
            labelRect.offsetMax = new Vector2(-52f, 0f);
        }

        // 화살표: 오른쪽에서 28px, 어두운색
        var arrowRt = dd.transform.Find("Arrow")?.GetComponent<RectTransform>();
        if (arrowRt != null)
        {
            arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(1f, 0.5f);
            arrowRt.pivot     = new Vector2(0.5f, 0.5f);
            arrowRt.sizeDelta = new Vector2(20f, 20f);
            arrowRt.anchoredPosition = new Vector2(-28f, 0f);
        }
        var arrow = dd.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow != null) arrow.color = PillText;

        var template = dd.transform.Find("Template");
        if (template == null) return;

        // 펼침 목록: 위쪽 직사각 + 아래쪽만 둥근 스프라이트 — TMP_Dropdown이 런타임에 Template 위치를 덮어쓰므로
        // anchoredPosition 오프셋 없이 기본 위치(버튼 바로 아래)를 그대로 사용
        // 펼침 목록 외곽: 흰색 배경 + 아래쪽 둥근 스프라이트
        var templateImg = template.GetComponent<Image>();
        if (templateImg != null)
        {
            templateImg.sprite = RoundedBottomSprite();
            templateImg.type   = Image.Type.Sliced;
            templateImg.color  = DdClosedBg; // 흰색
        }

        // Viewport: Mask용 불투명 유지 + showMaskGraphic=false
        var viewportGO  = template.Find("Viewport");
        var viewportImg = viewportGO?.GetComponent<Image>();
        if (viewportImg != null)
        {
            viewportImg.sprite = null;
            viewportImg.type   = Image.Type.Simple;
            viewportImg.color  = Color.white;
        }
        var viewportMask = viewportGO?.GetComponent<Mask>();
        if (viewportMask != null) viewportMask.showMaskGraphic = false;

        var itemBg = template.Find("Viewport/Content/Item/Item Background")?.GetComponent<Image>();
        if (itemBg != null)
        {
            itemBg.sprite = null;
            itemBg.type   = Image.Type.Simple;
            itemBg.color  = DdClosedBg; // 흰색
        }

        // 항목 라벨: 초기값 어두운색 (선택 시 tint가 밝게 전환)
        var itemLabel = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<TextMeshProUGUI>();
        if (itemLabel != null)
        {
            itemLabel.color     = DdClosedText;
            itemLabel.fontSize  = 24f;
            itemLabel.fontStyle = FontStyles.Normal;
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            ApplyFont(itemLabel);
        }
        var itemLabelRect = itemLabel?.GetComponent<RectTransform>();
        if (itemLabelRect != null)
        {
            itemLabelRect.offsetMin = new Vector2(20f, 0f);
            itemLabelRect.offsetMax = new Vector2(-52f, 0f);
        }

        // 선택 항목 강조 — 배경+텍스트 동시 전환 (중복 방지)
        var itemGO     = template.Find("Viewport/Content/Item")?.gameObject;
        var itemToggle = itemGO?.GetComponent<Toggle>();
        if (itemBg != null && itemToggle != null)
        {
            var existTint = itemGO.GetComponent<DropdownItemSelectedTint>();
            if (existTint != null) UnityEngine.Object.DestroyImmediate(existTint);
            var tint = itemGO.AddComponent<DropdownItemSelectedTint>();
            tint.background         = itemBg;
            tint.toggle             = itemToggle;
            tint.label              = itemLabel;
            tint.selectedColor      = DdSelected;
            tint.unselectedColor    = DdClosedBg;
            tint.selectedTextColor  = DdClosedText;  // 선택 시도 어두운 텍스트 (연한 배경에 맞게)
            tint.unselectedTextColor = DdClosedText; // 미선택 시 어두운 텍스트
        }

        // 체크마크: 오른쪽으로 이동, 밝은색(선택 항목 어두운 배경 위에 보여야 함)
        var checkmark = template.Find("Viewport/Content/Item/Item Checkmark") as RectTransform;
        if (checkmark != null)
        {
            checkmark.anchorMin = checkmark.anchorMax = new Vector2(1f, 0.5f);
            checkmark.pivot     = new Vector2(0.5f, 0.5f);
            checkmark.sizeDelta = new Vector2(20f, 20f);
            checkmark.anchoredPosition = new Vector2(-26f, 0f);
        }
        var checkmarkImg = checkmark?.GetComponent<Image>();
        if (checkmarkImg != null) checkmarkImg.color = DdClosedText; // 어두운색 (연한 선택 배경에 맞게)

        // 항목 높이: 닫힌 박스(64)와 동일하게
        var item = template.Find("Viewport/Content/Item") as RectTransform;
        if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, 64f);

        // 아이템 구분선 — 흰 배경에서 모든 아이템 경계를 동일하게 보이게 함
        // (선택된 아이템만 어두운 배경이라 경계가 뚜렷해 높이가 달라 보이는 착시 방지)
        var itemParentGO = template.Find("Viewport/Content/Item")?.gameObject;
        if (itemParentGO != null)
        {
            var existSep = itemParentGO.transform.Find("Separator");
            if (existSep != null) UnityEngine.Object.DestroyImmediate(existSep.gameObject);
            var sepGO = new GameObject("Separator", typeof(RectTransform));
            sepGO.transform.SetParent(itemParentGO.transform, false);
            var sepRt = sepGO.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 0f);
            sepRt.anchorMax = new Vector2(1f, 0f);
            sepRt.pivot     = new Vector2(0.5f, 0f);
            sepRt.offsetMin = new Vector2(16f, 0f);
            sepRt.offsetMax = new Vector2(-16f, 1f); // 1px 높이
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.sprite = null;
            sepImg.color  = new Color(0.72f, 0.73f, 0.75f, 1f); // 연한 구분선
            sepImg.raycastTarget = false;
        }

        // 스크롤바: ScrollRect 참조 해제 + GameObject 비활성 → 마우스휠/드래그 스크롤은 유지
        var scrollRect = template.GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalScrollbar = null;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }
        var scrollbarGO = template.Find("Scrollbar")?.gameObject;
        if (scrollbarGO != null) scrollbarGO.SetActive(false);
        // Viewport: 전체 너비 + 상단 8px 여백 (첫 아이템이 버튼에 바짝 붙어 보이지 않게)
        var viewportRt = viewportGO?.GetComponent<RectTransform>();
        if (viewportRt != null)
        {
            viewportRt.offsetMin = new Vector2(0f, 8f);
            viewportRt.offsetMax = new Vector2(0f, -24f);
        }

        // DropdownStyleSwitcher: 열릴 때 버튼 → RoundedTopSprite(위만 둥근)
        //                        닫힐 때 버튼 → RoundedPillSprite(전체 둥근)
        // Template = RoundedBottomSprite(아래만 둥근) → 둘이 맞닿으면 하나의 완전한 둥근 사각형
        var existFixer = dd.GetComponent<DropdownOverlapFixer>();
        if (existFixer != null) UnityEngine.Object.DestroyImmediate(existFixer);
        var existSwitcher = dd.GetComponent<DropdownStyleSwitcher>();
        if (existSwitcher != null) UnityEngine.Object.DestroyImmediate(existSwitcher);
        var switcher = dd.gameObject.AddComponent<DropdownStyleSwitcher>();
        switcher.closedSprite    = RoundedPillSprite();
        switcher.openSprite      = RoundedTopSprite();
        switcher.closedColor     = DdClosedBg;
        switcher.openColor       = DdOpenBg;
        switcher.closedTextColor = DdClosedText;
        switcher.openTextColor   = DdOpenText;
    }

    // 엔드필드 스타일 섹션 헤더: ■ 노란 막대 + 굵은 노란 글씨 + 우측으로 뻗는 얇은 선
    private static void CreateSectionHeader(Transform content, string text)
    {
        var header = CreateUIObject(text + "_Header", content);
        var hlg = header.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 12f;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false; // true(기본값)면 flexibleWidth=0인 자식도 강제로 늘어남
        hlg.childForceExpandHeight = false;
        var le = header.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.transform.SetParent(header.transform, false);
        var barImg = bar.AddComponent<Image>();
        barImg.color = AccentColor;
        var barLE = bar.AddComponent<LayoutElement>();
        barLE.preferredWidth = 6f; barLE.minWidth = 6f; barLE.flexibleWidth = 0f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(header.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = AccentColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(tmp);
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 220f; labelLE.minWidth = 220f; labelLE.flexibleWidth = 0f;

        var line = new GameObject("Line", typeof(RectTransform));
        line.transform.SetParent(header.transform, false);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.15f);
        var lineLE = line.AddComponent<LayoutElement>();
        lineLE.flexibleWidth = 1f; lineLE.minHeight = 2f; lineLE.preferredHeight = 2f;
    }

    // 표시 모드 같은 2지선다 행: [전체 화면 | 창 모드] 버튼 한 쌍, 선택된 쪽만 노란색
    private static (Image onBg, Image offBg, TMP_Text onLabel, TMP_Text offLabel) CreateSegmentedRow(Transform content, string label,
        string onText, string offText, UnityEngine.Events.UnityAction onClickOn, UnityEngine.Events.UnityAction onClickOff)
    {
        CreateRow(content, label, out var slot);

        var segGO = new GameObject("Segments", typeof(RectTransform));
        segGO.transform.SetParent(slot, false);
        var segRect = segGO.GetComponent<RectTransform>();
        segRect.anchorMin = new Vector2(1f, 0.5f);
        segRect.anchorMax = new Vector2(1f, 0.5f);
        segRect.pivot     = new Vector2(1f, 0.5f);
        segRect.anchoredPosition = Vector2.zero;
        segRect.sizeDelta = new Vector2(ControlWidth, ControlHeight);
        var segHlg = segGO.AddComponent<HorizontalLayoutGroup>();
        segHlg.childControlWidth = true;
        segHlg.childControlHeight = true;
        segHlg.childForceExpandWidth  = false;
        segHlg.childForceExpandHeight = false;
        segHlg.spacing = 4f;

        var onBtnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", segGO.transform);
        onBtnGO.name = "Btn_On";
        var onLE = onBtnGO.AddComponent<LayoutElement>();
        onLE.flexibleWidth = 1f; onLE.preferredHeight = ControlHeight; onLE.minHeight = ControlHeight;
        var onTmp = onBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (onTmp != null) { onTmp.text = onText; onTmp.fontSize = 24f; onTmp.fontStyle = FontStyles.Bold; ApplyFont(onTmp); }
        var onImg = onBtnGO.GetComponent<Image>();
        if (onImg != null)
        {
            onImg.color = ControlBg;
            onImg.sprite = RoundedPillSprite();
            onImg.type = Image.Type.Sliced;
        }
        var onBtn = onBtnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(onBtn.onClick, onClickOn);

        var offBtnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", segGO.transform);
        offBtnGO.name = "Btn_Off";
        var offLE = offBtnGO.AddComponent<LayoutElement>();
        offLE.flexibleWidth = 1f; offLE.preferredHeight = ControlHeight; offLE.minHeight = ControlHeight;
        var offTmp = offBtnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (offTmp != null) { offTmp.text = offText; offTmp.fontSize = 24f; offTmp.fontStyle = FontStyles.Bold; ApplyFont(offTmp); }
        var offImg = offBtnGO.GetComponent<Image>();
        if (offImg != null)
        {
            offImg.color = ControlBg;
            offImg.sprite = RoundedPillSprite();
            offImg.type = Image.Type.Sliced;
        }
        var offBtn = offBtnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(offBtn.onClick, onClickOff);

        return (onImg, offImg, onTmp, offTmp);
    }

    // 하단 고정 풋터: 좌측 안내 문구 + 우측 설정 초기화/설정 적용 알약 버튼 — 탭 스크롤과 무관하게 항상 보임
    private static void CreateFooter(Transform parent, GlobalSettingsManager mgr)
    {
        const float btnWidth = 260f, btnHeight = 64f, spacing = 20f, y = 50f, edgePad = 40f;
        const float mainMenuBtnWidth = 320f; // "메인 메뉴로 돌아가기"가 다른 버튼 라벨보다 길어서 더 넓게

        // 좌측 안내 문구 (레퍼런스의 "변경하고자 하는 키를 눌러서 선택해 주세요." 위치)
        var hintGO = new GameObject("Hint", typeof(RectTransform));
        hintGO.transform.SetParent(parent, false);
        var hintRt = hintGO.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot     = new Vector2(0f, 0f);
        hintRt.anchoredPosition = new Vector2(edgePad, y);
        hintRt.sizeDelta = new Vector2(700f, btnHeight);
        var hintTmp = hintGO.AddComponent<TextMeshProUGUI>();
        hintTmp.text = "변경하고자 하는 키를 눌러서 선택해 주세요.";
        hintTmp.fontSize = 20f;
        hintTmp.color = TextMuted;
        hintTmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(hintTmp);

        // 우측 알약 버튼 3개 (안쪽부터 적용 → 초기화 → 메인 메뉴로 돌아가기)
        CreateFooterButton(parent, "Btn_Apply", "설정 적용", LoadKitIcon("T_icon_check.png"),
            -edgePad, y, btnWidth, btnHeight, mgr.ApplySettings);
        // ResetAllToDefault()는 오디오/조작/그래픽/키바인딩을 전부 기본값으로 되돌린다.
        CreateFooterButton(parent, "Btn_Reset", "설정 초기화", LoadKitIcon("T_icon_refresh.png"),
            -edgePad - btnWidth - spacing, y, btnWidth, btnHeight, mgr.ResetAllToDefault);
        CreateFooterButton(parent, "Btn_MainMenu", "메인 메뉴로 돌아가기", LoadKitIcon("T_icon_quit.png"),
            -edgePad - (btnWidth + spacing) * 2f, y, mainMenuBtnWidth, btnHeight, mgr.QuitToMainMenu);
    }

    // 드롭다운 리스트(열린 상태): 완전 불투명 — alpha < 1이면 뒤 UI가 비쳐 보임
    private static readonly Color PillBg        = new Color(0.32f, 0.33f, 0.35f, 1.0f); // 불투명 회색
    private static readonly Color PillText      = new Color(0.76f, 0.78f, 0.80f, 1f);   // 밝은 텍스트
    private static readonly Color PillCircleBg  = new Color(0.18f, 0.19f, 0.20f, 1.0f); // 아이콘 원형 배경
    private static readonly Color DdSelected    = new Color(0.78f, 0.79f, 0.81f, 1f);   // 선택 항목 강조 (연한 회색)

    // 드롭다운 버튼 상태별 색 — 닫힌: 흰색 / 열린: 리스트와 동일한 회색
    private static readonly Color DdClosedBg   = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly Color DdClosedText = new Color(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Color DdOpenBg     = new Color(0.32f, 0.33f, 0.35f, 1f);
    private static readonly Color DdOpenText   = new Color(0.76f, 0.78f, 0.80f, 1f);

    // 풋터 버튼: 어두운 설정창 안에서 CTA로 눈에 띄도록 밝은 알약형 유지
    private static readonly Color FooterBtnBg   = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly Color FooterBtnText = new Color(0.12f, 0.12f, 0.14f, 1f);

    // 구버전 호환용 별칭 (코드 내 다른 참조 방지)
    private static readonly Color DdText       = new Color(0.87f, 0.90f, 0.95f, 1f);

    // 알약형 버튼: 가운데 텍스트 + 오른쪽 안쪽에 들어간 원형 아이콘(rightIcon으로 버튼마다
    // 구분: 새로고침/체크 등). 좌측 아이콘은 제거 — 흰 버튼 밖으로 삐져나오지 않게 전부 안쪽에 배치.
    private static void CreateFooterButton(Transform parent, string name, string text, Sprite rightIcon,
        float x, float y, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = CreateUIObject(name, parent);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, y);

        var pillImg = btnGO.AddComponent<Image>();
        pillImg.sprite = RoundedPillSprite();
        pillImg.type = Image.Type.Sliced;
        pillImg.color = FooterBtnBg;

        var btn = btnGO.AddComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, onClick);

        float circleSize = height * 0.7f;
        const float circlePad = 8f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = FooterBtnText;
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(tmp);
        var labelRt = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = new Vector2(-(circleSize + circlePad * 2f), 0f);

        // 오른쪽 원형 아이콘 — 버튼 안쪽에 완전히 들어가도록 중심을 안으로 당김(삐져나오지 않음)
        CreateIconCircle(btnGO.transform, rightIcon, new Vector2(1f, 0.5f),
            new Vector2(-(circleSize * 0.5f + circlePad), 0f), circleSize);
    }

    // ✓/↻ 같은 유니코드 글리프는 프로젝트의 모든 TMP 폰트 에셋이 Static 아틀라스라
    // 글자가 없으면 그냥 깨져버린다(□) — 폰트로는 못 고치는 문제라 스프라이트로 대체.
    private static void CreateIconCircle(Transform parent, Sprite icon, Vector2 anchor, Vector2 anchoredPos, float size)
    {
        var circleGO = new GameObject("Circle", typeof(RectTransform));
        circleGO.transform.SetParent(parent, false);
        var rt = circleGO.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var img = circleGO.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = PillCircleBg;

        var glyphGO = new GameObject("Glyph", typeof(RectTransform));
        glyphGO.transform.SetParent(circleGO.transform, false);
        var grt = glyphGO.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = new Vector2(7f, 7f);
        grt.offsetMax = new Vector2(-7f, -7f);
        var gimg = glyphGO.AddComponent<Image>();
        gimg.sprite = icon;
        gimg.preserveAspect = true;
        gimg.color = TextPrimary;
    }


    private const float SliderWidth = 480f; // 슬라이더 트랙 표준 폭(아이콘 제외)

    private const float ValueLabelWidth = 46f; // 클릭해서 직접 입력하는 칸이라 살짝 넓힘

    // OS 볼륨 슬라이더 스타일: 얇은 회색 라인 트랙 + 흰 캡슐형 핸들 + 우측 숫자 값(0-10)
    private static Slider CreateSliderRow(Transform content, string label, Sprite icon, Sprite mutedIcon = null)
    {
        CreateRow(content, label, out var slot);

        // 아이콘 + 슬라이더 + 숫자 라벨을 한 묶음으로 묶어 슬롯 "오른쪽 끝"에 고정폭으로 붙인다
        var groupGO = new GameObject("SliderGroup", typeof(RectTransform));
        groupGO.transform.SetParent(slot, false);
        var groupRect = groupGO.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(1f, 0.5f);
        groupRect.anchorMax = new Vector2(1f, 0.5f);
        groupRect.pivot     = new Vector2(1f, 0.5f);
        groupRect.anchoredPosition = Vector2.zero;
        float groupWidth = SliderWidth + ValueLabelWidth + 12f + (icon != null ? 60f : 0f);
        groupRect.sizeDelta = new Vector2(groupWidth, ControlHeight);
        var groupHlg = groupGO.AddComponent<HorizontalLayoutGroup>();
        groupHlg.childAlignment = TextAnchor.MiddleLeft;
        groupHlg.spacing = 12f;
        groupHlg.childControlWidth  = true;
        groupHlg.childControlHeight = true;
        groupHlg.childForceExpandWidth  = false;
        groupHlg.childForceExpandHeight = false;

        Image muteIconImg = null;
        if (icon != null)
        {
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(groupGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 56f; iconLE.minWidth = 56f; iconLE.flexibleWidth = 0f;
            if (mutedIcon != null)
            {
                // 아이콘 클릭 = 음소거 토글 — Button만 추가해두고, 실제 토글 로직(MuteToggleButton)은
                // 슬라이더가 만들어진 뒤에 연결한다 (아래 muteIconImg 참조 유지).
                iconGO.AddComponent<Button>();
                muteIconImg = iconImg;
            }
            iconLE.preferredHeight = 56f; iconLE.minHeight = 56f;
        }

        var sliderGO = CreateUIElementViaMenu("GameObject/UI/Slider", groupGO.transform);
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.flexibleWidth = 1f;
        sliderLE.preferredHeight = 40f; sliderLE.minHeight = 40f;
        var rect = sliderGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        // 트랙을 얇은 3px 선으로 줄이면서 Background/Fill의 레이캐스트 영역도 그 3px로 줄어들어,
        // 슬라이더 칸 안(40px 높이)이라도 그 가느다란 선에 정확히 클릭하지 않으면 아무 반응이 없었다.
        // Slider는 클릭된 화면 좌표가 핸들 영역 안인지를 기하학적으로 판단해 드래그/점프를 결정하므로,
        // 어떤 자식 그래픽이 레이를 받았는지는 무관 — 슬라이더 칸 전체를 덮는 투명 레이캐스트 캐처를
        // 하나 깔아두면 트랙 어디를 클릭해도 그 위치로 값이 바로 점프한다.
        var clickAreaGO = new GameObject("ClickArea", typeof(RectTransform));
        clickAreaGO.transform.SetParent(sliderGO.transform, false);
        clickAreaGO.transform.SetAsFirstSibling();
        var clickAreaRt = clickAreaGO.GetComponent<RectTransform>();
        clickAreaRt.anchorMin = Vector2.zero;
        clickAreaRt.anchorMax = Vector2.one;
        clickAreaRt.offsetMin = Vector2.zero;
        clickAreaRt.offsetMax = Vector2.zero;
        var clickAreaImg = clickAreaGO.AddComponent<Image>();
        clickAreaImg.sprite = null;
        clickAreaImg.color = new Color(0f, 0f, 0f, 0f);

        // 트랙: 얇은 회색 라인 (sprite=null, 단색만 사용)
        const float trackThickness = 3f;
        var bgRt = sliderGO.transform.Find("Background")?.GetComponent<RectTransform>();
        if (bgRt != null)
        {
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, trackThickness);
        }
        var bgImg = bgRt != null ? bgRt.GetComponent<Image>() : null;
        if (bgImg != null) { bgImg.sprite = null; bgImg.color = SliderLineGray; }

        // 필 영역: 트랙과 같은 색으로 채워서 "끊긴 라인" 없이 하나의 얇은 선처럼 보이게 함
        var fillAreaRt = sliderGO.transform.Find("Fill Area")?.GetComponent<RectTransform>();
        if (fillAreaRt != null)
        {
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.sizeDelta = new Vector2(fillAreaRt.sizeDelta.x, trackThickness);
        }
        var fillImg = sliderGO.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fillImg != null) { fillImg.sprite = null; fillImg.color = SliderLineGray; }

        // 핸들: Slider가 매 프레임 Handle의 anchorMin/Max.y를 (0,1)로 스트레치해버려서
        // sizeDelta.y는 "절대 높이"가 아니라 "풀스트레치에 더해지는 여분"이 된다 — 그래서
        // (16,9)를 줘도 실제로는 슬라이드 영역 높이(40)+9 = 49px짜리 거대한 막대로 보임.
        // 해결: Handle 자체(드래그 히트박스)는 투명하게 두고, 그 안에 절대 크기(16x9)로
        // point-anchor된 자식 Visual 하나를 따로 둬서 실제 캡슐 모양만 그건 담당하게 분리.
        var handleRt = sliderGO.transform.Find("Handle Slide Area/Handle")?.GetComponent<RectTransform>();
        // 드래그 히트박스가 16px로 너무 좁아 잡기 힘들다는 피드백 — 보이는 캡슐(Visual, 16x9)은
        // 그대로 두고 클릭/드래그 가능한 히트박스 폭만 32px로 넓힘.
        if (handleRt != null) handleRt.sizeDelta = new Vector2(32f, 24f);
        var handleImg = handleRt != null ? handleRt.GetComponent<Image>() : null;
        if (handleImg != null) handleImg.color = new Color(0f, 0f, 0f, 0f);
        if (handleRt != null)
        {
            var visualGO = new GameObject("Visual", typeof(RectTransform));
            visualGO.transform.SetParent(handleRt, false);
            var visualRt = visualGO.GetComponent<RectTransform>();
            visualRt.anchorMin = new Vector2(0.5f, 0.5f);
            visualRt.anchorMax = new Vector2(0.5f, 0.5f);
            visualRt.pivot     = new Vector2(0.5f, 0.5f);
            visualRt.anchoredPosition = Vector2.zero;
            visualRt.sizeDelta = new Vector2(16f, 9f);
            var visualImg = visualGO.AddComponent<Image>();
            visualImg.sprite = RoundedPillSprite();
            visualImg.type = Image.Type.Sliced;
            visualImg.color = TextPrimary;
            visualImg.raycastTarget = false;
        }

        // 우측 숫자 값 입력칸 (0~100 스케일) — 평소엔 값만 보이고, 클릭하면 직접 숫자를 입력해 슬라이더를 바꿀 수 있음
        var valueGO = CreateUIElementViaMenu("GameObject/UI/Input Field - TextMeshPro", groupGO.transform);
        valueGO.name = "Value";
        var valueLE = valueGO.AddComponent<LayoutElement>();
        valueLE.preferredWidth = ValueLabelWidth; valueLE.minWidth = ValueLabelWidth; valueLE.flexibleWidth = 0f;
        valueLE.preferredHeight = 40f; valueLE.minHeight = 40f;

        var valueBgImg = valueGO.GetComponent<Image>();
        if (valueBgImg != null) { valueBgImg.sprite = null; valueBgImg.color = new Color(0f, 0f, 0f, 0f); } // 평소엔 박스가 안 보이게

        var valueInput = valueGO.GetComponent<TMP_InputField>();
        valueInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        valueInput.characterLimit = 3;
        valueInput.text = "100";

        var valueText = valueGO.transform.Find("Text Area/Text")?.GetComponent<TextMeshProUGUI>();
        if (valueText != null)
        {
            valueText.fontSize = 20f;
            valueText.color = TextMuted;
            valueText.alignment = TextAlignmentOptions.MidlineRight;
        }
        var valuePlaceholder = valueGO.transform.Find("Text Area/Placeholder")?.gameObject;
        if (valuePlaceholder != null) valuePlaceholder.SetActive(false); // 항상 값이 채워져 있어 플레이스홀더 불필요

        // 람다를 직접 AddListener하면 비영속 리스너라 씬을 새로 로드했을 때 사라짐 —
        // 런타임에 OnEnable에서 다시 구독/입력값 반영하는 SliderValueInput 컴포넌트로 대체.
        var valueInputComp = valueGO.AddComponent<SliderValueInput>();
        valueInputComp.slider = slider;
        valueInputComp.input = valueInput;
        valueInputComp.scale = 100f;

        if (muteIconImg != null)
        {
            var muteToggle = muteIconImg.gameObject.AddComponent<MuteToggleButton>();
            muteToggle.slider = slider;
            muteToggle.icon = muteIconImg;
            muteToggle.unmutedSprite = icon;
            muteToggle.mutedSprite = mutedIcon;
        }

        return slider;
    }

    // 키 바인딩 칩: 레퍼런스(단축키 화면)처럼 행 전체에 옅은 카드 배경(CreateRow에서 공통 적용) + 넓은 키 칩 + 얇은 중립색 외곽선
    private static GlobalSettingsManager.RebindSlot CreateRebindRow(Transform content, string label, string actionId)
    {
        var row = CreateRow(content, label, out var slot);

        var btnGO = CreateUIElementViaMenu("GameObject/UI/Button - TextMeshPro", slot);
        FillSlot(btnGO, slot, 200f, ControlHeight);

        var img = btnGO.GetComponent<Image>();
        if (img != null)
        {
            img.color = ControlBg;
            img.sprite = RoundedPillSprite();
            img.type = Image.Type.Sliced;
        }

        var outline = btnGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f); // 레퍼런스는 칩마다 노랑 대신 옅은 중립색 테두리
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = false;

        var tmp = btnGO.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = "Space";
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = TextPrimary;
        }

        return new GlobalSettingsManager.RebindSlot
        {
            actionId    = actionId,
            displayName = label,
            button      = btnGO.GetComponent<Button>(),
            keyLabel    = tmp
        };
    }

    // 기존 SettingsPanel_Button_Close_BG.png는 다른 톤(시안 네온 SF 패널)이라 지금의
    // 다크 네이비+화이트 톤 레이아웃과 안 맞았다 — 풋터 원형 아이콘과 같은 톤으로 통일.
    private static Button CreateCloseButton(Transform parent, GlobalSettingsManager mgr)
    {
        var btnGO = CreateUIObject("Btn_Close", parent);
        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-45f, -45f);
        rect.sizeDelta = new Vector2(56f, 56f);

        var img = btnGO.AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.color = ControlBg;

        var btn = btnGO.AddComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, mgr.CloseSettings);

        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(btnGO.transform, false);
        var iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(16f, 16f);
        iconRt.offsetMax = new Vector2(-16f, -16f);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = LoadKitIcon("T_icon_close.png");
        iconImg.preserveAspect = true;
        iconImg.color = TextPrimary;

        return btn;
    }

    // 키 리바인딩 중 표시되는 중앙 모달: 안내 문구 + 동작 이름 + 키 입력 박스 + ESC 취소 안내.
    // 기본 비활성, GlobalSettingsManager.BeginRebind/CancelRebind/CompleteRebind에서 토글.
    private static (GameObject modal, TMP_Text actionLabel, TMP_Text keyDisplay) CreateRebindModal(Transform parent)
    {
        var modal = CreateUIObject("RebindModal", parent);
        var modalRect = modal.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.offsetMin = Vector2.zero;
        modalRect.offsetMax = Vector2.zero;
        var backdrop = modal.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(modal.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot     = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(640f, 300f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        panelImg.color = PillBg;

        var title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(panel.transform, false);
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "변경하고자 하는 키를 입력해 주세요.";
        titleTmp.fontSize = 26f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = PillText;
        titleTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(titleTmp);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(0f, 40f);

        var actionLabelGO = new GameObject("ActionLabel", typeof(RectTransform));
        actionLabelGO.transform.SetParent(panel.transform, false);
        var actionTmp = actionLabelGO.AddComponent<TextMeshProUGUI>();
        actionTmp.text = "";
        actionTmp.fontSize = 20f;
        actionTmp.color = TextMuted;
        actionTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(actionTmp);
        var actionRect = actionLabelGO.GetComponent<RectTransform>();
        actionRect.anchorMin = new Vector2(0f, 1f);
        actionRect.anchorMax = new Vector2(1f, 1f);
        actionRect.pivot     = new Vector2(0.5f, 1f);
        actionRect.anchoredPosition = new Vector2(0f, -82f);
        actionRect.sizeDelta = new Vector2(0f, 30f);

        var keyBox = new GameObject("KeyBox", typeof(RectTransform));
        keyBox.transform.SetParent(panel.transform, false);
        var keyBoxRect = keyBox.GetComponent<RectTransform>();
        keyBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
        keyBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        keyBoxRect.pivot     = new Vector2(0.5f, 0.5f);
        keyBoxRect.anchoredPosition = new Vector2(0f, -8f);
        keyBoxRect.sizeDelta = new Vector2(280f, 64f);
        var keyBoxImg = keyBox.AddComponent<Image>();
        keyBoxImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        keyBoxImg.type = Image.Type.Sliced;
        keyBoxImg.color = SliderLineGray;

        var keyDisplayGO = new GameObject("KeyDisplay", typeof(RectTransform));
        keyDisplayGO.transform.SetParent(keyBox.transform, false);
        var keyTmp = keyDisplayGO.AddComponent<TextMeshProUGUI>();
        keyTmp.text = "";
        keyTmp.fontSize = 24f;
        keyTmp.fontStyle = FontStyles.Bold;
        keyTmp.color = PillText;
        keyTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(keyTmp);
        var keyDisplayRect = keyDisplayGO.GetComponent<RectTransform>();
        keyDisplayRect.anchorMin = Vector2.zero;
        keyDisplayRect.anchorMax = Vector2.one;
        keyDisplayRect.offsetMin = Vector2.zero;
        keyDisplayRect.offsetMax = Vector2.zero;

        // ESC 취소 안내 (작은 키 배지 + 텍스트)
        var cancelRow = new GameObject("CancelRow", typeof(RectTransform));
        cancelRow.transform.SetParent(panel.transform, false);
        var cancelRowRect = cancelRow.GetComponent<RectTransform>();
        cancelRowRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRowRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRowRect.pivot     = new Vector2(0.5f, 0f);
        cancelRowRect.anchoredPosition = new Vector2(0f, 30f);
        cancelRowRect.sizeDelta = new Vector2(160f, 32f);
        var cancelHlg = cancelRow.AddComponent<HorizontalLayoutGroup>();
        cancelHlg.childAlignment = TextAnchor.MiddleCenter;
        cancelHlg.spacing = 8f;
        cancelHlg.childControlWidth  = true;
        cancelHlg.childControlHeight = true;
        cancelHlg.childForceExpandWidth  = false;
        cancelHlg.childForceExpandHeight = false;

        var escBadge = new GameObject("EscBadge", typeof(RectTransform));
        escBadge.transform.SetParent(cancelRow.transform, false);
        var escImg = escBadge.AddComponent<Image>();
        escImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        escImg.type = Image.Type.Sliced;
        escImg.color = ControlBg;
        var escLE = escBadge.AddComponent<LayoutElement>();
        escLE.preferredWidth = 48f; escLE.minWidth = 48f; escLE.flexibleWidth = 0f;
        escLE.preferredHeight = 28f; escLE.minHeight = 28f;
        var escTextGO = new GameObject("Text", typeof(RectTransform));
        escTextGO.transform.SetParent(escBadge.transform, false);
        var escTmp = escTextGO.AddComponent<TextMeshProUGUI>();
        escTmp.text = "ESC";
        escTmp.fontSize = 14f;
        escTmp.color = TextPrimary;
        escTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(escTmp);
        var escTextRect = escTextGO.GetComponent<RectTransform>();
        escTextRect.anchorMin = Vector2.zero; escTextRect.anchorMax = Vector2.one;
        escTextRect.offsetMin = Vector2.zero; escTextRect.offsetMax = Vector2.zero;

        var cancelLabelGO = new GameObject("Label", typeof(RectTransform));
        cancelLabelGO.transform.SetParent(cancelRow.transform, false);
        var cancelTmp = cancelLabelGO.AddComponent<TextMeshProUGUI>();
        cancelTmp.text = "설정 취소";
        cancelTmp.fontSize = 18f;
        cancelTmp.color = PillText;
        cancelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyFont(cancelTmp);
        var cancelLE = cancelLabelGO.AddComponent<LayoutElement>();
        cancelLE.preferredWidth = 90f; cancelLE.flexibleWidth = 0f;

        modal.SetActive(false);
        return (modal, actionTmp, keyTmp);
    }

    // 적용 안 한 변경사항이 있을 때 닫기를 막고 띄우는 안내 팝업 — 리바인딩 모달과 같은 톤(백드롭+라이트 패널).
    // 기본 비활성, GlobalSettingsManager.ShowApplyWarning/HideApplyWarning에서 토글.
    private static GameObject CreateApplyWarningModal(Transform parent, GlobalSettingsManager mgr)
    {
        var modal = CreateUIObject("ApplyWarningModal", parent);
        var modalRect = modal.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.offsetMin = Vector2.zero;
        modalRect.offsetMax = Vector2.zero;
        var backdrop = modal.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(modal.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot     = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(600f, 260f);
        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        panelImg.color = PillBg;

        var title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(panel.transform, false);
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "저장하지 않은 변경 사항이 있습니다.";
        titleTmp.fontSize = 26f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = PillText;
        titleTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(titleTmp);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -50f);
        titleRect.sizeDelta = new Vector2(0f, 40f);

        var subtitleGO = new GameObject("Subtitle", typeof(RectTransform));
        subtitleGO.transform.SetParent(panel.transform, false);
        var subtitleTmp = subtitleGO.AddComponent<TextMeshProUGUI>();
        subtitleTmp.text = "저장하시겠습니까?";
        subtitleTmp.fontSize = 22f;
        subtitleTmp.fontStyle = FontStyles.Bold;
        // 제목(Title)과 같은 진한 색 + Bold로 통일 — 기존엔 옅은 회색·얇은 글씨라 제목보다 훨씐 안 보였음.
        subtitleTmp.color = PillText;
        subtitleTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(subtitleTmp);
        var subtitleRect = subtitleGO.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot     = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -100f);
        subtitleRect.sizeDelta = new Vector2(0f, 30f);

        // "예" / "아니오" 버튼 — 가운데 기준으로 좌우에 나란히
        var yesGO = CreateUIObject("Btn_Yes", panel.transform);
        var yesRect = yesGO.GetComponent<RectTransform>();
        yesRect.anchorMin = new Vector2(0.5f, 0f);
        yesRect.anchorMax = new Vector2(0.5f, 0f);
        yesRect.pivot     = new Vector2(1f, 0f);
        yesRect.anchoredPosition = new Vector2(-10f, 40f);
        yesRect.sizeDelta = new Vector2(170f, 56f);
        var yesImg = yesGO.AddComponent<Image>();
        yesImg.sprite = RoundedPillSprite();
        yesImg.type = Image.Type.Sliced;
        yesImg.color = AccentColor;
        var yesBtn = yesGO.AddComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(yesBtn.onClick, mgr.ConfirmSaveAndClose);

        var yesLabelGO = new GameObject("Label", typeof(RectTransform));
        yesLabelGO.transform.SetParent(yesGO.transform, false);
        var yesTmp = yesLabelGO.AddComponent<TextMeshProUGUI>();
        yesTmp.text = "예";
        yesTmp.fontSize = 22f;
        yesTmp.fontStyle = FontStyles.Bold;
        yesTmp.color = PillText; // 노란 액센트 배경 위라 어두운 텍스트로
        yesTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(yesTmp);
        var yesLabelRect = yesLabelGO.GetComponent<RectTransform>();
        yesLabelRect.anchorMin = Vector2.zero;
        yesLabelRect.anchorMax = Vector2.one;
        yesLabelRect.offsetMin = Vector2.zero;
        yesLabelRect.offsetMax = Vector2.zero;

        var noGO = CreateUIObject("Btn_No", panel.transform);
        var noRect = noGO.GetComponent<RectTransform>();
        noRect.anchorMin = new Vector2(0.5f, 0f);
        noRect.anchorMax = new Vector2(0.5f, 0f);
        noRect.pivot     = new Vector2(0f, 0f);
        noRect.anchoredPosition = new Vector2(10f, 40f);
        noRect.sizeDelta = new Vector2(170f, 56f);
        var noImg = noGO.AddComponent<Image>();
        noImg.sprite = RoundedPillSprite();
        noImg.type = Image.Type.Sliced;
        noImg.color = ControlBg;
        var noBtn = noGO.AddComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(noBtn.onClick, mgr.DiscardChangesAndClose);

        var noLabelGO = new GameObject("Label", typeof(RectTransform));
        noLabelGO.transform.SetParent(noGO.transform, false);
        var noTmp = noLabelGO.AddComponent<TextMeshProUGUI>();
        noTmp.text = "아니오";
        noTmp.fontSize = 22f;
        noTmp.fontStyle = FontStyles.Bold;
        noTmp.color = TextPrimary;
        noTmp.alignment = TextAlignmentOptions.Center;
        ApplyFont(noTmp);
        var noLabelRect = noLabelGO.GetComponent<RectTransform>();
        noLabelRect.anchorMin = Vector2.zero;
        noLabelRect.anchorMax = Vector2.one;
        noLabelRect.offsetMin = Vector2.zero;
        noLabelRect.offsetMax = Vector2.zero;

        modal.SetActive(false);
        return modal;
    }
}
#endif
