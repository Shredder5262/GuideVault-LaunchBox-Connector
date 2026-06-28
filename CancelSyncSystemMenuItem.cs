using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;

namespace GuideVault.LaunchBoxConnector;

public sealed class CancelSyncSystemMenuItem : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault: Cancel Sync";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public async void OnSelected()
    {
        try
        {
            var settings = SettingsStore.Load();
            var job = await new GuideVaultClient(settings).CancelCurrentSyncJobAsync().ConfigureAwait(true);

            MessageBox.Show(
                $"GuideVault cancel request sent.\n\nJob: {(string.IsNullOrWhiteSpace(job.JobId) ? "None" : job.JobId)}\nStatus: {job.Status}\nMessage: {job.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not cancel GuideVault sync.\n\nCheck that GuideVault is running and that GuideVaultUrl is correct in:\n{SettingsStore.SettingsPath}\n\n{ex.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
