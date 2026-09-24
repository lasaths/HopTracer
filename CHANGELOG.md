# Changelog

All notable changes to HopTracer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-07-06

### Added

- **Git history comparison**: Select any two commits from file history and compare them directly.
- **Dual-commit selection UI**: Click to select OLD and NEW commits with visual feedback (blue/green highlighting).
- **Auto-chronological sorting**: OLD/NEW assignment is automatic based on commit date, regardless of click order.
- **Flexible deselection**: Deselect either commit independently and pick a different one without resetting.
- **Smart button states**: Dynamic "Load Selected Version" button shows appropriate action based on selection state.

### Changed

- Git history modal now shows OLD/NEW commit labels at the top for clarity.
- Status messages guide users through the selection process step-by-step.

## [Unreleased]

## [1.2.1] - 2026-07-10

### Added

- **Review & Export** sidebar: forensic report export, save baseline, and check baseline controls in the diff viewer.
- **Show changed wires only** filter toggle in View Options.
- **`hoptracer` CLI**: `doctor`, `baseline save|list|compare`, and `report` commands (parity with desktop review workflows).
- Agent skill renamed to **`hoptracer`** (`skills/hoptracer/`); install via `npx skills add lasaths/HopTracer@hoptracer`.

### Fixed

- Forensic report export failed at runtime because `ForensicReportArtifact` was missing from the source-generated JSON context.

## [1.2.0] - 2026-07-02

### Added

- **`hoptracer` CLI** (`Source/Tools/GhDiffTool`): `compare` and `git` commands with text, markdown, JSON, HTML, and **agent** output formats.
- **Agent diff format** (`--format agent`): resolved wire labels, classified property changes, opaque geometry cache detection, template summary, and 1-hop neighbor context for AI review workflows.
- **Diff output services**: `DiffOutputGenerator`, `DiffGraphResolver`, and `DiffAgentFormatter` in HopTracer.Core.
- CLI bundled in portable releases (`tools/hoptracer/`) and Microsoft Store MSIX (`hoptracer.exe` App Execution Alias on PATH).
- Unit tests for diff output and agent formatting.

### Changed

- Portable and MSIX build scripts now publish the CLI alongside the desktop app.
- JSON `--show-edges` output includes human-readable `sourceLabel` / `targetLabel` on changed wires.
- `skills/gh-diff` updated for real CLI install paths and agent format.

### Removed

- Stale `TestDiff` tool project (replaced by `GhDiffTool`).
- Redundant root-level git integration docs and `IMPLEMENTATION_COMPLETE.md` (content lives in `skills/gh-diff/`).

### Fixed

- CLI `.gh` conversion requires `Microsoft.WindowsDesktop.App` (GH_IO dependency on WinForms).
- CLI error messages now surface inner exception details.
- `.gitignore` no longer blocks `Source/Tools/` from version control.

## [1.1.1] - 2026-04-06

### Changed

- Script components now keep their code accessible in the details panel even when the source is unchanged. Changed scripts still render as diffs, while unchanged C# and Python scripts open in source view.
- App-managed `.gh` to `.ghx` conversions used for compare, review, and Git flows now write temporary `.ghx` files into HopTracer's app-data storage instead of next to the original Grasshopper file.
- Diff viewer search: the component catalog and spotlight show labeled excerpts when matches land inside component inputs or script bodies, with longer snippets and better multi-word matching.

### Fixed

- Disabled embedded WebView inspect entry points by turning off the default right-click context menu, DevTools shortcuts, and related browser accelerator keys in the desktop app.
- Windows-generated toolbar and tile icons now preserve transparent backgrounds instead of rendering a blue plate behind the HopTracer mark.
- `check_store_readiness.ps1` runs on Windows PowerShell 5.1 (removed PowerShell 7-only syntax).

## [1.1.0] - 2026-04-03

### Added

- **Port options tracking**: HopTracer now detects and displays per-port configuration changes across diff commits. Supported options: Flatten, Graft, Simplify, Reverse, Optional, Access (item/list/tree), DataMapping, AtLeast, and AtMost.
- **GhxParser**: New `ParseInputOptions()` method extracts all port options from `.ghx` files, with alias resolution and value normalization for robust cross-version comparison.
- **Expandable script diff modal**: Script diffs now have an expand button that opens a full-width modal (1440px) for easier reading of large scripts. The modal includes keyboard support, focus management, and backdrop-click-to-close.
- GH_IO.dll resolution now probes standard Rhino 6, 7, and 8 installation paths automatically so conversion works out-of-the-box without manual configuration.

### Changed

- `diff_viewer.html`: Refactored CSS to use `--radius-button` variable; updated editor color scheme for better contrast; improved code diff layout with flex-based sign/content separation.
- `build_msix.ps1`: Certificate provider availability is now checked before attempting self-signed cert generation, enabling graceful handling of sandboxed/non-Windows CI environments.
- `check_store_readiness.ps1`: Added non-blocking GH_IO.dll detection check with helpful path diagnostics.

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
