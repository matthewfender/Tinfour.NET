# Interpolation Overview

**Module:** Tinfour.Core.Interpolation  
**Purpose:** Surface reconstruction from discrete point measurements

## Introduction

Interpolation reconstructs continuous surfaces from discrete sample points in a Triangulated Irregular Network (TIN). Tinfour.NET provides three interpolation methods, each with different trade-offs between speed, quality, and computational complexity.

## Interpolation Methods

### Summary Comparison

| Method | Speed | Quality | Use Case |
|--------|-------|---------|----------|
| [Triangular Facet](./triangular-facet.md) | ★★★ Fast | ★☆☆ Basic | Real-time visualization, large datasets |
| [Inverse Distance Weighting](./inverse-distance-weighting.md) | ★★☆ Moderate | ★★☆ Good | General purpose, balanced performance |
| [Natural Neighbor](./natural-neighbor.md) | ★☆☆ Slow | ★★★ Excellent | High-quality products, irregular data |

### 1. Triangular Facet Interpolation

**Algorithm:** Linear interpolation using barycentric coordinates  
**Continuity:** C⁰ (discontinuous derivatives at edges)  
**Performance:** O(log n) per query (triangle location)

**Characteristics:**
- Produces faceted (piecewise planar) surfaces
- Exact at sample points
- Simple and computationally efficient
- Suitable for visualization and preliminary analysis

**Formula:**
```
z(x,y) = λ₁·z₁ + λ₂·z₂ + λ₃·z₃

where λᵢ are barycentric coordinates of (x,y) in triangle
```

[Details: Triangular Facet Interpolation](./triangular-facet.md)

### 2. Natural Neighbor Interpolation

**Algorithm:** Sibson's natural neighbor method  
**Continuity:** C¹ (continuous first derivatives) except at sample points  
**Performance:** O(k) per query where k = neighbor count (~6-8 average)

**Characteristics:**
- Smooth, high-quality surfaces
- Handles irregularly distributed data well
- Local method (uses only nearby samples)
- Exact at sample points
- Computationally intensive

**Basis:** Voronoi diagram and Thiessen polygon area ratios

**Formula:**
```
z(x,y) = Σ wᵢ·zᵢ

where wᵢ = A'ᵢ / A'total
A'ᵢ = area of overlap between Voronoi cell of vᵢ and new Voronoi cell at (x,y)
```

[Details: Natural Neighbor Interpolation](./natural-neighbor.md)

### 3. Inverse Distance Weighting (IDW)

**Algorithm:** Shepard's method with distance-based weights  
**Continuity:** C⁰ (continuous but not smooth)  
**Performance:** O(k) per query where k = search radius neighbors

**Characteristics:**
- Smooth appearance (but not C¹)
- Configurable power parameter
- Works with any point distribution
- Creates "bull's eyes" around samples
- Simple and intuitive

**Formula:**
```
z(x,y) = Σ wᵢ·zᵢ / Σ wᵢ

where wᵢ = 1 / d(x,y,vᵢ)ᵖ
p = power parameter (typically 2)
```

[Details: Inverse Distance Weighting](./inverse-distance-weighting.md)

## Common Interface

All interpolators implement `IInterpolatorOverTin`:

```csharp
public interface IInterpolatorOverTin
{
    /// <summary>
    /// Interpolates a Z value at the specified coordinates
    /// </summary>
    double Interpolate(double x, double y, IVertexValuator? valuator);
    
    /// <summary>
    /// Resets internal state after TIN modification
    /// </summary>
    void ResetForChangeToTin();
}
```

## Vertex Valuators

**Purpose:** Extract or transform Z values from vertices

```csharp
public interface IVertexValuator
{
    double Value(IVertex v);
}
```

**Use Cases:**
- Apply smoothing filters
- Use alternative data fields
- Transform elevation units
- Implement custom weighting

**Example:**
```csharp
public class ScaledValuator : IVertexValuator
{
    private readonly double _scale;
    public ScaledValuator(double scale) => _scale = scale;
    public double Value(IVertex v) => v.Z * _scale;
}

var interpolator = new NaturalNeighborInterpolator(tin);
double z = interpolator.Interpolate(x, y, new ScaledValuator(0.3048)); // feet to meters
```

## Usage Patterns

### Basic Usage

```csharp
using var tin = new IncrementalTin();
tin.AddSorted(vertices);

// Choose interpolator
var interpolator = new TriangularFacetInterpolator(tin);

// Interpolate single point
double z = interpolator.Interpolate(x, y, null);
```

### Raster Generation

```csharp
var rasterizer = new TinRasterizer(tin, interpolator);
var raster = rasterizer.Rasterize(bounds, resolution);

for (int row = 0; row < raster.Height; row++)
{
    for (int col = 0; col < raster.Width; col++)
    {
        double z = raster[row, col];
        // Process interpolated value
    }
}
```

### Custom Valuator

```csharp
public class SmoothingValuator : IVertexValuator
{
    private readonly Dictionary<int, double> _smoothed;
    
    public double Value(IVertex v)
    {
        return _smoothed.TryGetValue(v.GetIndex(), out var smooth)
            ? smooth
            : v.Z;
    }
}
```

## Method Selection Guide

[Details: Method Selection Guide](./method-selection.md)

### By Application

**Real-time Visualization:**
- **Primary:** Triangular Facet
- **Why:** Fastest, good enough for display

**Terrain Analysis:**
- **Primary:** Natural Neighbor
- **Why:** Smooth derivatives for slope/aspect

**General GIS:**
- **Primary:** Inverse Distance Weighting
- **Why:** Balanced quality and performance

**High-Quality Products:**
- **Primary:** Natural Neighbor
- **Why:** Best visual quality and mathematical properties

### By Data Characteristics

**Regular Grid:**
- Triangular Facet often sufficient
- IDW provides smoothing if needed

**Irregular/Sparse:**
- Natural Neighbor handles gaps well
- IDW with larger search radius

**High-Frequency Features:**
- Natural Neighbor preserves detail
- Triangular Facet may appear too angular

**Noisy Data:**
- IDW with high power for smoothing
- Natural Neighbor with pre-filtered vertices

## Performance Considerations

### Query Time Complexity

| Method | Per-Query | Notes |
|--------|-----------|-------|
| Triangular Facet | O(log n) | Triangle location dominates |
| Natural Neighbor | O(k) | k ≈ 6-8 neighbors typically |
| IDW | O(k) | k depends on search radius |

### Memory Requirements

| Method | Additional Memory |
|--------|------------------|
| Triangular Facet | Minimal (navigator state) |
| Natural Neighbor | O(1) per query (temporary arrays) |
| IDW | O(k) per query (neighbor collection) |

### Optimization Strategies

**Navigator Reuse:**
```csharp
var navigator = tin.GetNavigator();
var interpolator = new TriangularFacetInterpolator(tin);

// Reuse navigator for multiple queries
for each point:
    z = interpolator.Interpolate(x, y, null, navigator);
```

**Batch Processing:**
```csharp
// Sort query points by spatial locality
var sorted = HilbertSort.SortVertices(queryPoints);

foreach (var point in sorted)
{
    // Navigator's last position provides good starting guess
    z = interpolator.Interpolate(point.X, point.Y, null);
}
```

## Boundary Handling

### Points Outside TIN

**Behavior varies by method:**

**Triangular Facet:**
- Returns NaN for points outside convex hull
- No extrapolation

**Natural Neighbor:**
- Returns NaN outside hull
- Cannot determine neighbors for external points

**IDW:**
- Can extrapolate if configured
- Uses nearest vertices within search radius
- Quality degrades far from samples

### Edge Cases

**On Triangle Vertex:**
- All methods return exact vertex Z value
- Special case detection for numerical stability

**On Triangle Edge:**
- Triangular Facet: Linear interpolation between edge vertices
- Natural Neighbor: Degenerates to edge interpolation
- IDW: Weighted by endpoints

**Near Vertex (within tolerance):**
- Snaps to vertex value
- Avoids numerical instability

## Quality Assessment

### Validation Metrics

**Cross-validation:**
```csharp
// Leave-one-out testing
double totalError = 0;
foreach (var testVertex in vertices)
{
    // Remove test vertex, interpolate at its location
    tin.Remove(testVertex);
    double predicted = interpolator.Interpolate(testVertex.X, testVertex.Y, null);
    double error = Math.Abs(predicted - testVertex.Z);
    totalError += error;
    tin.Add(testVertex);
}
double mae = totalError / vertices.Count;
```

**Visual Inspection:**
- Generate contours or hillshade
- Check for artifacts (bull's eyes, discontinuities)
- Verify feature preservation

## Implementation Notes

### Thread Safety

**Interpolators are NOT thread-safe** when sharing navigator state.

**Safe parallel patterns:**
```csharp
// Option 1: Thread-local interpolators
Parallel.ForEach(points, () => new NaturalNeighborInterpolator(tin),
    (point, state, localInterp) => {
        return localInterp.Interpolate(point.X, point.Y, null);
    },
    _ => {});

// Option 2: Thread-local navigators
Parallel.ForEach(points, () => tin.GetNavigator(),
    (point, state, nav) => {
        return interpolator.Interpolate(point.X, point.Y, null, nav);
    },
    _ => {});
```

### NaN Handling

**Invalid vertices (NaN Z values):**
- Should be excluded before TIN construction
- Interpolation with NaN vertices produces undefined results
- Validation recommended during data loading

### Precision

**Coordinate vs Elevation:**
- X, Y use double precision (64-bit)
- Z uses float precision (32-bit) in Vertex struct
- Trade-off: memory vs elevation precision
- Sufficient for most terrain applications (~7 decimal digits)

## Related Documentation

- [Triangular Facet Interpolation](./triangular-facet.md)
- [Natural Neighbor Interpolation](./natural-neighbor.md)
- [Inverse Distance Weighting](./inverse-distance-weighting.md)
- [Method Selection Guide](./method-selection.md)
- [TIN Navigator](../core/incremental-tin.md#navigator)

## References

### Papers

- Sibson, R. (1981). "A brief description of natural neighbour interpolation"
- Shepard, D. (1968). "A two-dimensional interpolation function for irregularly-spaced data"
- Watson, D. F. (1992). "Contouring: A Guide to the Analysis and Display of Spatial Data"

### Books

- de Berg et al. (2008). "Computational Geometry: Algorithms and Applications"

---

**Last Updated:** November 26, 2025
