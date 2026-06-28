param(
  [string]$LaunchBoxRoot = "D:\HyperspinMasterbuild\LaunchBox",
  [string]$Configuration = "Release",
  [string]$Framework = "net9.0-windows",
  [switch]$SkipBuild,
  [switch]$IncludePdb
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
  throw "Built plugin DLL was not found: $PluginDll"
}
if (-not (Test-Path -LiteralPath $LauncherExe)) {
  throw "Built WebView2 launcher EXE was not found: $LauncherExe"
}

$DistDir = Join-Path $ProjectRoot "dist"
$StageDir = Join-Path $DistDir "_stage"
$PluginStage = Join-Path $StageDir "Plugins\GuideVault"
$LauncherStage = Join-Path $StageDir "ThirdParty\GuideVaultReaderLauncher"
$ReleaseZip = Join-Path $DistDir "GuideVault.LaunchBoxConnector-$Version-launchbox.zip"

Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $ReleaseZip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $PluginStage | Out-Null
New-Item -ItemType Directory -Force -Path $LauncherStage | Out-Null

Copy-Item -Path (Join-Path $PluginOutputDir "*") -Destination $PluginStage -Recurse -Force
Copy-Item -Path (Join-Path $LauncherOutputDir "*") -Destination $LauncherStage -Recurse -Force

if (-not $IncludePdb) {
  Get-ChildItem -Path $StageDir -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue | Remove-Item -Force
}

@"
GuideVault LaunchBox Connector $Version

Install:
1. Close LaunchBox and BigBox.
2. Extract this zip directly into your LaunchBox install folder.
3. Confirm these folders exist after extraction:
   - LaunchBox\Plugins\GuideVault
   - LaunchBox\ThirdParty\GuideVaultReaderLauncher
4. Start LaunchBox.
5. Open Tools > GuideVault Connector.

Requires GuideVault server 0.9.258 or newer.
GuideVault server 0.9.260 or newer is recommended.
"@ | Set-Content -LiteralPath (Join-Path $StageDir "README_INSTALL.txt") -Encoding UTF8

if (Test-Path -LiteralPath (Join-Path $ProjectRoot "RELEASE_NOTES_0.4.22.md")) {
  Copy-Item -LiteralPath (Join-Path $ProjectRoot "RELEASE_NOTES_0.4.22.md") -Destination (Join-Path $StageDir "RELEASE_NOTES_0.4.22.md") -Force
}

Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $ReleaseZip -Force
Remove-Item -LiteralPath $StageDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Created release package: $ReleaseZip"
