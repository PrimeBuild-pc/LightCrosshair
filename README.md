# 🎯 LightCrosshair

> **A lightweight, high-performance crosshair overlay designed for competitive gaming**

[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-6.0-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Release](https://img.shields.io/badge/Release-Latest-brightgreen)](../../releases)

LightCrosshair is a professional-grade crosshair overlay application that provides pixel-perfect accuracy and customization for gamers. With its transparent edges, vibrant neon colors, and intuitive interface, it's designed to enhance your gaming experience without impacting performance.

---

## ✨ Key Features

### 🎨 **Advanced Customization**
- **Multiple Shapes**: Cross, Circle, Dot, Plus, X, and combined shapes
- **Vibrant Colors**: Neon cyan, electric red, neon green, and custom colors
- **Transparent Edges**: Clean appearance with no unwanted borders
- **Adjustable Thickness**: 1-10 pixel edge thickness control
- **Dynamic Sizing**: 5-100% size adjustment with 5% increments

### 🚀 **Performance Optimized**
- **<1% CPU Usage**: Minimal impact during gaming sessions
- **Hardware Accelerated**: Leverages Windows' layered window optimizations
- **Smart Rendering**: Only redraws when changes are detected
- **Memory Efficient**: Optimized graphics object caching

### 🎮 **Gaming Features**
- **Pixel-Perfect Centering**: Mathematically precise positioning on all displays
- **Screen Recording Detection**: Auto-hide during streaming/recording
- **Multi-Monitor Support**: Works correctly on all display configurations
- **DPI Awareness**: Scales properly on high-DPI displays

### 🔧 **User Experience**
- **Persistent Context Menu**: Make multiple adjustments without menu closing
- **System Tray Integration**: Unobtrusive background operation
- **Profile Management**: Save and switch between multiple configurations
- **Hotkey Support**: Quick visibility toggle (Alt+X default)

---

## 📋 System Requirements

| Component | Requirement |
|-----------|-------------|
| **Operating System** | Windows 10 (1809+) or Windows 11 |
| **Architecture** | x64 (Intel/AMD 64-bit processors) |
| **Runtime** | .NET 6.0 (included in standalone builds) |
| **Memory** | 50MB RAM (typical usage) |
| **Storage** | 100MB available space |
| **Display** | Any resolution (optimized for 1080p, 1440p, 4K) |

---

## 🚀 Installation

### Option 1: Standalone Executable (Recommended)
1. **Download** the latest `LightCrosshair.exe` from the [Releases](../../releases) page
2. **Place** the executable in your preferred directory
3. **Run** `LightCrosshair.exe` - no installation required!
4. **Configure** your crosshair using the right-click context menu

### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/yourusername/LightCrosshair.git
cd LightCrosshair

# Build the application
dotnet build --configuration Release

# Or create standalone executable
dotnet publish --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true
```

---

## 🎯 Usage Guide

### **Getting Started**
1. **Launch** the application - a neon cyan cross will appear at screen center
2. **Right-click** anywhere on screen to open the context menu
3. **Customize** your crosshair using the menu options
4. **Close Menu** when finished, or click outside to dismiss

### **Context Menu Navigation**
- **Shape** → Choose from Cross, Circle, Dot, Plus, X, or combined shapes
- **Size** → Adjust from 5% to 100% in 5% increments
- **Thickness** → Set line thickness from 1-10 pixels
- **Edge Color** → Choose color and thickness for borders
- **Inner Color** → Set the main crosshair color
- **Profiles** → Save, load, and manage multiple configurations

### **Keyboard Shortcuts**
- `Alt + X` - Toggle crosshair visibility
- `Escape` - Close context menu
- Right-click - Open context menu

### **Pro Tips** 💡
- Use **transparent edge color** for clean appearance
- **Neon cyan** provides excellent visibility on all backgrounds
- **15% size** with **5px thickness** works well for most games
- Create separate profiles for different game types
- Enable **"Hide during screen recording"** for streaming

---

## 🛠️ Technical Specifications

### **Architecture**
- **Framework**: .NET 6.0 Windows Forms
- **Graphics**: GDI+ with hardware acceleration
- **Rendering**: Optimized double-buffering with anti-aliasing
- **Threading**: Asynchronous operations for UI responsiveness

### **Performance Metrics**
- **Startup Time**: <500ms (ReadyToRun optimized)
- **Memory Usage**: ~50MB baseline, stable during operation
- **CPU Impact**: <1% during idle gaming, <2% during menu operations
- **Rendering Latency**: <16ms (60+ FPS equivalent)

### **Compatibility**
- **Windows Versions**: 10 (1809+), 11 (all versions)
- **Display Scaling**: 100%, 125%, 150%, 200% DPI scaling
- **Multi-Monitor**: Primary and secondary display support
- **Gaming Software**: Compatible with OBS, XSplit, Discord overlay

---

## 🤝 Contributing

We welcome contributions from the gaming and development community! Here's how you can help:

### **Ways to Contribute**
- 🐛 **Report Bugs** - Submit detailed issue reports
- 💡 **Suggest Features** - Share ideas for new functionality
- 🔧 **Submit Code** - Fix bugs or implement new features
- 📖 **Improve Documentation** - Help make guides clearer
- 🧪 **Test Builds** - Try pre-release versions and provide feedback

### **Development Setup**
```bash
# Prerequisites
# - Visual Studio 2022 or VS Code
# - .NET 6.0 SDK
# - Git

# Clone and setup
git clone https://github.com/yourusername/LightCrosshair.git
cd LightCrosshair
dotnet restore
dotnet build
```

### **Coding Standards**
- Follow C# naming conventions
- Add XML documentation for public methods
- Include unit tests for new features
- Maintain <1% performance impact
- Test on multiple Windows versions

### **Pull Request Process**
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request with detailed description

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

### **What this means:**
- ✅ **Commercial Use** - Use in commercial projects
- ✅ **Modification** - Modify and distribute changes
- ✅ **Distribution** - Share with others freely
- ✅ **Private Use** - Use for personal projects
- ❌ **Liability** - No warranty or liability
- ❌ **Trademark** - Cannot use project trademarks

---

## 🙏 Acknowledgments

- **Gaming Community** - For feedback and feature requests
- **Open Source Contributors** - For code improvements and bug fixes
- **Beta Testers** - For helping identify and resolve issues
- **.NET Team** - For the excellent framework and tools

---

## 📞 Support & Contact

- **Issues**: [GitHub Issues](../../issues) - Bug reports and feature requests
- **Discussions**: [GitHub Discussions](../../discussions) - Community support
- **Documentation**: [Wiki](../../wiki) - Detailed guides and tutorials

---

<div align="center">

**Made with ❤️ for the gaming community**

[⭐ Star this repo](../../stargazers) • [🐛 Report Bug](../../issues) • [💡 Request Feature](../../issues) • [🤝 Contribute](../../pulls)

</div>
