using System.Runtime.CompilerServices;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services.Library;

namespace MKVOrchestrator.Core.Services;

/// <summary>
/// IMediaScannerService adapter that exposes the media scanner through the canonical MediaFile model.
/// </summary>
public sealed class MkvScannerServiceAdapter : IMediaScannerService
{
    private readonly MkvScannerService _scanner;

    public MkvScannerServiceAdapter(MkvScannerService scanner)
    {
        _scanner = scanner;
    }

    public async IAsyncEnumerable<MediaFile> ScanFolderAsync(
        MediaScanRequest request,
        IProgress<(int Completed, int Total)>? progress,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var item in _scanner.ScanAsync(
            request.FolderPath,
            request.MkvMergePath,
            request.FfProbePath,
            token,
            request.IgnoredFolderNames,
            progress,
            request.Workers))
        {
            yield return item.ToMediaFile();
        }
    }

    public async Task<MediaFile> ScanFileAsync(string filePath, string mkvMergePath, string ffProbePath, CancellationToken token, bool forceRefresh = false)
    {
        var item = await _scanner.ScanFileAsync(filePath, mkvMergePath, ffProbePath, token, forceRefresh);
        return item.ToMediaFile();
    }
}
