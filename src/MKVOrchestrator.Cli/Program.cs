using System.Text.Json;
using MKVOrchestrator.Core.Models;
using MKVOrchestrator.Core.Services;
using MKVOrchestrator.Core.Services.Rename;

var exitCode = await Cli.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
return exitCode;

internal static class Cli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || Has(args, "--help") || Has(args, "-h"))
        {
            WriteUsage(output);
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "scan" => await ScanAsync(options, output, cancellationToken),
                "inspect" => await ScanAsync(options with { Json = true }, output, cancellationToken),
                "cleanup" => await CleanupAsync(options, output, cancellationToken),
                "rename" => await RenameAsync(options, output, cancellationToken),
                _ => Unknown(command, error)
            };
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static async Task<int> ScanAsync(CliOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        var folder = RequirePath(options);
        var files = await ScanFolderAsync(folder, options, cancellationToken);

        if (options.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(files.Select(ToScanRow), JsonOptions));
            return 0;
        }

        await output.WriteLineAsync($"Scanned {files.Count} file(s).");
        foreach (var file in files)
        {
            await output.WriteLineAsync($"{file.FileName} | {file.Codec} | {file.Resolution} | {file.AudioSummary} | {file.SubtitleSummary} | {file.Status}");
        }

        return 0;
    }

    private static async Task<int> CleanupAsync(CliOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        var folder = RequirePath(options);
        var files = await ScanFolderAsync(folder, options, cancellationToken);
        var planner = new ActionPlanner();
        var actions = planner.BuildActions(files, new EditOptions
        {
            RemoveContainerTitle = !Has(options.RawArgs, "--keep-container-title"),
            RemoveVideoTrackTitles = !Has(options.RawArgs, "--keep-video-title"),
            RemoveAudioTrackTitles = Has(options.RawArgs, "--remove-audio-titles"),
            RemoveSubtitleTrackTitles = Has(options.RawArgs, "--remove-subtitle-titles"),
            SetAudioLanguage = ValueOrNull(options, "--set-audio-language"),
            SetSubtitleLanguage = ValueOrNull(options, "--set-subtitle-language")
        });

        if (options.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(actions, JsonOptions));
        }
        else
        {
            await output.WriteLineAsync($"Planned {actions.Count} cleanup action(s).");
            foreach (var action in actions)
            {
                await output.WriteLineAsync($"{Path.GetFileName(action.FilePath)}: {action.Description}");
            }
        }

        if (options.Apply)
        {
            var service = new MkvPropEditService();
            foreach (var action in actions)
            {
                var result = await service.ExecuteAsync(options.MkvPropEditPath, action, cancellationToken);
                await output.WriteLineAsync($"{Path.GetFileName(action.FilePath)}: exit {result.ExitCode}");
            }
        }

        return actions.Count == 0 ? 2 : 0;
    }

    private static async Task<int> RenameAsync(CliOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        var folder = RequirePath(options);
        var files = await ScanFolderAsync(folder, options, cancellationToken);
        var planner = new RenamePlanner();
        var plan = planner.BuildPlan(new RenamePlanRequest(
            files.Select(f => f.ToMediaFile()).ToList(),
            ValueOrNull(options, "--template") ?? "{series} - S{season:00}E{episode:00} - {episodeTitle}",
            CheckExistingFiles: true));

        if (options.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(plan.Items, JsonOptions));
        }
        else
        {
            await output.WriteLineAsync($"Rename plan: {plan.RenameCount} rename(s), {plan.SkipCount} skip(s).");
            foreach (var item in plan.Items)
            {
                await output.WriteLineAsync($"{Path.GetFileName(item.SourcePath)} -> {item.NewFileName} [{item.Status}]");
            }
        }

        if (options.Apply)
        {
            var applied = await planner.ApplyAsync(plan, cancellationToken);
            await output.WriteLineAsync($"Applied: {applied.Items.Count(i => i.Status == "Renamed")} renamed.");
        }

        return plan.HasBlockingIssues ? 2 : 0;
    }

    private static async Task<List<MkvFileItem>> ScanFolderAsync(string folder, CliOptions options, CancellationToken cancellationToken)
    {
        var scanner = new MkvScannerService();
        var files = new List<MkvFileItem>();
        await foreach (var item in scanner.ScanAsync(
                           folder,
                           options.MkvMergePath,
                           options.FfProbePath,
                           cancellationToken,
                           options.IgnoredFolders))
        {
            files.Add(item);
        }

        return files.OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static object ToScanRow(MkvFileItem file) => new
    {
        file.FilePath,
        file.FileName,
        file.Status,
        file.Resolution,
        file.Codec,
        file.BitDepth,
        file.AudioSummary,
        file.SubtitleSummary,
        Tracks = file.Tracks.Select(track => new
        {
            track.MkvMergeId,
            track.PropEditTrackNumber,
            track.Type,
            track.Codec,
            track.Language,
            track.Name,
            track.Default,
            track.Forced
        })
    };

    private static CliOptions ParseOptions(string[] args)
    {
        return new CliOptions(
            RawArgs: args,
            Path: ValueOrNull(args, "--path") ?? args.FirstOrDefault(arg => !arg.StartsWith("-", StringComparison.Ordinal)) ?? string.Empty,
            MkvMergePath: ValueOrNull(args, "--mkvmerge") ?? "mkvmerge",
            MkvPropEditPath: ValueOrNull(args, "--mkvpropedit") ?? "mkvpropedit",
            FfProbePath: ValueOrNull(args, "--ffprobe") ?? "ffprobe",
            IgnoredFolders: SplitCsv(ValueOrNull(args, "--ignore")),
            Json: Has(args, "--json"),
            Apply: Has(args, "--apply"));
    }

    private static string RequirePath(CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Path))
        {
            throw new InvalidOperationException("A folder path is required.");
        }

        return options.Path;
    }

    private static int Unknown(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command: {command}");
        return 1;
    }

    private static string? ValueOrNull(CliOptions options, string name) => ValueOrNull(options.RawArgs, name);

    private static string? ValueOrNull(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool Has(string[] args, string name) => args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyCollection<string> SplitCsv(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("mkvo scan <folder> [--json] [--ignore Extras,Backdrops]");
        output.WriteLine("mkvo inspect <folder> [--json]");
        output.WriteLine("mkvo cleanup <folder> [--json] [--apply] [--remove-audio-titles] [--remove-subtitle-titles]");
        output.WriteLine("mkvo rename <folder> [--template \"{series} - S{season:00}E{episode:00} - {episodeTitle}\"] [--json] [--apply]");
    }
}

internal sealed record CliOptions(
    string[] RawArgs,
    string Path,
    string MkvMergePath,
    string MkvPropEditPath,
    string FfProbePath,
    IReadOnlyCollection<string> IgnoredFolders,
    bool Json,
    bool Apply);
