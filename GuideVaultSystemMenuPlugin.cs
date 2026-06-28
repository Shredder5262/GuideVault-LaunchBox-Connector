using System.Drawing;
using Unbroken.LaunchBox.Plugins;

namespace GuideVault.LaunchBoxConnector;

public sealed class GuideVaultSystemMenuPlugin : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault";
    public Image? IconImage => GuideVaultAssets.MenuIcon();
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public void OnSelected()
    {
        GuideVaultConnectorWindow.ShowWindow(null, GuideVaultWindowStartupAction.Settings);
    }
}
