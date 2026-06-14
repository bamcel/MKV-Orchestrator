using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class FileConflictService
{
    public FileConflictResult CheckReadableWritable(string filePath, bool requireWrite = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return FileConflictResult.Blocked(filePath, "File path is blank.");
        }

        if (!File.Exists(filePath))
        {
            return FileConflictResult.Blocked(filePath, "Source file does not exist.");
        }

        try
        {
            var access = requireWrite ? FileAccess.ReadWrite : FileAccess.Read;
            using var stream = new FileStream(filePath, FileMode.Open, access, FileShare.None);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FileConflictResult.Blocked(filePath, "Permission denied: " + ex.Message);
        }
        catch (IOException ex)
        {
            return FileConflictResult.Blocked(filePath, "File appears locked or busy: " + ex.Message);
        }

        return FileConflictResult.Clear(filePath);
    }

    public FileConflictResult CheckRenameTarget(string sourcePath, string targetPath)
    {
        var source = CheckReadableWritable(sourcePath, requireWrite: true);
        if (!source.CanProceed) return source;

        if (File.Exists(targetPath))
        {
            return FileConflictResult.Blocked(targetPath, "Target file already exists.");
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            return FileConflictResult.Blocked(targetPath, "Target directory does not exist.");
        }

        return FileConflictResult.Clear(sourcePath);
    }
}

public sealed record FileConflictResult(string FilePath, bool CanProceed, string Reason)
{
    public static FileConflictResult Clear(string filePath) => new(filePath, true, string.Empty);
    public static FileConflictResult Blocked(string filePath, string reason) => new(filePath, false, reason);
}
