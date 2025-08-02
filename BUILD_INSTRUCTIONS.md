# LightCrosshair Build Instructions

This document explains how to build optimized portable releases of LightCrosshair for distribution.

## Prerequisites

- .NET 6.0 SDK or later
- Windows 10/11 (for building Windows targets)
- PowerShell 5.1+ (recommended for advanced build script)

## Performance Optimizations Implemented

### 1. Rendering Optimizations
- **Efficient Paint Refresh**: Only redraws when profile changes are detected
- **Graphics Quality Balance**: Optimized rendering settings for performance while maintaining visual quality
- **Memory Management**: Reduced allocations in the paint loop with object caching
- **Double Buffering**: Enhanced with additional performance control styles

### 2. System Integration
- **Enhanced DPI Awareness**: Uses `PerMonitorV2` mode for optimal multi-monitor performance
- **Hardware Acceleration**: Leverages Windows' layered window optimizations
- **Efficient Event Handling**: Minimized background processing with smart invalidation

### 3. Build Optimizations
- **ReadyToRun (R2R)**: Pre-compiled for faster startup
- **Trimming**: Removes unused code to reduce file size
- **Single File**: Self-contained executable with embedded resources
- **Compression**: Reduces deployment size

## Quick Build (Batch File)

For a simple build process, use the batch file:

```cmd
build-release.bat
```

This will create:
- `releases/x64/LightCrosshair.exe` - x64 version
- `releases/ARM64/LightCrosshair.exe` - ARM64 version
- ZIP packages (if PowerShell is available)

## Advanced Build (PowerShell)

For more control and features, use the PowerShell script:

```powershell
# Basic build
.\build-release.ps1

# Custom version
.\build-release.ps1 -Version "1.2.0"

# Skip cleaning previous builds
.\build-release.ps1 -SkipClean
```

## Manual Build Commands

If you prefer to build manually:

### x64 Build
```cmd
dotnet publish LightCrosshair ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output releases/x64 ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:PublishTrimmed=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true
```

### ARM64 Build
```cmd
dotnet publish LightCrosshair ^
    --configuration Release ^
    --runtime win-arm64 ^
    --self-contained true ^
    --output releases/ARM64 ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:PublishTrimmed=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true
```

## Output Structure

After building, you'll have:

```
releases/
├── x64/
│   ├── LightCrosshair.exe          # x64 executable (~15-25 MB)
│   └── LightCrosshair.pdb          # Debug symbols (Release builds exclude this)
├── ARM64/
│   ├── LightCrosshair.exe          # ARM64 executable (~15-25 MB)
│   └── LightCrosshair.pdb          # Debug symbols (Release builds exclude this)
├── LightCrosshair-v1.0.0-x64.zip   # x64 distribution package
├── LightCrosshair-v1.0.0-ARM64.zip # ARM64 distribution package
└── RELEASE_INFO.txt                # Release information
```

## Distribution

The ZIP files are ready for distribution and contain:
- Self-contained executable (no .NET runtime required)
- Embedded icon and resources
- Optimized for minimal performance impact

## Performance Characteristics

The optimized build provides:
- **Startup Time**: ~200-500ms (thanks to ReadyToRun)
- **Memory Usage**: ~15-30 MB (depending on profile complexity)
- **CPU Usage**: <1% during idle, minimal spikes during profile changes
- **File Size**: ~15-25 MB per executable (compressed in ZIP)

## Troubleshooting

### Build Errors

1. **"SDK not found"**: Install .NET 6.0 SDK or later
2. **"Runtime not available"**: Ensure you have the Windows SDK components
3. **"Trimming warnings"**: These are usually safe to ignore for this application

### Performance Issues

1. **High CPU usage**: Check if hardware acceleration is working
2. **Slow startup**: Verify ReadyToRun compilation succeeded
3. **Large file size**: Ensure trimming and compression are enabled

## Testing the Build

1. Run the executable on a clean Windows machine (without .NET installed)
2. Test on both x64 and ARM64 systems if available
3. Verify crosshair rendering performance during gaming
4. Check system tray functionality and hotkeys

## GitHub Release

To create a GitHub release:

1. Build using the PowerShell script
2. Upload the ZIP files to GitHub Releases
3. Include the `RELEASE_INFO.txt` content in the release notes
4. Tag the release with the version number (e.g., `v1.0.0`)

## Version Management

Update version numbers in:
- `LightCrosshair.csproj` (AssemblyVersion, FileVersion, Version)
- Build scripts (default version parameter)
- This README file (examples)
