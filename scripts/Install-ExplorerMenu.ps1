param(
    [string]$HopTracerExePath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($HopTracerExePath)) {
    $HopTracerExePath = Join-Path $PSScriptRoot "HopTracer.exe"
}

if (-not (Test-Path $HopTracerExePath)) {
    throw "HopTracer.exe was not found at '$HopTracerExePath'."
}

$resolvedExe = (Resolve-Path $HopTracerExePath).Path
$menuKey = "HKCU:\Software\Classes\SystemFileAssociations\.gh\shell\HopTracer.ConvertToGhx"
$commandKey = Join-Path $menuKey "command"
$commandValue = ('"{0}" --convert "%1"' -f $resolvedExe)

New-Item -Path $menuKey -Force | Out-Null
New-Item -Path $commandKey -Force | Out-Null

Set-Item -Path $menuKey -Value "Convert to GHX (HopTracer)"
Set-Item -Path $commandKey -Value $commandValue

New-ItemProperty -Path $menuKey -Name "MUIVerb" -Value "Convert to GHX (HopTracer)" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $menuKey -Name "Icon" -Value ('"{0}",0' -f $resolvedExe) -PropertyType String -Force | Out-Null
New-ItemProperty -Path $menuKey -Name "MultiSelectModel" -Value "Player" -PropertyType String -Force | Out-Null

Write-Host "Installed Explorer context-menu entry for .gh files." -ForegroundColor Green
Write-Host "Registry key: $menuKey" -ForegroundColor Cyan
Write-Host "Command: $commandValue" -ForegroundColor Cyan
