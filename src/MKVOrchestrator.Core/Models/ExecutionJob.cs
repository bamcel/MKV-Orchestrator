using System;

namespace MKVOrchestrator.Core.Models;

public sealed class ExecutionJob
{
    public string Workflow { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName => string.IsNullOrWhiteSpace(FilePath) ? string.Empty : System.IO.Path.GetFileName(FilePath);
    public string Description { get; set; } = string.Empty;
    public ExecutionJobStatus Status { get; set; } = ExecutionJobStatus.Pending;
    public int ProgressPercent { get; set; }
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue ? CompletedAt.Value - StartedAt.Value : null;
}
