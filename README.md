# GuideVault LaunchBox Connector

LaunchBox plugin for GuideVault. It syncs LaunchBox games with GuideVault manuals, strategy guides, and magazines, then lets you open matched documents directly from LaunchBox.

Current version: **0.4.22**

## Requires

- LaunchBox installed on Windows.
- .NET 9 SDK for building from source.
- GuideVault server **0.9.258 or newer**.
- GuideVault server **0.9.260 or newer recommended**.

Needed GuideVault server endpoints:

- `POST /api/integrations/launchbox/sync` with optional `matchTypes`.
- `GET /api/integrations/launchbox/relationships?matchType=Strategy%20Guide`.
- `GET /api/integrations/launchbox/relationships?matchType=Magazine`.
- `GET /api/integrations/launchbox/status` with plugin/server sync status.

## Features

- LaunchBox library sync into GuideVault.
- Manual, strategy guide, and magazine relationship matching.
- Scoped sync controls:
  - Sync selected/all manuals.
  - Sync selected/all strategy guides.
  - Sync selected/all magazines.
- Match review popups for manuals, strategy guides, and magazines.
- Embedded GuideVault reader window through an external WebView2 helper.
- Fullscreen reader option.
- LaunchBox badge support for matched GuideVault items.
- Status tab with plugin/server/last-sync details.

## Build

Run from the repository root on the Windows machine that has LaunchBox installed:

```powershell
.\scripts\Build-Plugin.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox"
```

Use your actual LaunchBox folder if different.

## Install locally

```powershell
.\scripts\Install-Plugin.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox" -ForceCloseLaunchBox
```

The install script deploys:

```text
LaunchBox\Plugins\GuideVault
LaunchBox\ThirdParty\GuideVaultReaderLauncher
```

## Package a release zip

```powershell
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox"
```

The generated zip is written to:

```text
dist\GuideVault.LaunchBoxConnector-0.4.22-launchbox.zip
```

That zip is meant to be attached to the GitHub release.

## Release notes

See:

- `CHANGELOG.md`
- `RELEASE_NOTES_0.4.22.md`
- `docs/RELEASE_CHECKLIST.md`

## Repository setup

See `docs/GITHUB_SETUP.md`.
