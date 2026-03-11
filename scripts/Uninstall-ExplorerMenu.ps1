$ErrorActionPreference = "Stop"

$menuKey = "HKCU:\Software\Classes\SystemFileAssociations\.gh\shell\HopTracer.ConvertToGhx"

if (Test-Path $menuKey) {
    Remove-Item -Path $menuKey -Recurse -Force
    Write-Host "Removed Explorer context-menu entry for .gh files." -ForegroundColor Green
}
else {
    Write-Host "Explorer context-menu entry was not present." -ForegroundColor Yellow
}
