using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;

namespace GuideVault.LaunchBoxConnector;

public sealed class OpenSettingsSystemMenuItem : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault: Open Connector Settings";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public void OnSelected()
    {
        try
        {
            SettingsStore.OpenSettingsFile();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open settings file.\n\n{ex.Message}", "GuideVault LaunchBox Connector", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
