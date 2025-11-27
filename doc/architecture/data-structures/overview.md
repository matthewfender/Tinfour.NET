# Data Structures Overview

**Module:** Tinfour.Core  
**Focus:** Efficient geometric data representation

## Overview

Tinfour.NET employs carefully designed data structures optimized for performance, memory efficiency, and geometric computations. The design balances .NET idioms with the proven patterns from the Java implementation.

## Core Data Types

### Vertex

**Type:** `readonly struct`  
**Size:** 32 bytes (in collections)  
**Purpose:** Point representation with 3D coordinates

```csharp
public readonly struct Vertex : IVertex
{
    public double X { get; }
    public double Y { get; }
    public float Z { get; }
    public int Index { get; }
    // ... additional metadata
}
```

**Design Decisions:**
- Struct for **value semantics** and **cache locality**
- Double precision (x, y) for GIS coordinate ranges
- Float precision (z) for elevation (memory trade-off)
- Immutable (readonly) for thread-safety and clarity

**NullVertex Pattern:**
```csharp
public static readonly Vertex NullVertex = new(double.NaN, double.NaN, double.NaN, -1, BitNull, 0, 0, 0);

// Usage
if (vertex.IsNullVertex()) { /* handle ghost */ }
```

Avoids `Nullable<Vertex>` boxing overhead while maintaining Java-compatible null semantics.

[Details: Vertex Structure](./vertex.md)

### QuadEdge

**Type:** `class` (reference type)  
**Purpose:** Edge representation with dual structure  
**Memory:** ~60 bytes per edge (dual pair ~120 bytes)

```csharp
public class QuadEdge : IQuadEdge
{
    protected internal int _index;
    protected internal QuadEdge _dual;
    protected internal IVertex _v;
    protected internal IQuadEdge? _f;  // forward
    protected internal IQuadEdge? _r;  // reverse
}
```

**Dual Edge Structure:**

Each QuadEdge has a companion `QuadEdgePartner` representing the opposite orientation:

```
QuadEdge (A→B):
  - Vertex: A
  - Forward: next CCW edge from A
  - Reverse: previous CCW edge to A
  
QuadEdgePartner (B→A):
  - Vertex: B  
  - Forward: next CCW edge from B
  - Reverse: previous CCW edge to B
```

**Why Classes:**
- Requires reference semantics for navigation
- Mutable state (forward/reverse links)
- Managed by EdgePool for reuse

[Details: QuadEdge Structure](./quad-edge.md)

### EdgePool

**Type:** `class`  
**Purpose:** Memory pool for edge allocation/reuse  
**Pattern:** Object pooling with paged allocation

```csharp
public class EdgePool : IDisposable, IEnumerable<IQuadEdge>
{
    private List<EdgePage> _pages;
    private QuadEdge? _freeEdge;
    // ...
}
```

**EdgePage Structure:**
- Fixed-size page (default: 1000 edge pairs)
- Contiguous allocation for cache efficiency
- Page expansion as needed

**Allocation Strategy:**
- Even indices for base edges
- Odd indices for dual partners
- Free list for deallocated edges

[Details: Edge Pool Management](./edge-pool.md)

## Interface Hierarchy

### IVertex

Base vertex interface:

```csharp
public interface IVertex
{
    double X { get; }
    double Y { get; }
    double Z { get; }
    int GetIndex();
    
    // Metadata
    bool IsSynthetic();
    bool IsNullVertex();
}
```

**Implementations:**
- `Vertex` - Standard struct implementation
- `VertexMergerGroup` - Groups coincident vertices

### IQuadEdge

Core edge interface:

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
    
    // Metadata
    int GetIndex();
    int GetBaseIndex();
    double GetLength();
    
    // Constraints
    bool IsConstrained();
    int GetConstraintIndex();
    // ...
}
```

**Implementations:**
- `QuadEdge` - Primary edge (even index)
- `QuadEdgePartner` - Dual edge (odd index)

### IIncrementalTin

TIN data structure interface:

```csharp
public interface IIncrementalTin : IDisposable
{
    // Vertex operations
    bool Add(IVertex vertex);
    bool Add(IEnumerable<IVertex> vertices, VertexOrder order);
    
    // Queries
    IList<IVertex> GetVertices();
    IEnumerable<SimpleTriangle> GetTriangles();
    IIncrementalTinNavigator GetNavigator();
    
    // Constraints
    void AddConstraints(IList<IConstraint> constraints, bool restoreConformity);
}
```

## Supporting Structures

### SimpleTriangle

Lightweight triangle representation:

```csharp
public class SimpleTriangle
{
    public SimpleTriangle(IQuadEdge edgeA, IQuadEdge edgeB, IQuadEdge edgeC)
    {
        EdgeA = edgeA;
        EdgeB = edgeB;
        EdgeC = edgeC;
    }
    
    public IVertex GetVertexA() => EdgeA.GetA();
    public IVertex GetVertexB() => EdgeB.GetA();
    public IVertex GetVertexC() => EdgeC.GetA();
    
    // Computed properties
    public double GetArea();
    public (double X, double Y, double Z) GetCircumcenter();
}
```

### VertexMergerGroup

Handles coincident vertices:

```csharp
public class VertexMergerGroup : IVertex
{
    private readonly List<Vertex> _vertices;
    private readonly Vertex _representative;
    
    public bool Contains(Vertex v);
    // Implements IVertex using representative
}
```

Used when multiple vertices occupy the same location within tolerance.

### Thresholds

Precision tolerance management:

```csharp
public class Thresholds
{
    public Thresholds(double nominalPointSpacing)
    {
        NominalPointSpacing = nominalPointSpacing;
        VertexTolerance = nominalPointSpacing / 1.0e+7;
        VertexTolerance2 = VertexTolerance * VertexTolerance;
        // ...
    }
    
    public double GetVertexTolerance();
    public double GetVertexTolerance2();
    public double GetHalfPlaneThreshold();
}
```

[Details: Thresholds and Precision](../utilities/thresholds.md)

## Memory Management Patterns

### Value vs Reference Types

**Use structs for:**
- Small, immutable types (Vertex)
- Frequently copied data
- Cache-friendly layout

**Use classes for:**
- Mutable state requiring identity (QuadEdge)
- Large objects
- Objects requiring inheritance

### Object Pooling

**EdgePool pattern:**
1. Pre-allocate pages of edges
2. Maintain free list of deallocated edges
3. Reuse edges rather than GC allocation
4. Clear edge state on reuse

**Benefits:**
- Reduced GC pressure
- Predictable memory footprint
- Improved cache locality

### NullVertex Sentinel

**Alternatives considered:**
- `Nullable<Vertex>` - Rejected due to boxing overhead
- `IVertex?` - Rejected for performance in hot paths
- `Vertex?` (C# 8 nullable) - Rejected for struct semantics

**Chosen solution:**
- Sentinel value with NaN coordinates
- Extension method for testing: `IsNullVertex()`
- Java-compatible semantics
- Zero boxing/allocation overhead

[Details: Memory Management](./memory-management.md)

## Enumeration and Iteration

### SimpleTriangleIterator

Efficient triangle enumeration:

```csharp
public class SimpleTriangleIterator : IEnumerable<SimpleTriangle>
{
    private readonly IncrementalTin _tin;
    private readonly BitArray _visited;
    
    public IEnumerator<SimpleTriangle> GetEnumerator()
    {
        // Depth-first traversal of triangulation
        // Uses visited bitmap to avoid duplicates
    }
}
```

**Performance:**
- O(n) traversal
- Minimal allocation (reuses BitArray)
- Skip ghost triangles automatically

### Edge Enumeration

```csharp
foreach (var edge in tin.GetEdgeIterator())
{
    // Process each edge (including ghosts)
}
```

## Constraint Metadata

### Edge Flags

Edges store constraint information:

```csharp
// Stored in QuadEdgePartner
private int _constraintIndex;  // Which constraint owns this edge
private bool _isConstraintLineMember;
private bool _isConstraintRegionBorder;
private bool _isConstraintRegionInterior;
```

**Region Classification:**
- Border: Edge is constraint boundary
- Interior: Edge is within constrained region
- Line member: Edge is part of linear constraint

## Performance Considerations

### Cache Efficiency

**Vertex as struct:**
- Stored inline in arrays/lists
- Sequential memory layout
- Fewer pointer indirections

**EdgePage allocation:**
- Contiguous edge storage
- Improved CPU cache utilization
- Reduced TLB misses

### Allocation Reduction

**PreAllocation:**
```csharp
tin.PreAllocateForVertices(100000); // Estimates 3.2 edges per vertex
```

**Hilbert Sorting:**
```csharp
tin.AddSorted(vertices); // Spatial locality
```

### Hot Path Optimization

**Aggressive inlining:**
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public int GetIndex() => _index;
```

Applied to:
- Property getters on critical paths
- Small navigation methods
- Predicate tests

## Comparison with Java

### Structural Differences

| Aspect | Java | C# |
|--------|------|-----|
| Vertex | class | struct |
| Null handling | null reference | NullVertex sentinel |
| Collections | ArrayList | List<T> |
| Iteration | Iterator | IEnumerable<T> |
| Precision | double throughout | double (x,y), float (z) |

### Maintained Similarities

- QuadEdge dual structure (identical)
- Edge pool paging strategy (identical)
- Interface hierarchies (equivalent)
- Constraint metadata storage (identical)

## Related Documentation

- [Vertex Structure](./vertex.md)
- [QuadEdge and Dual Edges](./quad-edge.md)
- [Edge Pool Management](./edge-pool.md)
- [Memory Management Patterns](./memory-management.md)
- [Thresholds and Precision](../utilities/thresholds.md)

---

**Last Updated:** November 26, 2025
