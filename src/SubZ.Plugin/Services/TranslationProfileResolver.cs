using System;
using System.Linq;
using SubZ.Plugin.Configuration;

namespace SubZ.Plugin.Services;

public sealed class TranslationProfileResolver
{
    public TranslationApiProfile ResolveActiveProfile(PluginOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Profiles != null && options.Profiles.Count > 0)
        {
            return Normalize(options.Profiles[0]);
        }

        // Backward compatibility fallback to legacy single-profile fields.
        return Normalize(new TranslationApiProfile
        {
            Name = "default",
            Provider = options.ApiProvider,
            BaseUrl = options.ApiBaseUrl,
            ApiKey = options.ApiKey,
            Model = options.Model
        });
    }

    private static TranslationApiProfile Normalize(TranslationApiProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "default" : profile.Name.Trim();
        profile.Provider = string.IsNullOrWhiteSpace(profile.Provider) ? "deepseek" : profile.Provider.Trim();
        profile.BaseUrl = string.IsNullOrWhiteSpace(profile.BaseUrl) ? "https://api.deepseek.com" : profile.BaseUrl.Trim();
        profile.Model = string.IsNullOrWhiteSpace(profile.Model) ? "deepseek-v4-flash" : profile.Model.Trim();
        profile.TimeoutSeconds = profile.TimeoutSeconds <= 0 ? 90 : profile.TimeoutSeconds;
        profile.BatchSize = profile.BatchSize <= 0 ? 120 : profile.BatchSize;
        profile.ParallelRequests = profile.ParallelRequests <= 0 ? 1 : profile.ParallelRequests;
        profile.RetryCount = profile.RetryCount < 0 ? 0 : profile.RetryCount;

        return profile;
    }
}
