using System;
using System.Collections.Generic;

namespace SubZ.Plugin.Api;
using SubZ.Plugin.Services;

public sealed class ManualExecutionRequest
{
    public string? TargetFolderPath { get; set; }
    public string? TargetFilePath { get; set; }

    public bool IsValid(out string error)
    {
        var hasFolder = !string.IsNullOrWhiteSpace(TargetFolderPath);
        var hasFile = !string.IsNullOrWhiteSpace(TargetFilePath);

        if (hasFolder == hasFile)
        {
            error = UiText.OneTargetRequired();
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class ManualExecutionResponse
{
    public bool Accepted { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> PlannedTargets { get; set; } = Array.Empty<string>();
    public string? ActiveProfileName { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
