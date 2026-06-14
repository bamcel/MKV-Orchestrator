using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services.Library;

namespace MKVOrchestrator.Core.Services.Cache;

/// <summary>
/// IMediaCacheService adapter over the SQLite cache. Keeps higher-level services independent of storage details.
/// </summary>
public sealed class MetadataCacheServiceAdapter : IMediaCacheService
{
    private readonly MetadataCacheDatabase _database;

    public MetadataCacheServiceAdapter(MetadataCacheDatabase database)
    {
        _database = database;
    }

    public int CountEntries() => _database.CountEntries();
    public int Clear() => _database.Clear();
    public int RemoveOlderThan(DateTime cutoffUtc) => _database.RemoveOlderThan(cutoffUtc);
    public MediaFile? TryGetValid(string filePath) => _database.TryGetValidMedia(filePath);
    public void Upsert(MediaFile mediaFile) => _database.Upsert(mediaFile);
    public void Remove(string filePath) => _database.Remove(filePath);
    public int RemoveMissingUnderRoots(IEnumerable<string> rootPaths) => _database.RemoveMissingUnderRoots(rootPaths);
    public int RemoveUnderPath(string path) => _database.RemoveUnderPath(path);
}
