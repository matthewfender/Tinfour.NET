# Delaunay Triangulation

**Component:** Core Triangulation Engine  
**Primary Class:** `IncrementalTin`  
**Algorithm:** Bowyer-Watson Incremental Construction

## Overview

Tinfour.NET implements **Delaunay triangulation** using an incremental construction algorithm. Vertices are added one at a time, with the triangulation maintaining the Delaunay criterion at each step. The implementation supports both interior insertion and hull extension.

## Delaunay Criterion

A triangulation is **Delaunay-optimal** when no vertex lies inside the circumcircle of any triangle. This property:
- **Maximizes minimum angles** (avoids sliver triangles)
- **Produces well-shaped triangles** for interpolation
- **Is unique** (given a point set in general position)
- **Enables dual Voronoi diagram** construction

## Algorithm: Bowyer-Watson

### Incremental Insertion

**For each vertex v:**

1. **Locate** - Find triangle containing v (using Lawson's Walk)
2. **Insert** - Add v and create edges to triangle vertices
3. **Restore** - Flip edges violating Delaunay criterion
4. **Update** - Maintain hull and ghost edges if v extends boundary

### Interior Insertion

When a vertex falls inside the existing triangulation:

```
Before:           After:
   A                A
  /|\              /|\
 / | \            / | \
B--+--C   →    B--v--C
 \ | /            \|/
  \|/              D
   D
```

**Process:**
1. Split enclosing triangle into 3 new triangles
2. Test each new edge with `InCircle` predicate
3. Flip edges where opposite vertex violates criterion
4. Continue flipping until local Delaunay property restored

### Hull Extension

When a vertex falls outside the convex hull:

```
Before:      After:
  A----B       A----B
  |   /        |\  /|
  |  /         | \/ |
  | /          | /\ |
  |/           |/  \|
  C            C----v
```

**Process:**
1. Identify visible hull edges from v
2. Connect v to visible edge endpoints
3. Update ghost edges for new perimeter
4. Maintain ghost triangles for infinite regions

[See: Bootstrap and Point Location](./bootstrap-and-walk.md)

## Ghost Vertices and Edges

**Ghost vertices** represent points at infinity, enabling uniform treatment of boundary triangles.

### Ghost Triangle Structure

Every hull edge has a companion **ghost triangle**:

```
Real triangulation:    With ghosts:
                       
  A----B----C           G0
  |   /|   /|          /| \
  |  / |  / |         / |  \
  | /  | /  |        /  |   \
  |/   |/   |       A---B---C
  D----E----F        \  |  /
                      \ | /
                       \|/
                       G1
```

**NullVertex representation:**
- Uses `Vertex._NullVertex` sentinel value
- Avoids boxing overhead of `Nullable<Vertex>`
- Efficiently tested with `IsNullVertex()` extension

**Purpose:**
- Uniform edge navigation (no boundary special cases)
- Simplifies orientation tests
- Enables Voronoi diagram to infinity

[See: Vertex Structure](../data-structures/vertex.md)

## Edge Flipping

**Edge flip** operation maintains Delaunay criterion:

```
Before flip:          After flip:
    A                     A
   /|\                   / \
  / | \                 /   \
 /  |  \               /     \
B   |   C      →      B-------C
 \  |  /               \     /
  \ | /                 \   /
   \|/                   \ /
    D                     D
```

**When to flip:**
- After inserting vertex into triangle
- After constraint insertion (restoring conformity)
- When opposite vertex fails in-circle test

**Test:**
```csharp
double test = _geoOp.InCircle(a, b, c, d);
if (test > 0) {
    // d is inside circumcircle of abc, flip required
    FlipEdge(edge);
}
```

[See: Geometric Operations](./geometric-operations.md)

## Constrained Delaunay Triangulation (CDT)

CDT extends Delaunay triangulation by **enforcing specific edges** that might otherwise violate the Delaunay criterion.

### Constraint Types

**Linear Constraints:**
- Force specific edges between vertex pairs
- Used for boundaries, roads, rivers
- Represented by `LinearConstraint` class

**Polygon Constraints:**
- Define regions with boundaries
- Support holes (clockwise orientation)
- Interior/exterior region classification
- Represented by `PolygonConstraint` class

### Constraint Insertion Algorithm

**Process (Sloan's Algorithm):**

1. **Pinwheel Search** - Check if constraint edge already exists
2. **Tunneling** - Remove edges intersecting constraint
3. **Edge Insertion** - Add constraint edge
4. **Cavity Filling** - Triangulate left/right cavities
5. **Region Marking** - Label interior/exterior regions (polygons)

**Key challenges:**
- Handling coincident vertices (VertexMergerGroup)
- Maintaining topology during edge removal
- Ensuring cavity triangulation quality

[See: Constraint Processing](./constraint-processing.md)

## Data Structures

### Edge-Based Representation

Triangulation uses **QuadEdge** structure:
- Each edge knows its forward and reverse neighbors
- Dual edge represents opposite orientation
- Enables efficient navigation and topology queries

```
Edge relationships:
    Forward
  B -----> C
  ^       /
  |      /
  |     /
  |    / Dual
  |   v
  A
```

[See: QuadEdge](../data-structures/quad-edge.md)

### Edge Pool

Edges allocated from **EdgePool**:
- Paged allocation (EdgePage)
- Even/odd index pairing for dual edges
- Reuse of deallocated edges
- Reduces GC pressure

[See: Edge Pool](../data-structures/edge-pool.md)

## Bootstrap Process

Initial triangle construction from vertices:

1. **Find initial vertices** - Well-separated, non-collinear
2. **Determine orientation** - Ensure counter-clockwise
3. **Create initial triangle** - 3 real edges + 3 ghost edges
4. **Set search edge** - Reference for subsequent insertions

[See: Bootstrap and Point Location](./bootstrap-and-walk.md)

## Performance Characteristics

### Time Complexity

- **Average case:** O(n log n) for n vertices
- **Worst case:** O(n²) for adversarial input
- **With Hilbert sorting:** Near-optimal O(n log n)

### Space Complexity

- **Vertices:** O(n)
- **Edges:** O(n) - approximately 3n edges
- **Triangles:** O(n) - approximately 2n triangles

### Optimization Strategies

**Pre-sorting:**
```csharp
tin.AddSorted(vertices); // Hilbert curve ordering
```
- Improves spatial locality
- Reduces edge flipping
- ~20-30% performance improvement

**Pre-allocation:**
```csharp
tin.PreAllocateForVertices(count); // Heuristic: 3.2 edges per vertex
```
- Reduces allocation overhead
- Minimizes EdgePool page creation
- Stabilizes memory footprint

## Quality Metrics

### Triangle Quality

**Delaunay triangulations maximize:**
- Minimum angle across all triangles
- Triangle circumradius-to-shortest-edge ratio

**Avoid:**
- Sliver triangles (nearly collinear vertices)
- Needle triangles (extreme aspect ratios)

### Validation

```csharp
var count = tin.CountTriangles();
// count.InteriorRegionCount - real triangles
// count.GhostCount - boundary ghost triangles
// count.TotalCount - all triangles
```

## Mathematical Foundations

### In-Circle Test

Determines if point d lies inside circumcircle of triangle abc:

```
|ax-dx  ay-dy  (ax-dx)² + (ay-dy)²|
|bx-dx  by-dy  (bx-dx)² + (by-dy)²| > 0  ⟹  d is inside
|cx-dx  cy-dy  (cx-dx)² + (cy-dy)²|
```

### Orientation Test

Determines if points are counter-clockwise:

```
|ax  ay  1|
|bx  by  1| > 0  ⟹  counter-clockwise
|cx  cy  1|
```

[See: Geometric Operations](./geometric-operations.md)

## Limitations and Considerations

### Precision Issues

- **Floating-point arithmetic** may cause inconsistencies
- **Thresholds** used for robustness (see [Thresholds](../utilities/thresholds.md))
- **Extended precision** fallback for degenerate cases

### Degenerate Cases

- **Collinear points** - May not bootstrap
- **Duplicate vertices** - Handled by VertexMergerGroup
- **Four cocircular points** - Arbitrary tie-breaking

### Constraints

- **Intersecting constraints** - Not supported
- **Self-intersecting polygons** - Results undefined
- **Constraint density** - May degrade from Delaunay optimality

## Usage Examples

### Basic Triangulation

```csharp
var tin = new IncrementalTin(nominalSpacing: 1.0);
tin.Add(vertices);
bool success = tin.IsBootstrapped();
```

### Optimized Construction

```csharp
var tin = new IncrementalTin(nominalSpacing: 1.0);
tin.PreAllocateForVertices(vertices.Count);
tin.AddSorted(vertices); // Hilbert ordered
```

### With Constraints

```csharp
var constraint = new LinearConstraint();
constraint.Add(v1, false);
constraint.Add(v2, false);
constraint.Complete();

tin.AddConstraints(new[] { constraint }, restoreConformity: true);
```

## References

### Papers

- Bowyer, A. (1981). "Computing Dirichlet tessellations"
- Watson, D.F. (1981). "Computing the n-dimensional Delaunay tessellation"
- Sloan, S.W. (1993). "A Fast Algorithm for Generating Constrained Delaunay Triangulations"

### Resources

- [Original Tinfour Java Documentation](http://www.tinfour.org)
- [Wikipedia: Delaunay Triangulation](https://en.wikipedia.org/wiki/Delaunay_triangulation)

## Related Documentation

- [Incremental TIN Implementation](./incremental-tin.md)
- [Constraint Processing](./constraint-processing.md)
- [Bootstrap and Point Location](./bootstrap-and-walk.md)
- [Geometric Operations](./geometric-operations.md)
- [QuadEdge Structure](../data-structures/quad-edge.md)

---

**Last Updated:** November 26, 2025
