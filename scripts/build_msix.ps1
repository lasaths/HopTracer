# HopTracer MSIX Build Script
# Produces Microsoft Store-ready MSIX artifacts (.msix / .msixupload).

param(
    [switch]$SkipClean = $false,
    [switch]$SkipTests = $false,
    [switch]$RequireStoreReadiness = $false,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "",
    [string]$PackageVersion = "",
    [string]$Publisher = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot | Split-Path -Parent
$sourceDir = Join-Path $rootDir "Source"
$releaseDir = Join-Path $rootDir "Release"
$projectFile = Join-Path $sourceDir "HopTracer\HopTracer.csproj"
$msixOutputDir = Join-Path $releaseDir "MSIX"
$shellExtensionProject = Join-Path $sourceDir "HopTracer.ShellExtension\HopTracer.ShellExtension.vcxproj"
$shellExtensionDll = Join-Path $sourceDir "HopTracer.ShellExtension\bin\$Configuration\x64\HopTracer.ShellExtension.dll"

function Resolve-MSBuildPath {
    $fromPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    $vswhereCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
        (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path $_) }

    foreach ($vswhere in $vswhereCandidates) {
        $installPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installPath)) {
            $candidate = Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $fallbacks = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $fallbacks) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    return $null
}

Write-Host "=== HopTracer MSIX Build ===" -ForegroundColor Cyan
Write-Host ""

if (-not $SkipClean) {
    Write-Host "[1/7] Cleaning..." -ForegroundColor Yellow
    Get-ChildItem -Path $sourceDir -Include bin,obj -Recurse -Directory -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    if (Test-Path $msixOutputDir) {
        Remove-Item (Join-Path $msixOutputDir "*") -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "  ✓ Clean complete" -ForegroundColor Green
}
else {
    Write-Host "[1/7] Clean skipped" -ForegroundColor Gray
}

Write-Host "`n[2/7] Restoring dependencies..." -ForegroundColor Yellow
Push-Location $sourceDir
try {
    dotnet restore HopTracer.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "  ✓ Dependencies restored" -ForegroundColor Green
}
finally {
    Pop-Location
}

if (-not $SkipTests) {
    Write-Host "`n[3/7] Running tests..." -ForegroundColor Yellow
    dotnet test (Join-Path $rootDir "Tests\HopTracer.UnitTests\HopTracer.UnitTests.csproj") --configuration $Configuration --verbosity quiet --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "  ✓ Tests passed" -ForegroundColor Green
}
else {
    Write-Host "`n[3/7] Tests skipped" -ForegroundColor Gray
}

Write-Host "`n[4/7] Checking Store readiness..." -ForegroundColor Yellow
$readinessScript = Join-Path $rootDir "scripts\check_store_readiness.ps1"
if (-not (Test-Path $readinessScript)) {
    throw "Store readiness script not found: $readinessScript"
}

& $readinessScript -Strict:$RequireStoreReadiness
if ($LASTEXITCODE -ne 0) {
    throw "Store readiness checks failed."
}

Write-Host "`n[5/7] Building Explorer shell extension..." -ForegroundColor Yellow
if (-not (Test-Path $shellExtensionProject)) {
    throw "Shell extension project not found: $shellExtensionProject"
}

$msbuildPath = Resolve-MSBuildPath
if ([string]::IsNullOrWhiteSpace($msbuildPath)) {
    throw "msbuild.exe not found. Install Visual Studio Build Tools with the C++ workload to build the MSIX shell extension."
}

& $msbuildPath $shellExtensionProject "/t:Build" "/p:Configuration=$Configuration" "/p:Platform=x64" "/m" "/nologo"
if ($LASTEXITCODE -ne 0) {
    throw "Shell extension build failed"
}

if (-not (Test-Path $shellExtensionDll)) {
    throw "Shell extension DLL was not produced: $shellExtensionDll"
}

Write-Host "  ✓ Shell extension built: $shellExtensionDll" -ForegroundColor Green

Write-Host "`n[6/7] Publishing MSIX package..." -ForegroundColor Yellow
if (-not (Test-Path $msixOutputDir)) {
    New-Item -ItemType Directory -Path $msixOutputDir -Force | Out-Null
}

$appxSigningEnabled = $false
$resolvedCertPath = ""
if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    $resolvedCertPath = Resolve-Path $CertificatePath -ErrorAction Stop | Select-Object -ExpandProperty Path
    $appxSigningEnabled = $true
}

$publishArgs = @(
    "publish", $projectFile,
    "-f", "net10.0-windows10.0.19041.0",
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "-p:GenerateAppxPackageOnBuild=true",
    "-p:WindowsPackageType=MSIX",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:AppxBundle=Never",
    "-p:UapAppxPackageBuildMode=StoreUpload",
    "-p:AppxPackageDir=$msixOutputDir\\",
    "-p:AppxPackageSigningEnabled=$appxSigningEnabled",
    "-p:ShellExtensionDllPath=$shellExtensionDll"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:ApplicationDisplayVersion=$Version"
}

if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $publishArgs += "-p:PackageVersion=$PackageVersion"
}

if (-not [string]::IsNullOrWhiteSpace($Publisher)) {
    $publishArgs += "-p:Publisher=$Publisher"
    $publishArgs += "-p:PackageCertificateSubjectName=$Publisher"
}

if ($appxSigningEnabled) {
    $publishArgs += "-p:PackageCertificateKeyFile=$resolvedCertPath"
    if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
        $publishArgs += "-p:PackageCertificatePassword=$CertificatePassword"
    }
}

Push-Location $sourceDir
try {
    dotnet @publishArgs --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "MSIX publish failed" }
}
finally {
    Pop-Location
}

Write-Host "`n[7/7] Collecting artifacts..." -ForegroundColor Yellow
$msixFiles = Get-ChildItem -Path $msixOutputDir -Recurse -File -Filter *.msix -ErrorAction SilentlyContinue
$msixUploadFiles = Get-ChildItem -Path $msixOutputDir -Recurse -File -Filter *.msixupload -ErrorAction SilentlyContinue

if (-not $msixFiles -and -not $msixUploadFiles) {
    throw "No MSIX artifacts were generated in $msixOutputDir."
}

if ($msixFiles) {
    foreach ($f in $msixFiles) {
        Write-Host ("  ✓ MSIX: {0}" -f $f.FullName) -ForegroundColor Green
    }
}

if ($msixUploadFiles) {
    foreach ($f in $msixUploadFiles) {
        Write-Host ("  ✓ MSIXUPLOAD: {0}" -f $f.FullName) -ForegroundColor Green
    }
}
else {
    if ($msixFiles) {
        Write-Host "  ! No .msixupload generated by SDK. Creating fallback .msixupload from .msix package..." -ForegroundColor Yellow
        foreach ($msix in $msixFiles) {
            $fallbackZip = "$($msix.FullName).zip"
            $fallbackUpload = [System.IO.Path]::ChangeExtension($msix.FullName, ".msixupload")
            if (Test-Path $fallbackZip) {
                Remove-Item $fallbackZip -Force
            }
            if (Test-Path $fallbackUpload) {
                Remove-Item $fallbackUpload -Force
            }
            Compress-Archive -Path $msix.FullName -DestinationPath $fallbackZip -Force
            Move-Item $fallbackZip $fallbackUpload -Force
            Write-Host ("  ✓ Fallback MSIXUPLOAD: {0}" -f $fallbackUpload) -ForegroundColor Green
        }
    }
    else {
        Write-Host "  ! No .msixupload file found. Microsoft Store submissions typically use .msixupload." -ForegroundColor Yellow
    }
}

Write-Host "`n=== MSIX Build Complete ===" -ForegroundColor Green
Write-Host "Output directory: $msixOutputDir" -ForegroundColor Cyan
