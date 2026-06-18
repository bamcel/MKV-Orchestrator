using System.Text.Json;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class RenameBatchHistoryService
{
    private const int MaxBatchCount = 20;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly FileConflictService _fileConflicts = new();

    public string HistoryPath { get; } = Path.Combine(CrossPlatformRuntime.AppDataDirectory, "rename-batches.json");

    public IReadOnlyList<RenameBatchRecord> Load()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return Array.Empty<RenameBatchRecord>();
            var json = File.ReadAllText(HistoryPath);
            var records = JsonSerializer.Deserialize<List<RenameBatchRecord>>(json, JsonOptions) ?? new List<RenameBatchRecord>();
            return records
                .Where(record => record.Entries.Count > 0)
                .OrderByDescending(record => record.CreatedAt)
                .Take(MaxBatchCount)
                .ToList();
        }
        catch
        {
            return Array.Empty<RenameBatchRecord>();
        }
    }

    public void RecordBatch(RenameBatchRecord record)
    {
        if (record.Entries.Count == 0) return;

        var records = Load().ToList();
        records.Insert(0, record);
        Save(records
            .OrderByDescending(batch => batch.CreatedAt)
            .Take(MaxBatchCount)
            .ToList());
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                File.Delete(HistoryPath);
            }
        }
        catch
        {
            // Clearing history is best effort; callers refresh the visible list afterward.
        }
    }

    public RenameBatchUndoPreview PreviewUndoBatch(RenameBatchRecord batch)
    {
        var preview = new RenameBatchUndoPreview();
        preview.Lines.Add($"Batch: {batch.DisplayName}");
        preview.Lines.Add($"Files in batch: {batch.Entries.Count}");

        var duplicateOriginals = FindDuplicates(batch.Entries.Select(entry => entry.OriginalPath));
        var duplicateRenamed = FindDuplicates(batch.Entries.Select(entry => entry.RenamedPath));

        foreach (var entry in batch.Entries)
        {
            var originalPath = NormalizePath(entry.OriginalPath);
            var renamedPath = NormalizePath(entry.RenamedPath);
            var label = $"{Path.GetFileName(renamedPath)} -> {Path.GetFileName(originalPath)}";
            var skipReason = GetUndoSkipReason(originalPath, renamedPath, duplicateOriginals, duplicateRenamed);

            if (string.IsNullOrWhiteSpace(skipReason))
            {
                preview.Restorable++;
            }
            else
            {
                preview.Skipped++;
                preview.Lines.Add($"SKIP: {label} | {skipReason}");
            }
        }

        if (preview.Skipped == 0)
        {
            preview.Lines.Add($"Ready to restore {preview.Restorable} file(s).");
        }
        else
        {
            preview.Lines.Add(string.Empty);
            preview.Lines.Add($"{preview.Skipped} file(s) will be skipped. {preview.Restorable} file(s) can still be restored.");
        }

        return preview;
    }

    public RenameBatchUndoResult UndoBatch(RenameBatchRecord batch)
    {
        var result = new RenameBatchUndoResult();
        result.Lines.Add($"Undo batch: {batch.DisplayName}");
        result.Lines.Add($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        result.Lines.Add(new string('=', 72));

        var duplicateOriginals = FindDuplicates(batch.Entries.Select(entry => entry.OriginalPath));
        var duplicateRenamed = FindDuplicates(batch.Entries.Select(entry => entry.RenamedPath));

        foreach (var entry in batch.Entries)
        {
            var originalPath = NormalizePath(entry.OriginalPath);
            var renamedPath = NormalizePath(entry.RenamedPath);
            var label = $"{Path.GetFileName(renamedPath)} -> {Path.GetFileName(originalPath)}";

            var skipReason = GetUndoSkipReason(originalPath, renamedPath, duplicateOriginals, duplicateRenamed);
            if (!string.IsNullOrWhiteSpace(skipReason))
            {
                result.Skipped++;
                result.Lines.Add($"SKIP: {label} | {skipReason}");
                continue;
            }

            try
            {
                File.Move(renamedPath, originalPath);
                result.Renamed++;
                result.Lines.Add($"UNDO: {label}");
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Lines.Add($"SKIP: {label} | Move failed: {ex.Message}");
            }
        }

        result.Lines.Add(new string('=', 72));
        result.Lines.Add($"Undo complete: {result.Renamed} restored | {result.Skipped} skipped");

        if (result.Renamed > 0 && result.Skipped == 0)
        {
            MarkBatchUndone(batch.Id);
        }

        return result;
    }

    private string GetUndoSkipReason(
        string originalPath,
        string renamedPath,
        ISet<string> duplicateOriginals,
        ISet<string> duplicateRenamed)
    {
        if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(renamedPath))
        {
            return "Original or renamed path is blank.";
        }

        if (duplicateOriginals.Contains(originalPath))
        {
            return "Multiple entries would restore to the same original path.";
        }

        if (duplicateRenamed.Contains(renamedPath))
        {
            return "Multiple entries reference the same current renamed file.";
        }

        var conflict = _fileConflicts.CheckRenameTarget(renamedPath, originalPath);
        return conflict.CanProceed ? string.Empty : conflict.Reason;
    }

    private void MarkBatchUndone(string batchId)
    {
        var records = Load().ToList();
        var batch = records.FirstOrDefault(record => string.Equals(record.Id, batchId, StringComparison.OrdinalIgnoreCase));
        if (batch is null) return;

        batch.UndoneAt = DateTime.Now;
        Save(records);
    }

    private void Save(IReadOnlyList<RenameBatchRecord> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath) ?? ".");
        var json = JsonSerializer.Serialize(records, JsonOptions);
        File.WriteAllText(HistoryPath, json);
    }

    private static HashSet<string> FindDuplicates(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(CrossPlatformRuntime.PathComparer);
        var duplicates = new HashSet<string>(CrossPlatformRuntime.PathComparer);

        foreach (var path in paths.Select(NormalizePath).Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (!seen.Add(path)) duplicates.Add(path);
        }

        return duplicates;
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path.Trim());
}
