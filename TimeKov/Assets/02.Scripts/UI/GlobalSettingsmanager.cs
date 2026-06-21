// =====================================================================
// GlobalSettingsManager.cs
// 설정창 콘텐츠 관리 — 그래픽(해상도/전체화면/품질/그림자/텍스처), 오디오(마스터/BGM/SFX),
// 조작(감도/키 리바인딩). 저장은 SettingsData(JSON, persistentDataPath/settings.json).
// ESC / X 버튼 → GameUIController.CloseSettings() 로 닫힘
// 게임 종료 버튼도 이 스크립트에서 처리
//
// 다른 스크립트(InGameAudioManager 등)는 OnBGMVolumeChanged/OnSFXVolumeChanged/
// OnSensitivityChanged 이벤트와 CurrentBGMVolume/CurrentSFXVolume/CurrentSensitivity
// static 프로퍼티만 참조한다 — 시그니처 바뀌면 그쪽도 깨지니 변경 시 주의.
// =====================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GlobalSettingsManager : MonoBehaviour
{
    public static GlobalSettingsManager Instance { get; private set; }

    public static event Action<float> OnBGMVolumeChanged;
    public static event Action<float> OnSFXVolumeChanged;
    public static event Action<float> OnSensitivityChanged;

    // 다른 스크립트의 PlayerPrefs 직접 읽기를 대체하는 정적 접근자 (effective 값 = master 적용 후)
    private static float _currentBGM = 1f;
    private static float _currentSFX = 1f;
    private static float _currentSensitivity = 1f;
    public static float CurrentBGMVolume    => _currentBGM;
    public static float CurrentSFXVolume    => _currentSFX;
    public static float CurrentSensitivity  => _currentSensitivity;

    [Serializable]
    public class RebindSlot
    {
        public string  actionId;   // "Jump","Skill1","Skill2","Skill3","Interact","Instant","QuickSlot","Attack","Dash","Inventory","Stat","Codex"
        public string  displayName; // "점프", "스킬 1" 등 — 리바인딩 모달에 표시용
        public Button  button;
        public TMP_Text keyLabel;
    }

    [Header("UI - 그래픽")]
    public TMP_Dropdown resolutionDropdown;
    public Image         fullscreenOnBg;   // "전체 화면" 버튼 배경 (선택 시 노란색)
    public Image         fullscreenOffBg;  // "창 모드" 버튼 배경 (선택 시 노란색)
    public TMP_Text      fullscreenOnLabel;
    public TMP_Text      fullscreenOffLabel;
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown shadowQualityDropdown;
    public TMP_Dropdown textureQualityDropdown;

    [Header("UI - 오디오")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("UI - 조작")]
    public Slider sensitivitySlider;
    public List<RebindSlot> rebindSlots = new();
    public GameObject rebindModal;
    public TMP_Text   rebindModalActionLabel;
    public TMP_Text   rebindModalKeyDisplay;

    [Header("UI - 탭 (순서: 0=그래픽 1=오디오 2=조작)")]
    public Button[]     tabButtons;
    public GameObject[] tabContents;
    public GameObject[] tabHighlights;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    private static readonly string[] ShadowQualityLabels  = { "낮음", "보통", "높음", "매우 높음" };
    private static readonly string[] TextureQualityLabels = { "매우 높음", "높음", "보통", "낮음" };

    private List<Resolution> _resolutions = new();

    // _data = 마지막으로 "설정 적용"을 눌러 엔진에 반영 + 저장된 상태.
    // _pending = 지금 UI에서 편집 중인 임시값 — 슬라이더/드롭다운을 바꿔도 여기만 바뀌고,
    // "설정 적용"을 눌러야 _data로 커밋되어 실제로 엔진에 반영/저장된다.
    private SettingsData _data;
    private SettingsData _pending;
    private bool _isDirty;
    private string _rebindingActionId;
    private int _currentTab;

    private TMP_Text _hintLabel;
    private string _hintDefaultText;
    private Color _hintDefaultColor;
    private Coroutine _warningCoroutine;
    private static readonly Color ApplyWarningColor = new Color(1f, 0.62f, 0.2f, 1f);
    private const string ApplyWarningText = "변경된 설정이 있습니다. '설정 적용'을 눌러주세요.";

    private static KeyCode[] _rebindCandidates;

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
        InitResolutionOptions();
        InitQualityDropdowns();
        SyncUIValues();
        WireListeners();
        ShowTab(0);
    }

    private static SettingsData Clone(SettingsData src) =>
        JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(src));

    void Update()
    {
        if (_rebindingActionId == null) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { CancelRebind(); return; }

        var candidates = GetRebindCandidates();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (Input.GetKeyDown(candidates[i]))
            {
                CompleteRebind(candidates[i]);
                return;
            }
        }
    }

    private void WireListeners()
    {
        if (bgmSlider != null)          bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null)          sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (masterSlider != null)       masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (sensitivitySlider != null)  sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (qualityDropdown != null)        qualityDropdown.onValueChanged.AddListener(SetQualityLevel);
        if (shadowQualityDropdown != null)  shadowQualityDropdown.onValueChanged.AddListener(SetShadowQuality);
        if (textureQualityDropdown != null) textureQualityDropdown.onValueChanged.AddListener(SetTextureQuality);

        if (tabButtons != null)
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int idx = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => ShowTab(idx));
            }

        if (rebindSlots != null)
            foreach (var slot in rebindSlots)
            {
                if (slot?.button == null) continue;
                string id = slot.actionId;
                slot.button.onClick.AddListener(() => BeginRebind(id));
            }
    }

    // ── 탭 ───────────────────────────────────────────────────────────

    public void ShowTab(int index)
    {
        _currentTab = index;
        if (tabContents != null)
            for (int i = 0; i < tabContents.Length; i++)
                if (tabContents[i] != null) tabContents[i].SetActive(i == index);

        if (tabHighlights != null)
            for (int i = 0; i < tabHighlights.Length; i++)
                if (tabHighlights[i] != null) tabHighlights[i].SetActive(i == index);
    }

    // ── 설정창 열기 / 닫기 ───────────────────────────────────────────

    public void OpenSettings()
    {
        _pending = Clone(_data);
        _isDirty = false;
        HideApplyWarning();
        SyncUIValues();
        GameUIController.Instance?.OpenSettings();
    }

    // 적용되지 않은 변경사항이 있으면 닫기를 거부하고 안내 메세지를 띄운다.
    // GameUIController가 X 버튼(CloseSettings)과 ESC(HandleEscape) 양쪽에서 호출.
    public bool RequestClose()
    {
        if (_rebindingActionId != null) CancelRebind();
        if (_isDirty)
        {
            ShowApplyWarning();
            return false;
        }
        return true;
    }

    public void CloseSettings()
    {
        if (!RequestClose()) return;
        GameUIController.Instance?.CloseSettings();
    }

    private void CacheHintLabel()
    {
        if (_hintLabel != null) return;
        _hintLabel = transform.Find("Option/BG/Settings/Hint")?.GetComponent<TMP_Text>();
        if (_hintLabel != null)
        {
            _hintDefaultText  = _hintLabel.text;
            _hintDefaultColor = _hintLabel.color;
        }
    }

    private void ShowApplyWarning()
    {
        CacheHintLabel();
        if (_hintLabel == null) return;
        if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);
        _hintLabel.text  = ApplyWarningText;
        _hintLabel.color = ApplyWarningColor;
        _warningCoroutine = StartCoroutine(HideWarningAfterDelay());
    }

    private void HideApplyWarning()
    {
        if (_warningCoroutine != null) { StopCoroutine(_warningCoroutine); _warningCoroutine = null; }
        if (_hintLabel == null) return;
        _hintLabel.text  = _hintDefaultText;
        _hintLabel.color = _hintDefaultColor;
    }

    // 설정창은 열려있는 동안 Time.timeScale = 0이라 WaitForSeconds는 절대 끝나지 않는다 — Realtime 사용.
    private System.Collections.IEnumerator HideWarningAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        if (_hintLabel != null) { _hintLabel.text = _hintDefaultText; _hintLabel.color = _hintDefaultColor; }
        _warningCoroutine = null;
    }

    public void ToggleSettings()
    {
        var ui = GameUIController.Instance;
        if (ui == null) return;

        if (ui.GetCurrentState() == GameUIController.UIState.Settings)
            CloseSettings();
        else
            OpenSettings();
    }

    // ── 게임 종료 (메인 메뉴로) ──────────────────────────────────────

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        CoreUtilities.LoadDirect(mainMenuSceneName);
    }

    // ── 설정값 적용 ───────────────────────────────────────────────────

    // "설정 적용" 버튼 — 그동안 폼에서 편집한 _pending을 _data로 커밋하고 저장 + 엔진에 반영.
    // 이 버튼을 누르기 전까지는 슬라이더/드롭다운을 움직여도 실제로 아무 효과가 없다.
    public void ApplySettings()
    {
        _data = Clone(_pending);
        _data.Save();
        ApplyToEngine(_data);
        _isDirty = false;
        HideApplyWarning();
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

        _currentBGM = data.bgmVolume * data.masterVolume;
        _currentSFX = data.sfxVolume * data.masterVolume;
        OnBGMVolumeChanged?.Invoke(_currentBGM);
        OnSFXVolumeChanged?.Invoke(_currentSFX);

        _currentSensitivity = data.sensitivity;
        OnSensitivityChanged?.Invoke(data.sensitivity);
    }

    private void SyncUIValues()
    {
        if (bgmSlider != null)         bgmSlider.SetValueWithoutNotify(_pending.bgmVolume);
        if (sfxSlider != null)         sfxSlider.SetValueWithoutNotify(_pending.sfxVolume);
        if (masterSlider != null)      masterSlider.SetValueWithoutNotify(_pending.masterVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(_pending.sensitivity);
        UpdateFullscreenButtonVisual(_pending.fullscreen);

        if (qualityDropdown != null)
        {
            qualityDropdown.SetValueWithoutNotify(_pending.qualityLevel);
            qualityDropdown.RefreshShownValue();
        }
        if (shadowQualityDropdown != null)
        {
            shadowQualityDropdown.SetValueWithoutNotify(_pending.shadowQualityLevel);
            shadowQualityDropdown.RefreshShownValue();
        }
        if (textureQualityDropdown != null)
        {
            textureQualityDropdown.SetValueWithoutNotify(_pending.textureQualityLevel);
            textureQualityDropdown.RefreshShownValue();
        }

        if (rebindSlots != null)
            foreach (var slot in rebindSlots)
                RestoreLabel(slot?.actionId);
    }

    // ── 그래픽 ───────────────────────────────────────────────────────

    public void SetFullscreen(bool isFullscreen)
    {
        _pending.fullscreen = isFullscreen;
        _isDirty = true;
        UpdateFullscreenButtonVisual(isFullscreen);
    }

    public void SetFullscreenOn()  => SetFullscreen(true);
    public void SetFullscreenOff() => SetFullscreen(false);

    private static readonly Color SegmentSelectedColor    = new Color(1f, 1f, 1f, 1f);            // 화이트 액센트
    private static readonly Color SegmentUnselectedColor  = new Color(0.106f, 0.125f, 0.153f, 0.95f); // 다크 네이비
    private static readonly Color SegmentSelectedText     = new Color(0.10f, 0.08f, 0.02f, 1f);  // 흰 배경 위 어두운 텍스트
    private static readonly Color SegmentUnselectedText   = new Color(0.60f, 0.63f, 0.67f, 1f);  // 다크 배경 위 밝은 회색 텍스트

    private void UpdateFullscreenButtonVisual(bool isFullscreen)
    {
        if (fullscreenOnBg != null)  fullscreenOnBg.color  = isFullscreen ? SegmentSelectedColor : SegmentUnselectedColor;
        if (fullscreenOffBg != null) fullscreenOffBg.color = isFullscreen ? SegmentUnselectedColor : SegmentSelectedColor;
        if (fullscreenOnLabel != null)  fullscreenOnLabel.color  = isFullscreen ? SegmentSelectedText : SegmentUnselectedText;
        if (fullscreenOffLabel != null) fullscreenOffLabel.color = isFullscreen ? SegmentUnselectedText : SegmentSelectedText;
    }

    // 그래픽 탭 전체를 기본값으로 초기화 ("설정 초기화" 버튼) — 폼만 기본값으로 되돌리고,
    // 실제 엔진 반영/저장은 다른 항목들처럼 "설정 적용"을 눌러야 이루어진다.
    public void ResetGraphicsToDefault()
    {
        var res = Screen.currentResolution;
        _pending.resolutionWidth  = res.width;
        _pending.resolutionHeight = res.height;
        _pending.fullscreen       = true;
        _pending.qualityLevel        = Mathf.Clamp(1, 0, QualitySettings.names.Length - 1);
        _pending.shadowQualityLevel  = 2; // 높음
        _pending.textureQualityLevel = 0; // 매우 높음
        _isDirty = true;

        int idx = _resolutions.FindIndex(r => r.width == res.width && r.height == res.height);
        if (idx < 0) idx = 0;
        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(idx);
            resolutionDropdown.RefreshShownValue();
        }

        SyncUIValues();
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= _resolutions.Count) return;
        Resolution res = _resolutions[index];
        _pending.resolutionWidth  = res.width;
        _pending.resolutionHeight = res.height;
        _isDirty = true;
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
        switch (level)
        {
            case 0:  QualitySettings.shadowResolution = ShadowResolution.Low;    QualitySettings.shadowDistance = 20f;  break;
            case 1:  QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.shadowDistance = 50f;  break;
            case 2:  QualitySettings.shadowResolution = ShadowResolution.High;   QualitySettings.shadowDistance = 100f; break;
            default: QualitySettings.shadowResolution = ShadowResolution.VeryHigh; QualitySettings.shadowDistance = 150f; break;
        }
    }

    private void ApplyTextureQuality(int level)
    {
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(level, 0, 3);
    }

    private void InitResolutionOptions()
    {
        if (resolutionDropdown == null) return;

        Resolution[] allRes = Screen.resolutions;
        _resolutions.Clear();
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        var seen = new HashSet<string>();
        int currentIndex = 0;

        foreach (var r in allRes)
        {
            if (r.width < 1280 || r.height < 720) continue;
            string label = $"{r.width} x {r.height}";
            if (!seen.Add(label)) continue;

            options.Add(label);
            _resolutions.Add(r);
            if (r.width == _data.resolutionWidth && r.height == _data.resolutionHeight)
                currentIndex = _resolutions.Count - 1;
        }

        if (options.Count == 0)
        {
            options.Add($"{Screen.width} x {Screen.height}");
            _resolutions.Add(Screen.currentResolution);
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(currentIndex);
        resolutionDropdown.RefreshShownValue();
    }

    private void InitQualityDropdowns()
    {
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }
        if (shadowQualityDropdown != null)
        {
            shadowQualityDropdown.ClearOptions();
            shadowQualityDropdown.AddOptions(new List<string>(ShadowQualityLabels));
        }
        if (textureQualityDropdown != null)
        {
            textureQualityDropdown.ClearOptions();
            textureQualityDropdown.AddOptions(new List<string>(TextureQualityLabels));
        }
    }

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

    public void BeginRebind(string actionId)
    {
        if (_rebindingActionId != null) return; // 이미 다른 키 리바인딩 중
        _rebindingActionId = actionId;
        var slot = FindSlot(actionId);
        if (slot?.keyLabel != null) slot.keyLabel.text = "키 입력...";
        ShowRebindModal(slot?.displayName ?? actionId);
    }

    private void CancelRebind()
    {
        RestoreLabel(_rebindingActionId);
        _rebindingActionId = null;
        HideRebindModal();
    }

    // 정적 KeyBindings(현재 적용 중인 값)가 아니라 _pending.keyBindings(아직 적용 안 한 폼 값) 기준으로
    // 충돌을 검사한다 — 같은 세션에서 먼저 바꾼 키(아직 미적용)와 충돌하는 것도 잡아야 하기 때문.
    private bool IsPendingConflict(KeyCode code, string excludeAction, out string conflictAction)
    {
        conflictAction = null;
        if (rebindSlots == null) return false;
        foreach (var slot in rebindSlots)
        {
            if (slot == null || slot.actionId == excludeAction) continue;
            if (GetKeyForAction(slot.actionId) == code) { conflictAction = slot.displayName; return true; }
        }
        return false;
    }

    private void CompleteRebind(KeyCode code)
    {
        if (IsPendingConflict(code, _rebindingActionId, out string conflictAction))
        {
            Debug.LogWarning($"[Settings] '{code}' 키는 이미 '{conflictAction}'에 사용 중입니다.");
            RestoreLabel(_rebindingActionId);
            _rebindingActionId = null;
            HideRebindModal();
            return;
        }

        SetKeyForAction(_rebindingActionId, code);
        RestoreLabel(_rebindingActionId);
        _rebindingActionId = null;
        HideRebindModal();
    }

    private void ShowRebindModal(string actionDisplayName)
    {
        if (rebindModal != null) rebindModal.SetActive(true);
        if (rebindModalActionLabel != null) rebindModalActionLabel.text = actionDisplayName;
        if (rebindModalKeyDisplay != null) rebindModalKeyDisplay.text = "";
    }

    private void HideRebindModal()
    {
        if (rebindModal != null) rebindModal.SetActive(false);
    }

    private void RestoreLabel(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return;
        var slot = FindSlot(actionId);
        if (slot?.keyLabel != null) slot.keyLabel.text = GetKeyForAction(actionId).ToString();
    }

    private RebindSlot FindSlot(string actionId)
    {
        if (rebindSlots == null || string.IsNullOrEmpty(actionId)) return null;
        foreach (var s in rebindSlots)
            if (s != null && s.actionId == actionId) return s;
        return null;
    }

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

    // 키보드 키 + 마우스 좌/우/휠클릭만 후보로 사용 (조이스틱·기타 마우스 버튼 제외)
    // 기본 공격/대시가 마우스 좌/우클릭이라 리바인딩 후보에 Mouse0~2를 포함해야
    // 사용자가 키보드로 옮긴 뒤 다시 마우스로 되돌릴 수 있다.
    private static KeyCode[] GetRebindCandidates()
    {
        if (_rebindCandidates != null) return _rebindCandidates;

        var list = new List<KeyCode>();
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None) continue;
            string n = kc.ToString();
            if (n.StartsWith("Joystick")) continue;
            if (n.StartsWith("Mouse") && kc != KeyCode.Mouse0 && kc != KeyCode.Mouse1 && kc != KeyCode.Mouse2) continue;
            list.Add(kc);
        }
        _rebindCandidates = list.ToArray();
        return _rebindCandidates;
    }
}
