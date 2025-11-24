# AGENTS.md

## Project Overview
**HopTracer** is a desktop application for diffing Grasshopper (`.gh`, `.ghx`) files. It allows users to visualize changes between two versions of a Grasshopper definition, including added/removed nodes, modified connections, and parameter changes.

The project is a **Hybrid .NET MAUI** application targeting **.NET 9**. It uses an embedded ASP.NET Core server to render a high-performance HTML5 Canvas visualization within a native window.

## Architecture

### Core Components (`src_csharp/`)
- **HopTracer.Maui**: Main Application Entry Point.
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
- **GH_Converter**: Utility.
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
If this fails, run: `scripts/setup_dependencies.ps1`

### Building and Running
1.  Open `src_csharp/HopTracer.sln`.
2.  Set **HopTracer** as the startup project.
3.  Run (F5).

### Creating a Portable Release
To build the optimized, single-file executable (~140MB):
```powershell
dotnet publish src_csharp/HopTracer.Maui/HopTracer.csproj `
    -f net9.0-windows10.0.19041.0 `
    -c Release `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishSingleFile=true `
    -p:RuntimeIdentifier=win-x64 `
    -o release/HopTracer_Portable
```

## Remaining Tasks & Improvements

### High Priority
- [x] **Zoom/Pan Controls**: Implemented in Web UI.
- [x] **Portable Build**: Single-file exe working.
- [x] **Git Integration**: Basic history viewing implemented.
- [ ] **Git Integration Polish**: Better error handling for non-git directories.

### Medium Priority
- [ ] **Settings UI**: Configure colors/ignore rules.
- [ ] **Export to PDF**: Generate static reports.
- [ ] **Blazor Migration**: Consider moving from Embedded Kestrel to Blazor Hybrid for tighter integration and smaller footprint.

## Code Organization
```
HopTracer/
├── release/             # Build output
├── scripts/             # Setup scripts
├── src_csharp/
│   ├── HopTracer.Maui/      # Main App (Shell)
│   ├── HopTracer.Web/       # UI & API (Logic)
│   ├── HopTracer.Core/      # Parsing & Diffing (Core)
│   ├── GH_Converter/        # GH->GHX Tool
│   └── TestDiff/            # Unit Tests
├── tests/
│   └── data/                # Sample files
├── AGENTS.md                # Developer Guide
└── README.md                # User Guide
```