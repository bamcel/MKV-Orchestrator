using System.Text.Json;
using System.Text.RegularExpressions;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class FfProbeService
{
    private readonly ProcessRunner _runner = new();

    public async Task<MkvFileItem> IdentifyAsync(string ffProbePath, string filePath, CancellationToken token)
    {
        ValidateFfProbePath(ffProbePath);
        ffProbePath = ResolveFfProbeExecutable(ffProbePath);

        var result = await _runner.RunAsync(ffProbePath, new[]
        {
            "-v", "error",
            "-show_entries", "format=format_name:stream=index,codec_type,codec_name,codec_long_name,profile,width,height,pix_fmt,bits_per_raw_sample,bits_per_sample:stream_tags=language,title:stream_disposition=default,forced",
            "-of", "json",
            CrossPlatformRuntime.ToProcessArgumentPath(filePath)
        }, token);

        if (result.ExitCode != 0)
        {
            var err = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new InvalidOperationException($"ffprobe failed: {err.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
            throw new InvalidOperationException("ffprobe returned no JSON output.");

        using var doc = JsonDocument.Parse(result.StandardOutput);
        var item = new MkvFileItem { FilePath = filePath };
        if (!doc.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
        {
            item.Status = "Scanned - no tracks found";
            return item;
        }

        var propEditNumber = 1;
        foreach (var stream in streams.EnumerateArray())
        {
            var type = NormalizeStreamType(GetString(stream, "codec_type"));
            if (string.IsNullOrWhiteSpace(type)) continue;

            var codec = GetString(stream, "codec_name")
                        ?? GetString(stream, "codec_long_name")
                        ?? string.Empty;

            var tags = stream.TryGetProperty("tags", out var tagElement) ? tagElement : default;
            var disposition = stream.TryGetProperty("disposition", out var dispositionElement) ? dispositionElement : default;
            var track = new MkvTrackItem
            {
                MkvMergeId = GetInt(stream, "index") ?? item.Tracks.Count,
                PropEditTrackNumber = propEditNumber++,
                Type = type,
                Codec = codec,
                Language = GetString(tags, "language") ?? "und",
                Name = GetString(tags, "title") ?? string.Empty,
                Default = GetInt(disposition, "default") == 1,
                Forced = GetInt(disposition, "forced") == 1
            };

            if (type.Equals("video", StringComparison.OrdinalIgnoreCase))
            {
                track.Resolution = GetResolution(stream);
                track.BitDepth = GetBitDepth(stream, filePath);
                item.Codec = DisplayValue(codec);
                item.Resolution = DisplayValue(track.Resolution);
                item.BitDepth = DisplayValue(track.BitDepth);
                item.VideoSummary = BuildVideoSummary(item.Codec, item.Resolution, item.BitDepth);
            }

            item.Tracks.Add(track);
        }

        item.AudioSummary = BuildTrackSummary(item.Tracks.Where(t => t.Type.Equals("audio", StringComparison.OrdinalIgnoreCase)));
        item.SubtitleSummary = BuildTrackSummary(item.Tracks.Where(t => t.Type.Equals("subtitles", StringComparison.OrdinalIgnoreCase)));
        item.AttachmentSummary = "None";
        item.Status = item.Tracks.Any(t => t.Type.Equals("video", StringComparison.OrdinalIgnoreCase))
            ? "Scanned"
            : "Scanned - no video track";

        return item;
    }

    public async Task ApplyMediaInfoAsync(string ffProbePath, MkvFileItem item, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ffProbePath)) return;
        ValidateFfProbePath(ffProbePath);
        ffProbePath = ResolveFfProbeExecutable(ffProbePath);

        var result = await _runner.RunAsync(ffProbePath, new[]
        {
            "-v", "error",
            "-select_streams", "v:0",
            "-show_entries", "stream=codec_type,codec_name,codec_long_name,profile,width,height,pix_fmt,bits_per_raw_sample,bits_per_sample",
            "-of", "json",
            CrossPlatformRuntime.ToProcessArgumentPath(item.FilePath)
        }, token);

        if (result.ExitCode != 0)
        {
            var err = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new InvalidOperationException($"ffprobe failed: {err.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
            throw new InvalidOperationException("ffprobe returned no JSON output.");

        using var doc = JsonDocument.Parse(result.StandardOutput);
        if (!doc.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            return;

        foreach (var stream in streams.EnumerateArray())
        {
            var type = GetString(stream, "codec_type") ?? string.Empty;
            var codec = GetString(stream, "codec_name")
                        ?? GetString(stream, "codec_long_name")
                        ?? string.Empty;
            if (type.Equals("video", StringComparison.OrdinalIgnoreCase))
            {
                var resolution = GetResolution(stream);
                var bitDepth = GetBitDepth(stream, item.FilePath);

                if (!string.IsNullOrWhiteSpace(codec)) item.Codec = codec;
                if (!string.IsNullOrWhiteSpace(resolution)) item.Resolution = resolution;
                if (!string.IsNullOrWhiteSpace(bitDepth)) item.BitDepth = bitDepth;

                var videoTrack = item.Tracks.FirstOrDefault(t => t.Type.Equals("video", StringComparison.OrdinalIgnoreCase));
                if (videoTrack is not null)
                {
                    if (!string.IsNullOrWhiteSpace(codec)) videoTrack.Codec = codec;
                    if (!string.IsNullOrWhiteSpace(resolution)) videoTrack.Resolution = resolution;
                    if (!string.IsNullOrWhiteSpace(bitDepth)) videoTrack.BitDepth = bitDepth;
                }
            }
        }

        item.Codec = DisplayValue(item.Codec);
        item.Resolution = DisplayValue(item.Resolution);
        item.BitDepth = DisplayValue(item.BitDepth);
        item.VideoSummary = string.Join(" | ", new[] { item.Codec, item.Resolution, item.BitDepth }
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));
    }

    private static void ValidateFfProbePath(string ffProbePath)
    {
        var exeName = Path.GetFileName(ffProbePath.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(exeName)) return;
        if (exeName.Equals("ffprobe", StringComparison.OrdinalIgnoreCase) || exeName.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)) return;
        if (exeName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) || exeName.Equals("ffplay.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The configured ffprobe path points to '{exeName}'. Select ffprobe in Settings or ensure ffprobe is available on PATH.");
    }

    private static string ResolveFfProbeExecutable(string ffProbePath)
    {
        return CrossPlatformRuntime.ResolveExecutable(
            ffProbePath,
            "ffprobe.exe",
            "ffprobe",
            @"C:\ffmpeg\bin\ffprobe.exe",
            @"C:\Program Files\ffmpeg\bin\ffprobe.exe",
            "/usr/bin/ffprobe",
            "/usr/local/bin/ffprobe",
            "/opt/homebrew/bin/ffprobe");
    }

    private static string NormalizeStreamType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        return type.Equals("subtitle", StringComparison.OrdinalIgnoreCase) ? "subtitles" : type;
    }

    private static string BuildVideoSummary(string codec, string resolution, string bitDepth)
        => string.Join(" | ", new[] { codec, resolution, bitDepth }
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));

    private static string BuildTrackSummary(IEnumerable<MkvTrackItem> tracks)
        => string.Join(", ", tracks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Language) ? "und" : t.Language)
            .Select(g => $"{g.Key} x{g.Count()}"));

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var v) &&
           v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return null;
    }

    private static string GetResolution(JsonElement stream)
    {
        var w = GetInt(stream, "width");
        var h = GetInt(stream, "height");
        return w.HasValue && h.HasValue ? $"{w}x{h}" : string.Empty;
    }

    private static string GetBitDepth(JsonElement stream, string filePath)
    {
        var bits = GetInt(stream, "bits_per_raw_sample")
                ?? GetInt(stream, "bits_per_sample");
        if (bits.HasValue && bits.Value > 0) return $"{bits.Value}bit";

        var pixFmt = GetString(stream, "pix_fmt") ?? string.Empty;
        var profile = GetString(stream, "profile") ?? string.Empty;
        var codec = (GetString(stream, "codec_name") ?? string.Empty) + " " + (GetString(stream, "codec_long_name") ?? string.Empty);
        var combined = (pixFmt + " " + profile + " " + codec + " " + Path.GetFileNameWithoutExtension(filePath)).ToLowerInvariant();

        if (Regex.IsMatch(combined, @"(p10|yuv420p10|yuv422p10|yuv444p10|10bit|10-bit|10 bit|hi10p|main\s*10)")) return "10bit";
        if (Regex.IsMatch(combined, @"(p12|yuv420p12|yuv422p12|yuv444p12|12bit|12-bit|12 bit)")) return "12bit";
        if (Regex.IsMatch(combined, @"(yuv420p\b|yuv422p\b|yuv444p\b|8bit|8-bit|8 bit)")) return "8bit";
        return string.Empty;
    }

    private static string DisplayValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
}
