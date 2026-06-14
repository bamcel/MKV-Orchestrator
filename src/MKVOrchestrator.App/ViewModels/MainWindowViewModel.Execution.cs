using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MKVOrchestrator.Core.Services;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.App.ViewModels;

public partial class MainWindowViewModel
{

    public Func<IReadOnlyList<FileConflictResult>, Task<bool>>? ConfirmSkipConflictsAsync { get; set; }

    private sealed record ExecutionConflictCheck(ExecutionJob Job, string SourcePath, string? TargetPath, bool RenameCheck);

    private async Task<bool> ConfirmOrCancelForConflictsAsync(IEnumerable<ExecutionConflictCheck> checks, Action<string> writeLine)
    {
        var conflicts = checks
            .Select(check => new { Check = check, Conflict = check.RenameCheck && !string.IsNullOrWhiteSpace(check.TargetPath)
                ? _fileConflict.CheckRenameTarget(check.SourcePath, check.TargetPath!)
                : _fileConflict.CheckReadableWritable(check.SourcePath, requireWrite: true) })
            .Where(item => !item.Conflict.CanProceed)
            .ToList();

        if (conflicts.Count == 0) return true;

        ExecutionStatusText = $"Execution Center: {conflicts.Count} conflict(s) detected";
        writeLine($"WARNING: {conflicts.Count} file conflict(s) detected before execution.");
        foreach (var item in conflicts.Take(10))
        {
            writeLine($"  CONFLICT: {Path.GetFileName(item.Check.SourcePath)} - {item.Conflict.Reason}");
        }
        if (conflicts.Count > 10)
        {
            writeLine($"  ... plus {conflicts.Count - 10} more conflict(s).");
        }

        RefreshExecutionSummary();

        var skipConflicts = ConfirmSkipConflictsAsync is not null
            ? await ConfirmSkipConflictsAsync(conflicts.Select(item => item.Conflict).ToList())
            : false;

        if (!skipConflicts)
        {
            var reason = "Canceled by user after conflict warning.";
            _executionQueue.CancelPending(reason);
            writeLine("RUN CANCELED: conflict warning was not accepted.");
            ExecutionStatusText = "Execution Center: canceled because conflicts were detected";
            RefreshExecutionSummary();
            return false;
        }

        foreach (var item in conflicts)
        {
            _executionQueue.Skip(item.Check.Job, item.Conflict.Reason);
            writeLine($"SKIPPED CONFLICT: {Path.GetFileName(item.Check.SourcePath)} - {item.Conflict.Reason}");
        }

        ExecutionStatusText = $"Execution Center: skipping {conflicts.Count} conflict(s)";
        RefreshExecutionSummary();
        return true;
    }

    private ExecutionSummary BeginExecutionWorkflow(string workflow, IEnumerable<ExecutionJob> jobs)
    {
        var summary = _executionQueue.BeginWorkflow(workflow, jobs.ToList());
        ExecutionSummaryLines.Clear();
        ExecutionStatusText = $"Execution Center: {workflow} queued ({summary.Total} job(s))";
        foreach (var line in summary.ToConsoleLines()) ExecutionSummaryLines.Add(line);
        return summary;
    }

    private void RefreshExecutionSummary()
    {
        ExecutionSummaryLines.Clear();
        foreach (var line in _executionQueue.CurrentSummary.ToConsoleLines())
        {
            ExecutionSummaryLines.Add(line);
        }
    }

    private void CompleteExecutionWorkflow(string statusText)
    {
        var summary = _executionQueue.CompleteWorkflow();
        ExecutionStatusText = $"Execution Center: {statusText}";
        RefreshExecutionSummary();
        Log($"Execution Center: {summary.Workflow} complete - total {summary.Total}, completed {summary.Completed}, failed {summary.Failed}, skipped {summary.Skipped}, canceled {summary.Canceled}.");
    }

    private ExecutionJob CreateExecutionJob(string workflow, string filePath, string description)
    {
        return new ExecutionJob
        {
            Workflow = workflow,
            FilePath = filePath,
            Description = description
        };
    }

    private bool TryPassFileConflictCheck(ExecutionJob job, string sourcePath, string? targetPath, bool renameCheck, Action<string> writeLine)
    {
        var conflict = renameCheck && !string.IsNullOrWhiteSpace(targetPath)
            ? _fileConflict.CheckRenameTarget(sourcePath, targetPath)
            : _fileConflict.CheckReadableWritable(sourcePath, requireWrite: true);

        if (conflict.CanProceed) return true;

        _executionQueue.Skip(job, conflict.Reason);
        writeLine($"SKIPPED: {Path.GetFileName(sourcePath)} - {conflict.Reason}");
        RefreshExecutionSummary();
        return false;
    }
}
