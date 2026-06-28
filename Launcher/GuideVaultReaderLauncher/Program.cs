using System.Windows.Forms;

namespace GuideVaultReaderLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var request = LaunchRequest.Parse(args);
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            MessageBox.Show(
                "GuideVault Reader Launcher did not receive a URL to open.",
                "GuideVault Reader Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        LauncherLogger.Configure(request.SettingsPath);
        LauncherLogger.Log("Start. Url=" + request.Url + " Title=" + request.Title + " PluginDirectory=" + request.PluginDirectory);

        using var form = new GuideVaultReaderForm(request.Url, request.Title, request.SettingsPath, request.PluginDirectory, request.TargetUrl);
        Application.Run(form);
    }
}
