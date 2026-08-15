// SettingsBinding.cs
// ============================================================================
// 설정 항목 식별자(SettingId) + 실제 읽기/쓰기의 단일 창구.
//
// 왜 이 계층이 필요한가:
//   씬에 미리 배치된 위젯은 람다(Func/Action)를 직렬화할 수 없다. 그래서 위젯은
//   "내가 담당하는 항목"을 SettingId 하나로만 들고, 값 접근은 전부 여기로 온다.
//   덤으로 GlobalSettingsManager와의 결합이 이 파일 하나로 모인다.
//
// 값의 출처는 언제나 GlobalSettingsManager._pending 이다. 이 UI는 값을 갖지 않는다.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace GameSettingsUI
{
    // 버튼이 하는 일. 클릭 콜백(람다)은 직렬화가 안 되므로, 씬에 배치된 버튼은
    // 이 열거형만 들고 있고 실제 처리는 패널 컨트롤러가 받아서 한다.
    public enum SettingsAction
    {
        None = 0,
        TabDisplay, TabAudio, TabControls,   // 상단 탭 3개
        Close,                                // 우상단 X
        Apply, ResetAll, MainMenu,            // 하단 버튼
        ToggleDropdown,                       // 드롭다운 열기/닫기 (param 미사용)
        SelectOption,                         // 드롭다운 옵션 선택 (param = 옵션 인덱스)
        FullscreenOn, FullscreenOff,          // 표시 모드 세그먼트
        ToggleMute,                           // 오디오 음소거 아이콘
        StartRebind,                          // 키 바인딩 버튼 (param = 액션 인덱스)
        WarnApplyClose, WarnDiscardClose, WarnCancel,   // 미적용 변경 경고창의 3버튼
    }

    public enum SettingId
    {
        None = 0,
        // 드롭다운
        Language, ScreenQuality, Resolution, ShadowQuality, TextureQuality,
        // 2-세그먼트 토글
        Fullscreen,
        // 슬라이더
        MasterVolume, BgmVolume, SfxVolume, Sensitivity,
    }

    public static class SettingsBinding
    {
        static GlobalSettingsManager M => GlobalSettingsManager.Instance;
        static SettingsData P => GlobalSettingsManager.Instance.PendingData;

        public static bool Ready => GlobalSettingsManager.Instance != null;

        /// 저장된 해상도가 선택지에 없으면 목록 안의 값으로 맞춘다(첫 표시 전 1회).
        public static void NormalizeResolution() => M.NormalizeResolution();

        // 볼륨만 모델(0~1)과 화면 표시(0~100)의 단위가 다르다.
        const float VolumeDisplayScale = 100f;

        // ── 드롭다운 ────────────────────────────────────────────────
        // 옵션 목록은 전부 매니저가 갖고 있는 것을 그대로 쓴다. UI가 따로 들고 있으면
        // 라벨/인덱스가 시스템과 어긋난다(화면 품질은 3단계, 그림자·텍스처는 내림차순).

        static string[] _resolutionLabels;
        static string[] ResolutionLabels
        {
            get
            {
                if (_resolutionLabels != null) return _resolutionLabels;
                var res = GlobalSettingsManager.ResolutionOptions;
                _resolutionLabels = new string[res.Count];
                for (int i = 0; i < res.Count; i++)
                    _resolutionLabels[i] = $"{res[i].width} x {res[i].height}";
                return _resolutionLabels;
            }
        }

        // 드롭다운 표시용: 번역된 레이블 반환 (KO이면 원문 그대로)
        public static string[] Options(SettingId id)
        {
            switch (id)
            {
                case SettingId.Language:       return Localize(GlobalSettingsManager.LanguageOptions);
                case SettingId.ScreenQuality:  return Localize(GlobalSettingsManager.QualityOptions);
                case SettingId.ShadowQuality:  return Localize(GlobalSettingsManager.ShadowQualityOptions);
                case SettingId.TextureQuality: return Localize(GlobalSettingsManager.TextureQualityOptions);
                case SettingId.Resolution:     return ResolutionLabels;
                default:                       return new string[0];
            }
        }

        // 현재 선택값을 번역된 레이블로 반환
        public static string GetLabel(SettingId id)
        {
            switch (id)
            {
                case SettingId.Language:       return Loc.Get(At(GlobalSettingsManager.LanguageOptions, (int)Loc.FromCode(P.language)));
                case SettingId.ScreenQuality:  return Loc.Get(At(GlobalSettingsManager.QualityOptions, P.qualityLevel));
                case SettingId.ShadowQuality:  return Loc.Get(At(GlobalSettingsManager.ShadowQualityOptions, P.shadowQualityLevel));
                case SettingId.TextureQuality: return Loc.Get(At(GlobalSettingsManager.TextureQualityOptions, P.textureQualityLevel));
                case SettingId.Resolution:     return $"{P.resolutionWidth} x {P.resolutionHeight}";
                default:                       return string.Empty;
            }
        }

        // 드롭다운에서 선택된 번역 레이블 → 원본 인덱스로 역변환 후 저장
        public static void SetLabel(SettingId id, string label)
        {
            int i = IndexOfLocalized(id, label);
            switch (id)
            {
                case SettingId.Language:       M.SetLanguage((LanguageCode)i);  break;
                case SettingId.ScreenQuality:  M.SetQualityLevel(i);            break;
                case SettingId.ShadowQuality:  M.SetShadowQuality(i);           break;
                case SettingId.TextureQuality: M.SetTextureQuality(i);          break;
                case SettingId.Resolution:
                    var res = GlobalSettingsManager.ResolutionOptions;
                    if (i >= 0 && i < res.Count) M.SetResolution(res[i].width, res[i].height);
                    break;
            }
        }

        // 번역 레이블 → 원본 배열에서 인덱스 검색 (번역 중 일치하는 것 우선)
        static int IndexOfLocalized(SettingId id, string translatedLabel)
        {
            IReadOnlyList<string> raw;
            switch (id)
            {
                case SettingId.Language:       raw = GlobalSettingsManager.LanguageOptions;       break;
                case SettingId.ScreenQuality:  raw = GlobalSettingsManager.QualityOptions;        break;
                case SettingId.ShadowQuality:  raw = GlobalSettingsManager.ShadowQualityOptions;  break;
                case SettingId.TextureQuality: raw = GlobalSettingsManager.TextureQualityOptions; break;
                default: return 0;
            }
            for (int i = 0; i < raw.Count; i++)
                if (Loc.Get(raw[i]) == translatedLabel || raw[i] == translatedLabel) return i;
            return 0;
        }

        // ── 슬라이더 ────────────────────────────────────────────────

        public static SliderMeta Meta(SettingId id) =>
            id == SettingId.Sensitivity ? SliderRanges.Sens : SliderRanges.Volume;

        public static float GetValue(SettingId id)
        {
            switch (id)
            {
                case SettingId.MasterVolume: return P.masterVolume * VolumeDisplayScale;
                case SettingId.BgmVolume:    return P.bgmVolume    * VolumeDisplayScale;
                case SettingId.SfxVolume:    return P.sfxVolume    * VolumeDisplayScale;
                case SettingId.Sensitivity:  return P.sensitivity;
                default:                     return 0f;
            }
        }

        public static void SetValue(SettingId id, float v)
        {
            switch (id)
            {
                case SettingId.MasterVolume: M.SetMasterVolume(v / VolumeDisplayScale); break;
                case SettingId.BgmVolume:    M.SetBGMVolume   (v / VolumeDisplayScale); break;
                case SettingId.SfxVolume:    M.SetSFXVolume   (v / VolumeDisplayScale); break;
                case SettingId.Sensitivity:  M.SetSensitivity (v);                      break;
            }
        }

        // ── 표시 모드 ───────────────────────────────────────────────

        public static bool GetFullscreen()          => P.fullscreen;
        public static void SetFullscreen(bool on)   => M.SetFullscreen(on);

        // ── 키 바인딩 ───────────────────────────────────────────────
        // 액션 목록·충돌 검사 모두 매니저 규칙을 그대로 쓴다.

        public static int    ActionCount            => GlobalSettingsManager.RebindActions.Length;
        public static string ActionLabel(int index) => GlobalSettingsManager.RebindActions[index].label;
        public static string ActionId(int index)    => GlobalSettingsManager.RebindActions[index].id;
        public static string KeyLabel(int index)    => Format(M.GetPendingKey(ActionId(index)));

        public static bool TryRebind(int index, KeyCode code, out string conflictAction) =>
            M.TryRebind(ActionId(index), code, out conflictAction);

        /// 중복이어도 일단 배치한다(전역 예약 키만 거부). 겹치는 건 화면에서 빨갛게 알리고
        /// '설정 적용' 단계에서 막는다 — 눌러도 원래 키로 되돌아가는 것보다 이유가 분명하다.
        public static bool Rebind(int index, KeyCode code, out string reservedBy) =>
            M.SetPendingKey(ActionId(index), code, out reservedBy);

        /// 액션별 중복 여부. ActionCount 와 같은 순서·길이.
        public static bool[] KeyConflicts() => M.GetKeyConflicts();

        /// 겹치는 키가 하나라도 있는가.
        public static bool HasKeyConflict => M != null && M.HasKeyConflict();

        public static string Format(KeyCode kc)
        {
            if (kc == KeyCode.Escape) return "Esc";
            string s = kc.ToString();
            return s.Length == 1 ? s.ToUpper() : s;
        }

        // ── 하단 버튼 ───────────────────────────────────────────────

        public static void Apply()          => M.ApplySettings();      // 커밋 + 저장 + 엔진 반영
        public static void ResetAll()       => M.ResetAllToDefault();  // 폼만 기본값 (적용은 별도)
        public static void QuitToMainMenu() => M.QuitToMainMenu();

        // ── 사운드 ──────────────────────────────────────────────────
        // 하단 버튼/표시모드 전환음은 매니저 메서드(ApplySettings 등) 안에서 이미 울린다.
        // 여기 있는 건 구 UI 위젯에 리스너로 붙어 있던 것들 — 위젯이 바뀌며 사라져서 새로 건다.
        public static void PlayClick()    => GameSfx.Play(SfxId.SettingsClick);
        public static void PlayTabClick() => GameSfx.Play(SfxId.SettingsTabClick);
        public static void PlayVolumePreview() => GameSfx.Play(SfxId.UIVolumePreview);

        /// 아직 적용하지 않은 변경사항이 있는가 (닫기 전 경고용)
        public static bool HasUnappliedChanges => M != null && M.HasUnappliedChanges;
        /// 편집 중이던 값을 버리고 마지막 적용 상태로 되돌린다
        public static void DiscardChanges() => M.RefreshOnOpen();

        // ── 유틸 ────────────────────────────────────────────────────

        static string[] ToArray(IReadOnlyList<string> src)
        {
            var a = new string[src.Count];
            for (int i = 0; i < src.Count; i++) a[i] = src[i];
            return a;
        }
        static string[] Localize(IReadOnlyList<string> src)
        {
            var a = new string[src.Count];
            for (int i = 0; i < src.Count; i++) a[i] = Loc.Get(src[i]);
            return a;
        }
        static int IndexOf(IReadOnlyList<string> src, string v)
        {
            for (int i = 0; i < src.Count; i++) if (src[i] == v) return i;
            return 0;
        }
        static string At(IReadOnlyList<string> src, int i) =>
            src.Count == 0 ? string.Empty : src[Mathf.Clamp(i, 0, src.Count - 1)];
    }
}
