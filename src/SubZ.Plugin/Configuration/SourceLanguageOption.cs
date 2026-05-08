using System.ComponentModel;

namespace SubZ.Plugin.Configuration;

public enum SourceLanguageOption
{
    [Description("English")]
    En = 0,

    [Description("日本語")]
    Ja = 1,

    [Description("中文（简体）")]
    ZhHans = 2,

    [Description("中文（繁体）")]
    ZhHant = 3,

    [Description("한국어")]
    Ko = 4,

    [Description("Français")]
    Fr = 5,

    [Description("Deutsch")]
    De = 6,

    [Description("Español")]
    Es = 7,

    [Description("Português")]
    Pt = 8,

    [Description("Русский")]
    Ru = 9,

    [Description("Italiano")]
    It = 10,

    [Description("العربية")]
    Ar = 11,

    [Description("हिन्दी")]
    Hi = 12,

    [Description("ภาษาไทย")]
    Th = 13,

    [Description("Tiếng Việt")]
    Vi = 14,

    [Description("Bahasa Indonesia")]
    Id = 15,

    [Description("Türkçe")]
    Tr = 16,

    [Description("Malay")]
    Ms = 17
}

public static class SourceLanguageMap
{
    public static string ToCode(SourceLanguageOption option)
    {
        switch (option)
        {
            case SourceLanguageOption.En:
                return "en";
            case SourceLanguageOption.Ja:
                return "ja";
            case SourceLanguageOption.ZhHans:
                return "zh-CN";
            case SourceLanguageOption.ZhHant:
                return "zh-TW";
            case SourceLanguageOption.Ko:
                return "ko";
            case SourceLanguageOption.Fr:
                return "fr";
            case SourceLanguageOption.De:
                return "de";
            case SourceLanguageOption.Es:
                return "es";
            case SourceLanguageOption.Pt:
                return "pt";
            case SourceLanguageOption.Ru:
                return "ru";
            case SourceLanguageOption.It:
                return "it";
            case SourceLanguageOption.Ar:
                return "ar";
            case SourceLanguageOption.Hi:
                return "hi";
            case SourceLanguageOption.Th:
                return "th";
            case SourceLanguageOption.Vi:
                return "vi";
            case SourceLanguageOption.Id:
                return "id";
            case SourceLanguageOption.Tr:
                return "tr";
            case SourceLanguageOption.Ms:
                return "ms";
            default:
                return "en";
        }
    }

    public static SourceLanguageOption FromCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "ja":
                return SourceLanguageOption.Ja;
            case "zh-cn":
            case "zh-hans":
            case "zh":
                return SourceLanguageOption.ZhHans;
            case "zh-tw":
            case "zh-hant":
            case "zh-hk":
                return SourceLanguageOption.ZhHant;
            case "ko":
                return SourceLanguageOption.Ko;
            case "fr":
                return SourceLanguageOption.Fr;
            case "de":
                return SourceLanguageOption.De;
            case "es":
                return SourceLanguageOption.Es;
            case "pt":
                return SourceLanguageOption.Pt;
            case "ru":
                return SourceLanguageOption.Ru;
            case "it":
                return SourceLanguageOption.It;
            case "ar":
                return SourceLanguageOption.Ar;
            case "hi":
                return SourceLanguageOption.Hi;
            case "th":
                return SourceLanguageOption.Th;
            case "vi":
                return SourceLanguageOption.Vi;
            case "id":
                return SourceLanguageOption.Id;
            case "tr":
                return SourceLanguageOption.Tr;
            case "ms":
                return SourceLanguageOption.Ms;
            case "en":
            default:
                return SourceLanguageOption.En;
        }
    }
}
