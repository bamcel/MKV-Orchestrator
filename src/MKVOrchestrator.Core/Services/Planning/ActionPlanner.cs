using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class ActionPlanner
{
    public List<PlannedAction> BuildActions(IEnumerable<MkvFileItem> files, EditOptions options)
    {
        var actions = new List<PlannedAction>();
        foreach (var file in files.Where(f => f.Selected))
        {
            var args = new List<string> { file.FilePath };
            var descriptions = new List<string>();

            if (options.RemoveContainerTitle && !string.IsNullOrWhiteSpace(file.ContainerTitle))
            {
                args.AddRange(new[] { "--edit", "info", "--delete", "title" });
                descriptions.Add("Remove container title");
            }

            foreach (var track in file.Tracks)
            {
                var removeTitle = track.Type switch
                {
                    "video" => options.RemoveVideoTrackTitles,
                    "audio" => options.RemoveAudioTrackTitles,
                    "subtitles" => options.RemoveSubtitleTrackTitles,
                    _ => false
                };

                if (removeTitle && !string.IsNullOrWhiteSpace(track.Name))
                {
                    args.AddRange(new[] { "--edit", $"track:{track.PropEditTrackNumber}", "--delete", "name" });
                    descriptions.Add($"Remove {track.Type} title from track {track.PropEditTrackNumber}");
                }

                if (track.Type == "audio" && !string.IsNullOrWhiteSpace(options.SetAudioLanguage))
                {
                    args.AddRange(new[] { "--edit", $"track:{track.PropEditTrackNumber}", "--set", $"language={options.SetAudioLanguage}" });
                    descriptions.Add($"Set audio track {track.PropEditTrackNumber} language to {options.SetAudioLanguage}");
                }

                if (track.Type == "subtitles" && !string.IsNullOrWhiteSpace(options.SetSubtitleLanguage))
                {
                    args.AddRange(new[] { "--edit", $"track:{track.PropEditTrackNumber}", "--set", $"language={options.SetSubtitleLanguage}" });
                    descriptions.Add($"Set subtitle track {track.PropEditTrackNumber} language to {options.SetSubtitleLanguage}");
                }
            }

            if (descriptions.Count > 0)
            {
                var action = new PlannedAction
                {
                    FilePath = file.FilePath,
                    Description = string.Join("; ", descriptions)
                };
                action.Arguments.AddRange(args);
                actions.Add(action);
            }
        }
        return actions;
    }
}
