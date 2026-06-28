# Changelog

## 0.4.22

Release hardening for the first packaged GuideVault LaunchBox Connector build.

- Added clearer plugin/server/last-sync status visibility.
- Added scoped sync controls for manuals, strategy guides, and magazines.
- Added selected/all sync support for strategy guides and magazines.
- Added match review popups for strategy guide and magazine relationships.
- Improved match review counts, low-confidence visibility, refresh time, and empty states.
- Reduced background polling and UI wait-cursor churn.
- Disabled sync controls while a sync/match job is active.
- Removed stale/obsolete selected-game guide actions from coverage tabs.
- Uses the external GuideVaultReaderLauncher helper for embedded WebView2 behavior.

Requires GuideVault server 0.9.258 or newer. Server 0.9.260 or newer is recommended.
