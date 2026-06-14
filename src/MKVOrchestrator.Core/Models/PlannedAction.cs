namespace MKVOrchestrator.Core.Models;

public sealed class PlannedAction
{
    public string FilePath { get; set; } = string.Empty;
    public string Tool { get; set; } = "mkvpropedit";
    public string Description { get; set; } = string.Empty;
    public List<string> Arguments { get; } = new();
}
