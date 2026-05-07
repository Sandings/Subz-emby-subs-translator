namespace SubZ.Plugin.Services;

public static class UiText
{
    public static string Queued() => "Queued";
    public static string Paused() => "Paused";
    public static string Running() => "Running";
    public static string Completed() => "Completed";
    public static string StoppedByUser() => "Stopped by user";

    public static string QueuedTarget(string target) => $"Queued target: {target}";

    public static string RunningTarget(string target) => $"Running: {target}";

    public static string CompletedTarget(string target) => $"Completed: {target}";

    public static string StoppedTarget(string target) => $"Stopped: {target}";

    public static string PausedAndRequeuedTarget(string target) => $"Paused and re-queued: {target}";

    public static string FailedTarget(string target, string error) => $"Failed: {target} | {error}";

    public static string DispatcherPaused() => "Dispatcher paused by user.";

    public static string DispatcherResumed() => "Dispatcher resumed by user.";

    public static string DispatcherStopRequested() => "Dispatcher stop requested by user.";

    public static string QueueCount(int count) => $"Queued target count: {count}";

    public static string PluginNotInitialized() => "Plugin is not initialized.";

    public static string DispatcherPausedShort() => "Dispatcher paused.";

    public static string DispatcherResumedShort() => "Dispatcher resumed.";

    public static string DispatcherStopRequestedShort() => "Dispatcher stop requested.";

    public static string UnknownControlAction(string action) => $"Unknown control action: {action}.";

    public static string OneTargetRequired() =>
        "Please provide exactly one target: TargetFolderPath or TargetFilePath.";

    public static string TargetFileNotExists() => "Target file does not exist.";

    public static string TargetFolderNotExists(string folder) => $"Target folder does not exist: {folder}";

    public static string ManualAccepted() => "Manual translation job accepted.";

    public static string PluginDisabled() => "Plugin is disabled. Enable plugin first.";
}
