using System.Text.Json;
using Microsoft.Data.Sqlite;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.Core.Services.Cache;

public sealed class MetadataCacheDatabase
{
    public const int CurrentCacheSchemaVersion = 1;
    private readonly string _databasePath;
    private readonly object _gate = new();

    public MetadataCacheDatabase()
        : this("metadata_cache.db")
    {
    }

    public MetadataCacheDatabase(string databaseFileName)
    {
        var folder = CrossPlatformRuntime.AppDataDirectory;
        var cleanFileName = string.IsNullOrWhiteSpace(databaseFileName) ? "metadata_cache.db" : Path.GetFileName(databaseFileName);
        _databasePath = Path.Combine(folder, cleanFileName);
        Initialize();
    }

    public string DatabasePath => _databasePath;

    public int CountEntries()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM media_cache";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public int Clear()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM media_cache";
            var before = Convert.ToInt32(countCommand.ExecuteScalar());

            using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM media_cache";
            deleteCommand.ExecuteNonQuery();

            using var vacuumCommand = connection.CreateCommand();
            vacuumCommand.CommandText = "VACUUM";
            vacuumCommand.ExecuteNonQuery();
            return before;
        }
    }

    public int RemoveOlderThan(DateTime cutoffUtc)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM media_cache WHERE scanned_utc_ticks < $cutoff_utc_ticks";
            command.Parameters.AddWithValue("$cutoff_utc_ticks", cutoffUtc.ToUniversalTime().Ticks);
            return command.ExecuteNonQuery();
        }
    }


    public MediaFile? TryGetValidMedia(string filePath)
    {
        var item = TryGetValid(filePath);
        return item?.ToMediaFile();
    }

    public MkvFileItem? TryGetValid(string filePath)
    {
        filePath = CrossPlatformRuntime.NormalizeUserPath(filePath);
        if (!File.Exists(filePath)) return null;
        var info = new FileInfo(filePath);
        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT file_size, modified_utc_ticks, payload_json, track_count
FROM media_cache
WHERE file_path = $file_path";
            command.Parameters.AddWithValue("$file_path", filePath);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            var size = reader.GetInt64(0);
            var ticks = reader.GetInt64(1);
            var payload = reader.GetString(2);
            var trackCount = reader.GetInt32(3);

            if (size != info.Length || ticks != info.LastWriteTimeUtc.Ticks || trackCount <= 0)
            {
                DeleteNoLock(connection, filePath);
                return null;
            }

            try
            {
                var dto = JsonSerializer.Deserialize<CachedMkvFileItem>(payload);
                var item = dto?.ToItem();
                if (item is null || item.Tracks.Count == 0)
                {
                    DeleteNoLock(connection, filePath);
                    return null;
                }

                item.Status = "Ready (cached)";
                return item;
            }
            catch
            {
                DeleteNoLock(connection, filePath);
                return null;
            }
        }
    }


    public void Upsert(MediaFile mediaFile)
    {
        Upsert(MediaFileMapper.ToMkvFileItem(mediaFile));
    }

    public void Upsert(MkvFileItem item)
    {
        item.FilePath = CrossPlatformRuntime.NormalizeUserPath(item.FilePath);
        if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath) || item.Tracks.Count == 0) return;
        var info = new FileInfo(item.FilePath);
        var dto = CachedMkvFileItem.FromItem(item);
        var payload = JsonSerializer.Serialize(dto);

        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO media_cache
(file_path, file_size, modified_utc_ticks, scanned_utc_ticks, track_count, payload_json)
VALUES ($file_path, $file_size, $modified_utc_ticks, $scanned_utc_ticks, $track_count, $payload_json)
ON CONFLICT(file_path) DO UPDATE SET
    file_size = excluded.file_size,
    modified_utc_ticks = excluded.modified_utc_ticks,
    scanned_utc_ticks = excluded.scanned_utc_ticks,
    track_count = excluded.track_count,
    payload_json = excluded.payload_json";
            command.Parameters.AddWithValue("$file_path", item.FilePath);
            command.Parameters.AddWithValue("$file_size", info.Length);
            command.Parameters.AddWithValue("$modified_utc_ticks", info.LastWriteTimeUtc.Ticks);
            command.Parameters.AddWithValue("$scanned_utc_ticks", DateTime.UtcNow.Ticks);
            command.Parameters.AddWithValue("$track_count", item.Tracks.Count);
            command.Parameters.AddWithValue("$payload_json", payload);
            command.ExecuteNonQuery();
        }
    }

    public void Remove(string filePath)
    {
        filePath = CrossPlatformRuntime.NormalizeUserPath(filePath);
        lock (_gate)
        {
            using var connection = OpenConnection();
            DeleteNoLock(connection, filePath);
        }
    }


    public int RemoveMissingUnderRoots(IEnumerable<string> rootPaths)
    {
        var roots = rootPaths
            .Select(CrossPlatformRuntime.NormalizeUserPath)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(CrossPlatformRuntime.PathComparer)
            .ToList();

        if (roots.Count == 0) return 0;

        var removed = 0;
        lock (_gate)
        {
            using var connection = OpenConnection();
            foreach (var root in roots)
            {
                using var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = @"
SELECT file_path
FROM media_cache
WHERE file_path = $exact
   OR file_path LIKE $separator_prefix
   OR file_path LIKE $alt_separator_prefix";
                selectCommand.Parameters.AddWithValue("$exact", root);
                selectCommand.Parameters.AddWithValue("$separator_prefix", root + Path.DirectorySeparatorChar + "%");
                selectCommand.Parameters.AddWithValue("$alt_separator_prefix", root + Path.AltDirectorySeparatorChar + "%");

                var paths = new List<string>();
                using (var reader = selectCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        paths.Add(reader.GetString(0));
                    }
                }

                foreach (var path in paths)
                {
                    if (File.Exists(path)) continue;
                    DeleteNoLock(connection, path);
                    removed++;
                }
            }
        }

        return removed;
    }

    public int RemoveUnderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return 0;
        var normalized = CrossPlatformRuntime.NormalizeUserPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var separatorPattern = normalized + Path.DirectorySeparatorChar;
        var altSeparatorPattern = normalized + Path.AltDirectorySeparatorChar;

        lock (_gate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
DELETE FROM media_cache
WHERE file_path = $exact
   OR file_path LIKE $separator_prefix
   OR file_path LIKE $alt_separator_prefix";
            command.Parameters.AddWithValue("$exact", normalized);
            command.Parameters.AddWithValue("$separator_prefix", separatorPattern + "%");
            command.Parameters.AddWithValue("$alt_separator_prefix", altSeparatorPattern + "%");
            return command.ExecuteNonQuery();
        }
    }

    private void Initialize()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
        }
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS cache_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS media_cache (
    file_path TEXT PRIMARY KEY,
    file_size INTEGER NOT NULL,
    modified_utc_ticks INTEGER NOT NULL,
    scanned_utc_ticks INTEGER NOT NULL,
    track_count INTEGER NOT NULL,
    payload_json TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_media_cache_scanned_utc ON media_cache(scanned_utc_ticks);";
        command.ExecuteNonQuery();

        var existingVersion = GetCacheSchemaVersion(connection);
        if (existingVersion <= 0)
        {
            SetCacheSchemaVersion(connection, CurrentCacheSchemaVersion);
            return;
        }

        if (existingVersion < CurrentCacheSchemaVersion)
        {
            // Future cache migrations should be added here in ascending version order.
            SetCacheSchemaVersion(connection, CurrentCacheSchemaVersion);
            return;
        }

        if (existingVersion > CurrentCacheSchemaVersion)
        {
            // A newer app created this cache. Clear entries rather than risk reading incompatible payloads.
            using var clearCommand = connection.CreateCommand();
            clearCommand.CommandText = "DELETE FROM media_cache";
            clearCommand.ExecuteNonQuery();
            SetCacheSchemaVersion(connection, CurrentCacheSchemaVersion);
        }
    }

    private static int GetCacheSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM cache_metadata WHERE key = 'schema_version'";
        var value = command.ExecuteScalar()?.ToString();
        return int.TryParse(value, out var version) ? version : 0;
    }

    private static void SetCacheSchemaVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO cache_metadata (key, value)
VALUES ('schema_version', $version)
ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        command.Parameters.AddWithValue("$version", version.ToString());
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static void DeleteNoLock(SqliteConnection connection, string filePath)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM media_cache WHERE file_path = $file_path";
        command.Parameters.AddWithValue("$file_path", filePath);
        command.ExecuteNonQuery();
    }

    private sealed class CachedMkvFileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string ContainerTitle { get; set; } = string.Empty;
        public string VideoSummary { get; set; } = string.Empty;
        public string AudioSummary { get; set; } = string.Empty;
        public string SubtitleSummary { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string Codec { get; set; } = string.Empty;
        public string BitDepth { get; set; } = string.Empty;
        public string Hdr { get; set; } = string.Empty;
        public string AttachmentSummary { get; set; } = string.Empty;
        public List<CachedTrackItem> Tracks { get; set; } = new();
        public List<CachedAttachmentItem> Attachments { get; set; } = new();

        public static CachedMkvFileItem FromItem(MkvFileItem item) => new()
        {
            FilePath = item.FilePath,
            ContainerTitle = item.ContainerTitle,
            VideoSummary = item.VideoSummary,
            AudioSummary = item.AudioSummary,
            SubtitleSummary = item.SubtitleSummary,
            Resolution = item.Resolution,
            Codec = item.Codec,
            BitDepth = item.BitDepth,
            Hdr = item.Hdr,
            AttachmentSummary = item.AttachmentSummary,
            Tracks = item.Tracks.Select(CachedTrackItem.FromItem).ToList(),
            Attachments = item.Attachments.Select(CachedAttachmentItem.FromItem).ToList()
        };

        public MkvFileItem ToItem()
        {
            var item = new MkvFileItem
            {
                FilePath = FilePath,
                ContainerTitle = ContainerTitle,
                VideoSummary = VideoSummary,
                AudioSummary = AudioSummary,
                SubtitleSummary = SubtitleSummary,
                Resolution = Resolution,
                Codec = Codec,
                BitDepth = BitDepth,
                Hdr = Hdr,
                AttachmentSummary = AttachmentSummary,
                Status = "Ready (cached)"
            };
            foreach (var track in Tracks) item.Tracks.Add(track.ToItem());
            foreach (var attachment in Attachments) item.Attachments.Add(attachment.ToItem());
            return item;
        }
    }

    private sealed class CachedTrackItem
    {
        public int MkvMergeId { get; set; }
        public int PropEditTrackNumber { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Codec { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string BitDepth { get; set; } = string.Empty;
        public string Hdr { get; set; } = string.Empty;
        public bool Default { get; set; }
        public bool Forced { get; set; }

        public static CachedTrackItem FromItem(MkvTrackItem item) => new()
        {
            MkvMergeId = item.MkvMergeId,
            PropEditTrackNumber = item.PropEditTrackNumber,
            Type = item.Type,
            Codec = item.Codec,
            Language = item.Language,
            Name = item.Name,
            Resolution = item.Resolution,
            BitDepth = item.BitDepth,
            Hdr = item.Hdr,
            Default = item.Default,
            Forced = item.Forced
        };

        public MkvTrackItem ToItem() => new()
        {
            MkvMergeId = MkvMergeId,
            PropEditTrackNumber = PropEditTrackNumber,
            Type = Type,
            Codec = Codec,
            Language = Language,
            Name = Name,
            Resolution = Resolution,
            BitDepth = BitDepth,
            Hdr = Hdr,
            Default = Default,
            Forced = Forced
        };
    }

    private sealed class CachedAttachmentItem
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long? SizeBytes { get; set; }

        public static CachedAttachmentItem FromItem(MkvAttachmentItem item) => new()
        {
            Id = item.Id,
            FileName = item.FileName,
            ContentType = item.ContentType,
            Description = item.Description,
            SizeBytes = item.SizeBytes
        };

        public MkvAttachmentItem ToItem() => new()
        {
            Id = Id,
            FileName = FileName,
            ContentType = ContentType,
            Description = Description,
            SizeBytes = SizeBytes
        };
    }
}
