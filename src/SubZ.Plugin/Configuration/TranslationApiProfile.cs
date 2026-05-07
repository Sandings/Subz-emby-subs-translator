namespace SubZ.Plugin.Configuration;

public sealed class TranslationApiProfile
{
    public string Name { get; set; } = "default";
    public string Provider { get; set; } = "deepseek";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-v4-flash";
    public int TimeoutSeconds { get; set; } = 90;
    public double Temperature { get; set; } = 0.1;
    public int BatchSize { get; set; } = 120;
    public int ParallelRequests { get; set; } = 2;
    public int RetryCount { get; set; } = 2;
}
