# HopTracer

> ⚠️ This repository contains code that was generated with AI assistance. Review, test, and use at your own risk.

HopTracer is a hybrid .NET MAUI desktop application for comparing Grasshopper (`.gh` / `.ghx`) files. It runs an embedded ASP.NET Core server inside a MAUI WebView to render an HTML5 Canvas diff viewer, highlighting added, removed, and modified components between two versions of a definition.

![Diff Viewer Screenshot](tests/data/gh_diff.png)

## How it works
- **MAUI Shell + Embedded Server**: `HopTracer.Maui` hosts a local Kestrel server that serves the UI and APIs to the in-app WebView.
- **Diff Engine**: `HopTracer.Core` parses `.ghx` graphs, computes diffs, and returns JSON for rendering.
- **Conversion**: Binary `.gh` files are converted to `.ghx` via the `GH_Converter` utility, which uses Rhino's `GH_IO.dll` and is based on the open-source [`ghtoghx`](https://bitbucket.org/RILGH/ghtoghx/wiki/Home) converter.

## Features
- **Visual Diff**: Added (green), removed (red), and modified (orange) components with connection highlighting.
- **Smart Highlighting**: Selecting a component dims unrelated nodes; long wires stay hidden until relevant.
- **Interactive Navigation**: Pan/zoom (scroll/drag), minimap for large graphs, and keyboard shortcuts (F to fit, +/- to zoom).
- **File Support**: Works with `.ghx` directly and `.gh` via built-in conversion.
- **Git Integration**: Pick prior versions straight from the file's Git history.
- **Portable Build**: Single-file executable (~140MB) with the runtime embedded.

## Installation
1. Download the latest `HopTracer.exe` from releases or `release/HopTracer_Portable`.
2. Run the executable—no installer required.

## Usage
1. Launch the application.
2. Drag and drop your "Old" and "New" Grasshopper files into the drop zones.
    - Optional: If the file lives in a Git repo, click "Select from Git History" to compare against a previous commit.
3. Click **Compare Files**.
4. Viewer controls:
    - Left Click: Select a node to view details and connections.
    - Drag: Pan the view.
    - Scroll: Zoom in/out.
    - Minimap: Drag the viewport box to navigate quickly.
    - Sidebar: Filter by status (Added/Removed/Modified) or search for components.

### Keyboard Shortcuts
| Key | Action |
| :--- | :--- |
| `F` | Fit graph to view |
| `Esc` | Deselect node |
| `+` / `=` | Zoom In |
| `-` | Zoom Out |

## Building from source

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.12+) or VS Code
- Rhino 7 or 8 installed (provides `GH_IO.dll` for the converter)

### Local build
```powershell
# Build the solution
dotnet build src_csharp/HopTracer.sln

# Run the MAUI app (Windows)
dotnet run --project src_csharp/HopTracer.Maui/HopTracer.csproj -f net9.0-windows10.0.19041.0
```

If `GH_IO.dll` is not copied automatically, run `scripts/setup_dependencies.ps1` to pull it from your Rhino installation.

### Create portable release
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

## License

This project is licensed under the MIT License - see `LICENSE` for details.
