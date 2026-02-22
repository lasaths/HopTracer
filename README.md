<p align="center">
  <img src="Assets/HopTrace_Logo.png" alt="HopTracer Logo" width="200"/>
</p>

# HopTracer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![CI](https://github.com/lasaths/HopTracer/actions/workflows/build.yml/badge.svg)](https://github.com/lasaths/HopTracer/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/lasaths/HopTracer)](https://github.com/lasaths/HopTracer/releases)

HopTracer is a desktop tool designed to help architects and computational designers visualize changes in their Grasshopper definitions. It provides a clear, interactive comparison between two versions of a file, highlighting what has been added, removed, or modified.

## Features

* **Visual Diffing**: See added, removed, and modified components on an interactive canvas.
* **Git Integration**: View commit history for a file and compare against previous versions.
* **Portable**: Self-contained Windows package with no external .NET runtime dependency.
* **Interactive UI**: Pan, zoom, filter by change type, and search for components.
* **File Format Support**: Works with both binary `.gh` and XML `.ghx` files.

![HopTracer UI](Tests/data/gh_ui_ports.png)

## Installation

1. Download the latest release from the [Releases](https://github.com/lasaths/HopTracer/releases) page.
2. Extract `HopTracer-Windows-x64.zip`.
3. Ensure `GH_IO.dll` is available (see **Required Dependency: GH_IO.dll** below).
4. Run `HopTracer.exe` from the extracted `HopTracer_Portable` folder.

No .NET runtime installation is required.

### Required Dependency: `GH_IO.dll`

HopTracer requires Grasshopper's `GH_IO.dll` at runtime.

How to satisfy this requirement:

1. Install Rhino 7 or Rhino 8 (recommended), which provides `GH_IO.dll`.
2. If you are building from source, run:
   ```powershell
   .\scripts\setup_dependencies.ps1
   ```
3. If auto-discovery fails, manually copy `GH_IO.dll` from one of:
   - `C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll`
   - `C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll`
   - `C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll`
   into:
   - `Source\HopTracer.Web\tools\` (source builds), or
   - the same directory as `HopTracer.exe` (portable/runtime scenario).

## Usage

1. **Launch the App**: Open `HopTracer.exe` from the extracted `HopTracer_Portable` folder.
2. **Select Files**:
   * Drag your "Old" file into the left box (supports both `.gh` and `.ghx`).
   * Drag your "New" file into the right box.
   * *Optional*: If the file is in a Git repo, click "Select from Git History" to pick a previous commit.
3. **Compare**: Click "Compare Files".
4. **Explore**:
   * Use the sidebar to filter changes (Added, Removed, Modified).
   * Click nodes to see property changes.
   * Use the "Wire Visibility" slider to hide long wires for cleaner viewing.

## Credits

* **GH to GHX Conversion**: Binary `.gh` to XML `.ghx` conversion functionality adapted from [GhToGhx](https://bitbucket.org/rilgh/ghtoghx/wiki/Home) by rilgh.

## For Developers

This section contains technical details for those interested in the backend or contributing to the project.

### Architecture

The project is built using **.NET 10** and **.NET MAUI** for Windows desktop support. It uses a hybrid approach where the UI is rendered via a local ASP.NET Core server hosting a web-based visualization.

* **Core**: Handles parsing of `.gh`/`.ghx` files and the diffing logic.
* **Web**: Serves the HTML/JS visualization and API endpoints.
* **MAUI**: Wraps the web application in a native desktop window using WebView2.

### Building from Source

#### Prerequisites

* .NET 10 SDK
* Visual Studio 2022 or VS Code
* Windows 10/11 (for MAUI Windows target)
* Rhino 7/8 (or Rhino 6) so `GH_IO.dll` is available

#### Steps

1. Clone the repository: `git clone https://github.com/lasaths/HopTracer.git`
2. Run dependency setup: `.\scripts\setup_dependencies.ps1`
3. Open `Source/HopTracer.sln`.
4. Build the `HopTracer` project.
5. Run the application.

#### Production Build

To create a portable release package:

```powershell
.\scripts\build.ps1
```

**Options:**
- `-SkipClean` - Keep previous builds
- `-SkipTests` - Skip running tests

The build creates:

- `Release\HopTracer_Portable\` (self-contained app folder)
- `Release\HopTracer-Windows-x64.zip` (GitHub-ready release artifact)

**Note**: The package includes the full .NET 10 runtime, MAUI framework, ASP.NET Core server, and all dependencies, so it works on Windows 10+ without a preinstalled runtime.

### Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Community & Security

- Code of Conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Security Policy: [SECURITY.md](SECURITY.md)

### Microsoft Store Packaging

Use the MSIX build script to create Store-ready artifacts:

```powershell
.\scripts\build_msix.ps1
```

Detailed Store submission steps are documented in [`docs/MICROSOFT_STORE.md`](docs/MICROSOFT_STORE.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

* Built with [.NET MAUI](https://dotnet.microsoft.com/apps/maui) and [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
* GH/GHX conversion based on [GhToGhx](https://bitbucket.org/rilgh/ghtoghx/wiki/Home) by David Rutten
