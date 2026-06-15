using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public static class CodecDisplayNormalizer
{
    public static string Normalize(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec)) return "Unknown";

        var clean = codec.Trim();
        var key = clean.ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        if (ContainsAny(key, "hevc", "h265", "h.265", "mpegh")) return "HEVC/H.265";
        if (ContainsAny(key, "avc", "h264", "h.264", "mpeg4avc")) return "AVC/H.264";
        if (ContainsAny(key, "av1")) return "AV1";
        if (ContainsAny(key, "vp9")) return "VP9";
        if (ContainsAny(key, "vp8")) return "VP8";
        if (ContainsAny(key, "mpeg2video", "mpeg2")) return "MPEG-2";
        if (ContainsAny(key, "mpeg4", "xvid", "divx")) return "MPEG-4";
        if (ContainsAny(key, "vc1", "wvc1")) return "VC-1";
        if (ContainsAny(key, "prores")) return "ProRes";
        if (ContainsAny(key, "theora")) return "Theora";

        return clean;
    }

    public static string DisplayValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();

    public static string BuildVideoSummary(string codec, string resolution, string bitDepth)
        => string.Join(" | ", new[] { codec, resolution, bitDepth }
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("Unknown", StringComparison.OrdinalIgnoreCase)));

    public static string BuildLanguageTrackSummary(IEnumerable<MkvTrackItem> tracks)
        => string.Join(", ", tracks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Language) ? "und" : t.Language)
            .Select(g => $"{g.Key} x{g.Count()}"));

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
