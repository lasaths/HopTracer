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

Write-Host "=== HopTracer Production Build ===" -ForegroundColor Cyan
Write-Host ""

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
    
    Write-Host "  ✓ Clean complete" -ForegroundColor Green
}

# Step 2: Restore
Write-Host "`n[2/5] Restoring dependencies..." -ForegroundColor Yellow
Push-Location $sourceDir
try {
    dotnet restore HopTracer.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "  ✓ Dependencies restored" -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 3: Test
if (-not $SkipTests) {
    Write-Host "`n[3/5] Running tests..." -ForegroundColor Yellow
    dotnet test (Join-Path $sourceDir "HopTracer.sln") --configuration Release --verbosity quiet --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ⚠ Some tests failed" -ForegroundColor Yellow
    } else {
        Write-Host "  ✓ Tests passed" -ForegroundColor Green
    }
} else {
    Write-Host "`n[3/5] Tests skipped" -ForegroundColor Gray
}

# Step 4: Build
Write-Host "`n[4/5] Building Release..." -ForegroundColor Yellow
Push-Location $sourceDir
try {
    dotnet build HopTracer.sln -c Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "  ✓ Build successful" -ForegroundColor Green
} finally {
    Pop-Location
}

# Step 5: Publish and package
Write-Host "`n[5/5] Publishing portable package..." -ForegroundColor Yellow
$outputDir = Join-Path $releaseDir "HopTracer_Portable"
$zipPath = Join-Path $releaseDir "HopTracer-Windows-x64.zip"
$projectFile = Join-Path $sourceDir "HopTracer\HopTracer.csproj"

Push-Location $sourceDir
try {
    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }

    dotnet publish $projectFile `
        -f net10.0-windows10.0.19041.0 `
        -c Release `
        -r win-x64 `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:SelfContained=true `
        -p:PublishSingleFile=false `
        -o $outputDir `
        --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    
    $exePath = Join-Path $outputDir "HopTracer.exe"
    if (Test-Path $exePath) {
        $fileCount = (Get-ChildItem -Path $outputDir -Recurse -File).Count
        $folderSizeMb = [math]::Round(((Get-ChildItem -Path $outputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum) / 1MB, 2)

        Write-Host "  ✓ Portable package created: $folderSizeMb MB ($fileCount files)" -ForegroundColor Green

        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force
        }

        Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -Force
        $zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
        Write-Host "  ✓ Release archive created: $zipPath ($zipSizeMb MB)" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

# Summary
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Portable folder: $outputDir" -ForegroundColor Cyan
Write-Host "Release archive: $zipPath" -ForegroundColor Cyan
Write-Host "To test: .\Release\HopTracer_Portable\HopTracer.exe" -ForegroundColor White
Write-Host ""
