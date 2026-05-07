using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SubZ.Plugin.Services;

public sealed class SubtitleTagProtector
{
    private static readonly Regex HtmlTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex AssTagRegex = new Regex("\\{[^}]+\\}", RegexOptions.Compiled);
    private const string LineBreakToken = "[[SUBZ_LB]]";

    public ProtectedSubtitleText Protect(string input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var tokenMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;

        string protectedText = input;

        protectedText = HtmlTagRegex.Replace(protectedText, m => AddToken(tokenMap, m.Value, ref index));
        protectedText = AssTagRegex.Replace(protectedText, m => AddToken(tokenMap, m.Value, ref index));

        // Keep explicit subtitle line-break semantics stable through translation.
        protectedText = protectedText.Replace("\\N", LineBreakToken);
        protectedText = protectedText.Replace("\r\n", "\n");

        return new ProtectedSubtitleText(protectedText, tokenMap);
    }

    public string Restore(string translated, IReadOnlyDictionary<string, string> tokenMap)
    {
        if (translated is null)
        {
            return string.Empty;
        }

        string restored = translated;
        foreach (var kv in tokenMap)
        {
            restored = restored.Replace(kv.Key, kv.Value);
        }

        restored = restored.Replace(LineBreakToken, "\\N");
        return restored;
    }

    private static string AddToken(IDictionary<string, string> tokenMap, string original, ref int index)
    {
        var token = $"[[SUBZ_TAG_{index++:D3}]]";
        tokenMap[token] = original;
        return token;
    }
}

public sealed class ProtectedSubtitleText
{
    public ProtectedSubtitleText(string text, IReadOnlyDictionary<string, string> tokenMap)
    {
        Text = text;
        TokenMap = tokenMap;
    }

    public string Text { get; }
    public IReadOnlyDictionary<string, string> TokenMap { get; }
}
