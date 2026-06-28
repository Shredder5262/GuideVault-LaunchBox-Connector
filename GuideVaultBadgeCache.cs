using System.Text.Json;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

internal static class GuideVaultBadgeCache
{
    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Badge checks are called very often while LaunchBox paints game grids. Keep those calls memory-only
    // most of the time, and let network/disk refreshes happen on a controlled cadence.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DiskReloadCheckInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SettingsReloadCheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SyncJobWatchPollInterval = TimeSpan.FromSeconds(10);

    private static HashSet<string> _matchedGameIds = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> _matchedGameKeys = new(StringComparer.OrdinalIgnoreCase);
    private static DateTimeOffset _lastRefreshAttempt = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastSuccessfulRefresh = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastDiskReloadCheck = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastSettingsReloadCheck = DateTimeOffset.MinValue;
    private static DateTime _lastCacheFileWriteUtc = DateTime.MinValue;
    private static bool _loadedFromDisk;
    private static bool _refreshing;
    private static bool _syncJobWatcherActive;
    private static bool _forceAllGamesCached;

    private static string CachePath => Path.Combine(SettingsStore.SettingsDirectory, "badge-cache.json");

    public static bool AppliesTo(IGame? game)
    {
        if (game is null) return false;
        if (IsForceAllGamesEnabled()) return true;

        EnsureLoadedFromDisk();
        ReloadFromDiskIfNewerThrottled();
        StartRefreshIfNeeded();

        lock (SyncRoot)
        {
            return AppliesToLoaded(game);
        }
    }

    public static void Invalidate()
    {
        lock (SyncRoot)
        {
            _lastSuccessfulRefresh = DateTimeOffset.MinValue;
            _lastRefreshAttempt = DateTimeOffset.MinValue;
            _lastDiskReloadCheck = DateTimeOffset.MinValue;
        }
        StartRefreshIfNeeded(force: true);
    }

    public static async Task<GuideVaultBadgeCacheStatus> RefreshNowAsync()
    {
        return await RefreshAsync(SettingsStore.Load()).ConfigureAwait(false);
    }

    public static GuideVaultBadgeCacheStatus GetStatus()
    {
        EnsureLoadedFromDisk();
        ReloadFromDiskIfNewerThrottled();
        lock (SyncRoot)
        {
            return new GuideVaultBadgeCacheStatus
            {
                MatchedGameIds = _matchedGameIds.Count,
                MatchedGameKeys = _matchedGameKeys.Count,
                LastSuccessfulRefresh = _lastSuccessfulRefresh,
                LastRefreshAttempt = _lastRefreshAttempt
            };
        }
    }

    public static GuideVaultBadgeLocalAudit AuditLocalGames(IEnumerable<IGame>? games)
    {
        EnsureLoadedFromDisk();
        ReloadFromDiskIfNewerThrottled();

        var gameList = (games ?? Array.Empty<IGame>()).Where(game => game is not null).ToList();
        var examples = new List<string>();
        var matched = 0;

        lock (SyncRoot)
        {
            foreach (var game in gameList)
            {
                if (!AppliesToLoaded(game)) continue;
                matched++;
                if (examples.Count < 8) examples.Add($"{game.Title} [{game.Platform}]");
            }
        }

        return new GuideVaultBadgeLocalAudit
        {
            LocalGames = gameList.Count,
            LocalMatchedGames = matched,
            ExampleMatches = examples
        };
    }

    public static string DescribeGameMatch(IGame? game)
    {
        if (game is null) return "No LaunchBox game is selected. Use the right-click GuideVault > Test GuideVault Badge action to test a specific game because the connector window may not inherit LaunchBox grid selection.";
        var forceAll = IsForceAllGamesEnabled();
        EnsureLoadedFromDisk();
        ReloadFromDiskIfNewerThrottled();

        var id = NormalizeId(game.Id);
        var keys = GameKeyVariations(game.Title, game.Platform).ToList();

        lock (SyncRoot)
        {
            var idHit = !string.IsNullOrWhiteSpace(id) && _matchedGameIds.Contains(id);
            var keyHit = keys.FirstOrDefault(key => _matchedGameKeys.Contains(key));
            return $"Selected game badge test: {game.Title} [{game.Platform}]  Id match: {(idHit ? "yes" : "no")}. Title/platform match: {(!string.IsNullOrWhiteSpace(keyHit) ? "yes - " + keyHit : "no")}. Force all games: {(forceAll ? "yes" : "no")}. Badge applies: {(forceAll || idHit || !string.IsNullOrWhiteSpace(keyHit) ? "yes" : "no")}.";
        }
    }

    public static void RefreshAfterCurrentSyncJobCompletes(GuideVaultConnectorSettings settings)
    {
        lock (SyncRoot)
        {
            if (_syncJobWatcherActive) return;
            _syncJobWatcherActive = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var client = new GuideVaultClient(settings);
                var started = DateTimeOffset.Now;

                while (DateTimeOffset.Now - started < TimeSpan.FromMinutes(15))
                {
                    var job = await client.GetCurrentSyncJobAsync().ConfigureAwait(false);
                    if (!IsActiveJobStatus(job.Status))
                    {
                        await RefreshAsync(settings).ConfigureAwait(false);
                        TryRefreshLaunchBoxView();
                        return;
                    }

                    await Task.Delay(SyncJobWatchPollInterval).ConfigureAwait(false);
                }

                await RefreshAsync(settings).ConfigureAwait(false);
                TryRefreshLaunchBoxView();
            }
            catch
            {
                // Badge cache refresh is best-effort. LaunchBox should never fail because the GuideVault server is unavailable.
            }
            finally
            {
                lock (SyncRoot)
                {
                    _syncJobWatcherActive = false;
                }
            }
        });
    }

    public static bool IsForceAllGamesEnabled()
    {
        var now = DateTimeOffset.Now;
        lock (SyncRoot)
        {
            if (_lastSettingsReloadCheck != DateTimeOffset.MinValue && now - _lastSettingsReloadCheck < SettingsReloadCheckInterval)
                return _forceAllGamesCached;
            _lastSettingsReloadCheck = now;
        }

        var enabled = false;
        try
        {
            enabled = SettingsStore.Load().ForceGuideVaultBadgeOnAllGames;
        }
        catch
        {
            enabled = false;
        }

        lock (SyncRoot)
        {
            _forceAllGamesCached = enabled;
            return _forceAllGamesCached;
        }
    }

    private static bool AppliesToLoaded(IGame game)
    {
        var id = NormalizeId(game.Id);
        if (!string.IsNullOrWhiteSpace(id) && _matchedGameIds.Contains(id)) return true;

        foreach (var key in GameKeyVariations(game.Title, game.Platform))
        {
            if (_matchedGameKeys.Contains(key)) return true;
        }

        return false;
    }

    private static void EnsureLoadedFromDisk()
    {
        lock (SyncRoot)
        {
            if (_loadedFromDisk) return;
            _loadedFromDisk = true;
        }

        LoadFromDisk();
    }

    private static void ReloadFromDiskIfNewerThrottled()
    {
        var now = DateTimeOffset.Now;
        lock (SyncRoot)
        {
            if (_lastDiskReloadCheck != DateTimeOffset.MinValue && now - _lastDiskReloadCheck < DiskReloadCheckInterval)
                return;
            _lastDiskReloadCheck = now;
        }

        ReloadFromDiskIfNewer();
    }

    private static void ReloadFromDiskIfNewer()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var writeUtc = File.GetLastWriteTimeUtc(CachePath);
            lock (SyncRoot)
            {
                if (writeUtc <= _lastCacheFileWriteUtc) return;
            }
            LoadFromDisk();
        }
        catch
        {
            // Ignore cache timestamp/read failures; the in-memory cache remains authoritative for this session.
        }
    }

    private static void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var loaded = JsonSerializer.Deserialize<GuideVaultBadgeDiskCache>(File.ReadAllText(CachePath), JsonOptions);
            if (loaded is null) return;

            var ids = (loaded.MatchedGameIds ?? new List<string>())
                .Select(NormalizeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keys = (loaded.MatchedGameKeys ?? new List<string>())
                .Select(NormalizeId)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var generatedAt = loaded.GeneratedAt == default ? DateTimeOffset.MinValue : loaded.GeneratedAt;
            var writeUtc = File.GetLastWriteTimeUtc(CachePath);

            lock (SyncRoot)
            {
                _matchedGameIds = ids;
                _matchedGameKeys = keys;
                _lastSuccessfulRefresh = generatedAt;
                _lastCacheFileWriteUtc = writeUtc;
            }
        }
        catch
        {
            // Badges should never break LaunchBox startup. A failed cache read just means the badge appears after the next successful refresh.
        }
    }

    private static void StartRefreshIfNeeded(bool force = false)
    {
        GuideVaultConnectorSettings settings;
        lock (SyncRoot)
        {
            if (_refreshing) return;

            var now = DateTimeOffset.Now;
            var refreshDue = _lastSuccessfulRefresh == DateTimeOffset.MinValue || now - _lastSuccessfulRefresh >= RefreshInterval;
            var retryAllowed = _lastRefreshAttempt == DateTimeOffset.MinValue || now - _lastRefreshAttempt >= FailureBackoff;
            if (!force && (!refreshDue || !retryAllowed)) return;

            _refreshing = true;
            _lastRefreshAttempt = now;
        }

        try
        {
            settings = SettingsStore.Load();
        }
        catch
        {
            lock (SyncRoot)
            {
                _refreshing = false;
            }
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(settings).ConfigureAwait(false);
            }
            catch
            {
                // Keep the badge provider quiet; LaunchBox may call this frequently while drawing grids.
            }
            finally
            {
                lock (SyncRoot)
                {
                    _refreshing = false;
                }
            }
        });
    }

    private static async Task<GuideVaultBadgeCacheStatus> RefreshAsync(GuideVaultConnectorSettings settings)
    {
        var result = await new GuideVaultClient(settings).GetBadgeMapAsync().ConfigureAwait(false);
        var matchedRows = (result.Games ?? new List<GuideVaultBadgeGameResult>())
            .Where(g => g.TotalMatches > 0 || g.ManualMatches > 0 || g.StrategyGuideMatches > 0 || g.MagazineMatches > 0)
            .ToList();

        var ids = matchedRows
            .Select(g => NormalizeId(g.LaunchBoxGameId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var keys = matchedRows
            .SelectMany(g => GameKeyVariations(g.LaunchBoxGameTitle, g.LaunchBoxPlatform))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var refreshedAt = DateTimeOffset.Now;
        lock (SyncRoot)
        {
            _matchedGameIds = ids;
            _matchedGameKeys = keys;
            _lastSuccessfulRefresh = refreshedAt;
        }

        try
        {
            Directory.CreateDirectory(SettingsStore.SettingsDirectory);
            var disk = new GuideVaultBadgeDiskCache
            {
                GeneratedAt = result.GeneratedAt == default ? refreshedAt : result.GeneratedAt,
                MatchedGameIds = ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList(),
                MatchedGameKeys = keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList()
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(disk, JsonOptions));
            var writeUtc = File.GetLastWriteTimeUtc(CachePath);
            lock (SyncRoot)
            {
                _lastCacheFileWriteUtc = writeUtc;
                _lastDiskReloadCheck = refreshedAt;
            }
        }
        catch
        {
            // Cache persistence is best-effort only.
        }

        return new GuideVaultBadgeCacheStatus
        {
            MatchedGameIds = ids.Count,
            MatchedGameKeys = keys.Count,
            LastSuccessfulRefresh = refreshedAt,
            LastRefreshAttempt = _lastRefreshAttempt
        };
    }

    private static bool IsActiveJobStatus(string? status)
    {
        return string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "CancelRequested", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GameKeyVariations(string? title, string? platform)
    {
        var titles = TitleVariations(title).ToList();
        var platforms = PlatformVariations(platform).ToList();
        foreach (var p in platforms)
        foreach (var t in titles)
        {
            if (!string.IsNullOrWhiteSpace(t) && !string.IsNullOrWhiteSpace(p)) yield return $"{p}|{t}";
        }
    }

    private static IEnumerable<string> TitleVariations(string? title)
    {
        var normalized = NormalizeKeyPart(title);
        if (string.IsNullOrWhiteSpace(normalized)) yield break;
        yield return normalized;

        var stripped = StripBracketQualifiers(title);
        var strippedNormalized = NormalizeKeyPart(stripped);
        if (!string.IsNullOrWhiteSpace(strippedNormalized) && !string.Equals(strippedNormalized, normalized, StringComparison.OrdinalIgnoreCase))
            yield return strippedNormalized;
    }

    private static IEnumerable<string> PlatformVariations(string? platform)
    {
        var normalized = NormalizeKeyPart(platform);
        if (string.IsNullOrWhiteSpace(normalized)) yield break;

        yield return normalized;
        foreach (var alias in PlatformAliases(normalized))
            yield return alias;
    }

    private static IEnumerable<string> PlatformAliases(string normalizedPlatform)
    {
        switch (normalizedPlatform)
        {
            case "nes":
            case "nintendo nes":
            case "nintendo entertainment system":
                yield return "nes"; yield return "nintendo nes"; yield return "nintendo entertainment system";
                break;
            case "snes":
            case "super nintendo":
            case "super nintendo entertainment system":
                yield return "snes"; yield return "super nintendo"; yield return "super nintendo entertainment system";
                break;
            case "sega genesis":
            case "genesis":
            case "mega drive":
            case "sega mega drive":
                yield return "sega genesis"; yield return "genesis"; yield return "mega drive"; yield return "sega mega drive";
                break;
            case "turbografx 16":
            case "turbo grafx 16":
            case "pc engine":
                yield return "turbografx 16"; yield return "turbo grafx 16"; yield return "pc engine";
                break;
            case "playstation":
            case "sony playstation":
            case "ps1":
                yield return "playstation"; yield return "sony playstation"; yield return "ps1";
                break;
            case "playstation 2":
            case "sony playstation 2":
            case "ps2":
                yield return "playstation 2"; yield return "sony playstation 2"; yield return "ps2";
                break;
            case "game boy":
            case "nintendo game boy":
                yield return "game boy"; yield return "nintendo game boy";
                break;
            case "game boy color":
            case "nintendo game boy color":
                yield return "game boy color"; yield return "nintendo game boy color";
                break;
            case "game boy advance":
            case "nintendo game boy advance":
            case "gba":
                yield return "game boy advance"; yield return "nintendo game boy advance"; yield return "gba";
                break;
        }
    }

    private static string StripBracketQualifiers(string? value)
    {
        var text = value ?? string.Empty;
        while (true)
        {
            var start = text.LastIndexOf('(');
            var end = text.LastIndexOf(')');
            if (start < 0 || end <= start) break;
            text = (text[..start] + " " + text[(end + 1)..]).Trim();
        }
        while (true)
        {
            var start = text.LastIndexOf('[');
            var end = text.LastIndexOf(']');
            if (start < 0 || end <= start) break;
            text = (text[..start] + " " + text[(end + 1)..]).Trim();
        }
        return text;
    }

    private static string NormalizeKeyPart(string? value)
    {
        var chars = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();
        return string.Join(" ", new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeId(string? value) => (value ?? string.Empty).Trim();

    private static void TryRefreshLaunchBoxView()
    {
        try
        {
            Unbroken.LaunchBox.Plugins.PluginHelper.LaunchBoxMainViewModel?.RefreshData();
        }
        catch
        {
            // UI refresh is best-effort.
        }
    }

    private sealed class GuideVaultBadgeDiskCache
    {
        public DateTimeOffset GeneratedAt { get; set; }
        public List<string> MatchedGameIds { get; set; } = new();
        public List<string> MatchedGameKeys { get; set; } = new();
    }
}

internal sealed class GuideVaultBadgeCacheStatus
{
    public int MatchedGameIds { get; set; }
    public int MatchedGameKeys { get; set; }
    public DateTimeOffset LastSuccessfulRefresh { get; set; }
    public DateTimeOffset LastRefreshAttempt { get; set; }
}

internal sealed class GuideVaultBadgeLocalAudit
{
    public int LocalGames { get; set; }
    public int LocalMatchedGames { get; set; }
    public List<string> ExampleMatches { get; set; } = new();
}
