<p align="center">
  <img src="logo/HopTrace_Logo.png" alt="HopTracer Logo" width="200"/>
</p>

# HopTracer

HopTracer is a powerful desktop tool designed to help architects and computational designers visualize changes in their Grasshopper definitions. It provides a clear, interactive comparison between two versions of a file, highlighting what has been added, removed, or modified.

> **AI Note**: This entire project was generated and refined by AI agents.

## Features

*   **Visual Diffing**: See added, removed, and modified components on an interactive canvas.
*   **Git Integration**: View commit history for a file and compare against previous versions.
*   **Portable**: Single-file executable with no external dependencies.
*   **Interactive UI**: Pan, zoom, filter by change type, and search for components.

## Installation

1.  Download the latest release from the [Releases](https://github.com/lasaths/HopTracer/releases) page.
2.  Run `HopTracer.exe`.

## Usage

1.  **Launch the App**: Open `HopTracer.exe`.
2.  **Select Files**:
    *   Drag your "Old" file into the left box.
    *   Drag your "New" file into the right box.
    *   *Optional*: If the file is in a Git repo, click "Select from Git History" to pick a previous commit.
3.  **Compare**: Click "Compare Files".
4.  **Explore**:
    *   Use the sidebar to filter changes (Added, Removed, Modified).
    *   Click nodes to see property changes.
    *   Use the "Wire Visibility" slider to hide long wires for cleaner viewing.

## For Developers

This section contains technical details for those interested in the backend or contributing to the project.

### Architecture
The project is built using **.NET 9** and **.NET MAUI** for cross-platform desktop support (Windows/macOS). It uses a hybrid approach where the UI is rendered via a local ASP.NET Core server hosting a web-based visualization.

*   **Core**: Handles parsing of `.gh`/`.ghx` files and the diffing logic.
*   **Web**: Serves the HTML/JS visualization and API endpoints.
*   **MAUI**: Wraps the web application in a native desktop window using WebView2.

### Building from Source

#### Prerequisites
*   .NET 9 SDK
*   Visual Studio 2022 or VS Code

#### Steps
1.  Clone the repository.
2.  Open `src_csharp/HopTracer.sln`.
3.  Build the `HopTracer.Maui` project.
4.  Run the application.

## License

MIT License
