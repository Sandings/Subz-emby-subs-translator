using System.ComponentModel;

namespace SubZ.Plugin.Configuration;

public enum SupportedLanguageOption
{
    [Description("中文（简体）")]
    ZhHans = 0,

    [Description("中文（繁体）")]
    ZhHant = 1,

    [Description("English")]
    En = 2,

    [Description("日本語")]
    Ja = 3,

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
