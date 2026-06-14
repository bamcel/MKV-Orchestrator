using System.Text.Json;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class MediaInfoService
{
    private readonly ProcessRunner _runner = new();

    public async Task EnrichAsync(MkvFileItem item, string mediaInfoPath, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(mediaInfoPath))
            return;

        var result = await _runner.RunAsync(mediaInfoPath, new[] { "--Output=JSON", CrossPlatformRuntime.ToProcessArgumentPath(item.FilePath) }, token);
        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"MediaInfo exited with code {result.ExitCode}." : error.Trim());
        }
        if (string.IsNullOrWhiteSpace(result.StandardOutput))
            throw new InvalidOperationException("MediaInfo returned no JSON output.");

        using var doc = JsonDocument.Parse(result.StandardOutput);
        if (!doc.RootElement.TryGetProperty("media", out var media) ||
            !media.TryGetProperty("track", out var tracks) ||
            tracks.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("MediaInfo JSON did not contain media.track.");

        var videoIndex = 0;
        var audioLangs = new List<string>();
        var subLangs = new List<string>();

        foreach (var track in tracks.EnumerateArray())
        {
            var type = GetString(track, "@type") ?? string.Empty;
            if (type.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                var title = GetString(track, "Title") ?? GetString(track, "Movie") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item.ContainerTitle) && !string.IsNullOrWhiteSpace(title))
                    item.ContainerTitle = title;
            }
            else if (type.Equals("Video", StringComparison.OrdinalIgnoreCase))
            {
                var codec = FirstNonBlank(GetString(track, "Format"), GetString(track, "CodecID"), item.Codec);
                var width = FirstNonBlank(GetString(track, "Width"), GetString(track, "Stored_Width"));
                var height = FirstNonBlank(GetString(track, "Height"), GetString(track, "Stored_Height"));
                var resolution = BuildResolution(width, height);
                var bitDepth = NormalizeBitDepth(FirstNonBlank(GetString(track, "BitDepth"), GetString(track, "BitDepth/String"), GetString(track, "BitDepth_String")));

                item.Codec = DisplayValue(codec);
                if (!string.IsNullOrWhiteSpace(resolution)) item.Resolution = resolution;
                if (!string.IsNullOrWhiteSpace(bitDepth)) item.BitDepth = bitDepth;
                item.VideoSummary = BuildVideoSummary(item.Codec, item.Resolution, item.BitDepth);

                var videoTrack = item.Tracks.Where(t => t.Type.Equals("video", StringComparison.OrdinalIgnoreCase)).Skip(videoIndex).FirstOrDefault();
                if (videoTrack is not null)
                {
                    videoTrack.Codec = DisplayValue(codec);
                    if (!string.IsNullOrWhiteSpace(resolution)) videoTrack.Resolution = resolution;
                    if (!string.IsNullOrWhiteSpace(bitDepth)) videoTrack.BitDepth = bitDepth;
                    var title = GetString(track, "Title");
                    if (string.IsNullOrWhiteSpace(videoTrack.Name) && !string.IsNullOrWhiteSpace(title)) videoTrack.Name = title;
                }
                videoIndex++;
            }
            else if (type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
            {
                var lang = NormalizeLanguage(FirstNonBlank(GetString(track, "Language"), GetString(track, "Language_String"), GetString(track, "Language_String3")));
                if (!string.IsNullOrWhiteSpace(lang)) audioLangs.Add(lang);
                var audioTrack = item.Tracks.FirstOrDefault(t => t.Type.Equals("audio", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(t.Codec));
                if (audioTrack is not null)
                {
                    audioTrack.Codec = FirstNonBlank(GetString(track, "Format"), audioTrack.Codec);
                }
            }
            else if (type.Equals("Text", StringComparison.OrdinalIgnoreCase) || type.Equals("Menu", StringComparison.OrdinalIgnoreCase))
            {
                var lang = NormalizeLanguage(FirstNonBlank(GetString(track, "Language"), GetString(track, "Language_String"), GetString(track, "Language_String3")));
                if (!string.IsNullOrWhiteSpace(lang)) subLangs.Add(lang);
            }
        }

        if (audioLangs.Count > 0) item.AudioSummary = BuildLanguageSummary(audioLangs);
        if (subLangs.Count > 0) item.SubtitleSummary = BuildLanguageSummary(subLangs);

        if (string.IsNullOrWhiteSpace(item.Resolution)) item.Resolution = "Unknown";
        if (string.IsNullOrWhiteSpace(item.BitDepth)) item.BitDepth = "Unknown";
        if (string.IsNullOrWhiteSpace(item.Codec)) item.Codec = "Unknown";
        if (string.IsNullOrWhiteSpace(item.VideoSummary)) item.VideoSummary = BuildVideoSummary(item.Codec, item.Resolution, item.BitDepth);
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string DisplayValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();

    private static string BuildResolution(string width, string height)
    {
        width = DigitsOnly(width);
        height = DigitsOnly(height);
        return !string.IsNullOrWhiteSpace(width) && !string.IsNullOrWhiteSpace(height) ? $"{width}x{height}" : string.Empty;
    }

    private static string DigitsOnly(string value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string NormalizeBitDepth(string value)
    {
        var digits = DigitsOnly(value);
        return string.IsNullOrWhiteSpace(digits) ? string.Empty : $"{digits}bit";
    }

    private static string NormalizeLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length >= 3 ? value[..3].ToLowerInvariant() : value.ToLowerInvariant();
    }

    private static string BuildLanguageSummary(IEnumerable<string> languages)
        => string.Join(", ", languages.Where(x => !string.IsNullOrWhiteSpace(x)).GroupBy(x => x).Select(g => $"{g.Key} x{g.Count()}"));

    private static string BuildVideoSummary(string codec, string resolution, string bitDepth)
        => string.Join(" | ", new[] { codec, resolution, bitDepth }.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));
}
