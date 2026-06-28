using System.Diagnostics;
using System.Windows.Forms;

namespace GuideVault.LaunchBoxConnector;

/// <summary>
/// Launches the GuideVault WebView2 reader in a separate helper process.
///
/// This intentionally mirrors the working Kavita/Convita reader pattern: keep WebView2
/// out of the LaunchBox plugin assembly and run it from LaunchBox\ThirdParty instead.
/// That avoids LaunchBox plugin-load-context/native WebView2Loader issues.
/// </summary>
internal static class GuideVaultWebViewWindow
{
    private const string LauncherFolderName = "GuideVaultReaderLauncher";
    private const string LauncherExeName = "GuideVaultReaderLauncher.exe";

    public static void Open(string url, string title = "GuideVault", string targetUrl = "")
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var launcherPath = GetLauncherExePath();
            if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
            {
                MessageBox.Show(
                    "GuideVault could not open inside LaunchBox because the WebView2 helper was not found." +
                    Environment.NewLine + Environment.NewLine +
                    "Expected path:" + Environment.NewLine + launcherPath +
                    Environment.NewLine + Environment.NewLine +
                    "Run scripts\\Build-Plugin.ps1, then scripts\\Install-Plugin.ps1 so the helper is deployed under LaunchBox\\ThirdParty\\GuideVaultReaderLauncher.",
                    "GuideVault LaunchBox Connector",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = launcherPath,
                WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add("--url");
            startInfo.ArgumentList.Add(NormalizeUrl(url));
            startInfo.ArgumentList.Add("--title");
            startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(title) ? "GuideVault" : title.Trim());
            startInfo.ArgumentList.Add("--settings");
            startInfo.ArgumentList.Add(SettingsStore.SettingsPath);
            startInfo.ArgumentList.Add("--plugin-dir");
            startInfo.ArgumentList.Add(SettingsStore.SettingsDirectory);

            if (!string.IsNullOrWhiteSpace(targetUrl))
            {
                startInfo.ArgumentList.Add("--target-url");
                startInfo.ArgumentList.Add(NormalizeUrl(targetUrl));
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "GuideVault could not open the embedded WebView2 helper." +
                Environment.NewLine + Environment.NewLine + ex.Message,
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string GetLauncherExePath()
    {
        var launchBoxRoot = AppDomain.CurrentDomain.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            ?? string.Empty;

        var installedPath = Path.Combine(launchBoxRoot, "ThirdParty", LauncherFolderName, LauncherExeName);
        if (File.Exists(installedPath)) return installedPath;

        // Source-tree fallback for debugging from a development build.
        var pluginDirectory = SettingsStore.SettingsDirectory;
        var sourceFallback = Path.Combine(pluginDirectory, "Launcher", LauncherFolderName, "bin", "Release", "net9.0-windows", LauncherExeName);
        return sourceFallback;
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "http://" + trimmed;
    }
}
