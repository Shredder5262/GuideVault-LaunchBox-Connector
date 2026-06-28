# GuideVault LaunchBox Connector 0.4.22

Release hardening pass for the first LaunchBox connector package.

## Added

- Status tab now shows:
  - plugin version
  - GuideVault server version
  - last plugin version that synced to the server
  - last sync time
  - last match time
- Strategy Guide and Magazine match-review popups now show clearer summary counts:
  - loaded title count
  - matched game connection count
  - low-confidence connection count
  - refresh timestamp
- Empty states now explain what to do next, such as running Sync All Guides or Sync All Magazines first.

## Changed

- Sync buttons are disabled while a GuideVault match job is active to reduce accidental stacked sync jobs.
- Sync output now includes magazine match counts alongside manual and strategy guide counts.
- Install script now reports the version from the project file instead of a stale hard-coded version.

## Requires

GuideVault server 0.9.258 or newer for the LaunchBox status/match-scope endpoint changes.
