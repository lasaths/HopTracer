# HopTracer Build Script
# Builds production-ready portable executable

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

# Step 1: Clean
if (-not $SkipClean) {
    Write-Host "[1/5] Cleaning..." -ForegroundColor Yellow
    
    # Remove bin/obj
    Get-ChildItem -Path $sourceDir -Include bin,obj -Recurse -Directory -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    
    # Clean Release
    if (Test-Path $releaseDir) {
        Remove-Item "$releaseDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # Remove unused files
    $unusedFiles = @(
        "$sourceDir\HopTracer.Web\Controllers\FileController.cs"
    )
    foreach ($file in $unusedFiles) {
        if (Test-Path $file) {
            Remove-Item $file -Force
            Write-Host "  Removed unused file: $(Split-Path $file -Leaf)" -ForegroundColor DarkGray
        }
    }
    
    # Remove internal docs
    $internalDocs = @(
        "$rootDir\Production_Readiness_Summary.md",
        "$rootDir\RELEASE_CHECKLIST.md",
        "$rootDir\FINAL_RELEASE_SUMMARY.md"
    )
    foreach ($doc in $internalDocs) {
        if (Test-Path $doc) {
            Remove-Item $doc -Force
            Write-Host "  Removed: $(Split-Path $doc -Leaf)" -ForegroundColor DarkGray
        }
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

# Step 5: Publish
Write-Host "`n[5/5] Publishing portable executable..." -ForegroundColor Yellow
$outputDir = Join-Path $releaseDir "HopTracer_Portable"
$projectFile = Join-Path $sourceDir "HopTracer\HopTracer.csproj"

Push-Location $sourceDir
try {
    dotnet publish $projectFile `
        -f net9.0-windows10.0.19041.0 `
        -c Release `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:SelfContained=true `
        -o $outputDir `
        --verbosity quiet
    
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    
    $exePath = Join-Path $outputDir "HopTracer.exe"
    if (Test-Path $exePath) {
        $size = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        
        # Count total files
        $fileCount = (Get-ChildItem $outputDir -File -Recurse).Count
        $totalSize = [math]::Round((Get-ChildItem $outputDir -File -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
        
        Write-Host "  ✓ Executable created: $size MB" -ForegroundColor Green
        Write-Host "  ✓ Total package size: $totalSize MB ($fileCount files)" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

# Summary
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Location: $outputDir" -ForegroundColor Cyan
Write-Host "Executable: $outputDir\HopTracer.exe" -ForegroundColor Cyan
Write-Host "`nIMPORTANT: Distribute the entire HopTracer_Portable folder, not just the .exe" -ForegroundColor Yellow
Write-Host "To test: .\Release\HopTracer_Portable\HopTracer.exe" -ForegroundColor White
Write-Host ""
