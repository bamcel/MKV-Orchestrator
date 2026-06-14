using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MKVOrchestrator.Core.Models;

public sealed class MkvTrackItem : INotifyPropertyChanged
{
    private VisualState _trackNumberVisualState = VisualState.Normal;
    private VisualState _typeVisualState = VisualState.Normal;
    private VisualState _languageVisualState = VisualState.Normal;
    private VisualState _codecVisualState = VisualState.Normal;
    private VisualState _nameVisualState = VisualState.Normal;
    private VisualState _detailVisualState = VisualState.Normal;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int MkvMergeId { get; set; }
    public int PropEditTrackNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string BitDepth { get; set; } = string.Empty;
    public string Hdr { get; set; } = string.Empty;
    public VisualState DetailVisualState
    {
        get => _detailVisualState;
        set => SetField(ref _detailVisualState, value);
    }

    public VisualState TrackNumberVisualState
    {
        get => _trackNumberVisualState;
        set => SetField(ref _trackNumberVisualState, value);
    }

    public VisualState TypeVisualState
    {
        get => _typeVisualState;
        set => SetField(ref _typeVisualState, value);
    }

    public VisualState LanguageVisualState
    {
        get => _languageVisualState;
        set => SetField(ref _languageVisualState, value);
    }

    public VisualState CodecVisualState
    {
        get => _codecVisualState;
        set => SetField(ref _codecVisualState, value);
    }

    public VisualState NameVisualState
    {
        get => _nameVisualState;
        set => SetField(ref _nameVisualState, value);
    }
    public bool Default { get; set; }
    public bool Forced { get; set; }
    public string MediaSummary => string.Join(" | ", new[] { Codec, Resolution, BitDepth }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Summary => $"ID {MkvMergeId} {Type} | {Language} | {Codec} | {Name}";

    public void ResetDifferenceHighlighting()
    {
        TrackNumberVisualState = VisualState.Normal;
        TypeVisualState = VisualState.Normal;
        LanguageVisualState = VisualState.Normal;
        CodecVisualState = VisualState.Normal;
        NameVisualState = VisualState.Normal;
        DetailVisualState = VisualState.Normal;
    }

    public void HighlightAllDifferenceFields()
    {
        TrackNumberVisualState = VisualState.Warning;
        TypeVisualState = VisualState.Warning;
        LanguageVisualState = VisualState.Warning;
        CodecVisualState = VisualState.Warning;
        NameVisualState = VisualState.Warning;
        DetailVisualState = VisualState.Warning;
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
