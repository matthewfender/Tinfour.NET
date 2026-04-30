# Technology Stack

**Analysis Date:** 2026-04-30

## Languages

**Primary:**
- C# 12 - All source code (`.cs` files), modern features enabled via `LangVersion=latest`

**Secondary:**
- XAML - Avalonia UI markup language for Visualiser project (`*.axaml` files)

## Runtime

**Environment:**
- .NET 8.0 - Primary target framework for all projects except PrecisionEval
- .NET 10.0 - PrecisionEval project only (`Tinfour.PrecisionEval.csproj`)

**Package Manager:**
- NuGet - Central package management via `Directory.Packages.props`
- Central Package Management (CPM) enabled - All dependency versions defined in single location

## Frameworks

**Core:**
- Microsoft.NET.Sdk - Standard .NET SDK for class libraries and executables
- System.Collections.Generic - Built-in .NET collections

**UI/Visualization:**
- Avalonia 11.3.4 - Cross-platform UI framework for desktop and browser applications
- Avalonia.Themes.Fluent 11.3.4 - Fluent design theme for Avalonia
- Avalonia.Fonts.Inter 11.3.4 - Inter font support for Avalonia
- Avalonia.Desktop 11.3.4 - Desktop platform support
- Avalonia.iOS 11.3.4 - iOS platform support
- Avalonia.Browser 11.3.4 - Web browser support
- Avalonia.Android 11.3.4 - Android platform support
- Avalonia.Diagnostics 11.3.4 - Debug/diagnostic tools (Debug configuration only)
- Avalonia.Skia 11.3.4 - Skia rendering engine backend

**Graphics:**
- SkiaSharp 2.88.9 - Cross-platform 2D graphics library used for rendering triangulations in visualizer
- Xamarin.AndroidX.Core.SplashScreen 1.0.1.15 - Android splash screen support

**Testing:**
- xunit 2.8.1 - Unit testing framework for `Tinfour.Core.Tests` project
- Microsoft.NET.Test.Sdk 17.10.0 - Test execution infrastructure
- xunit.runner.visualstudio 2.8.1 - Visual Studio test runner integration
- coverlet.collector 6.0.2 - Code coverage collection and reporting

**Build/Dev:**
- BenchmarkDotNet 0.13.12 - Performance benchmarking framework for `Tinfour.Benchmarks` project

**MVVM/UI Utilities:**
- CommunityToolkit.Mvvm 8.4.0 - MVVM Toolkit for data binding, property notifications, and observable patterns in visualizer

## Key Dependencies

**Critical:**
- Clipper2 2.0.0 - Polygon clipping library, used in `Tinfour.Core` for constraint handling and polygon operations

**Graphics & Rendering:**
- SkiaSharp 2.88.9 - Enables high-performance 2D rendering of triangulations in the visualizer

**Framework Interdependencies:**
- Avalonia 11.3.4 - Depends on Avalonia.Skia for rendering backend
- All Avalonia packages must remain synchronized at 11.3.4 to avoid incompatibility issues

## Configuration

**Environment:**
- Implicit using statements enabled (`ImplicitUsings=enable`) across all projects
- Nullable reference types enabled (`Nullable=enable`) for type safety
- XML documentation generation enabled in Tinfour.Core (`GenerateDocumentationFile=true`)
- Compiler warnings suppressed selectively via `NoWarn` property (CS1591, CS8602, CS8604, etc.)

**Build:**
- Platform configurations: AnyCPU (primary), x64, x86
- Conditional compilation for Debug vs Release (Avalonia.Diagnostics excluded from Release)
- DebugType set to portable for both Debug and Release
- AllowUnsafeBlocks enabled in Visualiser project for Skia interop

## Platform Requirements

**Development:**
- .NET SDK 8.0 minimum (for projects targeting net8.0)
- .NET SDK 10.0 (for PrecisionEval project targeting net10.0)
- Visual Studio 2022 Version 18.3.11222.16+ (from solution metadata)
- Windows, macOS, or Linux (Avalonia supports all platforms)

**Production:**
- .NET Runtime 8.0 (for Tinfour.Core library and most applications)
- .NET Runtime 10.0 (for PrecisionEval executable)
- No platform-specific requirements for core library; UI apps depend on Avalonia platform support

## Project Composition

The solution contains 6 projects:

**Library Projects:**
- `Tinfour.Core` - Main triangulation library (net8.0)
- `Tinfour.Visualiser` - Shared UI components library (net8.0)

**Application Projects:**
- `Tinfour.Visualiser.Desktop` - Desktop visualizer app using Avalonia (net8.0, WinExe)
- `Tinfour.Benchmarks` - Performance benchmarking suite (net8.0, Exe)
- `Tinfour.PrecisionEval` - Precision evaluation utility (net10.0, Exe)

**Test Projects:**
- `Tinfour.Core.Tests` - Unit tests for core library (net8.0)

---

*Stack analysis: 2026-04-30*
