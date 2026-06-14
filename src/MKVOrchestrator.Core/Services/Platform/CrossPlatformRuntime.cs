using System.Runtime.InteropServices;

namespace MKVOrchestrator.Core.Services;

public static class CrossPlatformRuntime
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static StringComparer PathComparer => IsWindows || IsMacOS
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static string AppDataDirectory
    {
        get
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = AppContext.BaseDirectory;
            }

            var directory = Path.Combine(baseDirectory, "MKVOrchestrator");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string NormalizeUserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var clean = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (!IsWindows && clean.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                clean = Path.Combine(home, clean[2..]);
            }
        }

        return clean;
    }

    public static bool IsMkvPath(string? path)
        => !string.IsNullOrWhiteSpace(path)
           && string.Equals(Path.GetExtension(path), ".mkv", StringComparison.OrdinalIgnoreCase);

    public static string GetToolDisplayName(string windowsName, string unixName)
        => IsWindows ? windowsName : unixName;

    public static string ResolveExecutable(string configuredPath, string windowsName, string unixName, params string[] commonAbsolutePaths)
    {
        var configured = NormalizeUserPath(configuredPath);
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = GetToolDisplayName(windowsName, unixName);
        }

        if (Path.IsPathFullyQualified(configured) || configured.Contains(Path.DirectorySeparatorChar) || configured.Contains(Path.AltDirectorySeparatorChar))
        {
            return configured;
        }

        var pathHit = FindExecutableOnPath(configured);
        if (!string.IsNullOrWhiteSpace(pathHit)) return pathHit;

        foreach (var candidate in commonAbsolutePaths.Select(NormalizeUserPath))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return candidate;
        }

        // Return the configured command name as a final fallback so ProcessRunner can surface a useful error.
        return configured;
    }

    public static string ToProcessArgumentPath(string path)
    {
        var normalized = NormalizeUserPath(path);
        if (!IsWindows || string.IsNullOrWhiteSpace(normalized)) return normalized;
        if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal)) return normalized;

        try
        {
            if (!Path.IsPathFullyQualified(normalized)) return normalized;
            normalized = Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }

        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return @"\\?\UNC\" + normalized.TrimStart('\\');
        }

        return @"\\?\" + normalized;
    }

    public static IReadOnlyList<string> ConvertExistingPathArgumentsForProcess(IEnumerable<string> args)
    {
        return args.Select(arg =>
        {
            if (string.IsNullOrWhiteSpace(arg)) return arg;
            var normalized = NormalizeUserPath(arg);
            if (!IsWindows || !Path.IsPathFullyQualified(normalized)) return arg;
            return File.Exists(normalized) || Directory.Exists(normalized) || Path.HasExtension(normalized)
                ? ToProcessArgumentPath(normalized)
                : arg;
        }).ToList();
    }

    public static string? FindExecutableOnPath(string executableName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = IsWindows
            ? ((Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            : Array.Empty<string>();

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;

            if (IsWindows && string.IsNullOrWhiteSpace(Path.GetExtension(executableName)))
            {
                foreach (var extension in extensions)
                {
                    var withExtension = candidate + extension.ToLowerInvariant();
                    if (File.Exists(withExtension)) return withExtension;
                    withExtension = candidate + extension.ToUpperInvariant();
                    if (File.Exists(withExtension)) return withExtension;
                }
            }
        }

        return null;
    }
}
