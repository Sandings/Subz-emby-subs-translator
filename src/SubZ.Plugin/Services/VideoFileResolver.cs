using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SubZ.Plugin.Services;

public static class VideoFileResolver
{
    private static readonly HashSet<string> VideoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".ts", ".m2ts", ".mov"
    };

    public static IEnumerable<string> ResolveVideoFiles(string target)
    {
        if (File.Exists(target))
        {
            if (VideoExts.Contains(Path.GetExtension(target)))
            {
                yield return target;
            }

            yield break;
        }

        if (Directory.Exists(target))
        {
            var files = Directory.EnumerateFiles(target, "*.*", SearchOption.AllDirectories)
                .Where(static f => VideoExts.Contains(Path.GetExtension(f)));

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    public static bool HasTargetSubtitle(string videoFile, string targetCode)
    {
        var dir = Path.GetDirectoryName(videoFile) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(videoFile);
        if (!Directory.Exists(dir))
        {
            return false;
        }

        var patterns = new[]
        {
            $"{stem}*.{targetCode}.ass",
            $"{stem}*.{targetCode}.srt",
            $"{stem}*.subz.{targetCode}.ass",
            $"{stem}*.subz.{targetCode}.srt"
        };

        return patterns.Any(p => Directory.EnumerateFiles(dir, p, SearchOption.TopDirectoryOnly).Any());
    }
}
