# Microsoft Store Packaging Guide

HopTracer is reserved in Partner Center as **`lasaths.HopTracer`**.

## Quick submit (one command)

After CI builds the MSIX artifact (or a local `build_msix.ps1` run):

```powershell
.\scripts\prepare_store_submission.ps1
```

This validates manifest metadata, locates the upload package, and prints every Partner Center field you need to paste.

## Identity (already aligned)

| Field | Value |
|-------|-------|
| Identity Name | `lasaths.HopTracer` |
| Publisher | `CN=AFE48087-3FFA-435C-A8A2-1776FA3FFA25` |
| Publisher display name | `lasaths` |
| Package version | `1.2.0.0` (Store requires revision `0`; use `1.2.1.0` for the next submission) |
| Privacy policy URL | https://github.com/lasaths/HopTracer/blob/main/docs/privacy.md |
| Support URL | https://github.com/lasaths/HopTracer/issues |

Files:

- `Source/HopTracer/Platforms/Windows/Package.appxmanifest`
- `Source/HopTracer/HopTracer.csproj`

## Build MSIX

### GitHub Actions (recommended)

Workflow: **Build Store MSIX** (manual trigger)

Defaults are pre-filled for `lasaths.HopTracer`. Leave `package_version` at `1.2.0.0` unless you are submitting a revision bump.

1. Actions → **Build Store MSIX** → Run workflow
2. Download artifact **HopTracer-MSIX**
3. Run `.\scripts\prepare_store_submission.ps1 -MsixDir <extracted-folder>`
4. Upload the `.msixupload` (or `.msix`) file in Partner Center

The workflow can also attach the package to the matching GitHub release when `attach_to_release` is enabled.

### Local build

Requires Visual Studio with **Desktop development with C++** (shell extension).

```powershell
.\scripts\build_msix.ps1 `
  -RequireStoreReadiness `
  -PackageVersion "1.2.0.0" `
  -Version "1.2.0"
```

Optional: copy `GH_IO.dll` for `.gh` conversion in the CLI:

```powershell
.\scripts\setup_dependencies.ps1
```

Artifacts:

- `Release\MSIX\**\*.msixupload` (preferred)
- `Release\MSIX\**\*.msix`

## Validate before upload

```powershell
.\scripts\check_store_readiness.ps1 -Strict
```

## Partner Center submission

1. Open [Partner Center](https://partner.microsoft.com/dashboard) → **HopTracer** → **Packages**
2. **Upload new package** → select the `.msixupload` from the CI artifact
3. **Store listings** → ensure privacy URL and support URL are set (see table above)
4. **What's new** — use the release notes printed by `prepare_store_submission.ps1`
5. **Capabilities** → when asked about `runFullTrust`, paste the justification from `prepare_store_submission.ps1`
6. Submit for certification

### runFullTrust justification (copy/paste)

HopTracer registers a COM shell extension and Explorer context menu for `.gh` files. The extension converts Grasshopper binary definitions to `.ghx` for local diffing. This requires `runFullTrust` because Explorer shell extensions and COM surrogate servers cannot run inside a strict sandboxed UWP container.

### What's new for 1.2.0

- `hoptracer` CLI for terminal and CI diff workflows
- Agent-oriented diff JSON with resolved wire labels for AI review
- `hoptracer.exe` on PATH after Store install (App Execution Alias)
- Forensic report export and baseline compare in the desktop app

## Signed packages (optional)

If you sign locally instead of using Microsoft Store signing:

```powershell
.\scripts\build_msix.ps1 `
  -RequireStoreReadiness `
  -PackageVersion "1.2.0.0" `
  -CertificatePath "C:\path\store-signing-cert.pfx" `
  -CertificatePassword "..."
```

GitHub secrets for CI signing: `MSIX_CERT_BASE64`, `MSIX_CERT_PASSWORD`

## Revision bumps

Microsoft Store rejects duplicate package versions. For each new submission without a marketing version change, increment only the fourth component:

Microsoft Store rejects non-zero revision numbers in the manifest (`1.2.0.0` is valid; `1.2.0.1` is not). For a new submission under the same display version, bump the **build** component instead (`1.2.1.0`).

| Submission | Package version | Display version |
|------------|-----------------|-----------------|
| 1.2.0 first upload | `1.2.0.0` | `1.2.0` |
| 1.2.0 resubmit (headless fix) | `1.2.0.0` | `1.2.0` |
| 1.2.1 release | `1.2.1.0` | `1.2.1` |

Update `Package.appxmanifest`, `HopTracer.csproj` (`ApplicationDisplayVersion` / `ApplicationVersion`), `CHANGELOG.md`, and workflow inputs together.

## Checklist

- [x] Logo and splash assets in manifest
- [x] Identity matches Partner Center reservation
- [x] Privacy policy published at public URL
- [x] `hoptracer` CLI bundled with console App Execution Alias
- [x] MSIX build workflow and strict readiness script
- [ ] Upload package in Partner Center
- [ ] Submit for certification
- [ ] Smoke test on clean Windows 10/11 VM after Store publish
