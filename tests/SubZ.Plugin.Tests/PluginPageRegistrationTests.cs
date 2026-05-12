using System.Text.RegularExpressions;

namespace SubZ.Plugin.Tests;

public sealed class PluginPageRegistrationTests
{
    [Fact]
    public void PluginRegistersStatusDashboardLegacyAndCurrentJsNames()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plugin.cs"));

        var names = Regex.Matches(source, @"Name\s*=\s*""(?<name>[^""]+)""")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("SubZStatusJs", names);
        Assert.Contains("SubZStatusJsV2", names);
        Assert.Contains("StatusDashboardJs", names);
    }
}
