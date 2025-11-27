# Thread Safety in Tinfour.Core

This document describes the thread safety guarantees and patterns for using Tinfour.Core in multi-threaded applications.

## Overview

Tinfour.Core is designed to support parallel processing for rasterization and interpolation. However, different components have different thread safety characteristics that must be understood to use them correctly.

## Component Thread Safety

### IncrementalTin

**Thread Safety: NOT thread-safe during modification**

The `IncrementalTin` class is NOT thread-safe during construction or modification. All vertex and constraint insertion must be performed from a single thread.

However, once a TIN is built and locked, it becomes effectively read-only and can be safely shared across multiple threads for interpolation.

```csharp
// Build TIN (single-threaded)
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.AddConstraints(constraints, restoreConformity: true);
tin.Lock(); // Mark as read-only - TIN can now be shared

// Safe to use from multiple threads after Lock()
```

**Important:** Never modify a TIN while other threads are reading from it. If modifications are needed:
1. Stop all reader threads
2. Perform modifications
3. Call `ResetForChangeToTin()` on all interpolators
4. Resume reader threads

### IInterpolatorOverTin Implementations

**Thread Safety: NOT thread-safe (instances cannot be shared)**

Interpolator instances (`TriangularFacetInterpolator`, `NaturalNeighborInterpolator`, `InverseDistanceWeightingInterpolator`) are NOT thread-safe. Each instance maintains internal state:

- Navigator position cache (for query-to-query locality optimization)
- Surface normal computation state
- Diagnostic counters

**Pattern: One interpolator per thread**

```csharp
// WRONG - sharing interpolator across threads
var interpolator = new NaturalNeighborInterpolator(tin);
Parallel.ForEach(points, point => {
    var z = interpolator.Interpolate(point.X, point.Y, null); // UNSAFE!
});

// CORRECT - each thread gets its own interpolator
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.Lock();

Parallel.ForEach(points, () => new NaturalNeighborInterpolator(tin),
    (point, state, interpolator) => {
        var z = interpolator.Interpolate(point.X, point.Y, null);
        return interpolator;
    },
    interpolator => { /* cleanup if needed */ });
```

**Sharing TIN is safe; sharing interpolators is not.**

### TinRasterizer

**Thread Safety: Thread-safe (creates thread-local interpolators internally)**

The `TinRasterizer` class is designed for parallel operation. It internally creates thread-local interpolator instances, so a single `TinRasterizer` can be used safely from multiple threads (though typically you call `CreateRaster` once and it handles parallelism internally).

```csharp
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.Lock();

var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);
var result = rasterizer.CreateRaster(1000, 1000); // Internally parallelized
```

The rasterizer uses `ThreadLocal<IInterpolatorOverTin>` to ensure each processing thread has its own interpolator instance.

### IIncrementalTinNavigator

**Thread Safety: NOT thread-safe**

Navigators cache their current position for performance. Each thread must have its own navigator instance.

### IRasterData Implementations

**Thread Safety: NOT thread-safe for writes**

`Float32RasterData`, `Float64RasterData`, and `Int16ScaledRasterData` use simple 2D arrays as backing storage. Concurrent writes to the same cell are not atomic. However, the `TinRasterizer` safely partitions rows across threads to avoid concurrent writes.

## Recommended Patterns

### Pattern 1: Single TIN, Multiple Rasterizers (Tiled Processing)

```csharp
// Build TIN once (single-threaded)
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.AddConstraints(constraints, true);
tin.Lock();

// Create rasters in parallel for different tiles
var tiles = CalculateTiles(totalBounds, tileSize);

Parallel.ForEach(tiles, tile => {
    // Each iteration creates its own rasterizer
    var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);
    var result = rasterizer.CreateRaster(
        tileWidth, tileHeight,
        RasterDataType.Float32,
        tile.Bounds);
    SaveTile(result, tile);
});
```

### Pattern 2: Custom Interpolation with Thread-Local Interpolators

```csharp
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.Lock();

var options = new InterpolatorOptions
{
    MaxInterpolationDistance = 500.0,
    ConstrainedRegionsOnly = true
};

// ThreadLocal ensures each thread gets its own interpolator
var threadLocalInterpolator = new ThreadLocal<IInterpolatorOverTin>(() =>
    InterpolatorFactory.Create(tin, InterpolationType.NaturalNeighbor, options));

Parallel.For(0, height, y => {
    var interpolator = threadLocalInterpolator.Value!;
    for (var x = 0; x < width; x++) {
        var worldX = bounds.Left + x * cellSize;
        var worldY = bounds.Top + y * cellSize;
        var z = interpolator.Interpolate(worldX, worldY, null);
        // Process z...
    }
});
```

### Pattern 3: TIN Reuse Across Multiple Operations

A single locked TIN can be reused for:
- Multiple rasterizations at different resolutions
- Multiple interpolation methods
- Multiple concurrent queries

```csharp
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.Lock();

// All these can run concurrently
var task1 = Task.Run(() => {
    var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);
    return rasterizer.CreateRaster(1000, 1000, RasterDataType.Float32);
});

var task2 = Task.Run(() => {
    var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);
    return rasterizer.CreateRaster(500, 500, RasterDataType.Float32);
});

await Task.WhenAll(task1, task2);
```

## Memory Considerations

When using large TINs with multiple threads:

1. **TIN Memory:** The TIN structure is shared and not duplicated
2. **Interpolator Memory:** Each thread-local interpolator has minimal overhead (navigator cache)
3. **Raster Memory:** Each rasterization allocates its own output array

For very large rasters, consider:
- Using `Float32` instead of `Float64` (halves memory)
- Using `Int16Scaled` for maximum compression (quarter of Float64)
- Processing in tiles to limit peak memory usage

## Summary Table

| Component | Thread-Safe | Shareable Across Threads | Notes |
|-----------|-------------|--------------------------|-------|
| `IncrementalTin` | No (build) / Yes (read after Lock) | Yes (after Lock) | Lock before sharing |
| `IInterpolatorOverTin` | No | No | One per thread |
| `IIncrementalTinNavigator` | No | No | One per thread |
| `TinRasterizer` | Yes | Yes | Creates internal thread-local interpolators |
| `IRasterData` | No (concurrent writes) | N/A | TinRasterizer partitions writes safely |
| `InterpolatorFactory` | Yes | Yes | Static factory methods |
