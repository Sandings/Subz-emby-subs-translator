using System.Reflection;
using SubZ.Plugin.Services;
using SubZ.Plugin.Configuration;

namespace SubZ.Plugin.Tests;

public class TranslationIntegrityTests
{
    private static List<string> Align(string raw, params string[] source)
    {
        var split = typeof(OpenAICompatibleTranslationClient).GetMethod("SplitOutput", BindingFlags.NonPublic | BindingFlags.Static)!;
        var lines = split.Invoke(null, new object[] {raw, "<<<SUBZ_SEP>>>", source.Length, source})!;
        var align = typeof(BatchSelfHealingTranslator).GetMethod("AlignOutputsByLineMarkers", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (List<string>)align.Invoke(null, new object[] {lines, source, 0})!;
    }

    [Theory]
    [InlineData("[[SUBZ_TAG_LINE_0000]] 你好\n<<SUBZ_TAG_LINE_0001]] 再见")]
    [InlineData("[[SUBZ_TAG_LINE_0000]] 你好\n<<<SUBZ_TAG_LINE_0001]] 再见")]
    [InlineData("[[SUBZ_TAG_LINE_0000]] 你好\n[[SUBZ_TAG_LINE_0001]] 再见")]
    [InlineData("[[SUBZ_TAG_LINE_0001]] 再见<<<SUBZ_SEP>>>[[SUBZ_TAG_LINE_0000]] 你好")]
    public void ParsesByIdsEvenWhenSeparatorsAreMissing(string raw)
        => Assert.Equal(new[] {"你好", "再见"}, Align(raw, "Hello", "Goodbye"));

    [Theory]
    [InlineData("unmarked text<<<SUBZ_SEP>>>[[SUBZ_TAG_LINE_0001]] 再见")]
    [InlineData("[[SUBZ_TAG_LINE_0000]] 你好<<<SUBZ_SEP>>>[[SUBZ_TAG_LINE_0000]] 重复<<<SUBZ_SEP>>>[[SUBZ_TAG_LINE_0001]] 再见")]
    public void NeverGuessesMissingOrDuplicateIds(string raw)
        => Assert.True(string.IsNullOrEmpty(Align(raw, "Hello", "Goodbye")[0]));

    [Fact]
    public void MissingIdAfterSeparatorInvalidatesAdjacentMergedText()
    {
        var lines = Align("[[SUBZ_TAG_LINE_0000]] 你好<<<SUBZ_SEP>>>无编号译文<<<SUBZ_SEP>>>[[SUBZ_TAG_LINE_0002]] 谢谢", "Hello", "Goodbye", "Thanks");
        Assert.Equal(new[]{"", "", "谢谢"}, lines);
    }

    [Fact]
    public void RejectsOutOfRangeIds()
        => Assert.All(Align("[[SUBZ_TAG_LINE_0000]] 你好[[SUBZ_TAG_LINE_0099]] 多余", "Hello"), x => Assert.True(string.IsNullOrEmpty(x)));

    [Theory]
    [InlineData("[[SUBZ_TAG_LINE_0000]] Hello")]
    [InlineData("[[SUBZ_TAG_LINE_0000]] 你好 SUBZ_BROKEN")]
    public async Task InvalidTranslationFailsInsteadOfReturningSuccess(string raw)
    {
        var translator = new BatchSelfHealingTranslator(new FakeClient(raw), new SubtitleTagProtector(), new TranslationProfileResolver());
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync(new[]{"Hello"}, new PluginOptions { EnableTailRetry = false }, CancellationToken.None));
    }

    [Fact]
    public async Task ExhaustedRetriesFail()
    {
        var translator = new BatchSelfHealingTranslator(new FakeClient("[[SUBZ_TAG_LINE_0000]] Hello"), new SubtitleTagProtector(), new TranslationProfileResolver());
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync(new[]{"Hello"}, new PluginOptions { EnableTailRetry = true, TailRetryMaxAttempts = 1 }, CancellationToken.None));
    }

    [Fact]
    public void BilingualComposerDoesNotIntroduceLiteralSlashN()
    {
        var cues = BilingualSubtitleComposer.BuildBilingualCues(new[]{new SubtitleCue {Text="Hello\nthere"}}, new[]{"你好\n朋友"});
        Assert.Equal("Hello there\n你好 朋友", cues[0].Text);
    }

    [Fact]
    public async Task RepairsOnlyMissingIdWithoutShiftingNeighbors()
    {
        var client = new RepairClient();
        var translator = new BatchSelfHealingTranslator(client, new SubtitleTagProtector(), new TranslationProfileResolver());
        var result = await translator.TranslateAsync(new[]{"Hello", "Goodbye", "Thanks"}, new PluginOptions { EnableTailRetry = true, TailRetryMaxAttempts = 2 }, CancellationToken.None);
        Assert.Equal(new[]{"你好", "再见", "谢谢"}, result.OutputLines);
        Assert.Equal(2, client.Calls);
    }

    private sealed class RepairClient : ISubtitleTranslationClient
    {
        public int Calls;
        public Task<TranslationBatchResponse> TranslateBatchAsync(TranslationBatchRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1) return Task.FromResult(new TranslationBatchResponse {OutputLines = new[]{"[[SUBZ_TAG_LINE_0000]] 你好\n[[SUBZ_TAG_LINE_0002]] 谢谢"}});
            Assert.Equal(new[]{"[[SUBZ_TAG_LINE_0000]] Goodbye"}, request.Inputs);
            return Task.FromResult(new TranslationBatchResponse {OutputLines = new[]{"[[SUBZ_TAG_LINE_0000]] 再见"}});
        }
    }

    [Theory]
    [InlineData("DJPhil")]
    [InlineData("player_42")]
    [InlineData("https://example.com")]
    [InlineData("2024")]
    public async Task AllowsUnchangedStandaloneIdentifiers(string source)
    {
        var translator = new BatchSelfHealingTranslator(new FakeClient("[[SUBZ_TAG_LINE_0000]] " + source), new SubtitleTagProtector(), new TranslationProfileResolver());
        var result = await translator.TranslateAsync(new[]{source}, new PluginOptions { EnableTailRetry = false }, CancellationToken.None);
        Assert.Equal(source, Assert.Single(result.OutputLines));
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("STOP")]
    [InlineData("I don't think so.")]
    public async Task OrdinaryUnchangedDialogueStillFails(string source)
    {
        var translator = new BatchSelfHealingTranslator(new FakeClient("[[SUBZ_TAG_LINE_0000]] " + source), new SubtitleTagProtector(), new TranslationProfileResolver());
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync(new[]{source}, new PluginOptions { EnableTailRetry = false }, CancellationToken.None));
    }

    private sealed class FakeClient(string raw) : ISubtitleTranslationClient
    {
        public Task<TranslationBatchResponse> TranslateBatchAsync(TranslationBatchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new TranslationBatchResponse {OutputLines = new[]{raw}});
    }
}
