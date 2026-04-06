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

### ✅ Already done
- [x] Logo assets present (`hoptrace_logo.png`, `hoptrace_logo_noshadow.png`)
- [x] `Package.appxmanifest` structure valid
- [x] Version `1.1.1.0` set in manifest and `.csproj`
- [x] Privacy policy and AI disclosure in `docs/privacy.md` and `README.md`
- [x] `store-msix.yml` workflow ready for manual trigger
- [x] `check_store_readiness.ps1` passes (non-strict)
- [x] Build and all tests pass

### ⚠️ Required before submitting

- [ ] **Align identity with Partner Center reservation.**
  Run strict validation with your actual Partner Center identity:
  ```powershell
  .\scripts\check_store_readiness.ps1 -Strict `
    -ExpectedIdentityName "YOUR_PARTNER_CENTER_NAME" `
    -ExpectedPublisher "CN=YOUR_PUBLISHER" `
    -ExpectedPackageVersion "1.1.1.0"
  ```
- [ ] **Configure GitHub secrets** (only needed if signing locally/in CI):
  - `MSIX_CERT_BASE64` — PFX base64: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))`
  - `MSIX_CERT_PASSWORD`
  - _Note: if using Microsoft's Store signing flow, these are not required._
- [ ] **Partner Center `runFullTrust` justification.** The manifest declares this restricted capability. During submission, provide justification: the app uses a COM shell extension to register a `.gh` right-click context menu, which requires full trust.
- [ ] **Trigger `store-msix.yml`** via Actions → Build Store MSIX with your Partner Center identity and publisher values.
- [ ] **Test on a clean Windows 10/11 machine** — launch, file pick, compare, diff render, Git history.
- [ ] **Upload `.msixupload`** artifact from the Actions run to Partner Center.
