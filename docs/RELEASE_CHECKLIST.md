# Release checklist

## Server prerequisite

- GuideVault server is running **0.9.260** or newer.
- Confirm the server has LaunchBox scoped sync and relationship endpoints.

## Local plugin build

From the repository root:

```powershell
.\scripts\Build-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

## Local install smoke test

```powershell
.\scripts\Install-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox" -ForceCloseLaunchBox
```

Smoke-test these items inside LaunchBox:

- GuideVault connector opens from the Tools menu.
- Status tab shows plugin and server version.
- Sync Library works.
- Sync Selected Manuals works.
- Sync All Manuals works.
- Sync Selected Guides works.
- Sync All Guides works.
- Sync Selected Magazines works.
- Sync All Magazines works.
- View Matched Manuals opens.
- View Matched Guides opens.
- View Matched Magazines opens.
- Open Manual works from a matched game.
- Open Strategy Guide works from a matched game.
- Open Magazine works from a matched game.
- Embedded WebView2 window opens.
- Fullscreen opens correctly.
- Escape closes fullscreen/window as expected.
- Cursor does not constantly flip into a loading state while idle.

## Package

```powershell
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

The package script creates a LaunchBox-root-style zip under `dist`, for example:

```text
dist\GuideVault-LaunchBox-Connector-0.4.22-LaunchBoxRoot.zip
```

Upload that generated file to GitHub Releases.

## Package structure check

Open the zip and confirm the top level contains:

```text
Plugins\GuideVault\
ThirdParty\GuideVaultReaderLauncher\
Images\Media Packs\Overrides\Badges\
README_INSTALL.txt
RELEASE_NOTES_0.4.22.md
```

The zip should not contain a personal machine path or a parent folder such as `C:\Users\...`.
