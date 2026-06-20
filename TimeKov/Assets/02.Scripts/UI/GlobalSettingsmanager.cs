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
        public string  actionId;   // "Jump","Skill1","Skill2","Skill3","Interact","Instant","QuickSlot"
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
    private SettingsData _data;
    private string _rebindingActionId;
    private int _currentTab;

    private static KeyCode[] _rebindCandidates;

    // ── 초기화 ───────────────────────────────────────────────────────

    void Start()
    {
        _data = SettingsData.Load();
        KeyBindings.Apply(_data.keyBindings);

        ApplyLoadedSettings();
        InitResolutionOptions();
        InitQualityDropdowns();
        SyncUIValues();
        WireListeners();
        ShowTab(0);
    }

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
        SyncUIValues();
        GameUIController.Instance?.OpenSettings();
    }

    public void CloseSettings()
    {
        if (_rebindingActionId != null) CancelRebind();
        GameUIController.Instance?.CloseSettings();
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

    // "설정 적용" 버튼 — 각 항목은 변경 즉시 저장/반영되지만, 해상도 등 한 번에 다시
    // 밀어넣고 싶을 때를 위해 현재 _data를 엔진에 재적용한다.
    public void ApplySettings() => ApplyLoadedSettings();

    private void ApplyLoadedSettings()
    {
        if (_data.resolutionWidth > 0 && _data.resolutionHeight > 0)
            Screen.SetResolution(_data.resolutionWidth, _data.resolutionHeight, _data.fullscreen);
        else
            Screen.fullScreen = _data.fullscreen;

        QualitySettings.SetQualityLevel(_data.qualityLevel, true);
        ApplyShadowQuality(_data.shadowQualityLevel);
        ApplyTextureQuality(_data.textureQualityLevel);

        SetMasterVolume(_data.masterVolume);
        SetBGMVolume(_data.bgmVolume);
        SetSFXVolume(_data.sfxVolume);
        SetSensitivity(_data.sensitivity);
    }

    private void SyncUIValues()
    {
        if (bgmSlider != null)         bgmSlider.SetValueWithoutNotify(_data.bgmVolume);
        if (sfxSlider != null)         sfxSlider.SetValueWithoutNotify(_data.sfxVolume);
        if (masterSlider != null)      masterSlider.SetValueWithoutNotify(_data.masterVolume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(_data.sensitivity);
        UpdateFullscreenButtonVisual(Screen.fullScreen);

        if (qualityDropdown != null)
        {
            qualityDropdown.SetValueWithoutNotify(_data.qualityLevel);
            qualityDropdown.RefreshShownValue();
        }
        if (shadowQualityDropdown != null)
        {
            shadowQualityDropdown.SetValueWithoutNotify(_data.shadowQualityLevel);
            shadowQualityDropdown.RefreshShownValue();
        }
        if (textureQualityDropdown != null)
        {
            textureQualityDropdown.SetValueWithoutNotify(_data.textureQualityLevel);
            textureQualityDropdown.RefreshShownValue();
        }

        if (rebindSlots != null)
            foreach (var slot in rebindSlots)
                RestoreLabel(slot?.actionId);
    }

    // ── 그래픽 ───────────────────────────────────────────────────────

    public void SetFullscreen(bool isFullscreen)
    {
        _data.fullscreen = isFullscreen;
        _data.Save();
        Screen.fullScreen = isFullscreen;
        UpdateFullscreenButtonVisual(isFullscreen);
    }

    public void SetFullscreenOn()  => SetFullscreen(true);
    public void SetFullscreenOff() => SetFullscreen(false);

    private static readonly Color SegmentSelectedColor    = new Color(1f, 0.82f, 0.10f, 1f);     // 노란 액센트
    private static readonly Color SegmentUnselectedColor  = new Color(0.106f, 0.125f, 0.153f, 0.95f); // 다크 네이비
    private static readonly Color SegmentSelectedText     = new Color(0.10f, 0.08f, 0.02f, 1f);  // 노란 배경 위 어두운 텍스트
    private static readonly Color SegmentUnselectedText   = new Color(0.60f, 0.63f, 0.67f, 1f);  // 다크 배경 위 밝은 회색 텍스트

    private void UpdateFullscreenButtonVisual(bool isFullscreen)
    {
        if (fullscreenOnBg != null)  fullscreenOnBg.color  = isFullscreen ? SegmentSelectedColor : SegmentUnselectedColor;
        if (fullscreenOffBg != null) fullscreenOffBg.color = isFullscreen ? SegmentUnselectedColor : SegmentSelectedColor;
        if (fullscreenOnLabel != null)  fullscreenOnLabel.color  = isFullscreen ? SegmentSelectedText : SegmentUnselectedText;
        if (fullscreenOffLabel != null) fullscreenOffLabel.color = isFullscreen ? SegmentUnselectedText : SegmentSelectedText;
    }

    // 그래픽 탭 전체를 기본값으로 초기화 ("설정 초기화" 버튼)
    public void ResetGraphicsToDefault()
    {
        var res = Screen.currentResolution;
        _data.resolutionWidth  = res.width;
        _data.resolutionHeight = res.height;
        _data.fullscreen       = true;
        _data.qualityLevel        = Mathf.Clamp(1, 0, QualitySettings.names.Length - 1);
        _data.shadowQualityLevel  = 2; // 높음
        _data.textureQualityLevel = 0; // 매우 높음
        _data.Save();

        Screen.SetResolution(res.width, res.height, true);
        QualitySettings.SetQualityLevel(_data.qualityLevel, true);
        ApplyShadowQuality(_data.shadowQualityLevel);
        ApplyTextureQuality(_data.textureQualityLevel);

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
        _data.resolutionWidth  = res.width;
        _data.resolutionHeight = res.height;
        _data.Save();
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    public void SetQualityLevel(int index)
    {
        _data.qualityLevel = index;
        _data.Save();
        QualitySettings.SetQualityLevel(index, true);
    }

    public void SetShadowQuality(int level)
    {
        _data.shadowQualityLevel = level;
        _data.Save();
        ApplyShadowQuality(level);
    }

    public void SetTextureQuality(int level)
    {
        _data.textureQualityLevel = level;
        _data.Save();
        ApplyTextureQuality(level);
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
        _data.masterVolume = master;
        _data.Save();
        BroadcastVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        _data.bgmVolume = volume;
        _data.Save();
        BroadcastVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        _data.sfxVolume = volume;
        _data.Save();
        BroadcastVolumes();
    }

    private void BroadcastVolumes()
    {
        _currentBGM = _data.bgmVolume * _data.masterVolume;
        _currentSFX = _data.sfxVolume * _data.masterVolume;
        OnBGMVolumeChanged?.Invoke(_currentBGM);
        OnSFXVolumeChanged?.Invoke(_currentSFX);
    }

    // ── 조작 ───────────────────────────────────────────────────────

    public void SetSensitivity(float sens)
    {
        _data.sensitivity = sens;
        _data.Save();
        _currentSensitivity = sens;
        OnSensitivityChanged?.Invoke(sens);
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

    private void CompleteRebind(KeyCode code)
    {
        if (KeyBindings.IsConflict(code, _rebindingActionId, out string conflictAction))
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

    private KeyCode GetKeyForAction(string actionId) => actionId switch
    {
        "Jump"      => KeyBindings.Jump,
        "Skill1"    => KeyBindings.Skill1,
        "Skill2"    => KeyBindings.Skill2,
        "Skill3"    => KeyBindings.Skill3,
        "Interact"  => KeyBindings.Interact,
        "Instant"   => KeyBindings.Instant,
        "QuickSlot" => KeyBindings.QuickSlot,
        _           => KeyCode.None
    };

    private void SetKeyForAction(string actionId, KeyCode code)
    {
        switch (actionId)
        {
            case "Jump":      KeyBindings.Jump      = code; _data.keyBindings.jump      = code; break;
            case "Skill1":    KeyBindings.Skill1    = code; _data.keyBindings.skill1    = code; break;
            case "Skill2":    KeyBindings.Skill2    = code; _data.keyBindings.skill2    = code; break;
            case "Skill3":    KeyBindings.Skill3    = code; _data.keyBindings.skill3    = code; break;
            case "Interact":  KeyBindings.Interact  = code; _data.keyBindings.interact  = code; break;
            case "Instant":   KeyBindings.Instant   = code; _data.keyBindings.instant   = code; break;
            case "QuickSlot": KeyBindings.QuickSlot = code; _data.keyBindings.quickSlot = code; break;
            default: return;
        }
        _data.Save();
    }

    // 키보드 키만 후보로 사용 (마우스/조이스틱 버튼 제외)
    private static KeyCode[] GetRebindCandidates()
    {
        if (_rebindCandidates != null) return _rebindCandidates;

        var list = new List<KeyCode>();
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None) continue;
            string n = kc.ToString();
            if (n.StartsWith("Mouse") || n.StartsWith("Joystick")) continue;
            list.Add(kc);
        }
        _rebindCandidates = list.ToArray();
        return _rebindCandidates;
    }
}
