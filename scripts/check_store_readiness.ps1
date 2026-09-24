# HopTracer Store Readiness Check
# Validates MSIX manifest fields and highlights Store submission risks.

param(
    [switch]$Strict = $false,
    [string]$ExpectedIdentityName = "lasaths.HopTracer",
    [string]$ExpectedPublisher = "CN=AFE48087-3FFA-435C-A8A2-1776FA3FFA25",
    [string]$ExpectedPublisherDisplayName = "lasaths",
    [string]$ExpectedPackageVersion = "1.2.1.0"
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot | Split-Path -Parent
$manifestPath = Join-Path $rootDir "Source\HopTracer\Platforms\Windows\Package.appxmanifest"

if (-not (Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}

[xml]$manifest = Get-Content -Raw -Path $manifestPath
$ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace("f", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
$ns.AddNamespace("rescap", "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities")
$ns.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")

$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Get-GitOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($null -eq $git) {
        return [pscustomobject]@{
            Available = $false
            ExitCode = 127
            Output = @()
        }
    }

    $output = & $git.Source -C $rootDir @Arguments 2>$null
    return [pscustomobject]@{
        Available = $true
        ExitCode = $LASTEXITCODE
        Output = @($output)
    }
}

$identity = $manifest.SelectSingleNode("/f:Package/f:Identity", $ns)
if ($null -eq $identity) {
    $errors.Add("Missing Identity node in Package.appxmanifest.")
}

$identityName = ""
$identityPublisher = ""
$identityVersion = ""
$publisherDisplayName = ""
if ($null -ne $identity) {
    $nameAttr = $identity.Attributes["Name"]
    $publisherAttr = $identity.Attributes["Publisher"]
    $versionAttr = $identity.Attributes["Version"]
    if ($null -ne $nameAttr) { $identityName = $nameAttr.Value }
    if ($null -ne $publisherAttr) { $identityPublisher = $publisherAttr.Value }
    if ($null -ne $versionAttr) { $identityVersion = $versionAttr.Value }
}

$propertiesNode = $manifest.SelectSingleNode("/f:Package/f:Properties", $ns)
if ($null -ne $propertiesNode) {
    $publisherDisplayNameNode = $propertiesNode.SelectSingleNode("f:PublisherDisplayName", $ns)
    if ($null -ne $publisherDisplayNameNode) {
        $publisherDisplayName = $publisherDisplayNameNode.InnerText
    }
}

if ([string]::IsNullOrWhiteSpace($identityName)) {
    $errors.Add("Identity Name is missing.")
}
if ([string]::IsNullOrWhiteSpace($identityPublisher)) {
    $errors.Add("Identity Publisher is missing.")
}
if ([string]::IsNullOrWhiteSpace($identityVersion)) {
    $errors.Add("Identity Version is missing.")
}
elseif ($identityVersion -notmatch "^\d+\.\d+\.\d+\.\d+$") {
    $errors.Add("Identity Version must be in major.minor.build.revision format (for example: 1.2.3.0).")
}
if ([string]::IsNullOrWhiteSpace($publisherDisplayName)) {
    $errors.Add("PublisherDisplayName is missing.")
}

if ($Strict) {
    if (-not [string]::IsNullOrWhiteSpace($ExpectedIdentityName) -and $identityName -ne $ExpectedIdentityName) {
        $errors.Add("Identity Name '$identityName' does not match expected '$ExpectedIdentityName'.")
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPublisher) -and $identityPublisher -ne $ExpectedPublisher) {
        $errors.Add("Identity Publisher '$identityPublisher' does not match expected '$ExpectedPublisher'.")
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPublisherDisplayName) -and $publisherDisplayName -ne $ExpectedPublisherDisplayName) {
        $errors.Add("PublisherDisplayName '$publisherDisplayName' does not match expected '$ExpectedPublisherDisplayName'.")
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPackageVersion) -and $identityVersion -ne $ExpectedPackageVersion) {
        $errors.Add("Identity Version '$identityVersion' does not match expected '$ExpectedPackageVersion'.")
    }
}
else {
    if ($identityName -eq "com.hoptracer.app") {
        $warnings.Add("Identity Name is default ('com.hoptracer.app'). For Store submission, use Partner Center reserved identity.")
    }
    if ($identityPublisher -eq "CN=HopTracer") {
        $warnings.Add("Identity Publisher is default ('CN=HopTracer'). For Store submission, it must match signing certificate subject.")
    }
    if ($publisherDisplayName -eq "HopTracer") {
        $warnings.Add("PublisherDisplayName is default ('HopTracer'). For Store submission, use the Partner Center publisher display name.")
    }
}

$runFullTrust = $manifest.SelectSingleNode("/f:Package/f:Capabilities/rescap:Capability[@Name='runFullTrust']", $ns)
if ($null -ne $runFullTrust) {
    $warnings.Add("Manifest declares restricted capability 'runFullTrust'. Ensure Partner Center submission includes explicit justification.")
}

$gitFiles = Get-GitOutput -Arguments @("ls-files")
if (-not $gitFiles.Available) {
    $warnings.Add("Git is not available; skipped tracked-file secret scan.")
}
elseif ($gitFiles.ExitCode -ne 0) {
    $warnings.Add("git ls-files failed; skipped tracked-file secret scan.")
}
else {
    $trackedFiles = @($gitFiles.Output)

    $trackedSecretFiles = @(
        $trackedFiles | Where-Object {
            $_ -match '(?i)(^|/)\.env($|\.)' -or
            $_ -match '(?i)\.(pfx|p12|pem|key|snk)$' -or
            $_ -match '(?i)(^|/)id_(rsa|ed25519)(\.pub)?$'
        }
    )
    foreach ($file in $trackedSecretFiles) {
        $errors.Add("Tracked secret-bearing file detected: $file")
    }

    $trackedConfigCandidates = @(
        $trackedFiles | Where-Object { $_ -match '(?i)(^|/)appsettings\.[^.]+\.json$' }
    )
    foreach ($file in $trackedConfigCandidates) {
        $warnings.Add("Tracked environment-specific config file detected: $file")
    }

    $highSignalSecretPattern = '(-----BEGIN [A-Z ]*PRIVATE KEY-----|AccountKey=|SharedAccessSignature=|AKIA[0-9A-Z]{16}|ghp_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{20,}|Bearer [A-Za-z0-9._-]{20,}|x-api-key\s*[:=]\s*[''""]?[A-Za-z0-9._-]{16,}|client_secret\s*[:=]\s*[''""]?[A-Za-z0-9._-]{16,}|access_token\s*[:=]\s*[''""]?[A-Za-z0-9._-]{16,})'
    $selfRelPath = "scripts/check_store_readiness.ps1"
    $secretHits = Get-GitOutput -Arguments @("grep", "-nI", "-E", $highSignalSecretPattern, "--", ".", ":(exclude)$selfRelPath")
    if ($secretHits.Available -and $secretHits.ExitCode -eq 0) {
        foreach ($hit in $secretHits.Output) {
            $errors.Add("High-signal secret pattern detected in tracked content: $hit")
        }
    }
    elseif ($secretHits.Available -and $secretHits.ExitCode -gt 1) {
        $warnings.Add("git grep failed while scanning tracked content for secrets.")
    }
}

$localCertArtifacts = @(Get-ChildItem -Path $rootDir -Force -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -match '(?i)\.(pfx|p12|pem|key)$' -or $_.Name -match '(?i)^_tmp_.*\.cer$'
})
if ($localCertArtifacts.Count -gt 0) {
    $artifactNames = ($localCertArtifacts | ForEach-Object { $_.Name }) -join ", "
    $warnings.Add("Local certificate/key files exist in the repo root: $artifactNames")
}

$localStateDirs = @(".appdata", ".localappdata", ".dotnet-home") | Where-Object {
    Test-Path (Join-Path $rootDir $_)
}
if ($localStateDirs.Count -gt 0) {
    $warnings.Add("Local machine state directories exist in the repo root: $($localStateDirs -join ', ')")
}

# GH_IO.dll availability check (non-blocking for Store readiness)
$ghIoCandidatePaths = @(
    (Join-Path $rootDir "Source\HopTracer.Web\tools\GH_IO.dll"),
    (Join-Path $rootDir "Release\HopTracer_Portable\GH_IO.dll"),
    (Join-Path $rootDir "Release\HopTracer_Portable\tools\GH_IO.dll"),
    "C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll",
    "C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll",
    "C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll"
)
$resolvedGhIoPath = $ghIoCandidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($resolvedGhIoPath)) {
    $warnings.Add(
        "GH_IO.dll was not detected in the supported local or Rhino paths. " +
        ".gh conversion and some cluster archive decoding will be unavailable until you run .\scripts\setup_dependencies.ps1 or install Rhino 8/7/6."
    )
}

Write-Host "=== Store Readiness Check ===" -ForegroundColor Cyan
Write-Host "Manifest: $manifestPath"
Write-Host "Identity Name: $identityName"
Write-Host "Identity Publisher: $identityPublisher"
Write-Host "Publisher Display Name: $publisherDisplayName"
Write-Host "Identity Version: $identityVersion"
Write-Host "Strict mode: $Strict"
$ghIoPathDisplay = if ([string]::IsNullOrWhiteSpace($resolvedGhIoPath)) { "not detected" } else { $resolvedGhIoPath }
Write-Host "GH_IO path: $ghIoPathDisplay"

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "Warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host " - $warning" -ForegroundColor Yellow
    }
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Errors:" -ForegroundColor Red
    foreach ($error in $errors) {
        Write-Host " - $error" -ForegroundColor Red
    }
    throw "Store readiness checks failed."
}

Write-Host ""
Write-Host "Store readiness check passed." -ForegroundColor Green
$global:LASTEXITCODE = 0
