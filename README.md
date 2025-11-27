# Tinfour.NET

A high-performance C# library for Constrained Delaunay Triangulation (CDT), interpolation, and rasterization.

> **Note:** This is a partial port of the Java Tinfour library. See [Port Status](#port-status) for details on what is and isn't included.

## About

Tinfour.NET is a C# port of the [Java Tinfour library](https://github.com/gwlucastrig/Tinfour), originally developed by **G.W. Lucas**. This port focuses on the core triangulation and interpolation functionality, providing the essential building blocks for applications requiring Delaunay triangulation.

### Key Differences from Java Tinfour

| Aspect | Java Tinfour | Tinfour.NET |
|--------|-------------|-------------|
| Language | Java | C# (.NET 8+) |
| Collections | Java Collections | System.Collections.Generic |
| Geometry | Custom types | Record structs where appropriate |
| Parallelism | Java concurrency | TPL, Parallel.ForEach |
| Memory | JVM GC | Value types, struct optimizations |
| Nullable | @Nullable annotations | C# nullable reference types |

## Features

- **Constrained Delaunay Triangulation (CDT)** - Build TINs from point clouds with linear and polygon constraints
- **Multiple Interpolation Methods:**
  - Triangular Facet (planar interpolation)
  - Natural Neighbor (smooth, area-weighted)
  - Inverse Distance Weighting (IDW)
- **Rasterization** - Convert TINs to regular grids with parallel processing
- **Memory-Efficient Storage** - Float64, Float32, and Int16Scaled raster formats
- **Voronoi Diagrams** - Generate bounded Voronoi tessellations
- **Contour Generation** - Extract isolines from TIN surfaces

## Installation

Add the NuGet package to your project:

```bash
dotnet add package Tinfour.Core
```

Or via Package Manager:

```powershell
Install-Package Tinfour.Core
```

## Quick Start

### Building a TIN

```csharp
using Tinfour.Core.Common;
using Tinfour.Core.Standard;

// Create vertices with x, y, z coordinates
var vertices = new List<IVertex>
{
    new Vertex(0, 0, 10.5),
    new Vertex(100, 0, 12.3),
    new Vertex(100, 100, 8.7),
    new Vertex(0, 100, 11.2),
    new Vertex(50, 50, 15.0)
};

// Build the TIN
var tin = new IncrementalTin();
tin.Add(vertices);

// Lock for thread-safe read access
tin.Lock();

Console.WriteLine($"TIN built with {tin.GetVertices().Count} vertices");
```

### Interpolation

```csharp
using Tinfour.Core.Interpolation;

// Create an interpolator
var interpolator = InterpolatorFactory.Create(tin, InterpolationType.NaturalNeighbor);

// Interpolate at a point
double z = interpolator.Interpolate(25.0, 75.0, null);
Console.WriteLine($"Interpolated value: {z}");
```

### Rasterization

```csharp
using Tinfour.Core.Interpolation;

// Configure interpolation options
var options = new InterpolatorOptions
{
    MaxInterpolationDistance = 200.0  // Return NaN beyond 200 units from data
};

// Create rasterizer
var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor, options);

// Create a 500x500 raster using Float32 for memory efficiency
var result = rasterizer.CreateRaster(500, 500, RasterDataType.Float32);

// Access results
Console.WriteLine($"Raster size: {result.Width}x{result.Height}");
Console.WriteLine($"Coverage: {result.CoveragePercent:F1}%");

var stats = result.GetStatistics();
Console.WriteLine($"Value range: {stats.Min:F2} to {stats.Max:F2}");
```

### Working with Constraints

```csharp
using Tinfour.Core.Constraints;

// Define a boundary polygon
var boundaryVertices = new List<IVertex>
{
    new Vertex(10, 10, 0),
    new Vertex(90, 10, 0),
    new Vertex(90, 90, 0),
    new Vertex(10, 90, 0),
    new Vertex(10, 10, 0)  // Close the polygon
};

var boundary = new PolygonConstraint(boundaryVertices);

// Add to TIN with conformity restoration
tin.AddConstraints(new List<IConstraint> { boundary }, restoreConformity: true);
```

## Memory-Efficient Raster Types

Choose the appropriate raster storage type based on your needs:

| Type | Size | Use Case |
|------|------|----------|
| `Float64` | 8 bytes/cell | Maximum precision |
| `Float32` | 4 bytes/cell | General purpose (recommended) |
| `Int16Scaled` | 2 bytes/cell | Large rasters with known value range |

```csharp
// Int16Scaled example for bathymetric data
// Scale 0.01 = 1cm resolution, range approximately -327m to +327m
var result = rasterizer.CreateRaster(1000, 1000, RasterDataType.Int16Scaled,
    int16Scale: 0.01,
    int16Offset: 0.0);

// Memory usage comparison (1000x1000 raster):
// Float64: 8 MB
// Float32: 4 MB
// Int16Scaled: 2 MB
```

## Thread Safety

Tinfour.NET supports parallel processing with specific patterns:

```csharp
// Build TIN (single-threaded)
var tin = new IncrementalTin();
tin.Add(vertices);
tin.Lock();  // Mark as read-only

// Safe: Multiple rasterizers can share the locked TIN
Parallel.ForEach(tiles, tile =>
{
    var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);
    var result = rasterizer.CreateRaster(tileWidth, tileHeight, bounds: tile.Bounds);
    ProcessTile(result, tile);
});
```

See [THREAD_SAFETY.md](doc/architecture/THREAD_SAFETY.md) for complete guidelines.

## Documentation

- [Getting Started Guide](doc/getting-started/usage-guide.md)
- [Architecture Overview](doc/architecture/overview.md)
- [Interpolation Methods](doc/architecture/interpolation/interpolation-overview.md)
- [Thread Safety](doc/architecture/THREAD_SAFETY.md)
- [API Documentation](doc/README.md)

## Project Structure

```
Tinfour.Net/
├── Tinfour.Core/           # Main library
│   ├── Common/             # Core types (Vertex, Edge, etc.)
│   ├── Constraints/        # Constraint handling
│   ├── Interpolation/      # Interpolation and rasterization
│   ├── Standard/           # IncrementalTin implementation
│   └── Voronoi/            # Voronoi diagram support
├── Tinfour.Core.Tests/     # Unit tests
├── Tinfour.Benchmarks/     # Performance benchmarks
├── Tinfour.Visualiser/     # Debug visualization tool
└── doc/                    # Documentation
```

## Performance

Tinfour.NET is optimized for high-performance applications:

- **Stochastic Lawson's Walk** - O(√n) average triangle location
- **Extended Precision Arithmetic** - Robust geometric predicates
- **Parallel Rasterization** - Thread-local interpolators with shared TIN
- **Value Types** - Reduced GC pressure through struct usage

## Port Status

This is a **partial port** of the Java Tinfour library. The following table summarizes what has been ported:

### Ported (Tinfour.Core)

| Feature | Status | Notes |
|---------|--------|-------|
| Incremental Delaunay Triangulation | ✅ Complete | `IncrementalTin` class |
| Constrained Delaunay Triangulation | ✅ Complete | Linear and polygon constraints |
| Triangular Facet Interpolation | ✅ Complete | Planar interpolation |
| Natural Neighbor Interpolation | ✅ Complete | Smooth, area-weighted |
| Inverse Distance Weighting | ✅ Complete | Configurable power parameter |
| Rasterization | ✅ Complete | Parallel processing, multiple output types |
| Voronoi Diagrams | ✅ Complete | Bounded Voronoi tessellation |
| Contour Generation | ✅ Complete | Isoline extraction |
| Extended Precision Arithmetic | ✅ Complete | Robust geometric predicates |
| Stochastic Lawson's Walk | ✅ Complete | Fast triangle location |

### Not Ported

The following Java Tinfour modules have **not** been ported to .NET:

| Module | Description | Why Not Ported |
|--------|-------------|----------------|
| **GIS** | Shapefile and LAS/LAZ Lidar file support | .NET has existing libraries (e.g., NetTopologySuite, LASzip.Net) |
| **SVM** | Simple Volumetric Model for lake/reservoir capacity analysis | Specialized hydrographic feature |
| **Analysis** | Geographically Weighted Regression and statistical utilities | Experimental in Java version |
| **Demo** | Example applications and visualization tools | Application-specific |
| **Virtual TIN** | Memory-efficient TIN for very large datasets | Not yet needed |
| **Delaunay Refinement** | Ruppert's Algorithm for mesh quality | Not yet implemented |
| **Alpha Shapes** | 2D boundary analysis utilities | Not yet implemented |

### Rationale

This port focuses on the **core** module because:

1. **Core functionality covers most use cases** - Triangulation, interpolation, and rasterization are the primary needs for bathymetric and terrain modeling
2. **GIS formats have existing .NET solutions** - Libraries like NetTopologySuite, DotSpatial, and LASzip.Net handle Shapefiles and Lidar formats
3. **Minimal dependencies** - Like the Java core module, Tinfour.Core has no external dependencies
4. **Extensibility** - The core provides the foundation; specialized modules can be added as needed

If you need features from the unported modules, consider:
- Using the original Java Tinfour via IKVM.NET
- Contributing a port of the needed module
- Using .NET equivalents for GIS file format support

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE.txt](LICENSE.txt) file for details.

## Acknowledgments

This library is a C# port of the excellent [Tinfour](https://github.com/gwlucastrig/Tinfour) library by **G.W. Lucas**. The original Java implementation provides the algorithmic foundation and design principles that make this port possible.

Additional resources:
- [Tinfour Website](http://www.tinfour.org)
- [Original Tinfour Documentation](https://github.com/gwlucastrig/Tinfour/wiki)

## Contributing

Contributions are welcome! Please read our coding standards and submit pull requests to the main branch.

## Version History

- **1.0.0** - Initial port from Java Tinfour
- **1.1.0** - Phase 1 enhancements:
  - MaxInterpolationDistance support
  - Memory-efficient raster types (Float32, Int16Scaled)
  - InterpolatorOptions configuration class
  - Thread safety documentation
