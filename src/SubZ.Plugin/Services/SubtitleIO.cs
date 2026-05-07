using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SubZ.Plugin.Services;

public static class SubtitleIO
{
    private static readonly Regex SrtTimeRegex = new Regex(@"^(\d{2}:\d{2}:\d{2},\d{3})\s+-->\s+(\d{2}:\d{2}:\d{2},\d{3})$", RegexOptions.Compiled);

    public static List<SubtitleCue> ReadFromFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var text = ReadTextWithBom(path);

        if (ext == ".ass" || ext == ".ssa")
        {
            return ParseAss(text);
        }

        return ParseSrt(text);
    }

    public static void WriteAssBilingual(string outputPath, IEnumerable<SubtitleCue> cues, string fontName, int fontSize)
    {
        WriteAssBilingual(outputPath, cues, fontName, fontSize, "&H00FFFFFF");
    }

    public static void WriteAssBilingual(string outputPath, IEnumerable<SubtitleCue> cues, string fontName, int fontSize, string primaryColor)
    {
        var normalizedPrimaryColor = NormalizeAssColor(primaryColor);
        var sb = new StringBuilder();
        sb.AppendLine("[Script Info]");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("PlayResX: 1920");
        sb.AppendLine("PlayResY: 1080");
        sb.AppendLine("WrapStyle: 0");
        sb.AppendLine("ScaledBorderAndShadow: no");
        sb.AppendLine();
        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        // Reference style adapted from existing library ASS defaults (white text, black outline).
        sb.AppendLine($"Style: Default,{fontName},{fontSize},{normalizedPrimaryColor},&HF0000000,&H00000000,&HF0000000,-1,0,0,0,100,100,0,0,1,1,0,2,5,5,18,1");
        sb.AppendLine();
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        var idx = 0;
        foreach (var cue in cues)
        {
            idx++;
            var text = (cue.Text ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", "\\N");
            sb.AppendLine($"Dialogue: 0,{ToAssTime(cue.Start)},{ToAssTime(cue.End)},Default,,0000,0000,0000,,{text}");
        }

        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static string NormalizeAssColor(string value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "&H00FFFFFF";
        }

        if (raw.StartsWith("#", StringComparison.Ordinal))
        {
            var hex = raw.Substring(1);
            if (hex.Length == 6 && IsHex(hex))
            {
                return "&H00" + hex.ToUpperInvariant();
            }
        }

        if (raw.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
        {
            var hex = raw.Substring(2);
            if (hex.Length == 8 && IsHex(hex))
            {
                return "&H" + hex.ToUpperInvariant();
            }
        }

        return "&H00FFFFFF";
    }

    private static bool IsHex(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var ok = (c >= '0' && c <= '9')
                || (c >= 'a' && c <= 'f')
                || (c >= 'A' && c <= 'F');
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    public static void WriteSrtBilingual(string outputPath, IEnumerable<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        var index = 1;
        foreach (var cue in cues)
        {
            sb.AppendLine(index.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine($"{ToSrtTime(cue.Start)} --> {ToSrtTime(cue.End)}");
            sb.AppendLine((cue.Text ?? string.Empty).Replace("\r", string.Empty));
            sb.AppendLine();
            index++;
        }

        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static string ReadTextWithBom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        // No BOM: try strict UTF-8 first, then fallback for common CJK legacy encodings.
        if (TryDecodeUtf8Strict(bytes, out var utf8Text))
        {
            return utf8Text;
        }

        if (TryDecodeByCodePage(bytes, "GB18030", out var gb18030Text))
        {
            Plugin.LogWarn("SubZ subtitle decode fallback to GB18030 for file: {0}", path);
            return gb18030Text;
        }

        if (TryDecodeByCodePage(bytes, 936, out var gbkText))
        {
            Plugin.LogWarn("SubZ subtitle decode fallback to code page 936 for file: {0}", path);
            return gbkText;
        }

        Plugin.LogWarn("SubZ subtitle decode fallback to UTF-8(replacement) for file: {0}", path);
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool TryDecodeUtf8Strict(byte[] bytes, out string text)
    {
        text = string.Empty;
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            text = strictUtf8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeByCodePage(byte[] bytes, string name, out string text)
    {
        text = string.Empty;
        try
        {
            var enc = Encoding.GetEncoding(name);
            text = enc.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeByCodePage(byte[] bytes, int codePage, out string text)
    {
        text = string.Empty;
        try
        {
            var enc = Encoding.GetEncoding(codePage);
            text = enc.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<SubtitleCue> ParseSrt(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var output = new List<SubtitleCue>();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            if (lines.Length < 2)
            {
                continue;
            }

            var pointer = 0;
            if (int.TryParse(lines[0].Trim(), out _))
            {
                pointer = 1;
            }

            if (pointer >= lines.Length)
            {
                continue;
            }

            var m = SrtTimeRegex.Match(lines[pointer].Trim());
            if (!m.Success)
            {
                continue;
            }

            if (!TimeSpan.TryParseExact(m.Groups[1].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var start))
            {
                continue;
            }

            if (!TimeSpan.TryParseExact(m.Groups[2].Value, @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture, out var end))
            {
                continue;
            }

            var textLines = lines.Skip(pointer + 1).Where(l => !string.IsNullOrWhiteSpace(l));
            output.Add(new SubtitleCue
            {
                Index = output.Count + 1,
                Start = start,
                End = end,
                Text = string.Join("\n", textLines)
            });
        }

        return output;
    }

    private static List<SubtitleCue> ParseAss(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var output = new List<SubtitleCue>();

        foreach (var line in lines)
        {
            if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var body = line.Substring("Dialogue:".Length).Trim();
            var parts = SplitCsvWithLimit(body, 10);
            if (parts.Count < 10)
            {
                continue;
            }

            if (!TryParseAssTime(parts[1], out var start) || !TryParseAssTime(parts[2], out var end))
            {
                continue;
            }

            var textPart = parts[9].Replace("\\N", "\n");
            output.Add(new SubtitleCue
            {
                Index = output.Count + 1,
                Start = start,
                End = end,
                Text = StripAssTags(textPart)
            });
        }

        return output;
    }

    private static string StripAssTags(string input)
    {
        return Regex.Replace(input ?? string.Empty, @"\{[^}]*\}", string.Empty);
    }

    private static List<string> SplitCsvWithLimit(string input, int expectedParts)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        var commas = 0;

        foreach (var ch in input)
        {
            if (ch == ',' && commas < expectedParts - 1)
            {
                parts.Add(sb.ToString());
                sb.Clear();
                commas++;
            }
            else
            {
                sb.Append(ch);
            }
        }

        parts.Add(sb.ToString());
        return parts;
    }

    private static string ToAssTime(TimeSpan ts)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds / 10);
    }

    private static string ToSrtTime(TimeSpan ts)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds);
    }

    private static bool TryParseAssTime(string input, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var m = Regex.Match(input.Trim(), @"^(\d+):(\d{2}):(\d{2})\.(\d{2})$");
        if (!m.Success)
        {
            return false;
        }

        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mi = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var s = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        var cs = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        value = new TimeSpan(0, h, mi, s, cs * 10);
        return true;
    }
}
