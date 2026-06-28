# GuideVault LaunchBox Connector

LaunchBox plugin for connecting your LaunchBox game library to GuideVault manuals, strategy guides, and magazines.

Current version: **0.4.22**

## What it does

- Syncs LaunchBox games into GuideVault.
- Matches games to GuideVault manuals, strategy guides, and magazines.
- Adds scoped sync controls for manuals, guides, and magazines.
- Adds match review popups so you can inspect what GuideVault matched.
- Opens matched GuideVault items from LaunchBox.
- Uses an external WebView2 reader helper so the embedded reader does not load WebView2 directly inside the LaunchBox plugin folder.
- Adds optional GuideVault badge icons for matched games.

## Requirements

- Windows.
- LaunchBox or BigBox.
- GuideVault server **0.9.260 or newer** recommended.
- Microsoft Edge WebView2 Runtime. Most current Windows systems already have this installed.
- .NET 9 SDK only if you are building from source.

## Install from a release zip

Download the release zip named similar to:

```text
GuideVault-LaunchBox-Connector-0.4.22-LaunchBoxRoot.zip
```

Close LaunchBox and BigBox, then extract the contents of the zip directly into your LaunchBox installation folder. This is the folder that contains `LaunchBox.exe`.

After extraction, the folder layout should look like this:

```text
LaunchBox\
  Plugins\
    GuideVault\
      GuideVault.LaunchBoxConnector.dll
      settings.json
  ThirdParty\
    GuideVaultReaderLauncher\
      GuideVaultReaderLauncher.exe
  Images\
    Media Packs\
      Overrides\
        Badges\
          GuideVault.MatchedItems.png
```

Start LaunchBox, then open:

```text
Tools > GuideVault Connector
```

Set your GuideVault server URL, for example:

```text
http://localhost:5478
```

Use the sync buttons from the connector window to sync manuals, strategy guides, and magazines.

## Build from source

The source build needs the LaunchBox plugin API DLL from an installed LaunchBox copy. Do not commit or redistribute the LaunchBox API DLL unless its license permits it.

Install the .NET 9 SDK, then run from the repository root:

```powershell
.\scripts\Build-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

Example LaunchBox roots might be:

```text
C:\LaunchBox
D:\LaunchBox
C:\Users\<you>\LaunchBox
```

## Install locally after building

```powershell
.\scripts\Install-Plugin.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox" -ForceCloseLaunchBox
```

The install script deploys files into:

```text
LaunchBox\Plugins\GuideVault
LaunchBox\ThirdParty\GuideVaultReaderLauncher
LaunchBox\Images\Media Packs\Overrides\Badges
```

## Create a drag-and-drop LaunchBox release package

Run:

```powershell
.\scripts\Package-PluginRelease.ps1 -LaunchBoxRoot "C:\Path\To\LaunchBox"
```

The generated zip is written to `dist` and is structured like a LaunchBox root folder:

```text
Plugins\GuideVault\...
ThirdParty\GuideVaultReaderLauncher\...
Images\Media Packs\Overrides\Badges\...
README_INSTALL.txt
RELEASE_NOTES_0.4.22.md
```

Users can extract that zip directly into their LaunchBox installation folder.

## Server compatibility

GuideVault server **0.9.260+** is recommended.

The connector expects these GuideVault server features:

- LaunchBox sync endpoint with optional match-type scoping.
- LaunchBox relationship review endpoints.
- LaunchBox status endpoint with server/plugin sync status.

## Release notes

See:

- `CHANGELOG.md`
- `RELEASE_NOTES_0.4.22.md`
- `docs/RELEASE_CHECKLIST.md`
