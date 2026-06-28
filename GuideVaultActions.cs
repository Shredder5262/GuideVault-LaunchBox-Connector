using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

internal static class GuideVaultActions
{
    public static async Task<string> TestConnectionAsync()
    {
        var settings = SettingsStore.Load();
        var status = await new GuideVaultClient(settings).GetStatusAsync().ConfigureAwait(false);
        var job = status.SyncJob;
        return $"Connection succeeded. GuideVault {status.Version}. Synced games: {status.GameCount}. Active matches: {status.MatchCount}. Current job: {job?.Status ?? "Unknown"}.";
    }

    public static async Task<string> SyncLibraryAsync(IReadOnlyList<string>? selectedPlatforms = null, IReadOnlyList<string>? matchTypes = null)
    {
        var settings = SettingsStore.Load();
        var games = PluginHelper.DataManager.GetAllGames() ?? Array.Empty<IGame>();
        var selected = selectedPlatforms?
            .Select(platform => platform?.Trim() ?? string.Empty)
            .Where(platform => !string.IsNullOrWhiteSpace(platform))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var requestedMatchTypes = matchTypes?
            .Select(type => type?.Trim() ?? string.Empty)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await Task.Run(async () =>
        {
            var request = LaunchBoxGameMapper.BuildSyncRequest(games, settings, selected, selected is { Count: > 0 }, requestedMatchTypes);
            var platformFilter = request.SyncMode.Equals("MergeSelectedPlatforms", StringComparison.OrdinalIgnoreCase)
                ? $" Selected platforms: {string.Join(", ", request.SelectedPlatforms)}. Sent {request.Games.Count:N0} LaunchBox game(s) for selected-platform merge."
                : " Sent the full LaunchBox catalog.";
            var matchScope = request.MatchTypes.Count > 0
                ? $" Match scope: {string.Join(", ", request.MatchTypes)} only."
                : " Match scope: all GuideVault content types.";
            var client = new GuideVaultClient(settings);
            await client.GetStatusAsync().ConfigureAwait(false);
            var result = await client.SyncAsync(request).ConfigureAwait(false);
            GuideVaultBadgeCache.Invalidate();
            GuideVaultBadgeCache.RefreshAfterCurrentSyncJobCompletes(settings);
            return $"Sync submitted. Imported games reported by GuideVault: {result.TotalGames:N0}. Job: {Blank(result.JobId, "None")}. Status: {Blank(result.JobStatus, "Unknown")}. Matching runs in GuideVault background. GuideVault badge cache will refresh again after the match job completes.{platformFilter}{matchScope}";
        }).ConfigureAwait(false);
    }

    public static async Task<GuideVaultCoverageResult> GetCoverageAsync()
    {
        var settings = SettingsStore.Load();
        return await new GuideVaultClient(settings).GetCoverageAsync().ConfigureAwait(false);
    }

    public static async Task<string> RefreshBadgeCacheAsync()
    {
        var status = await GuideVaultBadgeCache.RefreshNowAsync().ConfigureAwait(false);
        var localGames = PluginHelper.DataManager.GetAllGames() ?? Array.Empty<IGame>();
        var audit = GuideVaultBadgeCache.AuditLocalGames(localGames);
        var when = status.LastSuccessfulRefresh == DateTimeOffset.MinValue ? "never" : status.LastSuccessfulRefresh.LocalDateTime.ToString("g");
        var viewRefresh = TryRefreshLaunchBoxView() ? "LaunchBox view refresh requested." : "LaunchBox view refresh unavailable; switch platform/view or restart LaunchBox.";
        var examples = audit.ExampleMatches.Count > 0 ? $" Examples: {string.Join("; ", audit.ExampleMatches)}." : string.Empty;
        return $"GuideVault badge cache refreshed. Server matched IDs: {status.MatchedGameIds:N0}. Matched title/platform keys: {status.MatchedGameKeys:N0}. Local LaunchBox badge matches: {audit.LocalMatchedGames:N0}/{audit.LocalGames:N0}. Last refresh: {when}. {viewRefresh}{examples}";
    }

    public static string TestSelectedGameBadge(IGame? game)
    {
        var result = GuideVaultBadgeCache.DescribeGameMatch(game);
        TryRefreshLaunchBoxView();
        return result;
    }

    public static void TestGameBadgeDirect(IGame? game)
    {
        var result = TestSelectedGameBadge(game);
        GuideVaultConnectorWindow.AppendExternalLog(result);
        try
        {
            MessageBox.Show(result, "GuideVault Badge Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            // LaunchBox may not allow message boxes in every context; the connector log still receives the result.
        }
    }

    public static string SetForceGuideVaultBadgeOnAllGames(bool enabled)
    {
        var settings = SettingsStore.Load();
        settings.ForceGuideVaultBadgeOnAllGames = enabled;
        SettingsStore.Save(settings);
        GuideVaultBadgeCache.Invalidate();
        TryRefreshLaunchBoxView();
        return enabled
            ? "GuideVault badge debug mode enabled: the GuideVault badge should apply to every LaunchBox game after LaunchBox refreshes/restarts."
            : "GuideVault badge debug mode disabled: the GuideVault badge only applies to games with matched GuideVault items.";
    }

    private static bool TryRefreshLaunchBoxView()
    {
        try
        {
            PluginHelper.LaunchBoxMainViewModel?.RefreshData();
            return PluginHelper.LaunchBoxMainViewModel is not null;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<(GuideVaultStatusResult Status, GuideVaultSyncJobStatus Job)> GetSyncStatusAsync()
    {
        var settings = SettingsStore.Load();
        var client = new GuideVaultClient(settings);
        var status = await client.GetStatusAsync().ConfigureAwait(false);
        var job = status.SyncJob ?? await client.GetCurrentSyncJobAsync().ConfigureAwait(false);
        return (status, job);
    }

    public static async Task<string> CancelSyncAsync()
    {
        var settings = SettingsStore.Load();
        var job = await new GuideVaultClient(settings).CancelCurrentSyncJobAsync().ConfigureAwait(false);
        return $"Cancel request sent. Job: {Blank(job.JobId, "None")}. Status: {job.Status}. {job.Message}".Trim();
    }

    public static async Task<string> OpenDocumentAsync(IGame? game, string matchType, string guideVaultItemId = "")
    {
        if (game is null) return "No LaunchBox game was selected.";

        var settings = SettingsStore.Load();
        var client = new GuideVaultClient(settings);
        var result = await client.OpenAsync(new GuideVaultOpenRequest
        {
            LaunchBoxGameId = game.Id,
            MatchType = matchType,
            GuideVaultItemId = guideVaultItemId ?? string.Empty,
            ItemId = guideVaultItemId ?? string.Empty,
            BroadcastOpenSignal = !settings.OpenInEmbeddedWindow && !settings.OpenInDefaultBrowser,
            SuppressOpenSignal = settings.OpenInEmbeddedWindow || settings.OpenInDefaultBrowser
        }).ConfigureAwait(true);

        if (!result.Found)
        {
            return !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : $"No active {matchType} match was found for {game.Title}. Run GuideVault sync/rematch first, then confirm a match if needed.";
        }

        var targetUrl = !string.IsNullOrWhiteSpace(result.AbsoluteReaderUrl)
            ? result.AbsoluteReaderUrl
            : result.AbsoluteDetailsUrl;

        if (settings.OpenInEmbeddedWindow)
        {
            var launchUrl = await BuildLaunchUrlAsync(settings, targetUrl).ConfigureAwait(true);
            GuideVaultWebViewWindow.Open(launchUrl, $"GuideVault - {matchType}: {result.ItemTitle}", targetUrl);
            return $"Opened {matchType}: {result.ItemTitle} inside the LaunchBox GuideVault window.";
        }

        if (settings.OpenInDefaultBrowser)
        {
            var openMessage = await OpenBrowserTargetAsync(settings, targetUrl, $"{matchType}: {result.ItemTitle}").ConfigureAwait(false);
            return openMessage;
        }

        return $"Open request sent to the active GuideVault browser tab: {result.ItemTitle}";
    }

    public static void OpenDocumentDirect(IGame? game, string matchType)
    {
        OpenDocumentDirect(game, matchType, string.Empty);
    }

    public static async void OpenDocumentDirect(IGame? game, string matchType, string guideVaultItemId)
    {
        try
        {
            var message = await OpenDocumentAsync(game, matchType, guideVaultItemId).ConfigureAwait(true);
            GuideVaultConnectorWindow.AppendExternalLog(message);

            if (message.StartsWith("No ", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("did not return", StringComparison.OrdinalIgnoreCase))
            {
                GuideVaultConnectorWindow.ShowWindow(game, GuideVaultWindowStartupAction.None);
                GuideVaultConnectorWindow.AppendExternalLog(message);
            }
        }
        catch (Exception ex)
        {
            GuideVaultConnectorWindow.ShowWindow(game, GuideVaultWindowStartupAction.None);
            GuideVaultConnectorWindow.AppendExternalLog($"ERROR opening {matchType}: {ex.Message}");
        }
    }

    public static async Task<string> OpenGuideVaultInBrowserAsync()
    {
        var settings = SettingsStore.Load();
        var targetUrl = settings.GuideVaultUrl.TrimEnd('/') + "/";
        return await OpenTargetAsync(settings, targetUrl, "GuideVault home").ConfigureAwait(true);
    }

    public static async Task<string> OpenGuideVaultLaunchBoxPageAsync()
    {
        var settings = SettingsStore.Load();
        var targetUrl = settings.GuideVaultUrl.TrimEnd('/') + "/?settings=server&integration=launchbox";
        return await OpenTargetAsync(settings, targetUrl, "GuideVault LaunchBox page").ConfigureAwait(true);
    }

    public static async Task<string> OpenTargetAsync(GuideVaultConnectorSettings settings, string targetUrl, string label)
    {
        if (settings.OpenInEmbeddedWindow)
        {
            var launchUrl = await BuildLaunchUrlAsync(settings, targetUrl).ConfigureAwait(true);
            GuideVaultWebViewWindow.Open(launchUrl, "GuideVault - " + label, targetUrl);
            return $"Opened {label} inside the LaunchBox GuideVault window.";
        }

        return await OpenBrowserTargetAsync(settings, targetUrl, label).ConfigureAwait(false);
    }

    public static async Task<string> BuildLaunchUrlAsync(GuideVaultConnectorSettings settings, string targetUrl)
    {
        if (settings.UseBrowserLoginBridge && settings.HasBrowserLoginProfile)
        {
            try
            {
                var client = new GuideVaultClient(settings);
                var bridge = await client.CreateBrowserLoginLinkAsync(targetUrl).ConfigureAwait(false);
                if (bridge.Success && !string.IsNullOrWhiteSpace(bridge.Url)) return bridge.Url;

                if (!string.IsNullOrWhiteSpace(bridge.Message))
                    GuideVaultConnectorWindow.AppendExternalLog($"GuideVault login bridge did not return a usable link: {bridge.Message}");
            }
            catch (Exception ex)
            {
                GuideVaultConnectorWindow.AppendExternalLog($"GuideVault login bridge failed, using interactive sign-in page: {ex.Message}");
            }
        }

        return settings.GuideVaultUrl.TrimEnd('/') + "/launchbox/sign-in?target=" + Uri.EscapeDataString(targetUrl);
    }

    private static async Task<string> OpenBrowserTargetAsync(GuideVaultConnectorSettings settings, string targetUrl, string label)
    {
        var client = new GuideVaultClient(settings);
        var launchUrl = await BuildLaunchUrlAsync(settings, targetUrl).ConfigureAwait(false);
        client.OpenInBrowser(launchUrl);
        return $"Opened {label} through the GuideVault browser sign-in/login bridge.";
    }

    public static void OpenSettingsFile() => SettingsStore.OpenSettingsFile();

    private static string Blank(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
