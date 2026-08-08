using System;
using System.Collections.Generic;
using UnityEngine;

// Loc.Get("한글 원문") → 현재 언어 번역 반환, 없으면 한글 원문 그대로 반환 (KO fallback)
// 사용법:  tmp.text = Loc.Get("인벤토리");
public static class Loc
{
    public static LanguageCode CurrentLanguage { get; private set; } = LanguageCode.KO;

    public static event Action OnLanguageChanged;

    // ★저장된 언어를 '씬이 열리기 전에' 복원한다.
    //   이게 없으면 순서가 이렇게 된다:
    //     로딩 씬에서 번역 테이블 수신(이때 언어는 아직 기본값 KO)
    //     -> 월드 씬 UI 가 전부 Awake/Start 에서 Loc.Get 호출 -> 한국어로 그려짐
    //     -> 그 뒤에야 GlobalSettingsmanager.Start() 가 저장된 언어를 적용
    //   그래서 언어 변경 이벤트를 재구독하지 않는 UI(한 번 쓰고 마는 라벨)는 전부
    //   한국어로 굳었다. 여기서 미리 정해두면 UI 가 처음부터 올바른 언어로 그려진다.
    //   설정창의 SetLanguage 는 같은 값이면 조용히 빠져나가므로 중복 적용도 없다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreSavedLanguage()
    {
        try { CurrentLanguage = FromCode(SettingsData.Load().language); }
        catch (Exception e) { Debug.LogWarning($"[Loc] 저장된 언어 복원 실패, 한국어로 진행: {e.Message}"); }

        // 직전 실행에서 받아둔 번역표를 먼저 깐다. 시트 다운로드는 로딩 씬에서야 끝나는데
        // 그 전에 뜨는 화면(로딩 문구)까지 번역하려면 이 시점에 표가 있어야 한다.
        // 네트워크 결과가 도착하면 최신 값으로 덮어쓴다.
        if (CurrentLanguage != LanguageCode.KO) LocalizationLoader.LoadFromCache();
    }

    private static readonly Dictionary<LanguageCode, Dictionary<string, string>> _tables = new();

    public static string Get(string koreanKey)
    {
        if (string.IsNullOrEmpty(koreanKey)) return koreanKey;
        if (CurrentLanguage == LanguageCode.KO) return koreanKey;
        string normalizedKey = koreanKey.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        if (_tables.TryGetValue(CurrentLanguage, out var table) &&
            table.TryGetValue(normalizedKey, out var translated) &&
            !string.IsNullOrEmpty(translated))
            return translated;
        return koreanKey;
    }

    /// <summary>이 문구가 번역 테이블의 키인가. 씬에 구운 '정적 라벨'과 런타임 갱신값
    /// ("12 / 35" 같은 것)을 가르는 데 쓴다 - 후자는 시트에 있을 리가 없다.</summary>
    public static bool HasKey(string koreanKey)
    {
        if (string.IsNullOrEmpty(koreanKey)) return false;
        string n = koreanKey.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        foreach (var t in _tables.Values) if (t.ContainsKey(n)) return true;
        return false;
    }

    public static void SetLanguage(LanguageCode code)
    {
        if (CurrentLanguage == code) return;
        CurrentLanguage = code;
        OnLanguageChanged?.Invoke();
    }

    public static void LoadTable(LanguageCode code, Dictionary<string, string> table)
    {
        _tables[code] = table;
        if (code == CurrentLanguage)
            OnLanguageChanged?.Invoke();
    }

    public static void ClearTables()
    {
        _tables.Clear();
    }

    // LanguageCode ↔ string 변환 (SettingsData.language JSON 필드용)
    public static string ToCode(LanguageCode lang) => lang.ToString();

    public static LanguageCode FromCode(string code)
    {
        if (Enum.TryParse<LanguageCode>(code, out var result)) return result;
        return LanguageCode.KO;
    }
}
