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
- `Release\HopTracer-Windows-x64.zip`

The app is intentionally multi-file (`PublishSingleFile=false`) because MAUI/WindowsAppSDK needs companion runtime files.

## CI and Tag Release

Workflow: `.github/workflows/build.yml`

- Pull requests/main pushes run build + tests and upload artifact.
- Tag pushes matching `v*` also create a GitHub Release with the ZIP artifact.

Suggested release flow:

```powershell
.\scripts\build.ps1
git add .
git commit -m "Release v1.0.0"
git tag v1.0.0
git push origin main
git push origin v1.0.0
```

## Microsoft Store Packaging

Build MSIX artifacts:

```powershell
.\scripts\build_msix.ps1
```

Signed package example:

```powershell
.\scripts\build_msix.ps1 `
  -CertificatePath "C:\path\store-signing-cert.pfx" `
  -CertificatePassword "..."
```

Output directory:

- `Release\MSIX\` (contains `.msix` and, when generated, `.msixupload`)

Use `.msixupload` for Microsoft Store submissions when available.

For full submission checklist and CI-based signed build setup, see `docs/MICROSOFT_STORE.md`.

## Troubleshooting

- Build fails with target errors: confirm `.NET 10` (`dotnet --version`), then clean + restore.
- App fails to launch: ensure the entire `HopTracer_Portable` directory is intact.
- Missing cluster archive decode: run `.\scripts\setup_dependencies.ps1` to copy `GH_IO.dll` (optional enhancement).
