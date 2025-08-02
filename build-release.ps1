# LightCrosshair Release Build Script
# This script builds optimized portable releases for x64 and ARM64 architectures

param(
    [string]$Version = "1.0.0",
    [switch]$SkipClean = $false
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "LightCrosshair"
$OutputDir = Join-Path $ScriptDir "releases"

Write-Host "LightCrosshair Release Builder v$Version" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Clean previous builds
if (-not $SkipClean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    
    # Clean project bin/obj directories
    $BinDir = Join-Path $ProjectDir "bin"
    $ObjDir = Join-Path $ProjectDir "obj"
    
    if (Test-Path $BinDir) { Remove-Item $BinDir -Recurse -Force }
    if (Test-Path $ObjDir) { Remove-Item $ObjDir -Recurse -Force }
}

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Define target architectures
$Architectures = @(
    @{ Name = "x64"; Runtime = "win-x64" },
    @{ Name = "ARM64"; Runtime = "win-arm64" }
)

foreach ($Arch in $Architectures) {
    Write-Host "Building $($Arch.Name) release..." -ForegroundColor Cyan
    
    $ArchOutputDir = Join-Path $OutputDir $Arch.Name
    New-Item -ItemType Directory -Path $ArchOutputDir -Force | Out-Null
    
    # Build the application
    $PublishArgs = @(
        "publish"
        $ProjectDir
        "--configuration", "Release"
        "--runtime", $Arch.Runtime
        "--self-contained", "true"
        "--output", $ArchOutputDir
        "/p:PublishSingleFile=true"
        "/p:PublishReadyToRun=true"
        "/p:IncludeNativeLibrariesForSelfExtract=true"
        "/p:EnableCompressionInSingleFile=true"
        "/p:Version=$Version"
        "/p:AssemblyVersion=$Version.0"
        "/p:FileVersion=$Version.0"
        "--verbosity", "minimal"
    )
    
    Write-Host "Running: dotnet $($PublishArgs -join ' ')" -ForegroundColor Gray
    
    try {
        & dotnet @PublishArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "✓ $($Arch.Name) build completed successfully" -ForegroundColor Green
        
        # Create ZIP package
        $ZipName = "LightCrosshair-v$Version-$($Arch.Name).zip"
        $ZipPath = Join-Path $OutputDir $ZipName
        
        Write-Host "Creating ZIP package: $ZipName" -ForegroundColor Yellow
        
        # Get all files in the architecture output directory
        $FilesToZip = Get-ChildItem -Path $ArchOutputDir -File
        
        # Create ZIP using .NET compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        
        if (Test-Path $ZipPath) {
            Remove-Item $ZipPath -Force
        }
        
        $Zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
        
        try {
            foreach ($File in $FilesToZip) {
                $EntryName = $File.Name
                $Entry = $Zip.CreateEntry($EntryName)
                $EntryStream = $Entry.Open()
                $FileStream = [System.IO.File]::OpenRead($File.FullName)
                
                try {
                    $FileStream.CopyTo($EntryStream)
                }
                finally {
                    $FileStream.Close()
                    $EntryStream.Close()
                }
            }
        }
        finally {
            $Zip.Dispose()
        }
        
        Write-Host "✓ ZIP package created: $ZipPath" -ForegroundColor Green
        
        # Display file sizes
        $ExeFile = Get-ChildItem -Path $ArchOutputDir -Filter "*.exe" | Select-Object -First 1
        if ($ExeFile) {
            $ExeSize = [math]::Round($ExeFile.Length / 1MB, 2)
            Write-Host "  Executable size: $ExeSize MB" -ForegroundColor Gray
        }
        
        $ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
        Write-Host "  ZIP package size: $ZipSize MB" -ForegroundColor Gray
        
    }
    catch {
        Write-Host "✗ Failed to build $($Arch.Name): $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
    
    Write-Host ""
}

# Create a combined release info file
$ReleaseInfoPath = Join-Path $OutputDir "RELEASE_INFO.txt"
$ReleaseInfo = @"
LightCrosshair v$Version - Release Information
============================================

Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")
.NET Version: $(dotnet --version)

Included Packages:
- x64 Windows (Intel/AMD 64-bit)
- ARM64 Windows (ARM 64-bit)

Features:
- Optimized for minimal performance impact during gaming
- Hardware-accelerated graphics rendering
- Efficient memory management and CPU usage
- Enhanced DPI awareness for multi-monitor setups
- Self-contained deployment (no .NET runtime required)
- Single-file executable with embedded resources

Installation:
1. Download the appropriate package for your architecture
2. Extract the ZIP file to any directory
3. Run LightCrosshair.exe
4. Configure your crosshair using the system tray icon

System Requirements:
- Windows 10 version 1809 or later
- Windows 11 (all versions)

Performance Optimizations:
- Reduced CPU usage through optimized paint refresh
- Minimized memory allocations in render loop
- Hardware acceleration for graphics operations
- Efficient event handling and background processing
- Optimized transparency and layered window operations

For support and updates, visit: https://github.com/your-repo/LightCrosshair
"@

Set-Content -Path $ReleaseInfoPath -Value $ReleaseInfo -Encoding UTF8

Write-Host "Release build completed successfully!" -ForegroundColor Green
Write-Host "Output directory: $OutputDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Available packages:" -ForegroundColor Yellow
Get-ChildItem -Path $OutputDir -Filter "*.zip" | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}
