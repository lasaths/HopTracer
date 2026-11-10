# Prepare a Microsoft Store submission packet for HopTracer.
# Validates metadata, locates MSIX artifacts, and prints Partner Center copy-paste fields.

param(
    [string]$PackageVersion = "1.4.0.0",
    [string]$DisplayVersion = "1.4.0",
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

# Policy 10.2.4.1: name non-integrated deps in the first two description lines.
$storeDescription = @"
Requires .NET and Rhino 7 or 8.
HopTracer diffs Grasshopper .gh/.ghx files; Rhino supplies GH_IO for .gh conversion.

Compare definitions visually, see component git blame and History (past commit vs current file), export forensic reports, and run hoptracer from a terminal or CI. Works offline; no account required.
"@.Trim()

$releaseNotes = @"
HopTracer $DisplayVersion

- Component git blame (CLI + desktop, loads in the background)
- History (Time Machine): compare a past commit against the current file
- Export forensic reports to a chosen folder
- Clearer risk bands, resizable sidebars, and History dialog polish
- hoptracer CLI bundled in the package (doctor, blame, compare, report)
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
Write-Host "Store listing description (policy 10.2.4.1 — deps in first two lines):" -ForegroundColor Yellow
Write-Host $storeDescription
Write-Host ""
Write-Host "What's new / Release notes:" -ForegroundColor Yellow
Write-Host $releaseNotes
Write-Host ""
Write-Host "runFullTrust capability justification:" -ForegroundColor Yellow
Write-Host $runFullTrustJustification
Write-Host ""
Write-Host "=== Submission steps ===" -ForegroundColor Cyan
Write-Host "1. Partner Center -> HopTracer -> Store listings -> Description"
Write-Host "2. Paste the Store listing description above (keep the first two lines)"
Write-Host "3. Packages -> keep existing package (or re-upload if required)"
Write-Host "4. Paste privacy URL, release notes, and runFullTrust justification when prompted"
Write-Host "5. Submit for certification"
Write-Host ""
Write-Host "Store submission prep complete." -ForegroundColor Green
