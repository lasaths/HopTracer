<#
.SYNOPSIS
    Publishes the HopTracer application for Windows.

.DESCRIPTION
    This script builds a self-contained, single-file executable for Windows (win-x64).
    It cleans the release directory, publishes the app, and creates a ZIP file for distribution.

.EXAMPLE
    .\scripts\publish_windows.ps1
#>

$ErrorActionPreference = "Stop"

$ProjectFile = Join-Path $PSScriptRoot "..\src_csharp\HopTracer.Maui\HopTracer.Maui.csproj"
$ReleaseDir = Join-Path $PSScriptRoot "..\release"
$OutputDir = Join-Path $ReleaseDir "HopTracer_Windows"
$ZipPath = Join-Path $ReleaseDir "HopTracer_Windows.zip"

Write-Host "Build Configuration:" -ForegroundColor Cyan
Write-Host "  Project: $ProjectFile"
Write-Host "  Output:  $OutputDir"
Write-Host ""

# 1. Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# 2. Publish
Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    -f net9.0-windows10.0.19041.0 `
    -c Release `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:RuntimeIdentifier=win-x64 `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
}

# 3. Zip for distribution
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipPath

Write-Host ""
Write-Host "SUCCESS! Build available at:" -ForegroundColor Green
Write-Host "  Folder: $OutputDir"
Write-Host "  Zip:    $ZipPath"
Write-Host ""
Write-Host "NOTE: When distributing, users may see an 'Unknown Publisher' warning."
Write-Host "      To fix this, you would need to sign the executable with a code signing certificate."
