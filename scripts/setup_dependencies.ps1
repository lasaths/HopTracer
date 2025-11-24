$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$destDir = Join-Path $scriptDir "..\src_csharp\HopTracer.Web\tools"
$destPath = Join-Path $destDir "GH_IO.dll"

# Ensure destination directory exists
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

# List of paths to check in order of preference (Newest Rhino first)
$pathsToCheck = @(
    "C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll",
    "C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll",
    "C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll"
)

$found = $false

foreach ($path in $pathsToCheck) {
    if (Test-Path $path) {
        Write-Host "Found GH_IO.dll at: $path"
        Copy-Item -Path $path -Destination $destPath -Force
        Write-Host "Successfully copied to: $destPath"
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Error "Could not find GH_IO.dll in standard Rhino installation paths."
    exit 1
}