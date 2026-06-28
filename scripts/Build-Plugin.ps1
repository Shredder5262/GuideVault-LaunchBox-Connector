param(
  [string]$LaunchBoxRoot = "D:\HyperspinMasterbuild\LaunchBox",
  [string]$Configuration = "Release",
  [string]$Framework = "net9.0-windows"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot "GuideVault.LaunchBoxConnector.csproj"
$LauncherProject = Join-Path $ProjectRoot "Launcher\GuideVaultReaderLauncher\GuideVaultReaderLauncher.csproj"
$CoreDll = Join-Path $LaunchBoxRoot "Core\Unbroken.LaunchBox.Plugins.dll"
$MetadataDll = Join-Path $LaunchBoxRoot "Metadata\Unbroken.LaunchBox.Plugins.dll"

if (-not (Test-Path $Project)) {
  throw "Could not find project file: $Project. This package should be extracted directly into C:\Users\Andrew\Documents\VSCode\Guidevault-launchbox-plugin."
}

if (-not (Test-Path $LauncherProject)) {
  throw "Could not find WebView2 launcher project: $LauncherProject."
}

$PluginApi = $CoreDll
if (-not (Test-Path $PluginApi) -and (Test-Path $MetadataDll)) {
  $PluginApi = $MetadataDll
}

if (-not (Test-Path $PluginApi)) {
  throw "Could not find Unbroken.LaunchBox.Plugins.dll under '$LaunchBoxRoot\Core' or '$LaunchBoxRoot\Metadata'. Pass -LaunchBoxRoot with your actual LaunchBox install path."
}

# Repair any stale hard favicon resource references from older extracted patches before MSBuild evaluates the project.
# This deliberately removes ANY embedded-resource item whose Include or LogicalName mentions favicon.png.
# The plugin no longer needs favicon.png as a compile-time resource; it uses GuideVault.MatchedItems.png for icons/badges.
$projectText = Get-Content -LiteralPath $Project -Raw
$projectText = [regex]::Replace($projectText, '(?is)\s*<EmbeddedResource\b(?=[^>]*(?:Include|LogicalName)=[''\"][^''\"]*favicon\.png)[^>]*(?:/>|>.*?</EmbeddedResource>)\s*', "`r`n")
$projectText = [regex]::Replace($projectText, '(?is)\s*<Resource\b(?=[^>]*(?:Include|LogicalName)=[''\"][^''\"]*favicon\.png)[^>]*(?:/>|>.*?</Resource>)\s*', "`r`n")
$projectText = [regex]::Replace($projectText, '(?im)^\s*<EmbeddedResource\s+Include=[''\"]Assets[/\\]favicon\.png[''\"][^>]*(?:/>|>.*?</EmbeddedResource>)\s*$', '')
if ($projectText -notmatch '<EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>') {
  $projectText = $projectText -replace '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>', '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`r`n    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>'
}
Set-Content -LiteralPath $Project -Value $projectText -Encoding UTF8

$targetLine = Select-String -Path $Project -Pattern '<TargetFramework>' | Select-Object -First 1
Write-Host "Project root: $ProjectRoot"
Write-Host "Project: $Project"
Write-Host "WebView2 launcher: $LauncherProject"
Write-Host "LaunchBox plugin API: $PluginApi"
Write-Host "Target: $($targetLine.Line.Trim())"

# Normalize local asset folder before MSBuild evaluates resources.
# This prevents stale/partial extractions from creating hard resource failures.
$AssetsDir = Join-Path $ProjectRoot "Assets"
$BadgeAsset = Join-Path $AssetsDir "GuideVault.MatchedItems.png"
$FaviconAsset = Join-Path $AssetsDir "favicon.png"
if (-not (Test-Path -LiteralPath $AssetsDir)) { New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null }
if ((-not (Test-Path -LiteralPath $FaviconAsset)) -and (Test-Path -LiteralPath $BadgeAsset)) {
  Copy-Item -LiteralPath $BadgeAsset -Destination $FaviconAsset -Force
  Write-Host "Restored missing favicon.png from GuideVault.MatchedItems.png."
}
Write-Host "Assets present: $((Get-ChildItem -Path $AssetsDir -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join ', ')"
Write-Host "Resource lines:"
Select-String -Path $Project -Pattern "EmbeddedResource|EnableDefaultEmbeddedResourceItems|favicon" | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
$remainingFaviconResource = Select-String -Path $Project -Pattern "EmbeddedResource.*favicon|favicon.*EmbeddedResource" -Quiet
if ($remainingFaviconResource) { throw "A stale favicon embedded resource is still present in $Project. Open the project file and remove the favicon EmbeddedResource line." }

$sdks = dotnet --list-sdks
if ($LASTEXITCODE -ne 0) { throw "dotnet SDK was not found on PATH." }
if (($sdks -join "`n") -notmatch '^9\.') {
  throw "The .NET 9 SDK is required because your LaunchBox plugin API references System.Runtime 9.0. Install .NET 9 SDK, then retry."
}

$expectedSourceFiles = @(
  "GuideVaultActions.cs",
  "GuideVaultClient.cs",
  "GuideVaultAssets.cs",
  "GuideVaultMatchedItemsBadge.cs",
  "GuideVaultRelationshipWindow.cs",
  "GuideVaultBadgeCache.cs",
  "GuideVaultConnectorWindow.cs",
  "GuideVaultWebViewWindow.cs",
  "GuideVaultGameMenuPlugin.cs",
  "GuideVaultSystemMenuPlugin.cs",
  "LaunchBoxGameMapper.cs",
  "PluginModels.cs",
  "SettingsStore.cs"
)
$rootCsFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.cs" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
$ignoredSourceFiles = $rootCsFiles | Where-Object { $expectedSourceFiles -notcontains $_ }
if ($ignoredSourceFiles) {
  Write-Warning "Ignoring stale root .cs files that are no longer compiled: $($ignoredSourceFiles -join ', ')"
}

Remove-Item -Recurse -Force (Join-Path $ProjectRoot "bin"), (Join-Path $ProjectRoot "obj") -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force (Join-Path $ProjectRoot "Launcher\GuideVaultReaderLauncher\bin"), (Join-Path $ProjectRoot "Launcher\GuideVaultReaderLauncher\obj") -ErrorAction SilentlyContinue

dotnet restore $Project /p:LaunchBoxRoot="$LaunchBoxRoot" /p:LaunchBoxPluginApi="$PluginApi"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build $Project -c $Configuration -f $Framework --no-restore /p:LaunchBoxRoot="$LaunchBoxRoot" /p:LaunchBoxPluginApi="$PluginApi"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet restore $LauncherProject
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build $LauncherProject -c $Configuration -f $Framework --no-restore
exit $LASTEXITCODE
