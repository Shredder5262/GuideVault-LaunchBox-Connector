namespace GuideVaultReaderLauncher;

internal sealed class LaunchRequest
{
    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = "GuideVault";
    public string SettingsPath { get; init; } = string.Empty;
    public string PluginDirectory { get; init; } = string.Empty;
    public string TargetUrl { get; init; } = string.Empty;

    public static LaunchRequest Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i] ?? string.Empty;
            if (!current.StartsWith("--", StringComparison.Ordinal)) continue;

            var value = i + 1 < args.Length ? args[i + 1] ?? string.Empty : string.Empty;
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                values[current] = string.Empty;
                continue;
            }

            values[current] = value;
            i++;
        }

        return new LaunchRequest
        {
            Url = NormalizeUrl(Get(values, "--url")),
            Title = string.IsNullOrWhiteSpace(Get(values, "--title")) ? "GuideVault" : Get(values, "--title").Trim(),
            SettingsPath = NormalizePath(Get(values, "--settings")),
            PluginDirectory = NormalizePath(Get(values, "--plugin-dir")),
            TargetUrl = NormalizeUrl(Get(values, "--target-url"))
        };
    }

    private static string Get(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

    private static string NormalizePath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "http://" + trimmed;
    }
}
