namespace SubZ.Plugin.Configuration;

public static class LanguageCodeMap
{
    public static string ToCode(SupportedLanguageOption option)
    {
        switch (option)
        {
            case SupportedLanguageOption.ZhHans:
                return "zh-CN";
            case SupportedLanguageOption.ZhHant:
                return "zh-TW";
            case SupportedLanguageOption.En:
                return "en";
            case SupportedLanguageOption.Ja:
                return "ja";
            case SupportedLanguageOption.Ko:
                return "ko";
            case SupportedLanguageOption.Fr:
                return "fr";
            case SupportedLanguageOption.De:
                return "de";
            case SupportedLanguageOption.Es:
                return "es";
            case SupportedLanguageOption.Pt:
                return "pt";
            case SupportedLanguageOption.Ru:
                return "ru";
            case SupportedLanguageOption.It:
                return "it";
            case SupportedLanguageOption.Ar:
                return "ar";
            case SupportedLanguageOption.Hi:
                return "hi";
            case SupportedLanguageOption.Th:
                return "th";
            case SupportedLanguageOption.Vi:
                return "vi";
            case SupportedLanguageOption.Id:
                return "id";
            case SupportedLanguageOption.Tr:
                return "tr";
            case SupportedLanguageOption.Ms:
                return "ms";
            default:
                return "zh-CN";
        }
    }

    public static SupportedLanguageOption FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return SupportedLanguageOption.ZhHans;
        }

        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "zh-cn":
            case "zh-hans":
            case "zh":
                return SupportedLanguageOption.ZhHans;
            case "zh-tw":
            case "zh-hant":
            case "zh-hk":
                return SupportedLanguageOption.ZhHant;
            case "en":
                return SupportedLanguageOption.En;
            case "ja":
                return SupportedLanguageOption.Ja;
            case "ko":
                return SupportedLanguageOption.Ko;
            case "fr":
                return SupportedLanguageOption.Fr;
            case "de":
                return SupportedLanguageOption.De;
            case "es":
                return SupportedLanguageOption.Es;
            case "pt":
                return SupportedLanguageOption.Pt;
            case "ru":
                return SupportedLanguageOption.Ru;
            case "it":
                return SupportedLanguageOption.It;
            case "ar":
                return SupportedLanguageOption.Ar;
            case "hi":
                return SupportedLanguageOption.Hi;
            case "th":
                return SupportedLanguageOption.Th;
            case "vi":
                return SupportedLanguageOption.Vi;
            case "id":
                return SupportedLanguageOption.Id;
            case "tr":
                return SupportedLanguageOption.Tr;
            case "ms":
                return SupportedLanguageOption.Ms;
            default:
                return SupportedLanguageOption.ZhHans;
        }
    }
}
