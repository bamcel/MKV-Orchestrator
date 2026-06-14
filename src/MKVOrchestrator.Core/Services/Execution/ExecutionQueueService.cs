using System;
using System.Collections.ObjectModel;
using System.Linq;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class ExecutionQueueService
{
    public ObservableCollection<ExecutionJob> Jobs { get; } = new();
    public ExecutionSummary CurrentSummary { get; private set; } = new();

    public ExecutionSummary BeginWorkflow(string workflow, IEnumerable<ExecutionJob> jobs)
    {
        Jobs.Clear();
        CurrentSummary = new ExecutionSummary
        {
            Workflow = workflow,
            StartedAt = DateTime.Now
        };

        foreach (var job in jobs)
        {
            job.Workflow = workflow;
            job.Status = ExecutionJobStatus.Pending;
            job.ProgressPercent = 0;
            job.Result = string.Empty;
            job.CreatedAt = DateTime.Now;
            CurrentSummary.Jobs.Add(job);
            Jobs.Add(job);
        }

        return CurrentSummary;
    }

    public void MarkRunning(ExecutionJob job)
    {
        job.Status = ExecutionJobStatus.Running;
        job.StartedAt = DateTime.Now;
    }

    public void UpdateProgress(ExecutionJob job, int percent)
    {
        job.ProgressPercent = Math.Clamp(percent, 0, 100);
    }

    public void Complete(ExecutionJob job, string result = "SUCCESS")
    {
        job.Status = ExecutionJobStatus.Completed;
        job.ProgressPercent = 100;
        job.Result = result;
        job.CompletedAt = DateTime.Now;
    }

    public void Fail(ExecutionJob job, string result)
    {
        job.Status = ExecutionJobStatus.Failed;
        job.Result = result;
        job.CompletedAt = DateTime.Now;
    }

    public void Skip(ExecutionJob job, string result)
    {
        job.Status = ExecutionJobStatus.Skipped;
        job.Result = result;
        job.CompletedAt = DateTime.Now;
    }

    public void CancelPending(string reason = "Canceled")
    {
        foreach (var job in Jobs.Where(j => j.Status is ExecutionJobStatus.Pending or ExecutionJobStatus.Running))
        {
            job.Status = ExecutionJobStatus.Canceled;
            job.Result = reason;
            job.CompletedAt = DateTime.Now;
        }
        CompleteWorkflow();
    }

    public ExecutionSummary CompleteWorkflow()
    {
        CurrentSummary.CompletedAt = DateTime.Now;
        return CurrentSummary;
    }
}
