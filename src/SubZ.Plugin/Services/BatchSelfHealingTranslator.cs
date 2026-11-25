using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SubZ.Plugin.Configuration;

namespace SubZ.Plugin.Services;

public interface ISubtitleTranslationClient
{
    Task<TranslationBatchResponse> TranslateBatchAsync(TranslationBatchRequest request, CancellationToken cancellationToken);
}

public enum TranslationPromptMode
{
    Default = 0,
    RetryUnchanged = 1
}

public sealed class TranslationBatchRequest
{
    public TranslationApiProfile Profile { get; set; } = new TranslationApiProfile();
    public string TargetLanguage { get; set; } = "zh-CN";
    public IReadOnlyList<string> Inputs { get; set; } = Array.Empty<string>();
    public bool EnableDebugLog { get; set; }
    public TranslationPromptMode PromptMode { get; set; } = TranslationPromptMode.Default;
    public string RetryReasonHint { get; set; } = string.Empty;
}

public sealed class TranslationBatchResponse
{
    public IReadOnlyList<string> OutputLines { get; set; } = Array.Empty<string>();
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public sealed class BatchSelfHealingTranslationResult
{
    public List<string> OutputLines { get; } = new List<string>();
    public int BatchCount { get; set; }
    public int TailRetryCount { get; set; }
    public List<string> Warnings { get; } = new List<string>();
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
}

public sealed class BatchSelfHealingTranslator
{
    private static readonly Regex LineIdRegex = new Regex(@"(?:\[\[|<{2,3})SUBZ_TAG_LINE_(\d{4,})\]\]", RegexOptions.Compiled);

    private readonly ISubtitleTranslationClient _client;
    private readonly SubtitleTagProtector _tagProtector;
    private readonly TranslationProfileResolver _profileResolver;

    public BatchSelfHealingTranslator(
        ISubtitleTranslationClient client,
        SubtitleTagProtector tagProtector,
        TranslationProfileResolver profileResolver)
    {
        _client = client;
        _tagProtector = tagProtector;
        _profileResolver = profileResolver;
    }

    public async Task<BatchSelfHealingTranslationResult> TranslateAsync(
        IReadOnlyList<string> sourceLines,
        PluginOptions options,
        CancellationToken cancellationToken,
        IReadOnlyList<SubtitleCue>? sourceCues = null)
    {
        if (sourceLines is null)
        {
            throw new ArgumentNullException(nameof(sourceLines));
        }

        var result = new BatchSelfHealingTranslationResult();
        if (sourceLines.Count == 0)
        {
            return result;
        }

        var profile = _profileResolver.ResolveActiveProfile(options);
        var retryPolicy = new TranslationRetryPolicy(profile.RetryCount);
        var batchSize = Math.Max(1, profile.BatchSize);
        var maxParallel = Math.Max(1, profile.ParallelRequests);
        var batches = Split(sourceLines, batchSize);
        result.BatchCount = batches.Count;

        var outputs = new string[sourceLines.Count];
        var sharedLock = new object();
        var nextBatchIndex = -1;
        var workers = new List<Task>(Math.Min(maxParallel, batches.Count));

        for (var workerIndex = 0; workerIndex < maxParallel; workerIndex++)
        {
            workers.Add(Task.Run(async () =>
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batchIndex = Interlocked.Increment(ref nextBatchIndex);
                    if (batchIndex >= batches.Count)
                    {
                        break;
                    }

                    var batch = batches[batchIndex];
                    InMemoryTranslationJobDispatcher.AppendRuntimeLog(
                        "Info",
                        $"Batch {batchIndex + 1}/{batches.Count}: size={batch.Count}");

                    var startLine = batchIndex * batchSize;
                    var work = await TranslateOneBatchAsync(
                        batch,
                        batchIndex,
                        startLine,
                        sourceCues,
                        options,
                        profile,
                        retryPolicy,
                        result,
                        cancellationToken).ConfigureAwait(false);

                    for (var i = 0; i < work.RestoredLines.Count; i++)
                    {
                        var targetIndex = startLine + i;
                        if (targetIndex < outputs.Length)
                        {
                            outputs[targetIndex] = work.RestoredLines[i];
                        }
                    }

                    lock (sharedLock)
                    {
                        result.PromptTokens += work.PromptTokens;
                        result.CompletionTokens += work.CompletionTokens;
                        result.TotalTokens += work.TotalTokens;
                        result.TailRetryCount += work.TailRetryCount;
                        if (work.Warnings.Count > 0)
                        {
                            result.Warnings.AddRange(work.Warnings);
                        }
                    }
                }
            }, cancellationToken));
        }

        await Task.WhenAll(workers).ConfigureAwait(false);

        for (var i = 0; i < outputs.Length; i++)
        {
            result.OutputLines.Add(outputs[i] ?? string.Empty);
        }

        return result;
    }

    private async Task<BatchWorkResult> TranslateOneBatchAsync(
        IReadOnlyList<string> batch,
        int batchIndex,
        int batchStartGlobalIndex,
        IReadOnlyList<SubtitleCue>? sourceCues,
        PluginOptions options,
        TranslationApiProfile profile,
        TranslationRetryPolicy retryPolicy,
        BatchSelfHealingTranslationResult totalResult,
        CancellationToken cancellationToken)
    {
        var protectedBatch = new List<ProtectedSubtitleText>(batch.Count);
        var payload = new List<string>(batch.Count);

        foreach (var line in batch)
        {
            if (options.PreserveSubtitleTags)
            {
                var protectedItem = _tagProtector.Protect(line ?? string.Empty);
                protectedBatch.Add(protectedItem);
                payload.Add(protectedItem.Text);
            }
            else
            {
                var raw = line ?? string.Empty;
                protectedBatch.Add(new ProtectedSubtitleText(raw, new Dictionary<string, string>()));
                payload.Add(raw);
            }
        }

        var translatedResponse = await retryPolicy.ExecuteAsync(
            ct => _client.TranslateBatchAsync(new TranslationBatchRequest
            {
                Profile = profile,
                TargetLanguage = options.GetTargetLanguageCode(),
                Inputs = AddLineMarkers(payload),
                EnableDebugLog = options.EnableDebugLog,
                PromptMode = TranslationPromptMode.Default
            }, ct),
            IsRetryable,
            cancellationToken).ConfigureAwait(false);

        var work = new BatchWorkResult();
        AddUsage(work, translatedResponse);
        var alignedOutput = AlignOutputsByLineMarkers(translatedResponse.OutputLines, payload, out var structuralMismatchCount);
        if (structuralMismatchCount > 0)
        {
            work.Warnings.Add($"Detected {structuralMismatchCount} subtitle lines with marker mismatch in a batch; auto-healing is applied.");
            InMemoryTranslationJobDispatcher.AppendRuntimeLog("Warn", $"Marker mismatch detected in batch lines={structuralMismatchCount}");
        }

        var finalized = await HealFailedSegmentsIfNeededAsync(
            profile,
            batch,
            batchIndex,
            batchStartGlobalIndex,
            sourceCues,
            payload,
            alignedOutput,
            options,
            totalResult,
            retryPolicy,
            cancellationToken,
            work).ConfigureAwait(false);

        for (var i = 0; i < batch.Count; i++)
        {
            var safeText = i < finalized.Count ? finalized[i] : (payload[i] ?? string.Empty);
            var restored = _tagProtector.Restore(safeText, protectedBatch[i].TokenMap);
            if (restored.IndexOf("SUBZ_", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Unresolved subtitle markers; no subtitle was written.");
            work.RestoredLines.Add(restored);
        }

        return work;
    }

    private async Task<IReadOnlyList<string>> HealFailedSegmentsIfNeededAsync(
        TranslationApiProfile profile,
        IReadOnlyList<string> sourceBatch,
        int batchIndex,
        int batchStartGlobalIndex,
        IReadOnlyList<SubtitleCue>? sourceCues,
        IReadOnlyList<string> payload,
        IReadOnlyList<string> translated,
        PluginOptions options,
        BatchSelfHealingTranslationResult totalResult,
        TranslationRetryPolicy retryPolicy,
        CancellationToken cancellationToken,
        BatchWorkResult work)
    {
        var merged = NormalizeLength(translated, payload);
        var failed = FindFailedItems(sourceBatch, merged, payload);

        if (failed.Count == 0)
        {
            return merged;
        }

        if (!options.EnableTailRetry)
        {
            throw new InvalidOperationException($"Translation incomplete: {failed.Count} invalid lines in batch {batchIndex + 1}; retry is disabled. No subtitle was written.");
        }

        var attempts = Math.Max(0, options.TailRetryMaxAttempts);
        for (var attempt = 1; attempt <= attempts && failed.Count > 0; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            work.TailRetryCount++;
            var reasonSummary = SummarizeFailedReasons(failed);
            var failedReasonByIndex = failed.ToDictionary(static x => x.Index, static x => x.Reason);
            InMemoryTranslationJobDispatcher.AppendRuntimeLog(
                "Warn",
                $"Healing failed segments: batch={batchIndex + 1}, attempt {attempt}/{attempts}, failed lines={failed.Count}, reasons={reasonSummary}");

            // Smaller repair batches prevent a malformed long answer from repeating wholesale.
            var repairSize = Math.Max(1, 16 / attempt);
            var ranges = BuildFailedRanges(failed.Select(static f => f.Index).ToArray())
                .SelectMany(r => Enumerable.Range(0, (r.Count + repairSize - 1) / repairSize)
                    .Select(n => (Start: r.Start + n * repairSize, Count: Math.Min(repairSize, r.Count - n * repairSize))));
            foreach (var range in ranges)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var globalStart = batchStartGlobalIndex + range.Start;
                var globalEnd = globalStart + range.Count - 1;
                var timeWindow = BuildCueTimeWindow(sourceCues, globalStart, globalEnd);
                var rangeReasons = new List<string>(range.Count);
                for (var cursor = range.Start; cursor < range.Start + range.Count; cursor++)
                {
                    if (failedReasonByIndex.TryGetValue(cursor, out var reason) && !string.IsNullOrWhiteSpace(reason))
                    {
                        rangeReasons.Add(reason);
                    }
                }

                var promptMode = ShouldUseRetryUnchangedPrompt(rangeReasons)
                    ? TranslationPromptMode.RetryUnchanged
                    : TranslationPromptMode.Default;
                var rangeReasonSummary = rangeReasons.Count > 0
                    ? string.Join(",", rangeReasons.Distinct(StringComparer.OrdinalIgnoreCase))
                    : "unknown";
                InMemoryTranslationJobDispatcher.AppendRuntimeLog(
                    "Warn",
                    $"Retry range localStart={range.Start}, count={range.Count}, globalStart={globalStart}, globalEnd={globalEnd}, promptMode={promptMode}, reasons={rangeReasonSummary}{timeWindow}");

                var segmentInputs = payload.Skip(range.Start).Take(range.Count).ToList();
                var segmentTaggedInputs = AddLineMarkers(segmentInputs);
                var retried = await retryPolicy.ExecuteAsync(
                    ct => _client.TranslateBatchAsync(new TranslationBatchRequest
                    {
                        Profile = profile,
                        TargetLanguage = options.GetTargetLanguageCode(),
                        Inputs = segmentTaggedInputs,
                        EnableDebugLog = options.EnableDebugLog,
                        PromptMode = promptMode,
                        RetryReasonHint = rangeReasonSummary
                    }, ct),
                    IsRetryable,
                    cancellationToken).ConfigureAwait(false);

                AddUsage(work, retried);
                var alignedRetried = AlignOutputsByLineMarkers(retried.OutputLines, segmentInputs, out var retryStructuralMismatchCount);
                if (retryStructuralMismatchCount > 0)
                {
                    work.Warnings.Add($"Detected {retryStructuralMismatchCount} marker mismatches during retry segment; kept best-effort alignment.");
                    InMemoryTranslationJobDispatcher.AppendRuntimeLog("Warn", $"Marker mismatch detected in retry lines={retryStructuralMismatchCount}");
                }

                for (var i = 0; i < range.Count; i++)
                {
                    var idx = range.Start + i;
                    var translatedItem = i < alignedRetried.Count ? alignedRetried[i] : string.Empty;
                    if (!string.IsNullOrWhiteSpace(translatedItem))
                    {
                        merged[idx] = translatedItem;
                    }
                }
            }

            failed = FindFailedItems(sourceBatch, merged, payload);
        }

        if (failed.Count > 0)
        {
            throw new InvalidOperationException($"Translation incomplete after retries: {failed.Count} invalid lines in batch {batchIndex + 1}. No subtitle was written.");
        }

        return merged;
    }

    private static bool ShouldUseRetryUnchangedPrompt(IReadOnlyList<string> reasons)
    {
        if (reasons == null || reasons.Count == 0)
        {
            return false;
        }

        var unchangedCount = 0;
        for (var i = 0; i < reasons.Count; i++)
        {
            if (IsUnchangedReason(reasons[i]))
            {
                unchangedCount++;
            }
        }

        return unchangedCount == reasons.Count || unchangedCount * 2 >= reasons.Count;
    }

    private static bool IsUnchangedReason(string reason)
    {
        return string.Equals(reason, "unchanged_source_and_protected", StringComparison.Ordinal)
            || string.Equals(reason, "unchanged_source", StringComparison.Ordinal)
            || string.Equals(reason, "unchanged_protected_source", StringComparison.Ordinal);
    }

    private static void AddUsage(BatchWorkResult work, TranslationBatchResponse response)
    {
        work.PromptTokens += Math.Max(0, response.PromptTokens);
        work.CompletionTokens += Math.Max(0, response.CompletionTokens);
        work.TotalTokens += Math.Max(0, response.TotalTokens);
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is TranslationServiceException serviceEx)
        {
            return serviceEx.IsRetryable;
        }

        if (ex is TimeoutException)
        {
            return true;
        }

        return false;
    }

    private static string SummarizeFailedReasons(IReadOnlyList<FailedLineItem> failed)
    {
        return string.Join(
            ", ",
            failed
                .GroupBy(static f => f.Reason)
                .OrderByDescending(static g => g.Count())
                .Select(g => $"{g.Key}:{g.Count()}"));
    }

    private static string BuildCueTimeWindow(IReadOnlyList<SubtitleCue>? sourceCues, int globalStart, int globalEnd)
    {
        if (sourceCues == null || sourceCues.Count == 0)
        {
            return string.Empty;
        }

        if (globalStart < 0 || globalEnd < globalStart || globalEnd >= sourceCues.Count)
        {
            return string.Empty;
        }

        var start = sourceCues[globalStart].Start;
        var end = sourceCues[globalEnd].End;
        return $" | cueTime={FormatSrtTime(start)}~{FormatSrtTime(end)}";
    }

    private static string FormatSrtTime(TimeSpan ts)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00},{3:000}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds);
    }

    private static List<FailedLineItem> FindFailedItems(
        IReadOnlyList<string> sourceBatch,
        IReadOnlyList<string> translated,
        IReadOnlyList<string>? protectedSourceBatch = null)
    {
        var failed = new List<FailedLineItem>();
        for (var i = 0; i < sourceBatch.Count; i++)
        {
            if (i >= translated.Count || string.IsNullOrWhiteSpace(translated[i]))
            {
                failed.Add(new FailedLineItem(i, "empty_output"));
                continue;
            }

            var translatedText = translated[i];
            var sourceTokens = Regex.Matches(protectedSourceBatch?[i] ?? sourceBatch[i], @"\[\[SUBZ_(?:TAG_\d+|LB)\]\]")
                .Cast<Match>().Select(m => m.Value).Distinct().ToArray();
            var withoutTokens = translatedText;
            foreach (var token in sourceTokens) withoutTokens = withoutTokens.Replace(token, string.Empty);
            if (withoutTokens.IndexOf("SUBZ_", StringComparison.OrdinalIgnoreCase) >= 0
                || sourceTokens.Any(token => !translatedText.Contains(token)))
            {
                failed.Add(new FailedLineItem(i, "invalid_markers"));
                continue;
            }

            var sameAsSource = IsEffectivelySameText(sourceBatch[i], translatedText);
            var sameAsProtectedSource =
                protectedSourceBatch != null
                && i < protectedSourceBatch.Count
                && IsEffectivelySameText(protectedSourceBatch[i], translatedText);

            if (!sameAsSource && !sameAsProtectedSource)
            {
                continue;
            }

            if (IsStandaloneIdentifier(sourceBatch[i]))
            {
                continue;
            }

            if (sameAsSource && sameAsProtectedSource)
            {
                failed.Add(new FailedLineItem(i, "unchanged_source_and_protected"));
            }
            else if (sameAsSource)
            {
                failed.Add(new FailedLineItem(i, "unchanged_source"));
            }
            else
            {
                failed.Add(new FailedLineItem(i, "unchanged_protected_source"));
            }
        }

        return failed;
    }

    private static bool IsStandaloneIdentifier(string text)
    {
        // Preserve only recognizable non-prose tokens. Do not exempt ordinary words
        // or all-uppercase dialogue merely because a model copied them unchanged.
        var plain = Regex.Replace(text ?? string.Empty, @"<[^>]+>|\{[^}]+\}", string.Empty).Trim();
        if (Regex.IsMatch(plain, @"^(?:https?://|www\.)\S+$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(plain, @"^\d+(?:[.,:/-]\d+)*$")) return true;
        if (!Regex.IsMatch(plain, @"^[A-Za-z0-9_]+$")) return false;
        return plain.Contains("_")
            || (Regex.IsMatch(plain, "[A-Za-z]") && Regex.IsMatch(plain, "[0-9]"))
            || Regex.IsMatch(plain, "[a-z][A-Z]")
            || Regex.IsMatch(plain, "^[A-Z]{2,}[a-z]+");
    }

    private static bool IsEffectivelySameText(string? left, string? right)
    {
        var l = NormalizeForComparison(left);
        var r = NormalizeForComparison(right);
        return !string.IsNullOrEmpty(l) && l == r;
    }

    private static string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var raw = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        var compact = new char[raw.Length];
        var pos = 0;
        var prevSpace = false;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                {
                    compact[pos++] = ' ';
                    prevSpace = true;
                }
            }
            else
            {
                compact[pos++] = ch;
                prevSpace = false;
            }
        }

        return new string(compact, 0, pos).Trim();
    }

    private static IReadOnlyList<string> AddLineMarkers(IReadOnlyList<string> inputs)
    {
        var tagged = new List<string>(inputs.Count);
        for (var i = 0; i < inputs.Count; i++)
        {
            tagged.Add($"[[SUBZ_TAG_LINE_{i:D4}]] {(inputs[i] ?? string.Empty)}");
        }

        return tagged;
    }

    private static List<string> AlignOutputsByLineMarkers(
        IReadOnlyList<string> outputLines,
        IReadOnlyList<string> fallbackInputs,
        out int structuralMismatchCount)
    {
        var expected = fallbackInputs.Count;
        var resolved = Enumerable.Repeat(string.Empty, expected).ToArray();
        var seen = new bool[expected];
        var duplicates = new bool[expected];
        var raw = string.Join("\n", outputLines);
        var matches = LineIdRegex.Matches(raw);
        var invalidEnvelope = matches.Count == 0;
        if (matches.Count > 0 && !string.IsNullOrWhiteSpace(raw.Substring(0, matches[0].Index).Replace("<<<SUBZ_SEP>>>", string.Empty)))
            invalidEnvelope = true;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (!int.TryParse(match.Groups[1].Value, out var id) || id < 0 || id >= expected)
            {
                invalidEnvelope = true;
                continue;
            }
            var end = i + 1 < matches.Count ? matches[i + 1].Index : raw.Length;
            var text = raw.Substring(match.Index + match.Length, end - match.Index - match.Length).Trim();
            // A separator may terminate this cue, but text after it without a new ID
            // is ambiguous and must not be folded into the preceding translation.
            var separator = text.IndexOf("<<<SUBZ_SEP>>>", StringComparison.Ordinal);
            if (separator >= 0)
            {
                var suffix = text.Substring(separator).Replace("<<<SUBZ_SEP>>>", string.Empty);
                text = string.IsNullOrWhiteSpace(suffix) ? text.Substring(0, separator).Trim() : string.Empty;
            }
            if (seen[id]) duplicates[id] = true;
            else resolved[id] = text;
            seen[id] = true;
        }

        structuralMismatchCount = 0;
        for (var i = 0; i < expected; i++)
        {
            if (invalidEnvelope || duplicates[i] || !seen[i] || string.IsNullOrWhiteSpace(resolved[i]))
            {
                resolved[i] = string.Empty;
                structuralMismatchCount++;
            }
        }
        return resolved.ToList();
    }

    private static List<(int Start, int Count)> BuildFailedRanges(IReadOnlyList<int> failed)
    {
        var ranges = new List<(int Start, int Count)>();
        if (failed.Count == 0)
        {
            return ranges;
        }

        var start = failed[0];
        var prev = failed[0];
        for (var i = 1; i < failed.Count; i++)
        {
            var cur = failed[i];
            if (cur == prev + 1)
            {
                prev = cur;
                continue;
            }

            ranges.Add((start, prev - start + 1));
            start = cur;
            prev = cur;
        }

        ranges.Add((start, prev - start + 1));
        return ranges;
    }

    private static List<string> NormalizeLength(IReadOnlyList<string>? translated, IReadOnlyList<string> payload)
    {
        var list = translated?.ToList() ?? new List<string>();

        while (list.Count > payload.Count)
        {
            list.RemoveAt(list.Count - 1);
        }

        while (list.Count < payload.Count)
        {
            list.Add(string.Empty);
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(list[i]))
            {
                list[i] = string.Empty;
            }
        }

        return list;
    }

    private static List<List<string>> Split(IReadOnlyList<string> lines, int batchSize)
    {
        var output = new List<List<string>>();
        for (var i = 0; i < lines.Count; i += batchSize)
        {
            var count = Math.Min(batchSize, lines.Count - i);
            output.Add(lines.Skip(i).Take(count).ToList());
        }

        return output;
    }

    private sealed class BatchWorkResult
    {
        public List<string> RestoredLines { get; } = new List<string>();
        public int TailRetryCount { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long TotalTokens { get; set; }
    }

    private sealed class FailedLineItem
    {
        public FailedLineItem(int index, string reason)
        {
            Index = index;
            Reason = reason;
        }

        public int Index { get; }
        public string Reason { get; }
    }
}
