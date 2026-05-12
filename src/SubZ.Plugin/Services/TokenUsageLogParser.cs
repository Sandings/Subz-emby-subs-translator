using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SubZ.Plugin.Services;

public sealed class TokenUsageEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public long Cues { get; set; }
}

public sealed class TokenUsageSummary
{
    public int TaskCount { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public long Cues { get; set; }
    public TokenUsageEntry? Latest { get; set; }
}

public static class TokenUsageLogParser
{
    private static readonly Regex TokenUsageRegex = new Regex(
        @"^(?<timestamp>\S+).*Token usage \|(?: File=(?<file>.*) \|)? Prompt=(?<prompt>\d+), Completion=(?<completion>\d+), Total=(?<total>\d+), Cues=(?<cues>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TokenUsageEntry> ParseEntries(IEnumerable<string> lines, bool newestFirst)
    {
        if (lines == null)
        {
            return Array.Empty<TokenUsageEntry>();
        }

        var entries = new List<TokenUsageEntry>();
        foreach (var line in lines)
        {
            if (TryParse(line, out var entry))
            {
                entries.Add(entry);
            }
        }

        if (newestFirst)
        {
            entries.Reverse();
        }

        return entries;
    }

    public static TokenUsageSummary Summarize(IEnumerable<string> lines)
    {
        return Summarize(ParseEntries(lines, newestFirst: false));
    }

    public static TokenUsageSummary Summarize(IEnumerable<TokenUsageEntry> entries)
    {
        var summary = new TokenUsageSummary();
        if (entries == null)
        {
            return summary;
        }

        foreach (var entry in entries)
        {
            summary.TaskCount++;
            summary.PromptTokens += entry.PromptTokens;
            summary.CompletionTokens += entry.CompletionTokens;
            summary.TotalTokens += entry.TotalTokens;
            summary.Cues += entry.Cues;
            summary.Latest = entry;
        }

        return summary;
    }

    public static IReadOnlyList<TokenUsageEntry> FilterByDays(IEnumerable<TokenUsageEntry> entries, int tokenDaysUtc)
    {
        var list = entries?.ToList() ?? new List<TokenUsageEntry>();
        if (tokenDaysUtc <= 0)
        {
            return list;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-tokenDaysUtc);
        return list
            .Where(entry => TryParseTimestamp(entry.Timestamp, out var ts) && ts >= cutoff)
            .ToArray();
    }

    public static bool TryParseTimestamp(string? timestamp, out DateTimeOffset value)
    {
        return DateTimeOffset.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out value);
    }

    private static bool TryParse(string? line, out TokenUsageEntry entry)
    {
        entry = new TokenUsageEntry();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = TokenUsageRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        entry.Timestamp = match.Groups["timestamp"].Value;
        entry.FileName = (match.Groups["file"].Value ?? string.Empty).Trim();
        entry.PromptTokens = ParseInt64(match.Groups["prompt"].Value);
        entry.CompletionTokens = ParseInt64(match.Groups["completion"].Value);
        entry.TotalTokens = ParseInt64(match.Groups["total"].Value);
        entry.Cues = ParseInt64(match.Groups["cues"].Value);
        return true;
    }

    private static long ParseInt64(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}
