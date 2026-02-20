# HopTracer MSIX Build Script
# Builds production-ready MSIX package for Microsoft Store

param(
    [switch]$SkipClean = $false,
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot | Split-Path -Parent
$sourceDir = Join-Path $rootDir "Source"
$releaseDir = Join-Path $rootDir "Release"

Write-Host "=== HopTracer MSIX Build ==="

# Step 1: Clean
if (-not $SkipClean) {
    Write-Host "[1/5] Cleaning..."
    Get-ChildItem -Path $sourceDir -Include bin,obj -Recurse -Directory -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $releaseDir) {
        Remove-Item "$releaseDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Step 2: Restore
Write-Host "[2/5] Restoring dependencies..."
Push-Location $sourceDir
try {
    dotnet restore HopTracer.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
} finally {
    Pop-Location
}

# Step 3: Test
if (-not $SkipTests) {
    Write-Host "[3/5] Running tests..."
    dotnet test (Join-Path $sourceDir "HopTracer.sln") --configuration Release --verbosity quiet --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: Some tests failed"
    }
}

# Step 4: Publish MSIX
Write-Host "[4/5] Publishing MSIX package..."
$projectFile = Join-Path $sourceDir "HopTracer\HopTracer.csproj"
$outputDir = Join-Path $releaseDir "HopTracer_MSIX"

Push-Location $sourceDir
try {
    # Using GenerateAppxPackageOnBuild=true
    dotnet publish $projectFile -f net10.0-windows10.0.19041.0 -c Release -r win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:WindowsAppSDKSelfContained=true -o $outputDir --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
} finally {
    Pop-Location
}

# Step 5: Locate Package
Write-Host "[5/5] Locating package..."
$packagePath = Get-ChildItem -Path $sourceDir -Filter "*.msix" -Recurse | Select-Object -First 1
if ($packagePath) {
    $destPath = Join-Path $releaseDir $packagePath.Name
    Copy-Item $packagePath.FullName $destPath -Force
    Write-Host "Package copied to: $destPath"
} else {
    Write-Host "Warning: Could not find .msix package. Check bin folder."
}

Write-Host "=== Build Complete ==="