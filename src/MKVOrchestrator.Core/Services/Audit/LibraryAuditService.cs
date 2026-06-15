using System.Text.RegularExpressions;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services.Cache;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.Core.Services.Audit;

public sealed class LibraryAuditService
{
    private readonly MetadataCacheServiceAdapter _cache;

    public LibraryAuditService(MetadataCacheServiceAdapter cache)
    {
        _cache = cache;
    }

    public LibraryAuditResult BuildAudit(string watchRoot, IReadOnlyCollection<string> ignoredFolderNames, ICollection<LibraryAuditSeasonItem> destination)
    {
        destination.Clear();
        watchRoot = CrossPlatformRuntime.NormalizeUserPath(watchRoot);
        if (string.IsNullOrWhiteSpace(watchRoot) || !Directory.Exists(watchRoot)) return new LibraryAuditResult(0, 0, 0, 0, 0);

        var files = MkvScannerService.EnumerateMediaFiles(watchRoot, ignoredFolderNames, CancellationToken.None).ToList();
        var uncached = 0;
        var grouped = new Dictionary<AuditGroupKey, List<MediaFile>>();

        foreach (var file in files)
        {
            var media = _cache.TryGetValid(file);
            if (media is null)
            {
                uncached++;
                continue;
            }

            var key = BuildGroupKey(watchRoot, file);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<MediaFile>();
                grouped[key] = list;
            }
            list.Add(media);
        }

        foreach (var entry in grouped.OrderBy(x => x.Key.ShowName, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(x => x.Key.SortSeason)
                                     .ThenBy(x => x.Key.SeasonFolder, StringComparer.OrdinalIgnoreCase))
        {
            destination.Add(BuildSeasonItem(watchRoot, entry.Key, entry.Value));
        }

        return new LibraryAuditResult(
            grouped.Keys.Select(x => x.ShowName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            grouped.Count,
            files.Count,
            destination.Count(x => x.HasIssues),
            uncached);
    }

    private static LibraryAuditSeasonItem BuildSeasonItem(string watchRoot, AuditGroupKey key, List<MediaFile> files)
    {
        var item = new LibraryAuditSeasonItem
        {
            WatchRoot = watchRoot,
            ShowName = key.ShowName,
            SeasonFolder = key.SeasonFolder,
            RelativeFolder = key.RelativeFolder,
            FileCount = files.Count,
            StandardVideo = BuildVideoStandard(files),
            StandardAudio = Dominant(files.Select(BuildAudioSignature)),
            StandardSubtitles = Dominant(files.Select(BuildSubtitleSignature))
        };

        foreach (var file in files.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
        {
            AddAllFilePath(item, file.FilePath);
        }

        item.TemplateFilePath = FindTemplateFilePath(files, item.StandardVideo, item.StandardAudio, item.StandardSubtitles);

        AddMetadataMismatchIssues(item, files, "video", item.StandardVideo, f => BuildVideoStandard(new[] { f }));
        AddMetadataMismatchIssues(item, files, "audio", item.StandardAudio, BuildAudioSignature);
        AddMetadataMismatchIssues(item, files, "subtitles", item.StandardSubtitles, BuildSubtitleSignature);
        AddEpisodeIssues(item, files);

        item.HasIssues = item.Issues.Count > 0;
        item.Status = item.HasIssues ? "warning" : "standard";
        item.IssueSummary = item.HasIssues ? string.Join(" | ", item.Issues.Take(3)) : "no issues found";
        return item;
    }

    private static void AddMetadataMismatchIssues(LibraryAuditSeasonItem item, List<MediaFile> files, string label, string standard, Func<MediaFile, string> selector)
    {
        if (string.IsNullOrWhiteSpace(standard) || standard == "unknown") return;
        foreach (var file in files.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var value = selector(file);
            if (string.Equals(value, standard, StringComparison.OrdinalIgnoreCase)) continue;

            item.Issues.Add($"{file.FileName}: {label} mismatch ({Display(value)} vs {Display(standard)})");
            AddIssueFilePath(item, file.FilePath);
        }
    }



    private static string FindTemplateFilePath(List<MediaFile> files, string videoStandard, string audioStandard, string subtitleStandard)
    {
        var template = files
            .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(file =>
                string.Equals(BuildVideoStandard(new[] { file }), videoStandard, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(BuildAudioSignature(file), audioStandard, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(BuildSubtitleSignature(file), subtitleStandard, StringComparison.OrdinalIgnoreCase));

        return template?.FilePath ?? files.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault()?.FilePath ?? string.Empty;
    }

    private static void AddIssueFilePath(LibraryAuditSeasonItem item, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (item.IssueFilePaths.Any(x => CrossPlatformRuntime.PathComparer.Equals(x, filePath))) return;
        item.IssueFilePaths.Add(filePath);
    }

    private static void AddAllFilePath(LibraryAuditSeasonItem item, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (item.AllFilePaths.Any(x => CrossPlatformRuntime.PathComparer.Equals(x, filePath))) return;
        item.AllFilePaths.Add(filePath);
    }

    private static string BuildMismatchTrackDetails(string label, MediaFile file)
    {
        var tracks = label.Equals("audio", StringComparison.OrdinalIgnoreCase)
            ? file.Tracks.Where(t => string.Equals(t.Type, "audio", StringComparison.OrdinalIgnoreCase))
            : label.Equals("subtitles", StringComparison.OrdinalIgnoreCase)
                ? file.Tracks.Where(t => string.Equals(t.Type, "subtitles", StringComparison.OrdinalIgnoreCase) || string.Equals(t.Type, "subtitle", StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<MediaTrack>();

        var details = tracks
            .Select(DescribeTrackForIssue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return details.Count == 0 ? string.Empty : $" | {label} tracks: {string.Join("; ", details)}";
    }

    private static string DescribeTrackForIssue(MediaTrack track)
    {
        var parts = new List<string>();
        var language = Clean(track.Language, "und");
        var codec = Clean(track.Codec, "unknown");
        var name = Clean(track.Name);

        parts.Add($"#{track.MkvMergeId}");
        parts.Add(language);
        parts.Add(codec);
        if (!string.IsNullOrWhiteSpace(name)) parts.Add($"name='{name}'");
        if (track.Default) parts.Add("default");
        if (track.Forced) parts.Add("forced");

        return string.Join(" ", parts);
    }

    private static void AddEpisodeIssues(LibraryAuditSeasonItem item, List<MediaFile> files)
    {
        var episodes = files.Select(f => new { File = f, Episode = TryParseEpisodeNumber(f.FileName) }).Where(x => x.Episode.HasValue).ToList();
        if (episodes.Count < 2) return;

        var duplicateGroups = episodes.GroupBy(x => x.Episode!.Value).Where(g => g.Count() > 1).OrderBy(g => g.Key).ToList();
        var duplicates = duplicateGroups.Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            item.Issues.Add($"duplicate episode numbers: {string.Join(", ", duplicates.Select(x => x.ToString("00")))}");
            foreach (var file in duplicateGroups.SelectMany(g => g.Select(x => x.File)))
            {
                AddIssueFilePath(item, file.FilePath);
            }
        }

        var numbers = episodes.Select(x => x.Episode!.Value).Distinct().OrderBy(x => x).ToList();
        if (numbers.Count < 3) return;
        var missing = Enumerable.Range(numbers.First(), numbers.Last() - numbers.First() + 1).Except(numbers).Take(20).ToList();
        if (missing.Count > 0) item.Issues.Add($"possible missing episode numbers: {string.Join(", ", missing.Select(x => x.ToString("00")))}");
    }

    private static string BuildVideoStandard(IEnumerable<MediaFile> files)
    {
        return Dominant(files.Select(f => string.Join(" ", new[] { Clean(f.Metadata.Resolution), Clean(f.Metadata.Codec), Clean(f.Metadata.BitDepth), Clean(f.Metadata.Hdr) }.Where(x => !string.IsNullOrWhiteSpace(x)))));
    }

    private static string BuildAudioSignature(MediaFile file)
    {
        var parts = file.Tracks.Where(t => string.Equals(t.Type, "audio", StringComparison.OrdinalIgnoreCase))
            .Select(FormatTrackSignature)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string BuildSubtitleSignature(MediaFile file)
    {
        var parts = file.Tracks.Where(t => string.Equals(t.Type, "subtitles", StringComparison.OrdinalIgnoreCase) || string.Equals(t.Type, "subtitle", StringComparison.OrdinalIgnoreCase))
            .Select(FormatTrackSignature)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string FormatTrackSignature(MediaTrack track)
    {
        var signature = $"{Clean(track.Language, "und")}:{Clean(track.Codec, "unknown")}";
        if (track.Forced) signature += ":forced";

        var name = Clean(track.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            signature += $" - \"{name}\"";
        }

        return signature;
    }

    private static string Dominant(IEnumerable<string> values)
    {
        return values.Select(v => Clean(v, "unknown"))
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "unknown";
    }

    private static AuditGroupKey BuildGroupKey(string watchRoot, string filePath)
    {
        var relative = Path.GetRelativePath(watchRoot, filePath);
        var directory = Path.GetDirectoryName(relative) ?? string.Empty;
        var segments = directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return new AuditGroupKey("root", "root", string.Empty, 0);

        var seasonIndex = Array.FindLastIndex(segments, IsSeasonFolderName);
        if (seasonIndex >= 0)
        {
            var showName = seasonIndex > 0 ? segments[seasonIndex - 1] : segments[0];
            var seasonFolder = segments[seasonIndex];
            return new AuditGroupKey(showName, seasonFolder, string.Join(Path.DirectorySeparatorChar, segments.Take(seasonIndex + 1)), TryParseSeasonNumber(seasonFolder) ?? seasonIndex);
        }

        var folder = segments.Last();
        return new AuditGroupKey(folder, "movie/single folder", directory, 9999);
    }

    private static bool IsSeasonFolderName(string value) => Regex.IsMatch(value ?? string.Empty, @"^(season\s*\d+|s\d+)$", RegexOptions.IgnoreCase);

    private static int? TryParseSeasonNumber(string value)
    {
        var match = Regex.Match(value ?? string.Empty, @"(?:season\s*|s)(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var result) ? result : null;
    }

    private static int? TryParseEpisodeNumber(string fileName)
    {
        var sx = Regex.Match(fileName, @"[Ss]\d{1,2}[Ee](\d{1,3})");
        if (sx.Success && int.TryParse(sx.Groups[1].Value, out var sxe)) return sxe;
        var ep = Regex.Match(fileName, @"(?:^|[^A-Za-z0-9])(?:E|EP|Episode)\s*0*(\d{1,3})(?:[^A-Za-z0-9]|$)", RegexOptions.IgnoreCase);
        if (ep.Success && int.TryParse(ep.Groups[1].Value, out var e)) return e;
        return null;
    }

    private static string Clean(string? value, string fallback = "")
    {
        var clean = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private sealed record AuditGroupKey(string ShowName, string SeasonFolder, string RelativeFolder, int SortSeason);
}
