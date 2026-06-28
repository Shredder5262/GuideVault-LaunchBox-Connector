using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

public sealed class OpenManualMenuItem : IGameMenuItemPlugin
{
    public string Caption => "GuideVault: Open Manual";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => true;
    public bool SupportsMultipleGames => false;

    public bool GetIsValidForGame(IGame selectedGame) => selectedGame is not null;
    public bool GetIsValidForGames(IGame[] selectedGames) => selectedGames?.Length == 1;

    public void OnSelected(IGame selectedGame) => Open(selectedGame, "Manual");
    public void OnSelected(IGame[] selectedGames)
    {
        var game = selectedGames?.FirstOrDefault();
        if (game is not null) Open(game, "Manual");
    }

    private static void Open(IGame game, string matchType)
    {
        try
        {
            var settings = SettingsStore.Load();
            var client = new GuideVaultClient(settings);
            var result = client.OpenAsync(new GuideVaultOpenRequest
            {
                LaunchBoxGameId = game.Id,
                MatchType = matchType,
                BroadcastOpenSignal = !settings.OpenInEmbeddedWindow && !settings.OpenInDefaultBrowser,
                SuppressOpenSignal = settings.OpenInEmbeddedWindow || settings.OpenInDefaultBrowser
            }).GetAwaiter().GetResult();
            if (!result.Found)
            {
                MessageBox.Show(result.Message.Length > 0 ? result.Message : $"No active {matchType} match was found for {game.Title}. Run GuideVault sync/rematch first, then confirm a match if needed.", "GuideVault LaunchBox Connector", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (settings.OpenInDefaultBrowser)
            {
                client.OpenInBrowser(result.AbsoluteReaderUrl);
                return;
            }

            MessageBox.Show(
                $"Open request sent to the active GuideVault browser tab.\n\n{result.ItemTitle}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open GuideVault manual.\n\n{ex.Message}", "GuideVault LaunchBox Connector", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
