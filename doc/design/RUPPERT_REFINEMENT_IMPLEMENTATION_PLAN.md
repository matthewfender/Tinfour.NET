# Plan: Implementing Ruppert's Delaunay Refinement Algorithm

**Date:** December 16, 2025
**Author:** Claude Code
**Status:** Planning
**Branch:** feature/increase-constraint-number-limitation
**Reference:** Tinfour Java `org.tinfour.refinement.RuppertRefiner`

---

## Executive Summary

This document details the implementation plan for porting Ruppert's Delaunay Refinement algorithm from the original Tinfour (Java) library to Tinfour.NET. The Java implementation (~47KB, battle-tested) uses Shewchuk's off-center technique and includes sophisticated handling of pathological cases via seditious edge detection and shell indexing.

**Goal:** Port the Java implementation as faithfully as possible while adapting to C# idioms and the existing Tinfour.NET infrastructure.

---

## Source Reference

### Java Files to Port

| File | Size | Purpose |
|------|------|---------|
| `IDelaunayRefiner.java` | ~3.7KB | Interface with `refine()` and `refineOnce()` methods |
| `RuppertRefiner.java` | ~47KB | Complete implementation |

### Repository Location

```
https://github.com/gwlucastrig/Tinfour
└── core/src/main/java/org/tinfour/refinement/
    ├── IDelaunayRefiner.java
    ├── RuppertRefiner.java
    └── package-info.java
```

---

## Algorithm Overview

### Core Refinement Loop

```
1. Initialize constrained segments set from TIN edges
2. Build corner info (minimum angles at constraint vertices)
3. Initialize bad triangle priority queue

4. While work remains:
   a. Process encroached segments first (split at midpoint)
   b. Then process bad triangles (insert off-center or circumcenter)
   c. Update local queues after each insertion

5. Terminate when no encroached segments and no bad triangles remain
```

### Key Innovation: Off-Center Insertion (Shewchuk)

Instead of inserting at the circumcenter, the algorithm computes an "off-center" point:

1. Find the shortest edge of the bad triangle (endpoints p, q)
2. Compute midpoint m = (p + q) / 2
3. Compute normal vector n perpendicular to edge, pointing into triangle
4. Compute off-center distance: d = min(circumradius_distance, β × edge_length)
5. Insert at: off = m + d × n

This prevents insertions near constrained segments, reducing cascading splits.

### Seditious Edge Handling

Pathological cases occur near acute constraint corners (< 60°). The algorithm tracks:

- **Shell index**: log₂(distance from corner) - identifies concentric shells
- **Critical corners**: Constraint vertices with incident angles < 60°
- **Seditious edges**: Edges connecting midpoints on the same shell around a critical corner

Seditious triangles/encroachments can be skipped to prevent infinite cascades.

---

## Java Implementation Structure

### Constants

```java
private static final double DEFAULT_MIN_TRIANGLE_AREA = 1e-3;
private static final double NEAR_VERTEX_REL_TOL = 1e-9;
private static final double NEAR_EDGE_REL_TOL = 1e-9;
private static final double SHELL_BASE = 2.0;
private static final double SHELL_EPS = 1e-9;
private static final double SMALL_CORNER_DEG = 60.0;
private static final double SQRT2 = Math.sqrt(2.0);
```

### Fields

```java
// Core references
private final IIncrementalTin tin;
private final IIncrementalTinNavigator navigator;

// Quality parameters (derived from minAngleDeg)
private final double minAngleRad;
private final double beta;              // 1 / (2 * sin(minAngle))
private final double rhoTarget;         // Same as beta
private final double rhoMin;            // max(sqrt(2), rhoTarget) if enforcing sqrt2 guard
private final double minTriangleArea;

// Configuration flags
private final boolean skipSeditiousTriangles;
private final boolean ignoreSeditiousEncroachments;
private final boolean interpolateZ;

// Interpolation
private final TriangularFacetSpecialInterpolator interpolator;

// Vertex tracking
private Vertex lastInsertedVertex = null;
private final Map<Vertex, VData> vdata = new IdentityHashMap<>();
private Map<Vertex, CornerInfo> cornerInfo = new IdentityHashMap<>();
private int vertexIndexer;

// Constrained segments
private final Set<IQuadEdge> constrainedSegments = new HashSet<>();
private boolean constrainedSegmentsInitialized = false;

// Bad triangle queue (priority = cross² of triangle, worst first)
private final PriorityQueue<BadTri> badTriangles;
private boolean badTrianglesInitialized = false;

// Encroachment queue
private final Queue<IQuadEdge> encroachedSegmentQueue = new ArrayDeque<>();
private final Set<IQuadEdge> inEncroachmentQueue;
```

### Inner Classes

```java
// Triangle queued for refinement
private static final class BadTri {
    final IQuadEdge repEdge;    // Representative edge (for triangle lookup)
    final double priority;       // Cross product squared (larger = worse)
}

// Vertex type classification
private enum VType {
    INPUT,        // Original input vertex
    MIDPOINT,     // Created by segment split
    OFFCENTER,    // Created by off-center insertion
    CIRCUMCENTER  // Created by circumcenter insertion (fallback)
}

// Vertex metadata
private static class VData {
    VType t;           // How vertex was created
    Vertex corner;     // Associated critical corner (for midpoints)
    int shell;         // Shell index relative to corner
}

// Corner angle information
private static class CornerInfo {
    double minAngleDeg = 180.0;  // Minimum angle at this corner
}
```

---

## Method Catalog

### Public Interface

| Method | Purpose |
|--------|---------|
| `RuppertRefiner(tin, minAngleDeg, ...)` | Constructor with options |
| `static fromEdgeRatio(tin, ratio)` | Factory from circumradius-to-edge ratio |
| `boolean refine()` | Main loop, returns true if converged |
| `Vertex refineOnce()` | Single step, returns inserted vertex or null |

### Initialization Methods

| Method | Purpose |
|--------|---------|
| `initConstrainedSegments()` | Collect constrained edges, check initial encroachments |
| `initBadTriangleQueue()` | Populate priority queue with poor-quality triangles |
| `buildCornerInfo()` | Compute minimum angles at constraint vertices |

### Quality Assessment

| Method | Purpose |
|--------|---------|
| `triangleBadPriority(t)` | Returns cross² if triangle needs refinement, else ≤0 |

**Quality Formula:**
```
For triangle with vertices A, B, C:
1. Compute edge lengths squared: la, lb, lc
2. Compute cross product squared: cross² = (AB × AC)²
3. Find shortest edge and compute pairProd = (other two lengths squared)
4. Triangle is bad if: pairProd ≥ 4 × ρ²min × cross²
5. Priority = cross² (larger cross = worse triangle)
```

### Encroachment Detection

| Method | Purpose |
|--------|---------|
| `closestEncroacherOrNull(edge)` | Find apex vertex encroaching on segment's diametral circle |
| `findEncroachedSegment()` | Get next encroached segment from queue |
| `addEncroachmentCandidate(e)` | Add segment to encroachment queue |
| `isEncroachedByPoint(seg, p)` | Test if point encroaches segment |
| `firstEncroachedByPoint(p)` | Find any segment encroached by candidate point |

**Encroachment Test (Gabriel Circle):**
```
For segment AB and point P:
midpoint = (A + B) / 2
radius² = |AB|² / 4
encroached = |P - midpoint|² < radius²
```

### Insertion Methods

| Method | Purpose |
|--------|---------|
| `insertOffcenterOrSplit(tri)` | Primary insertion - off-center or split if encroaches |
| `insertCircumcenterOrSplit(tri)` | Fallback insertion at circumcenter |
| `splitSegmentSmart(seg)` | Split constrained segment at midpoint with shell tracking |

**Off-Center Calculation:**
```
1. Find shortest edge (p, q) of triangle
2. midpoint m = (p + q) / 2
3. normal n = perpendicular to (p,q), pointing into triangle, normalized
4. circumcenter distance dCirc = |circumcenter - m|
5. off-center distance d = min(dCirc, β × |pq|)
6. insertion point = m + d × n
```

### Local Update Methods

| Method | Purpose |
|--------|---------|
| `addVertex(v, type, corner, shell)` | Insert vertex and update local queues |
| `updateBadTrianglesAroundVertex(v, s)` | Re-evaluate triangles around vertex |
| `updateConstrainedSegmentsAroundVertex(v)` | Update segment set and encroachment queue |
| `nextBadTriangleFromQueue()` | Get next valid bad triangle |

### Seditious Edge Handling

| Method | Purpose |
|--------|---------|
| `shellIndex(z, x, y)` | Compute shell index: round(log(distance) / log(2)) |
| `isCornerCritical(z)` | Check if corner angle < 60° |
| `isSeditious(u, v)` | Check if edge connects same-shell midpoints at critical corner |
| `shouldIgnoreEncroachment(e, witness)` | Determine if encroachment should be skipped |
| `sameShell(z, a, b)` | Check if two vertices are on same shell around corner |

### Helper Methods

| Method | Purpose |
|--------|---------|
| `nearestNeighbor(x, y)` | Find nearest vertex via navigator |
| `firstNearConstrainedEdgeInterior(v, tol)` | Check proximity to constraint edge interior |
| `isNearEdgeInterior(seg, px, tol)` | Test if point is within tolerance of edge interior |
| `checkEdge(e, p)` | Helper for encroachment checking |
| `angleSmallBetweenDeg(a, b)` | Compute smaller angle between two directions |

---

## C# Implementation Plan

### Target Location

```
Tinfour.Core/
└── Refinement/                    (new folder)
    ├── IDelaunayRefiner.cs
    ├── RuppertRefiner.cs
    └── RuppertOptions.cs          (configuration class)
```

### Java to C# Mapping

| Java | C# Equivalent |
|------|---------------|
| `IdentityHashMap<Vertex, VData>` | `Dictionary<IVertex, VData>` with `ReferenceEqualityComparer` |
| `PriorityQueue<BadTri>` (max-heap via Comparator.reversed()) | `PriorityQueue<BadTri, double>` (min-heap, negate priority) |
| `ArrayDeque<IQuadEdge>` | `Queue<IQuadEdge>` |
| `Collections.newSetFromMap(new IdentityHashMap<>())` | `HashSet<IQuadEdge>` with `ReferenceEqualityComparer` |
| `tin.vertices()` | `tin.GetVertices()` |
| `tin.triangles()` | `tin.GetTriangles()` |
| `tin.edges()` | `tin.GetEdges()` |
| `tin.add(vertex)` | `tin.Add(vertex)` |
| `navigator.getContainingTriangle(x, y)` | `navigator.GetContainingTriangle(x, y)` (need to verify) |
| `navigator.getNearestVertex(x, y)` | Need to check/implement |
| `navigator.getNeighborEdge(x, y)` | Need to check/implement |
| `edge.pinwheel()` | `edge.GetPinwheel()` |
| `edge.getBaseReference()` | `edge.GetBaseReference()` |
| `edge.getForward()` | `edge.GetForward()` |
| `edge.getForwardFromDual()` | `edge.GetDual().GetForward()` |
| `edge.getLengthSq()` | `edge.GetLength()` squared, or add method |
| `vertex.getDistanceSq(x, y)` | `vertex.GetDistance(x, y)` squared, or add method |
| `tin.splitEdge(edge, t, z)` | **Need to verify/implement** |
| `SimpleTriangle(tin, edge)` | `new SimpleTriangle(edge)` |
| `TriangularFacetSpecialInterpolator` | Use existing interpolator or create |

### Dependencies to Verify/Implement

#### Critical - Must Exist or Be Added

| Dependency | Status | Notes |
|------------|--------|-------|
| `IIncrementalTin.SplitEdge(edge, t, z)` | **NEED TO CHECK** | Split constrained edge at parameter t |
| `IIncrementalTinNavigator.GetContainingTriangle(x, y)` | **NEED TO CHECK** | Point location returning triangle |
| `IIncrementalTinNavigator.GetNearestVertex(x, y)` | **NEED TO CHECK** | Nearest vertex query |
| `IIncrementalTinNavigator.GetNeighborEdge(x, y)` | **NEED TO CHECK** | Edge near point |
| `IQuadEdge.GetLengthSquared()` | May need to add | Avoid sqrt for comparison |
| `IVertex.GetDistanceSquared(x, y)` | May need to add | Avoid sqrt for comparison |
| `SimpleTriangle.GetContainingRegion()` | **NEED TO CHECK** | For constraint region membership |
| `SimpleTriangle.GetShortestEdge()` | May need to add | For local scale calculation |

#### Interpolation

| Option | Description |
|--------|-------------|
| Use `TriangularFacetInterpolator` | Existing class, may work |
| Use `NaturalNeighborInterpolator` | For smoother Z values |
| Create `IRefinementZStrategy` | Pluggable Z computation (per research doc) |

---

## Detailed Implementation Steps

### Phase 1: Infrastructure

1. **Create Refinement folder** under `Tinfour.Core/`

2. **Add missing TIN methods** (if needed):
   - `IIncrementalTin.SplitEdge(IQuadEdge edge, double t, double z)`
   - Verify navigator methods exist

3. **Add helper methods** (if needed):
   - `IQuadEdge.GetLengthSquared()`
   - `IVertex.GetDistanceSquared(double x, double y)`

### Phase 2: Interface and Supporting Types

4. **Create `IDelaunayRefiner.cs`**:
```csharp
namespace Tinfour.Core.Refinement;

/// <summary>
/// Interface for Delaunay mesh refinement algorithms.
/// </summary>
public interface IDelaunayRefiner
{
    /// <summary>
    /// Perform a single refinement step.
    /// </summary>
    /// <returns>The inserted vertex, or null if no refinement needed.</returns>
    IVertex? RefineOnce();

    /// <summary>
    /// Perform repeated refinements until quality criteria are met.
    /// </summary>
    /// <returns>True if converged successfully, false if iteration limit reached.</returns>
    bool Refine();
}
```

5. **Create `RuppertOptions.cs`**:
```csharp
namespace Tinfour.Core.Refinement;

/// <summary>
/// Configuration options for Ruppert's refinement algorithm.
/// </summary>
public class RuppertOptions
{
    /// <summary>
    /// Minimum angle threshold in degrees. Default 20°, max ~33.8°.
    /// </summary>
    public double MinimumAngleDegrees { get; set; } = 20.0;

    /// <summary>
    /// Minimum triangle area below which triangles are skipped.
    /// Prevents refinement of degenerate triangles.
    /// </summary>
    public double MinimumTriangleArea { get; set; } = 1e-3;

    /// <summary>
    /// Whether to enforce the sqrt(2) guard for the circumradius-to-edge ratio.
    /// </summary>
    public bool EnforceSqrt2Guard { get; set; } = true;

    /// <summary>
    /// Whether to skip seditious triangles (prevents cascading at acute corners).
    /// </summary>
    public bool SkipSeditiousTriangles { get; set; } = true;

    /// <summary>
    /// Whether to ignore seditious encroachments.
    /// </summary>
    public bool IgnoreSeditiousEncroachments { get; set; } = true;

    /// <summary>
    /// Whether to interpolate Z values for new vertices.
    /// </summary>
    public bool InterpolateZ { get; set; } = true;
}
```

### Phase 3: Core Implementation

6. **Create `RuppertRefiner.cs`** with structure:

```csharp
namespace Tinfour.Core.Refinement;

public class RuppertRefiner : IDelaunayRefiner
{
    #region Constants
    private const double DefaultMinTriangleArea = 1e-3;
    private const double NearVertexRelTol = 1e-9;
    private const double NearEdgeRelTol = 1e-9;
    private const double ShellBase = 2.0;
    private const double ShellEps = 1e-9;
    private const double SmallCornerDeg = 60.0;
    private static readonly double Sqrt2 = Math.Sqrt(2.0);
    #endregion

    #region Inner Types
    private enum VType { Input, Midpoint, Offcenter, Circumcenter }

    private sealed class VData
    {
        public VType Type;
        public IVertex? Corner;
        public int Shell;
        public VData(VType t, IVertex? c, int s) { Type = t; Corner = c; Shell = s; }
    }

    private sealed class CornerInfo
    {
        public double MinAngleDeg = 180.0;
    }

    private readonly record struct BadTri(IQuadEdge RepEdge, double Priority);
    #endregion

    #region Fields
    private readonly IIncrementalTin _tin;
    private readonly IIncrementalTinNavigator _navigator;

    private readonly double _minAngleRad;
    private readonly double _beta;
    private readonly double _rhoTarget;
    private readonly double _rhoMin;
    private readonly double _minTriangleArea;

    private readonly bool _skipSeditiousTriangles;
    private readonly bool _ignoreSeditiousEncroachments;
    private readonly bool _interpolateZ;

    // Interpolator for Z values
    private readonly IInterpolatorOverTin? _interpolator;

    private IVertex? _lastInsertedVertex;
    private readonly Dictionary<IVertex, VData> _vdata;
    private Dictionary<IVertex, CornerInfo> _cornerInfo;
    private int _vertexIndexer;

    private readonly HashSet<IQuadEdge> _constrainedSegments;
    private bool _constrainedSegmentsInitialized;

    // Priority queue: negate priority for max-heap behavior in C#'s min-heap
    private readonly PriorityQueue<BadTri, double> _badTriangles;
    private bool _badTrianglesInitialized;

    private readonly Queue<IQuadEdge> _encroachedSegmentQueue;
    private readonly HashSet<IQuadEdge> _inEncroachmentQueue;
    #endregion

    #region Constructors
    public RuppertRefiner(IIncrementalTin tin, RuppertOptions? options = null)
    {
        // ... implementation
    }

    public static RuppertRefiner FromEdgeRatio(IIncrementalTin tin, double ratio)
    {
        // ... implementation
    }
    #endregion

    #region Public Methods
    public bool Refine() { /* ... */ }
    public IVertex? RefineOnce() { /* ... */ }
    #endregion

    #region Initialization
    private void InitConstrainedSegments() { /* ... */ }
    private void InitBadTriangleQueue() { /* ... */ }
    private Dictionary<IVertex, CornerInfo> BuildCornerInfo() { /* ... */ }
    #endregion

    #region Quality Assessment
    private double TriangleBadPriority(SimpleTriangle t) { /* ... */ }
    #endregion

    #region Encroachment Detection
    private static IVertex? ClosestEncroacherOrNull(IQuadEdge edge) { /* ... */ }
    private IQuadEdge? FindEncroachedSegment() { /* ... */ }
    private void AddEncroachmentCandidate(IQuadEdge e) { /* ... */ }
    private bool IsEncroachedByPoint(IQuadEdge seg, IVertex p) { /* ... */ }
    private IQuadEdge? FirstEncroachedByPoint(IVertex p) { /* ... */ }
    #endregion

    #region Insertion Methods
    private IVertex? InsertOffcenterOrSplit(SimpleTriangle tri) { /* ... */ }
    private IVertex? InsertCircumcenterOrSplit(SimpleTriangle tri) { /* ... */ }
    private IVertex? SplitSegmentSmart(IQuadEdge seg) { /* ... */ }
    #endregion

    #region Local Updates
    private void AddVertex(IVertex v, VType type, IVertex? corner, int shell) { /* ... */ }
    private void UpdateBadTrianglesAroundVertex(IVertex v, SimpleTriangle? s) { /* ... */ }
    private void UpdateConstrainedSegmentsAroundVertex(IVertex v) { /* ... */ }
    private SimpleTriangle? NextBadTriangleFromQueue() { /* ... */ }
    #endregion

    #region Seditious Edge Handling
    private int ShellIndex(IVertex z, double x, double y) { /* ... */ }
    private bool IsCornerCritical(IVertex z) { /* ... */ }
    private bool IsSeditious(IVertex u, IVertex v) { /* ... */ }
    private bool ShouldIgnoreEncroachment(IQuadEdge e, IVertex witness) { /* ... */ }
    private bool SameShell(IVertex z, IVertex a, IVertex b) { /* ... */ }
    #endregion

    #region Helper Methods
    private IVertex? NearestNeighbor(double x, double y) { /* ... */ }
    private IQuadEdge? FirstNearConstrainedEdgeInterior(IVertex v, double tol) { /* ... */ }
    private bool IsNearEdgeInterior(IQuadEdge seg, IVertex px, double tol) { /* ... */ }
    private bool CheckEdge(IQuadEdge e, IVertex p) { /* ... */ }
    private static double AngleSmallBetweenDeg(double a, double b) { /* ... */ }
    #endregion
}
```

### Phase 4: Testing

7. **Create test file** `Tinfour.Core.Tests/Refinement/RuppertRefinerTests.cs`:

```csharp
namespace Tinfour.Core.Tests.Refinement;

public class RuppertRefinerTests
{
    [Fact]
    public void Refine_SimpleSquare_ProducesQualityMesh() { /* ... */ }

    [Fact]
    public void Refine_WithConstraints_PreservesConstraints() { /* ... */ }

    [Fact]
    public void Refine_SkinnyTriangle_EliminatesSmallAngles() { /* ... */ }

    [Fact]
    public void Refine_AcuteCorner_HandlesSeditiousEdges() { /* ... */ }

    [Fact]
    public void RefineOnce_ReturnsInsertedVertex() { /* ... */ }

    [Fact]
    public void RefineOnce_NoWorkNeeded_ReturnsNull() { /* ... */ }

    [Fact]
    public void FromEdgeRatio_ConvertsCorrectly() { /* ... */ }
}
```

### Phase 5: Integration

8. **Integration with Z-strategy** (optional, per research doc):
   - Create `IRefinementZStrategy` interface
   - Implement `LinearZStrategy`, `NaturalNeighborZStrategy`, etc.
   - Modify `RuppertRefiner` to accept strategy

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Missing TIN methods (SplitEdge, etc.) | High | High | Check codebase first; implement if needed |
| Reference equality issues | Medium | Medium | Use `ReferenceEqualityComparer` |
| Priority queue ordering | Low | Medium | Negate priority for max-heap behavior |
| Geometric precision issues | Medium | High | Use existing `GeometricOperations` class |
| Performance regression | Low | Medium | Benchmark against Java implementation |
| Edge cases in seditious handling | Medium | Medium | Port tests from Java; add regression tests |

---

## Estimated Effort

| Phase | Effort | Dependencies |
|-------|--------|--------------|
| Phase 1: Infrastructure | 2-4 hours | Check/add missing methods |
| Phase 2: Interface and Options | 1 hour | None |
| Phase 3: Core Implementation | 8-12 hours | Phases 1-2 |
| Phase 4: Testing | 4-6 hours | Phase 3 |
| Phase 5: Integration (Z-strategy) | 4-6 hours | Phase 3 |

**Total: 19-29 hours** (plus time for any missing infrastructure)

---

## Testing Strategy

### Unit Tests

1. **Basic refinement**: Square, circle, L-shape regions
2. **Constraint preservation**: Verify constraints remain intact
3. **Quality metrics**: Check minimum angles meet threshold
4. **Seditious handling**: Acute corner test cases
5. **Edge cases**: Empty TIN, single triangle, collinear points

### Integration Tests

1. **Large datasets**: 10k, 100k, 1M vertices
2. **Complex constraints**: Multiple overlapping regions
3. **Real bathymetric data**: If available

### Benchmarks

1. **Comparison with Java**: Same inputs, measure time/memory
2. **Scalability**: O(n log n) verification

---

## Appendix: Key Algorithm Formulas

### Off-Center Calculation

```
Given bad triangle with vertices A, B, C:

1. Find shortest edge (p, q):
   - Compute: |AB|², |BC|², |CA|²
   - Select pair with minimum length

2. Compute midpoint:
   mx = (px + qx) / 2
   my = (py + qy) / 2

3. Compute normal (perpendicular to edge, into triangle):
   nx = -(qy - py)
   ny = qx - px
   Normalize: n = n / |n|

4. Compute circumcenter distance:
   cc = triangle.GetCircumcircle()
   dCirc = sqrt((cc.X - mx)² + (cc.Y - my)²)

5. Compute off-center distance:
   d = min(dCirc, β × |pq|)
   where β = 1 / (2 × sin(minAngle))

6. Off-center point:
   ox = mx + nx × d
   oy = my + ny × d
```

### Triangle Quality Test

```
Given triangle with vertices A, B, C:

1. Compute edge vectors:
   AB = B - A
   AC = C - A

2. Compute edge lengths squared:
   la = |AB|² = AB.x² + AB.y²
   lc = |AC|² = AC.x² + AC.y²
   dot = AB · AC
   lb = la + lc - 2×dot  (law of cosines)

3. Compute cross product squared:
   cross = AB.x × AC.y - AB.y × AC.x
   cross² = cross × cross

4. Find shortest edge and pair product:
   if la ≤ lb and la ≤ lc: pairProd = lb × lc
   else if lb ≤ la and lb ≤ lc: pairProd = la × lc
   else: pairProd = la × lb

5. Quality test:
   threshold = 4 × ρ²min × cross²
   bad = (pairProd ≥ threshold) AND (cross² > 4 × minArea²)

6. Priority:
   return cross² if bad, else 0
```

### Shell Index Calculation

```
Given corner vertex z and point (x, y):

d = sqrt((x - z.x)² + (y - z.y)²)

if d ≤ ε:
    return 0
else:
    return round(log(d) / log(2))
```

### Encroachment Test (Gabriel Circle)

```
Given segment AB and candidate point P:

1. Compute midpoint:
   mx = (A.x + B.x) / 2
   my = (A.y + B.y) / 2

2. Compute radius squared:
   r² = |AB|² / 4

3. Compute distance squared:
   d² = (P.x - mx)² + (P.y - my)²

4. Encroachment test:
   encroached = (d² < r²)
```

---

## Related Documents

- [NON_RASTER_PIPELINE_RESEARCH.md](NON_RASTER_PIPELINE_RESEARCH.md) - Section 13: Ruppert's Algorithm research
- [CONSTRAINT_LIMIT_INCREASE_PLAN.md](CONSTRAINT_LIMIT_INCREASE_PLAN.md) - Constraint system documentation

---

**Document Version:** 1.0
**Last Updated:** December 16, 2025
