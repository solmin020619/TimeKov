using UnityEngine;

public class SystemLanguageProvider : ILocalizationProvider
{
    public LanguageCode Detect()
    {
        return Application.systemLanguage switch
        {
            SystemLanguage.Korean                => LanguageCode.KO,
            SystemLanguage.ChineseSimplified     => LanguageCode.CN,
            SystemLanguage.ChineseTraditional    => LanguageCode.CN,
            SystemLanguage.French                => LanguageCode.FR,
            _                                    => LanguageCode.EN,
        };
    }
}
