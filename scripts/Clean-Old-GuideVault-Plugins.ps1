param(
  [string]$LaunchBoxRoot = "D:\HyperspinMasterbuild\LaunchBox",
  [switch]$ForceCloseLaunchBox
)
$ErrorActionPreference = "Stop"
$PluginsRoot = Join-Path $LaunchBoxRoot "Plugins"
if ($ForceCloseLaunchBox) {
  Get-Process LaunchBox, BigBox -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Seconds 2
}
$paths = @(
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVaultLaunchBoxConnector"),
  (Join-Path $PluginsRoot "GuideVault Connector"),
  (Join-Path $PluginsRoot "GuideVault.LaunchBoxConnector.Clean"),
  (Join-Path $PluginsRoot "GuideVault\WebView2Profile")
)
foreach ($path in $paths) {
  if (Test-Path $path) {
    Remove-Item $path -Recurse -Force
    Write-Host "Removed $path"
  }
}
Get-ChildItem $PluginsRoot -Filter "GuideVault.LaunchBoxConnector*.dll" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $PluginsRoot -Filter "GuideVault.LaunchBoxConnector*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $PluginsRoot -Directory | Where-Object Name -like "*GuideVault*" | Select-Object FullName
