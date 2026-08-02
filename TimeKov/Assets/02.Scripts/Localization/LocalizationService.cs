using System;
using System.Collections.Generic;

// Loc.Get("한글 원문") → 현재 언어 번역 반환, 없으면 한글 원문 그대로 반환 (KO fallback)
// 사용법:  tmp.text = Loc.Get("인벤토리");
public static class Loc
{
    public static LanguageCode CurrentLanguage { get; private set; } = LanguageCode.KO;

    public static event Action OnLanguageChanged;

    private static readonly Dictionary<LanguageCode, Dictionary<string, string>> _tables = new();

    public static string Get(string koreanKey)
    {
        if (string.IsNullOrEmpty(koreanKey)) return koreanKey;
        if (CurrentLanguage == LanguageCode.KO) return koreanKey;
        if (_tables.TryGetValue(CurrentLanguage, out var table) &&
            table.TryGetValue(koreanKey, out var translated) &&
            !string.IsNullOrEmpty(translated))
            return translated;
        return koreanKey;
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
