# Builds the DeckSurf MSI: publishes the self-contained app, nests the Barn
# plugin under plugins\, and hands the staging tree to the WiX project.
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$staging = Join-Path $root "src\bin\installer-staging"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}

Write-Host "Publishing DeckSurf.App to $staging"
dotnet publish (Join-Path $root "src\DeckSurf\DeckSurf.App\DeckSurf.App.csproj") `
    -c $Configuration --self-contained true -o $staging -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing DeckSurf.Plugin.Barn into the staged plugins folder"
dotnet publish (Join-Path $root "src\DeckSurf\DeckSurf.Plugin.Barn\DeckSurf.Plugin.Barn.csproj") `
    -c $Configuration -o (Join-Path $staging "plugins\DeckSurf.Plugin.Barn")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Debug symbols have no business inside an installer.
Get-ChildItem $staging -Recurse -Filter *.pdb | Remove-Item -Force

Write-Host "Building the MSI"
dotnet build (Join-Path $root "src\DeckSurf\DeckSurf.Installer\DeckSurf.Installer.wixproj") `
    -c $Configuration -p:ProductVersion=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$msi = Get-ChildItem (Join-Path $root "src\DeckSurf\DeckSurf.Installer\bin") -Recurse -Filter DeckSurf.msi |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "MSI: $($msi.FullName)"
