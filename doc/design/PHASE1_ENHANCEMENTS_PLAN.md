# Tinfour.Core Phase 1 Enhancements Plan

## Status: COMPLETED

**Implementation Date:** November 2025

## Overview

This document outlines the Phase 1 enhancements required to prepare Tinfour.Core for integration with ReefMaster v3. These enhancements focus on making the library production-ready for large-scale bathymetric mapping while maintaining its role as a pure triangulation/interpolation library.

## Goals

1. ✅ Add MaxInterpolationDistance support to prevent interpolation far from data points
2. ✅ Provide memory-efficient raster output options for large maps
3. ✅ Add interpolator configuration flexibility
4. ✅ Document thread safety guarantees

## Summary of Changes

### New Files Created
- `Tinfour.Core/Interpolation/InterpolatorOptions.cs` - Configuration class for interpolators
- `Tinfour.Core/Interpolation/RasterDataType.cs` - Enum for raster storage types
- `Tinfour.Core/Interpolation/IRasterData.cs` - Interface for flexible raster storage
- `Tinfour.Core/Interpolation/Float32RasterData.cs` - 4 bytes/cell storage
- `Tinfour.Core/Interpolation/Float64RasterData.cs` - 8 bytes/cell storage
- `Tinfour.Core/Interpolation/Int16ScaledRasterData.cs` - 2 bytes/cell with scale/offset
- `doc/architecture/THREAD_SAFETY.md` - Thread safety documentation

### Modified Files
- `Tinfour.Core/Interpolation/IInterpolatorOverTin.cs` - Added MaxInterpolationDistance property
- `Tinfour.Core/Interpolation/InterpolatorFactory.cs` - Added overload with InterpolatorOptions
- `Tinfour.Core/Interpolation/TriangularFacetInterpolator.cs` - Implemented MaxInterpolationDistance
- `Tinfour.Core/Interpolation/NaturalNeighborInterpolator.cs` - Implemented MaxInterpolationDistance
- `Tinfour.Core/Interpolation/InverseDistanceWeightingInterpolator.cs` - Implemented MaxInterpolationDistance
- `Tinfour.Core/Interpolation/TinRasterizer.cs` - Added constructor with options, CreateRaster overload with RasterDataType
- `Tinfour.Core/Interpolation/RasterResult.cs` - Added IRasterData support, nullable Data property

---

## Enhancement 1: MaxInterpolationDistance

### Problem
Currently, interpolation will compute values even when the query point is arbitrarily far from any vertex. For bathymetric mapping, interpolating beyond 200-500m from actual soundings produces meaningless data.

### Solution
Add `MaxInterpolationDistance` property to interpolators. When set, return `NaN` if the query point exceeds this distance from the nearest vertex.

### Implementation

#### 1.1 Update `IInterpolatorOverTin` interface

```csharp
// Add to IInterpolatorOverTin.cs
/// <summary>
///     Gets or sets the maximum distance from a data point at which
///     interpolation will be performed. If the query point is further
///     than this distance from the nearest vertex, NaN is returned.
///     A value of null (default) disables this check.
/// </summary>
double? MaxInterpolationDistance { get; set; }
```

#### 1.2 Update interpolator implementations

Files to modify:
- `TriangularFacetInterpolator.cs`
- `NaturalNeighborInterpolator.cs`
- `InverseDistanceWeightingInterpolator.cs`

Each interpolator's `Interpolate()` method needs to:
1. After finding the enclosing triangle, compute distance to nearest vertex
2. If distance > MaxInterpolationDistance, return `NaN`

#### 1.3 Update `InterpolatorFactory`

Add overload accepting configuration:

```csharp
public static IInterpolatorOverTin Create(
    IIncrementalTin tin,
    InterpolationType type,
    InterpolatorOptions? options = null)
```

#### 1.4 Create `InterpolatorOptions` class

```csharp
public class InterpolatorOptions
{
    public bool ConstrainedRegionsOnly { get; set; } = false;
    public double? MaxInterpolationDistance { get; set; } = null;

    // IDW-specific
    public double IdwPower { get; set; } = 2.0;
    public bool IdwUseDistanceWeighting { get; set; } = false;
}
```

### Files to Create/Modify
- [x] `Tinfour.Core/Interpolation/IInterpolatorOverTin.cs` - Add property
- [x] `Tinfour.Core/Interpolation/InterpolatorOptions.cs` - New file
- [x] `Tinfour.Core/Interpolation/InterpolatorFactory.cs` - Add overload
- [x] `Tinfour.Core/Interpolation/TriangularFacetInterpolator.cs` - Implement
- [x] `Tinfour.Core/Interpolation/NaturalNeighborInterpolator.cs` - Implement
- [x] `Tinfour.Core/Interpolation/InverseDistanceWeightingInterpolator.cs` - Implement

---

## Enhancement 2: Flexible Raster Output Types

### Problem
Current `RasterResult` uses `double[,]` which for large rasters (40km × 40km @ 1m = 1.6B cells) requires 12.8 GB of memory. Bathymetric data doesn't need double precision.

### Solution
Add generic raster output with common type implementations.

### Implementation

#### 2.1 Create `RasterDataType` enum

```csharp
public enum RasterDataType
{
    /// <summary>Float64 (double) - 8 bytes per cell, full precision</summary>
    Float64,

    /// <summary>Float32 (float) - 4 bytes per cell, sufficient for most applications</summary>
    Float32,

    /// <summary>Int16 with scale factor - 2 bytes per cell, good for bounded ranges</summary>
    Int16Scaled
}
```

#### 2.2 Create `IRasterData` interface and implementations

```csharp
public interface IRasterData
{
    int Width { get; }
    int Height { get; }
    RasterDataType DataType { get; }
    double GetValue(int x, int y);
    void SetValue(int x, int y, double value);
    long MemorySize { get; }
}

public class Float32RasterData : IRasterData { ... }
public class Float64RasterData : IRasterData { ... }
public class Int16ScaledRasterData : IRasterData { ... }
```

#### 2.3 Update `TinRasterizer`

Add overload:

```csharp
public RasterResult CreateRaster(
    int width,
    int height,
    RasterDataType dataType = RasterDataType.Float32,
    (double Left, double Top, double Width, double Height)? bounds = null,
    CancellationToken cancellationToken = default)
```

#### 2.4 Update `RasterResult`

```csharp
public class RasterResult
{
    // Existing double[,] for backward compatibility
    public double[,]? Data { get; }

    // New flexible data storage
    public IRasterData? RasterData { get; }

    // ... rest unchanged
}
```

### Files to Create/Modify
- [x] `Tinfour.Core/Interpolation/RasterDataType.cs` - New file
- [x] `Tinfour.Core/Interpolation/IRasterData.cs` - New file
- [x] `Tinfour.Core/Interpolation/Float32RasterData.cs` - New file
- [x] `Tinfour.Core/Interpolation/Float64RasterData.cs` - New file
- [x] `Tinfour.Core/Interpolation/Int16ScaledRasterData.cs` - New file
- [x] `Tinfour.Core/Interpolation/RasterResult.cs` - Modify
- [x] `Tinfour.Core/Interpolation/TinRasterizer.cs` - Add overloads

---

## Enhancement 3: Thread Safety Documentation

### Problem
Thread safety guarantees are documented in code comments but not in a centralized, discoverable location.

### Solution
Add XML documentation and a THREAD_SAFETY.md document.

### Key Points to Document

1. **IncrementalTin**: NOT thread-safe during construction. After `Lock()`, read-only access is safe.

2. **Interpolators**: Each instance maintains internal state (navigator cache). Multiple instances can safely share a locked TIN.

3. **TinRasterizer**: Thread-safe - creates thread-local interpolators internally.

4. **Pattern for parallel use**:
```csharp
// Build TIN (single-threaded)
var tin = new IncrementalTin(pointSpacing);
tin.Add(vertices);
tin.AddConstraints(constraints, restoreConformity: true);
tin.Lock(); // Mark as read-only

// Use in parallel (safe)
Parallel.ForEach(tiles, tile => {
    var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);
    var result = rasterizer.CreateRaster(tile.Width, tile.Height, tile.Bounds);
    // process result...
});
```

### Files to Create/Modify
- [x] `doc/architecture/THREAD_SAFETY.md` - New file
- [x] `Tinfour.Core/Interpolation/IInterpolatorOverTin.cs` - Add XML docs

---

## Implementation Order

### Step 1: InterpolatorOptions and MaxInterpolationDistance
1. Create `InterpolatorOptions.cs`
2. Update `IInterpolatorOverTin.cs` with new property
3. Update `InterpolatorFactory.cs` with new overload
4. Update `TriangularFacetInterpolator.cs`
5. Update `NaturalNeighborInterpolator.cs`
6. Update `InverseDistanceWeightingInterpolator.cs`
7. Add unit tests

### Step 2: Flexible Raster Output
1. Create `RasterDataType.cs`
2. Create `IRasterData.cs` and implementations
3. Update `RasterResult.cs`
4. Update `TinRasterizer.cs`
5. Add unit tests

### Step 3: Documentation
1. Create `THREAD_SAFETY.md`
2. Update interface XML documentation
3. Review and update existing code comments

---

## Testing Strategy

### Unit Tests Required
- [ ] MaxInterpolationDistance returns NaN when exceeded
- [ ] MaxInterpolationDistance returns value when within range
- [ ] MaxInterpolationDistance null disables check (backward compatible)
- [ ] Float32RasterData correctly stores/retrieves values
- [ ] Int16ScaledRasterData handles scale factor correctly
- [ ] TinRasterizer produces identical results with Float64 vs legacy double[,]
- [ ] InterpolatorFactory creates correctly configured interpolators

### Integration Tests
- [ ] Large raster generation with Float32 (memory usage verification)
- [ ] Parallel rasterization with shared TIN

---

## Backward Compatibility

All changes maintain backward compatibility:
- Existing `InterpolatorFactory.Create()` signature unchanged
- Existing `TinRasterizer.CreateRaster()` signatures unchanged
- Existing `RasterResult.Data` property unchanged
- `MaxInterpolationDistance` defaults to null (disabled)

---

## Estimated Scope

| Enhancement | New Files | Modified Files | Complexity |
|-------------|-----------|----------------|------------|
| MaxInterpolationDistance | 1 | 5 | Medium |
| Flexible Raster Output | 5 | 2 | Medium |
| Thread Safety Docs | 1 | 2 | Low |
| **Total** | **7** | **9** | |

---

## Open Questions

1. **Int16Scaled range**: Should we auto-detect min/max from first pass, or require caller to specify?
   - Recommendation: Require caller to specify for predictable behavior

2. **Memory<T> support**: Should we support `Memory<float>` for streaming scenarios?
   - Recommendation: Defer to Phase 2, assess need after initial integration

3. **Interpolator pooling**: Should we provide an interpolator pool for high-frequency tile generation?
   - Recommendation: Defer - TinRasterizer already handles this internally
