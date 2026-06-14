using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.App.Views.Shared;

internal static class FileGridSelectionHelper
{
    public static void ToggleSelectedRowsOnSpace(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || sender is not DataGrid grid)
        {
            return;
        }

        var selectedFiles = grid.SelectedItems
            .OfType<MkvFileItem>()
            .ToList();

        if (selectedFiles.Count == 0 && grid.SelectedItem is MkvFileItem currentFile)
        {
            selectedFiles.Add(currentFile);
        }

        if (selectedFiles.Count == 0)
        {
            return;
        }

        var newUseValue = !selectedFiles.All(file => file.Selected);

        foreach (var file in selectedFiles)
        {
            file.Selected = newUseValue;
        }

        e.Handled = true;
    }

    public static void HandleFileGridKeyDown(object? sender, KeyEventArgs e, ObservableCollection<MkvFileItem> files, Action<MkvFileItem?>? setSelectedFile = null)
    {
        if (e.Key == Key.Space)
        {
            ToggleSelectedRowsOnSpace(sender, e);
            return;
        }

        if (e.Key == Key.Delete)
        {
            RemoveSelectedRows(sender, e, files, setSelectedFile);
        }
    }

    public static void ApplyClickedUseValueToSelectedRows(object? sender, RoutedEventArgs e, DataGrid grid)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not MkvFileItem clickedFile)
        {
            return;
        }

        var selectedFiles = grid.SelectedItems
            .OfType<MkvFileItem>()
            .ToList();

        if (selectedFiles.Count <= 1 || !selectedFiles.Contains(clickedFile))
        {
            return;
        }

        var newUseValue = checkBox.IsChecked == true;
        foreach (var file in selectedFiles)
        {
            file.Selected = newUseValue;
        }

        e.Handled = true;
    }

    private static void RemoveSelectedRows(object? sender, KeyEventArgs e, ObservableCollection<MkvFileItem> files, Action<MkvFileItem?>? setSelectedFile)
    {
        if (sender is not DataGrid grid || files.Count == 0)
        {
            return;
        }

        var selectedFiles = grid.SelectedItems
            .OfType<MkvFileItem>()
            .Distinct()
            .Where(files.Contains)
            .ToList();

        if (selectedFiles.Count == 0 && grid.SelectedItem is MkvFileItem currentFile && files.Contains(currentFile))
        {
            selectedFiles.Add(currentFile);
        }

        if (selectedFiles.Count == 0)
        {
            return;
        }

        var nextIndex = files.IndexOf(selectedFiles[0]);

        foreach (var file in selectedFiles)
        {
            files.Remove(file);
        }

        var nextSelection = files.Count == 0
            ? null
            : files[Math.Clamp(nextIndex, 0, files.Count - 1)];

        grid.SelectedItem = nextSelection;
        setSelectedFile?.Invoke(nextSelection);
        e.Handled = true;
    }
}
