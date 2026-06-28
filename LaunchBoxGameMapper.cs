using System.Reflection;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

internal static class LaunchBoxGameMapper
{
    private static readonly object PlatformSummarySyncRoot = new();
    private static readonly TimeSpan PlatformSummaryCacheTtl = TimeSpan.FromSeconds(30);
    private static DateTimeOffset _platformSummaryGeneratedAt = DateTimeOffset.MinValue;
    private static List<LaunchBoxPlatformSummary> _cachedPlatformSummaries = new();

    public static List<LaunchBoxPlatformSummary> GetPlatformSummaries(bool forceRefresh = false)
    {
        lock (PlatformSummarySyncRoot)
        {
            if (!forceRefresh && _cachedPlatformSummaries.Count > 0 && DateTimeOffset.Now - _platformSummaryGeneratedAt < PlatformSummaryCacheTtl)
                return _cachedPlatformSummaries.Select(ClonePlatformSummary).ToList();
        }

        var games = PluginHelper.DataManager.GetAllGames() ?? Array.Empty<IGame>();
        var summaries = games
            .Select(game => Clean(game.Platform))
            .Where(platform => !string.IsNullOrWhiteSpace(platform))
            .GroupBy(platform => platform, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LaunchBoxPlatformSummary
            {
                Platform = group.Key,
                GameCount = group.Count()
            })
            .OrderBy(row => row.Platform, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (PlatformSummarySyncRoot)
        {
            _cachedPlatformSummaries = summaries.Select(ClonePlatformSummary).ToList();
            _platformSummaryGeneratedAt = DateTimeOffset.Now;
        }

        return summaries;
    }

    private static LaunchBoxPlatformSummary ClonePlatformSummary(LaunchBoxPlatformSummary summary) => new()
    {
        Platform = summary.Platform,
        GameCount = summary.GameCount
    };

    public static LaunchBoxSyncRequest BuildSyncRequest(IEnumerable<IGame> games, GuideVaultConnectorSettings settings, IReadOnlyList<string>? selectedPlatformsOverride = null, bool? limitToSelectedOverride = null, IReadOnlyList<string>? matchTypesOverride = null)
    {
        var source = games ?? Array.Empty<IGame>();
        var platformSource = selectedPlatformsOverride ?? settings.ManualSyncPlatforms ?? new List<string>();
        var selectedPlatforms = platformSource
            .Select(Clean)
            .Where(platform => !string.IsNullOrWhiteSpace(platform))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchTypes = (matchTypesOverride ?? Array.Empty<string>())
            .Select(NormalizeMatchType)
            .Where(matchType => !string.IsNullOrWhiteSpace(matchType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(matchType => MatchTypeSortKey(matchType))
            .ThenBy(matchType => matchType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mergeSelectedPlatforms = (limitToSelectedOverride ?? settings.LimitManualSyncToSelectedPlatforms) && selectedPlatforms.Count > 0;
        if (mergeSelectedPlatforms)
        {
            var selectedSet = selectedPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);
            source = source.Where(game => selectedSet.Contains(Clean(game.Platform)));
        }

        if (settings.MaxGamesToSync > 0)
            source = source.Take(settings.MaxGamesToSync);

        return new LaunchBoxSyncRequest
        {
            LaunchBoxRoot = ResolveLaunchBoxRoot(),
            SyncMode = mergeSelectedPlatforms ? "MergeSelectedPlatforms" : "FullReplace",
            SelectedPlatforms = mergeSelectedPlatforms ? selectedPlatforms : new List<string>(),
            MatchTypes = matchTypes,
            Games = source.Select(game => ToPayload(game, settings)).Where(g => !string.IsNullOrWhiteSpace(g.Id) && !string.IsNullOrWhiteSpace(g.Title)).ToList()
        };
    }

    public static LaunchBoxGamePayload ToPayload(IGame game, GuideVaultConnectorSettings settings)
    {
        var alternateNames = settings.IncludeAlternateNames
            ? SafeEnumerable(game.GetAllAlternateNames())
                .Select(name => ReadStringProperty(name, "Name", "Title", "Value"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        var customFields = settings.IncludeCustomFields
            ? SafeEnumerable(game.GetAllCustomFields())
                .Select(field => new
                {
                    Name = ReadStringProperty(field, "Name", "Key"),
                    Value = ReadStringProperty(field, "Value", "Text")
                })
                .Where(field => !string.IsNullOrWhiteSpace(field.Name))
                .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new LaunchBoxGamePayload
        {
            Id = Clean(game.Id),
            Title = Clean(game.Title),
            SortTitle = Clean(game.SortTitle),
            Platform = Clean(game.Platform),
            Region = Clean(game.Region),
            ReleaseYear = Clean(game.ReleaseYear),
            Developer = Clean(game.Developer),
            Publisher = Clean(game.Publisher),
            Series = Clean(game.Series),
            DatabaseId = Clean(game.LaunchBoxDbId),
            ApplicationPath = Clean(game.ApplicationPath),
            ManualPath = Clean(game.ManualPath),
            Source = Clean(game.Source),
            AlternateNames = alternateNames,
            CustomFields = customFields
        };
    }

    private static IEnumerable<object> SafeEnumerable(Array? values)
    {
        if (values is null) yield break;
        foreach (var value in values)
        {
            if (value is not null) yield return value;
        }
    }

    private static string ReadStringProperty(object target, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            var value = prop?.GetValue(target)?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    private static string NormalizeMatchType(string? value)
    {
        var raw = Clean(value);
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.ToLowerInvariant() switch
        {
            "manual" or "manuals" or "game manual" or "game manuals" => "Manual",
            "strategy" or "strategy guide" or "strategy guides" or "guide" or "guides" or "walkthrough" or "walkthroughs" => "Strategy Guide",
            "magazine" or "magazines" or "issue" or "issues" => "Magazine",
            _ when string.Equals(raw, "Strategy Guide", StringComparison.OrdinalIgnoreCase) => "Strategy Guide",
            _ when string.Equals(raw, "Manual", StringComparison.OrdinalIgnoreCase) => "Manual",
            _ when string.Equals(raw, "Magazine", StringComparison.OrdinalIgnoreCase) => "Magazine",
            _ => string.Empty
        };
    }

    private static int MatchTypeSortKey(string matchType) => matchType switch
    {
        "Manual" => 0,
        "Strategy Guide" => 1,
        "Magazine" => 2,
        _ => 99
    };

    private static string ResolveLaunchBoxRoot()
    {
        var entry = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        if (string.IsNullOrWhiteSpace(entry)) return string.Empty;
        var dir = Path.GetDirectoryName(entry) ?? string.Empty;
        if (dir.EndsWith("Core", StringComparison.OrdinalIgnoreCase)) return Directory.GetParent(dir)?.FullName ?? dir;
        return dir;
    }

    private static string Clean(object? value) => value?.ToString()?.Trim() ?? string.Empty;
}
