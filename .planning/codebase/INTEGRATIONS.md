# External Integrations

**Analysis Date:** 2026-04-30

## APIs & External Services

**None detected** - Tinfour.NET is a standalone library with no external API dependencies.

This is intentional design: the core library (`Tinfour.Core`) has zero external dependencies and focuses on geometric computation (Delaunay triangulation, interpolation, rasterization).

## Data Storage

**Databases:**
- None - The library is in-memory only. No database clients or connection strings are used.

**File Storage:**
- Local filesystem only
  - Binary serialization via `Tinfour.Core.Serialization.TinSerializer` (see `C:/Users/matt/source/repos/Tinfour.NET/Tinfour.Core/Serialization/TinSerializer.cs`)
  - Custom binary format with magic number `0x54494E53` ("TINS")
  - Optional GZip compression support
  - No cloud storage integration

**Test Data Files:**
- CSV test data embedded as resources:
  - `AllAmLakeTrackPointsCSV.csv` - Track point data
  - `AMLakeShorelines.csv` - Shoreline constraint data
  - Located in: `C:/Users/matt/source/repos/Tinfour.NET/Tinfour.Visualiser/Tinfour.Visualiser/Assets/TestFiles/`
  - Copied to output directory (`CopyToOutputDirectory=PreserveNewest`)

**Caching:**
- None detected. Memory-based caching occurs naturally through .NET object references during TIN operations.

## Authentication & Identity

**Auth Provider:**
- None - No authentication is implemented. The library has no user/identity management.

## Monitoring & Observability

**Error Tracking:**
- None detected. No error tracking services (Sentry, Application Insights, etc.) integrated.

**Logs:**
- Standard .NET approaches only
  - `System.Diagnostics` used in rendering pipeline (see `Tinfour.Visualiser`)
  - Console output via `System.Console` in benchmarks and utilities
  - No structured logging framework (Serilog, NLog, etc.) detected

**Performance Diagnostics:**
- BenchmarkDotNet 0.13.12 in `Tinfour.Benchmarks` for performance measurement
  - Generates detailed performance reports
  - Used for optimization analysis, not runtime monitoring

## CI/CD & Deployment

**Hosting:**
- GitHub (repository) - `https://github.com/matthewfender/Tinfour.NET`
- NuGet package distribution planned for `Tinfour.Core` (package ID: `Tinfour.NET`)

**CI Pipeline:**
- Not detected in codebase analysis. Build configuration exists in projects but no GitHub Actions, Azure Pipelines, or other CI/CD infrastructure found.

**Build Process:**
- Standard `dotnet build` and `dotnet test` via project files
- NuGet package metadata configured in `Tinfour.Core.csproj`:
  - Package ID: `Tinfour.NET`
  - Version: `0.99.0-rc1` (pre-release)
  - License: Apache-2.0

## Environment Configuration

**Required env vars:**
- None detected. The library requires no environment variables.

**Secrets location:**
- Not applicable - No API keys, tokens, or secrets are used.

## Webhooks & Callbacks

**Incoming:**
- None - The library is not a server and does not accept webhooks.

**Outgoing:**
- None - The library does not make outbound webhooks or HTTP requests.

## Constraint & Geometry Dependencies

**Polygon Clipping:**
- Clipper2 2.0.0 used for constraint polygon operations
  - Purpose: Polygon clipping and manipulation during constraint handling
  - NuGet package: `Clipper2`
  - No external API, purely computational library
  - Integrated directly into constraint resolution logic in `Tinfour.Core`

## Graphics & Rendering (Visualiser Only)

**2D Rendering Backend:**
- SkiaSharp 2.88.9 (via Avalonia.Skia)
  - Purpose: High-performance rendering of triangulations, contours, and Voronoi diagrams
  - Integration: `C:/Users/matt/source/repos/Tinfour.NET/Tinfour.Visualiser/Tinfour.Visualiser/Controls/TriangulationCanvas.cs`
  - Direct Skia bindings via `SkiaSharp` NuGet package
  - No external graphics services (cloud rendering, etc.)

**UI Framework:**
- Avalonia 11.3.4
  - Cross-platform desktop framework
  - No backend services; entirely local rendering
  - Desktop support via Avalonia.Desktop

## Summary: Integration Surface

| Category | Status | Notes |
|----------|--------|-------|
| External APIs | None | Intentionally zero-dependency design |
| Databases | None | In-memory only |
| Cloud Services | None | Local-only library |
| Authentication | None | Not applicable |
| File I/O | Local filesystem | Binary serialization with compression option |
| Network | None | No network calls |
| Third-party Services | Clipper2 (polygon ops) | Local computational library |
| Monitoring | None | Performance benchmarking tool available but not integrated |

**Design Philosophy:** Tinfour.NET is a pure computational library focused on geometric algorithms with minimal external dependencies. All integration points are either local (file I/O, in-memory state) or self-contained computational libraries (Clipper2). This design maximizes portability and minimizes deployment complexity.

---

*Integration audit: 2026-04-30*
