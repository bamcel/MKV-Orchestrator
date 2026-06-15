using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.Core.Models;

public sealed class MkvFileItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _status = "Ready";
    private bool _hasTrackMismatch;
    private string _mismatchSummary = string.Empty;
    private bool _selected = true;
    private VisualState _resolutionVisualState = VisualState.Normal;
    private VisualState _codecVisualState = VisualState.Normal;
    private VisualState _bitDepthVisualState = VisualState.Normal;
    private VisualState _audioSummaryVisualState = VisualState.Normal;
    private VisualState _subtitleSummaryVisualState = VisualState.Normal;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetField(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string FileName => Path.GetFileName(FilePath);
    public string ContainerTitle { get; set; } = string.Empty;
    public string VideoSummary { get; set; } = string.Empty;
    public string AudioSummary { get; set; } = string.Empty;
    public string SubtitleSummary { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string BitDepth { get; set; } = string.Empty;
    public string Hdr { get; set; } = string.Empty;
    public string AttachmentSummary { get; set; } = string.Empty;
    public MediaFile CanonicalMedia { get; set; } = new();

    public string Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(RowVisualState));
            }
        }
    }

    public bool HasTrackMismatch
    {
        get => _hasTrackMismatch;
        set
        {
            if (SetField(ref _hasTrackMismatch, value))
            {
                OnPropertyChanged(nameof(RowVisualState));
            }
        }
    }

    public string MismatchSummary
    {
        get => _mismatchSummary;
        set => SetField(ref _mismatchSummary, value);
    }

    public VisualState RowVisualState => Status.Equals("Template", StringComparison.OrdinalIgnoreCase)
        ? VisualState.Template
        : HasTrackMismatch ? VisualState.Warning : VisualState.Normal;

    public VisualState ResolutionVisualState
    {
        get => _resolutionVisualState;
        set => SetField(ref _resolutionVisualState, value);
    }

    public VisualState CodecVisualState
    {
        get => _codecVisualState;
        set => SetField(ref _codecVisualState, value);
    }

    public VisualState BitDepthVisualState
    {
        get => _bitDepthVisualState;
        set => SetField(ref _bitDepthVisualState, value);
    }

    public VisualState AudioSummaryVisualState
    {
        get => _audioSummaryVisualState;
        set => SetField(ref _audioSummaryVisualState, value);
    }

    public VisualState SubtitleSummaryVisualState
    {
        get => _subtitleSummaryVisualState;
        set => SetField(ref _subtitleSummaryVisualState, value);
    }

    public void ResetDifferenceHighlighting()
    {
        ResolutionVisualState = VisualState.Normal;
        CodecVisualState = VisualState.Normal;
        BitDepthVisualState = VisualState.Normal;
        AudioSummaryVisualState = VisualState.Normal;
        SubtitleSummaryVisualState = VisualState.Normal;

        foreach (var track in Tracks)
        {
            track.ResetDifferenceHighlighting();
        }
    }

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public ObservableCollection<MkvTrackItem> Tracks { get; } = new();
    public ObservableCollection<MkvAttachmentItem> Attachments { get; } = new();

    public MediaFile ToMediaFile() => MediaFileMapper.FromMkvFileItem(this);

    public static MkvFileItem FromMediaFile(MediaFile media) => MediaFileMapper.ToMkvFileItem(media);

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
