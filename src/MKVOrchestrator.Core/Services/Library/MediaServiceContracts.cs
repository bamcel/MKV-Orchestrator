using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services.Library;

public sealed record MediaScanRequest(
    string FolderPath,
    string MkvMergePath,
    string FfProbePath,
    IReadOnlyCollection<string> IgnoredFolderNames,
    bool ForceRefresh = false,
    WorkerSettings? Workers = null);

public sealed record MediaCacheBuildResult(
    int Total,
    int Skipped,
    int Updated,
    int Failed,
    int StaleRemoved,
    int DatabaseEntries);

public interface IMediaScannerService
{
    IAsyncEnumerable<MediaFile> ScanFolderAsync(MediaScanRequest request, IProgress<(int Completed, int Total)>? progress, CancellationToken token);
    Task<MediaFile> ScanFileAsync(string filePath, string mkvMergePath, string ffProbePath, CancellationToken token, bool forceRefresh = false);
}

public interface IMediaCacheService
{
    int CountEntries();
    int Clear();
    int RemoveOlderThan(DateTime cutoffUtc);
    MediaFile? TryGetValid(string filePath);
    void Upsert(MediaFile mediaFile);
    void Remove(string filePath);
    int RemoveMissingUnderRoots(IEnumerable<string> rootPaths);
    int RemoveUnderPath(string path);
}

public interface IMediaLibraryService
{
    IAsyncEnumerable<MediaFile> ScanFolderAsync(MediaScanRequest request, IProgress<(int Completed, int Total)>? progress, CancellationToken token);
    Task<MediaFile> ScanFileAsync(string filePath, string mkvMergePath, string ffProbePath, CancellationToken token, bool forceRefresh = false);
    Task<MediaCacheBuildResult> BuildCacheFromWatchFoldersAsync(
        IReadOnlyCollection<string> watchRoots,
        string mkvMergePath,
        string ffProbePath,
        IReadOnlyCollection<string> ignoredFolderNames,
        WorkerSettings? workers,
        Action<string>? onMessage,
        Action<string>? onStatus,
        CancellationToken token);
}
