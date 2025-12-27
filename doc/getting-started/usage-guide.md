# Tinfour.NET Usage Guide

**Purpose:** Quick start guide for using Tinfour.NET  
**Audience:** Developers new to the library

## Installation

Add a reference to the Tinfour.Core NuGet package (when available) or reference the project directly:

```xml
<ProjectReference Include="..\Tinfour.Core\Tinfour.Core.csproj" />
```

## Quick Start

### Basic Triangulation

```csharp
using Tinfour.Core.Common;
using Tinfour.Core.Triangulation;

// Create vertices
var vertices = new List<Vertex>
{
    new Vertex(0, 0, 10),
    new Vertex(100, 0, 20),
    new Vertex(100, 100, 30),
    new Vertex(0, 100, 15),
    new Vertex(50, 50, 25)
};

// Create the TIN
var tin = new IncrementalTin();

// Add vertices (using Hilbert sorting for better performance)
tin.AddSorted(vertices);

// Check results
Console.WriteLine($"Vertices: {tin.GetVertices().Count}");
Console.WriteLine($"Triangles: {tin.CountTriangles().Count}");
```

### Pre-allocating for Large Datasets

For better performance with large datasets:

```csharp
var tin = new IncrementalTin();
tin.PreAllocateForVertices(1_000_000); // Pre-allocate for 1M vertices
tin.AddSorted(vertices);
```

## Constrained Delaunay Triangulation (CDT)

### Linear Constraints

```csharp
using Tinfour.Core.Constraints;

// Create a linear constraint (e.g., a road or river)
var roadVertices = new List<Vertex>
{
    new Vertex(10, 10, 0),
    new Vertex(90, 90, 0)
};
var roadConstraint = new LinearConstraint(roadVertices);

// Add to TIN
tin.AddConstraints(new List<IConstraint> { roadConstraint }, true);
```

### Polygon Constraints

```csharp
// Create a polygon constraint (e.g., a lake boundary)
var lakeVertices = new List<Vertex>
{
    new Vertex(30, 30, 5),
    new Vertex(70, 30, 5),
    new Vertex(70, 70, 5),
    new Vertex(30, 70, 5)
};
var lakeConstraint = new PolygonConstraint(lakeVertices);
lakeConstraint.SetConstraintIndex(1); // Assign an index for identification

// Add to TIN
tin.AddConstraints(new List<IConstraint> { lakeConstraint }, true);
```

### Constraint Z-Value Interpolation

When adding constraints, you can optionally request that the Z-values for the constraint vertices be interpolated from the existing TIN surface. This is useful when you have 2D constraints (e.g., building footprints) that you want to drape onto a 3D terrain.

To use this feature:
1.  Set the Z-value of your constraint vertices to `double.NaN`.
2.  Pass `true` for the `preInterpolateZ` parameter in `AddConstraints`.

```csharp
// Create vertices with NaN Z-values
var vertices = new List<Vertex>
{
    new Vertex(10, 10, double.NaN),
    new Vertex(20, 10, double.NaN),
    new Vertex(20, 20, double.NaN)
};
var constraint = new PolygonConstraint(vertices);

// Add to TIN with pre-interpolation enabled
// The TIN will use Triangular Facet Interpolation to populate the Z values
tin.AddConstraints(new List<IConstraint> { constraint }, true, preInterpolateZ: true);
```

> **Note:** The `NaturalNeighborInterpolator` and `InverseDistanceWeightingInterpolator` have been updated to ignore vertices with `NaN` Z-values. This allows you to add "topology-only" constraints that affect the triangulation structure but do not distort the surface interpolation.

## Accessing Triangles and Edges

### Iterating Over Triangles

```csharp
foreach (var triangle in tin.GetTriangles())
{
    var a = triangle.GetVertexA();
    var b = triangle.GetVertexB();
    var c = triangle.GetVertexC();
    
    Console.WriteLine($"Triangle: ({a.X}, {a.Y}) - ({b.X}, {b.Y}) - ({c.X}, {c.Y})");
}
```

### Iterating Over Edges

```csharp
foreach (var edge in tin.GetEdges())
{
    var a = edge.GetA();
    var b = edge.GetB();
    
    if (a != null && b != null)
    {
        Console.WriteLine($"Edge: ({a.X}, {a.Y}) -> ({b.X}, {b.Y})");
        
        // Check if it's a constraint edge
        if (edge.IsConstrained())
        {
            Console.WriteLine("  (Constrained)");
        }
    }
}
```

## Interpolation

### Triangular Facet (Linear) Interpolation

```csharp
using Tinfour.Core.Interpolation;

var interpolator = new TriangularFacetInterpolator(tin);
double z = interpolator.Interpolate(50, 50, null);
Console.WriteLine($"Interpolated Z at (50, 50): {z}");
```

### Natural Neighbor Interpolation

```csharp
var nnInterpolator = new NaturalNeighborInterpolator(tin);
double z = nnInterpolator.Interpolate(50, 50, null);
Console.WriteLine($"Natural neighbor Z at (50, 50): {z}");
```

### Inverse Distance Weighting

```csharp
var idwInterpolator = new InverseDistanceWeightingInterpolator(tin);
double z = idwInterpolator.Interpolate(50, 50, null);
Console.WriteLine($"IDW Z at (50, 50): {z}");
```

### Custom Vertex Valuator

To transform or filter vertex values during interpolation:

```csharp
public class ScaledValuator : IVertexValuator
{
    private readonly double _scale;
    
    public ScaledValuator(double scale) => _scale = scale;
    
    public double Value(IVertex v) => v.GetZ() * _scale;
}

// Convert feet to meters
var metersValuator = new ScaledValuator(0.3048);
double zMeters = interpolator.Interpolate(50, 50, metersValuator);
```

## Raster Generation

```csharp
using Tinfour.Core.Raster;

var interpolator = new NaturalNeighborInterpolator(tin);
var rasterizer = new TinRasterizer(tin, interpolator);

// Define bounds and resolution
var bounds = tin.GetBounds();
int width = 100;
int height = 100;

var raster = rasterizer.Rasterize(bounds.Value.Left, bounds.Value.Top, 
    bounds.Value.Width, bounds.Value.Height, width, height);

// Access raster values
for (int row = 0; row < height; row++)
{
    for (int col = 0; col < width; col++)
    {
        double z = raster[row, col];
        // Process value...
    }
}
```

## Point Location

### Check if Point is Inside TIN

```csharp
bool isInside = tin.IsPointInsideTin(50, 50);
```

### Find Containing Triangle

```csharp
var navigator = tin.GetNavigator();
var triangle = navigator.GetContainingTriangle(50, 50);

if (triangle != null)
{
    Console.WriteLine("Point is in triangle with vertices:");
    Console.WriteLine($"  A: {triangle.GetVertexA()}");
    Console.WriteLine($"  B: {triangle.GetVertexB()}");
    Console.WriteLine($"  C: {triangle.GetVertexC()}");
}
```

## Performance Tips

### 1. Use Hilbert Sorting

Always use `AddSorted()` instead of `Add()` for bulk vertex insertion:

```csharp
// Good - uses Hilbert sorting
tin.AddSorted(vertices);

// Less efficient - no spatial ordering
tin.Add(vertices, VertexOrder.AsIs);
```

### 2. Pre-allocate Memory

For large datasets, pre-allocate edge pool memory:

```csharp
tin.PreAllocateForVertices(expectedVertexCount);
```

### 3. Reuse Navigator

When performing multiple point locations, reuse the navigator:

```csharp
var navigator = tin.GetNavigator();
foreach (var point in queryPoints)
{
    var triangle = navigator.GetContainingTriangle(point.X, point.Y);
    // Navigator remembers last position for faster subsequent lookups
}
```

### 4. Batch Constraint Addition

Add all constraints in a single call:

```csharp
// Good - single operation
tin.AddConstraints(allConstraints, true);

// Less efficient - multiple operations
foreach (var constraint in constraints)
{
    tin.AddConstraints(new List<IConstraint> { constraint }, true);
}
```

## Common Patterns

### Loading Points from File

```csharp
public static List<Vertex> LoadPointsFromCsv(string filename)
{
    var vertices = new List<Vertex>();
    foreach (var line in File.ReadLines(filename).Skip(1)) // Skip header
    {
        var parts = line.Split(',');
        var x = double.Parse(parts[0]);
        var y = double.Parse(parts[1]);
        var z = float.Parse(parts[2]);
        vertices.Add(new Vertex(x, y, z));
    }
    return vertices;
}
```

### Computing Surface Statistics

```csharp
var vertices = tin.GetVertices();
var zValues = vertices.Where(v => !v.IsNullVertex()).Select(v => v.Z);

double minZ = zValues.Min();
double maxZ = zValues.Max();
double avgZ = zValues.Average();

Console.WriteLine($"Z range: {minZ} to {maxZ}, Average: {avgZ}");
```

## Error Handling

```csharp
try
{
    tin.AddSorted(vertices);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid vertex data: {ex.Message}");
}

// Check for degenerate triangulation
if (!tin.IsBootstrapped())
{
    Console.WriteLine("Warning: Not enough vertices for triangulation");
}
```

## Boundary Extraction

The `TinBoundaryExtractor` utility provides reliable methods for extracting boundary vertices from a TIN.

### Getting Boundary Vertices

```csharp
using Tinfour.Core.Utils;

// Get the external boundary vertices (convex hull for unconstrained TINs)
var boundaryVertices = TinBoundaryExtractor.GetBoundaryVertices(tin);

Console.WriteLine($"Boundary has {boundaryVertices.Count} vertices");
foreach (var v in boundaryVertices)
{
    Console.WriteLine($"  ({v.X}, {v.Y}, Z={v.GetZ()})");
}
```

### Creating a Boundary Constraint

You can create a polygon constraint from the TIN boundary to limit operations to the original data extent:

```csharp
// Create a polygon constraint from the TIN boundary
var boundaryConstraint = TinBoundaryExtractor.CreateBoundaryConstraint(tin);
if (boundaryConstraint != null)
{
    // Add buffer vertices outside the constraint first
    var bounds = tin.GetBounds()!.Value;
    var buffer = bounds.Width * 0.01; // 1% buffer
    tin.Add(new List<Vertex>
    {
        new Vertex(bounds.Left - buffer, bounds.Top - buffer, 0, 900001),
        new Vertex(bounds.Left + bounds.Width + buffer, bounds.Top - buffer, 0, 900002),
        new Vertex(bounds.Left + bounds.Width + buffer, bounds.Top + bounds.Height + buffer, 0, 900003),
        new Vertex(bounds.Left - buffer, bounds.Top + bounds.Height + buffer, 0, 900004)
    });

    // Add the boundary as a constraint
    tin.AddConstraints(new List<IConstraint> { boundaryConstraint }, true);
}
```

This is useful when you need to add a constraint around existing data without modifying the original vertices.

## Contour Generation

Generate contour lines from a TIN using the `ContourBuilderForTin` class.

### Basic Contour Generation

```csharp
using Tinfour.Core.Contour;

// Define contour levels
var levels = new double[] { 10, 20, 30, 40, 50 };

// Build contours
var builder = new ContourBuilderForTin(tin, null, levels);
var contours = builder.GetContours();

// Access the contour regions (areas between contour levels)
foreach (var region in builder.GetRegions())
{
    double zFloor = region.GetContourZMin();
    double zCeiling = region.GetContourZMax();

    // Get the perimeter ring
    var ring = region.GetContourRing();
    foreach (var point in ring.GetXY())
    {
        // Process contour point (x, y)
    }
}
```

### Contour Generation within Constraint Regions Only

When working with constrained triangulations, you can limit contour generation to areas within constraint regions:

```csharp
// Build contours only within constraint regions
var builder = new ContourBuilderForTin(
    tin,
    vertexValuator: null,
    zContour: levels,
    buildRegions: true,
    constrainedRegionsOnly: true);  // Only generate contours inside constraints

var contours = builder.GetContours();
```

This is useful when you have a bounding constraint and don't want contours extending into buffer areas outside your region of interest.

### Contour Generation with Custom Valuator

Use `IVertexValuator` to transform Z values during contour generation:

```csharp
// Convert depth to elevation (e.g., bathymetry)
public class DepthToElevationValuator : IVertexValuator
{
    private readonly double _waterLevel;

    public DepthToElevationValuator(double waterLevel) => _waterLevel = waterLevel;

    public double Value(IVertex v) => _waterLevel - v.GetZ();
}

var valuator = new DepthToElevationValuator(100.0);
var builder = new ContourBuilderForTin(tin, valuator, levels);
var contours = builder.GetContours();
```

## Smoothing Filter

The `SmoothingFilter` provides a low-pass filter over TIN surfaces, reducing noise and complexity while preserving constraint boundaries.

### How It Works

The smoothing filter uses generalized barycentric coordinates (Hormann's algorithm) to iteratively blend vertex Z values with their neighbors. Each pass reduces surface complexity:

1. For each vertex, compute barycentric weights based on its neighbors
2. Replace the vertex's Z value with the weighted average of neighbor Z values
3. Repeat for the specified number of passes (default: 25)

**Important:** Vertices on constraint boundaries and the TIN perimeter are NOT smoothed - they retain their original values.

### Basic Smoothing

```csharp
using Tinfour.Core.Utils;

// Create filter with default 25 passes
var filter = new SmoothingFilter(tin);

// Get smoothed Z value for a vertex
double smoothedZ = filter.Value(someVertex);

// Check statistics
Console.WriteLine($"Construction time: {filter.TimeToConstructFilterMs:F1}ms");
Console.WriteLine($"Smoothed Z range: {filter.MinZ} to {filter.MaxZ}");
```

### Custom Pass Count

```csharp
// More passes = smoother result (try 5-40)
var smoothFilter = new SmoothingFilter(tin, 35);
```

### Smoothed Contours

Combine smoothing with contour generation for cleaner results:

```csharp
// Create smoothing filter as a vertex valuator
var smoothingFilter = new SmoothingFilter(tin, 25);

// Build contours using smoothed values
var builder = new ContourBuilderForTin(tin, smoothingFilter);
var contourGraph = builder.BuildContours(levels);

// The contours will follow the smoothed surface
```

### When to Use Smoothing

- **Noisy data:** LiDAR or survey data with measurement noise
- **Cleaner visualization:** Contour lines will be less jagged
- **Surface simplification:** Reduce detail while preserving overall shape

### Smoothing Limitations

- Does NOT smooth vertices on constraint boundaries (by design)
- Does NOT smooth perimeter vertices (no valid barycentric coords)
- Memory usage scales with vertex count (dictionary-based storage)
- Higher pass counts increase construction time linearly

## Ruppert's Refinement (Mesh Quality Improvement)

`RuppertRefiner` implements Ruppert's Delaunay refinement algorithm to improve mesh quality by eliminating poorly-shaped triangles.

### Overview

The algorithm:
1. Identifies triangles with minimum angles below a threshold
2. Inserts new vertices (circumcenters or segment midpoints) to improve quality
3. Respects constraint boundaries through encroachment checks
4. Optionally interpolates Z values for new vertices

### Basic Refinement

```csharp
using Tinfour.Core.Refinement;

// Create options with minimum angle threshold (degrees)
var options = new RuppertOptions
{
    MinAngleDegrees = 25.0,  // Triangles below this are refined (max ~33°)
    InterpolateZ = true      // Interpolate Z values for new vertices
};

// Create refiner and process
var refiner = new RuppertRefiner(tin, options);
refiner.Refine();

// Check results
Console.WriteLine($"Triangles refined: {refiner.TrianglesRefined}");
Console.WriteLine($"Vertices inserted: {refiner.VerticesInserted}");
```

### Refinement Options

```csharp
var options = new RuppertOptions
{
    // Angle constraint (higher = stricter, max ~33°)
    MinimumAngleDegrees = 30.0,

    // Z value interpolation for new vertices
    InterpolateZ = true,

    // Interpolation method (when InterpolateZ = true)
    InterpolationType = InterpolationType.TriangularFacet,  // or NaturalNeighbor

    // Maximum iterations (safety limit)
    MaxIterations = 100000,

    // Constraint region options
    RefineOnlyInsideConstraints = true,  // Only refine triangles inside polygon constraints
    AddBoundingBoxConstraint = false,    // Auto-add bounding constraint around data
    BoundingBoxBufferPercent = 1.0       // Buffer size for bounding box (1% of bounds)
};
```

### Refinement with Bounding Box Constraint

When refining without existing constraints, you can automatically add a bounding box constraint to prevent the mesh from expanding beyond the original data bounds:

```csharp
var options = new RuppertOptions
{
    MinimumAngleDegrees = 25.0,
    InterpolateZ = true,
    RefineOnlyInsideConstraints = true,
    AddBoundingBoxConstraint = true,     // Add rectangular constraint around data
    BoundingBoxBufferPercent = 1.0       // 1% buffer beyond data bounds
};

var refiner = new RuppertRefiner(tin, options);
refiner.Refine();
```

This is useful when you want to refine a TIN that doesn't have explicit constraints but you want to limit refinement to the original data extent.

### Interpolation Options Explained

| Option | Effect |
|--------|--------|
| `InterpolationType.TriangularFacet` | Fast linear interpolation within triangles (default) |
| `InterpolationType.NaturalNeighbor` | Smoother C1 interpolation, ~3-5x slower |
| `UseOriginalTinForInterpolation = false` | Use evolving mesh (faster, may accumulate error) |
| `UseOriginalTinForInterpolation = true` | Build separate original TIN (2x memory, prevents error accumulation) |

### Refinement with Area Constraint

Limit maximum triangle area for denser meshes:

```csharp
var options = new RuppertOptions
{
    MinAngleDegrees = 25.0,
    MaxTriangleArea = 500.0,  // Square units
    InterpolateZ = true
};
```

### Monitoring Progress

```csharp
var refiner = new RuppertRefiner(tin, options);

// Process with progress callback
refiner.Refine(progress =>
{
    Console.WriteLine($"Progress: {progress.PercentComplete:F1}%");
    Console.WriteLine($"  Triangles processed: {progress.TrianglesProcessed}");
    Console.WriteLine($"  Vertices inserted: {progress.VerticesInserted}");
});
```

### Best Practices

1. **Angle threshold:** Stay below ~33° to guarantee termination
2. **Add constraints first:** Refine AFTER adding all constraints
3. **Check results:** Verify mesh quality improved as expected
4. **Memory:** Refinement can significantly increase vertex count

## Complete Workflow Example

Here's a complete example combining TIN creation, constraints, refinement, smoothing, and contouring:

```csharp
using Tinfour.Core.Common;
using Tinfour.Core.Standard;
using Tinfour.Core.Constraints;
using Tinfour.Core.Refinement;
using Tinfour.Core.Utils;
using Tinfour.Core.Contour;

// 1. Create TIN with vertices
var vertices = LoadVerticesFromFile("terrain.csv");
var tin = new IncrementalTin();
tin.AddSorted(vertices);

// 2. Add constraint boundary
var boundaryVertices = LoadBoundaryFromFile("boundary.csv");
var boundary = new PolygonConstraint(boundaryVertices);
tin.AddConstraints(new List<IConstraint> { boundary }, true);

// 3. Refine mesh quality
var refineOptions = new RuppertOptions
{
    MinAngleDegrees = 25.0,
    InterpolateZ = true,
    InterpolationType = InterpolationType.NaturalNeighbor,
    UseOriginalTinForInterpolation = true
};
var refiner = new RuppertRefiner(tin, refineOptions);
refiner.Refine();

Console.WriteLine($"After refinement: {tin.GetVertices().Count()} vertices");

// 4. Create smoothing filter
var smoothingFilter = new SmoothingFilter(tin, 25);

// 5. Generate smoothed contours
var levels = Enumerable.Range(0, 20).Select(i => 100.0 + i * 10.0).ToArray();
var builder = new ContourBuilderForTin(tin, smoothingFilter);
var contours = builder.BuildContours(levels);

// 6. Export contours
foreach (var region in contours.GetContourRegions())
{
    var ring = region.GetContourRing();
    // Export ring coordinates...
}
```

## Next Steps

- See [Interpolation Overview](../architecture/interpolation/interpolation-overview.md) for detailed method comparison
- See [Architecture Overview](../architecture/overview.md) for understanding the internal design
- Explore the `Tinfour.Demo` and `Tinfour.Visualiser` projects for more examples

---

**Last Updated:** December 2025
