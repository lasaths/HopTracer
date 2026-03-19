# Changelog

All notable changes to HopTracer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-03-19

### Changed

- Decoupled `GH_IO.dll` from compile-time references so build/test work without a local Rhino installation.
- `ConverterService` and cluster preview parsing now probe `GH_IO.dll` at runtime when available.
- Unified release preflight in `scripts/check_store_readiness.ps1` for GitHub ZIP and Microsoft Store packaging.
- GitHub CI now uses the same `scripts/build.ps1` portable release path used for local release validation.
- Strengthened `scripts/build_msix.ps1` and Store workflow inputs around explicit identity, publisher, and signing validation.
- Updated release documentation for the `1.0.0` reissue flow, including Store package revision guidance.

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
