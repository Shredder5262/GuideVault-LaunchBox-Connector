namespace GuideVault.LaunchBoxConnector;

internal sealed class GuideVaultConnectorSettings
{
    public string GuideVaultUrl { get; set; } = "http://localhost:5478";
    public bool OpenInEmbeddedWindow { get; set; } = true;
    public bool OpenInDefaultBrowser { get; set; } = false;
    public bool UseBrowserLoginBridge { get; set; } = true;
    public bool OpenReaderMaximized { get; set; } = false;
    public bool OpenReaderFullscreen { get; set; } = false;
    public string GuideVaultUsername { get; set; } = string.Empty;
    public string GuideVaultEmail { get; set; } = string.Empty;
    public string GuideVaultPassword { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
    public bool IncludeCustomFields { get; set; } = false;
    public bool IncludeAlternateNames { get; set; } = true;
    public int MaxGamesToSync { get; set; } = 0;
    public bool LimitManualSyncToSelectedPlatforms { get; set; } = false;
    public List<string> ManualSyncPlatforms { get; set; } = new();
    public bool LimitStrategyGuideSyncToSelectedPlatforms { get; set; } = false;
    public List<string> StrategyGuideSyncPlatforms { get; set; } = new();
    public bool LimitMagazineSyncToSelectedPlatforms { get; set; } = false;
    public List<string> MagazineSyncPlatforms { get; set; } = new();
    public bool ForceGuideVaultBadgeOnAllGames { get; set; } = false;

    public bool HasBrowserLoginProfile =>
        (!string.IsNullOrWhiteSpace(GuideVaultUsername) || !string.IsNullOrWhiteSpace(GuideVaultEmail))
        && !string.IsNullOrWhiteSpace(GuideVaultPassword);
}

internal sealed class LaunchBoxPlatformSummary
{
    public string Platform { get; set; } = string.Empty;
    public int GameCount { get; set; }
}

internal sealed class LaunchBoxSyncRequest
{
    public string Source { get; set; } = "LaunchBox";
    public string PluginVersion { get; set; } = ConnectorConstants.PluginVersion;
    public string LaunchBoxRoot { get; set; } = string.Empty;
    public string SyncMode { get; set; } = "FullReplace";
    public List<string> SelectedPlatforms { get; set; } = new();
    public List<string> MatchTypes { get; set; } = new();
    public List<LaunchBoxGamePayload> Games { get; set; } = new();
}

internal sealed class LaunchBoxGamePayload
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SortTitle { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ReleaseYear { get; set; } = string.Empty;
    public string Developer { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public string DatabaseId { get; set; } = string.Empty;
    public string ApplicationPath { get; set; } = string.Empty;
    public string ManualPath { get; set; } = string.Empty;
    public string Source { get; set; } = "LaunchBox";
    public List<string> AlternateNames { get; set; } = new();
    public Dictionary<string, string> CustomFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class GuideVaultOpenRequest
{
    public string LaunchBoxGameId { get; set; } = string.Empty;
    public string MatchType { get; set; } = "Manual";
    public bool BroadcastOpenSignal { get; set; } = false;
    public bool SuppressOpenSignal { get; set; } = true;
    public string GuideVaultItemId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
}

internal sealed class GuideVaultOpenResult
{
    public bool Found { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string AbsoluteReaderUrl { get; set; } = string.Empty;
    public string AbsoluteDetailsUrl { get; set; } = string.Empty;
    public string ReaderUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}


internal sealed class GuideVaultGameMatchDetailsResult
{
    public LaunchBoxGamePayload? Game { get; set; }
    public List<GuideVaultGameMatchView> Matches { get; set; } = new();
    public List<GuideVaultGameMatchView> Candidates { get; set; } = new();
}

internal sealed class GuideVaultGameMatchView
{
    public string Id { get; set; } = string.Empty;
    public string LaunchBoxGameId { get; set; } = string.Empty;
    public string GuideVaultItemId { get; set; } = string.Empty;
    public string GuideVaultItemTitle { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string ReaderUrl { get; set; } = string.Empty;
    public string DetailsUrl { get; set; } = string.Empty;
}

internal sealed class GuideVaultBrowserLoginLinkRequest
{
    public string TargetUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal sealed class GuideVaultBrowserLoginLinkResult
{
    public bool Success { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

internal sealed class GuideVaultCoverageResult
{
    public string Source { get; set; } = "LaunchBox";
    public DateTimeOffset LastSyncedAt { get; set; }
    public int TotalLaunchBoxGames { get; set; }
    public int ManualMatchedGames { get; set; }
    public int StrategyGuideMatchedGames { get; set; }
    public int MagazineMatchedGames { get; set; }
    public int AnyMatchedGames { get; set; }
    public int MissingGames { get; set; }
    public int AmbiguousMatches { get; set; }
    public double CoveragePercent { get; set; }
    public double ManualCoveragePercent { get; set; }
    public double StrategyGuideCoveragePercent { get; set; }
    public double MagazineCoveragePercent { get; set; }
    public List<GuideVaultPlatformCoverage> ByPlatform { get; set; } = new();
}

internal sealed class GuideVaultBadgeMapResult
{
    public DateTimeOffset GeneratedAt { get; set; }
    public int TotalGames { get; set; }
    public int MatchedGames { get; set; }
    public List<GuideVaultBadgeGameResult> Games { get; set; } = new();
}

internal sealed class GuideVaultBadgeGameResult
{
    public string LaunchBoxGameId { get; set; } = string.Empty;
    public string LaunchBoxGameTitle { get; set; } = string.Empty;
    public string LaunchBoxPlatform { get; set; } = string.Empty;
    public int ManualMatches { get; set; }
    public int StrategyGuideMatches { get; set; }
    public int MagazineMatches { get; set; }
    public int TotalMatches { get; set; }
}

internal sealed class GuideVaultPlatformCoverage
{
    public string Platform { get; set; } = string.Empty;
    public int GameCount { get; set; }
    public int ManualMatchedGames { get; set; }
    public int StrategyGuideMatchedGames { get; set; }
    public int MagazineMatchedGames { get; set; }
    public int AnyMatchedGames { get; set; }
    public int MissingGames { get; set; }
    public double CoveragePercent { get; set; }
    public double MagazineCoveragePercent { get; set; }
}

internal sealed class GuideVaultSyncResult
{
    public bool Success { get; set; }
    public string Source { get; set; } = "LaunchBox";
    public DateTimeOffset LastSyncedAt { get; set; }
    public int TotalGames { get; set; }
    public int MatchedGames { get; set; }
    public int ManualMatchedGames { get; set; }
    public int StrategyGuideMatchedGames { get; set; }
    public int MagazineMatchedGames { get; set; }
    public int MissingGames { get; set; }
    public int AmbiguousMatches { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string JobStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public GuideVaultSyncJobStatus? Job { get; set; }
}

internal sealed class GuideVaultStatusResult
{
    public bool Enabled { get; set; }
    public string Version { get; set; } = string.Empty;
    public bool Configured { get; set; }
    public int GameCount { get; set; }
    public int MatchCount { get; set; }
    public string Source { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? LastMatchedAt { get; set; }
    public GuideVaultSyncJobStatus? SyncJob { get; set; }
}

internal sealed class GuideVaultSyncJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Idle";
    public string Message { get; set; } = string.Empty;
    public int TotalGames { get; set; }
    public int ImportedGames { get; set; }
    public int ProcessedGames { get; set; }
    public int MatchedGames { get; set; }
    public int ManualMatchedGames { get; set; }
    public int StrategyGuideMatchedGames { get; set; }
    public int MagazineMatchedGames { get; set; }
    public int AmbiguousMatches { get; set; }
    public int MissingGames { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}


internal sealed class GuideVaultDocumentRelationshipResult
{
    public string MatchType { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public int TotalItems { get; set; }
    public int TotalConnections { get; set; }
    public List<GuideVaultDocumentRelationshipItem> Items { get; set; } = new();
}

internal sealed class GuideVaultDocumentRelationshipItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public int TotalConnections { get; set; }
    public List<GuideVaultDocumentRelationshipConnection> Connections { get; set; } = new();
}

internal sealed class GuideVaultDocumentRelationshipConnection
{
    public string LaunchBoxGameId { get; set; } = string.Empty;
    public string LaunchBoxGameTitle { get; set; } = string.Empty;
    public string LaunchBoxPlatform { get; set; } = string.Empty;
    public string LaunchBoxRegion { get; set; } = string.Empty;
    public string LaunchBoxReleaseYear { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

internal static class ConnectorConstants
{
    public const string PluginVersion = "0.4.22";
    public const string SettingsFolderName = "GuideVault";
    public const string SettingsSubFolderName = "LaunchBoxConnector";
}
