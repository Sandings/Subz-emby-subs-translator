using System;
using System.Collections.Generic;
using System.Linq;
using SubZ.Plugin.Configuration;

namespace SubZ.Plugin.Services;

public sealed class SubtitleTrackInfo
{
    public int Id { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public bool IsForced { get; set; }
    public bool IsHearingImpaired { get; set; }
    public bool IsDefault { get; set; }
    public bool IsTextTrack { get; set; }
}

public sealed class SubtitleTrackSelector
{
    public SubtitleTrackInfo? SelectBest(IReadOnlyList<SubtitleTrackInfo> tracks, PluginOptions options)
    {
        if (tracks is null || tracks.Count == 0)
        {
            return null;
        }

        return tracks
            .OrderByDescending(t => Score(t, options))
            .ThenBy(t => t.Id)
            .FirstOrDefault();
    }

    private static int Score(SubtitleTrackInfo track, PluginOptions options)
    {
        var score = 0;

        if (options.PreferTextSubtitleTrack)
        {
            score += track.IsTextTrack ? 500 : -500;
        }

        if (options.PreferNonForcedTrack)
        {
            score += track.IsForced ? -120 : 120;
        }

        if (options.PreferNonHearingImpairedTrack)
        {
            score += track.IsHearingImpaired ? -80 : 80;
        }

        if (track.IsDefault)
        {
            score += 40;
        }

        if (string.Equals(track.Language, options.GetTargetLanguageCode(), StringComparison.OrdinalIgnoreCase))
        {
            // Lightly demote already-target-language tracks for translation scenarios.
            score -= 20;
        }

        var codec = track.Codec?.Trim().ToLowerInvariant() ?? string.Empty;
        if (codec == "subrip" || codec == "srt" || codec == "ass" || codec == "ssa" || codec == "webvtt")
        {
            score += 30;
        }

        if (codec == "pgs" || codec == "dvd_subtitle" || codec == "vobsub")
        {
            score -= 200;
        }

        return score;
    }
}
