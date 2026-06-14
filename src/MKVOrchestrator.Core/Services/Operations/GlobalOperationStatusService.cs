namespace MKVOrchestrator.Core.Services.Operations;

public sealed class GlobalOperationStatusService
{
    public string CurrentOperation { get; private set; } = "Ready";
    public int Completed { get; private set; }
    public int Total { get; private set; }
    public string CurrentItem { get; private set; } = string.Empty;
    public string CurrentText { get; private set; } = "Ready";
    public string ProgressText { get; private set; } = "Ready";
    public double ProgressPercent { get; private set; }
    public bool HasDeterminateProgress => Total > 0;

    public string Begin(string operation, int total = 0, string currentItem = "")
    {
        CurrentOperation = string.IsNullOrWhiteSpace(operation) ? "Operation" : operation.Trim();
        Completed = 0;
        Total = Math.Max(0, total);
        CurrentItem = currentItem ?? string.Empty;
        UpdateProgressDetail();
        CurrentText = Format("executing");
        return CurrentText;
    }

    public string Step(int completed, int total, string currentItem = "")
    {
        Completed = Math.Max(0, completed);
        Total = Math.Max(0, total);
        CurrentItem = currentItem ?? string.Empty;
        UpdateProgressDetail();
        CurrentText = Format("executing");
        return CurrentText;
    }

    public string Complete(string? summary = null)
    {
        if (Total > 0)
        {
            Completed = Total;
            ProgressPercent = 100;
            ProgressText = $"{Total}/{Total} files - 100%";
        }
        else
        {
            ProgressText = "Complete";
        }

        CurrentText = string.IsNullOrWhiteSpace(summary)
            ? $"{CurrentOperation} complete"
            : summary;
        return CurrentText;
    }

    public string Fail(string message)
    {
        ProgressText = "Failed";
        CurrentText = $"{CurrentOperation} failed: {message}";
        return CurrentText;
    }

    private void UpdateProgressDetail()
    {
        if (Total <= 0)
        {
            ProgressPercent = 0;
            ProgressText = "Working...";
            return;
        }

        var boundedCompleted = Math.Clamp(Completed, 0, Total);
        ProgressPercent = Total == 0 ? 0 : Math.Round((double)boundedCompleted / Total * 100, 1);
        ProgressText = $"{boundedCompleted}/{Total} files - {ProgressPercent:0.#}%";
    }

    private string Format(string state)
    {
        var count = Total > 0 ? $" {Completed}/{Total}" : string.Empty;
        var item = string.IsNullOrWhiteSpace(CurrentItem) ? string.Empty : $" - {CurrentItem}";
        return $"{CurrentOperation} {state}{count}{item}";
    }
}
