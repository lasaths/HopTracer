# Changelog

All notable changes to HopTracer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.2] - 2026-04-03

### Fixed

- MSIX package is now signed before creating the `.msixupload` archive for Store submission.
- Excluded the build script itself from secret-pattern scanning to eliminate false positives.
- Non-ASCII checkmark character replaced in `build_msix.ps1` for cross-locale compatibility.
- Removed the `GhConverter` project from the solution build to fix CI restore errors.
- Skipped CodeQL result upload step when code scanning is not enabled on the repository.

### Added

- Microsoft Store publishing workflow (`Codex/publish-store-hoptracer`) for automated MSIX upload.
- Comprehensive script-based CI build with quality gates and readiness checks.
- AI disclosure note and privacy policy added to README and project documentation.
- Store logos included in the repository for submission and marketing assets.

## [1.0.1] - 2026-03-20

### Changed

- Decoupled `GH_IO.dll` from compile-time references so build/test work without a local Rhino installation.
- `ConverterService` and cluster preview parsing now probe `GH_IO.dll` at runtime when available.
- Improved production build gating: `scripts/build.ps1` now fails fast when tests fail.
- Added dedicated Microsoft Store packaging workflow and stronger `scripts/build_msix.ps1` output/signing controls.
- Added GitHub maintenance scaffolding (`Dependabot`, PR template, Store MSIX workflow).
- Updated contributor/release documentation for .NET 10, CI flow, and Store submission.
- Fixed cluster nodes so internal cluster diffs remain visibly marked as `modified` in the viewer.

## [1.0.0] - 2025-11-24

### Added

- Initial release of HopTracer
- Visual diffing of Grasshopper (.gh/.ghx) files
- Interactive canvas with pan/zoom controls
- Git integration for viewing commit history
- Support for both binary (.gh) and XML (.ghx) formats
- Automatic conversion of .gh to .ghx using GH_IO.dll
- Filter by change type (Added, Removed, Modified)
- Search functionality for components
- Wire visibility controls
- Property change details for modified components
- Portable self-contained Windows package (multi-file, includes full runtime)
- Cross-platform support (Windows/macOS via .NET MAUI)

### Technical Details

- Built with .NET 10 and .NET MAUI
- Hybrid architecture: ASP.NET Core backend + HTML5 Canvas frontend
- Embedded web server (Kestrel) hosting visualization
- Uses GH_IO.dll for `.gh` parsing when available

### Known Issues

- Git integration requires manual file path input
- macOS build not yet tested/released
