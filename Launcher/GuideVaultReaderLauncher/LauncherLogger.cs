namespace GuideVaultReaderLauncher;

internal static class LauncherLogger
{
    private static string _logPath = Path.Combine(
        AppContext.BaseDirectory,
        "GuideVaultReaderLauncher.log");

    public static void Configure(string settingsPath)
    {
        try
        {
            var directory = !string.IsNullOrWhiteSpace(settingsPath)
                ? Path.GetDirectoryName(settingsPath)
                : null;

            if (!string.IsNullOrWhiteSpace(directory))
                _logPath = Path.Combine(directory, "GuideVaultReaderLauncher.log");

            Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory);
            Log("Logger configured. Path=" + _logPath);
        }
        catch
        {
            // Logging must never block the reader from opening.
        }
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? AppContext.BaseDirectory);
            File.AppendAllText(_logPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
