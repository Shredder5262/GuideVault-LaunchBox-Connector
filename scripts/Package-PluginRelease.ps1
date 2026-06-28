param(
  [string]$LaunchBoxRoot = "",
  [string]$Configuration = "Release",
  [string]$Framework = "net9.0-windows",
  [switch]$SkipBuild,
  [switch]$IncludePdb,
  [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot "GuideVault.LaunchBoxConnector.csproj"

if (-not (Test-Path -LiteralPath $Project)) {
  throw "Could not find project file: $Project"
}

$projectText = Get-Content -LiteralPath $Project -Raw
$versionMatch = [regex]::Match($projectText, '<Version>(.*?)</Version>')
$Version = if ($versionMatch.Success) { $versionMatch.Groups[1].Value.Trim() } else { "0.0.0" }

if (-not $SkipBuild) {
  & (Join-Path $PSScriptRoot "Build-Plugin.ps1") -LaunchBoxRoot $LaunchBoxRoot -Configuration $Configuration -Framework $Framework
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$PluginOutputDir = Join-Path $ProjectRoot "bin\$Configuration\$Framework"
$LauncherOutputDir = Join-Path $ProjectRoot "Launcher\GuideVaultReaderLauncher\bin\$Configuration\$Framework"
$PluginDll = Join-Path $PluginOutputDir "GuideVault.LaunchBoxConnector.dll"
$LauncherExe = Join-Path $LauncherOutputDir "GuideVaultReaderLauncher.exe"

if (-not (Test-Path -LiteralPath $PluginDll)) {
  throw "Built plugin DLL was not found: $PluginDll. Run .\scripts\Build-Plugin.ps1 first, or rerun this script without -SkipBuild."
}
if (-not (Test-Path -LiteralPath $LauncherExe)) {
  throw "Built WebView2 launcher EXE was not found: $LauncherExe. Run .\scripts\Build-Plugin.ps1 first, or rerun this script without -SkipBuild."
}

$DistDir = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $ProjectRoot "dist" } else { $OutputDirectory }
$StageDir = Join-Path $DistDir "_stage_launchbox_root"
$PluginStage = Join-Path $StageDir "Plugins\GuideVault"
$LauncherStage = Join-Path $StageDir "ThirdParty\GuideVaultReaderLauncher"
$BadgeOverrideStage = Join-Path $StageDir "Images\Media Packs\Overrides\Badges"
$BadgePackStage = Join-Path $StageDir "Images\Media Packs\Badges\Nostalgic Platform Badges"
$BadgeLegacyStage = Join-Path $StageDir "Images\Badges"
$ReleaseZip = Join-Path $DistDir "GuideVault-LaunchBox-Connector-$Version-LaunchBoxRoot.zip"

Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $ReleaseZip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $PluginStage | Out-Null
New-Item -ItemType Directory -Force -Path $LauncherStage | Out-Null
New-Item -ItemType Directory -Force -Path $BadgeOverrideStage | Out-Null
New-Item -ItemType Directory -Force -Path $BadgePackStage | Out-Null
New-Item -ItemType Directory -Force -Path $BadgeLegacyStage | Out-Null

Copy-Item -Path (Join-Path $PluginOutputDir "*") -Destination $PluginStage -Recurse -Force
Copy-Item -Path (Join-Path $LauncherOutputDir "*") -Destination $LauncherStage -Recurse -Force

# Do not include a default settings.json in the release zip. Existing users may already have one.
# The connector creates Plugins\GuideVault\settings.json on first launch if it is missing.
Remove-Item -LiteralPath (Join-Path $PluginStage "settings.json") -Force -ErrorAction SilentlyContinue

if (-not $IncludePdb) {
  Get-ChildItem -Path $StageDir -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}

$sourceBadge = Join-Path $ProjectRoot "Assets\GuideVault.MatchedItems.png"
if (Test-Path -LiteralPath $sourceBadge) {
  $badgeNames = @(
    "GuideVault.MatchedItems.png",
    "GuideVaultMatchedItems.png",
    "GuideVault Matched Item.png"
  )
  foreach ($targetDir in @($BadgeOverrideStage, $BadgePackStage, $BadgeLegacyStage)) {
    foreach ($name in $badgeNames) {
      Copy-Item -LiteralPath $sourceBadge -Destination (Join-Path $targetDir $name) -Force
    }
  }
}
else {
  Write-Warning "Badge image not found: $sourceBadge"
}

@"
GuideVault LaunchBox Connector $Version

Install:
1. Close LaunchBox and BigBox.
2. Extract this zip directly into your LaunchBox installation folder, the folder that contains LaunchBox.exe.
3. Confirm these folders exist after extraction:
   - LaunchBox\Plugins\GuideVault
   - LaunchBox\ThirdParty\GuideVaultReaderLauncher
   - LaunchBox\Images\Media Packs\Overrides\Badges
4. Start LaunchBox.
5. Open Tools > GuideVault Connector.
6. Set your GuideVault server URL in the connector settings.

Update:
- Close LaunchBox and BigBox first.
- Extract this zip into the LaunchBox folder and allow files to overwrite.
- This package does not include settings.json, so existing connector settings should not be overwritten.

Requires:
- LaunchBox or BigBox on Windows.
- GuideVault server 0.9.260 or newer is recommended.
- Microsoft Edge WebView2 Runtime.
"@ | Set-Content -LiteralPath (Join-Path $StageDir "README_INSTALL.txt") -Encoding UTF8

$releaseNotes = Join-Path $ProjectRoot "RELEASE_NOTES_0.4.22.md"
if (Test-Path -LiteralPath $releaseNotes) {
  Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $StageDir "RELEASE_NOTES_0.4.22.md") -Force
}

Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $ReleaseZip -Force
Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Created LaunchBox-root release package: $ReleaseZip"
Write-Host "Users should extract the zip directly into the folder that contains LaunchBox.exe."
