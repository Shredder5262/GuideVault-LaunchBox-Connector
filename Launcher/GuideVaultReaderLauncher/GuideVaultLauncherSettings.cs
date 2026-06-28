using System.Text.Json;

namespace GuideVaultReaderLauncher;

internal sealed class GuideVaultLauncherSettings
{
    public string GuideVaultUrl { get; set; } = "http://localhost:5478";
    public bool UseBrowserLoginBridge { get; set; } = true;
    public bool OpenReaderMaximized { get; set; } = false;
    public bool OpenReaderFullscreen { get; set; } = false;
    public string GuideVaultUsername { get; set; } = string.Empty;
    public string GuideVaultEmail { get; set; } = string.Empty;
    public string GuideVaultPassword { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;

    public bool HasBrowserLoginProfile =>
        (!string.IsNullOrWhiteSpace(GuideVaultUsername) || !string.IsNullOrWhiteSpace(GuideVaultEmail))
        && !string.IsNullOrWhiteSpace(GuideVaultPassword);

    public static GuideVaultLauncherSettings Load(string settingsPath, string pluginDirectory)
    {
        try
        {
            var resolvedPath = ResolveSettingsPath(settingsPath, pluginDirectory);
            LauncherLogger.Log("Loading launcher settings. requestedPath=" + settingsPath + " pluginDirectory=" + pluginDirectory + " resolvedPath=" + resolvedPath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                LauncherLogger.Log("Launcher settings file was not found; using defaults.");
                return new GuideVaultLauncherSettings();
            }

            var json = File.ReadAllText(resolvedPath);
            var settings = Normalize(JsonSerializer.Deserialize<GuideVaultLauncherSettings>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new GuideVaultLauncherSettings());
            LauncherLogger.Log(
                "Launcher settings loaded. guideVaultUrl=" + settings.GuideVaultUrl +
                " useBridge=" + settings.UseBrowserLoginBridge +
                " maximized=" + settings.OpenReaderMaximized +
                " fullscreen=" + settings.OpenReaderFullscreen +
                " usernameSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultUsername) +
                " emailSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultEmail) +
                " passwordSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultPassword));
            return settings;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Failed to load GuideVault launcher settings: " + ex.Message);
            return new GuideVaultLauncherSettings();
        }
    }

    private static GuideVaultLauncherSettings Normalize(GuideVaultLauncherSettings settings)
    {
        var url = (settings.GuideVaultUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:5478";

        return new GuideVaultLauncherSettings
        {
            GuideVaultUrl = url,
            UseBrowserLoginBridge = settings.UseBrowserLoginBridge,
            OpenReaderMaximized = settings.OpenReaderMaximized,
            OpenReaderFullscreen = settings.OpenReaderFullscreen,
            GuideVaultUsername = (settings.GuideVaultUsername ?? string.Empty).Trim(),
            GuideVaultEmail = (settings.GuideVaultEmail ?? string.Empty).Trim(),
            GuideVaultPassword = settings.GuideVaultPassword ?? string.Empty,
            TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds <= 0 ? 300 : settings.TimeoutSeconds, 10, 600)
        };
    }

    private static string ResolveSettingsPath(string settingsPath, string pluginDirectory)
    {
        var trimmed = (settingsPath ?? string.Empty).Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            try
            {
                return Path.IsPathRooted(trimmed) ? Path.GetFullPath(trimmed) : Path.GetFullPath(trimmed);
            }
            catch
            {
                return trimmed;
            }
        }

        var pluginDir = (pluginDirectory ?? string.Empty).Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(pluginDir))
        {
            try
            {
                return Path.Combine(Path.GetFullPath(pluginDir), "settings.json");
            }
            catch
            {
                return Path.Combine(pluginDir, "settings.json");
            }
        }

        return string.Empty;
    }
}
