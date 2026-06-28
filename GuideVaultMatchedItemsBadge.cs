using System.Drawing;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

public sealed class GuideVaultMatchedItemsBadge : IGameBadge
{
    public string Name => "GuideVault Matched Item";
    public string UniqueId => "GuideVault.MatchedItems";
    public int Index { get; set; } = 480;
    public Image DefaultIcon => GuideVaultAssets.BadgeIcon() ?? new Bitmap(24, 24);

    public bool GetAppliesToGame(IGame game)
    {
        return GuideVaultBadgeCache.AppliesTo(game);
    }
}
