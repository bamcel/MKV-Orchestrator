using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.Core.Services;

public sealed class MkvPropEditService
{
    private readonly ProcessRunner _runner = new();

    public async Task<ProcessResult> ExecuteAsync(string mkvPropEditPath, PlannedAction action, CancellationToken token)
    {
        mkvPropEditPath = CrossPlatformRuntime.ResolveExecutable(
            mkvPropEditPath,
            "mkvpropedit.exe",
            "mkvpropedit",
            @"C:\Program Files\MKVToolNix\mkvpropedit.exe",
            @"C:\Program Files (x86)\MKVToolNix\mkvpropedit.exe",
            "/usr/bin/mkvpropedit",
            "/usr/local/bin/mkvpropedit",
            "/opt/homebrew/bin/mkvpropedit");
        return await _runner.RunAsync(mkvPropEditPath, CrossPlatformRuntime.ConvertExistingPathArgumentsForProcess(action.Arguments), token);
    }
}
