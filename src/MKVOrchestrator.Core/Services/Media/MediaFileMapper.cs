using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

/// <summary>
/// Single projection boundary between the canonical service model and the existing UI row model.
/// This prevents scan/cache/watch/rename/remux from creating slightly different file objects.
/// </summary>
public static class MediaFileMapper
{
    public static MediaFile FromMkvFileItem(MkvFileItem item)
    {
        var media = new MediaFile
        {
            FilePath = CrossPlatformRuntime.NormalizeUserPath(item.FilePath),
            OriginalFileName = item.FileName,
            Status = item.Status,
            Metadata = new MediaTechnicalMetadata
            {
                ContainerTitle = item.ContainerTitle,
                VideoSummary = item.VideoSummary,
                AudioSummary = item.AudioSummary,
                SubtitleSummary = item.SubtitleSummary,
                Resolution = item.Resolution,
                Codec = item.Codec,
                BitDepth = item.BitDepth,
                Hdr = item.Hdr,
                AttachmentSummary = item.AttachmentSummary
            },
            Tracks = item.Tracks.Select(FromTrackItem).ToList(),
            Attachments = item.Attachments.Select(FromAttachmentItem).ToList()
        };
        return media;
    }

    public static MkvFileItem ToMkvFileItem(MediaFile media)
    {
        var item = new MkvFileItem
        {
            FilePath = CrossPlatformRuntime.NormalizeUserPath(media.FilePath),
            ContainerTitle = media.Metadata.ContainerTitle,
            VideoSummary = media.Metadata.VideoSummary,
            AudioSummary = media.Metadata.AudioSummary,
            SubtitleSummary = media.Metadata.SubtitleSummary,
            Resolution = media.Metadata.Resolution,
            Codec = media.Metadata.Codec,
            BitDepth = media.Metadata.BitDepth,
            Hdr = media.Metadata.Hdr,
            AttachmentSummary = media.Metadata.AttachmentSummary,
            Status = string.IsNullOrWhiteSpace(media.Status) ? "Ready" : media.Status,
            CanonicalMedia = media
        };

        foreach (var track in media.Tracks)
        {
            item.Tracks.Add(ToTrackItem(track));
        }

        foreach (var attachment in media.Attachments)
        {
            item.Attachments.Add(ToAttachmentItem(attachment));
        }

        item.CanonicalMedia = FromMkvFileItem(item);
        item.CanonicalMedia.WatchRoot = media.WatchRoot;
        item.CanonicalMedia.RelativePath = media.RelativePath;
        item.CanonicalMedia.SeriesTitle = media.SeriesTitle;
        item.CanonicalMedia.Season = media.Season;
        item.CanonicalMedia.Episode = media.Episode;
        item.CanonicalMedia.AbsoluteEpisode = media.AbsoluteEpisode;
        item.CanonicalMedia.EpisodeTitle = media.EpisodeTitle;
        item.CanonicalMedia.ProviderMatch = media.ProviderMatch;
        return item;
    }

    private static MediaTrack FromTrackItem(MkvTrackItem item) => new()
    {
        MkvMergeId = item.MkvMergeId,
        PropEditTrackNumber = item.PropEditTrackNumber,
        Type = item.Type,
        Codec = item.Codec,
        Language = item.Language,
        Name = item.Name,
        Resolution = item.Resolution,
        BitDepth = item.BitDepth,
        Hdr = item.Hdr,
        Default = item.Default,
        Forced = item.Forced
    };

    private static MkvTrackItem ToTrackItem(MediaTrack track) => new()
    {
        MkvMergeId = track.MkvMergeId,
        PropEditTrackNumber = track.PropEditTrackNumber,
        Type = track.Type,
        Codec = track.Codec,
        Language = track.Language,
        Name = track.Name,
        Resolution = track.Resolution,
        BitDepth = track.BitDepth,
        Hdr = track.Hdr,
        Default = track.Default,
        Forced = track.Forced
    };

    private static MediaAttachment FromAttachmentItem(MkvAttachmentItem item) => new()
    {
        Id = item.Id,
        FileName = item.FileName,
        ContentType = item.ContentType,
        Description = item.Description,
        SizeBytes = item.SizeBytes
    };

    private static MkvAttachmentItem ToAttachmentItem(MediaAttachment attachment) => new()
    {
        Id = attachment.Id,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        Description = attachment.Description,
        SizeBytes = attachment.SizeBytes
    };
}
