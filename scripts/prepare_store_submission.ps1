# Prepare a Microsoft Store submission packet for HopTracer.
# Validates metadata, locates MSIX artifacts, and prints Partner Center copy-paste fields.

param(
    [string]$PackageVersion = "1.2.1.0",
    [string]$DisplayVersion = "1.2.1",
    [string]$MsixDir = "",
    [switch]$Strict = $true
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot | Split-Path -Parent
$privacyUrl = "https://github.com/lasaths/HopTracer/blob/main/docs/privacy.md"
$supportUrl = "https://github.com/lasaths/HopTracer/issues"

Write-Host "=== HopTracer Store Submission Prep ===" -ForegroundColor Cyan

$readinessScript = Join-Path $rootDir "scripts\check_store_readiness.ps1"
& $readinessScript `
    -Strict:$Strict `
    -ExpectedIdentityName "lasaths.HopTracer" `
    -ExpectedPublisher "CN=AFE48087-3FFA-435C-A8A2-1776FA3FFA25" `
    -ExpectedPublisherDisplayName "lasaths" `
    -ExpectedPackageVersion $PackageVersion

if ([string]::IsNullOrWhiteSpace($MsixDir)) {
    $MsixDir = Join-Path $rootDir "Release\MSIX"
}

$uploadFiles = @(Get-ChildItem -Path $MsixDir -Recurse -File -Filter *.msixupload -ErrorAction SilentlyContinue)
$msixFiles = @(Get-ChildItem -Path $MsixDir -Recurse -File -Filter *.msix -ErrorAction SilentlyContinue)

if ($uploadFiles.Count -eq 0 -and $msixFiles.Count -eq 0) {
    Write-Host ""
    Write-Host "No MSIX artifacts found under: $MsixDir" -ForegroundColor Yellow
    Write-Host "Build one with:" -ForegroundColor Yellow
    Write-Host "  .\scripts\build_msix.ps1 -RequireStoreReadiness -PackageVersion `"$PackageVersion`" -Version `"$DisplayVersion`"" -ForegroundColor Gray
    Write-Host "Or trigger GitHub Actions -> Build Store MSIX and download the HopTracer-MSIX artifact." -ForegroundColor Gray
    exit 1
}

$preferredUpload = $uploadFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$preferredMsix = $msixFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$uploadPath = if ($null -ne $preferredUpload) { $preferredUpload.FullName } else { $preferredMsix.FullName }

Write-Host ""
Write-Host "=== Package to upload ===" -ForegroundColor Green
Write-Host $uploadPath
if ($null -ne $preferredUpload) {
    Write-Host "Type: .msixupload (preferred for Partner Center)" -ForegroundColor DarkGray
}
else {
    Write-Host "Type: .msix (unsigned; Microsoft Store will sign)" -ForegroundColor DarkGray
}

$releaseNotes = @"
HopTracer $DisplayVersion

- Review & Export panel: forensic report download, save baseline, check baseline
- Fix forensic export serialization crash
- Show changed wires only filter in diff viewer
- hoptracer CLI: doctor, baseline, and forensic report commands
- Agent skill: npx skills add lasaths/HopTracer@hoptracer
"@.Trim()

$runFullTrustJustification = @"
HopTracer registers a COM shell extension and Explorer context menu for .gh files.
The extension converts Grasshopper binary definitions to .ghx for local diffing.
This requires runFullTrust because Explorer shell extensions and COM surrogate servers
cannot run inside a strict sandboxed UWP container.
"@.Trim()

Write-Host ""
Write-Host "=== Partner Center fields (copy/paste) ===" -ForegroundColor Cyan
Write-Host "Privacy policy URL:" -ForegroundColor Yellow
Write-Host $privacyUrl
Write-Host ""
Write-Host "Support contact URL:" -ForegroundColor Yellow
Write-Host $supportUrl
Write-Host ""
Write-Host "Package version:" -ForegroundColor Yellow
Write-Host $PackageVersion
Write-Host ""
Write-Host "What's new / Release notes:" -ForegroundColor Yellow
Write-Host $releaseNotes
Write-Host ""
Write-Host "runFullTrust capability justification:" -ForegroundColor Yellow
Write-Host $runFullTrustJustification
Write-Host ""
Write-Host "=== Submission steps ===" -ForegroundColor Cyan
Write-Host "1. Partner Center -> HopTracer -> Packages -> Upload new package"
Write-Host "2. Upload the file listed above"
Write-Host "3. Paste privacy URL, release notes, and runFullTrust justification when prompted"
Write-Host "4. Submit for certification"
Write-Host ""
Write-Host "Store submission prep complete." -ForegroundColor Green
