param(
  [string]$LaunchBoxRoot = "",
  [string]$Configuration = "Release",
  [string]$Framework = "net9.0-windows",
  [string]$PluginFolderName = "GuideVault",
  [string]$LauncherFolderName = "GuideVaultReaderLauncher",
  [switch]$ForceCloseLaunchBox
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Test-LaunchBoxRoot([string]$path) {
  if ([string]::IsNullOrWhiteSpace($path)) { return $false }
  if (-not (Test-Path -LiteralPath $path)) { return $false }

  $coreDll = Join-Path $path "Core\Unbroken.LaunchBox.Plugins.dll"
  $metadataDll = Join-Path $path "Metadata\Unbroken.LaunchBox.Plugins.dll"
  $launchBoxExe = Join-Path $path "LaunchBox.exe"
  return (Test-Path -LiteralPath $coreDll) -or (Test-Path -LiteralPath $metadataDll) -or (Test-Path -LiteralPath $launchBoxExe)
}

function Resolve-LaunchBoxRoot([string]$candidate) {
  if (Test-LaunchBoxRoot $candidate) {
    return [System.IO.Path]::GetFullPath($candidate)
  }

  $candidates = @()
  if ($env:LAUNCHBOX_ROOT) { $candidates += $env:LAUNCHBOX_ROOT }
  if ($env:LaunchBoxRoot) { $candidates += $env:LaunchBoxRoot }
  $candidates += @(
    "C:\LaunchBox",
    "D:\LaunchBox",
    "E:\LaunchBox",
    (Join-Path $env:USERPROFILE "LaunchBox"),
    (Join-Path $env:ProgramFiles "LaunchBox"),
    (Join-Path ${env:ProgramFiles(x86)} "LaunchBox")
  ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  foreach ($path in $candidates | Select-Object -Unique) {
    if (Test-LaunchBoxRoot $path) {
      return [System.IO.Path]::GetFullPath($path)
    }
  }

  throw "Could not find your LaunchBox installation. Rerun with: .\scripts\Install-Plugin.ps1 -LaunchBoxRoot 'C:\Path\To\LaunchBox'"
}

$LaunchBoxRoot = Resolve-LaunchBoxRoot $LaunchBoxRoot

$OutputDir = Join-Path $ProjectRoot "bin\$Configuration\$Framework"
$LauncherOutputDir = Join-Path $ProjectRoot "Launcher\GuideVaultReaderLauncher\bin\$Configuration\$Framework"
$Dll = Join-Path $OutputDir "GuideVault.LaunchBoxConnector.dll"
$LauncherExe = Join-Path $LauncherOutputDir "GuideVaultReaderLauncher.exe"
$PluginsRoot = Join-Path $LaunchBoxRoot "Plugins"
$PluginDir = Join-Path $PluginsRoot $PluginFolderName
$ThirdPartyRoot = Join-Path $LaunchBoxRoot "ThirdParty"
$LauncherDeployDir = Join-Path $ThirdPartyRoot $LauncherFolderName

function Stop-LaunchBoxProcessesIfRequested {
  $running = Get-Process -Name "LaunchBox","BigBox","GuideVaultReaderLauncher" -ErrorAction SilentlyContinue
  if (-not $running) { return }

  if ($ForceCloseLaunchBox) {
    Write-Host "Stopping LaunchBox/BigBox/GuideVaultReaderLauncher before plugin install..."
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
    return
  }

  $names = ($running | Select-Object -ExpandProperty ProcessName -Unique) -join ", "
  throw "Plugin files are locked because $names is still running. Close LaunchBox/BigBox and any GuideVault reader windows, then run this install script again, or rerun with -ForceCloseLaunchBox."
}

function Remove-PathSafely([string]$path) {
  if (-not (Test-Path -LiteralPath $path)) { return }
  try {
    Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
    Write-Host "Removed stale path: $path"
  }
  catch {
    throw "Unable to remove path '$path'. It is probably locked. Close LaunchBox/BigBox/GuideVault reader windows, then rerun with -ForceCloseLaunchBox. Original error: $($_.Exception.Message)"
  }
}

function Remove-PluginFilesSafely([string]$directory, [string]$filter) {
  if (-not (Test-Path -LiteralPath $directory)) { return }
  $files = Get-ChildItem -Path $directory -Filter $filter -ErrorAction SilentlyContinue
  foreach ($file in $files) {
    try {
      Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
      Write-Host "Removed stale plugin file: $($file.FullName)"
    }
    catch {
      throw "Unable to remove existing plugin file '$($file.FullName)'. It is probably locked. Close LaunchBox/BigBox and retry. Original error: $($_.Exception.Message)"
    }
  }
}

function Remove-StaleGuideVaultBinariesOutsideCanonicalFolder([string]$pluginsRoot, [string]$canonicalDir) {
  if (-not (Test-Path -LiteralPath $pluginsRoot)) { return }
  $canonicalFull = [System.IO.Path]::GetFullPath($canonicalDir).TrimEnd('\\')
  $staleFiles = Get-ChildItem -Path $pluginsRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
      $_.Name -like "GuideVault.LaunchBoxConnector*.dll" -or
      $_.Name -like "GuideVault.LaunchBoxConnector*.pdb"
    } |
    Where-Object {
      -not ([System.IO.Path]::GetFullPath($_.DirectoryName).TrimEnd('\\').Equals($canonicalFull, [System.StringComparison]::OrdinalIgnoreCase))
    }

  foreach ($file in $staleFiles) {
    try {
      Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
      Write-Host "Removed stale GuideVault binary outside canonical folder: $($file.FullName)"
    }
    catch {
      throw "Unable to remove stale GuideVault plugin binary '$($file.FullName)'. Close LaunchBox/BigBox and retry with -ForceCloseLaunchBox. Original error: $($_.Exception.Message)"
    }
  }
}

function Install-GuideVaultBadgeImages([string]$launchBoxRoot, [string]$projectRoot) {
  $sourceBadge = Join-Path $projectRoot "Assets\GuideVault.MatchedItems.png"
  if (-not (Test-Path -LiteralPath $sourceBadge)) {
    Write-Warning "GuideVault badge image not found in source package: $sourceBadge"
    return
  }

  # LaunchBox 13.21+ can fail to extract embedded custom badge icons from plugin DLLs.
  # Install the same badge image into the current Media Pack locations so LaunchBox can render it.
  $badgeDirs = @(
    (Join-Path $launchBoxRoot "Images\Media Packs\Overrides\Badges"),
    (Join-Path $launchBoxRoot "Images\Media Packs\Badges\Nostalgic Platform Badges"),
    (Join-Path $launchBoxRoot "Images\Badges")
  )

  $badgeNames = @(
    "GuideVault.MatchedItems.png",
    "GuideVaultMatchedItems.png",
    "GuideVault Matched Item.png"
  )

  foreach ($dir in $badgeDirs) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    foreach ($name in $badgeNames) {
      Copy-Item -Path $sourceBadge -Destination (Join-Path $dir $name) -Force
    }
    Write-Host "Installed GuideVault badge icon to: $dir"
  }
}

if (-not (Test-Path $Dll)) {
  throw "Built plugin DLL was not found: $Dll. Run .\scripts\Build-Plugin.ps1 first."
}

if (-not (Test-Path $LauncherExe)) {
  throw "Built WebView2 launcher EXE was not found: $LauncherExe. Run .\scripts\Build-Plugin.ps1 first."
}

Stop-LaunchBoxProcessesIfRequested
New-Item -ItemType Directory -Force -Path $PluginsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ThirdPartyRoot | Out-Null

# Remove older test/development layouts that caused duplicate right-click and Tools menu entries.
$stalePaths = @(
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVaultLaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVault Connector"),
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector.Clean"),
  (Join-Path $PluginsRoot "GuideVault LaunchBox Connector"),
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector.Old"),
  (Join-Path $PluginsRoot "Guidevault-launchbox-plugin")
)
foreach ($path in $stalePaths) { Remove-PathSafely $path }

# Remove loose copies that may have been copied directly into LaunchBox\Plugins during early tests.
Remove-PluginFilesSafely -directory $PluginsRoot -filter "GuideVault.LaunchBoxConnector*.dll"
Remove-PluginFilesSafely -directory $PluginsRoot -filter "GuideVault.LaunchBoxConnector*.pdb"
Remove-StaleGuideVaultBinariesOutsideCanonicalFolder -pluginsRoot $PluginsRoot -canonicalDir $PluginDir

# Keep the canonical plugin folder, but delete stale connector binaries and stale WebView2 dependencies inside it.
New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
Remove-PluginFilesSafely -directory $PluginDir -filter "GuideVault.LaunchBoxConnector*.dll"
Remove-PluginFilesSafely -directory $PluginDir -filter "GuideVault.LaunchBoxConnector*.pdb"
Remove-PluginFilesSafely -directory $PluginDir -filter "Microsoft.Web.WebView2*.dll"
Remove-PluginFilesSafely -directory $PluginDir -filter "WebView2Loader.dll"
Remove-PathSafely (Join-Path $PluginDir "runtimes")
# Critical: WebView2Profile cannot live under LaunchBox\Plugins. LaunchBox scans
# plugin folders recursively and will try to load native DLLs created inside EBWebView.
Remove-PathSafely (Join-Path $PluginDir "WebView2Profile")

# Copy the plugin output. WebView2 is intentionally not loaded by this plugin DLL anymore.
Copy-Item -Path (Join-Path $OutputDir "*") -Destination $PluginDir -Recurse -Force
Install-GuideVaultBadgeImages -launchBoxRoot $LaunchBoxRoot -projectRoot $ProjectRoot

# Deploy the WebView2 reader helper outside the LaunchBox plugin load context, matching the Kavita reader pattern.
Remove-PathSafely $LauncherDeployDir
New-Item -ItemType Directory -Force -Path $LauncherDeployDir | Out-Null
Copy-Item -Path (Join-Path $LauncherOutputDir "*") -Destination $LauncherDeployDir -Recurse -Force

$TargetDll = Join-Path $PluginDir (Split-Path -Leaf $Dll)
$Pdb = [System.IO.Path]::ChangeExtension($Dll, ".pdb")
$TargetPdb = Join-Path $PluginDir (Split-Path -Leaf $Pdb)
$TargetLauncherExe = Join-Path $LauncherDeployDir (Split-Path -Leaf $LauncherExe)

$SettingsPath = Join-Path $PluginDir "settings.json"
if (-not (Test-Path $SettingsPath)) {
@'
{
  "guideVaultUrl": "http://localhost:5478",
  "openInEmbeddedWindow": true,
  "openInDefaultBrowser": false,
  "useBrowserLoginBridge": true,
  "guideVaultUsername": "",
  "guideVaultEmail": "",
  "guideVaultPassword": "",
  "timeoutSeconds": 300,
  "includeCustomFields": false,
  "includeAlternateNames": true,
  "maxGamesToSync": 0
}
'@ | Set-Content -Path $SettingsPath -Encoding UTF8
}
else {
  try {
    $settingsJson = Get-Content -Raw -Path $SettingsPath | ConvertFrom-Json
    if (-not ($settingsJson.PSObject.Properties.Name -contains "openInEmbeddedWindow")) { $settingsJson | Add-Member -NotePropertyName openInEmbeddedWindow -NotePropertyValue $true }
    $settingsJson.openInEmbeddedWindow = $true
    if (-not ($settingsJson.PSObject.Properties.Name -contains "openInDefaultBrowser")) { $settingsJson | Add-Member -NotePropertyName openInDefaultBrowser -NotePropertyValue $false }
    $settingsJson.openInDefaultBrowser = $false
    if (-not ($settingsJson.PSObject.Properties.Name -contains "useBrowserLoginBridge")) { $settingsJson | Add-Member -NotePropertyName useBrowserLoginBridge -NotePropertyValue $true }
    $settingsJson.useBrowserLoginBridge = $true
    if (-not ($settingsJson.PSObject.Properties.Name -contains "guideVaultUsername")) { $settingsJson | Add-Member -NotePropertyName guideVaultUsername -NotePropertyValue "" }
    if (-not ($settingsJson.PSObject.Properties.Name -contains "guideVaultEmail")) { $settingsJson | Add-Member -NotePropertyName guideVaultEmail -NotePropertyValue "" }
    if (-not ($settingsJson.PSObject.Properties.Name -contains "guideVaultPassword")) { $settingsJson | Add-Member -NotePropertyName guideVaultPassword -NotePropertyValue "" }
    $settingsJson | ConvertTo-Json -Depth 10 | Set-Content -Path $SettingsPath -Encoding UTF8
  }
  catch {
    Write-Warning "Could not normalize $SettingsPath. You can recreate it from the plugin Settings tab."
  }
}

try {
  if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
    Get-ChildItem -Path $PluginDir -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Get-ChildItem -Path $LauncherDeployDir -Recurse -File -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
    Unblock-File -Path $TargetDll -ErrorAction SilentlyContinue
    Unblock-File -Path $TargetLauncherExe -ErrorAction SilentlyContinue
    if (Test-Path $TargetPdb) { Unblock-File -Path $TargetPdb -ErrorAction SilentlyContinue }
  }
} catch {
  Write-Warning "Unable to run Unblock-File automatically. If LaunchBox blocks the plugin, run: Unblock-File -Path '$TargetDll' and Unblock-File -Path '$TargetLauncherExe'"
}

$versionMatch = Select-String -Path (Join-Path $ProjectRoot "GuideVault.LaunchBoxConnector.csproj") -Pattern "<Version>(.*?)</Version>" | Select-Object -First 1
$pluginVersion = if ($versionMatch -and $versionMatch.Matches.Count -gt 0) { $versionMatch.Matches[0].Groups[1].Value } else { "unknown" }
Write-Host "Installed GuideVault LaunchBox Connector $pluginVersion to: $PluginDir"
Write-Host "Installed GuideVault WebView2 launcher to: $LauncherDeployDir"
Write-Host "WebView2 profile path: $(Join-Path $LauncherDeployDir 'WebView2Profile')"
Write-Host "Cleaned stale GuideVault connector plugin folders/files under: $PluginsRoot"
Write-Host "Restart LaunchBox before testing."
