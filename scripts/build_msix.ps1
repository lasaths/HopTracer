# HopTracer MSIX Build Script
# Produces Microsoft Store-ready MSIX artifacts (.msix / .msixupload).

param(
    [switch]$SkipClean = $false,
    [switch]$SkipTests = $false,
    [switch]$RequireStoreReadiness = $false,
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$IdentityName = "",
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
$cliProject = Join-Path $sourceDir "Tools\GhDiffTool\GhDiffTool.csproj"
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

function Resolve-SigntoolPath {
    $candidates = @(Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'x64' } |
        Sort-Object FullName -Descending)
    if ($candidates.Count -gt 0) { return $candidates[0].FullName }
    $fromPath = Get-Command signtool -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) { return $fromPath.Source }
    return $null
}

function Get-ManifestPublisher {
    param([string]$ManifestPath)
    [xml]$manifest = Get-Content -Raw -Path $ManifestPath
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("f", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $identity = $manifest.SelectSingleNode("/f:Package/f:Identity", $ns)
    if ($null -eq $identity) { return "" }
    $attr = $identity.Attributes["Publisher"]
    if ($null -eq $attr) { return "" }
    return $attr.Value
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

    Write-Host "  OK Clean complete" -ForegroundColor Green
}
else {
    Write-Host "[1/7] Clean skipped" -ForegroundColor Gray
}

Write-Host "`n[2/7] Restoring dependencies..." -ForegroundColor Yellow
Push-Location $sourceDir
try {
    dotnet restore HopTracer.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    Write-Host "  OK Dependencies restored" -ForegroundColor Green
}
finally {
    Pop-Location
}

if (-not $SkipTests) {
    Write-Host "`n[3/7] Running tests..." -ForegroundColor Yellow
    dotnet test (Join-Path $rootDir "Tests\HopTracer.UnitTests\HopTracer.UnitTests.csproj") --configuration $Configuration --verbosity quiet --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "  OK Tests passed" -ForegroundColor Green
}
else {
    Write-Host "`n[3/7] Tests skipped" -ForegroundColor Gray
}

Write-Host "`n[4/7] Checking Store readiness..." -ForegroundColor Yellow
$readinessScript = Join-Path $rootDir "scripts\check_store_readiness.ps1"
if (-not (Test-Path $readinessScript)) {
    throw "Store readiness script not found: $readinessScript"
}

& $readinessScript `
    -Strict:$RequireStoreReadiness `
    -ExpectedIdentityName $(if ($IdentityName) { $IdentityName } else { "lasaths.HopTracer" }) `
    -ExpectedPublisher $(if ($Publisher) { $Publisher } else { "CN=AFE48087-3FFA-435C-A8A2-1776FA3FFA25" }) `
    -ExpectedPublisherDisplayName "lasaths" `
    -ExpectedPackageVersion $(if ($PackageVersion) { $PackageVersion } else { "1.2.0.1" })
if (-not $?) {
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

Write-Host "  OK Shell extension built: $shellExtensionDll" -ForegroundColor Green

Write-Host "`n[5b/7] Publishing hoptracer CLI for MSIX..." -ForegroundColor Yellow
$cliStagingDir = Join-Path $msixOutputDir "_cli_staging"
if (Test-Path $cliStagingDir) {
    Remove-Item $cliStagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

dotnet publish $cliProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $cliStagingDir `
    --verbosity quiet `
    --ignore-failed-sources `
    -p:NuGetAudit=false `
    -p:BuildInParallel=false

if ($LASTEXITCODE -ne 0) { throw "CLI publish failed" }

$ghIoSource = Join-Path $sourceDir "HopTracer.Web\tools\GH_IO.dll"
if (Test-Path $ghIoSource) {
    Copy-Item -Path $ghIoSource -Destination (Join-Path $cliStagingDir "GH_IO.dll") -Force
}

if (-not (Test-Path (Join-Path $cliStagingDir "hoptracer.exe"))) {
    throw "hoptracer.exe was not produced in $cliStagingDir"
}

# Satellite resource DLLs trigger PRI263 warnings in MSIX; English-only CLI does not need them.
Get-ChildItem -Path $cliStagingDir -Recurse -Filter "*.resources.dll" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "  OK CLI staged: $cliStagingDir" -ForegroundColor Green

Write-Host "`n[6/7] Publishing MSIX package..." -ForegroundColor Yellow
if (-not (Test-Path $msixOutputDir)) {
    New-Item -ItemType Directory -Path $msixOutputDir -Force | Out-Null
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
    "-p:AppxPackageSigningEnabled=false",
    "-p:ShellExtensionDllPath=$shellExtensionDll",
    "-p:HopTracerCliDir=$cliStagingDir"
)

if (-not [string]::IsNullOrWhiteSpace($IdentityName)) {
    $publishArgs += "-p:ApplicationId=$IdentityName"
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:ApplicationDisplayVersion=$Version"
}

if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $publishArgs += "-p:PackageVersion=$PackageVersion"
}

if (-not [string]::IsNullOrWhiteSpace($Publisher)) {
    $publishArgs += "-p:Publisher=$Publisher"
}

Push-Location $sourceDir
try {
    dotnet @publishArgs --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "MSIX publish failed" }
}
finally {
    Pop-Location
}

Write-Host "`n[7/7] Signing and packaging artifacts..." -ForegroundColor Yellow
$msixFiles = @(Get-ChildItem -Path $msixOutputDir -Recurse -File -Filter *.msix -ErrorAction SilentlyContinue)

if (-not $msixFiles) {
    throw "No MSIX artifacts were generated in $msixOutputDir."
}

# Resolve signtool
$signtool = Resolve-SigntoolPath
if ([string]::IsNullOrWhiteSpace($signtool)) {
    Write-Host "  ! signtool.exe not found - package will not be signed. Install Windows SDK." -ForegroundColor Yellow
}
else {
    # Resolve signing certificate
    $resolvedCertPath = ""
    $tempPfxPath = ""

    if (-not [string]::IsNullOrWhiteSpace($CertificatePath) -and (Test-Path $CertificatePath)) {
        $resolvedCertPath = $CertificatePath
        Write-Host "  Using provided certificate: $resolvedCertPath" -ForegroundColor DarkGray
    }
    else {
        # Auto-generate a self-signed cert matching the manifest Publisher
        $manifestPath = Join-Path $sourceDir "HopTracer\Platforms\Windows\Package.appxmanifest"
        $publisherSubject = if (-not [string]::IsNullOrWhiteSpace($Publisher)) { $Publisher } else { Get-ManifestPublisher $manifestPath }

        if (-not [string]::IsNullOrWhiteSpace($publisherSubject)) {
            Write-Host "  Checking for certificate support..." -ForegroundColor DarkGray
            
            # Check if Certificate provider exists
            if (Get-PSProvider Certificate -ErrorAction SilentlyContinue) {
                Write-Host "  Auto-generating self-signed cert for: $publisherSubject" -ForegroundColor DarkGray
                
                # Ensure Cert: drive is available
                if (-not (Get-PSDrive Cert -ErrorAction SilentlyContinue)) {
                    New-PSDrive -Name Cert -PSProvider Certificate -Root \ | Out-Null
                }

                $tempPfxPath = Join-Path $msixOutputDir "_temp-signing.pfx"
                $tempPass = "HopTracerTempSign!"
                $cert = New-SelfSignedCertificate `
                    -Type Custom `
                    -Subject $publisherSubject `
                    -KeyUsage DigitalSignature `
                    -FriendlyName "HopTracer Store Signing (temp)" `
                    -CertStoreLocation "Cert:\CurrentUser\My" `
                    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
                $secPass = ConvertTo-SecureString -String $tempPass -Force -AsPlainText
                Export-PfxCertificate -Cert $cert -FilePath $tempPfxPath -Password $secPass | Out-Null
                $resolvedCertPath = $tempPfxPath
                $CertificatePassword = $tempPass
            }
            else {
                Write-Host "  ! Certificate provider not available. Skipping auto-signing." -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  ! Could not determine Publisher subject - skipping signing." -ForegroundColor Yellow
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($resolvedCertPath)) {
        foreach ($msix in $msixFiles) {
            & $signtool sign /fd SHA256 /a /p "$CertificatePassword" /f "$resolvedCertPath" "$($msix.FullName)" | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "signtool failed for: $($msix.FullName)"
            }
            Write-Host ("  OK Signed: {0}" -f $msix.Name) -ForegroundColor Green
        }

        # Clean up temp cert
        if (-not [string]::IsNullOrWhiteSpace($tempPfxPath) -and (Test-Path $tempPfxPath)) {
            Remove-Item $tempPfxPath -Force -ErrorAction SilentlyContinue
        }
    }
}

foreach ($f in $msixFiles) {
    Write-Host ("  OK MSIX: {0}" -f $f.FullName) -ForegroundColor Green
}

$msixUploadFiles = @(Get-ChildItem -Path $msixOutputDir -Recurse -File -Filter *.msixupload -ErrorAction SilentlyContinue)
if ($msixUploadFiles.Count -gt 0) {
    foreach ($upload in $msixUploadFiles) {
        Write-Host ("  OK MSIXUPLOAD (SDK): {0}" -f $upload.FullName) -ForegroundColor Green
    }
}
else {
    Write-Host "  ! SDK did not emit .msixupload; creating upload archive from .msix" -ForegroundColor Yellow
    foreach ($msix in $msixFiles) {
        $uploadPath = [System.IO.Path]::ChangeExtension($msix.FullName, ".msixupload")
        $tmpZip     = "$uploadPath.zip"
        if (Test-Path $tmpZip) { Remove-Item $tmpZip -Force }
        Compress-Archive -Path $msix.FullName -DestinationPath $tmpZip -Force
        Move-Item $tmpZip $uploadPath -Force
        Write-Host ("  OK MSIXUPLOAD (fallback): {0}" -f $uploadPath) -ForegroundColor Green
    }
}

Write-Host "`n=== MSIX Build Complete ===" -ForegroundColor Green
Write-Host "Output directory: $msixOutputDir" -ForegroundColor Cyan
