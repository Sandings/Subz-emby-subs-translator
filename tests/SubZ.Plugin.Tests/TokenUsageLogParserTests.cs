using SubZ.Plugin.Services;

namespace SubZ.Plugin.Tests;

public sealed class TokenUsageLogParserTests
{
    [Fact]
    public void SummarizeAggregatesTokenUsageLinesAndKeepsLatestEntry()
    {
        var lines = new[]
        {
            "2026-05-12T05:20:44.9920815+00:00 [Info] Token usage | Prompt=20077, Completion=15817, Total=35894, Cues=340",
            "2026-05-12T06:20:44.9920815+00:00 [Info] Something else",
            "2026-05-12T07:20:44.9920815+00:00 [Info] Token usage | Prompt=10, Completion=20, Total=30, Cues=2"
        };

        var summary = TokenUsageLogParser.Summarize(lines);

        Assert.Equal(2, summary.TaskCount);
        Assert.Equal(20087, summary.PromptTokens);
        Assert.Equal(15837, summary.CompletionTokens);
        Assert.Equal(35924, summary.TotalTokens);
        Assert.Equal(342, summary.Cues);
        Assert.NotNull(summary.Latest);
        Assert.Equal(10, summary.Latest!.PromptTokens);
        Assert.Equal("2026-05-12T07:20:44.9920815+00:00", summary.Latest.Timestamp);
    }

    [Fact]
    public void ParseEntriesParsesFileNameWhenPresent()
    {
        var lines = new[]
        {
            "2026-05-12T06:58:58.4734527+00:00 [Info] Token usage | File=/mnt/MediaServer/Anime/test.mkv | Prompt=14663, Completion=11431, Total=26094, Cues=228"
        };

        var entries = TokenUsageLogParser.ParseEntries(lines, newestFirst: false);

        Assert.Single(entries);
        Assert.Equal("/mnt/MediaServer/Anime/test.mkv", entries[0].FileName);
        Assert.Equal(14663, entries[0].PromptTokens);
    }
}
