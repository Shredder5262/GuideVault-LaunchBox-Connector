param(
  [string]$ProjectPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "GuideVault.LaunchBoxConnector.csproj")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ProjectPath)) {
  throw "Could not find project file: $ProjectPath"
}

$text = Get-Content -LiteralPath $ProjectPath -Raw
$text = [regex]::Replace($text, '(?is)\s*<EmbeddedResource\b(?=[^>]*(?:Include|LogicalName)=[''\"][^''\"]*favicon\.png)[^>]*(?:/>|>.*?</EmbeddedResource>)\s*', "`r`n")
$text = [regex]::Replace($text, '(?is)\s*<Resource\b(?=[^>]*(?:Include|LogicalName)=[''\"][^''\"]*favicon\.png)[^>]*(?:/>|>.*?</Resource>)\s*', "`r`n")
$text = [regex]::Replace($text, '(?im)^\s*<EmbeddedResource\s+Include=[''\"]Assets[/\\]favicon\.png[''\"][^>]*(?:/>|>.*?</EmbeddedResource>)\s*$', '')

if ($text -notmatch '<EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>') {
  $text = $text -replace '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>', '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`r`n    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>'
}

Set-Content -LiteralPath $ProjectPath -Value $text -Encoding UTF8
Remove-Item -Recurse -Force (Join-Path (Split-Path -Parent $ProjectPath) "bin"), (Join-Path (Split-Path -Parent $ProjectPath) "obj") -ErrorAction SilentlyContinue
Write-Host "Cleaned favicon embedded-resource references from: $ProjectPath"
Write-Host "Remaining matching lines:"
Select-String -Path $ProjectPath -Pattern "favicon|EmbeddedResource|EnableDefaultEmbeddedResourceItems" | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
