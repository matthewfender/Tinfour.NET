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

## Next Steps

- See [Interpolation Overview](../architecture/interpolation/interpolation-overview.md) for detailed method comparison
- See [Architecture Overview](../architecture/overview.md) for understanding the internal design
- Explore the `Tinfour.Demo` and `Tinfour.Visualiser` projects for more examples

---

**Last Updated:** November 2025
