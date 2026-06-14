using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace MKVOrchestrator.Core.Models;

public sealed class RenamePreviewItem : INotifyPropertyChanged
{
    private bool _selected = true;
    private string _newFileName = string.Empty;
    private string _matchedEpisodeTitle = string.Empty;
    private string _confidence = "Pending";
    private string _status = "Awaiting match";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MediaFile MediaFile { get; set; } = new();
    public string FilePath
    {
        get => MediaFile.FilePath;
        set
        {
            if (!string.Equals(MediaFile.FilePath, value, StringComparison.OrdinalIgnoreCase))
            {
                MediaFile.FilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFileName));
            }
        }
    }
    public string CurrentFileName => Path.GetFileName(FilePath);
    public int? Season { get => MediaFile.Season; set => MediaFile.Season = value; }
    public int? Episode { get => MediaFile.Episode; set => MediaFile.Episode = value; }
    public int? AbsoluteEpisode { get => MediaFile.AbsoluteEpisode; set => MediaFile.AbsoluteEpisode = value; }
    public string DetectedEpisode => Season.HasValue && Episode.HasValue ? $"S{Season.Value:00}E{Episode.Value:00}" : "Unknown";
    public string SeriesTitle { get => MediaFile.SeriesTitle; set => MediaFile.SeriesTitle = value; }
    public int? SeriesYear { get; set; }
    public int? TvdbEpisodeId { get => MediaFile.ProviderMatch.EpisodeId; set => MediaFile.ProviderMatch.EpisodeId = value; }

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public string MatchedEpisodeTitle
    {
        get => _matchedEpisodeTitle;
        set => SetField(ref _matchedEpisodeTitle, value);
    }

    public string NewFileName
    {
        get => _newFileName;
        set => SetField(ref _newFileName, value);
    }

    public string Confidence
    {
        get => _confidence;
        set => SetField(ref _confidence, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public void RefreshFileName()
    {
        OnPropertyChanged(nameof(CurrentFileName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
