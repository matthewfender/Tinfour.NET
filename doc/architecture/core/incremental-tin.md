# IncrementalTin Implementation

**Class:** `IncrementalTin`  
**Namespace:** `Tinfour.Core.Standard`  
**Implements:** `IIncrementalTin`

## Overview

`IncrementalTin` is the primary implementation of Delaunay triangulation in Tinfour.NET. It provides incremental construction, constraint handling, and query operations through a comprehensive API that maintains algorithmic fidelity to the Java implementation.

## Class Structure

```csharp
public class IncrementalTin : IIncrementalTin
{
    // Core components
    private readonly EdgePool _edgePool;
    private readonly GeometricOperations _geoOp;
    private readonly Thresholds _thresholds;
    private readonly BootstrapUtility _bootstrapUtility;
    private readonly StochasticLawsonsWalk _walker;
    private readonly ConstraintProcessor _constraintProcessor;
    
    // State
    private List<IVertex>? _vertexList;  // Pre-bootstrap staging
    private IQuadEdge? _searchEdge;      // Reference for queries
    private bool _isLocked;
    private bool _isBootstrapped;
    
    // Bounds tracking
    private double _boundsMinX, _boundsMinY;
    private double _boundsMaxX, _boundsMaxY;
}
```

## Construction

### Constructors

```csharp
// Default: nominal spacing = 1.0
public IncrementalTin()

// Specify expected point spacing for threshold calculation
public IncrementalTin(double estimatedPointSpacing)
```

**Nominal Point Spacing:**
- Used to compute geometric tolerance thresholds
- Should approximate average distance between vertices
- Affects numerical robustness
- Default of 1.0 suitable for normalized coordinates

### Initialization

```csharp
var tin = new IncrementalTin(nominalSpacing: 10.0);
// TIN is empty, not bootstrapped
// Ready to accept vertices
```

## Vertex Addition

### Single Vertex

```csharp
public bool Add(IVertex vertex)
```

**Process:**
1. Check if bootstrapped; if not, stage vertex
2. Locate enclosing triangle (or hull edge)
3. Insert vertex (interior or hull extension)
4. Restore Delaunay property via edge flipping
5. Update bounds and search edge

**Returns:** `true` if TIN modified, `false` if duplicate/coincident

### Bulk Addition

```csharp
public bool Add(IEnumerable<IVertex> vertices, VertexOrder order)
```

**Vertex Order Options:**
- `VertexOrder.AsIs` - Use provided order
- `VertexOrder.Hilbert` - Sort by Hilbert curve (recommended)

**Recommended Pattern:**
```csharp
// Pre-allocate, Hilbert sort, bulk add
tin.PreAllocateForVertices(vertices.Count);
tin.AddSorted(vertices);  // Convenience method
```

**Performance Impact:**
- Hilbert sorting: ~20-30% improvement for large datasets
- Pre-allocation: Reduces page allocation overhead

### Pre-allocation

```csharp
public void PreAllocateForVertices(int vertexCount)
```

Heuristic: Allocates `vertexCount × 3.2` edges

**Benefits:**
- Reduces EdgePool page allocations
- Stabilizes memory footprint
- Improves bulk insertion performance

## Bootstrap Process

**Automatic bootstrap** occurs when:
- Third vertex added (minimum for triangle)
- Bootstrap utility finds suitable initial triangle

**Requirements:**
- At least 3 non-collinear vertices
- Vertices sufficiently separated (relative to thresholds)

**Initial Triangle:**
- Selects well-separated, non-collinear points
- Creates 3 real edges + 3 ghost edges
- Establishes counter-clockwise orientation

[See: Bootstrap and Point Location](./bootstrap-and-walk.md)

## Query Operations

### Vertices

```csharp
public IList<IVertex> GetVertices()
```

Returns all vertices currently in TIN (expensive, materializes list).

### Triangles

```csharp
public IEnumerable<SimpleTriangle> GetTriangles()
```

Returns enumerable for triangle iteration (efficient, lazy).

### Bounds

```csharp
public (double Left, double Top, double Width, double Height)? GetBounds()
```

Returns axis-aligned bounding box, or `null` if not bootstrapped.

### Navigation

```csharp
public IIncrementalTinNavigator GetNavigator()
```

Creates navigator for interpolation and spatial queries.

[See: Navigator Usage](./bootstrap-and-walk.md#navigator)

## Constraint Operations

### Adding Constraints

```csharp
public void AddConstraints(IList<IConstraint> constraints, bool restoreConformity)
```

**Parameters:**
- `constraints` - Linear or polygon constraints
- `restoreConformity` - Whether to restore Delaunay after insertion

**Process:**
1. Validate TIN is bootstrapped
2. Lock TIN (prevents modifications during constraint processing)
3. Delegate to ConstraintProcessor for insertion
4. Optionally restore Delaunay conformity in non-constrained regions
5. Mark TIN as non-conformant if edges forced

**Constraint Types:**

**LinearConstraint:**
```csharp
var constraint = new LinearConstraint();
constraint.Add(vertex1, false);
constraint.Add(vertex2, false);
constraint.Complete();
tin.AddConstraints(new[] { constraint }, true);
```

**PolygonConstraint:**
```csharp
var polygon = new PolygonConstraint();
polygon.Add(v1, false);
polygon.Add(v2, false);
polygon.Add(v3, false);
polygon.Complete();
// Interior automatically marked
```

[See: Constraint Processing](./constraint-processing.md)

## Locking and State

### Lock State

```csharp
public bool IsLocked()
public void SetLocked(bool locked)
```

**Locked TIN:**
- Prevents vertex addition/removal
- Allows queries and navigation
- Automatically locked during constraint processing

### Bootstrap State

```csharp
public bool IsBootstrapped()
```

Indicates whether initial triangle successfully created.

### Conformity

```csharp
public bool IsDelaunayConformant()
```

Indicates if TIN satisfies Delaunay criterion throughout.
- `false` after constraint addition (constrained edges violate criterion)
- Can be restored in non-constrained regions

## Statistics

### Triangle Count

```csharp
public TriangleCount CountTriangles()
```

Returns detailed triangle statistics:

```csharp
public class TriangleCount
{
    public int InteriorRegionCount { get; }     // Real triangles
    public int GhostCount { get; }               // Boundary ghosts
    public int TotalCount { get; }               // Interior + Ghost
    public int ConstrainedRegionCount { get; }   // Constrained interior
    public int ConstrainedRegionInteriorCount { get; }
    public int ConstrainedRegionExteriorCount { get; }
}
```

### Edge Statistics

```csharp
public int GetMaximumEdgeAllocationIndex()
```

Returns highest edge index allocated (for diagnostics).

## Memory Management

### Disposal

```csharp
public void Dispose()
```

Releases EdgePool resources. TIN unusable after disposal.

**Pattern:**
```csharp
using var tin = new IncrementalTin();
tin.AddSorted(vertices);
// Automatically disposed at end of scope
```

### Clear

```csharp
public void Clear()
```

Resets TIN to empty state:
- Clears vertex list
- Clears constraints
- Clears edge pool
- Resets bounds
- Unlocks TIN

Useful for reusing TIN instance with new data.

## Internal Components

### StochasticLawsonsWalk

Point location algorithm:
- Walks from search edge toward query point
- Uses orientation tests to guide direction
- Stochastic perturbation avoids infinite loops
- Fallback to extended precision for degenerate cases

[See: Point Location](./bootstrap-and-walk.md#lawsons-walk)

### BootstrapUtility

Initial triangle construction:
- Finds well-separated vertices
- Ensures non-collinearity
- Establishes orientation
- Creates initial ghost edges

### ConstraintProcessor

Constraint insertion engine:
- Pinwheel search for existing edges
- Tunneling through intersecting edges
- Cavity filling
- Region marking (interior/exterior)

[See: Constraint Processing](./constraint-processing.md)

### GeometricOperations

Geometric predicates:
- Orientation tests (counter-clockwise)
- In-circle tests (Delaunay criterion)
- Circumcircle calculations
- Extended precision fallbacks

[See: Geometric Operations](./geometric-operations.md)

## Thread Safety

**Not thread-safe** for concurrent modifications.

**Safe patterns:**
- Single-threaded construction
- Read-only queries after construction (from multiple threads)
- External synchronization for modification

**Parallel interpolation:**
```csharp
// Each thread gets own navigator
Parallel.ForEach(queryPoints, () => tin.GetNavigator(),
    (point, state, nav) => {
        return interpolator.Interpolate(point.X, point.Y, null, nav);
    },
    _ => {});
```

## Performance Characteristics

### Time Complexity

| Operation | Average | Worst Case |
|-----------|---------|------------|
| Add vertex | O(1) amortized | O(n) |
| Bootstrap | O(n log n) | O(n²) |
| Add sorted vertices | O(n log n) | O(n²) |
| Query triangle | O(log n) | O(n) |
| Add constraint | O(k) | O(n) where k = intersected edges |

### Memory Complexity

- **Vertices:** O(n)
- **Edges:** O(n) - approximately 3n edges
- **EdgePool overhead:** ~20% for page structure

### Optimization Tips

1. **Hilbert sort vertices** before adding
2. **Pre-allocate** edge pool if count known
3. **Reuse TIN** instance rather than recreate
4. **Batch operations** when possible
5. **Avoid GetVertices()** in hot paths (expensive)

## Usage Examples

### Basic Workflow

```csharp
using var tin = new IncrementalTin(nominalSpacing: 1.0);

// Add vertices
tin.AddSorted(vertices);

// Check if successful
if (!tin.IsBootstrapped())
{
    throw new InvalidOperationException("Bootstrap failed");
}

// Query
var bounds = tin.GetBounds();
var triangles = tin.GetTriangles();
foreach (var tri in triangles)
{
    // Process triangle
}
```

### With Constraints

```csharp
using var tin = new IncrementalTin(1.0);
tin.AddSorted(vertices);

// Create boundary constraint
var boundary = new LinearConstraint();
boundary.Add(vertices[0], false);
boundary.Add(vertices[10], false);
boundary.Add(vertices[20], false);
boundary.Complete();

tin.AddConstraints(new[] { boundary }, restoreConformity: true);

// Check conformity
bool isDelaunay = tin.IsDelaunayConformant();  // false (has constraints)
```

### Interpolation

```csharp
using var tin = new IncrementalTin(1.0);
tin.AddSorted(vertices);

var interpolator = new NaturalNeighborInterpolator(tin);
var nav = tin.GetNavigator();

double z = interpolator.Interpolate(x, y, null, nav);
```

## Diagnostic Support

### PrintDiagnostics

```csharp
public void PrintDiagnostics(TextWriter writer)
```

Outputs diagnostic information:
- Vertex count
- Edge allocation
- Bootstrap status
- Constraint count
- Memory statistics

**Example:**
```csharp
tin.PrintDiagnostics(Console.Out);
```

## Related Documentation

- [Triangulation Algorithm](./triangulation.md)
- [Constraint Processing](./constraint-processing.md)
- [Bootstrap and Point Location](./bootstrap-and-walk.md)
- [Geometric Operations](./geometric-operations.md)
- [Edge Pool Management](../data-structures/edge-pool.md)

---

**Last Updated:** November 26, 2025
