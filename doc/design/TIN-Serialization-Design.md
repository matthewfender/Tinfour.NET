# TIN Serialization Design for Tinfour.NET

**Date**: 2025-12-19
**Status**: Implemented
**Purpose**: Enable saving and loading of complete TIN structures, including refined meshes

---

## Problem Statement

Currently, Tinfour.NET provides no mechanism for persisting a TIN (Triangulated Irregular Network) to disk. TINs are ephemeral - built in memory, used for interpolation/contouring, then disposed.

This becomes a significant limitation when:
1. **Ruppert refinement** has been applied - refinement is computationally expensive and the resulting mesh cannot be recreated by simply re-triangulating the same vertices
2. **Caching/checkpointing** is desired for faster map regeneration
3. **Export/sharing** of TIN data is needed

### Why Simple Vertex Serialization Won't Work

The TIN is stored as a **quad-edge data structure**. Simply storing vertices and re-triangulating would NOT reproduce the same mesh because:

- **Steiner points** added during Ruppert's refinement depend on the iterative state of the algorithm
- **Edge flips** during Delaunay restoration depend on insertion order and numerical precision
- **Constraint edges** have specific topological relationships that must be preserved

To perfectly recreate a refined TIN, we must serialize the **complete edge structure**.

---

## Tinfour.NET Data Structures

### Vertex / IVertex (implementation reality)

The triangulation stores vertices as `IVertex` references.

- `Vertex` is currently a **sealed class** (not a struct) and is the common case.
- A canonical ghost vertex sentinel exists: `Vertex.Null` (index `-1`).
- Some workflows can introduce `VertexMergerGroup : IVertex` (coincident-vertex resolution).

For serialization purposes, the important persisted fields for a `Vertex` instance are:

- `X` (double)
- `Y` (double)
- stored Z (float; API exposes `double GetZ()` but storage is a float)
- index (int)
- status (byte; includes `BitSynthetic`, `BitConstraint`, `BitWithheld`)
- auxiliary (byte)

`Vertex.Null` should not be written as a normal vertex record; it is represented by a dedicated vertex-object kind.

### QuadEdge / QuadEdgePartner (implementation reality)

Edges are stored as paired objects:

- `QuadEdge` (the base/side-0 edge)
   - Stores: base index, A vertex, forward/reverse links, and a reference to its dual.
   - Delegates all constraint queries to its dual.
- `QuadEdgePartner` (the side-1 edge)
   - Stores: B vertex and forward/reverse links for side-1.
   - Uses its own `_index` field **purely as a packed constraint bitfield**, not as an identity.
   - Identity is computed as `GetIndex() == baseIndex + 1`.

Constraint packing (`QuadEdgeConstants`) encodes:

- sign bit indicates constrained
- flags for region border/interior, line-member, synthetic edge
- lower bits store region/polygon index (packed as `index+1`, where 0 means “unset”)
- upper bits store line constraint index (packed similarly)

Serialization must preserve the partner packed constraint bits exactly.

### EdgePool (identity and indexing)

Base edges use even indices; partner edges are always `baseIndex + 1`. Many algorithms build arrays keyed by edge index, so stable restoration of indices (or a stable remap strategy) is required.

#### Page-Based Allocation (verified from `EdgePool.cs`)

The EdgePool uses a page-based memory management system:

```csharp
private const int EdgePoolPageSize = 1024;  // Edges per page
private int _pageSize2 = 2048;              // Edge indices per page (2x for partners)
private EdgePage[] _pages;                   // Array of pages
```

**Index Mapping Formula:**
```
Page Index = baseEdgeIndex / _pageSize2
Position In Page = (baseEdgeIndex % _pageSize2) / 2
```

**Key Fields for Serialization:**
- `_nAllocated` - Currently allocated edge count
- `_linearConstraintMap` - Maps edge indices to linear constraints (must be rebuilt)

**Important:** During normal operation, `DeallocateEdge` compacts edges by swapping the last allocated edge into the freed slot. However, for serialization we capture a stable snapshot where edge indices are sequential within pages.

### Constraint Bitfield Layout (verified from `QuadEdgeConstants.cs`)

The `QuadEdgePartner._index` field is NOT an identity—it's a packed constraint bitfield:

```
Bit 31 (sign)  : ConstraintEdgeFlag        - Edge is constrained (checked via _index < 0)
Bit 30         : ConstraintRegionBorderFlag - Edge is polygon boundary
Bit 29         : ConstraintRegionInteriorFlag - Edge inside constraint region
Bit 28         : ConstraintLineMemberFlag   - Edge is linear constraint member
Bit 27         : SyntheticEdgeFlag         - Edge is synthetic
Bit 26         : EdgeFlagReservedBit       - Reserved for future use
Bits 15-26     : Line constraint index     - 12 bits (0-4094 max)
Bits 0-14      : Region constraint index   - 15 bits (0-32766 max)
```

**Index Storage Convention:**
- Indices are stored as `value + 1` (0 means "unset")
- Extract lower: `(index & 0x7FFF) - 1` or -1 if zero
- Extract upper: `((index & 0x07FF8000) >> 15) - 1` or -1 if zero

### Constraints
- **PolygonConstraint**: Closed polygon with `_definesRegion` and `_isHole` flags, `_linkingEdge`, `_defaultZ`
- **LinearConstraint**: Open polyline with `_linkingEdge`, `_defaultZ`

Both store:
- `_constraintIndex` - Position in TIN's constraint list (0-based)
- `_linkingEdge` - Reference edge for traversal (must be restored)
- `_managingTin` - Reference to containing TIN (set during restoration)
- `_applicationData` - Optional user data (out of scope for v1)

### IncrementalTin Internal State (verified from `IncrementalTin.cs`)

Fields that must be persisted for complete state restoration:

```csharp
// Geometry bounds
private double _boundsMinX = double.PositiveInfinity;
private double _boundsMaxX = double.NegativeInfinity;
private double _boundsMinY = double.PositiveInfinity;
private double _boundsMaxY = double.NegativeInfinity;

// Edge management
private readonly EdgePool _edgePool;          // Contains all QuadEdge objects
private IQuadEdge? _searchEdge;               // Reference edge for point location

// Constraint state
private readonly List<IConstraint> _constraintList = new();  // All constraints in order
private int _maxLengthOfQueueInFloodFill;     // >0 indicates flood fill performed

// Synthetic vertex tracking
private int _nSyntheticVertices;              // Counter for Steiner points

// Status flags
private bool _isLocked;                       // TIN is locked (no modifications)
private bool _lockedDueToConstraints;         // Locked because constraints added
private bool _isConformant = true;            // TIN conforms to Delaunay criterion

// Configuration (needed to reconstruct thresholds)
private readonly double _nominalPointSpacing; // For threshold reconstruction
```

**Fields NOT persisted (derived/reconstructed):**
- `_thresholds` - Reconstructed from `_nominalPointSpacing`
- `_geoOp` - Reconstructed from `_thresholds`
- `_bootstrapUtility` - Reconstructed from `_thresholds`
- `_walker` - Reconstructed from `_thresholds`
- `_vertexList` - Only used pre-bootstrap, null after
- `_vertexCount` - Can be recomputed from edge traversal
- `_isBootstrapped` - Implied by having edges (will be true after deserialization)
- `_isDisposed` - Always false for a newly loaded TIN

---

## Proposed Binary Format

### File Structure

```
┌─────────────────────────────────────┐
│ Header                              │
├─────────────────────────────────────┤
│ Vertex Object Table                 │
├─────────────────────────────────────┤
│ Base-Edge Table                     │
├─────────────────────────────────────┤
│ Constraint Table                    │
├─────────────────────────────────────┤
│ TIN State (small fixed record)      │
├─────────────────────────────────────┤
│ Metadata (optional)                 │
└─────────────────────────────────────┘
```

All integers are little-endian.

### Header (32 bytes)
```
Offset  Size  Description
0       4     Magic number: 0x54494E53 ("TINS")
4       2     Format version (currently 1)
6       2     Flags (bit 0 = payload is GZip-compressed)
8       4     Vertex object count
12      4     Base-edge count
16      4     Constraint count
20      12    Reserved (0)
```

Note: Total directed edges in memory is `baseEdgeCount * 2`.

### Vertex Object Table

Because edges store `IVertex` (not always `Vertex`), this table is an **object table**.

Each entry begins with a 1-byte kind tag:

- `0 = NullVertex` (represents `Vertex.Null`, no payload)
- `1 = Vertex`
- `2 = VertexMergerGroup`

#### Kind 1: Vertex

Fixed payload (30 bytes):

```
Offset  Size  Description
0       1     Kind (1)
1       8     X (double)
9       8     Y (double)
17      4     ZStored (float)
21      4     Index (int)
25      1     Status (byte)
26      1     Auxiliary (byte)
27      3     Reserved (0)
```

#### Kind 2: VertexMergerGroup

Payload (variable):

```
0       1     Kind (2)
1       8     X (double)
9       8     Y (double)
17      4     Index (int)
21      1     Flags (bit0 = synthetic, bit1 = constraintMember)
22      1     ResolutionRule (0=min,1=mean,2=max)
23      2     Reserved (0)
25      4     MemberVertexCount (int)
29      N*4   MemberVertexObjectIds (int[])   // ids referencing Kind=1 Vertex entries
```

Rationale: Some code paths use `IVertex.Contains(Vertex)` and expect merger groups to preserve membership semantics.

### Base-Edge Table

We store one record per base (side-0) edge, but the record includes topology for **both** sides.

Each base-edge entry (36 bytes):

```
Offset  Size  Description
0       4     BaseIndex (int)                    // even: 0, 2, 4, ...
4       4     AVertexObjectId (int)              // -1 for Vertex.Null
8       4     BVertexObjectId (int)              // -1 for Vertex.Null
12      4     F0EdgeIndex (int)                  // forward link on side-0 (full edge index)
16      4     R0EdgeIndex (int)                  // reverse link on side-0 (full edge index)
20      4     F1EdgeIndex (int)                  // forward link on side-1 (full edge index)
24      4     R1EdgeIndex (int)                  // reverse link on side-1 (full edge index)
28      4     PartnerPackedConstraintBits (int)  // exact QuadEdgePartner._index value
32      4     Reserved (0)
```

**Important: Edge Index vs Base Index**

Forward/reverse links store the **full edge index** (not base index), because they can point to either side of an edge pair:
- Even indices (0, 2, 4, ...) reference base edges (side-0)
- Odd indices (1, 3, 5, ...) reference partner edges (side-1)

To resolve a link:
```csharp
int linkedEdgeIndex = F0EdgeIndex;
int linkedBaseIndex = linkedEdgeIndex & ~1;  // Clear low bit to get base
int linkedSide = linkedEdgeIndex & 1;        // 0 = base, 1 = partner
QuadEdge linkedBase = edgesByBaseIndex[linkedBaseIndex];
QuadEdge linkedEdge = (linkedSide == 0) ? linkedBase : linkedBase._dual;
```

**Vertex Object ID Convention:**
- `-1` represents `Vertex.Null` (the canonical ghost vertex singleton)
- `0..N-1` index into the vertex object table

**PartnerPackedConstraintBits** is the exact value of `QuadEdgePartner._index`, preserving all constraint flags and indices.

### Constraint Table

Constraints are serialized **by value** and in the same order as the TIN’s internal constraint list so that constraint indices remain stable.

Each constraint entry:

```
Offset  Size  Description
0       1     Type (0 = Polygon, 1 = Linear)
1       1     Flags (bit0 = definesRegion, bit1 = isHole)
2       2     Reserved (0)
4       4     ConstraintIndex (int)              // 0-based
8       4     VertexObjectCount (int)
12      N*4   VertexObjectIds (int[])
..      4     LinkingEdgeBaseIndex (int)         // -1 if not set
```

Notes:

- The constraint’s linking edge is required for region traversal utilities and some collectors.
- Constraint application-data is intentionally out of scope for v1 unless a concrete use emerges.

### TIN State (fixed record)

The file format includes a small, fixed record for restoring the parts of `IncrementalTin` state that affect downstream behavior.

Proposed record (64 bytes):

```
Offset  Size  Description
0       8     XMin (double)
8       8     XMax (double)
16      8     YMin (double)
24      8     YMax (double)
32      8     NominalPointSpacing (double)     // Required for threshold reconstruction
40      4     NSyntheticVertices (int)
44      4     SearchEdgeBaseIndex (int)        // -1 if not set
48      4     MaxLengthOfQueueInFloodFill (int)
52      1     Flags (bit0=isLocked, bit1=lockedDueToConstraints, bit2=isConformant)
53      11    Reserved (0)
```

Notes:

- `NominalPointSpacing` is critical - it's used to reconstruct `Thresholds`, `GeometricOperations`, `BootstrapUtility`, and `StochasticLawsonsWalk`.
- If `SearchEdgeBaseIndex` is missing/invalid, the reader may select any non-ghost edge as a search seed.
- If exact base indices cannot be restored for some reason, the design must switch to an explicit remap table and update all index-keyed structures accordingly (not recommended for v1).

---

## Size Estimates

For a TIN with N vertices:
- Approximately 3N base edges (directed-edge pairs)
- Vertex object data: ≈ N × ~30 bytes (plus 1 byte kind tag; merger groups add variable membership)
- Base-edge data: ≈ 3N × 36 bytes ≈ 108N bytes
- Total: ~138N bytes (before compression, constraints/metadata excluded)

| Vertices | Edges | Uncompressed Size | Est. Compressed |
|----------|-------|-------------------|-----------------|
| 10,000   | 30,000 | ~1.1 MB | ~400 KB |
| 100,000  | 300,000 | ~11 MB | ~4 MB |
| 1,000,000 | 3,000,000 | ~110 MB | ~40 MB |

Compression (gzip/deflate) should achieve 60-70% reduction due to repetitive index patterns.

---

## Implementation Approach

### Option A: Add Serialization Methods to Tinfour.Core (Recommended)

Add a new `Tinfour.Core/Serialization` namespace with:

```csharp
namespace Tinfour.Core.Serialization;

public static class TinSerializer
{
    /// <summary>
    /// Writes a complete TIN to a binary stream.
    /// </summary>
    public static void Write(IIncrementalTin tin, Stream stream, bool compress = true);

    /// <summary>
    /// Reads a TIN from a binary stream.
    /// </summary>
    public static IIncrementalTin Read(Stream stream);

    /// <summary>
    /// Writes a TIN to a file.
    /// </summary>
    public static void WriteToFile(IIncrementalTin tin, string path, bool compress = true);

    /// <summary>
    /// Reads a TIN from a file.
    /// </summary>
    public static IIncrementalTin ReadFromFile(string path);
}
```

### Required Internal Access

To properly serialize and deserialize, the serializer needs access to:

1. **EdgePool** - to iterate edges and recreate the pool
2. **QuadEdge internal fields** - `_f`, `_r`, `_dual`, `_v`, and `QuadEdgePartner._index`
3. **IncrementalTin internal state** - bounds, thresholds, search edge, constraint list

**Recommended Approach: Internal Factory Methods**

Add internal methods to support serialization without exposing internals publicly:

```csharp
// In IncrementalTin.cs
internal static class TinSerializationSupport
{
    /// <summary>
    /// Creates a new IncrementalTin with pre-allocated edge pool for deserialization.
    /// </summary>
    internal static IncrementalTin CreateForDeserialization(
        double nominalPointSpacing,
        int expectedEdgeCount);

    /// <summary>
    /// Gets internal state for serialization.
    /// </summary>
    internal static TinSerializationState GetSerializationState(IncrementalTin tin);

    /// <summary>
    /// Restores internal state after edge reconstruction.
    /// </summary>
    internal static void RestoreState(IncrementalTin tin, TinSerializationState state);
}

// In EdgePool.cs
internal class EdgePoolSerializationSupport
{
    /// <summary>
    /// Pre-allocates edges with specific indices for deserialization.
    /// </summary>
    internal static void PreAllocateWithIndices(EdgePool pool, int[] baseIndices);

    /// <summary>
    /// Gets edge by base index for linking.
    /// </summary>
    internal static QuadEdge GetEdgeByBaseIndex(EdgePool pool, int baseIndex);
}

// In QuadEdge.cs
internal class QuadEdgeSerializationSupport
{
    /// <summary>
    /// Sets internal fields directly for deserialization.
    /// </summary>
    internal static void SetInternals(
        QuadEdge edge,
        IVertex a, IVertex b,
        QuadEdge forward, QuadEdge reverse,
        QuadEdge dualForward, QuadEdge dualReverse,
        int partnerConstraintBits);
}
```

This approach:
- Keeps internals `internal` (not `public`)
- Groups serialization concerns in dedicated support classes
- Allows the serializer to live in `Tinfour.Core.Serialization` namespace
- Uses `[assembly: InternalsVisibleTo("Tinfour.Core")]` if serialization is in separate assembly (not needed if same assembly)

### Reconstruction Algorithm

```
1. Read header; validate magic/version/flags
2. Read TIN state record first (need nominalPointSpacing early)
3. Create IncrementalTin with nominalPointSpacing
4. Read vertex object table into `IVertex[] vertexObjects`
   - Kind 0 yields `Vertex.Null` (canonical singleton)
   - Kind 1 yields a `Vertex` instance with stored float Z and status/auxiliary restored
   - Kind 2 yields a `VertexMergerGroup` and restores its member list
5. Pre-allocate EdgePool pages to accommodate maximum base index from file
6. Allocate all base edges sequentially (they will get indices 0, 2, 4, ...)
   - Since EdgePool allocates sequentially, indices will match if we allocate in order
7. First pass over base-edge table:
   - For base edge `e` at file position `i`:
     - Verify edge.GetBaseIndex() matches expected (i * 2)
     - Set base A vertex and partner B vertex from `vertexObjects[]`
     - Set partner packed constraint bits directly on `QuadEdgePartner._index`
     - Store forward/reverse base indices for both sides for later linking
8. Second pass over base-edge table:
   - Resolve links for both sides using base index -> edge lookup:
     - base side: `_f`/`_r` using base-edge references
     - partner side: `_f`/`_r` using partner-edge references (via `_dual`)
9. Read constraints table and reconstruct constraint objects (Polygon/Linear) in-order
   - Set constraint index explicitly via SetConstraintIndex()
   - Restore linking edge from LinkingEdgeBaseIndex
   - Add to TIN's constraint list
10. Rebuild EdgePool._linearConstraintMap by scanning edges:
    - For each edge where IsConstraintLineMember() is true:
      - Get line constraint index from GetConstraintLineIndex()
      - Look up constraint from constraint list
      - Call EdgePool.AddLinearConstraintToMap(edge, constraint)
11. Restore TIN state: bounds, flags, counters, _searchEdge
12. Mark TIN as bootstrapped (internal state)
```

**Critical Insight: Edge Index Preservation**

The EdgePool allocates edges sequentially within pages:
- Page 0: indices 0, 2, 4, ..., 2046
- Page 1: indices 2048, 2050, 2052, ..., 4094
- etc.

When serializing, edges are stored in allocation order, so their indices ARE sequential.
When deserializing, we allocate the same number of edges in the same order, so indices match automatically.

**No remapping needed** as long as:
1. We serialize edges in allocation order (not deallocated/freed edges)
2. We allocate the exact same number of edges during deserialization
3. We pre-allocate enough pages before starting

### Vertex Reconstruction Challenge

The `Vertex` class is **sealed** and fields are set via constructor or `With*` methods that return new instances. For deserialization, we need to construct vertices with all fields set correctly.

**Current Vertex constructors:**
```csharp
public Vertex(double x, double y, double z)
public Vertex(double x, double y, double z, int index)
// Private: Vertex(x, y, z, index, status, auxiliary, reserved0, reserved1)
```

**Options:**

1. **Add internal factory method** (Recommended):
```csharp
// In Vertex.cs
internal static Vertex CreateForDeserialization(
    double x, double y, float z, int index, byte status, byte auxiliary)
{
    return new Vertex(x, y, z, index, status, auxiliary, 0, 0);
}
```

2. **Use existing With* methods** (Less efficient, creates intermediate objects):
```csharp
var v = new Vertex(x, y, z, index)
    .WithStatus(status)
    .WithAuxiliaryIndex(auxiliary);
```

**VertexMergerGroup reconstruction:**
- Constructor takes first `Vertex` and initializes from it
- Call `AddVertex()` for each member vertex
- Call `SetResolutionRule()` to restore resolution rule
- The `_zRule` is automatically recomputed by `ApplyRule()`

---

## Test-Driven Validation Matrix

The non-negotiable requirement is **functional equivalence**: a deserialized tin must be usable for all downstream operations.

Minimum matrix (each test is “build → serialize → deserialize → run same downstream ops”):

1. **Empty / tiny tins**
   - 0 vertices, 1 vertex, 3 vertices (single triangle)
2. **Unconstrained random points**
   - deterministic seed; validate that interpolators and collectors run without exceptions
3. **Linear constraints**
   - validate line-member indices survive and the linear-constraint map is rebuilt
4. **Polygon constraints (region + hole)**
   - validate border/interior indices and region traversal utilities using linking edges
5. **Refined meshes (Ruppert)**
   - apply refinement; validate synthetic/Steiner behavior is preserved and downstream ops still work
6. **Downstream operations on deserialized tin**
   - smoothing respects constraint-member vertices
   - contour generation runs and produces stable results for the same inputs/settings

Suggested non-brittle assertions:

- base edge count and base-index range match
- for each constrained edge: partner packed constraint bits match pre/post
- constraint count/order and indices match
- constraint linking edges restore to expected non-null edges
- for `Vertex` objects: X/Y/ZStored/index/status/auxiliary match
- for `VertexMergerGroup` objects: resolution rule + membership semantics preserved

---

## API Design

### Writing

```csharp
using var tin = new IncrementalTin();
tin.Add(vertices);
tin.AddConstraints(constraints, restoreConformity: true);
// ... perform Ruppert refinement ...

// Save to file
TinSerializer.WriteToFile(tin, "mesh.tin");

// Or to stream
using var stream = File.Create("mesh.tin");
TinSerializer.Write(tin, stream, compress: true);
```

### Reading

```csharp
// Load from file
using var tin = TinSerializer.ReadFromFile("mesh.tin");

// Or from stream
using var stream = File.OpenRead("mesh.tin");
using var tin = TinSerializer.Read(stream);

// TIN is ready to use - no need to rebuild
var triangles = tin.GetTriangles();
```

---

## Alternative Formats

### GeoPackage (SQLite)
- Pro: OGC standard, spatial index support
- Con: More complex, overkill for internal caching

### Shapefile
- Pro: Widely supported
- Con: Cannot store edge topology

### Custom Text Format (for debugging)
```
TINFOUR_TEXT_V1
VERTICES 1000
0: 100.5 200.3 5.2 0
1: 101.2 200.8 5.5 0
...
EDGES 3000
0: 0 1 F:2 R:5
1: 1 0 F:6 R:3
...
CONSTRAINTS 2
POLYGON REGION 0: 0 1 2 3 4
LINEAR 1: 10 11 12
```

---

## Implementation Phases

### Phase 1: Round-trip topology (no constraints)
- [ ] Add `Tinfour.Core.Serialization` namespace
- [ ] Implement `TinSerializer.Write()` for vertex objects + base-edge table
- [ ] Implement `TinSerializer.Read()` with two-pass reconstruction
- [ ] Unit tests for round-trip and continued use of deserialized tin

### Phase 2: Constraints + linking edges + maps
- [ ] Serialize constraints (by value, preserving order)
- [ ] Preserve/restore partner packed constraint bits
- [ ] Restore constraint linking edges
- [ ] Rebuild the linear-constraint map

### Phase 3: Compression + perf
- [ ] Add optional GZip wrapper (flagged in header)
- [ ] Optimize allocations (array pooling for large reads)
- [ ] (Optional) async APIs

### Phase 4: Robustness
- [ ] Add checksum/integrity verification (optional)
- [ ] Validate topology after load (debug-only assertions)
- [ ] Performance benchmarks

---

## Considerations

### Thread Safety
- Serialization should lock the TIN during write
- Deserialization creates a new TIN instance

### Versioning
- Format version in header allows future extensions
- Backward compatibility: older readers reject newer versions
- Forward compatibility: reserve flag bits for future features

### Error Handling
- Validate magic number
- Check vertex-object/base-edge counts against file size
- Verify edge topology integrity after load

### Memory Efficiency
- Stream-based reading to avoid loading entire file
- Consider memory-mapped files for very large TINs

---

## Open Questions

### Resolved

1. ~~Should we support partial serialization (e.g., just vertices + triangles without full topology)?~~
   **Decision: No for v1.** Full topology is required for the primary use case (refined meshes). Partial serialization could be added later as a separate format.

2. ~~Should constraints be serialized by reference to original data or by value?~~
   **Decision: By value.** Constraints are serialized with their vertex references (as vertex object IDs) so the deserialized TIN is self-contained.

3. ~~Is compression worth the CPU overhead for typical use cases?~~
   **Decision: Yes, optional.** GZip compression is flagged in header. For disk storage/transfer, 60-70% size reduction is valuable. For in-memory caching, compression can be disabled.

### Still Open

4. Should we support incremental/append operations?
   **Tentative: No for v1.** This would require a more complex format with append markers. Not needed for primary use case.

5. Should `_applicationData` on constraints be serialized?
   **Tentative: No for v1.** Application data is arbitrary objects and would require custom serialization support. Can be added later if needed.

6. Should we add a checksum/integrity verification?
   **Tentative: Optional for v2.** CRC32 at end of file would catch corruption but adds complexity.

---

## Implementation Checklist

All items completed as of 2025-12-19.

### Prerequisites (Internal Access)

- [x] Add `Vertex.CreateForDeserialization()` internal factory method
- [x] Add `QuadEdge` internal methods to set `_f`, `_r`, and partner `_index`
- [x] Add `EdgePool` method to get edge by base index (or use existing iteration)
- [x] Add `IncrementalTin` internal constructor for deserialization (or factory method)
- [x] Add internal methods to set `IncrementalTin` state fields

### Phase 1: Core Serialization

- [x] Create `Tinfour.Core/Serialization/TinSerializer.cs`
- [x] Write header (magic number, version, flags)
- [x] Write TIN state record
- [x] Write vertex object table (Kind 1=Regular, 2=MergerGroup)
- [x] Write base-edge table
- [x] Read and validate header
- [x] Read TIN state record
- [x] Read vertex object table
- [x] Read and reconstruct edges with topology
- [x] Unit test: simple triangle round-trip
- [x] Unit test: square round-trip
- [x] Unit test: random points round-trip

### Phase 2: Constraints

- [x] Write constraint table (Polygon and Linear)
- [x] Read and reconstruct constraints
- [x] Rebuild linear constraint map
- [x] Restore constraint linking edges
- [x] Unit test: polygon constraint round-trip
- [x] Unit test: linear constraint round-trip
- [x] Unit test: constraint edge flags preserved

### Phase 3: Vertex Features

- [x] Synthetic vertex status preserved
- [x] Vertex auxiliary index preserved
- [x] VertexMergerGroup serialization with resolution rule

### Phase 4: Compression & Polish

- [x] Add GZip compression option (via `compress` parameter)
- [x] Add file convenience methods (`WriteToFile`, `ReadFromFile`)
- [x] All 15 serialization tests passing

---

## Implementation Notes

### Files Added/Modified

**New Files:**
- `Tinfour.Core/Serialization/TinSerializer.cs` - Main serialization API
- `Tinfour.Core.Tests/Serialization/TinSerializerTests.cs` - Test suite

**Modified Files:**
- `Tinfour.Core/Common/Vertex.cs` - Added `CreateForDeserialization()` and `GetZAsFloat()`
- `Tinfour.Core/Common/VertexMergerGroup.cs` - Added `GetResolutionRule()`
- `Tinfour.Core/Edge/QuadEdge.cs` - Added serialization support methods
- `Tinfour.Core/Edge/EdgePool.cs` - Added serialization support methods
- `Tinfour.Core/Standard/IncrementalTin.cs` - Added `TinSerializationState` and factory methods

### Actual Binary Format (as implemented)

**Header (8 bytes, uncompressed):**
```
Offset  Size  Description
0       4     Magic: 0x54494E53 ("TINS")
4       2     Version: 1
6       2     Flags: bit0 = compressed
```

**Payload (may be GZip compressed):**

1. **TIN State (48 bytes):**
   - 8 bytes: BoundsMinX (double)
   - 8 bytes: BoundsMaxX (double)
   - 8 bytes: BoundsMinY (double)
   - 8 bytes: BoundsMaxY (double)
   - 8 bytes: NominalPointSpacing (double)
   - 4 bytes: NSyntheticVertices (int)
   - 4 bytes: SearchEdgeBaseIndex (int)
   - 4 bytes: MaxLengthOfQueueInFloodFill (int)
   - 1 byte: Flags (bit0=isLocked, bit1=lockedDueToConstraints, bit2=isConformant)
   - 3 bytes: Reserved

2. **Vertex Table:**
   - 4 bytes: Vertex count
   - For each vertex:
     - 1 byte: Kind (1=Regular, 2=MergerGroup)
     - Regular: 8+8+4+4+1+1 = 26 bytes (x, y, z, index, status, auxiliary)
     - MergerGroup: variable (members + rule)

3. **Edge Table:**
   - 4 bytes: Edge count
   - For each edge (32 bytes):
     - 4 bytes: BaseIndex
     - 4 bytes: AVertexId (-1 for null)
     - 4 bytes: BVertexId (-1 for null)
     - 4 bytes: F0EdgeIndex (full edge index)
     - 4 bytes: R0EdgeIndex
     - 4 bytes: F1EdgeIndex
     - 4 bytes: R1EdgeIndex
     - 4 bytes: PartnerConstraintBits

4. **Constraint Table:**
   - 4 bytes: Constraint count
   - For each constraint: variable (type + properties + vertices)

---

## References

- Guibas, L. and Stolfi, J. (1985) "Primitives for the manipulation of subdivisions and the computation of Voronoi diagrams" ACM Transactions on Graphics
- Original Java Tinfour: https://github.com/gwlucastrig/Tinfour
- Quad-edge data structure: https://en.wikipedia.org/wiki/Quad-edge
