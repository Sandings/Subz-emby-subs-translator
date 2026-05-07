using System.ComponentModel;

namespace SubZ.Plugin.Configuration;

public enum OutputFormatOption
{
    [Description("srt")]
    Srt = 0,

    [Description("ass")]
    Ass = 1
}

public static class OutputFormatMap
{
    public static OutputFormatOption FromCode(string? code)
    {
        if (string.Equals(code, "ass", System.StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormatOption.Ass;
        }

        return OutputFormatOption.Srt;
    }

    public static string ToCode(OutputFormatOption option)
    {
        return option == OutputFormatOption.Ass ? "ass" : "srt";
    }
}
