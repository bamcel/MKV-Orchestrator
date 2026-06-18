namespace MKVOrchestrator.Core.Models;

public sealed class RenameBatchRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UndoneAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public List<RenameBatchEntry> Entries { get; set; } = new();
    public bool IsUndone => UndoneAt.HasValue;
    public string DisplayName => $"{CreatedAt:yyyy-MM-dd HH:mm:ss} - {TotalFiles} file(s){(IsUndone ? " - undone" : string.Empty)}";
}

public sealed class RenameBatchEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string RenamedPath { get; set; } = string.Empty;
    public string OriginalFileName => Path.GetFileName(OriginalPath);
    public string RenamedFileName => Path.GetFileName(RenamedPath);
}

public sealed class RenameBatchUndoResult
{
    public int Renamed { get; set; }
    public int Skipped { get; set; }
    public List<string> Lines { get; } = new();
}

public sealed class RenameBatchUndoPreview
{
    public int Restorable { get; set; }
    public int Skipped { get; set; }
    public List<string> Lines { get; } = new();
    public bool HasSkippedFiles => Skipped > 0;
}
