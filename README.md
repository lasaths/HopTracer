<p align="center">
  <img src="Assets/HopTrace_Logo.png" alt="HopTracer Logo" width="200"/>
</p>

# HopTracer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/github/v/release/lasaths/HopTracer)](https://github.com/lasaths/HopTracer/releases)

HopTracer is a powerful desktop tool designed to help architects and computational designers visualize changes in their Grasshopper definitions. It provides a clear, interactive comparison between two versions of a file, highlighting what has been added, removed, or modified.

> **AI Note**: This entire project was generated and refined by AI agents.

## Features

* **Visual Diffing**: See added, removed, and modified components on an interactive canvas.
* **Git Integration**: View commit history for a file and compare against previous versions.
* **Portable**: Single-file executable with no external dependencies.
* **Interactive UI**: Pan, zoom, filter by change type, and search for components.
* **File Format Support**: Works with both binary `.gh` and XML `.ghx` files.

![HopTracer UI](Tests/data/gh_ui_ports.png)

## Installation

1. Download the latest release from the [Releases](https://github.com/lasaths/HopTracer/releases) page.
2. Run `HopTracer.exe`.

No installation or .NET runtime required - just download and run!

## Usage

1. **Launch the App**: Open `HopTracer.exe`.
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

The project is built using **.NET 9** and **.NET MAUI** for cross-platform desktop support (Windows/macOS). It uses a hybrid approach where the UI is rendered via a local ASP.NET Core server hosting a web-based visualization.

* **Core**: Handles parsing of `.gh`/`.ghx` files and the diffing logic.
* **Web**: Serves the HTML/JS visualization and API endpoints.
* **MAUI**: Wraps the web application in a native desktop window using WebView2.

### Building from Source

#### Prerequisites

* .NET 9 SDK
* Visual Studio 2022 or VS Code
* Rhino 7 or 8 (for GH_IO.dll dependency)

#### Steps

1. Clone the repository: `git clone https://github.com/lasaths/HopTracer.git`
2. Run the setup script: `.\Scripts\setup_dependencies.ps1`
3. Open `Source/HopTracer.sln`.
4. Build the `HopTracer` project.
5. Run the application.

#### Production Build

To create a portable single-file executable:

```powershell
.\Scripts\build.ps1
```

**Options:**
- `-SkipClean` - Keep previous builds
- `-SkipTests` - Skip running tests

The output is a ~100-120MB standalone `.exe` file in `Release/HopTracer_Portable/` with no external dependencies.

**Note**: The size includes the full .NET 9 runtime, MAUI framework, ASP.NET Core server, and all dependencies - this ensures the app works on any Windows 10+ machine without requiring .NET installation.

### Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

* Built with [.NET MAUI](https://dotnet.microsoft.com/apps/maui) and [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
* GH/GHX conversion based on [GhToGhx](https://bitbucket.org/rilgh/ghtoghx/wiki/Home) by David Rutten
* Entire project generated and refined with AI assistance
