namespace MKVOrchestrator.Core.Models;

public sealed class EditOptions
{
    public bool RemoveContainerTitle { get; set; } = true;
    public bool RemoveVideoTrackTitles { get; set; } = true;
    public bool RemoveAudioTrackTitles { get; set; }
    public bool RemoveSubtitleTrackTitles { get; set; }
    public string? SetAudioLanguage { get; set; }
    public string? SetSubtitleLanguage { get; set; }
}
