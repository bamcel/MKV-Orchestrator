using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services.State;

/// <summary>
/// Central in-memory state for cross-panel coordination.
/// Keep UI collections here so future feature panels can share the same source of truth
/// instead of duplicating selected files, scan progress, and operation status.
/// </summary>
public partial class AppStateService : ObservableObject
{
    private bool _suppressDashboardSelectionChanged;

    public ObservableCollection<MkvFileItem> Files { get; } = new();

    public event EventHandler? DashboardFileSelectionChanged;
    public ObservableCollection<MkvTrackItem> SelectedTracks { get; } = new();
    public ObservableCollection<string> PlannedActions { get; } = new();
    public ObservableCollection<string> ConsoleLines { get; } = new();
    public ObservableCollection<string> DashboardConsoleLines { get; } = new();
    public ObservableCollection<RenamePreviewItem> RenameItems { get; } = new();

    [ObservableProperty] private string currentFolderPath = string.Empty;
    [ObservableProperty] private string currentOperation = "Ready";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private bool canCancel;
    [ObservableProperty] private int scanCompleted;
    [ObservableProperty] private int scanTotal;
    [ObservableProperty] private MkvFileItem? selectedFile;
    [ObservableProperty] private int dashboardSelectedFileCount;
    [ObservableProperty] private string dashboardSelectionStatus = "Linked to Dashboard: 0 selected files";

    public AppStateService()
    {
        Files.CollectionChanged += Files_CollectionChanged;
    }

    public IReadOnlyList<MkvFileItem> GetDashboardRenameSourceFiles()
    {
        return Files.Where(f => f.Selected).ToList();
    }

    public void UpdateDashboardFilePath(MkvFileItem file, string newPath)
    {
        _suppressDashboardSelectionChanged = true;
        try
        {
            file.FilePath = newPath;
        }
        finally
        {
            _suppressDashboardSelectionChanged = false;
        }

        RefreshDashboardSelectionState();
    }

    private void Files_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (MkvFileItem item in e.OldItems)
            {
                item.PropertyChanged -= File_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (MkvFileItem item in e.NewItems)
            {
                item.PropertyChanged += File_PropertyChanged;
            }
        }

        RefreshDashboardSelectionState();
    }

    private void File_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MkvFileItem.Selected) || e.PropertyName == nameof(MkvFileItem.FilePath))
        {
            RefreshDashboardSelectionState();
        }
    }

    private void RefreshDashboardSelectionState()
    {
        DashboardSelectedFileCount = Files.Count(f => f.Selected);
        DashboardSelectionStatus = Files.Count == 0
            ? "Linked to Dashboard: no scanned files"
            : DashboardSelectedFileCount == 0
                ? $"Linked to Dashboard: 0 selected of {Files.Count} scanned file(s)"
                : $"Linked to Dashboard: {DashboardSelectedFileCount} selected of {Files.Count} scanned file(s)";

        if (!_suppressDashboardSelectionChanged)
        {
            DashboardFileSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void BeginOperation(string operationName)
    {
        CurrentOperation = operationName;
        IsBusy = true;
        CanCancel = true;
    }

    public void BeginScan(string folderPath)
    {
        CurrentFolderPath = folderPath;
        ScanCompleted = 0;
        ScanTotal = 0;
        IsScanning = true;
        BeginOperation("Scanning");
    }

    public void UpdateScanProgress(int completed, int total)
    {
        ScanCompleted = completed;
        ScanTotal = total;
    }

    public void CompleteOperation(string statusText = "Ready")
    {
        var completedScan = IsScanning;

        CurrentOperation = statusText;
        IsBusy = false;
        IsScanning = false;
        CanCancel = false;

        // During a scan, files are added while IsScanning is true. Rename live-sync
        // intentionally ignores those interim collection-change events to avoid
        // rebuilding the rename table hundreds of times. Once scanning completes,
        // raise one consolidated dashboard-selection change so MKVRename hydrates
        // from the final checked Dashboard file set automatically.
        if (completedScan)
        {
            RefreshDashboardSelectionState();
        }
    }

    public void ClearScanCollections()
    {
        SelectedFile = null;
        Files.Clear();
        SelectedTracks.Clear();
        PlannedActions.Clear();
        DashboardConsoleLines.Clear();
        RefreshDashboardSelectionState();
    }
}
