namespace MKVOrchestrator.Core.Models;

public sealed class MkvAttachmentItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }

    public string SizeDisplay => SizeBytes.HasValue ? FormatSize(SizeBytes.Value) : string.Empty;

    public string Summary
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FileName) ? $"Attachment {Id}" : FileName;
            var type = string.IsNullOrWhiteSpace(ContentType) ? "Unknown type" : ContentType;
            var size = string.IsNullOrWhiteSpace(SizeDisplay) ? string.Empty : $" | {SizeDisplay}";
            return $"#{Id} {name} | {type}{size}";
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kb = bytes / 1024d;
        if (kb < 1024) return $"{kb:0.#} KB";
        var mb = kb / 1024d;
        if (mb < 1024) return $"{mb:0.#} MB";
        var gb = mb / 1024d;
        return $"{gb:0.#} GB";
    }
}
