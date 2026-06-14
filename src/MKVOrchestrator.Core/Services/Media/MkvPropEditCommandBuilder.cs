using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed record MkvPropEditCommandBuildRequest(
    MkvFileItem File,
    IReadOnlyList<PropEditTrackConfig> AudioConfigs,
    IReadOnlyList<PropEditTrackConfig> SubtitleConfigs,
    string SelectedDefaultAudio,
    string SelectedForcedAudio,
    string SelectedDefaultSubtitle,
    string SelectedForcedSubtitle,
    bool ContainerTitleFromFile,
    bool ContainerTitleCustom,
    bool RemoveContainerTitle,
    string CustomContainerTitle,
    bool VideoTitleFromFile,
    bool VideoTitleCustom,
    bool RemoveVideoTitle,
    string CustomVideoTitle);

public sealed record MkvPropEditCommandBuildResult(
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> Descriptions);

/// <summary>
/// Single assembly point for mkvpropedit arguments. Container title, track names,
/// languages, default flags, and forced flags should flow through this builder so
/// selector semantics remain consistent.
/// </summary>
public sealed class MkvPropEditCommandBuilder
{
    public MkvPropEditCommandBuildResult Build(MkvPropEditCommandBuildRequest request)
    {
        var args = new List<string> { request.File.FilePath };
        var descriptions = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(request.File.FilePath);

        AddContainerTitleEdits(request, baseName, args, descriptions);
        AddVideoTitleEdits(request, baseName, args, descriptions);
        AddTrackMetadataEdits(request.File, request.AudioConfigs, request.SelectedDefaultAudio, args, descriptions);
        AddTrackMetadataEdits(request.File, request.SubtitleConfigs, request.SelectedDefaultSubtitle, args, descriptions);
        AddForcedFlagEdits(request.File, request.AudioConfigs, request.SelectedForcedAudio, args, descriptions);
        AddForcedFlagEdits(request.File, request.SubtitleConfigs, request.SelectedForcedSubtitle, args, descriptions);

        return new MkvPropEditCommandBuildResult(args, descriptions);
    }

    private static void AddContainerTitleEdits(MkvPropEditCommandBuildRequest request, string baseName, List<string> args, List<string> descriptions)
    {
        var currentTitle = request.File.ContainerTitle ?? string.Empty;
        if (request.ContainerTitleFromFile)
        {
            if (!string.Equals(currentTitle, baseName, StringComparison.Ordinal))
            {
                args.AddRange(new[] { "--edit", "info", "--set", $"title={baseName}" });
                descriptions.Add("Set MKV container title from file name");
            }
        }
        else if (request.ContainerTitleCustom)
        {
            var customTitle = request.CustomContainerTitle ?? string.Empty;
            if (!string.Equals(currentTitle, customTitle, StringComparison.Ordinal))
            {
                args.AddRange(new[] { "--edit", "info", "--set", $"title={customTitle}" });
                descriptions.Add("Set custom MKV container title");
            }
        }
        else if (request.RemoveContainerTitle && !string.IsNullOrWhiteSpace(currentTitle))
        {
            args.AddRange(new[] { "--edit", "info", "--delete", "title" });
            descriptions.Add("Remove MKV container title");
        }
    }

    private static void AddVideoTitleEdits(MkvPropEditCommandBuildRequest request, string baseName, List<string> args, List<string> descriptions)
    {
        var videoTrack = request.File.Tracks.FirstOrDefault(t => MkvTrackSelector.NormalizeTrackType(t.Type) == MkvTrackSelector.VideoType);
        if (videoTrack is null) return;

        var selector = MkvTrackSelector.ForMkvPropEdit(videoTrack, request.File.Tracks.ToList());
        if (request.VideoTitleFromFile)
        {
            if (!string.Equals(videoTrack.Name ?? string.Empty, baseName, StringComparison.Ordinal))
            {
                args.AddRange(new[] { "--edit", selector, "--set", $"name={baseName}" });
                descriptions.Add($"Set video track name from file name");
            }
        }
        else if (request.VideoTitleCustom)
        {
            var customTitle = request.CustomVideoTitle ?? string.Empty;
            if (!string.Equals(videoTrack.Name ?? string.Empty, customTitle, StringComparison.Ordinal))
            {
                args.AddRange(new[] { "--edit", selector, "--set", $"name={customTitle}" });
                descriptions.Add("Set custom video track name");
            }
        }
        else if (request.RemoveVideoTitle && !string.IsNullOrWhiteSpace(videoTrack.Name))
        {
            args.AddRange(new[] { "--edit", selector, "--delete", "name" });
            descriptions.Add("Remove video track name");
        }
    }

    private static void AddTrackMetadataEdits(
        MkvFileItem file,
        IReadOnlyList<PropEditTrackConfig> configs,
        string selectedDefault,
        List<string> args,
        List<string> descriptions)
    {
        foreach (var config in configs)
        {
            var track = FindTrack(file, config);
            if (track is null) continue;

            var selector = MkvTrackSelector.ForMkvPropEdit(track, file.Tracks.ToList());
            var newName = config.EditedName?.Trim() ?? string.Empty;
            var newLang = config.EditedLanguage?.Trim() ?? string.Empty;

            if (!string.Equals(newName, track.Name ?? string.Empty, StringComparison.Ordinal))
            {
                args.AddRange(new[] { "--edit", selector });
                if (string.IsNullOrWhiteSpace(newName))
                {
                    args.AddRange(new[] { "--delete", "name" });
                    descriptions.Add($"Remove {config.TrackLabel} name");
                }
                else
                {
                    args.AddRange(new[] { "--set", $"name={newName}" });
                    descriptions.Add($"Set {config.TrackLabel} name to '{newName}'");
                }
            }

            if (!string.IsNullOrWhiteSpace(newLang) && !string.Equals(newLang, track.Language ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                args.AddRange(new[] { "--edit", selector, "--set", $"language={newLang}" });
                descriptions.Add($"Set {config.TrackLabel} language to '{newLang}'");
            }
        }

        if (string.IsNullOrWhiteSpace(selectedDefault) || string.Equals(selectedDefault, "Keep existing", StringComparison.OrdinalIgnoreCase)) return;

        foreach (var config in configs)
        {
            var track = FindTrack(file, config);
            if (track is null) continue;

            var isDefault = string.Equals(config.TrackLabel, selectedDefault, StringComparison.OrdinalIgnoreCase);
            if (track.Default == isDefault) continue;

            var selector = MkvTrackSelector.ForMkvPropEdit(track, file.Tracks.ToList());
            args.AddRange(new[] { "--edit", selector, "--set", $"flag-default={(isDefault ? "1" : "0")}" });
            descriptions.Add($"Set {config.TrackLabel} default flag to {isDefault}");
        }
    }

    private static void AddForcedFlagEdits(
        MkvFileItem file,
        IReadOnlyList<PropEditTrackConfig> configs,
        string selectedForced,
        List<string> args,
        List<string> descriptions)
    {
        if (string.IsNullOrWhiteSpace(selectedForced) || string.Equals(selectedForced, "Keep existing", StringComparison.OrdinalIgnoreCase)) return;

        foreach (var config in configs)
        {
            var track = FindTrack(file, config);
            if (track is null) continue;

            var shouldBeForced = !string.Equals(selectedForced, "None", StringComparison.OrdinalIgnoreCase)
                && string.Equals(config.TrackLabel, selectedForced, StringComparison.OrdinalIgnoreCase);
            var selector = MkvTrackSelector.ForMkvPropEdit(track, file.Tracks.ToList());
            args.AddRange(new[] { "--edit", selector, "--set", $"flag-forced={(shouldBeForced ? "1" : "0")}" });
            descriptions.Add($"Set {config.TrackLabel} forced flag to {(shouldBeForced ? "enabled" : "disabled")}");
        }
    }

    private static MkvTrackItem? FindTrack(MkvFileItem file, PropEditTrackConfig config)
    {
        return file.Tracks.FirstOrDefault(t =>
            MkvTrackSelector.NormalizeTrackType(t.Type) == MkvTrackSelector.NormalizeTrackType(config.Type) &&
            t.PropEditTrackNumber == config.TrackNumber);
    }
}
