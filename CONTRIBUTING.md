# Contributing to HopTracer

Thank you for your interest in contributing to HopTracer! This document provides guidelines for contributing to the project.

## Development Setup

1. **Prerequisites**
   - .NET 9 SDK
   - Visual Studio 2022 (17.12+) or VS Code
   - Rhino 7 or 8 installed (for GH_IO.dll)

2. **Clone and Setup**

   ```bash
   git clone https://github.com/lasaths/HopTracer.git
   cd HopTracer
   .\Scripts\setup_dependencies.ps1
   ```

3. **Build and Run**

   ```bash
   cd Source
   dotnet restore
   dotnet build
   dotnet run --project HopTracer
   ```

## Project Structure

```text
HopTracer/
├── Source/
│   ├── HopTracer/          # Main MAUI app (Shell)
│   ├── HopTracer.Web/      # Web backend & UI
│   ├── HopTracer.Core/     # Parsing & diffing logic
│   ├── GhConverter/        # GH to GHX converter
│   └── TestDiff/           # Core logic tests
├── Tests/data/             # Test fixtures
├── Scripts/                # Build and setup scripts
└── Assets/                 # Images and resources
```

## Making Changes

1. **Create a Branch**

   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Your Changes**

   - Follow existing code style and conventions
   - Keep changes focused and minimal
   - Add tests for new functionality

3. **Test Your Changes**

   ```bash
   dotnet test Source/HopTracer.sln
   ```

4. **Commit**

   ```bash
   git add .
   git commit -m "Brief description of changes"
   ```

5. **Push and Create PR**

   ```bash
   git push origin feature/your-feature-name
   ```

## Code Style

- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods small and focused
- Avoid console logging in production code (use ILogger)
- Use async/await for I/O operations

## Areas for Contribution

### High Priority

- macOS testing and build configuration
- Git integration improvements (better error handling)
- Performance optimization for large files
- Memory usage optimization

### Medium Priority

- Settings UI for customization
- Export to PDF/PNG
- Blazor Hybrid migration (reduce bundle size)
- Dark/light theme toggle
- Keyboard shortcuts

### Low Priority

- Additional diff algorithms
- Custom color schemes
- Plugin system for custom visualizations

## Testing

- Add unit tests in `TestDiff/` for core logic changes
- Test with both .gh and .ghx files
- Test with files from different Rhino/Grasshopper versions
- Test git integration with real repositories

## Questions?

Open an issue or discussion on GitHub. We're happy to help!
