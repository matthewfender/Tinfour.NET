# Tinfour.Core Library

**Module:** Tinfour.Core  
**Namespace:** `Tinfour.Core.*`  
**Target Framework:** .NET 8.0

## Overview

Tinfour.Core is the foundational library providing all core triangulation, interpolation, and geometric computation capabilities. It contains no external dependencies and implements faithful ports of the Java Tinfour algorithms.

## Assembly Organization

### Namespace Structure

```
Tinfour.Core
├── Common/              # Interfaces and shared types
├── Standard/            # Primary TIN implementation
├── Edge/                # Edge data structures
├── Interpolation/       # Interpolation algorithms
├── Contour/             # Contour generation
├── Voronoi/             # Voronoi diagram generation
├── Utils/               # Utility classes
└── Diagnostics/         # Debugging and profiling tools
```

## Core Components

### Common (`Tinfour.Core.Common`)

**Purpose:** Interfaces, enumerations, and shared data types

**Key Types:**
- `IVertex` - Vertex interface
- `IQuadEdge` - Edge interface
- `IIncrementalTin` - TIN interface
- `IConstraint` - Constraint base interface
- `ILinearConstraint`, `IPolygonConstraint` - Constraint specializations
- `SimpleTriangle` - Triangle representation
- `TriangleCount` - Triangle statistics

[See: Data Structures Overview](../data-structures/overview.md)

### Standard (`Tinfour.Core.Standard`)

**Purpose:** Primary incremental TIN implementation

**Key Classes:**
- `IncrementalTin` - Main triangulation engine
- `ConstraintProcessor` - Constraint handling for CDT
- `BootstrapUtility` - Initial triangle construction
- `StochasticLawsonsWalk` - Point location algorithm
- `SimpleTriangleIterator` - Triangle enumeration
- `IncrementalTinNavigator` - Query and navigation support

[See: Triangulation](./triangulation.md), [Incremental TIN](./incremental-tin.md), [Constraint Processing](./constraint-processing.md)

### Edge (`Tinfour.Core.Edge`)

**Purpose:** Edge data structures and memory management

**Key Classes:**
- `QuadEdge` - Primary edge representation
- `QuadEdgePartner` - Dual edge
- `EdgePool` - Memory pool for edge allocation
- `EdgePage` - Paged storage

[See: QuadEdge](../data-structures/quad-edge.md), [Edge Pool](../data-structures/edge-pool.md)

### Interpolation (`Tinfour.Core.Interpolation`)

**Purpose:** Surface interpolation algorithms

**Key Classes:**
- `IInterpolatorOverTin` - Interpolator interface
- `TriangularFacetInterpolator` - Linear interpolation
- `NaturalNeighborInterpolator` - Sibson's method
- `InverseDistanceWeightingInterpolator` - IDW interpolation
- `IVertexValuator` - Custom Z-value extraction
- `TinRasterizer` - Raster generation from TIN

[See: Interpolation Overview](../interpolation/interpolation-overview.md)

### Contour (`Tinfour.Core.Contour`)

**Purpose:** Contour line and region extraction

**Key Classes:**
- `ContourBuilderForTin` - Main contour generation engine
- `Contour` - Contour line representation
- `ContourRegion` - Polygon region representation
- `PerimeterLink`, `TipLink` - Boundary handling

[See: Contour Generation](../analysis/contour-generation.md)

### Voronoi (`Tinfour.Core.Voronoi`)

**Purpose:** Voronoi diagram generation from Delaunay triangulation

**Key Classes:**
- `BoundedVoronoiDiagram` - Main Voronoi generator
- `ThiessenPolygon` - Voronoi cell representation
- `BoundedVoronoiBuildOptions` - Configuration options
- `PerimeterVertex` - Boundary vertex handling

[See: Voronoi Diagrams](../analysis/voronoi-diagrams.md)

### Utils (`Tinfour.Core.Utils`)

**Purpose:** Utility classes and helper functions

**Key Classes:**
- `HilbertSort` - Hilbert curve spatial ordering
- `GeometricOperations` - Predicates and calculations
- `Thresholds` - Precision tolerance management
- `Polyside` - Point-in-polygon testing
- `Vertex` - Concrete vertex implementation
- `VertexMergerGroup` - Coincident vertex handling

[See: Utilities](../utilities/hilbert-sort.md), [Thresholds](../utilities/thresholds.md)

## Key Interfaces

### IIncrementalTin

Primary interface for triangulation operations:

```csharp
public interface IIncrementalTin : IDisposable
{
    // Vertex operations
    bool Add(IVertex vertex);
    bool Add(IEnumerable<IVertex> vertices, VertexOrder order);
    void PreAllocateForVertices(int vertexCount);
    
    // Constraint operations
    void AddConstraints(IList<IConstraint> constraints, bool restoreConformity);
    
    // Query operations
    IList<IVertex> GetVertices();
    IEnumerable<SimpleTriangle> GetTriangles();
    (double Left, double Top, double Width, double Height)? GetBounds();
    
    // Navigation
    IIncrementalTinNavigator GetNavigator();
    
    // Statistics
    TriangleCount CountTriangles();
    bool IsBootstrapped();
}
```

[See: Incremental TIN](./incremental-tin.md)

### IQuadEdge

Core edge interface for navigation and constraint handling:

```csharp
public interface IQuadEdge
{
    // Vertices
    IVertex GetA();
    IVertex GetB();
    
    // Navigation
    IQuadEdge GetForward();
    IQuadEdge GetReverse();
    IQuadEdge GetDual();
    IQuadEdge GetDualFromReverse();
    
    // Properties
    int GetIndex();
    double GetLength();
    
    // Constraints
    bool IsConstrained();
    int GetConstraintIndex();
    bool IsConstraintLineMember();
    bool IsConstraintRegionBorder();
    bool IsConstraintRegionInterior();
}
```

[See: QuadEdge](../data-structures/quad-edge.md)

### IInterpolatorOverTin

Base interface for interpolation:

```csharp
public interface IInterpolatorOverTin
{
    double Interpolate(double x, double y, IVertexValuator? valuator);
    void ResetForChangeToTin();
}
```

[See: Interpolation Overview](../interpolation/interpolation-overview.md)

## Design Patterns

### Interface-Based Design

All major components expose interfaces to enable:
- Multiple implementations (standard vs. SVM)
- Testing and mocking
- Future extensibility

### Object Pooling

EdgePool implements object pooling for memory efficiency:
- Paged allocation (EdgePage)
- Even/odd index pairing for dual edges
- Reuse of deallocated edges

### Value Types for Performance

Strategic use of structs:
- `Vertex` - readonly struct for cache efficiency
- Reduces heap allocations and GC pressure

### NullVertex Pattern

Avoids boxing overhead of `Nullable<Vertex>`:
- `Vertex._NullVertex` sentinel value
- `IsNullVertex()` extension method
- Enables ghost vertex representation

[See: Memory Management](../data-structures/memory-management.md)

## Threading and Concurrency

**Current Status:** Single-threaded

The TIN data structure is **not thread-safe**. Concurrent modifications require external synchronization.

**Read-only operations** after construction can be safely parallelized:
- Interpolation queries
- Triangle enumeration
- Contour generation

**Future Work:** Thread-safe query operations, parallel construction strategies

## Performance Considerations

### Optimal Usage Patterns

1. **Pre-sort with Hilbert curve** - `tin.AddSorted(vertices)`
2. **Pre-allocate edge pool** - `tin.PreAllocateForVertices(count)`
3. **Batch insertion** - Add vertices in bulk rather than one-by-one
4. **Reuse TIN instance** - Expensive to bootstrap; reuse where possible

### Memory Footprint

Approximate memory usage:
- **Vertex:** 32 bytes (struct in collection)
- **QuadEdge pair:** ~120 bytes (dual edges + metadata)
- **Ratio:** ~3 edges per vertex average
- **Formula:** `Memory ≈ N_vertices × (32 + 3 × 120) bytes`

Example: 100,000 vertices ≈ 39 MB

### Performance Bottlenecks

Current known bottlenecks:
- LINQ usage in diagnostic accessors (GetVertices, GetEdges)
- Allocations during triangle iteration
- Ghost vertex orientation tests

Optimizations in progress:
- Aggressive inlining on hot paths
- Span<T> for array operations
- Vectorization opportunities (SIMD)

[See: Original Performance Notes](../../Copilot/TinfourNetOptimizations.md)

## Testing

Comprehensive test suites in `Tinfour.Core.Tests`:

- **Unit tests** - Individual component validation
- **Integration tests** - End-to-end triangulation scenarios
- **Geometric tests** - Predicate correctness
- **Constraint tests** - CDT validation
- **Reference tests** - Comparison with Java results

## Dependencies

**None** - Tinfour.Core is dependency-free

Uses only:
- .NET 8.0 BCL
- `System.Numerics` (Vector2 for rendering support)

## Related Documentation

- [Triangulation](./triangulation.md)
- [Incremental TIN](./incremental-tin.md)
- [Constraint Processing](./constraint-processing.md)
- [Data Structures Overview](../data-structures/overview.md)
- [Interpolation Overview](../interpolation/interpolation-overview.md)

---

**Last Updated:** November 26, 2025
