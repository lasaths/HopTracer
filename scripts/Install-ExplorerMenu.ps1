param(
    [string]$HopTracerExePath = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$storePackage = Get-AppxPackage -Name "lasaths.HopTracer" -ErrorAction SilentlyContinue
if ($storePackage -and -not $Force) {
    $staleKey = "HKCU:\Software\Classes\SystemFileAssociations\.gh\shell\HopTracer.ConvertToGhx"
    if (Test-Path $staleKey) {
        Remove-Item -Path $staleKey -Recurse -Force
        Write-Host "Removed stale portable Explorer menu (Store HopTracer already registers Convert to GHX)." -ForegroundColor Green
    }
    else {
        Write-Host "Store HopTracer is installed and already registers the Convert to GHX Explorer menu." -ForegroundColor Green
    }
    Write-Host "Package: $($storePackage.PackageFullName)" -ForegroundColor Cyan
    Write-Host "Skip portable registry install. Pass -Force to install a portable override anyway." -ForegroundColor Yellow
    return
}

if ([string]::IsNullOrWhiteSpace($HopTracerExePath)) {
    $HopTracerExePath = Join-Path $PSScriptRoot "HopTracer.exe"
}

if (-not (Test-Path -LiteralPath $HopTracerExePath)) {
    throw "HopTracer.exe was not found at '$HopTracerExePath'."
}

$resolvedExe = (Resolve-Path -LiteralPath $HopTracerExePath).Path
if (-not $resolvedExe.EndsWith("HopTracer.exe", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Expected a HopTracer.exe path, got '$resolvedExe'."
}

$menuKey = "HKCU:\Software\Classes\SystemFileAssociations\.gh\shell\HopTracer.ConvertToGhx"
$commandKey = Join-Path $menuKey "command"
$commandValue = ('"{0}" --convert "%1"' -f $resolvedExe)

New-Item -Path $menuKey -Force | Out-Null
New-Item -Path $commandKey -Force | Out-Null

Set-Item -Path $menuKey -Value "Convert to GHX (HopTracer)"
Set-Item -Path $commandKey -Value $commandValue

$exeDir = Split-Path -Parent $resolvedExe
$iconCandidates = @(
    (Join-Path $exeDir "hoptrace_logo.ico"),
    (Join-Path $exeDir "HopTracer.ico"),
    $resolvedExe
)
$iconPath = $iconCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$iconValue = if ($iconPath.EndsWith(".ico", [System.StringComparison]::OrdinalIgnoreCase)) {
    $iconPath
} else {
    ('"{0}",0' -f $iconPath)
}

New-ItemProperty -Path $menuKey -Name "MUIVerb" -Value "Convert to GHX (HopTracer)" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $menuKey -Name "Icon" -Value $iconValue -PropertyType String -Force | Out-Null
New-ItemProperty -Path $menuKey -Name "MultiSelectModel" -Value "Player" -PropertyType String -Force | Out-Null

Write-Host "Installed Explorer context-menu entry for .gh files." -ForegroundColor Green
Write-Host "Registry key: $menuKey" -ForegroundColor Cyan
Write-Host "Command: $commandValue" -ForegroundColor Cyan
Write-Host "If you move or delete this HopTracer folder, re-run this script or Uninstall-ExplorerMenu.ps1." -ForegroundColor Yellow
