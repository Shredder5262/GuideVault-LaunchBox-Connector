using System.Drawing;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

public sealed class GuideVaultGameMenuPlugin : IGameMultiMenuItemPlugin
{
    private const int MenuMatchLookupSeconds = 2;

    public IEnumerable<IGameMenuItem> GetMenuItems(params IGame[] selectedGames)
    {
        var hasOneGame = selectedGames is { Length: 1 } && selectedGames[0] is not null;
        var game = hasOneGame ? selectedGames[0] : null;
        var matches = game is null ? Array.Empty<GuideVaultGameMatchView>() : TryLoadActiveMatches(game);

        return new[]
        {
            new GuideVaultGameMenuItem(
                "GuideVault",
                enabled: hasOneGame,
                icon: GuideVaultAssets.MenuIcon(),
                children: new[]
                {
                    new GuideVaultGameMenuItem(
                        "Open",
                        enabled: hasOneGame,
                        children: BuildOpenMenuItems(hasOneGame, matches))
                })
        };
    }

    private static IEnumerable<IGameMenuItem> BuildOpenMenuItems(bool enabled, IReadOnlyList<GuideVaultGameMatchView> matches)
    {
        var items = new List<IGameMenuItem>
        {
            new GuideVaultGameMenuItem("Manual", enabled, null, games => RunOpen(games, "Manual"))
        };

        items.Add(BuildMatchedDocumentMenu("Strategy Guide", "Strategy Guides", enabled, matches));
        items.Add(BuildMatchedDocumentMenu("Magazine", "Magazines", enabled, matches));
        return items;
    }

    private static IGameMenuItem BuildMatchedDocumentMenu(string matchType, string pluralCaption, bool enabled, IReadOnlyList<GuideVaultGameMatchView> matches)
    {
        var typedMatches = matches
            .Where(match => string.Equals(NormalizeMatchType(match.MatchType), matchType, StringComparison.OrdinalIgnoreCase))
            .Where(match => !string.IsNullOrWhiteSpace(match.GuideVaultItemId))
            .GroupBy(match => match.GuideVaultItemId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(match => IsConfirmed(match.MatchStatus)).ThenByDescending(match => match.ConfidenceScore).First())
            .OrderBy(match => CaptionTitle(match.GuideVaultItemTitle), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (typedMatches.Count == 0)
            return new GuideVaultGameMenuItem(matchType, enabled, null, games => RunOpen(games, matchType));

        if (typedMatches.Count == 1)
        {
            var match = typedMatches[0];
            return new GuideVaultGameMenuItem($"{matchType}: {Shorten(CaptionTitle(match.GuideVaultItemTitle), 72)}", enabled, null, games => RunOpen(games, matchType, match.GuideVaultItemId));
        }

        var children = typedMatches
            .Select(match => new GuideVaultGameMenuItem(Shorten(CaptionTitle(match.GuideVaultItemTitle), 96), enabled, null, games => RunOpen(games, matchType, match.GuideVaultItemId)))
            .Cast<IGameMenuItem>()
            .ToList();

        return new GuideVaultGameMenuItem($"{pluralCaption} ({typedMatches.Count})", enabled, children);
    }

    private static IReadOnlyList<GuideVaultGameMatchView> TryLoadActiveMatches(IGame game)
    {
        try
        {
            var settings = SettingsStore.Load();
            var client = new GuideVaultClient(settings);
            var details = client.GetGameMatchesAsync(game.Id, TimeSpan.FromSeconds(MenuMatchLookupSeconds)).GetAwaiter().GetResult();
            return (details.Matches ?? new List<GuideVaultGameMatchView>())
                .Where(match => IsActive(match.MatchStatus))
                .ToList();
        }
        catch (Exception ex)
        {
            GuideVaultConnectorWindow.AppendExternalLog($"GuideVault right-click match lookup failed for {game.Title}: {ex.Message}");
            return Array.Empty<GuideVaultGameMatchView>();
        }
    }

    private static void RunOpen(IGame[]? games, string matchType, string guideVaultItemId = "")
    {
        var game = games?.FirstOrDefault();
        GuideVaultActions.OpenDocumentDirect(game, matchType, guideVaultItemId);
    }

    private static bool IsActive(string? status) =>
        string.Equals(status, "AutoMatched", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase);

    private static bool IsConfirmed(string? status) => string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMatchType(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        if (text.Contains("magazine") || text.Contains("issue")) return "Magazine";
        if (text.Contains("strategy") || text.Contains("guide")) return "Strategy Guide";
        if (text.Contains("manual") || text.Contains("instruction")) return "Manual";
        return value ?? string.Empty;
    }

    private static string CaptionTitle(string? title) => string.IsNullOrWhiteSpace(title) ? "Untitled GuideVault item" : title.Trim();

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength) return trimmed;
        return trimmed[..Math.Max(1, maxLength - 1)].TrimEnd() + "…";
    }
}

internal sealed class GuideVaultGameMenuItem : IGameMenuItem
{
    private readonly Action<IGame[]>? _onSelect;
    private readonly Image? _icon;

    public GuideVaultGameMenuItem(string caption, bool enabled, IEnumerable<IGameMenuItem>? children = null, Action<IGame[]>? onSelect = null, Image? icon = null)
    {
        Caption = caption;
        Enabled = enabled;
        Children = children ?? Array.Empty<IGameMenuItem>();
        _onSelect = onSelect;
        _icon = icon;
    }

    public string Caption { get; }
    public IEnumerable<IGameMenuItem> Children { get; }
    public bool Enabled { get; }
    public Image? Icon => _icon;

    public void OnSelect(params IGame[] games)
    {
        if (!Enabled) return;
        _onSelect?.Invoke(games ?? Array.Empty<IGame>());
    }
}
