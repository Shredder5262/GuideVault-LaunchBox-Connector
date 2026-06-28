param(
  [string]$LaunchBoxRoot = "",
  [switch]$ForceCloseLaunchBox
)

$ErrorActionPreference = "Stop"

function Test-LaunchBoxRoot([string]$path) {
  if ([string]::IsNullOrWhiteSpace($path)) { return $false }
  if (-not (Test-Path -LiteralPath $path)) { return $false }
  return (Test-Path -LiteralPath (Join-Path $path "LaunchBox.exe")) -or
    (Test-Path -LiteralPath (Join-Path $path "Core\Unbroken.LaunchBox.Plugins.dll")) -or
    (Test-Path -LiteralPath (Join-Path $path "Metadata\Unbroken.LaunchBox.Plugins.dll"))
}

function Resolve-LaunchBoxRoot([string]$candidate) {
  if (Test-LaunchBoxRoot $candidate) { return [System.IO.Path]::GetFullPath($candidate) }

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
    if (Test-LaunchBoxRoot $path) { return [System.IO.Path]::GetFullPath($path) }
  }

  throw "Could not find your LaunchBox installation. Rerun with: .\scripts\Clean-Old-GuideVault-Plugins.ps1 -LaunchBoxRoot 'C:\Path\To\LaunchBox'"
}

$LaunchBoxRoot = Resolve-LaunchBoxRoot $LaunchBoxRoot
$PluginsRoot = Join-Path $LaunchBoxRoot "Plugins"

if ($ForceCloseLaunchBox) {
  Get-Process LaunchBox, BigBox, GuideVaultReaderLauncher -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
}

$paths = @(
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVaultLaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVault Connector"),
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector.Clean"),
  (Join-Path $PluginsRoot "GuideVault LaunchBox Connector"),
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector.Old"),
  (Join-Path $PluginsRoot "Guidevault-launchbox-plugin"),
  (Join-Path $PluginsRoot "GuideVault\WebView2Profile")
)

foreach ($path in $paths) {
  if (Test-Path -LiteralPath $path) {
    Remove-Item -LiteralPath $path -Recurse -Force
    Write-Host "Removed $path"
  }
}

Get-ChildItem $PluginsRoot -Filter "GuideVault.LaunchBoxConnector*.dll" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $PluginsRoot -Filter "GuideVault.LaunchBoxConnector*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $PluginsRoot -Directory -ErrorAction SilentlyContinue | Where-Object Name -like "*GuideVault*" | Select-Object FullName
