# Release checklist

## Server prerequisite

- GuideVault server is running 0.9.258 or newer.
- GuideVault server 0.9.260 or newer is recommended.
- Confirm the server has LaunchBox scoped sync and relationship endpoints.

## Local plugin build

```powershell
cd "C:\Users\Andrew\Documents\VSCode\GuideVault-LaunchBox-Connector"
.\scripts\Build-Plugin.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox"
```

## Local install smoke test

```powershell
.\scripts\Install-Plugin.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox" -ForceCloseLaunchBox
```

Smoke-test these items inside LaunchBox:

- GuideVault connector opens.
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
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "D:\HyperspinMasterbuild\LaunchBox"
```

Upload this generated file to GitHub Releases:

```text
dist\GuideVault.LaunchBoxConnector-0.4.22-launchbox.zip
```
