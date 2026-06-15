using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.Core.Services.Library;

/// <summary>
/// Coordinates media scanning, cache reuse, and watch-folder validation around the canonical MediaFile model.
/// UI projections should happen only at the ViewModel boundary.
/// </summary>
public sealed class MediaLibraryService : IMediaLibraryService
{
    private readonly IMediaScannerService _scanner;
    private readonly IMediaCacheService _cache;

    public MediaLibraryService(IMediaScannerService scanner, IMediaCacheService cache)
    {
        _scanner = scanner;
        _cache = cache;
    }

    public IAsyncEnumerable<MediaFile> ScanFolderAsync(
        MediaScanRequest request,
        IProgress<(int Completed, int Total)>? progress,
        CancellationToken token)
    {
        return _scanner.ScanFolderAsync(request, progress, token);
    }

    public async Task<MediaFile> ScanFileAsync(string filePath, string mkvMergePath, string ffProbePath, CancellationToken token, bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            var cached = _cache.TryGetValid(filePath);
            if (cached is not null) return cached;
        }

        var media = await _scanner.ScanFileAsync(filePath, mkvMergePath, ffProbePath, token, forceRefresh);
        if (media.Tracks.Count > 0)
        {
            _cache.Upsert(media);
        }

        return media;
    }

    public async Task<MediaCacheBuildResult> BuildCacheFromWatchFoldersAsync(
        IReadOnlyCollection<string> watchRoots,
        string mkvMergePath,
        string ffProbePath,
        IReadOnlyCollection<string> ignoredFolderNames,
        WorkerSettings? workers,
        Action<string>? onMessage,
        Action<string>? onStatus,
        CancellationToken token)
    {
        var allFiles = new List<string>();
        foreach (var root in watchRoots.Where(Directory.Exists))
        {
            allFiles.AddRange(MkvScannerService.EnumerateMediaFiles(root, ignoredFolderNames, token));
        }

        allFiles = allFiles
            .Distinct(CrossPlatformRuntime.PathComparer)
            .OrderBy(path => path, CrossPlatformRuntime.PathComparer)
            .ToList();

        var total = allFiles.Count;
        var completed = 0;
        var skipped = 0;
        var updated = 0;
        var failed = 0;
        var workerCount = Math.Min(total == 0 ? 1 : total, (workers ?? WorkerSettings.Defaults).CloneNormalized().MaxScanWorkers);

        await Parallel.ForEachAsync(allFiles, new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = workerCount
        }, async (file, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var current = Interlocked.Increment(ref completed);
            onStatus?.Invoke($"Validating {current} of {total}: {Path.GetFileName(file)}");

            try
            {
                var existing = _cache.TryGetValid(file);
                if (existing is not null)
                {
                    Interlocked.Increment(ref skipped);
                    onMessage?.Invoke($"Cache skip: {existing.FileName} | unchanged | Tracks: {existing.Tracks.Count}");
                    return;
                }

                var media = await ScanFileAsync(file, mkvMergePath, ffProbePath, ct, forceRefresh: true);
                if (media.Tracks.Count > 0)
                {
                    _cache.Upsert(media);
                    Interlocked.Increment(ref updated);
                    onMessage?.Invoke($"Cache updated: {media.FileName} | Tracks: {media.Tracks.Count} | Video: {media.Metadata.Codec} {media.Metadata.Resolution} {media.Metadata.BitDepth}".Trim());
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    onMessage?.Invoke($"Cache warning: {Path.GetFileName(file)} returned no tracks. {media.Status}");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                onMessage?.Invoke($"Cache failed: {Path.GetFileName(file)} - {ex.Message}");
            }
        });

        var staleRemoved = _cache.RemoveMissingUnderRoots(watchRoots);
        return new MediaCacheBuildResult(total, skipped, updated, failed, staleRemoved, _cache.CountEntries());
    }
}
