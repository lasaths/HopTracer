# Changelog

All notable changes to HopTracer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Decoupled `GH_IO.dll` from compile-time references; build/test now work without local Rhino installation.
- `ConverterService` and cluster preview parsing now probe `GH_IO.dll` at runtime when available.
- Improved production build gating: `scripts/build.ps1` now fails fast when tests fail.
- Added dedicated Microsoft Store packaging workflow and stronger `scripts/build_msix.ps1` output/signing controls.
- Added GitHub maintenance scaffolding (`Dependabot`, PR template, Store MSIX workflow).
- Updated contributor/release documentation for .NET 10, CI flow, and Store submission.

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
