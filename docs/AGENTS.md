# AGENTS.md

## Project Overview
**HopTracer** is a desktop application for diffing Grasshopper (`.gh`, `.ghx`) files. It allows users to visualize changes between two versions of a Grasshopper definition, including added/removed nodes, modified connections, and parameter changes.

The project is a **Hybrid .NET MAUI** application targeting **.NET 9**. It uses an embedded ASP.NET Core server to render a high-performance HTML5 Canvas visualization within a native window.

## Architecture

### Core Components (`Source/`)
- **HopTracer**: Main Application Entry Point.
    - **Type**: .NET MAUI (Windows/macOS).
    - **Role**: Native Shell, Window Management, Embedded Server Host.
    - **Key Logic**: `MainPage.xaml.cs` starts a Kestrel server on `localhost:5000` and points a `WebView` to it. It uses `ManifestEmbeddedFileProvider` to serve static assets from the single-file bundle.
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

### Data Flow
1.  User drops files in MAUI WebView.
2.  Files are POSTed to `CompareController` (embedded server).
3.  `ConverterService` converts `.gh` -> `.ghx` if needed.
4.  `GhxParser` parses XML into a Graph model.
5.  `Differ` compares two graphs.
6.  Controller injects the Diff JSON into `diff_viewer.html`.
7.  WebView renders the interactive graph.

## Development Workflow

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 (17.12+) or VS Code
- Rhino 7 or 8 installed (for `GH_IO.dll`)

### Setup
The project automatically copies `GH_IO.dll` from your local Rhino installation during build.
If this fails, run: `Scripts/setup_dependencies.ps1`

### Building and Running
1.  Open `Source/HopTracer.sln`.
2.  Set **HopTracer** as the startup project.
3.  Run (F5).

### Creating a Portable Release
Use the build script:
```powershell
.\Scripts\build.ps1
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
5. Publishes self-contained multi-file package (~106MB, 575 files)

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
- [ ] **Git Integration Polish**: Better error handling for non-git directories.

### Medium Priority
- [ ] **Settings UI**: Configure colors/ignore rules.
- [ ] **Export to PDF**: Generate static reports.
- [ ] **Blazor Migration**: Consider moving from Embedded Kestrel to Blazor Hybrid for tighter integration and smaller footprint.
- [ ] **Build Optimization**: Reduce executable size with better trimming configuration.

### Technical Notes from Build Script Development
- **Runtime Identifier**: MAUI Windows projects automatically use `win-x64` RID; explicit specification can cause conflicts
- **Restore Strategy**: Simple `dotnet restore` works best; avoid specifying runtime during restore for MAUI projects
- **Publish Settings**: Remove `--no-restore` flag to ensure proper dependency resolution during publish
- **File Encoding**: PowerShell scripts must use UTF-8 encoding to avoid parsing errors
- **Single-File Publishing**: Cannot be used with MAUI/WindowsAppSDK - WinUI3 requires external runtime files
  - Using `-p:PublishSingleFile=true` causes `ClassFactory cannot supply requested class` error
  - The application must be distributed as a multi-file package (~575 files)
  - All files in the output directory are required for the app to run

## Code Organization
```
HopTracer/
├── Release/             # Build output
├── Scripts/             # Setup scripts
├── Source/
│   ├── HopTracer/           # Main App (Shell)
│   ├── HopTracer.Web/       # UI & API (Logic)
│   ├── HopTracer.Core/      # Parsing & Diffing (Core)
│   ├── GhConverter/         # GH->GHX Tool
│   └── TestDiff/            # Unit Tests
├── Tests/
│   └── data/                # Sample files
├── AGENTS.md                # Developer Guide
└── README.md                # User Guide
```