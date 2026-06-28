using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;

namespace GuideVault.LaunchBoxConnector;

public sealed class TestConnectionSystemMenuItem : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault: Test Connection";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public async void OnSelected()
    {
        try
        {
            var settings = SettingsStore.Load();
            var status = await new GuideVaultClient(settings).GetStatusAsync().ConfigureAwait(true);
            MessageBox.Show(
                $"GuideVault connection succeeded.\n\nURL: {settings.GuideVaultUrl}\nGuideVault version: {status.Version}\nLaunchBox games currently synced: {status.GameCount}\nActive matches: {status.MatchCount}\nCurrent sync job: {status.SyncJob?.Status ?? "Unknown"}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"GuideVault connection failed.\n\nCheck that GuideVault is running and that GuideVaultUrl is correct in:\n{SettingsStore.SettingsPath}\n\n{ex.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
