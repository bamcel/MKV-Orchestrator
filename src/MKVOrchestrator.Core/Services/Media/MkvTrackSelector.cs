using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

/// <summary>
/// Centralizes the difference between mkvmerge numeric track IDs and mkvpropedit
/// type-specific ordinal selectors such as track:a1, track:s1, and track:v1.
/// </summary>
public static class MkvTrackSelector
{
    public const string VideoType = "video";
    public const string AudioType = "audio";
    public const string SubtitleType = "subtitles";

    public static int GetMkvMergeTrackId(MkvTrackItem track) => track.MkvMergeId;

    public static string ForMkvMergeTrackId(int mkvMergeTrackId) => mkvMergeTrackId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string ForMkvPropEdit(MkvTrackItem track, IReadOnlyList<MkvTrackItem> allTracks)
    {
        var prefix = GetPropEditPrefix(track.Type);
        var ordinal = GetTypeOrdinal(track, allTracks);
        return $"track:{prefix}{ordinal}";
    }

    public static string ForMkvPropEdit(string type, int oneBasedTypeOrdinal)
    {
        if (oneBasedTypeOrdinal < 1) oneBasedTypeOrdinal = 1;
        return $"track:{GetPropEditPrefix(type)}{oneBasedTypeOrdinal}";
    }

    public static string ForMkvPropEditNumber(int propEditTrackNumber) => $"track:{propEditTrackNumber}";

    private static string GetPropEditPrefix(string type)
    {
        return NormalizeTrackType(type) switch
        {
            VideoType => "v",
            AudioType => "a",
            SubtitleType => "s",
            _ => string.Empty
        };
    }

    public static string NormalizeTrackType(string type)
    {
        if (string.Equals(type, "subtitles", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "subtitle", StringComparison.OrdinalIgnoreCase))
        {
            return SubtitleType;
        }

        if (string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase)) return AudioType;
        if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)) return VideoType;
        return type.Trim().ToLowerInvariant();
    }

    private static int GetTypeOrdinal(MkvTrackItem track, IReadOnlyList<MkvTrackItem> allTracks)
    {
        var normalizedType = NormalizeTrackType(track.Type);
        var ordinal = 0;
        foreach (var candidate in allTracks)
        {
            if (NormalizeTrackType(candidate.Type) != normalizedType) continue;
            ordinal++;
            if (ReferenceEquals(candidate, track) ||
                candidate.MkvMergeId == track.MkvMergeId ||
                candidate.PropEditTrackNumber == track.PropEditTrackNumber)
            {
                return ordinal;
            }
        }

        return Math.Max(1, track.PropEditTrackNumber);
    }
}
