using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services.Library;

namespace MKVOrchestrator.Core.Services.Pipeline;

public sealed record ScanPipelineRequest(
    string FolderPath,
    string MkvMergePath,
    string FfProbePath,
    IReadOnlyCollection<string> IgnoredFolderNames,
    WorkerSettings? Workers = null);

public sealed record ScanPipelineResult(int FileCount, bool WasCanceled);

/// <summary>
/// Coordinates scan execution through MediaLibraryService so normal scans, cache builds,
/// and watcher refreshes share the same scanner/cache/model boundary.
/// </summary>
public sealed class ScanPipeline
{
    private readonly IMediaLibraryService _library;

    public ScanPipeline(IMediaLibraryService library)
    {
        _library = library;
    }

    public async Task<ScanPipelineResult> ExecuteAsync(
        ScanPipelineRequest request,
        Func<MkvFileItem, Task> onFileFound,
        Action<int, int>? onProgress,
        Action<string>? onMessage,
        CancellationToken cancellationToken)
    {
        var fileCount = 0;
        var progress = new Progress<(int Completed, int Total)>(p => onProgress?.Invoke(p.Completed, p.Total));
        var mediaRequest = new MediaScanRequest(
            request.FolderPath,
            request.MkvMergePath,
            request.FfProbePath,
            request.IgnoredFolderNames,
            Workers: request.Workers);

        await foreach (var media in _library.ScanFolderAsync(mediaRequest, progress, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileCount++;
            var item = MkvFileItem.FromMediaFile(media);
            await onFileFound(item);
            onMessage?.Invoke($"Found: {item.FileName} [{item.Status}]");
            onMessage?.Invoke($"  Video: {item.Codec} | {item.Resolution} | {item.BitDepth}");
            onMessage?.Invoke($"  Tracks: {item.Tracks.Count}");
        }

        return new ScanPipelineResult(fileCount, WasCanceled: false);
    }
}
