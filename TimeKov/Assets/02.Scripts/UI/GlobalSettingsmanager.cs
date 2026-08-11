// =====================================================================
// GlobalSettingsManager.cs
// 설정값의 모델·엔진 계층 — 그래픽(해상도/전체화면/품질/그림자/텍스처),
// 오디오(마스터/BGM/SFX), 조작(감도/키 리바인딩).
// 저장은 SettingsData(JSON, persistentDataPath/settings.json).
//
// UI는 갖지 않는다. 화면은 GameSettingsUI(코드 생성 + 씬 베이크)가 담당하고,
// 값 접근은 GameSettingsUI/SettingsBinding 한 곳을 통해 들어온다.
// _pending(편집 중) → "설정 적용" → _data(커밋·저장·엔진 반영) 2단 구조는 그대로다.
//
// 다른 스크립트(InGameAudioManager 등)는 OnBGMVolumeChanged/OnSFXVolumeChanged/
// OnSensitivityChanged/OnKeyBindingsChanged 이벤트와 CurrentBGMVolume/CurrentSFXVolume/
// CurrentSensitivity static 프로퍼티만 참조한다 — 시그니처 바뀌면 그쪽도 깨지니 변경 시 주의.
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class GlobalSettingsManager : MonoBehaviour
{
    private static GlobalSettingsManager _instance;
    public static GlobalSettingsManager Instance
    {
        get
        {
#if UNITY_EDITOR
            // 재생 전(에디터 툴에서 설정 UI를 씬에 굽는 등)에는 Awake가 돌지 않아 비어 있다.
            // 씬에서 직접 찾아준다. 비활성 오브젝트에 붙어 있어도 찾도록 FindObjectsOfTypeAll을 쓰되,
            // 프로젝트의 프리팹 에셋은 제외하기 위해 씬에 속한 것만 고른다.
            if (_instance == null && !Application.isPlaying)
            {
                foreach (var m in Resources.FindObjectsOfTypeAll<GlobalSettingsManager>())
                    if (m.gameObject.scene.IsValid()) { _instance = m; break; }
            }
#endif
            return _instance;
        }
        private set => _instance = value;
    }

    public static event Action<float> OnBGMVolumeChanged;
    public static event Action<float> OnSFXVolumeChanged;
    public static event Action<float> OnSensitivityChanged;
    public static event Action OnKeyBindingsChanged;
    public static event Action<LanguageCode> OnLanguageChanged;

    // 다른 스크립트의 PlayerPrefs 직접 읽기를 대체하는 정적 접근자.
    // 주의: 마스터는 여기 안 섞인다 - 마스터는 AudioListener.volume(전역)이 담당한다.
    //   그래서 이 값은 'BGM/SFX 슬라이더 값' 그 자체다.
    private static float _currentBGM = 1f;
    private static float _currentSFX = 1f;
    private static float _currentSensitivity = 1f;
    public static float CurrentBGMVolume    => _currentBGM;
    public static float CurrentSFXVolume    => _currentSFX;
    public static float CurrentSensitivity  => _currentSensitivity;
    public static LanguageCode CurrentLanguage => Loc.CurrentLanguage;


    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    private static readonly string[] QualityLabels        = { "낮음", "보통", "높음" }; // QualitySettings: Low(0)/Medium(1)/High(2)
    private static readonly string[] ShadowQualityLabels  = { "매우 높음", "높음", "보통", "낮음" };
    private static readonly string[] TextureQualityLabels = { "매우 높음", "높음", "보통", "낮음" };
    private static readonly string[] LanguageLabels       = { "한국어", "영어", "중국어", "프랑스어" };


    // _data = 마지막으로 "설정 적용"을 눌러 엔진에 반영 + 저장된 상태.
    // _pending = 지금 UI에서 편집 중인 임시값 — 슬라이더/드롭다운을 바꿔도 여기만 바뀌고,
    // "설정 적용"을 눌러야 _data로 커밋되어 실제로 엔진에 반영/저장된다.
    private SettingsData _data;
    private SettingsData _pending;
    private bool _isDirty;


    // GameUIController.settingsPanel은 이 컴포넌트가 붙은 루트(SettingsPanel)가 아니라
    // 자식 "Option"을 가리킨다(World 씬에서 실제로 켜고 끄는 대상) — SettingsPanel 자신은
    // World 씬에서 항상 켜져 있는 컨트롤러 래퍼일 뿐이라, 이 컴포넌트가 직접 패널을 열고
    // 닫아야 하는 MainMenu 같은 씬(GameUIController 없음)에서는 gameObject가 아니라
    // "Option"을 켜고 꺼야 실제 비주얼이 보인다.
    private GameObject _visualRoot;
    private GameObject VisualRoot => _visualRoot ??= transform.Find("Option")?.gameObject;

    // ── 초기화 ───────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _data = SettingsData.Load();
        _pending = Clone(_data);

        ApplyToEngine(_data);

        // DataBoot를 거치지 않은 씬(MainMenu 직접 실행 등)에서도 번역 테이블을 로드한다.
        // DataBoot.IsLoaded == true이면 이미 로드됐으므로 건너뛴다.
        if (!DataBoot.IsLoaded)
            LocalizationLoader.LoadAsync(this, _ => { });

        // GameUIController가 없는 씬(MainMenu)에서는 패널을 직접 관리해야 한다.
        // World 씬은 GameUIController.Awake()의 ApplyState()가 시작 시 강제로 닫아주지만
        // 여기는 그게 없으므로, 초기화가 끝난 뒤 여기서 직접 닫아둔다.
        if (GameUIController.Instance == null && VisualRoot != null)
            VisualRoot.SetActive(false);
    }

    private static SettingsData Clone(SettingsData src) =>
        JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(src));


    // ── 설정창 열기 / 닫기 ───────────────────────────────────────────

    // RefreshOnOpen()은 직접 호출하지 않는다 — GameUIController.SetState()가 Settings로
    // 진입하는 모든 경로(이 메서드 포함)에서 단일 지점으로 이미 호출해준다. 여기서 또 부르면
    // 같은 오픈에 두 번 호출되는 것뿐이라 해는 없지만(_pending 리셋은 멱등) 그냥 중복이다.
    public void OpenSettings()
    {
        if (GameUIController.Instance != null) { GameUIController.Instance.OpenSettings(); return; }

        // GameUIController가 없는 씬(MainMenu) — 직접 열고 동기화.
        RefreshOnOpen();
        if (VisualRoot != null) VisualRoot.SetActive(true);
    }

    // 폼을 _data 기준으로 리셋(편집 중이던 _pending 폐기) + UI 동기화.
    // GameUIController.SetState()가 Settings로 들어가는 모든 경로(ESC 포함)에서 호출해
    // 누락 없이 보장한다.
    public void RefreshOnOpen()
    {
        _pending = Clone(_data);
        _isDirty = false;


    }

    // 적용되지 않은 변경사항이 있으면 닫기를 거부하고 안내 메세지를 띄운다.
    // GameUIController가 X 버튼(CloseSettings)과 ESC(HandleEscape) 양쪽에서 호출.
    public bool RequestClose()
    {

        if (_isDirty)
        {

            return false;
        }
        return true;
    }

    public void CloseSettings()
    {
        if (!RequestClose()) return;

        if (GameUIController.Instance != null) { GameUIController.Instance.CloseSettings(); return; }

        // GameUIController가 없는 씬(MainMenu) — 직접 닫기.
        if (VisualRoot != null) VisualRoot.SetActive(false);
    }


    // ── 게임 종료 (메인 메뉴로) ──────────────────────────────────────

    public void QuitToMainMenu()
    {
        CoreUtilities.SaveAndLoadMainMenu(mainMenuSceneName);   // 나가기 전 저장(진행 유실 방지) + timeScale 정상화 포함
        // 버튼음은 잔향 컷(GameSfx.CutAll) 뒤에 재생해야 같이 안 끊긴다.
        // GameSfx 는 DDOL 이라 씬 전환 중에도 클릭음이 정상적으로 들린다.
        GameSfx.Play(SfxId.SettingsClick);
    }

    // ── 설정값 적용 ───────────────────────────────────────────────────

    // "설정 적용" 버튼 — 그동안 폼에서 편집한 _pending을 _data로 커밋하고 저장 + 엔진에 반영.
    // 이 버튼을 누르기 전까지는 슬라이더/드롭다운을 움직여도 실제로 아무 효과가 없다.
    public void ApplySettings()
    {
        GameSfx.Play(SfxId.SettingsClick);   // 하단 '적용' 버튼음
        _data = Clone(_pending);
        _data.Save();
        ApplyToEngine(_data);
        _isDirty = false;

    }

    private void ApplyToEngine(SettingsData data)
    {
        if (data.resolutionWidth > 0 && data.resolutionHeight > 0)
            Screen.SetResolution(data.resolutionWidth, data.resolutionHeight, data.fullscreen);
        else
            Screen.fullScreen = data.fullscreen;

        QualitySettings.SetQualityLevel(data.qualityLevel, true);
        ApplyShadowQuality(data.shadowQualityLevel);
        ApplyTextureQuality(data.textureQualityLevel);
        KeyBindings.Apply(data.keyBindings);
        OnKeyBindingsChanged?.Invoke(); // 스킬바 등 키 라벨을 직접 그리는 UI에게 리바인딩 결과를 알림

        // 마스터는 AudioListener 전역 볼륨으로 건다.
        // 개별 AudioSource 볼륨에 곱해 넣으면 '지금 씬에 있는 소리'에만 반영된다 -> 그 뒤에
        // 스폰되는 몬스터나 PlayClipAtPoint 로 나는 소리는 동기화 시점을 놓쳐서
        // 마스터 0 인데도 최대 음량으로 났다(QA 보고). 전역이면 태어나는 시점과 무관하게 걸린다.
        AudioListener.volume = Mathf.Clamp01(data.masterVolume);

        _currentBGM = data.bgmVolume;
        _currentSFX = data.sfxVolume;
        OnBGMVolumeChanged?.Invoke(_currentBGM);
        OnSFXVolumeChanged?.Invoke(_currentSFX);

        _currentSensitivity = data.sensitivity;
        OnSensitivityChanged?.Invoke(data.sensitivity);

        var langCode = Loc.FromCode(data.language);
        Loc.SetLanguage(langCode);
        OnLanguageChanged?.Invoke(langCode);
    }

    // 언어 설정 변경 (옵션 UI에서 드롭다운 선택 시 호출)
    public void SetLanguage(LanguageCode code)
    {
        _pending.language = Loc.ToCode(code);
        _isDirty = true;
    }


    // ── 그래픽 ───────────────────────────────────────────────────────

    public void SetFullscreen(bool isFullscreen)
    {
        GameSfx.Play(SfxId.SettingsClick);   // 전체화면/창모드 전환음
        _pending.fullscreen = isFullscreen;
        _isDirty = true;

    }


    // 설정 전체(오디오/조작/그래픽/키바인딩)를 기본값으로 초기화 ("설정 초기화" 버튼) — 폼만
    // 기본값으로 되돌리고, 실제 엔진 반영/저장은 다른 항목들처럼 "설정 적용"을 눌러야 이루어진다.
    public void ResetAllToDefault()
    {
        GameSfx.Play(SfxId.SettingsClick);   // 하단 '초기화' 버튼음
        var defaults = SettingsData.CreateDefault();
        defaults.qualityLevel = Mathf.Clamp(1, 0, QualitySettings.names.Length - 1);
        _pending = defaults;
        _isDirty = true;
        // 표시 갱신은 UI 쪽 책임 — 새 설정 UI가 이 호출 뒤 RefreshAll()로 다시 읽어간다.
    }


    public void SetQualityLevel(int index)
    {
        _pending.qualityLevel = index;
        _isDirty = true;
    }

    public void SetShadowQuality(int level)
    {
        _pending.shadowQualityLevel = level;
        _isDirty = true;
    }

    public void SetTextureQuality(int level)
    {
        _pending.textureQualityLevel = level;
        _isDirty = true;
    }

    private void ApplyShadowQuality(int level)
    {
        UnityEngine.ShadowResolution legacyRes;
        int urpRes;
        float distance;
        // 인덱스 0="매우 높음"(best)→3="낮음"(worst) — ShadowQualityLabels 내림차순과 동기화
        switch (level)
        {
            case 0:  legacyRes = UnityEngine.ShadowResolution.VeryHigh; urpRes = 4096; distance = 150f; break;
            case 1:  legacyRes = UnityEngine.ShadowResolution.High;     urpRes = 2048; distance = 100f; break;
            case 2:  legacyRes = UnityEngine.ShadowResolution.Medium;   urpRes = 1024; distance = 50f;  break;
            default: legacyRes = UnityEngine.ShadowResolution.Low;      urpRes = 512;  distance = 20f;  break;
        }

        // QualitySettings.shadowResolution/shadowDistance는 Built-in 렌더러 전용 필드라
        // 이 프로젝트가 쓰는 URP에서는 완전히 무시된다 — URP는 파이프라인 에셋 자체의
        // mainLightShadowmapResolution/shadowDistance를 따로 갖고 있어 그쪽도 같이 바꿔야 실제로 반영된다.
        QualitySettings.shadowResolution = legacyRes;
        QualitySettings.shadowDistance = distance;

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
        {
            urpAsset.mainLightShadowmapResolution = urpRes;
            urpAsset.shadowDistance = distance;
        }
    }

    private void ApplyTextureQuality(int level)
    {
        // 인덱스 0="매우 높음"(mipmapLimit=0) → 3="낮음"(mipmapLimit=3) — TextureQualityLabels 내림차순과 동기화
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(level, 0, 3);
    }

    // 모니터가 지원하는 해상도를 전부 나열하지 않고, 이 3개로 고정한다 (요청에 따라 축소).
    private static readonly (int width, int height)[] FixedResolutions =
    {
        (1280, 720),
        (1920, 1080),
        (2560, 1440),
    };



    // ── 오디오 ───────────────────────────────────────────────────────

    public void SetMasterVolume(float master)
    {
        _pending.masterVolume = master;
        _isDirty = true;
    }

    public void SetBGMVolume(float volume)
    {
        _pending.bgmVolume = volume;
        _isDirty = true;
    }

    public void SetSFXVolume(float volume)
    {
        _pending.sfxVolume = volume;
        _isDirty = true;
    }

    // ── 조작 ───────────────────────────────────────────────────────

    public void SetSensitivity(float sens)
    {
        _pending.sensitivity = sens;
        _isDirty = true;
    }

    // ── 키 리바인딩 ───────────────────────────────────────────────────


    // _pending.keyBindings(폼에서 편집 중인 값) 기준 — "설정 적용"을 눌러야 KeyBindings(static)에 반영된다.
    private KeyCode GetKeyForAction(string actionId) => actionId switch
    {
        "Jump"      => _pending.keyBindings.jump,
        "Skill1"    => _pending.keyBindings.skill1,
        "Skill2"    => _pending.keyBindings.skill2,
        "Skill3"    => _pending.keyBindings.skill3,
        "Interact"  => _pending.keyBindings.interact,
        "Instant"   => _pending.keyBindings.instant,
        "QuickSlot" => _pending.keyBindings.quickSlot,
        "Attack"    => _pending.keyBindings.attack,
        "Dash"      => _pending.keyBindings.dash,
        "Inventory" => _pending.keyBindings.inventory,
        "Stat"      => _pending.keyBindings.stat,
        "Codex"     => _pending.keyBindings.codex,
        _           => KeyCode.None
    };

    private void SetKeyForAction(string actionId, KeyCode code)
    {
        switch (actionId)
        {
            case "Jump":      _pending.keyBindings.jump      = code; break;
            case "Skill1":    _pending.keyBindings.skill1    = code; break;
            case "Skill2":    _pending.keyBindings.skill2    = code; break;
            case "Skill3":    _pending.keyBindings.skill3    = code; break;
            case "Interact":  _pending.keyBindings.interact  = code; break;
            case "Instant":   _pending.keyBindings.instant   = code; break;
            case "QuickSlot": _pending.keyBindings.quickSlot = code; break;
            case "Attack":    _pending.keyBindings.attack    = code; break;
            case "Dash":      _pending.keyBindings.dash      = code; break;
            case "Inventory": _pending.keyBindings.inventory = code; break;
            case "Stat":      _pending.keyBindings.stat      = code; break;
            case "Codex":     _pending.keyBindings.codex     = code; break;
            default: return;
        }
        _isDirty = true;
    }

    // ── 코드 생성 설정 UI(GameSettingsUI) 연동 ─────────────────────────
    // 기존 로직·저장 형식·적용 순서는 그대로 두고, 새 UI가 편집 중인 값을 읽고
    // 커밋할 수 있도록 최소한만 공개한다. 새 UI는 인스펙터 참조(드롭다운/슬라이더)를
    // 하나도 쓰지 않으므로, 그 조합에서 이 컴포넌트는 모델·엔진 계층으로만 동작한다.

    /// 아직 "설정 적용"을 누르지 않은 변경사항이 있는가.
    public bool HasUnappliedChanges => _isDirty;

    /// 편집 중인 임시값. "설정 적용"을 눌러야 _data로 커밋된다.
    public SettingsData PendingData { get { EnsureLoaded(); return _pending; } }

    // _data/_pending은 Start()에서 초기화되는데, Start끼리의 실행 순서는 보장되지 않는다.
    // 새 UI가 자기 Start에서 이 값들을 읽으므로, 아직 로드 전이면 여기서 먼저 채운다.
    // (Start가 나중에 돌아 다시 로드해도 결과는 같으므로 기존 흐름에는 영향이 없다)
    private void EnsureLoaded()
    {
        if (_pending != null) return;
        if (_data == null) _data = SettingsData.Load();
        _pending = Clone(_data);
    }

    /// 리바인딩 대상 12개 액션 (id, 표시명).
    public static readonly (string id, string label)[] RebindActions =
    {
        ("Attack",    "기본 공격"), ("Dash",   "대시"),   ("Jump",   "점프"),
        ("Skill1",    "스킬 1"),    ("Skill2", "스킬 2"), ("Skill3", "스킬 3"),
        ("Interact",  "상호작용"),  ("Instant","즉시완료"), ("QuickSlot","퀵슬롯"),
        ("Inventory", "인벤토리"),  ("Stat",   "스탯창"), ("Codex",  "도감"),
    };

    public KeyCode GetPendingKey(string actionId) { EnsureLoaded(); return GetKeyForAction(actionId); }

    /// 캡처한 키를 충돌 검사 후 _pending에 커밋한다. 충돌이면 false + 충돌 상대 이름.
    /// rebindSlots(구 UI의 인스펙터 목록)에 의존하지 않도록 RebindActions로 검사한다.
    // 리바인딩 가능한 12개 액션 외에, 게임 전역에서 상태와 무관하게 항상 활성화된 하드코딩 키.
    // (건설모드/디버그용 키(E/R/B/X/Alpha1-9/F7~F11 등)는 해당 모드에서만 켜져서 일반 액션과
    //  동시에 쓰이지 않으므로 제외 — 항상 켜져 있는 키만 여기 등록한다.)
    private static readonly (KeyCode code, string label)[] ReservedGlobalKeys =
    {
        // J(퀘스트 팝업)는 잔재로 제거됨(GameUIController) - 더 이상 예약 키 없음.
    };

    private bool IsReservedKeyConflict(KeyCode code, out string conflictAction)
    {
        foreach (var (reservedCode, label) in ReservedGlobalKeys)
        {
            if (reservedCode == code) { conflictAction = label; return true; }
        }
        conflictAction = null;
        return false;
    }

    public bool TryRebind(string actionId, KeyCode code, out string conflictAction)
    {
        EnsureLoaded();
        foreach (var (id, label) in RebindActions)
            if (id != actionId && GetKeyForAction(id) == code) { conflictAction = label; return false; }
        if (IsReservedKeyConflict(code, out conflictAction)) return false;
        SetKeyForAction(actionId, code);
        conflictAction = null;
        return true;
    }

    /// 저장된 해상도가 선택지(FixedResolutions)에 없으면 드롭다운에 아무것도 선택되지 않고
    /// 표시값과 실제 값이 어긋난다. 기본값이 모니터 네이티브 해상도(SettingsData.CreateDefault)라
    /// 4K·울트라와이드에서는 첫 실행부터 이 상태가 된다.
    /// 목록 안의 값(저장값 이하 중 가장 큰 것)으로 맞춘다.
    /// 사용자가 바꾼 게 아니므로 _isDirty는 건드리지 않는다 — 건드리면 열자마자 "미적용 변경"이 된다.
    public void NormalizeResolution()
    {
        EnsureLoaded();
        foreach (var (w, h) in FixedResolutions)
            if (w == _pending.resolutionWidth && h == _pending.resolutionHeight) return;

        int pick = 0;
        for (int i = 0; i < FixedResolutions.Length; i++)
            if (FixedResolutions[i].width <= _pending.resolutionWidth) pick = i;

        var r = FixedResolutions[pick];
        _pending.resolutionWidth = r.width; _pending.resolutionHeight = r.height;
        if (_data != null) { _data.resolutionWidth = r.width; _data.resolutionHeight = r.height; }
    }

    /// 해상도를 값으로 지정. 선택지는 FixedResolutions(= ResolutionOptions)에서 고른다.
    public void SetResolution(int width, int height)
    {
        EnsureLoaded();
        _pending.resolutionWidth  = width;
        _pending.resolutionHeight = height;
        _isDirty = true;
    }

    public static IReadOnlyList<(int width, int height)> ResolutionOptions   => FixedResolutions;
    public static IReadOnlyList<string> QualityOptions        => QualityLabels;
    public static IReadOnlyList<string> ShadowQualityOptions  => ShadowQualityLabels;
    public static IReadOnlyList<string> TextureQualityOptions => TextureQualityLabels;
    public static IReadOnlyList<string> LanguageOptions       => LanguageLabels;

    // 키보드 키 + 마우스 좌/우/휠클릭만 후보로 사용 (조이스틱·기타 마우스 버튼 제외)
    // 기본 공격/대시가 마우스 좌/우클릭이라 리바인딩 후보에 Mouse0~2를 포함해야
    // 사용자가 키보드로 옮긴 뒤 다시 마우스로 되돌릴 수 있다.
}
