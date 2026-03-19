# Shared release preflight for portable ZIP and MSIX packaging.

param(
    [switch]$Strict = $false,
    [string]$ExpectedIdentityName = "",
    [string]$ExpectedPublisher = "",
    [string]$ExpectedDisplayVersion = "",
    [string]$ExpectedPackageVersion = "",
    [switch]$RequireSigning = $false,
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"

$rootDir = $PSScriptRoot | Split-Path -Parent
$appProjectDir = Join-Path $rootDir "Source\HopTracer"
$csprojPath = Join-Path $appProjectDir "HopTracer.csproj"
$manifestPath = Join-Path $appProjectDir "Platforms\Windows\Package.appxmanifest"
$shellExtensionProject = Join-Path $rootDir "Source\HopTracer.ShellExtension\HopTracer.ShellExtension.vcxproj"
$buildWorkflowPath = Join-Path $rootDir ".github\workflows\build.yml"
$storeWorkflowPath = Join-Path $rootDir ".github\workflows\store-msix.yml"
$readmePath = Join-Path $rootDir "README.md"
$releaseGuidePath = Join-Path $rootDir "docs\BUILD_AND_RELEASE.md"
$storeGuidePath = Join-Path $rootDir "docs\MICROSOFT_STORE.md"

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error {
    param([string]$Message)
    $script:errors.Add($Message) | Out-Null
}

function Add-Warning {
    param([string]$Message)
    $script:warnings.Add($Message) | Out-Null
}

function Test-VersionFormat {
    param(
        [string]$Value,
        [ValidateSet("Display", "Package")] [string]$Kind
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    switch ($Kind) {
        "Display" { return $Value -match '^\d+\.\d+\.\d+$' }
        "Package" { return $Value -match '^\d+\.\d+\.\d+\.\d+$' }
    }

    return $false
}

function Get-VersionPrefix {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $parts = $Value.Split(".")
    if ($parts.Length -lt 3) {
        return ""
    }

    return ($parts[0..2] -join ".")
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Error("$Description not found: $Path")
        return $false
    }

    return $true
}

function Get-ProjectPropertyValue {
    param(
        [xml]$ProjectXml,
        [string]$PropertyName
    )

    $propertyGroups = @($ProjectXml.Project.PropertyGroup)
    foreach ($group in $propertyGroups) {
        $value = $group.$PropertyName
        if ($null -eq $value) {
            continue
        }

        $first = @($value | Where-Object { $null -ne $_ }) | Select-Object -First 1
        if ($null -ne $first) {
            return [string]$first
        }
    }

    return ""
}

function Add-RelativePathIfPresent {
    param(
        [System.Collections.Generic.HashSet[string]]$Paths,
        [string]$PathValue
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return
    }

    $trimmed = $PathValue.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return
    }

    $normalized = $trimmed -replace '/', '\'
    $paths.Add($normalized) | Out-Null
}

Write-Host "=== HopTracer Release Readiness ===" -ForegroundColor Cyan
Write-Host ""

$requiredFiles = @(
    @{ Path = $csprojPath; Description = "MAUI app project" },
    @{ Path = $manifestPath; Description = "Windows package manifest" },
    @{ Path = $shellExtensionProject; Description = "Explorer shell extension project" },
    @{ Path = $buildWorkflowPath; Description = "GitHub build workflow" },
    @{ Path = $storeWorkflowPath; Description = "GitHub Store workflow" },
    @{ Path = $readmePath; Description = "README" },
    @{ Path = $releaseGuidePath; Description = "Build/release guide" },
    @{ Path = $storeGuidePath; Description = "Store packaging guide" }
)

foreach ($item in $requiredFiles) {
    Assert-FileExists -Path $item.Path -Description $item.Description | Out-Null
}

$projectXml = $null
$manifestXml = $null
$manifestNamespace = $null

if (Test-Path -LiteralPath $csprojPath) {
    try {
        [xml]$projectXml = Get-Content -LiteralPath $csprojPath -Raw
    }
    catch {
        Add-Error("Unable to parse project file XML: $csprojPath")
    }
}

if (Test-Path -LiteralPath $manifestPath) {
    try {
        [xml]$manifestXml = Get-Content -LiteralPath $manifestPath -Raw
        $manifestNamespace = [System.Xml.XmlNamespaceManager]::new($manifestXml.NameTable)
        $manifestNamespace.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
        $manifestNamespace.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10")
    }
    catch {
        Add-Error("Unable to parse Windows package manifest XML: $manifestPath")
    }
}

$applicationId = ""
$displayVersion = ""
$applicationVersion = ""
$applicationIcon = ""

if ($null -ne $projectXml) {
    $applicationId = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "ApplicationId"
    $displayVersion = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "ApplicationDisplayVersion"
    $applicationVersion = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "ApplicationVersion"
    $applicationIcon = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "ApplicationIcon"

    if ([string]::IsNullOrWhiteSpace($applicationId)) {
        Add-Error("HopTracer.csproj is missing <ApplicationId>.")
    }

    if ([string]::IsNullOrWhiteSpace($displayVersion)) {
        Add-Error("HopTracer.csproj is missing <ApplicationDisplayVersion>.")
    }
    elseif (-not (Test-VersionFormat -Value $displayVersion -Kind Display)) {
        Add-Error("HopTracer.csproj ApplicationDisplayVersion must use major.minor.patch format. Current value: $displayVersion")
    }

    if ([string]::IsNullOrWhiteSpace($applicationVersion)) {
        Add-Error("HopTracer.csproj is missing <ApplicationVersion>.")
    }
    elseif ($applicationVersion -notmatch '^\d+$') {
        Add-Error("HopTracer.csproj ApplicationVersion must be an integer. Current value: $applicationVersion")
    }

    if ([string]::IsNullOrWhiteSpace($applicationIcon)) {
        Add-Error("HopTracer.csproj is missing <ApplicationIcon>.")
    }
}

$manifestIdentityName = ""
$manifestPublisher = ""
$manifestPackageVersion = ""
$manifestLogo = ""
$manifestDisplayName = ""
$visualElementsNode = $null
$defaultTileNode = $null

if ($null -ne $manifestXml -and $null -ne $manifestNamespace) {
    $identityNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Identity", $manifestNamespace)
    if ($null -eq $identityNode) {
        Add-Error("Package.appxmanifest is missing the <Identity> element.")
    }
    else {
        $manifestIdentityName = [string]$identityNode.Attributes["Name"].Value
        $manifestPublisher = [string]$identityNode.Attributes["Publisher"].Value
        $manifestPackageVersion = [string]$identityNode.Attributes["Version"].Value
    }

    $propertiesNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Properties", $manifestNamespace)
    if ($null -eq $propertiesNode) {
        Add-Error("Package.appxmanifest is missing the <Properties> section.")
    }
    else {
        $displayNameNode = $propertiesNode.SelectSingleNode("appx:DisplayName", $manifestNamespace)
        $logoNode = $propertiesNode.SelectSingleNode("appx:Logo", $manifestNamespace)

        $manifestDisplayName = if ($null -ne $displayNameNode) { [string]$displayNameNode.InnerText } else { "" }
        $manifestLogo = if ($null -ne $logoNode) { [string]$logoNode.InnerText } else { "" }

        if ([string]::IsNullOrWhiteSpace($manifestDisplayName)) {
            Add-Error("Package.appxmanifest is missing <Properties><DisplayName>.")
        }

        if ([string]::IsNullOrWhiteSpace($manifestLogo)) {
            Add-Error("Package.appxmanifest is missing <Properties><Logo>.")
        }
    }

    if ([string]::IsNullOrWhiteSpace($manifestIdentityName)) {
        Add-Error("Package.appxmanifest identity name is missing.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestPublisher)) {
        Add-Error("Package.appxmanifest publisher is missing.")
    }

    if ([string]::IsNullOrWhiteSpace($manifestPackageVersion)) {
        Add-Error("Package.appxmanifest package version is missing.")
    }
    elseif (-not (Test-VersionFormat -Value $manifestPackageVersion -Kind Package)) {
        Add-Error("Package.appxmanifest identity version must use major.minor.patch.revision format. Current value: $manifestPackageVersion")
    }

    $visualElementsNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Applications/appx:Application/uap:VisualElements", $manifestNamespace)
    if ($null -eq $visualElementsNode) {
        Add-Error("Package.appxmanifest is missing <uap:VisualElements>.")
    }

    if ($null -ne $visualElementsNode) {
        $defaultTileNode = $visualElementsNode.SelectSingleNode("uap:DefaultTile", $manifestNamespace)
        if ($null -eq $defaultTileNode) {
            Add-Error("Package.appxmanifest is missing <uap:DefaultTile>.")
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($displayVersion) -and -not [string]::IsNullOrWhiteSpace($manifestPackageVersion)) {
    if ((Get-VersionPrefix -Value $manifestPackageVersion) -ne $displayVersion) {
        Add-Error("Manifest package version prefix ($manifestPackageVersion) does not align with ApplicationDisplayVersion ($displayVersion).")
    }
}

$assetRelativePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
Add-RelativePathIfPresent -Paths $assetRelativePaths -PathValue $applicationIcon
Add-RelativePathIfPresent -Paths $assetRelativePaths -PathValue $manifestLogo

if ($null -ne $visualElementsNode) {
    foreach ($attrName in @("Square150x150Logo", "Square44x44Logo")) {
        $attribute = $visualElementsNode.Attributes[$attrName]
        if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
            Add-Error("Package.appxmanifest VisualElements is missing the '$attrName' asset.")
        }
        else {
            Add-RelativePathIfPresent -Paths $assetRelativePaths -PathValue $attribute.Value
        }
    }

    $splashNode = $visualElementsNode.SelectSingleNode("uap:SplashScreen", $manifestNamespace)
    if ($null -eq $splashNode -or $null -eq $splashNode.Attributes["Image"] -or [string]::IsNullOrWhiteSpace($splashNode.Attributes["Image"].Value)) {
        Add-Error("Package.appxmanifest is missing the splash screen asset path.")
    }
    else {
        Add-RelativePathIfPresent -Paths $assetRelativePaths -PathValue $splashNode.Attributes["Image"].Value
    }
}

if ($null -ne $defaultTileNode) {
    foreach ($attrName in @("Square71x71Logo", "Wide310x150Logo", "Square310x310Logo")) {
        $attribute = $defaultTileNode.Attributes[$attrName]
        if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
            Add-Error("Package.appxmanifest DefaultTile is missing the '$attrName' asset.")
        }
        else {
            Add-RelativePathIfPresent -Paths $assetRelativePaths -PathValue $attribute.Value
        }
    }
}

foreach ($relativePath in $assetRelativePaths) {
    $absolutePath = Join-Path $appProjectDir $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath)) {
        Add-Error("Packaging asset not found: $relativePath")
    }
}

if (-not [string]::IsNullOrWhiteSpace($applicationId) -and -not [string]::IsNullOrWhiteSpace($manifestIdentityName) -and $applicationId -ne $manifestIdentityName) {
    Add-Error("HopTracer.csproj ApplicationId ($applicationId) does not match Package.appxmanifest identity name ($manifestIdentityName).")
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedDisplayVersion)) {
    if (-not (Test-VersionFormat -Value $ExpectedDisplayVersion -Kind Display)) {
        Add-Error("ExpectedDisplayVersion must use major.minor.patch format. Current value: $ExpectedDisplayVersion")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($displayVersion) -and $displayVersion -ne $ExpectedDisplayVersion) {
        Add-Error("ApplicationDisplayVersion ($displayVersion) does not match ExpectedDisplayVersion ($ExpectedDisplayVersion).")
    }
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedPackageVersion)) {
    if (-not (Test-VersionFormat -Value $ExpectedPackageVersion -Kind Package)) {
        Add-Error("ExpectedPackageVersion must use major.minor.patch.revision format. Current value: $ExpectedPackageVersion")
    }
    else {
        if (-not [string]::IsNullOrWhiteSpace($displayVersion) -and (Get-VersionPrefix -Value $ExpectedPackageVersion) -ne $displayVersion) {
            Add-Error("ExpectedPackageVersion ($ExpectedPackageVersion) does not align with ApplicationDisplayVersion ($displayVersion).")
        }

        if (-not [string]::IsNullOrWhiteSpace($manifestPackageVersion) -and (Get-VersionPrefix -Value $ExpectedPackageVersion) -ne (Get-VersionPrefix -Value $manifestPackageVersion)) {
            Add-Error("ExpectedPackageVersion ($ExpectedPackageVersion) does not align with the manifest package version prefix ($manifestPackageVersion).")
        }

        if (-not [string]::IsNullOrWhiteSpace($manifestPackageVersion) -and $manifestPackageVersion -ne $ExpectedPackageVersion) {
            Add-Warning("Manifest package version is $manifestPackageVersion; packaging can override it to $ExpectedPackageVersion for this build.")
        }
    }
}

if ($Strict -and [string]::IsNullOrWhiteSpace($ExpectedIdentityName)) {
    Add-Error("Strict readiness requires -ExpectedIdentityName.")
}

if ($Strict -and [string]::IsNullOrWhiteSpace($ExpectedPublisher)) {
    Add-Error("Strict readiness requires -ExpectedPublisher.")
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedIdentityName)) {
    if (-not [string]::IsNullOrWhiteSpace($applicationId) -and $applicationId -ne $ExpectedIdentityName) {
        Add-Error("ApplicationId ($applicationId) does not match ExpectedIdentityName ($ExpectedIdentityName).")
    }

    if (-not [string]::IsNullOrWhiteSpace($manifestIdentityName) -and $manifestIdentityName -ne $ExpectedIdentityName) {
        Add-Error("Manifest identity name ($manifestIdentityName) does not match ExpectedIdentityName ($ExpectedIdentityName).")
    }
}
elseif (($applicationId -eq "com.hoptracer.app") -or ($manifestIdentityName -eq "com.hoptracer.app")) {
    Add-Warning("Checked-in package identity still uses the repository default 'com.hoptracer.app'. Verify it matches the Partner Center reservation before a signed Store submission.")
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedPublisher)) {
    if (-not [string]::IsNullOrWhiteSpace($manifestPublisher) -and $manifestPublisher -ne $ExpectedPublisher) {
        Add-Error("Manifest publisher ($manifestPublisher) does not match ExpectedPublisher ($ExpectedPublisher).")
    }
}
elseif ($manifestPublisher -eq "CN=HopTracer") {
    Add-Warning("Checked-in package publisher still uses the repository default 'CN=HopTracer'. Verify it matches the signing certificate subject before a signed Store submission.")
}

if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    if (-not (Test-Path -LiteralPath $CertificatePath)) {
        Add-Error("CertificatePath does not exist: $CertificatePath")
    }
    elseif ([System.IO.Path]::GetExtension($CertificatePath) -notin @(".pfx", ".PFX")) {
        Add-Error("CertificatePath must point to a .pfx file. Current value: $CertificatePath")
    }
}
elseif ($RequireSigning) {
    Add-Error("RequireSigning was set, but no -CertificatePath was provided.")
}

if ($RequireSigning -and [string]::IsNullOrWhiteSpace($CertificatePassword)) {
    Add-Error("RequireSigning was set, but no -CertificatePassword was provided.")
}

$buildWorkflowText = ""
$storeWorkflowText = ""
$readmeText = ""
$releaseGuideText = ""
$storeGuideText = ""

if (Test-Path -LiteralPath $buildWorkflowPath) {
    $buildWorkflowText = Get-Content -LiteralPath $buildWorkflowPath -Raw
    if ($buildWorkflowText -notmatch [regex]::Escape("./scripts/check_store_readiness.ps1")) {
        Add-Error("build.yml does not run scripts/check_store_readiness.ps1.")
    }

    if ($buildWorkflowText -notmatch [regex]::Escape("./scripts/build.ps1")) {
        Add-Error("build.yml does not run scripts/build.ps1.")
    }

    $readinessIndex = $buildWorkflowText.IndexOf("./scripts/check_store_readiness.ps1", [System.StringComparison]::Ordinal)
    $buildIndex = $buildWorkflowText.IndexOf("./scripts/build.ps1", [System.StringComparison]::Ordinal)
    if ($readinessIndex -ge 0 -and $buildIndex -ge 0 -and $readinessIndex -gt $buildIndex) {
        Add-Error("build.yml runs scripts/build.ps1 before scripts/check_store_readiness.ps1.")
    }
}

if (Test-Path -LiteralPath $storeWorkflowPath) {
    $storeWorkflowText = Get-Content -LiteralPath $storeWorkflowPath -Raw
    foreach ($requiredToken in @(
        "identity_name:",
        "publisher:",
        "./scripts/build_msix.ps1",
        "-RequireStoreReadiness",
        "MSIX_CERT_BASE64",
        "MSIX_CERT_PASSWORD"
    )) {
        if ($storeWorkflowText -notmatch [regex]::Escape($requiredToken)) {
            Add-Error("store-msix.yml is missing expected release reference '$requiredToken'.")
        }
    }
}

if (Test-Path -LiteralPath $readmePath) {
    $readmeText = Get-Content -LiteralPath $readmePath -Raw
    foreach ($requiredToken in @(
        ".\scripts\check_store_readiness.ps1",
        ".\scripts\build_msix.ps1",
        "PackageVersion ""1.0.0.1""",
        'For a `1.0.0` reissue'
    )) {
        if ($readmeText -notmatch [regex]::Escape($requiredToken)) {
            Add-Error("README.md is missing expected release guidance '$requiredToken'.")
        }
    }
}

if (Test-Path -LiteralPath $releaseGuidePath) {
    $releaseGuideText = Get-Content -LiteralPath $releaseGuidePath -Raw
    foreach ($requiredToken in @(
        ".\scripts\check_store_readiness.ps1",
        ".\scripts\build.ps1",
        ".\scripts\build_msix.ps1",
        "git tag v1.0.0"
    )) {
        if ($releaseGuideText -notmatch [regex]::Escape($requiredToken)) {
            Add-Error("docs/BUILD_AND_RELEASE.md is missing expected release guidance '$requiredToken'.")
        }
    }
}

if (Test-Path -LiteralPath $storeGuidePath) {
    $storeGuideText = Get-Content -LiteralPath $storeGuidePath -Raw
    foreach ($requiredToken in @(
        ".\scripts\check_store_readiness.ps1",
        ".\scripts\build_msix.ps1",
        "MSIX_CERT_BASE64",
        "MSIX_CERT_PASSWORD",
        "1.0.0.1"
    )) {
        if ($storeGuideText -notmatch [regex]::Escape($requiredToken)) {
            Add-Error("docs/MICROSOFT_STORE.md is missing expected Store packaging guidance '$requiredToken'.")
        }
    }
}

Write-Host "Discovered metadata:" -ForegroundColor Yellow
if (-not [string]::IsNullOrWhiteSpace($applicationId)) {
    Write-Host "  ApplicationId: $applicationId"
}
if (-not [string]::IsNullOrWhiteSpace($displayVersion)) {
    Write-Host "  ApplicationDisplayVersion: $displayVersion"
}
if (-not [string]::IsNullOrWhiteSpace($applicationVersion)) {
    Write-Host "  ApplicationVersion: $applicationVersion"
}
if (-not [string]::IsNullOrWhiteSpace($manifestIdentityName)) {
    Write-Host "  Manifest Identity Name: $manifestIdentityName"
}
if (-not [string]::IsNullOrWhiteSpace($manifestPublisher)) {
    Write-Host "  Manifest Publisher: $manifestPublisher"
}
if (-not [string]::IsNullOrWhiteSpace($manifestPackageVersion)) {
    Write-Host "  Manifest Package Version: $manifestPackageVersion"
}
Write-Host ""

if ($warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($errors.Count -gt 0) {
    Write-Host "Errors:" -ForegroundColor Red
    foreach ($errorMessage in $errors) {
        Write-Host "  - $errorMessage" -ForegroundColor Red
    }
    Write-Host ""
    throw "Release readiness checks failed."
}

Write-Host "Release readiness checks passed." -ForegroundColor Green
