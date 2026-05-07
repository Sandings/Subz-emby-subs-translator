using System;
using System.Collections.Generic;
using System.IO;
using SubZ.Plugin.Api;
using SubZ.Plugin.Configuration;

namespace SubZ.Plugin.Services;

public sealed class TranslationExecutionPolicy
{
    private readonly PluginOptions _options;

    public TranslationExecutionPolicy(PluginOptions options)
    {
        _options = options;
    }

    public bool ShouldRunOnLibraryIngest()
    {
        if (!_options.Enabled)
        {
            return false;
        }

        // Key requirement: if manual-target-only mode is enabled,
        // do not execute automatically during library ingest.
        if (_options.ManualTargetOnlyMode)
        {
            return false;
        }

        return true;
    }

    public IReadOnlyList<string> BuildManualTargets(ManualExecutionRequest request)
    {
        if (!request.IsValid(out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetFilePath))
        {
            var filePath = request.TargetFilePath!.Trim();
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(UiText.TargetFileNotExists(), filePath);
            }

            return new[] { filePath };
        }

        var folderPath = request.TargetFolderPath!.Trim();
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(UiText.TargetFolderNotExists(folderPath));
        }

        return new[] { folderPath };
    }
}
