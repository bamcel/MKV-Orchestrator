using CommunityToolkit.Mvvm.Input;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.App.ViewModels;

public partial class MainWindowViewModel
{
    partial void OnSelectedLibraryAuditItemChanged(LibraryAuditSeasonItem? value)
    {
        SelectedLibraryAuditIssueLines.Clear();
        if (value is null) return;
        LibraryAuditDetailSummary = value.DashboardPullSummary;
        foreach (var issue in value.Issues)
        {
            SelectedLibraryAuditIssueLines.Add(LibraryAuditIssueLine.FromText(issue));
        }
    }

    partial void OnShowLibraryWarningsOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(LibraryWarningFilterButtonText));
        RefreshDisplayedLibraryItems();
    }

    partial void OnSelectedLibraryAuditWatchFolderChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            LibraryAuditStatusText = $"Ready to build library overview: {value}";
        }
    }

    [RelayCommand]
    private void RefreshLibraryAuditWatchFolders()
    {
        RefreshLibraryAuditWatchFolderOptions();
    }

    [RelayCommand]
    private async Task RunLibraryAudit()
    {
        if (IsLibraryAuditBusy) return;
        RefreshLibraryAuditWatchFolderOptions();

        var root = SelectedLibraryAuditWatchFolder;
        if (string.IsNullOrWhiteSpace(root) && LibraryAuditWatchFolderOptions.Count > 0)
        {
            root = LibraryAuditWatchFolderOptions[0];
            SelectedLibraryAuditWatchFolder = root;
        }

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            LibraryAuditStatusText = "No valid watch folder selected.";
            Log(LibraryAuditStatusText);
            return;
        }

        _auditCts?.Cancel();
        _auditCts?.Dispose();
        _auditCts = new CancellationTokenSource();

        IsLibraryAuditBusy = true;
        LibraryAuditStatusText = "Building library overview from cache...";
        BeginGlobalOperation("library overview");
        SelectedLibraryAuditIssueLines.Clear();
        try
        {
            var ignored = ParseIgnoredScanFolderNames(IgnoredScanFolderNameText).ToList();
            var auditRows = new List<LibraryAuditSeasonItem>();
            var result = await Task.Run(() =>
            {
                _auditCts.Token.ThrowIfCancellationRequested();
                var auditResult = _libraryAudit.BuildAudit(root, ignored, auditRows);
                _auditCts.Token.ThrowIfCancellationRequested();
                return auditResult;
            }, _auditCts.Token);
            LibraryAuditItems.Clear();
            foreach (var row in auditRows)
            {
                LibraryAuditItems.Add(row);
            }
            RefreshDisplayedLibraryItems();
            LibraryAuditStatusText = $"Library overview ready: {result.Shows} shows, {result.SeasonFolders} folders, {result.Files} files, {result.IssueGroups} warning groups, {result.UncachedFiles} uncached files.";
            SelectedLibraryAuditItem = null;
            SelectedLibraryAuditIssueLines.Clear();
            LibraryAuditDetailSummary = $"Build Overview: {result.Shows} shows | {result.SeasonFolders} folders | {result.Files} files | {result.IssueGroups} warning groups | {result.UncachedFiles} uncached files. Select a row to review warning details.";
            CompleteGlobalOperation(LibraryAuditStatusText);
            Log(LibraryAuditStatusText);
        }
        catch (OperationCanceledException)
        {
            LibraryAuditStatusText = "Library overview canceled.";
            CompleteGlobalOperation(LibraryAuditStatusText);
            Log(LibraryAuditStatusText);
        }
        catch (Exception ex)
        {
            LibraryAuditStatusText = $"Library overview failed: {ex.Message}";
            FailGlobalOperation(ex.Message);
            Log(LibraryAuditStatusText);
        }
        finally
        {
            IsLibraryAuditBusy = false;
            _auditCts?.Dispose();
            _auditCts = null;
        }
    }

    private void RefreshLibraryAuditWatchFolderOptions()
    {
        var current = SelectedLibraryAuditWatchFolder;
        LibraryAuditWatchFolderOptions.Clear();
        foreach (var root in ParseWatchFolderText(WatchFolderText).Where(Directory.Exists))
        {
            LibraryAuditWatchFolderOptions.Add(root);
        }

        if (!string.IsNullOrWhiteSpace(current) && LibraryAuditWatchFolderOptions.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            SelectedLibraryAuditWatchFolder = current;
        }
        else if (LibraryAuditWatchFolderOptions.Count > 0)
        {
            SelectedLibraryAuditWatchFolder = LibraryAuditWatchFolderOptions[0];
        }
        else
        {
            SelectedLibraryAuditWatchFolder = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleLibraryWarningsOnly()
    {
        ShowLibraryWarningsOnly = !ShowLibraryWarningsOnly;
    }

    private void RefreshDisplayedLibraryItems()
    {
        var selected = SelectedLibraryAuditItem;
        DisplayedLibraryAuditItems.Clear();
        foreach (var item in LibraryAuditItems.Where(item => !ShowLibraryWarningsOnly || item.HasIssues))
        {
            DisplayedLibraryAuditItems.Add(item);
        }

        if (selected is not null && DisplayedLibraryAuditItems.Contains(selected))
        {
            SelectedLibraryAuditItem = selected;
        }
        else
        {
            SelectedLibraryAuditItem = DisplayedLibraryAuditItems.FirstOrDefault();
        }
    }

    [RelayCommand]
    private async Task SendSelectedAuditIssuesToDashboard()
    {
        var audit = SelectedLibraryAuditItem;
        if (audit is null)
        {
            LibraryAuditStatusText = "Select a library row first.";
            Log(LibraryAuditStatusText);
            return;
        }

        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(audit.TemplateFilePath))
        {
            paths.Add(audit.TemplateFilePath);
        }

        var issuePaths = audit.IssueFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(CrossPlatformRuntime.PathComparer)
            .ToList();

        // Some audit warnings, such as possible missing episode numbers, do not point to a single bad file.
        // In that case, send the whole selected folder set so the Dashboard can still be used for remediation.
        if (issuePaths.Count == 0 && audit.HasIssues)
        {
            issuePaths = audit.AllFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(CrossPlatformRuntime.PathComparer)
                .ToList();
        }

        paths.AddRange(issuePaths);
        var missingPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            .Distinct(CrossPlatformRuntime.PathComparer)
            .ToList();

        paths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(CrossPlatformRuntime.PathComparer)
            .ToList();

        if (paths.Count == 0)
        {
            LibraryAuditStatusText = missingPaths.Count > 0
                ? $"No library files could be found on disk. Missing paths: {missingPaths.Count}. Try refreshing the watch folder cache."
                : "No cached issue/template files are available to send to Dashboard.";
            Log(LibraryAuditStatusText);
            return;
        }

        _auditCts?.Cancel();
        _auditCts?.Dispose();
        _auditCts = new CancellationTokenSource();

        IsLibraryAuditBusy = true;
        LibraryAuditStatusText = $"Sending {paths.Count} file(s) to Dashboard...";
        try
        {
            var loaded = new List<MkvFileItem>();
            foreach (var path in paths)
            {
                var media = _mediaCache.TryGetValid(path);
                if (media is null)
                {
                    media = await _mediaLibrary.ScanFileAsync(path, MkvMergePath, FfProbePath, _auditCts.Token, forceRefresh: false);
                }

                if (media is null) continue;

                var item = MkvFileItem.FromMediaFile(media);
                item.Selected = true;
                item.Status = CrossPlatformRuntime.PathComparer.Equals(path, audit.TemplateFilePath)
                    ? "Template"
                    : "Library Warning";
                loaded.Add(item);
            }

            if (loaded.Count == 0)
            {
                LibraryAuditStatusText = "No files could be loaded into Dashboard.";
                Log(LibraryAuditStatusText);
                return;
            }

            AppState.ClearScanCollections();
            var folder = string.IsNullOrWhiteSpace(audit.RelativeFolder)
                ? audit.WatchRoot
                : Path.Combine(audit.WatchRoot, audit.RelativeFolder);
            FolderPath = Directory.Exists(folder) ? folder : audit.WatchRoot;
            AppState.BeginScan(FolderPath);

            foreach (var file in loaded)
            {
                InsertFileSorted(file);
            }

            var loadedIssueCount = issuePaths.Count(path => paths.Any(p => CrossPlatformRuntime.PathComparer.Equals(p, path)));
            var loadedTemplateCount = !string.IsNullOrWhiteSpace(audit.TemplateFilePath)
                && paths.Any(path => CrossPlatformRuntime.PathComparer.Equals(path, audit.TemplateFilePath))
                    ? 1
                    : 0;

            RefreshDashboardSelection(Files.FirstOrDefault());
            EvaluateTrackTemplateDeviations();

            // Preserve the audit import role after the normal dashboard mismatch pass,
            // because that pass intentionally rewrites Status to Ready/Warning.
            foreach (var file in Files)
            {
                if (CrossPlatformRuntime.PathComparer.Equals(file.FilePath, audit.TemplateFilePath))
                {
                    file.Status = "Template";
                }
                else if (issuePaths.Any(path => CrossPlatformRuntime.PathComparer.Equals(path, file.FilePath)))
                {
                    file.Status = "Library Warning";
                }
            }

            BuildDashboardMismatchReport();
            SyncRenameFromDashboardSelection(preserveSearchTitle: true, writeLog: false);
            AppState.CompleteOperation($"Loaded library selection: {Files.Count} file(s)");

            var fallbackNote = audit.IssueFilePaths.Count == 0 && audit.HasIssues ? " using full selected folder set" : string.Empty;
            StatusText = $"Loaded {loadedIssueCount} issue file(s) and {loadedTemplateCount} template file into Dashboard{fallbackNote}.";
            LibraryAuditStatusText = StatusText;
            Log(StatusText);
        }
        catch (OperationCanceledException)
        {
            LibraryAuditStatusText = "Send to Dashboard canceled.";
            Log(LibraryAuditStatusText);
        }
        catch (Exception ex)
        {
            LibraryAuditStatusText = $"Send to Dashboard failed: {ex.Message}";
            Log(LibraryAuditStatusText);
        }
        finally
        {
            IsLibraryAuditBusy = false;
            _auditCts?.Dispose();
            _auditCts = null;
        }
    }
}
