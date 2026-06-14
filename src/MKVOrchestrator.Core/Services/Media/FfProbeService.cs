using System.Text.Json;
using System.Text.RegularExpressions;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class FfProbeService
{
    private readonly ProcessRunner _runner = new();

    public async Task ApplyMediaInfoAsync(string ffProbePath, MkvFileItem item, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ffProbePath)) return;
        ValidateFfProbePath(ffProbePath);
        ffProbePath = CrossPlatformRuntime.ResolveExecutable(
            ffProbePath,
            "ffprobe.exe",
            "ffprobe",
            @"C:\ffmpeg\bin\ffprobe.exe",
            @"C:\Program Files\ffmpeg\bin\ffprobe.exe",
            "/usr/bin/ffprobe",
            "/usr/local/bin/ffprobe",
            "/opt/homebrew/bin/ffprobe");

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

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement element, string name)
    {
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
