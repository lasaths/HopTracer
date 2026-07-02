# Build and Release Guide

This document covers local builds, GitHub release artifacts, and handoff to Microsoft Store packaging.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2022 (or Build Tools with MAUI workload)
- Windows 10/11
- Optional: Rhino 7/8 for enhanced cluster archive decoding (`GH_IO.dll`)

## Portable Build (GitHub Release)

### Quick build

```powershell
.\scripts\check_store_readiness.ps1
.\scripts\build.ps1
```

Options:

- `-SkipClean` keeps previous build outputs.
- `-SkipTests` skips unit tests.

### What `build.ps1` does

1. Cleans `bin/obj` and `Release/` (unless skipped)
2. Restores solution dependencies
3. Runs unit tests
4. Builds release configuration
5. Publishes a self-contained Windows package
6. Creates `Release\HopTracer-Windows-x64.zip`

Output:

- `Release\HopTracer_Portable\`
- `Release\HopTracer_Portable\tools\hoptracer\hoptracer.exe` (CLI for diff reports)
- `Release\HopTracer-Windows-x64.zip`

The app is intentionally multi-file (`PublishSingleFile=false`) because MAUI/WindowsAppSDK needs companion runtime files.

## CI and Tag Release

Workflow: `.github/workflows/build.yml`

- Pull requests/main pushes run the shared readiness gate, then `scripts/build.ps1`, then upload the ZIP artifact.
- Tag pushes matching `v*` also create a GitHub Release with the ZIP artifact.

Suggested release flow:

```powershell
.\scripts\check_store_readiness.ps1
.\scripts\build.ps1
git add .
git commit -m "chore: release prep for v1.0.0"
git tag v1.0.0
git push origin main
git push origin v1.0.0
```

If `v1.0.0` already exists publicly, replace the tag/release only intentionally and only after validating the exact commit you plan to publish.

## Microsoft Store Packaging

Build MSIX artifacts:

```powershell
.\scripts\build_msix.ps1 `
  -IdentityName "com.hoptracer.app" `
  -Publisher "CN=HopTracer"
```

Signed package example:

```powershell
.\scripts\build_msix.ps1 `
  -RequireStoreReadiness `
  -IdentityName "com.hoptracer.app" `
  -Publisher "CN=HopTracer" `
  -PackageVersion "1.0.0.1" `
  -CertificatePath "C:\path\store-signing-cert.pfx" `
  -CertificatePassword "..."
```

For the `1.0.0` reissue, keep the display version at `1.0.0`. Use package version `1.0.0.0` only if that exact Store revision has never been uploaded; otherwise use the next available `1.0.0.x` revision.

Output directory:

- `Release\MSIX\` (contains `.msix` and, when generated, `.msixupload`)

Use `.msixupload` for Microsoft Store submissions when available.

For full submission checklist and CI-based signed build setup, see `docs/MICROSOFT_STORE.md`.

## Troubleshooting

- Build fails with target errors: confirm `.NET 10` (`dotnet --version`), then clean + restore.
- Readiness fails in strict mode: confirm the checked-in manifest identity matches the Partner Center reservation and that you passed `-IdentityName`, `-Publisher`, and certificate inputs.
- App fails to launch: ensure the entire `HopTracer_Portable` directory is intact.
- Missing cluster archive decode: run `.\scripts\setup_dependencies.ps1` to copy `GH_IO.dll` (optional enhancement).
