# Changelog

All notable changes to HopTracer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Portable single-file executable (~100-120MB, includes full runtime)
- Cross-platform support (Windows/macOS via .NET MAUI)

### Technical Details

- Built with .NET 9 and .NET MAUI
- Hybrid architecture: ASP.NET Core backend + HTML5 Canvas frontend
- Embedded web server (Kestrel) hosting visualization
- Uses GH_IO.dll from Rhino installation for .gh parsing

### Known Issues

- Git integration requires manual file path input
- macOS build not yet tested/released
