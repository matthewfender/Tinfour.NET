# Tinfour.NET TPL Dataflow Integration Design

**Version:** 1.1
**Date:** November 27, 2025
**Status:** Design Review

## Executive Summary

This document outlines the assembly structure and API design for integrating Tinfour.NET with ReefMaster v3 (Avalonia/.NET 10) via TPL Dataflow. The design maintains Tinfour.Core as a pure triangulation/interpolation library while providing a clean integration layer for pipeline processing.

### Key Design Decisions (from Review)

1. **Coordinate projection**: Handled by ReefMaster before passing to Tinfour (no spatial projection dependencies)
2. **Raster format**: Flexible output types (float, short, double) with configurable scaling - `double[,]` too heavy for large maps
3. **Max interpolation distance**: Phase 2 addition to Tinfour.Core
4. **TIN lifecycle**: Deferred - depends on downstream usage patterns
5. **Contour generation**: Handled by ReefMaster using its existing raster-based smoothed contour code

---

## 1. Current State Assessment

### 1.1 Tinfour.Core Readiness

| Aspect | Status | Notes |
|--------|--------|-------|
| **Triangulation (CDT)** | ✅ Production Ready | Bowyer-Watson algorithm, O(n log n) with Hilbert sort |
| **Interpolation** | ✅ Production Ready | 3 methods: Triangular Facet, Natural Neighbor, IDW |
| **Rasterization** | ✅ Production Ready | `TinRasterizer` with parallel processing, `CancellationToken` support |
| **Contour Generation** | ✅ Production Ready | `ContourBuilderForTin` with regions |
| **Thread Safety** | ✅ Read-only operations | TIN modifications require exclusive access |
| **Memory Efficiency** | ✅ Optimized | Struct vertices, edge pooling, Span<T> usage |
| **Cancellation Support** | ⚠️ Partial | `TinRasterizer` supports it; triangulation does not |

### 1.2 Current Public API Surface

```
Tinfour.Core
├── Common/
│   ├── Vertex (struct)           # Input point data
│   ├── IVertex (interface)       # Vertex abstraction
│   ├── IConstraint               # Constraint abstraction
│   ├── LinearConstraint          # Edge chain constraint
│   ├── PolygonConstraint         # Area constraint (shorelines)
│   └── IIncrementalTin           # Main TIN interface
├── Standard/
│   └── IncrementalTin            # TIN implementation
├── Interpolation/
│   ├── InterpolationType         # Enum: TriangularFacet, NaturalNeighbor, IDW
│   ├── TinRasterizer             # Grid generation
│   ├── RasterResult              # Output structure
│   └── IInterpolatorOverTin      # Interpolator interface
└── Contour/
    ├── ContourBuilderForTin      # Contour extraction
    ├── Contour                   # Isoline representation
    └── ContourRegion             # Polygon region
```

---

## 2. Recommended Assembly Structure

### 2.1 Assembly Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              ReefMaster                                  │
│  (Avalonia/.NET 10 - Bathymetric Mapping)                               │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ References
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      Tinfour.Dataflow (NEW)                             │
│  TPL Dataflow blocks, pipeline builders, ReefMaster-specific wrappers   │
│  Dependencies: Tinfour.Core, System.Threading.Tasks.Dataflow            │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ References
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          Tinfour.Core                                   │
│  Pure triangulation, interpolation, contour generation                  │
│  No Dataflow dependencies - remains clean and reusable                  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Assembly Responsibilities

#### Tinfour.Core (Keep Pure)
- Constrained Delaunay Triangulation
- Interpolation algorithms
- Rasterization
- Contour/Voronoi generation
- **No TPL Dataflow dependencies**
- **No ReefMaster-specific code**

#### Tinfour.Dataflow (New Assembly)
- TPL Dataflow block implementations
- Pipeline builder fluent API
- Progress reporting infrastructure
- Input/output DTOs for pipeline stages
- Parallelization strategies
- ReefMaster integration helpers (optional namespace)

---

## 3. Data Types for Pipeline Stages

### 3.1 Input DTOs

```csharp
namespace Tinfour.Dataflow.Messages;

/// <summary>
/// Input data for triangulation stage.
/// Immutable record for safe pipeline passing.
/// </summary>
public sealed record TriangulationInput
{
    /// <summary>
    /// Vertices with X, Y (coordinates) and Z (depth).
    /// For ReefMaster: typically lat/lon projected to local coordinates.
    /// </summary>
    public required IReadOnlyList<VertexData> Vertices { get; init; }

    /// <summary>
    /// Optional constraints (shorelines, boundaries).
    /// </summary>
    public IReadOnlyList<ConstraintData>? Constraints { get; init; }

    /// <summary>
    /// Nominal spacing between vertices (affects precision thresholds).
    /// For bathymetry, often derived from survey line spacing.
    /// </summary>
    public double NominalPointSpacing { get; init; } = 1.0;

    /// <summary>
    /// Application-specific metadata to flow through pipeline.
    /// </summary>
    public object? Metadata { get; init; }
}

/// <summary>
/// Simple vertex data transfer object.
/// Separates ReefMaster's internal representation from Tinfour's Vertex struct.
/// </summary>
public readonly record struct VertexData(double X, double Y, double Z, int Index = 0);

/// <summary>
/// Constraint data transfer object.
/// </summary>
public sealed record ConstraintData
{
    public required IReadOnlyList<VertexData> Vertices { get; init; }
    public ConstraintType Type { get; init; } = ConstraintType.Linear;
    public bool DefinesRegion { get; init; } = false;
    public object? ApplicationData { get; init; }
}

public enum ConstraintType { Linear, Polygon }
```

### 3.2 Intermediate DTOs

```csharp
/// <summary>
/// Output from triangulation stage, input to interpolation/analysis stages.
/// Wraps the TIN but provides metadata and statistics.
/// </summary>
public sealed record TriangulationResult
{
    /// <summary>
    /// The constructed TIN. Thread-safe for read operations.
    /// </summary>
    public required IIncrementalTin Tin { get; init; }

    /// <summary>
    /// Bounding box in world coordinates.
    /// </summary>
    public required BoundsData Bounds { get; init; }

    /// <summary>
    /// Statistics about the triangulation.
    /// </summary>
    public required TriangulationStats Statistics { get; init; }

    /// <summary>
    /// Metadata passed through from input.
    /// </summary>
    public object? Metadata { get; init; }
}

public readonly record struct BoundsData(double Left, double Top, double Width, double Height);

public sealed record TriangulationStats
{
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
    public int EdgeCount { get; init; }
    public int ConstraintCount { get; init; }
    public TimeSpan BuildDuration { get; init; }
}
```

### 3.3 Interpolation Configuration

```csharp
/// <summary>
/// Configuration for rasterization/interpolation stage.
/// </summary>
public sealed record InterpolationConfig
{
    /// <summary>
    /// Interpolation algorithm to use.
    /// </summary>
    public InterpolationType Method { get; init; } = InterpolationType.NaturalNeighbor;

    /// <summary>
    /// Output raster width in pixels. If 0, calculated from CellSize.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Output raster height in pixels. If 0, calculated from CellSize.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Cell size in world units. Used if Width/Height are 0.
    /// </summary>
    public double CellSize { get; init; }

    /// <summary>
    /// Custom bounds for rasterization. If null, uses TIN bounds.
    /// </summary>
    public BoundsData? Bounds { get; init; }

    /// <summary>
    /// If true, only interpolate within constrained regions.
    /// Useful for shoreline-bounded bathymetry.
    /// </summary>
    public bool ConstrainedRegionsOnly { get; init; } = false;

    /// <summary>
    /// Maximum distance from nearest vertex for valid interpolation.
    /// Points beyond this distance return NaN. 0 = unlimited.
    /// </summary>
    public double MaxInterpolationDistance { get; init; } = 0;
}
```

### 3.4 Output DTOs - Flexible Raster Types

Given the scale requirements (40km @ 1m = 40,000 x 40,000 = 1.6 billion cells), we need flexible output types:

```csharp
/// <summary>
/// Specifies the output data type for rasterization.
/// </summary>
public enum RasterDataType
{
    /// <summary>Float32 - good balance of precision and memory (4 bytes/cell)</summary>
    Float32,

    /// <summary>Float64 - full precision (8 bytes/cell) - use sparingly for large rasters</summary>
    Float64,

    /// <summary>Int16 with scaling - efficient for bathymetry (2 bytes/cell)</summary>
    Int16Scaled,

    /// <summary>UInt16 with offset/scaling - for positive-only depths (2 bytes/cell)</summary>
    UInt16Scaled
}

/// <summary>
/// Configuration for scaled integer output.
/// For bathymetry: Scale=100 gives centimeter precision, Offset=0 for depths below datum.
/// Range: Int16 with Scale=100 → ±327.67m
/// </summary>
public sealed record ScalingConfig
{
    /// <summary>Scale factor: stored_value = real_value * Scale</summary>
    public double Scale { get; init; } = 100.0;  // cm precision

    /// <summary>Offset applied before scaling: stored = (real + Offset) * Scale</summary>
    public double Offset { get; init; } = 0.0;

    /// <summary>Value to use for no-data cells</summary>
    public int NoDataValue { get; init; } = short.MinValue;  // -32768
}

/// <summary>
/// Generic raster output that can hold different data types.
/// For large rasters, prefer Float32 or Int16Scaled over Float64.
/// </summary>
public abstract class RasterOutputBase
{
    public required BoundsData Bounds { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double CellWidth { get; init; }
    public double CellHeight { get; init; }
    public required RasterStats Statistics { get; init; }
    public object? Metadata { get; init; }

    /// <summary>The data type of this raster</summary>
    public abstract RasterDataType DataType { get; }

    /// <summary>Memory footprint in bytes</summary>
    public abstract long MemoryBytes { get; }

    /// <summary>Get value at position as double (for compatibility)</summary>
    public abstract double GetValue(int x, int y);

    /// <summary>Check if cell is no-data</summary>
    public abstract bool IsNoData(int x, int y);
}

/// <summary>
/// Float32 raster - recommended for most use cases.
/// 40,000 x 40,000 @ 4 bytes = 6.4 GB
/// </summary>
public sealed class RasterOutputFloat32 : RasterOutputBase
{
    public required float[,] Data { get; init; }
    public override RasterDataType DataType => RasterDataType.Float32;
    public override long MemoryBytes => (long)Width * Height * sizeof(float);
    public override double GetValue(int x, int y) => Data[x, y];
    public override bool IsNoData(int x, int y) => float.IsNaN(Data[x, y]);
}

/// <summary>
/// Int16 scaled raster - most memory efficient for bathymetry.
/// 40,000 x 40,000 @ 2 bytes = 3.2 GB
/// </summary>
public sealed class RasterOutputInt16Scaled : RasterOutputBase
{
    public required short[,] Data { get; init; }
    public required ScalingConfig Scaling { get; init; }
    public override RasterDataType DataType => RasterDataType.Int16Scaled;
    public override long MemoryBytes => (long)Width * Height * sizeof(short);

    public override double GetValue(int x, int y)
    {
        var stored = Data[x, y];
        if (stored == Scaling.NoDataValue) return double.NaN;
        return (stored / Scaling.Scale) - Scaling.Offset;
    }

    public override bool IsNoData(int x, int y) => Data[x, y] == Scaling.NoDataValue;
}

/// <summary>
/// Float64 raster - use only when precision is critical and size is manageable.
/// </summary>
public sealed class RasterOutputFloat64 : RasterOutputBase
{
    public required double[,] Data { get; init; }
    public override RasterDataType DataType => RasterDataType.Float64;
    public override long MemoryBytes => (long)Width * Height * sizeof(double);
    public override double GetValue(int x, int y) => Data[x, y];
    public override bool IsNoData(int x, int y) => double.IsNaN(Data[x, y]);
}

public sealed record RasterStats
{
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public double MeanValue { get; init; }
    public double StdDev { get; init; }
    public double CoveragePercent { get; init; }
    public int NoDataCount { get; init; }
    public TimeSpan InterpolationDuration { get; init; }
}
```

### 3.5 Memory Comparison for Large Rasters

| Dimensions | Float64 | Float32 | Int16 | Notes |
|------------|---------|---------|-------|-------|
| 1,000 × 1,000 | 8 MB | 4 MB | 2 MB | Small survey |
| 10,000 × 10,000 | 800 MB | 400 MB | 200 MB | Medium map |
| 20,000 × 20,000 | 3.2 GB | 1.6 GB | 800 MB | Large map |
| 40,000 × 40,000 | 12.8 GB | 6.4 GB | 3.2 GB | Extreme (40km @ 1m) |

**Recommendation**: Use `Float32` as default, `Int16Scaled` for very large bathymetric maps where ±327m range with cm precision is acceptable.

---

## 4. TPL Dataflow Block Implementations

### 4.1 Triangulation Block

```csharp
namespace Tinfour.Dataflow.Blocks;

/// <summary>
/// TPL Dataflow block that builds a TIN from vertices and constraints.
/// </summary>
public static class TriangulationBlock
{
    /// <summary>
    /// Creates a TransformBlock that performs triangulation.
    /// </summary>
    public static TransformBlock<TriangulationInput, TriangulationResult> Create(
        ExecutionDataflowBlockOptions? options = null,
        IProgress<TriangulationProgress>? progress = null)
    {
        options ??= new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,  // TIN building is single-threaded
            BoundedCapacity = 4
        };

        return new TransformBlock<TriangulationInput, TriangulationResult>(
            input => BuildTriangulation(input, progress),
            options);
    }

    private static TriangulationResult BuildTriangulation(
        TriangulationInput input,
        IProgress<TriangulationProgress>? progress)
    {
        var sw = Stopwatch.StartNew();

        // Create TIN with appropriate spacing
        var tin = new IncrementalTin(input.NominalPointSpacing);

        // Pre-allocate for performance
        tin.PreAllocateForVertices(input.Vertices.Count);

        progress?.Report(new TriangulationProgress(
            TriangulationPhase.AddingVertices, 0, input.Vertices.Count));

        // Convert DTOs to Tinfour vertices
        var vertices = input.Vertices
            .Select((v, i) => (IVertex)new Vertex(v.X, v.Y, v.Z, v.Index != 0 ? v.Index : i))
            .ToList();

        // Add with Hilbert sorting for optimal performance
        tin.AddSorted(vertices);

        progress?.Report(new TriangulationProgress(
            TriangulationPhase.AddingVertices, input.Vertices.Count, input.Vertices.Count));

        // Add constraints if present
        if (input.Constraints is { Count: > 0 })
        {
            progress?.Report(new TriangulationProgress(
                TriangulationPhase.AddingConstraints, 0, input.Constraints.Count));

            var constraints = input.Constraints
                .Select(CreateConstraint)
                .ToList();

            tin.AddConstraints(constraints, restoreConformity: true);

            progress?.Report(new TriangulationProgress(
                TriangulationPhase.AddingConstraints,
                input.Constraints.Count,
                input.Constraints.Count));
        }

        sw.Stop();

        var bounds = tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");
        var triangleCount = tin.CountTriangles();

        return new TriangulationResult
        {
            Tin = tin,
            Bounds = new BoundsData(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
            Statistics = new TriangulationStats
            {
                VertexCount = tin.GetVertices().Count,
                TriangleCount = triangleCount.InteriorRegionCount,
                EdgeCount = tin.GetEdges().Count,
                ConstraintCount = tin.GetConstraints().Count,
                BuildDuration = sw.Elapsed
            },
            Metadata = input.Metadata
        };
    }

    private static IConstraint CreateConstraint(ConstraintData data)
    {
        var vertices = data.Vertices
            .Select(v => (IVertex)new Vertex(v.X, v.Y, v.Z, v.Index))
            .ToList();

        return data.Type switch
        {
            ConstraintType.Polygon => new PolygonConstraint(vertices, data.DefinesRegion),
            _ => new LinearConstraint(vertices, data.ApplicationData)
        };
    }
}

public sealed record TriangulationProgress(
    TriangulationPhase Phase,
    int Current,
    int Total);

public enum TriangulationPhase
{
    AddingVertices,
    AddingConstraints,
    Complete
}
```

### 4.2 Interpolation Block

```csharp
/// <summary>
/// TPL Dataflow block that rasterizes a TIN using specified interpolation.
/// </summary>
public static class InterpolationBlock
{
    /// <summary>
    /// Creates a TransformBlock that performs interpolation/rasterization.
    /// Supports parallel rasterization internally.
    /// </summary>
    public static TransformBlock<(TriangulationResult Tin, InterpolationConfig Config), RasterOutput> Create(
        ExecutionDataflowBlockOptions? options = null,
        IProgress<InterpolationProgress>? progress = null)
    {
        options ??= new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,  // Internal parallelism in TinRasterizer
            BoundedCapacity = 2
        };

        return new TransformBlock<(TriangulationResult Tin, InterpolationConfig Config), RasterOutput>(
            input => Rasterize(input.Tin, input.Config, progress, CancellationToken.None),
            options);
    }

    /// <summary>
    /// Creates a TransformBlock with cancellation support.
    /// </summary>
    public static TransformBlock<(TriangulationResult Tin, InterpolationConfig Config), RasterOutput> Create(
        CancellationToken cancellationToken,
        ExecutionDataflowBlockOptions? options = null,
        IProgress<InterpolationProgress>? progress = null)
    {
        options ??= new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            BoundedCapacity = 2,
            CancellationToken = cancellationToken
        };

        return new TransformBlock<(TriangulationResult Tin, InterpolationConfig Config), RasterOutput>(
            input => Rasterize(input.Tin, input.Config, progress, cancellationToken),
            options);
    }

    private static RasterOutput Rasterize(
        TriangulationResult tinResult,
        InterpolationConfig config,
        IProgress<InterpolationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        progress?.Report(new InterpolationProgress(InterpolationPhase.Initializing, 0));

        var rasterizer = new TinRasterizer(
            tinResult.Tin,
            config.Method,
            config.ConstrainedRegionsOnly);

        progress?.Report(new InterpolationProgress(InterpolationPhase.Interpolating, 0));

        // Determine bounds
        var bounds = config.Bounds.HasValue
            ? (config.Bounds.Value.Left, config.Bounds.Value.Top,
               config.Bounds.Value.Width, config.Bounds.Value.Height)
            : (tinResult.Bounds.Left, tinResult.Bounds.Top,
               tinResult.Bounds.Width, tinResult.Bounds.Height);

        // Create raster
        RasterResult result;
        if (config.CellSize > 0)
        {
            result = rasterizer.CreateRaster(config.CellSize, cancellationToken);
        }
        else
        {
            result = rasterizer.CreateRaster(
                config.Width,
                config.Height,
                bounds,
                cancellationToken);
        }

        sw.Stop();

        progress?.Report(new InterpolationProgress(InterpolationPhase.Complete, 100));

        var stats = result.GetStatistics();

        return new RasterOutput
        {
            Data = result.Data,
            Bounds = new BoundsData(result.Bounds.Left, result.Bounds.Top,
                                    result.Bounds.Width, result.Bounds.Height),
            Width = result.Width,
            Height = result.Height,
            CellWidth = result.CellWidth,
            CellHeight = result.CellHeight,
            Statistics = new RasterStats
            {
                MinValue = stats.Min,
                MaxValue = stats.Max,
                MeanValue = stats.Mean,
                StdDev = stats.StdDev,
                CoveragePercent = result.CoveragePercent,
                NoDataCount = result.NoDataCount,
                InterpolationDuration = sw.Elapsed
            },
            Metadata = tinResult.Metadata
        };
    }
}

public sealed record InterpolationProgress(InterpolationPhase Phase, int PercentComplete);

public enum InterpolationPhase
{
    Initializing,
    Interpolating,
    Complete
}
```

### 4.3 Pipeline Builder

```csharp
namespace Tinfour.Dataflow;

/// <summary>
/// Fluent builder for constructing Tinfour processing pipelines.
/// </summary>
public class TinfourPipelineBuilder
{
    private ExecutionDataflowBlockOptions? _triangulationOptions;
    private ExecutionDataflowBlockOptions? _interpolationOptions;
    private InterpolationConfig? _interpolationConfig;
    private IProgress<PipelineProgress>? _progress;
    private CancellationToken _cancellationToken = CancellationToken.None;

    /// <summary>
    /// Configure triangulation block options.
    /// </summary>
    public TinfourPipelineBuilder WithTriangulationOptions(ExecutionDataflowBlockOptions options)
    {
        _triangulationOptions = options;
        return this;
    }

    /// <summary>
    /// Configure interpolation block options.
    /// </summary>
    public TinfourPipelineBuilder WithInterpolationOptions(ExecutionDataflowBlockOptions options)
    {
        _interpolationOptions = options;
        return this;
    }

    /// <summary>
    /// Configure the interpolation/rasterization parameters.
    /// </summary>
    public TinfourPipelineBuilder WithInterpolation(InterpolationConfig config)
    {
        _interpolationConfig = config;
        return this;
    }

    /// <summary>
    /// Configure progress reporting.
    /// </summary>
    public TinfourPipelineBuilder WithProgress(IProgress<PipelineProgress> progress)
    {
        _progress = progress;
        return this;
    }

    /// <summary>
    /// Configure cancellation.
    /// </summary>
    public TinfourPipelineBuilder WithCancellation(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>
    /// Build a complete pipeline from input to raster output.
    /// </summary>
    public TinfourPipeline Build()
    {
        var config = _interpolationConfig
            ?? throw new InvalidOperationException("Interpolation config required");

        // Create blocks
        var triangulationBlock = TriangulationBlock.Create(
            _triangulationOptions,
            _progress != null ? new TriangulationProgressAdapter(_progress) : null);

        var configBlock = new TransformBlock<TriangulationResult, (TriangulationResult, InterpolationConfig)>(
            tin => (tin, config),
            new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });

        var interpolationBlock = InterpolationBlock.Create(
            _cancellationToken,
            _interpolationOptions,
            _progress != null ? new InterpolationProgressAdapter(_progress) : null);

        // Link blocks
        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        triangulationBlock.LinkTo(configBlock, linkOptions);
        configBlock.LinkTo(interpolationBlock, linkOptions);

        return new TinfourPipeline(triangulationBlock, interpolationBlock);
    }
}

/// <summary>
/// Represents a complete Tinfour processing pipeline.
/// </summary>
public class TinfourPipeline
{
    private readonly ITargetBlock<TriangulationInput> _input;
    private readonly ISourceBlock<RasterOutput> _output;

    internal TinfourPipeline(
        ITargetBlock<TriangulationInput> input,
        ISourceBlock<RasterOutput> output)
    {
        _input = input;
        _output = output;
    }

    /// <summary>
    /// Input block for posting triangulation requests.
    /// </summary>
    public ITargetBlock<TriangulationInput> Input => _input;

    /// <summary>
    /// Output block for receiving raster results.
    /// </summary>
    public ISourceBlock<RasterOutput> Output => _output;

    /// <summary>
    /// Process a single input and return the result.
    /// </summary>
    public async Task<RasterOutput> ProcessAsync(
        TriangulationInput input,
        CancellationToken cancellationToken = default)
    {
        await _input.SendAsync(input, cancellationToken);
        _input.Complete();
        return await _output.ReceiveAsync(cancellationToken);
    }
}

public sealed record PipelineProgress(
    PipelineStage Stage,
    string Message,
    int PercentComplete);

public enum PipelineStage
{
    Triangulating,
    Interpolating,
    Complete
}
```

---

## 5. Parallelization Opportunities

### 5.1 Current Parallelization in Tinfour.Core

| Component | Parallelization | Notes |
|-----------|-----------------|-------|
| `IncrementalTin.Add()` | ❌ Sequential | Vertex insertion modifies TIN state |
| `TinRasterizer` | ✅ `Parallel.For` | Row-based distribution with thread-local interpolators |
| `ContourBuilderForTin` | ❌ Sequential | Edge traversal is inherently sequential |
| Read operations on TIN | ✅ Thread-safe | Multiple threads can interpolate simultaneously |

### 5.2 Dataflow Parallelization Strategy

```
┌─────────────────────────────────────────────────────────────────────┐
│ ReefMaster Feed: Multiple survey files                               │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ Multiple inputs queued
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ TriangulationBlock (MaxDegreeOfParallelism = 1 per block)           │
│ • Single-threaded TIN construction                                   │
│ • Can run MULTIPLE blocks in parallel for different surveys          │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ InterpolationBlock (Internal parallelism via TinRasterizer)          │
│ • Parallel.For across CPU cores within each rasterization           │
│ • Thread-local interpolators for each row band                       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ ReefMaster: Post-processing (contours, flow analysis)               │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.3 Multiple Survey Parallel Processing

For processing multiple surveys simultaneously:

```csharp
public static class ParallelSurveyProcessor
{
    public static async Task<IEnumerable<RasterOutput>> ProcessSurveysAsync(
        IEnumerable<TriangulationInput> surveys,
        InterpolationConfig config,
        int maxParallelSurveys = 2,
        CancellationToken cancellationToken = default)
    {
        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = maxParallelSurveys,
            BoundedCapacity = maxParallelSurveys * 2,
            CancellationToken = cancellationToken
        };

        var processorBlock = new TransformBlock<TriangulationInput, RasterOutput>(
            async input =>
            {
                var pipeline = new TinfourPipelineBuilder()
                    .WithInterpolation(config)
                    .WithCancellation(cancellationToken)
                    .Build();

                return await pipeline.ProcessAsync(input, cancellationToken);
            },
            options);

        var results = new List<RasterOutput>();
        var outputBlock = new ActionBlock<RasterOutput>(
            output => results.Add(output),
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        processorBlock.LinkTo(outputBlock, new DataflowLinkOptions { PropagateCompletion = true });

        foreach (var survey in surveys)
        {
            await processorBlock.SendAsync(survey, cancellationToken);
        }

        processorBlock.Complete();
        await outputBlock.Completion;

        return results;
    }
}
```

---

## 6. ReefMaster Integration Example

### 6.1 Simple Usage

```csharp
// In ReefMaster
public class BathymetryProcessor
{
    public async Task<RasterOutput> ProcessSurveyAsync(
        IEnumerable<SurveyPoint> surveyPoints,
        IEnumerable<ShorelinePolygon> shorelines,
        ProcessingOptions options,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        // Convert ReefMaster types to Tinfour DTOs
        var input = new TriangulationInput
        {
            Vertices = surveyPoints
                .Select((p, i) => new VertexData(p.X, p.Y, p.Depth, i))
                .ToList(),
            Constraints = shorelines
                .Select(s => new ConstraintData
                {
                    Vertices = s.Points.Select(p => new VertexData(p.X, p.Y, 0)).ToList(),
                    Type = ConstraintType.Polygon,
                    DefinesRegion = true
                })
                .ToList(),
            NominalPointSpacing = options.PointSpacing,
            Metadata = new { SurveyId = options.SurveyId }
        };

        var config = new InterpolationConfig
        {
            Method = options.UseNaturalNeighbor
                ? InterpolationType.NaturalNeighbor
                : InterpolationType.TriangularFacet,
            CellSize = options.RasterCellSize,
            ConstrainedRegionsOnly = options.RestrictToShoreline
        };

        var pipeline = new TinfourPipelineBuilder()
            .WithInterpolation(config)
            .WithProgress(new Progress<PipelineProgress>(p =>
                progress.Report($"{p.Stage}: {p.PercentComplete}%")))
            .WithCancellation(cancellationToken)
            .Build();

        return await pipeline.ProcessAsync(input, cancellationToken);
    }
}
```

### 6.2 Advanced: Streaming Pipeline

```csharp
// For continuous processing of survey tiles
public class StreamingBathymetryPipeline : IDisposable
{
    private readonly BufferBlock<TriangulationInput> _inputBuffer;
    private readonly ActionBlock<RasterOutput> _outputHandler;
    private readonly TinfourPipeline _pipeline;

    public StreamingBathymetryPipeline(
        Action<RasterOutput> outputHandler,
        InterpolationConfig config)
    {
        _inputBuffer = new BufferBlock<TriangulationInput>(
            new DataflowBlockOptions { BoundedCapacity = 10 });

        _pipeline = new TinfourPipelineBuilder()
            .WithInterpolation(config)
            .Build();

        _outputHandler = new ActionBlock<RasterOutput>(outputHandler);

        // Wire up
        _inputBuffer.LinkTo(_pipeline.Input, new DataflowLinkOptions { PropagateCompletion = true });
        _pipeline.Output.LinkTo(_outputHandler, new DataflowLinkOptions { PropagateCompletion = true });
    }

    public Task<bool> SubmitAsync(TriangulationInput input, CancellationToken ct = default)
        => _inputBuffer.SendAsync(input, ct);

    public void Complete() => _inputBuffer.Complete();

    public Task Completion => _outputHandler.Completion;

    public void Dispose()
    {
        Complete();
    }
}
```

---

## 7. Additional Considerations

### 7.1 Memory Management for Large Datasets

```csharp
/// <summary>
/// Options for controlling memory usage with large datasets.
/// </summary>
public sealed record LargeDatasetOptions
{
    /// <summary>
    /// Maximum vertices to process in a single TIN.
    /// Larger datasets should be tiled.
    /// </summary>
    public int MaxVerticesPerTin { get; init; } = 1_000_000;

    /// <summary>
    /// Overlap between tiles (as fraction of tile size).
    /// Needed to avoid edge artifacts.
    /// </summary>
    public double TileOverlap { get; init; } = 0.1;

    /// <summary>
    /// Dispose TIN after rasterization to free memory.
    /// </summary>
    public bool DisposeTinAfterRasterization { get; init; } = true;
}
```

### 7.2 Error Handling

```csharp
/// <summary>
/// Exception wrapper for pipeline errors with context.
/// </summary>
public class TinfourPipelineException : Exception
{
    public PipelineStage FailedStage { get; }
    public object? Metadata { get; }

    public TinfourPipelineException(
        PipelineStage stage,
        string message,
        Exception? inner = null,
        object? metadata = null)
        : base($"Pipeline failed at {stage}: {message}", inner)
    {
        FailedStage = stage;
        Metadata = metadata;
    }
}
```

### 7.3 Future Extensions

The design allows for future stages:

```csharp
// Future: Contour generation block
public static class ContourBlock
{
    public static TransformBlock<TriangulationResult, ContourOutput> Create(
        double[] contourLevels,
        bool buildRegions = true);
}

// Future: Flow analysis block (for ReefMaster drainage analysis)
public static class FlowAnalysisBlock
{
    public static TransformBlock<RasterOutput, FlowOutput> Create(
        FlowAnalysisConfig config);
}
```

---

## 8. Recommended Implementation Order

1. **Phase 1: Core DTOs** - Define `TriangulationInput`, `RasterOutput`, etc.
2. **Phase 2: Basic Blocks** - `TriangulationBlock`, `InterpolationBlock`
3. **Phase 3: Pipeline Builder** - Fluent API for pipeline construction
4. **Phase 4: Progress Reporting** - `IProgress<T>` integration
5. **Phase 5: ReefMaster Integration** - Wrapper helpers if needed
6. **Phase 6: Parallel Survey Processing** - Multi-survey support
7. **Phase 7: Streaming Pipeline** - Continuous processing support

---

## 9. ReefMaster v3 Integration Specifics

Based on analysis of the ReefMaster v3 codebase (Avalonia/.NET 10):

### 9.1 Data Flow: ReefMaster → Tinfour

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ReefMaster Trail Data                                                    │
│ • IAsyncEnumerable<LightweightTrailPoint> from Trail.GetAllPointsAsync()│
│ • LightweightTrailPoint: 24 bytes (lat: f64, lon: f64, time: u32, depth: i32 cm)
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ Stream + Project
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Coordinate Projection (ReefMaster responsibility)                        │
│ • IDisplayProjection.ProjectForwards(lat/lon) → (x, y) meters           │
│ • Web Mercator (EPSG:3857) or UTM for local accuracy                    │
│ • Depth: cm → meters (divide by 100)                                    │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ VertexData[]
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Tinfour.Dataflow Pipeline                                               │
│ • Triangulation → Interpolation → RasterOutput                          │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ RasterOutputInt16Scaled or Float32
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ ReefMaster Grid Storage                                                  │
│ • Store in PointDataLOD table (existing blob format) or new GridData   │
│ • Apply LOD pyramid for multi-scale rendering                           │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ ReefMaster Visualization                                                 │
│ • SkiaSharp GPU rendering with palette-based coloring                   │
│ • Contour/isobath generation from raster (existing RM code)             │
└─────────────────────────────────────────────────────────────────────────┘
```

### 9.2 LightweightTrailPoint Adapter

```csharp
namespace ReefMaster.Interpolation.Tinfour;

/// <summary>
/// Converts ReefMaster trail data to Tinfour input format.
/// Handles coordinate projection and streaming.
/// </summary>
public static class TrailPointAdapter
{
    /// <summary>
    /// Stream trail points to Tinfour vertices with projection.
    /// </summary>
    public static async Task<TriangulationInput> CreateInputAsync(
        Trail trail,
        IDisplayProjection projection,
        IReadOnlyList<PolygonConstraint>? shorelines = null,
        CancellationToken ct = default)
    {
        var vertices = new List<VertexData>();
        var index = 0;

        await foreach (var point in trail.GetAllPointsAsync(ct))
        {
            // Skip invalid depths
            if (point.DepthCm <= 0) continue;

            // Project lat/lon to local meters
            var projected = projection.ProjectForwards(
                new Point(point.Latitude, point.Longitude));

            vertices.Add(new VertexData(
                X: projected.X,
                Y: projected.Y,
                Z: point.DepthCm / 100.0,  // cm → meters
                Index: index++));
        }

        // Calculate nominal spacing from point density
        var extent = trail.Extent;
        var projectedExtent = projection.ProjectExtent(extent);
        var area = projectedExtent.Width * projectedExtent.Height;
        var nominalSpacing = Math.Sqrt(area / vertices.Count);

        return new TriangulationInput
        {
            Vertices = vertices,
            Constraints = shorelines?.Select(s => CreateConstraint(s, projection)).ToList(),
            NominalPointSpacing = nominalSpacing,
            Metadata = new TrailMetadata(trail.Id, trail.Name)
        };
    }

    private static ConstraintData CreateConstraint(
        PolygonConstraint shoreline,
        IDisplayProjection projection)
    {
        var vertices = shoreline.Points
            .Select(p => projection.ProjectForwards(p))
            .Select((proj, i) => new VertexData(proj.X, proj.Y, 0, i))
            .ToList();

        return new ConstraintData
        {
            Vertices = vertices,
            Type = ConstraintType.Polygon,
            DefinesRegion = true,
            ApplicationData = shoreline.Id
        };
    }
}

public sealed record TrailMetadata(Guid TrailId, string TrailName);
```

### 9.3 Grid Storage Integration

ReefMaster uses a binary blob format for point storage. For interpolated grids:

```csharp
/// <summary>
/// Stores interpolated grid in ReefMaster's data layer.
/// Compatible with existing PointDataLOD infrastructure.
/// </summary>
public sealed class GridDataEntity
{
    public Guid Id { get; set; }

    // Source data - supports multiple trails/point clouds
    // This is a ReefMaster concern - Tinfour just sees vertices
    public ICollection<Guid> SourceAssetIds { get; set; } = new List<Guid>();
    public string SourceDescription { get; set; } = string.Empty;  // e.g., "Composite of 15 trails"

    // Grid metadata
    public int Width { get; set; }
    public int Height { get; set; }
    public double CellSize { get; set; }  // meters

    // Bounds (projected coordinates)
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    // Tiling info (if this is part of a tiled result)
    public int? TileX { get; set; }
    public int? TileY { get; set; }
    public Guid? ParentGridId { get; set; }  // For tile → parent relationship

    // Scaling for Int16 storage
    public double Scale { get; set; } = 100.0;  // cm precision
    public double Offset { get; set; } = 0.0;
    public short NoDataValue { get; set; } = short.MinValue;

    // Processing parameters (for reproducibility)
    public string InterpolationMethod { get; set; } = "NaturalNeighbor";
    public double MaxInterpolationDistance { get; set; }
    public int SourceVertexCount { get; set; }

    // Statistics
    public double MinDepth { get; set; }
    public double MaxDepth { get; set; }
    public double MeanDepth { get; set; }
    public double CoveragePercent { get; set; }

    // Compressed grid data
    public byte[] BlobData { get; set; } = Array.Empty<byte>();
    public bool IsCompressed { get; set; } = true;

    // Timestamps
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### 9.4 Palette Integration

ReefMaster has 21 built-in depth palettes. Grid rendering should use existing infrastructure:

```csharp
/// <summary>
/// Renders interpolated grid using ReefMaster's palette system.
/// </summary>
public interface IGridRenderer
{
    /// <summary>
    /// Render grid to SkiaSharp surface using depth palette.
    /// </summary>
    SKBitmap RenderToTile(
        GridDataEntity grid,
        SKRect viewportBounds,
        DepthPalette palette,
        double minDepth,
        double maxDepth);
}
```

---

## 10. Tiling Strategy for Large Areas

For areas like 40km × 40km @ 1m resolution:

### 10.1 Problem Analysis

| Scenario | Dimensions | Cells | Int16 Memory | Triangulation Vertices |
|----------|------------|-------|--------------|------------------------|
| Small bay | 2km × 2km | 4M | 8 MB | ~100K typical |
| Medium lake | 10km × 10km | 100M | 200 MB | ~500K typical |
| Large area | 40km × 40km | 1.6B | 3.2 GB | ~2M+ typical |

**Constraints:**
- Single TIN performs well up to ~1M vertices (benchmarks show ~1.4s for 1M)
- Rasterization is already parallel but memory-bound for huge grids
- ReefMaster uses streaming/LOD to manage large datasets

### 10.2 Revised Architecture: Tiling in ReefMaster, Not Tinfour

**Key Insight**: Tiling, stitching, and overlap handling are ReefMaster concerns, not Tinfour concerns.

Tinfour.NET should remain a focused triangulation/interpolation library. The tiling strategy moves to ReefMaster:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ReefMaster Tiling Layer (ReefMaster.Processing.Pipeline)                │
│ • Calculate tiles based on area size and max interpolation distance    │
│ • Overlap = MaxInterpolationDistance (e.g., 200-500m)                   │
│ • Partition vertices to tiles with overlap                              │
│ • Orchestrate parallel processing                                       │
│ • Stitch results, handling overlap regions                              │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ Per-tile: vertices + constraints + bounds
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Tinfour.Dataflow (or direct Tinfour.Core)                               │
│ • Receives bounded vertex set (doesn't know about tiling)              │
│ • Triangulates + interpolates within provided bounds                    │
│ • Returns single RasterOutput per invocation                            │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why this is better:**
1. Tinfour stays generic - usable by any client, not just ReefMaster
2. ReefMaster controls overlap based on its `MaxInterpolationDistance` setting
3. ReefMaster handles multi-trail compositing before passing to Tinfour
4. Stitching logic stays with the app that understands the use case

### 10.3 What Tinfour.Dataflow Should Provide

Simple, focused API - no tiling awareness:

```csharp
/// <summary>
/// Tinfour.Dataflow provides single-shot triangulation + interpolation.
/// Clients handle tiling/compositing externally.
/// </summary>
public sealed record TinfourConfig
{
    /// <summary>Interpolation algorithm</summary>
    public InterpolationType Method { get; init; } = InterpolationType.NaturalNeighbor;

    /// <summary>Output raster dimensions or cell size</summary>
    public int Width { get; init; }
    public int Height { get; init; }
    public double CellSize { get; init; }

    /// <summary>Bounds for rasterization (required - no auto-detection)</summary>
    public required BoundsData Bounds { get; init; }

    /// <summary>Restrict to constrained regions only</summary>
    public bool ConstrainedRegionsOnly { get; init; }

    /// <summary>Output data type</summary>
    public RasterDataType OutputType { get; init; } = RasterDataType.Float32;

    /// <summary>Scaling config for Int16/UInt16 output</summary>
    public ScalingConfig? Scaling { get; init; }
}

```

### 10.4 ReefMaster Tiling Implementation (Reference)

The tiling implementation belongs in `ReefMaster.Processing.Pipeline`, not in Tinfour.
Here's a reference design for ReefMaster's tiling layer:

```csharp
namespace ReefMaster.Processing.Pipeline;

/// <summary>
/// ReefMaster-specific tiling configuration.
/// Overlap is derived from MaxInterpolationDistance to ensure edge accuracy.
/// </summary>
public sealed record TilingConfig
{
    /// <summary>Maximum raster dimension per tile (affects memory)</summary>
    public int MaxTileDimension { get; init; } = 8192;  // 8K × 8K = 128MB @ Int16

    /// <summary>
    /// Maximum interpolation distance in meters.
    /// Overlap = this value to ensure edge cells have full context.
    /// </summary>
    public double MaxInterpolationDistance { get; init; } = 200.0;

    /// <summary>Minimum vertices required to process a tile</summary>
    public int MinVerticesPerTile { get; init; } = 100;

    /// <summary>Computed overlap based on max interpolation distance</summary>
    public double OverlapMeters => MaxInterpolationDistance;
}

/// <summary>
/// ReefMaster orchestrates tiled processing, calling Tinfour for each tile.
/// </summary>
public class ReefMasterTiledProcessor
{
    // Implementation in ReefMaster.Processing.Pipeline
    // - Calculates tile grid
    // - Partitions vertices (from multiple trails) with overlap
    // - Calls Tinfour.Dataflow for each tile
    // - Stitches results, resolving overlap by preferring center values
    // - Stores tiles in GridDataEntity with parent/child relationships
}
```

**Key points:**
- Overlap = `MaxInterpolationDistance` (ensures interpolation at tile edges has full vertex context)
- Tinfour sees each tile as an independent job with explicit bounds
- ReefMaster handles vertex partitioning, stitching, and storage

---

## 11. Updated Implementation Phases

### Tinfour.NET Phases (Public Library)

#### Phase 1: Tinfour.Core Enhancement
1. Add `MaxInterpolationDistance` to interpolators
2. Ensure `CancellationToken` support throughout
3. Add `RasterDataType` output options to `TinRasterizer`

#### Phase 2: Tinfour.Dataflow Assembly (Optional)
1. Create assembly with TPL Dataflow dependency
2. Define DTOs: `TriangulationInput`, `InterpolationConfig`, `RasterOutputBase` hierarchy
3. Basic `TriangulationBlock` and `InterpolationBlock`
4. Unit tests

**Note**: Tinfour.Dataflow may be optional - ReefMaster could use Tinfour.Core directly if the Dataflow abstraction doesn't add value.

### ReefMaster Phases (Private to RM)

#### Phase 3: ReefMaster.Interpolation.Tinfour
1. `TrailPointAdapter` / `PointCloudAdapter` for data conversion
2. Coordinate projection integration
3. Multi-trail compositing (combine trails before sending to Tinfour)

#### Phase 4: ReefMaster.Processing.Pipeline
1. Tiling configuration with `MaxInterpolationDistance`-based overlap
2. Vertex partitioning with overlap
3. Parallel tile orchestration via TPL Dataflow
4. Stitching with overlap resolution

#### Phase 5: ReefMaster Data & Visualization
1. `GridDataEntity` and repository
2. `IGridRenderer` with palette integration
3. LOD pyramid generation for grids
4. Map overlay integration

---

## 12. Assembly Summary

| Assembly | Responsibility | Dependencies |
|----------|---------------|--------------|
| **Tinfour.Core** | Triangulation, interpolation, rasterization | None (pure .NET) |
| **Tinfour.Dataflow** | TPL Dataflow wrappers (optional) | Tinfour.Core, System.Threading.Tasks.Dataflow |
| **ReefMaster.Interpolation.Tinfour** | RM → Tinfour adapters | Tinfour.Core, ReefMaster.Domain |
| **ReefMaster.Processing.Pipeline** | Tiling, stitching, orchestration | ReefMaster.Interpolation.*, TPL Dataflow |

---

**Document Version:** 1.2
**Author:** Claude Code Assistant
**Review Status:** Updated with clearer Tinfour/ReefMaster separation
