using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace MKVOrchestrator.Core.Models;

public sealed class LibraryAuditSeasonItem : INotifyPropertyChanged
{
    private bool _hasIssues;
    private string _status = "standard";
    private string _issueSummary = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WatchRoot { get; set; } = string.Empty;
    public string ShowName { get; set; } = string.Empty;
    public string SeasonFolder { get; set; } = string.Empty;
    public string RelativeFolder { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public string StandardVideo { get; set; } = string.Empty;
    public string StandardAudio { get; set; } = string.Empty;
    public string StandardSubtitles { get; set; } = string.Empty;
    public ObservableCollection<string> Issues { get; } = new();
    public ObservableCollection<string> IssueFilePaths { get; } = new();
    public ObservableCollection<string> AllFilePaths { get; } = new();
    public string TemplateFilePath { get; set; } = string.Empty;
    public string TemplateFileName => string.IsNullOrWhiteSpace(TemplateFilePath) ? string.Empty : System.IO.Path.GetFileName(TemplateFilePath);
    public string DashboardPullSummary
    {
        get
        {
            if (IssueFilePaths.Count > 0)
            {
                return $"Will send {IssueFilePaths.Count} mismatched file(s) plus template: {TemplateFileName}";
            }

            if (HasIssues && AllFilePaths.Count > 0)
            {
                return $"No specific mismatched file list was available; will send the whole folder set ({AllFilePaths.Count} file(s)) plus template: {TemplateFileName}";
            }

            return "No mismatched files available to send.";
        }
    }
    public VisualState RowVisualState => HasIssues ? VisualState.Warning : VisualState.Normal;

    public bool HasIssues { get => _hasIssues; set { if (_hasIssues == value) return; _hasIssues = value; OnPropertyChanged(); OnPropertyChanged(nameof(RowVisualState)); } }
    public string Status { get => _status; set { if (_status == value) return; _status = value; OnPropertyChanged(); } }
    public string IssueSummary { get => _issueSummary; set { if (_issueSummary == value) return; _issueSummary = value; OnPropertyChanged(); } }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record LibraryAuditIssueLine(string Prefix, string Highlight, string Suffix)
{
    private static readonly Regex MismatchPattern = new(@"^(.*? mismatch \()(.+?)(\s+vs\s+.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EpisodePattern = new(@"^(duplicate episode numbers: |possible missing episode numbers: )(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LibraryAuditIssueLine FromText(string issue)
    {
        if (string.IsNullOrWhiteSpace(issue)) return new LibraryAuditIssueLine(string.Empty, string.Empty, string.Empty);

        var mismatch = MismatchPattern.Match(issue);
        if (mismatch.Success)
        {
            return new LibraryAuditIssueLine(mismatch.Groups[1].Value, mismatch.Groups[2].Value, mismatch.Groups[3].Value);
        }

        var episode = EpisodePattern.Match(issue);
        if (episode.Success)
        {
            return new LibraryAuditIssueLine(episode.Groups[1].Value, episode.Groups[2].Value, string.Empty);
        }

        return new LibraryAuditIssueLine(issue, string.Empty, string.Empty);
    }
}

public sealed record LibraryAuditResult(int Shows, int SeasonFolders, int Files, int IssueGroups, int UncachedFiles);
