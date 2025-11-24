# HopTracer

**HopTracer** is a cross-platform, portable desktop application for comparing Grasshopper (`.gh` / `.ghx`) files. It visualizes the differences between two versions of a definition in an interactive graph view, highlighting added, removed, and modified components.

![Diff Viewer Screenshot](tests/data/gh_diff.png)

## Features

-   **Visual Diff**: Clearly see added (green), removed (red), and modified (orange) components.
-   **Smart Highlighting**: 
    -   Select a component to highlight its connections and dim unrelated nodes.
    -   Long wires are hidden by default to reduce clutter, appearing only when relevant.
-   **Interactive Navigation**: 
    -   Pan and zoom (scroll/drag).
    -   **Minimap** for quick navigation of large graphs.
    -   **Keyboard Shortcuts** (F to fit, +/- to zoom).
-   **File Support**: Supports both XML-based `.ghx` files and binary `.gh` files (via built-in conversion).
-   **Git Integration**: Select previous versions directly from your file's Git history.
-   **Portable**: Single-file executable (~140MB) with no external dependencies (embedded runtime).

## Installation

HopTracer is distributed as a single portable executable.

1.  Download the latest `HopTracer.Maui.exe` from the releases.
2.  Run the executable. No installation required.

## Usage

1.  **Launch** the application.
2.  **Drag and drop** your "Old" and "New" Grasshopper files into the drop zones.
    -   *Optional*: If the file is in a Git repo, click "Select from Git History" to compare against a previous commit.
3.  Click **Compare Files**.
4.  **Viewer Controls**:
    -   **Left Click**: Select a node to view details and connections.
    -   **Drag**: Pan the view.
    -   **Scroll**: Zoom in/out.
    -   **Minimap**: Drag the viewport box to navigate quickly.
    -   **Sidebar**: Filter by status (Added/Removed/Modified) or search for components.

### Keyboard Shortcuts
| Key | Action |
| :--- | :--- |
| `F` | Fit graph to view |
| `Esc` | Deselect node |
| `+` / `=` | Zoom In |
| `-` | Zoom Out |

## Building from Source

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 (17.12+) or VS Code

### Local Build
```powershell
# Build the solution
dotnet build src_csharp/HopTracer.sln

# Run the MAUI app (Windows)
dotnet run --project src_csharp/HopTracer.Maui/HopTracer.Maui.csproj -f net9.0-windows10.0.19041.0
```

### Create Portable Release
To build the optimized single-file executable:

```powershell
dotnet publish src_csharp/HopTracer.Maui/HopTracer.Maui.csproj `
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

[MIT](LICENSE)
