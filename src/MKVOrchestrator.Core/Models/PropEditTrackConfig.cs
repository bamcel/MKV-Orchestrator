using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

namespace MKVOrchestrator.Core.Models;

public partial class PropEditTrackConfig : ObservableObject
{
    public int TrackNumber { get; set; }
    public string TrackLabel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string CurrentName { get; set; } = string.Empty;
    public string CurrentLanguage { get; set; } = string.Empty;
    public bool CurrentDefault { get; set; }

    [ObservableProperty] private string editedName = string.Empty;
    [ObservableProperty] private string selectedNamePreset = string.Empty;
    [ObservableProperty] private bool useCustomName;
    [ObservableProperty] private string customName = string.Empty;
    [ObservableProperty] private string editedLanguage = string.Empty;

    public ObservableCollection<string> NameOptions { get; } = new();
    public ObservableCollection<string> LanguageOptions { get; } = new();

    partial void OnSelectedNamePresetChanged(string value)
    {
        if (!UseCustomName && !string.IsNullOrWhiteSpace(value))
        {
            EditedName = value;
        }
    }

    partial void OnUseCustomNameChanged(bool value)
    {
        if (value)
        {
            if (string.IsNullOrWhiteSpace(CustomName))
            {
                CustomName = EditedName;
            }
            EditedName = CustomName;
        }
        else if (!string.IsNullOrWhiteSpace(SelectedNamePreset))
        {
            EditedName = SelectedNamePreset;
        }
    }

    partial void OnCustomNameChanged(string value)
    {
        if (UseCustomName)
        {
            EditedName = value;
        }
    }

    public string CurrentSummary => $"Name='{CurrentName}' | Lang='{CurrentLanguage}' | Default={CurrentDefault}";
}
