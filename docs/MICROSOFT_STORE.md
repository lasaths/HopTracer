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
- Version must be incremented for each submission.

## 2. Build MSIX Locally

Unsigned package:

```powershell
.\scripts\build_msix.ps1
```

Signed package:

```powershell
.\scripts\build_msix.ps1 `
  -PackageVersion "1.2.3.0" `
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

- `version` (optional)
- `package_version` (optional, `major.minor.patch.revision`)
- `publisher` (optional)
- `sign_package` (`true` or `false`)

Secrets for signed packages:

- `MSIX_CERT_BASE64` (PFX file encoded to base64)
- `MSIX_CERT_PASSWORD`

Example to create `MSIX_CERT_BASE64`:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\store-signing-cert.pfx"))
```

## 5. Submission Checklist

- [ ] Package identity matches Partner Center reservation.
- [ ] Version incremented.
- [ ] Package signed with correct publisher certificate.
- [ ] App launches on clean Windows 10/11 test machine.
- [ ] Core flows pass (file pick, compare, diff render, Git history).
- [ ] Upload `.msixupload` (or `.msix` if required by current portal flow).
