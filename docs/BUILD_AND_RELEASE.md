# Build and Release Guide

This document provides instructions for building and releasing HopTracer.

## Building

### Quick Build

```powershell
.\Scripts\build.ps1
```

**Options:**

- `-SkipClean` - Keep previous builds
- `-SkipTests` - Skip running tests

### What the Build Script Does

1. Cleans previous builds (bin/obj folders, Release directory)
2. Removes unused files and internal docs
3. Restores NuGet dependencies for all projects
4. Runs tests (unless `-SkipTests` is used)
5. Builds Release configuration
6. Publishes portable multi-file package (~106MB, 575 files)

The build process takes approximately 3-5 minutes depending on system performance.

**Important**: The application is published as a multi-file package, not a single-file executable, because MAUI with WindowsAppSDK requires external WinUI3 runtime files that cannot be bundled into a single executable.

### Build Configuration

The build uses the following .NET publish settings:

- **Target Framework**: `net9.0-windows10.0.19041.0`
- **Configuration**: Release
- **Self-Contained**: Yes (includes .NET runtime)
- **Single File**: No (WindowsAppSDK requires external files)
- **Trimming**: Enabled (full trim mode)
- **WindowsAppSDK**: Self-contained mode

### Output

The build creates a portable package in `Release/HopTracer_Portable/` containing:

- **HopTracer.exe** (~290KB) - Main executable
- **575 support files** - .NET runtime, MAUI framework, WindowsAppSDK, dependencies
- **Total size**: ~106MB

The entire `HopTracer_Portable` folder must be distributed together. Run `HopTracer.exe` to start the application.

### Troubleshooting

**"ClassFactory cannot supply requested class" error**
- This means single-file publishing was used incorrectly
- Solution: Rebuild without `-p:PublishSingleFile=true`
- The app requires WindowsAppSDK files that must remain external

**Build Fails with "Assets file doesn't have a target"**
- Ensure you're using .NET 9 SDK: `dotnet --version`
- Clean and restore: `dotnet clean && dotnet restore`

**Build Script Syntax Error**
- Verify PowerShell execution policy: `Get-ExecutionPolicy`
- If restricted, run: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

**Application doesn't start or crashes immediately**
- Ensure all files in `HopTracer_Portable` folder are present
- Don't try to move just the .exe file - the entire folder is needed
- Check Windows Event Viewer for detailed error messages

## Testing

After building, test the executable:

```powershell
.\Release\HopTracer_Portable\HopTracer.exe
```

### Test Checklist

- [ ] Application launches successfully
- [ ] Can select and compare .ghx files
- [ ] Can select and compare .gh files
- [ ] Can mix .gh and .ghx files
- [ ] Git history button appears
- [ ] Git history modal works (enter path, load commits)
- [ ] Pan/zoom controls work
- [ ] Filter buttons work (Added/Removed/Modified)
- [ ] Search functionality works
- [ ] Property inspector shows changes
- [ ] Wire visibility slider works

## Release Process

### 1. Commit & Tag

```powershell
git add .
git commit -m "Release v1.0.0 - Description"
git tag v1.0.0
git push origin main
git push origin v1.0.0
```

### 2. Create GitHub Release

1. Go to: <https://github.com/lasaths/HopTracer/releases/new>
2. **Tag**: `v1.0.0` (or appropriate version)
3. **Title**: `HopTracer v1.0.0 - Release Title`
4. **Description**: Copy from CHANGELOG.md
5. **Assets**: Upload `HopTracer.exe` from `Release\HopTracer_Portable\`
6. Check: **Set as latest release**
7. Click: **Publish release**

### 3. Post-Release

- [ ] Test download link
- [ ] Verify executable downloads correctly
- [ ] Update README if needed
- [ ] Monitor issues/feedback

## Project Structure

```text
HopTracer/
├── README.md
├── CHANGELOG.md
├── CONTRIBUTING.md
├── AGENTS.md
├── LICENSE
├── .gitignore
│
├── Scripts/
│   ├── setup_dependencies.ps1
│   └── build.ps1
│
├── Source/
│   ├── HopTracer/          # Main MAUI app
│   ├── HopTracer.Web/      # Web backend
│   ├── HopTracer.Core/     # Core logic
│   ├── GhConverter/        # GH→GHX converter
│   └── TestDiff/           # Unit tests
│
├── Tests/data/             # Test fixtures
├── Assets/                 # Logo & images
│
└── Release/
    └── HopTracer_Portable/  # Build output
```
