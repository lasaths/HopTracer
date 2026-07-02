# HopTracer Build Script
# Builds production-ready portable package for GitHub releases

param(
    [switch]$SkipClean = $false,
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot | Split-Path -Parent
$sourceDir = Join-Path $rootDir "Source"
$releaseDir = Join-Path $rootDir "Release"
$appProject = Join-Path $sourceDir "HopTracer\HopTracer.csproj"
$cliProject = Join-Path $sourceDir "Tools\GhDiffTool\GhDiffTool.csproj"
$testProject = Join-Path $rootDir "Tests\HopTracer.UnitTests\HopTracer.UnitTests.csproj"

# Keep dotnet's first-run state inside the repository so the build does not touch
# a sandbox-specific profile path.
$dotnetHome = Join-Path $rootDir ".dotnet-home"
$dotnetAppData = Join-Path $dotnetHome "AppData"
$localPackages = Join-Path $env:USERPROFILE ".nuget\packages"
$nugetSource = "https://api.nuget.org/v3/index.json"

$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:APPDATA = Join-Path $dotnetAppData "Roaming"
$env:LOCALAPPDATA = Join-Path $dotnetAppData "Local"
$env:RestoreSources = "$localPackages;$nugetSource"
$env:RestoreIgnoreFailedSources = "true"
$env:NuGetAudit = "false"

$restoreSourceArgs = @("--source", $nugetSource)
if (Test-Path $localPackages) {
    $restoreSourceArgs = @("--source", $localPackages) + $restoreSourceArgs
}

if (-not (Test-Path $dotnetHome)) {
    New-Item -ItemType Directory -Path $dotnetHome | Out-Null
}
foreach ($path in @($env:APPDATA, $env:LOCALAPPDATA)) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

Write-Host "=== HopTracer Production Build ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "  dotnet home: $env:DOTNET_CLI_HOME" -ForegroundColor DarkGray
Write-Host "  user profile: $env:USERPROFILE" -ForegroundColor DarkGray
Write-Host "  package cache: $localPackages" -ForegroundColor DarkGray
Write-Host "  package feed: $nugetSource" -ForegroundColor DarkGray

# Step 1: Clean build outputs
if (-not $SkipClean) {
    Write-Host "[1/5] Cleaning..." -ForegroundColor Yellow

    # Remove bin/obj
    Get-ChildItem -Path $sourceDir -Include bin,obj -Recurse -Directory -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    # Clean Release
    if (Test-Path $releaseDir) {
        Remove-Item "$releaseDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "  OK Clean complete" -ForegroundColor Green
}

# Step 2: Restore
Write-Host "`n[2/5] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore $appProject --verbosity normal --ignore-failed-sources -p:NuGetAudit=false -p:BuildInParallel=false @restoreSourceArgs
if ($LASTEXITCODE -ne 0) { throw "App restore failed" }
dotnet restore $cliProject --verbosity normal --ignore-failed-sources -p:NuGetAudit=false -p:BuildInParallel=false @restoreSourceArgs
if ($LASTEXITCODE -ne 0) { throw "CLI restore failed" }
dotnet restore $testProject --verbosity normal --ignore-failed-sources -p:NuGetAudit=false -p:BuildInParallel=false @restoreSourceArgs
if ($LASTEXITCODE -ne 0) { throw "Test restore failed" }
Write-Host "  OK Dependencies restored" -ForegroundColor Green

# Step 3: Test
if (-not $SkipTests) {
    Write-Host "`n[3/5] Running tests..." -ForegroundColor Yellow
    dotnet test $testProject --configuration Release --verbosity quiet --no-restore -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed"
    }
    Write-Host "  OK Tests passed" -ForegroundColor Green
}
else {
    Write-Host "`n[3/5] Tests skipped" -ForegroundColor Gray
}

# Step 4: Build
Write-Host "`n[4/5] Building Release..." -ForegroundColor Yellow
dotnet build $appProject -c Release --no-restore --verbosity quiet -p:BuildInParallel=false
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "  OK Build successful" -ForegroundColor Green

# Step 5: Publish and package
Write-Host "`n[5/5] Publishing portable package..." -ForegroundColor Yellow
$outputDir = Join-Path $releaseDir "HopTracer_Portable"
$stagingDir = Join-Path $releaseDir "HopTracer_Portable_staging"
$zipPath = Join-Path $releaseDir "HopTracer-Windows-x64.zip"

# Publish to staging first so a locked HopTracer.exe under HopTracer_Portable does not fail the publish.
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

dotnet publish $appProject `
    -f net10.0-windows10.0.19041.0 `
    -c Release `
    -r win-x64 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishSingleFile=false `
    -o $stagingDir `
    --verbosity quiet `
    --ignore-failed-sources `
    -p:NuGetAudit=false `
    -p:BuildInParallel=false `
    @restoreSourceArgs

if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

$cliOutputDir = Join-Path $stagingDir "tools\hoptracer"
dotnet publish $cliProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $cliOutputDir `
    --verbosity quiet `
    --ignore-failed-sources `
    -p:NuGetAudit=false `
    -p:BuildInParallel=false `
    @restoreSourceArgs

if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

$ghIoSource = Join-Path $sourceDir "HopTracer.Web\tools\GH_IO.dll"
if (Test-Path $ghIoSource) {
    Copy-Item -Path $ghIoSource -Destination (Join-Path $cliOutputDir "GH_IO.dll") -Force
}

$finalOutputDir = $outputDir
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path $outputDir) {
    Write-Host "  Warning: Could not remove $outputDir (close HopTracer.exe if it is running from this folder)." -ForegroundColor Yellow
    Write-Host "  Using staging output: $stagingDir" -ForegroundColor Yellow
    $finalOutputDir = $stagingDir
} else {
    Rename-Item -Path $stagingDir -NewName "HopTracer_Portable"
}

$exePath = Join-Path $finalOutputDir "HopTracer.exe"
if (Test-Path $exePath) {
    $portableScripts = @(
        "Install-ExplorerMenu.ps1",
        "Uninstall-ExplorerMenu.ps1"
    )

    foreach ($scriptName in $portableScripts) {
        $sourceScript = Join-Path $rootDir "scripts\$scriptName"
        if (Test-Path $sourceScript) {
            Copy-Item -Path $sourceScript -Destination (Join-Path $finalOutputDir $scriptName) -Force
        }
    }

    $fileCount = (Get-ChildItem -Path $finalOutputDir -Recurse -File).Count
    $folderSizeMb = [math]::Round(((Get-ChildItem -Path $finalOutputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum) / 1MB, 2)

    Write-Host "  OK Portable package created: $folderSizeMb MB ($fileCount files)" -ForegroundColor Green

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $finalOutputDir "*") -DestinationPath $zipPath -Force
    $zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "  OK Release archive created: $zipPath ($zipSizeMb MB)" -ForegroundColor Green
}

# Summary
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Portable folder: $finalOutputDir" -ForegroundColor Cyan
Write-Host "Release archive: $zipPath" -ForegroundColor Cyan
Write-Host "To test: $finalOutputDir\HopTracer.exe" -ForegroundColor White
Write-Host "CLI tool: $finalOutputDir\tools\hoptracer\hoptracer.exe" -ForegroundColor White
Write-Host ""
