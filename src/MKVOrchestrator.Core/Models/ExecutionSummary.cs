using System;
using System.Collections.Generic;
using System.Linq;

namespace MKVOrchestrator.Core.Models;

public sealed class ExecutionSummary
{
    public string Workflow { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public List<ExecutionJob> Jobs { get; } = new();

    public int Total => Jobs.Count;
    public int Completed => Jobs.Count(j => j.Status == ExecutionJobStatus.Completed);
    public int Failed => Jobs.Count(j => j.Status == ExecutionJobStatus.Failed);
    public int Skipped => Jobs.Count(j => j.Status == ExecutionJobStatus.Skipped);
    public int Pending => Jobs.Count(j => j.Status == ExecutionJobStatus.Pending);
    public int Running => Jobs.Count(j => j.Status == ExecutionJobStatus.Running);
    public int Canceled => Jobs.Count(j => j.Status == ExecutionJobStatus.Canceled);
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    public IEnumerable<string> ToConsoleLines()
    {
        yield return $"Execution Summary - {Workflow}";
        yield return $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        yield return $"Total: {Total} | Completed: {Completed} | Failed: {Failed} | Skipped: {Skipped} | Canceled: {Canceled}";
        if (Duration.HasValue) yield return $"Duration: {Duration.Value:hh\\:mm\\:ss}";
        yield return new string('=', 72);
        foreach (var job in Jobs)
        {
            var status = job.Status.ToString().ToUpperInvariant();
            var detail = string.IsNullOrWhiteSpace(job.Result) ? job.Description : job.Result;
            yield return $"{status,-9} {job.FileName}";
            if (!string.IsNullOrWhiteSpace(detail)) yield return $"  {detail}";
        }
    }
}
