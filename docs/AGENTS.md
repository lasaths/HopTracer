# AGENTS.md

## Project Overview
**HopTracer** is a desktop application for diffing Grasshopper (`.gh`, `.ghx`) files. It allows users to visualize changes between two versions of a Grasshopper definition, including added/removed nodes, modified connections, parameter changes, script code diffs, and cluster-level change previews.

The project is a **Hybrid .NET MAUI** application targeting **.NET 10**. It uses an embedded ASP.NET Core server to render a high-performance HTML5 Canvas visualization within a native window.

## Architecture

### Core Components (`Source/`)
- **HopTracer**: Main Application Entry Point.
    - **Type**: .NET MAUI (Windows/macOS).
    - **Role**: Native Shell, Window Management, Embedded Server Host.
    - **Key Logic**: `MainPage.xaml.cs` starts a Kestrel server on `localhost:5000` and points a `WebView` to it. It uses embedded file providers to serve bundled static assets.
- **HopTracer.Web**: Web Backend & UI.
    - **Type**: ASP.NET Core Web API.
    - **Role**: Serves the HTML/JS frontend and handles API requests (`/compare`, `/git`).
    - **Frontend**: `wwwroot/` contains the `diff_viewer.html` and custom Canvas rendering logic.
- **HopTracer.Core**: Shared Logic.
    - **Type**: .NET Class Library.
    - **Role**: Parsing, Diffing, Git Operations.
    - **Key Classes**: `GhxParser`, `Differ`, `GitWrapper`, `ConverterService`.
- **GhConverter**: Utility.
    - **Role**: Converts binary `.gh` files to XML `.ghx` using `GH_IO.dll`.
- **GhDiffTool** (`hoptracer` CLI): Headless diff tool.
    - **Type**: .NET Console (`Source/Tools/GhDiffTool`).
    - **Role**: `compare` and `git` commands with text, markdown, JSON, HTML, and **agent** output formats.
    - **Agent skill**: [`skills/hoptracer/SKILL.md`](../skills/hoptracer/SKILL.md) — `npx skills add lasaths/HopTracer@hoptracer`; commands, `--format agent`, baselines, forensic report.

### CLI Data Flow (agents / CI)
1. `hoptracer compare <old> <new> --format agent` (or `hoptracer git <file> --commit <hash>`).
2. For `git`, the tool writes the commit snapshot to a temp file, then diffs it against the working-tree file.
3. `ConverterService` converts `.gh` → `.ghx` when `GH_IO.dll` is present.
4. `GhxParser` + `Differ` produce `DiffComputation`.
5. `DiffAgentFormatter` emits JSON with resolved wires, property deltas, risk summary, and diagnostics.
6. Exit `1` when `--fail-on-risk` detects critical/high risks.

### Desktop Data Flow
1.  User drops files in MAUI WebView.
2.  Files are POSTed to `CompareController` (embedded server).
3.  `ConverterService` converts `.gh` -> `.ghx` if needed.
4.  `GhxParser` parses XML into a Graph model.
5.  `Differ` compares two graphs.
6.  Controller injects the Diff JSON into `diff_viewer.html`.
7.  WebView renders the interactive graph.

## Development Workflow

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022 (17.12+) or VS Code
- Windows 10/11
- Optional: Rhino 7/8 for enhanced cluster archive decoding (`GH_IO.dll`)

### Setup
`GH_IO.dll` is optional for build/test. If you need full `.gh` conversion and enhanced cluster archive decoding, run:

`scripts/setup_dependencies.ps1`

### Building and Running
1.  Open `Source/HopTracer.sln`.
2.  Set **HopTracer** as the startup project.
3.  Run (F5).
4.  For every code-change cycle, run the PowerShell build script before handoff/commit.

### Required Build Command (Agents)
Run builds from the repository root using the PowerShell script:

```powershell
.\scripts\build.ps1
```

If needed for faster iteration:

```powershell
.\scripts\build.ps1 -SkipTests
```

```powershell
.\scripts\build.ps1 -SkipClean
```

If script execution is blocked by policy, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Note: Prefer `scripts/build.ps1` over ad-hoc `dotnet build` for final validation, because the script runs the full clean/test/build/publish flow used for releases.

### Creating a Portable Release
Use the build script:
```powershell
.\scripts\build.ps1
```

This will clean, build, test, and create a portable executable in `Release/HopTracer_Portable/`.

**Build Script Options:**
- `-SkipClean`: Preserve previous build artifacts
- `-SkipTests`: Skip unit test execution (faster builds)

**Build Process:**
1. Cleans bin/obj directories and Release folder
2. Restores NuGet packages
3. Runs unit tests (unless skipped)
4. Builds in Release configuration
5. Publishes self-contained multi-file package

**Common Issues:**
- **ClassFactory Error**: Don't use single-file publishing with MAUI - WindowsAppSDK requires external files
- **Syntax Error**: Ensure file has UTF-8 encoding without BOM
- **Runtime Identifier Error**: The script automatically uses the correct RID for MAUI Windows apps
- **Application Won't Start**: Ensure entire `HopTracer_Portable` folder is distributed together, not just the .exe

## Remaining Tasks & Improvements

### High Priority
- [x] **Zoom/Pan Controls**: Implemented in Web UI.
- [x] **Portable Build**: Multi-file package working (~106MB, 575 files).
- [x] **Git Integration**: History viewing implemented with text input.
- [x] **Production Build Script**: Automated cleanup and build process.
- [x] **Code Cleanup**: Removed hardcoded paths, unused files.
- [x] **Build Script Fixes**: Resolved RID and encoding issues (November 2025).
- [x] **Single-File Publishing Issue**: Removed single-file option - MAUI/WindowsAppSDK requires external files.
- [x] **Script Diff Tab**: Dedicated right-panel tab with GitHub-style code diff.
- [x] **Cluster Preview Tab**: Dedicated right-panel tab with nested cluster diff canvas.
- [ ] **Git Integration Polish**: Better error handling for non-git directories.

### Medium Priority
- [ ] **Settings UI**: Configure colors/ignore rules.
- [ ] **Export to PDF**: Generate static reports.
- [ ] **Blazor Migration**: Consider moving from Embedded Kestrel to Blazor Hybrid for tighter integration and smaller footprint.
- [ ] **Build Optimization**: Reduce executable size with better trimming configuration.

## Highly Critical Feature Updates (Proposed)

### P0 (Reliability / Correctness)
- [ ] **Deterministic Node Identity Engine**: Add robust ID matching fallback (signature + topology + port schema) when Grasshopper GUIDs churn across save/export operations. Prevent false add/remove noise.
- [ ] **Deep Cluster Diff (Recursive)**: Expand current cluster preview into true recursive diffing for nested clusters (cluster-in-cluster), including propagated status rollups to parent canvas.
- [ ] **Diff Integrity Diagnostics**: Add explicit warnings when edges are dropped due to unresolved endpoints, parser ambiguities, or conversion failures. Surface this in UI and exported report.
- [ ] **Conversion Dependency Health Check**: Add startup diagnostics for `GH_IO.dll` / Rhino compatibility and hard-fail with guided remediation instead of late runtime exceptions.

### P1 (Scalability / Performance)
- [ ] **Large-Model Pipeline**: Support 50k+ node definitions with viewport-based culling, incremental rendering, and worker-thread parsing to keep interaction smooth.
- [ ] **Progressive Diff Loading**: Stream parse/diff stages to UI (parse old, parse new, match, edge resolve) with progress and cancellation support.
- [ ] **Edge Density Controls v2**: Add semantic wire filters (selected subgraph, changed-only wires, same-status suppression) on top of distance threshold.

### P1 (Review Workflow)
- [ ] **Forensic Diff Report**: Export a signed JSON+HTML artifact including metadata, summary metrics, top-risk changes (scripts/clusters), and parser diagnostics for code reviews.
- [ ] **Risk Scoring Layer**: Rank changes by impact (script edits, removed clusters, broken inputs, parameter value mutations) and highlight the top critical nodes first.
- [ ] **Baseline & Regression Mode**: Allow saving a known-good snapshot and running future diffs against it in CI to catch unintended Grasshopper definition regressions.

### Technical Notes from Build Script Development
- **Runtime Identifier**: MAUI Windows projects automatically use `win-x64` RID; explicit specification can cause conflicts
- **Restore Strategy**: Simple `dotnet restore` works best; avoid specifying runtime during restore for MAUI projects
- **Publish Settings**: Remove `--no-restore` flag to ensure proper dependency resolution during publish
- **File Encoding**: PowerShell scripts must use UTF-8 encoding to avoid parsing errors
- **Single-File Publishing**: Cannot be used with MAUI/WindowsAppSDK - WinUI3 requires external runtime files
  - Using `-p:PublishSingleFile=true` causes `ClassFactory cannot supply requested class` error
  - The application must be distributed as a multi-file package (~575 files)
  - All files in the output directory are required for the app to run

### Diff Engine Guardrails (Feb 2026)
- Movement noise tolerance is `0.05` units: deltas below this are normalized to `0` so `Delta 0.0, 0.0` is not treated as meaningful movement.
- Invariant: any node emitted with `status = modified` must have `RiskScore > 0`.
- Connection-only modifications receive a low risk floor (`15`) with reason `Connection changes` to avoid `modified + risk 0` output.
- Edge identity comparison normalizes GUID tokens (trim, remove `{}` braces, lowercase) so format-only ID differences do not create false wire add/remove changes.

## Code Organization
```
HopTracer/
├── Release/             # Build output
├── scripts/             # Setup/build scripts
├── Source/
│   ├── HopTracer/           # Main App (Shell)
│   ├── HopTracer.Web/       # UI & API (Logic)
│   ├── HopTracer.Core/      # Parsing & Diffing (Core)
│   ├── GhConverter/         # GH->GHX Tool
│   └── Tools/GhDiffTool/    # hoptracer CLI (compare, git, --format agent)
├── skills/hoptracer/          # Agent skill for CLI workflows
├── Tests/
│   ├── HopTracer.UnitTests/ # Unit tests
│   └── data/                # Sample .ghx fixtures
└── README.md                # User Guide
```
