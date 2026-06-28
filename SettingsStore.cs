using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace GuideVault.LaunchBoxConnector;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string SettingsDirectory
    {
        get
        {
            var assemblyPath = typeof(SettingsStore).Assembly.Location;
            var pluginDirectory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(pluginDirectory)) return pluginDirectory;
            return AppContext.BaseDirectory;
        }
    }

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static string LegacySettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ConnectorConstants.SettingsFolderName,
        ConnectorConstants.SettingsSubFolderName);

    private static string LegacySettingsPath => Path.Combine(LegacySettingsDirectory, "settings.json");

    public static GuideVaultConnectorSettings Load()
    {
        Directory.CreateDirectory(SettingsDirectory);
        MigrateLegacySettingsIfNeeded();

        if (!File.Exists(SettingsPath))
        {
            var created = new GuideVaultConnectorSettings();
            Save(created);
            return created;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<GuideVaultConnectorSettings>(File.ReadAllText(SettingsPath), JsonOptions);
            var normalized = Normalize(loaded ?? new GuideVaultConnectorSettings());
            Save(normalized);
            return normalized;
        }
        catch
        {
            var fallback = new GuideVaultConnectorSettings();
            Save(fallback);
            return fallback;
        }
    }

    public static void Save(GuideVaultConnectorSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Normalize(settings), JsonOptions));
    }

    public static void OpenSettingsFile()
    {
        Directory.CreateDirectory(SettingsDirectory);
        if (!File.Exists(SettingsPath)) Save(new GuideVaultConnectorSettings());
        Process.Start(new ProcessStartInfo(SettingsPath) { UseShellExecute = true });
    }

    private static void MigrateLegacySettingsIfNeeded()
    {
        try
        {
            if (File.Exists(SettingsPath) || !File.Exists(LegacySettingsPath)) return;
            File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
        }
        catch
        {
            // Keep plugin startup resilient. If migration fails, a fresh settings file will be created in the plugin folder.
        }
    }

    private static List<string> NormalizePlatformList(IEnumerable<string>? platforms) => (platforms ?? Array.Empty<string>())
        .Select(platform => (platform ?? string.Empty).Trim())
        .Where(platform => !string.IsNullOrWhiteSpace(platform))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static GuideVaultConnectorSettings Normalize(GuideVaultConnectorSettings settings)
    {
        var url = (settings.GuideVaultUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:5478";
        return new GuideVaultConnectorSettings
        {
            GuideVaultUrl = url,
            OpenInEmbeddedWindow = settings.OpenInEmbeddedWindow,
            OpenInDefaultBrowser = settings.OpenInDefaultBrowser,
            UseBrowserLoginBridge = settings.UseBrowserLoginBridge,
            OpenReaderMaximized = settings.OpenReaderMaximized,
            OpenReaderFullscreen = settings.OpenReaderFullscreen,
            GuideVaultUsername = (settings.GuideVaultUsername ?? string.Empty).Trim(),
            GuideVaultEmail = (settings.GuideVaultEmail ?? string.Empty).Trim(),
            GuideVaultPassword = settings.GuideVaultPassword ?? string.Empty,
            TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds <= 0 ? 300 : settings.TimeoutSeconds, 10, 600),
            IncludeCustomFields = settings.IncludeCustomFields,
            IncludeAlternateNames = settings.IncludeAlternateNames,
            MaxGamesToSync = 0,
            LimitManualSyncToSelectedPlatforms = settings.LimitManualSyncToSelectedPlatforms,
            ManualSyncPlatforms = NormalizePlatformList(settings.ManualSyncPlatforms),
            LimitStrategyGuideSyncToSelectedPlatforms = settings.LimitStrategyGuideSyncToSelectedPlatforms,
            StrategyGuideSyncPlatforms = NormalizePlatformList(settings.StrategyGuideSyncPlatforms),
            LimitMagazineSyncToSelectedPlatforms = settings.LimitMagazineSyncToSelectedPlatforms,
            MagazineSyncPlatforms = NormalizePlatformList(settings.MagazineSyncPlatforms),
            ForceGuideVaultBadgeOnAllGames = false
        };
    }
}
