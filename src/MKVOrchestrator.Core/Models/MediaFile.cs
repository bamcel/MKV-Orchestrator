using System.Collections.ObjectModel;
using System.IO;

namespace MKVOrchestrator.Core.Models;

/// <summary>
/// Canonical media identity used by scan, cache, watcher, rename, propedit, and remux flows.
/// UI row models are projections of this model; services should pass this model when possible.
/// </summary>
public sealed class MediaFile
{
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public string WatchRoot { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string SeriesTitle { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public int? AbsoluteEpisode { get; set; }
    public string EpisodeTitle { get; set; } = string.Empty;
    public MediaTechnicalMetadata Metadata { get; set; } = new();
    public ProviderMatch ProviderMatch { get; set; } = new();
    public List<MediaTrack> Tracks { get; set; } = new();
    public List<MediaAttachment> Attachments { get; set; } = new();
    public string Status { get; set; } = "Ready";
}

public sealed class MediaTechnicalMetadata
{
    public string ContainerTitle { get; set; } = string.Empty;
    public string VideoSummary { get; set; } = string.Empty;
    public string AudioSummary { get; set; } = string.Empty;
    public string SubtitleSummary { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string BitDepth { get; set; } = string.Empty;
    public string Hdr { get; set; } = string.Empty;
    public string AttachmentSummary { get; set; } = string.Empty;
}

public sealed class MediaTrack
{
    public int MkvMergeId { get; set; }
    public int PropEditTrackNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string BitDepth { get; set; } = string.Empty;
    public string Hdr { get; set; } = string.Empty;
    public bool Default { get; set; }
    public bool Forced { get; set; }
}

public sealed class MediaAttachment
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
}

public sealed class ProviderMatch
{
    public string Provider { get; set; } = string.Empty;
    public int? SeriesId { get; set; }
    public int? EpisodeId { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public string EpisodeName { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
