using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.App.ViewModels;

public partial class MainWindowViewModel
{
    public void LogDashboardMessage(string message) => Log(message);

    private void Log(string message) => ConsoleLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    private void RenameLog(string message) => RenameConsoleLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    private void BeginGlobalOperation(string operation, int total = 0, string currentItem = "")
    {
        var text = _operationStatus.Begin(operation, total, currentItem);
        RefreshGlobalProgress(total <= 0);
        StatusText = text;
        ExecutionStatusText = $"Execution Center: {text}";
    }

    private void UpdateGlobalOperation(int completed, int total, string currentItem = "")
    {
        var text = _operationStatus.Step(completed, total, currentItem);
        RefreshGlobalProgress(total <= 0);
        StatusText = text;
        ExecutionStatusText = $"Execution Center: {text}";
    }

    private void CompleteGlobalOperation(string summary)
    {
        var text = _operationStatus.Complete(summary);
        RefreshGlobalProgress(indeterminate: false);
        StatusText = text;
        ExecutionStatusText = $"Execution Center: {text}";
    }

    private void FailGlobalOperation(string message)
    {
        var text = _operationStatus.Fail(message);
        RefreshGlobalProgress(indeterminate: false);
        StatusText = text;
        ExecutionStatusText = $"Execution Center: {text}";
    }

    private void RefreshGlobalProgress(bool indeterminate)
    {
        GlobalProgressText = _operationStatus.ProgressText;
        GlobalProgressValue = _operationStatus.ProgressPercent;
        IsGlobalProgressIndeterminate = indeterminate;
    }
}
