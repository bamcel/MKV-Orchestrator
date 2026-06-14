using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MKVOrchestrator.App.Views.Shared;
using MKVOrchestrator.App.ViewModels;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.App.Views.Merge;

public partial class MergePanel : UserControl
{
    public MergePanel()
    {
        InitializeComponent();
    }

    private void FileGrid_KeyDown(object? sender, KeyEventArgs e) =>
        FileGridSelectionHelper.ToggleSelectedRowsOnSpace(sender, e);

    private void UseCheckBox_Click(object? sender, RoutedEventArgs e) =>
        FileGridSelectionHelper.ApplyClickedUseValueToSelectedRows(sender, e, RemuxFilesGrid);

    private void RemuxFilesGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || RemuxFilesGrid.SelectedItem is not MkvFileItem file)
        {
            return;
        }

        // Force the shared selected file to follow the Merge table selection.
        // This keeps the Selected File Tracks panel in sync even with Extended selection enabled.
        vm.SelectedFile = file;
    }
}
