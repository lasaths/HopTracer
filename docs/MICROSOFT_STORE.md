# Microsoft Store Packaging Guide

This guide documents how to produce Store-ready packages for HopTracer.

## 1. Prepare Identity

Before submission, align package identity with your Partner Center app reservation.

Files to verify:

- `Source/HopTracer/Platforms/Windows/Package.appxmanifest`
- `Source/HopTracer/HopTracer.csproj`

Required alignment:

- `Identity Name` must match the reserved Store identity.
- `Identity Publisher` must match the certificate subject used for signing.
- Display version stays `1.0.0` for the reissue; increment only the fourth package-version component for repeat Store submissions (`1.0.0.1`, `1.0.0.2`, ...).

Validate the checked-in metadata before building:

```powershell
.\scripts\check_store_readiness.ps1 `
  -Strict `
  -ExpectedIdentityName "com.hoptracer.app" `
  -ExpectedPublisher "CN=HopTracer" `
  -ExpectedPackageVersion "1.0.0.1" `
  -CertificatePath "C:\path\store-signing-cert.pfx" `
  -CertificatePassword "..."
```

## 2. Build MSIX Locally

Unsigned package:

```powershell
.\scripts\build_msix.ps1 `
  -IdentityName "com.hoptracer.app" `
  -Publisher "CN=HopTracer"
```

Signed package:

```powershell
.\scripts\build_msix.ps1 `
  -RequireStoreReadiness `
  -IdentityName "com.hoptracer.app" `
  -Publisher "CN=HopTracer" `
  -PackageVersion "1.0.0.1" `
  -CertificatePath "C:\path\store-signing-cert.pfx" `
  -CertificatePassword "..."
```

Artifacts are written under:

- `Release\MSIX\**\*.msix`
- `Release\MSIX\**\*.msixupload` (preferred for Store submission when present)

## 3. Validate Signature (Signed Builds)

```powershell
Get-AuthenticodeSignature "path\to\HopTracer.msix"
```

Expected: `Status` should be `Valid`.

If you see `mspdbcmf.exe could not be found` during packaging, install Visual Studio C++ build tools / Windows SDK components. The script still emits `.msix`; symbol packaging may be skipped.

## 4. Build via GitHub Actions

Workflow: `.github/workflows/store-msix.yml` (manual trigger)

Inputs:

- `identity_name` (required)
- `version` (optional)
- `package_version` (optional, `major.minor.patch.revision`)
- `publisher` (required)
- `sign_package` (`true` or `false`)

Secrets for signed packages:

- `MSIX_CERT_BASE64` (PFX file encoded to base64)
- `MSIX_CERT_PASSWORD`

Example to create `MSIX_CERT_BASE64`:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\store-signing-cert.pfx"))
```

## 5. Submission Checklist

- [ ] `.\scripts\check_store_readiness.ps1 -Strict ...` passes with the exact identity/publisher/signing inputs used for the build.
- [ ] Package identity matches Partner Center reservation.
- [ ] Display version remains `1.0.0`, with a new `1.0.0.x` package revision if Store already consumed `1.0.0.0`.
- [ ] Package signed with correct publisher certificate.
- [ ] App launches on clean Windows 10/11 test machine.
- [ ] Core flows pass (file pick, compare, diff render, Git history).
- [ ] Upload `.msixupload` (or `.msix` if required by current portal flow).
