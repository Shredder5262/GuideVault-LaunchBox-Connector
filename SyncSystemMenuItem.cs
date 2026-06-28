using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

public sealed class SyncSystemMenuItem : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault: Sync Library";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public async void OnSelected()
    {
        try
        {
            var settings = SettingsStore.Load();
            var games = PluginHelper.DataManager.GetAllGames() ?? Array.Empty<IGame>();

            var result = await Task.Run(async () =>
            {
                var request = LaunchBoxGameMapper.BuildSyncRequest(games, settings);
                var client = new GuideVaultClient(settings);
                await client.GetStatusAsync().ConfigureAwait(false);
                return await client.SyncAsync(request).ConfigureAwait(false);
            }).ConfigureAwait(true);

            var job = result.Job;
            var jobLine = !string.IsNullOrWhiteSpace(result.JobId)
                ? $"\nJob: {result.JobId}\nJob Status: {result.JobStatus}"
                : string.Empty;

            MessageBox.Show(
                $"GuideVault LaunchBox sync was submitted.\n\nImported games: {result.TotalGames}{jobLine}\n\nGuideVault will run matching in the background. Use 'GuideVault: Sync Status' to check progress.",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (TaskCanceledException ex)
        {
            var settings = SettingsStore.Load();
            MessageBox.Show(
                $"GuideVault sync timed out before GuideVault accepted the import.\n\nSettings: {SettingsStore.SettingsPath}\nCurrent TimeoutSeconds: {settings.TimeoutSeconds}\n\nTry confirming GuideVault is running, then test with MaxGamesToSync set to 100.\n\n{ex.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"GuideVault sync failed.\n\nCheck that GuideVault is running and that the connector settings are correct.\n\nSettings: {SettingsStore.SettingsPath}\n\n{ex.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
