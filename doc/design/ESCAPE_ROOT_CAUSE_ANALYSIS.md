# Steiner Point Escape: Root Cause Analysis

## Summary

Ruppert refinement inserted Steiner points outside constraint polygon boundaries.
The root cause is a single check in `IncrementalTin.InsertVertex()` that uses
`IsConstraintRegionMember()` (which matches both border AND interior edges) when
it should use `IsConstraintRegionInterior()` (interior only). This conflation
causes vertices inserted in border-straddling triangles to inherit an interior
constraint index, even when the vertex is geometrically outside the constraint.
The resulting flag corruption cascades exponentially.

## 1. The Exact Mechanism

### Location
`Tinfour.Core/Standard/IncrementalTin.cs`, method `InsertVertex()`, lines 1327-1345.

### The Faulty Check (Before Fix)
```csharp
IQuadEdge? memberEdge = null;
if (eA.IsConstraintRegionMember()) memberEdge = eA;  // <-- BUG
else if (eB.IsConstraintRegionMember()) memberEdge = eB;
else if (eC.IsConstraintRegionMember()) memberEdge = eC;
```

### Why It's Wrong
`IsConstraintRegionMember()` returns true for BOTH border edges and interior edges.
A border edge separates the inside of the constraint from the outside. A triangle
that straddles the border has one edge that IS a border (a member), but the vertex
being inserted may be on the EXTERIOR side of that border. The check falsely
concludes the vertex is inside the constraint and sets `vertexConstraintIndex >= 0`.

### The Cascade
1. Ruppert selects a bad interior triangle near the constraint border
2. The off-center point lands outside the constraint polygon (a geometric escape)
3. `InsertVertex()` finds the containing triangle of the off-center
4. The containing triangle straddles the border -- one edge IS a border edge
5. `IsConstraintRegionMember()` fires on the border edge
6. `vertexConstraintIndex` is set to the constraint's index
7. `PropagateConstraintRegionMembership()` marks ALL new edges from the exterior
   vertex as interior (typically 6-8 edges)
8. These falsely-interior edges create triangles that pass `TriangleBadPriority`
9. Ruppert refines these exterior triangles, inserting more exterior Steiner points
10. Each exterior insertion amplifies the corruption (positive feedback loop)

### Empirical Evidence

Initial state after `AddConstraints()`: **0 wrongly-flagged edges** (confirmed clean).

First corruption at insertion #24:
- Off-center at (533422.8, 5219486.1) lands **below** the rectangle's bottom edge
- Containing triangle has the bottom border as one edge (a constraint region member)
- 8 edges from this exterior point to border vertices get flagged as interior
- Corruption grows ~3 edges per subsequent exterior insertion

Without fix (final state): **2439 wrongly-flagged edges**, 562 leaked Steiner points (18.2%).

### Bucket Classification (2909 total insertions, no re-flood)

| Bucket | Count | Description |
|--------|-------|-------------|
| Correct | 2347 | Triangle inside, point inside |
| Bucket 1 | 4 | Geometric escape only (no flag involvement) |
| Bucket 2 | 45 | Flag crossed border during propagation |
| **Bucket 3** | **513** | **Flag corruption: exterior triangle, all edges flagged interior** |
| Bucket 4 | 0 | TriangleBadPriority correctly rejects unflagged triangles |

**91% of all leaks are Bucket 3** (flag corruption cascade).

## 2. The Fix

### Change
In `InsertVertex()`, replace `IsConstraintRegionMember()` with `IsConstraintRegionInterior()`:

```csharp
IQuadEdge? memberEdge = null;
if (eA.IsConstraintRegionInterior()) memberEdge = eA;
else if (eB.IsConstraintRegionInterior()) memberEdge = eB;
else if (eC.IsConstraintRegionInterior()) memberEdge = eC;
```

### Why It Works
Interior edges unambiguously indicate the triangle is inside the constraint. In a
valid CDT, any inside triangle adjacent to the border has at least one interior edge
(connecting to vertices strictly inside, or between border vertices on the interior
side). An outside triangle adjacent to the border has only border edges as members --
no interior edges.

### Post-Fix Results

**Rectangle Constraint:**

| Metric | Before Fix | After Fix | Change |
|--------|-----------|-----------|--------|
| Leaked Steiner points | 562 (18.2%) | 42 (1.6%) | -92.5% |
| Bucket 3 (flag corruption) | 513 | **0** | -100% |
| Total insertions | 2909 | 2498 | -14% fewer (no wasted exterior work) |

**Convex Hull Constraint:**

| Metric | Before Fix | After Fix | Change |
|--------|-----------|-----------|--------|
| Bucket 3 (flag corruption) | dominant | **0** | -100% |
| Correct insertions | - | 3977/3977 | 100% |

Both constraint types confirm Bucket 3 is fully eliminated.

## 3. Why the 4 Prior Targeted Fixes Had No Effect

All four prior fixes addressed flag propagation DOWNSTREAM of the faulty decision:

1. **FlipEdge clearing flags**: The flip in `ExtendTin()` is for hull extension, not
   the Lawson flip during interior insertion. The bug is in `InsertVertex()`, not `ExtendTin()`.

2. **InsertVertex clearing flags after ExtendTin**: The hull-extension path (vertex
   outside convex hull) correctly clears flags. The bug is in the interior-insertion
   path (vertex inside convex hull but outside constraint).

3. **PropagateConstraintRegionMembership checking all 3 edges**: The propagation
   correctly checks all edges. The problem is that the DECISION to propagate
   (`vertexConstraintIndex >= 0`) was wrong.

4. **RestoreConformity triangle-adjacency inference**: This only runs during initial
   `AddConstraints()`, where the flag state is verified clean. It doesn't affect
   the ongoing Ruppert refinement.

The fixes were treating symptoms of the cascade (incorrectly flagged edges) rather
than the root cause (the faulty membership check that seeds the cascade).

## 4. Whether Re-Flood Is Still Needed

**No.** Re-flood was a workaround that periodically reset interior flags to contain
the cascade. With the fix applied:

- Bucket 3 (flag corruption) drops to **0** with or without re-flood
- Results are identical with `EnableReFlood = true` or `false`
- The re-flood mechanism can be disabled or removed

## 5. Remaining Leaks

42 Steiner points (1.6%) still escape with the fix. These fall into:

- **Bucket 1 (33)**: Geometric escapes where the off-center of an interior triangle
  lands outside the constraint, but no flag corruption occurs. The point is simply
  placed outside. These could be addressed by adding a geometric PIP check on the
  off-center before insertion, or by widening the encroachment search radius.

- **Bucket 2 (9)**: Cases where `PropagateConstraintRegionMembership` crosses a
  border edge in the pinwheel traversal without resetting the constraint index.
  These are a secondary bug in the propagation logic (the pinwheel sets
  `currentConstraintIndex` to the border's constraint index when crossing from
  inside to outside, instead of setting it to -1).

Both are separate, lower-priority issues. The dominant mechanism (Bucket 3, 91% of
leaks) is fully eliminated by this fix.
