using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Api;
using MediaBrowser.Model.Services;
using SubZ.Plugin.Services;

namespace SubZ.Plugin.Api;

[Route("/SubZ/Translate/Run", "POST", Summary = "Run translation manually for target folder or file")]
public sealed class RunManualTranslation : IReturn<ManualExecutionResponse>
{
    public string? TargetFolderPath { get; set; }
    public string? TargetFilePath { get; set; }
    public string? ControlAction { get; set; }
}

[Route("/SubZ/Translate/Queue", "GET", Summary = "Inspect queued translation targets")]
public sealed class GetTranslationQueue : IReturn<ManualExecutionResponse>
{
}

[Route("/SubZ/Translate/Status", "GET", Summary = "Inspect task statuses")]
public sealed class GetTranslationStatus : IReturn<object>
{
    public int LogLimit { get; set; } = 120;
    public string? LogSource { get; set; }
    public string? Cmd { get; set; }
}

public sealed class ManualTranslationService : BaseApiService
{
    private static readonly TranslationExecutionEngine Engine = new TranslationExecutionEngine();
    internal static readonly InMemoryTranslationJobDispatcher Dispatcher = new InMemoryTranslationJobDispatcher(HandleTargetAsync);

    public Task<ManualExecutionResponse> Post(RunManualTranslation request)
    {
        var action = NormalizeAction(request.ControlAction);
        if (!string.IsNullOrEmpty(action))
        {
            return Task.FromResult(HandleControlAction(action));
        }

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return Task.FromResult(new ManualExecutionResponse
            {
                Accepted = false,
                Message = UiText.PluginNotInitialized()
            });
        }

        var options = plugin.CurrentOptions;
        var orchestrator = new TranslationOrchestrator(
            options,
            new TranslationExecutionPolicy(options),
            Dispatcher,
            new TranslationProfileResolver());

        return orchestrator.RunManualAsync(
            new ManualExecutionRequest
            {
                TargetFolderPath = request.TargetFolderPath,
                TargetFilePath = request.TargetFilePath
            },
            CancellationToken.None);
    }

    public object Get(GetTranslationQueue request)
    {
        var queued = Dispatcher.SnapshotQueuedTargets();

        return new ManualExecutionResponse
        {
            Accepted = true,
            Message = UiText.QueueCount(queued.Count),
            PlannedTargets = queued
        };
    }

    public object Get(GetTranslationStatus request)
    {
        var cmd = (request.Cmd ?? string.Empty).Trim().ToLowerInvariant();
        switch (cmd)
        {
            case "pause":
                Dispatcher.Pause();
                break;
            case "resume":
                Dispatcher.Resume();
                break;
            case "stop":
                Dispatcher.StopAll();
                break;
        }

        var statuses = Dispatcher.SnapshotStatuses()
            .Select(s => new
            {
                s.Target,
                State = s.State.ToString(),
                s.Message,
                UpdatedAt = s.UpdatedAt.ToString("O")
            })
            .ToArray();

        var logSource = NormalizeLogSource(request.LogSource);
        var logs = logSource == "file"
            ? FileRuntimeLogger.ReadLastLines(request.LogLimit)
                .Select(line => new
                {
                    Timestamp = string.Empty,
                    Level = "File",
                    Message = line
                })
                .ToArray()
            : Dispatcher.SnapshotRuntimeLogs(request.LogLimit)
                .Select(l => new
                {
                    Timestamp = l.Timestamp.ToString("O"),
                    l.Level,
                    l.Message
                })
                .ToArray();

        var control = Dispatcher.SnapshotControl();

        return new
        {
            Count = statuses.Length,
            Items = statuses,
            Logs = logs,
            Debug = new
            {
                ServerNow = DateTimeOffset.UtcNow.ToString("O"),
                Cmd = cmd,
                LogLimit = request.LogLimit,
                LogSource = logSource
            },
            Control = new
            {
                control.IsRunning,
                control.IsPaused,
                control.QueuedCount
            },
            Log = new
            {
                Source = logSource,
                Directory = FileRuntimeLogger.GetLogDirectory()
            }
        };
    }

    private static string NormalizeAction(string? action)
    {
        return (action ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeLogSource(string? source)
    {
        var normalized = (source ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == "file" ? "file" : "memory";
    }

    private static ManualExecutionResponse HandleControlAction(string action)
    {
        switch (action)
        {
            case "pause":
                Dispatcher.Pause();
                return new ManualExecutionResponse
                {
                    Accepted = true,
                    Message = UiText.DispatcherPausedShort()
                };
            case "resume":
                Dispatcher.Resume();
                return new ManualExecutionResponse
                {
                    Accepted = true,
                    Message = UiText.DispatcherResumedShort()
                };
            case "stop":
                Dispatcher.StopAll();
                return new ManualExecutionResponse
                {
                    Accepted = true,
                    Message = UiText.DispatcherStopRequestedShort()
                };
            default:
                return new ManualExecutionResponse
                {
                    Accepted = false,
                    Message = UiText.UnknownControlAction(action)
                };
        }
    }

    private static Task HandleTargetAsync(string target, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            throw new InvalidOperationException("Plugin instance is not initialized.");
        }

        var options = plugin.CurrentOptions;
        return Engine.ProcessTargetAsync(target, options, cancellationToken);
    }

}
