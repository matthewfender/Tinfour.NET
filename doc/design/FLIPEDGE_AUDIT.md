# FlipEdge and Topology Change Audit

## Purpose

This audit identifies every call site in the Tinfour.NET codebase where edge topology changes (flip, split, or manual vertex reassignment), evaluates constraint flag safety at each site, and flags gaps for Phase 2 repair. It is the primary reference for implementers tasked with fixing constraint-region-interior flag leakage that causes Steiner points and refined triangles to escape the constrained region boundary.

## Summary Table

| # | Location | Method | Flag Handling | Risk Level |
|---|----------|--------|---------------|------------|
| 1 | `EdgePool.cs` line 272 | `FlipEdge()` | NONE | **HIGH** |
| 2 | `IncrementalTin.cs` line 1055 | `ExtendTin()` | NONE (calls FlipEdge with no sweep) | **HIGH** |
| 3 | `IncrementalTin.cs` line 1671 | `RestoreConformity()` flip path | Clear + sweep + manual interior propagation | LOW |
| 4 | `ConstraintProcessor.cs` line 676 | `RecursiveRestoreDelaunay()` | NONE (safe by timing only) | MEDIUM |
| 5 | `EdgePool.cs` line 497 | `EdgePool.SplitEdge()` | Copies dual index; explicit border propagation | LOW |
| 6 | `IncrementalTin.cs` line 804 | `IncrementalTin.SplitEdge()` | Full constraint propagation (border + interior) | LOW |
| 7 | `IncrementalTin.cs` line 1566 | `RestoreConformity()` constrained-edge split path | Explicit border/interior propagation to new edges | LOW |

## Detailed Audit

### Site 1: EdgePool.FlipEdge()

- **File:** `Tinfour.Core/Edge/EdgePool.cs` (line 272)
- **Trigger:** Called exclusively by `IncrementalTin.ExtendTin()` (confirmed via grep -- the only call site is at `IncrementalTin.cs` line 1096)
- **What it does:** Reassigns vertices on a quad-edge pair so that edge `e(a,b)` becomes `e(c,d)` using the opposite diagonal of the convex quadrilateral. Rewires forward cycles for both new triangles.
- **Constraint flag operations performed:** NONE. The method does not read, set, clear, copy, or propagate any constraint flags.
- **Comment at lines 305-313:** States: _"The caller (RestoreConformity) should call SweepForConstraintAssignments which will properly propagate constraint membership from neighboring edges."_ **This comment is misleading**: RestoreConformity does NOT call EdgePool.FlipEdge -- it performs its own inline flip with proper flag handling. The actual and only caller is ExtendTin, which does NOT sweep.
- **Constraint flag operations missing:** At minimum, `ClearConstraintRegionFlags()` should be called on the flipped edge, since the edge now connects different vertices and its old region membership is stale. A subsequent sweep or propagation is needed to re-establish correct flags.
- **Gap:** The sole caller (`ExtendTin`) does not call `SweepForConstraintAssignments` or `ClearConstraintRegionFlags` after the flip loop.
- **Risk:** **HIGH** -- Flipped edges retain stale constraint-region-interior flags. After constraints are added and flood fill has marked interior edges, any subsequent vertex insertion that triggers `ExtendTin` (which calls `FlipEdge`) will produce edges with incorrect region membership. This is the primary mechanism by which Ruppert refinement can produce Steiner points outside the constrained region boundary.
- **Phase 2 action needed:** YES. Either (a) add flag clearing/sweep logic inside `FlipEdge()` itself, or (b) add sweep logic in `ExtendTin()` after the flip loop, or (c) both.

### Site 2: IncrementalTin.ExtendTin()

- **File:** `Tinfour.Core/Standard/IncrementalTin.cs` (line 1055)
- **Trigger:** Called by `InsertVertex()` (line 1291) when the new vertex lies outside the current convex hull (i.e., the point-location walk finds a ghost edge). This can happen during Ruppert refinement when circumcenter Steiner points fall outside the TIN boundary.
- **What it does:** Creates new edges connecting the exterior vertex to the perimeter of the TIN. Then walks around the perimeter in a `while(true)` loop (lines 1084-1111) calling `_edgePool.FlipEdge(n1)` to restore the Delaunay criterion along the new hull boundary.
- **Constraint flag operations performed:** NONE. No flag operations before, during, or after the flip loop.
- **Constraint flag operations missing:** After the flip loop completes, `SweepForConstraintAssignments` should be called on the affected edges to ensure correct constraint region membership. Alternatively, `ClearConstraintRegionFlags()` should be called on each edge before it is flipped.
- **Gap:** This is the "other half" of Site 1's problem. Even if FlipEdge itself were fixed, ExtendTin creates new edges (n1, n2 at lines 1072-1075) with no constraint region membership consideration. When these edges enter the flip loop, they may emerge with stale flags inherited from the flip.
- **Risk:** **HIGH** -- This is the direct caller of the unsafe FlipEdge. Every vertex insertion that extends the convex hull after constraints have been applied will produce edges with potentially incorrect constraint flags.
- **Phase 2 action needed:** YES. Add constraint flag handling to the flip loop. This may include clearing flags before each flip and/or running a sweep after the loop completes. The newly allocated edges (n1, n2) also need correct constraint region assignment via `PropagateConstraintRegionMembership` or similar.

### Site 3: IncrementalTin.RestoreConformity() -- Flip Path (unconstrained edge)

- **File:** `Tinfour.Core/Standard/IncrementalTin.cs` (lines 1671-1727)
- **Trigger:** Called on every edge in the TIN after constraint processing in `AddConstraints()` (line 467). Also called recursively on surrounding edges (lines 1722-1725). Enters the flip path when `ab.IsConstrained()` is false and the Delaunay criterion is violated.
- **What it does:** Performs an inline edge flip (does NOT call `EdgePool.FlipEdge()`). Reassigns vertices at line 1706 (`ab.SetVertices(d, c)`), then rewires forward/reverse links for both triangles.
- **Constraint flag operations performed:**
  1. **Pre-flip analysis** (lines 1683-1701): Scans all 4 surrounding edges (bc, ca, ad, db) for `IsConstraintRegionInterior()`. If all interior edges share the same constraint index, records it.
  2. **ClearConstraintRegionFlags** (line 1704): Clears stale region flags before reassigning vertices.
  3. **Re-mark interior** (lines 1715-1718): If surrounding edges consistently indicate interior membership, marks the flipped edge with `SetConstraintRegionInteriorIndex`.
  4. **SweepForConstraintAssignments** (line 1727): Called at the end when `constraintSweepRequired` is true (i.e., when `_maxLengthOfQueueInFloodFill > 0`, meaning flood fill has completed).
- **Constraint flag operations missing:** None identified. This is the correct pattern: clear, flip, re-mark, sweep.
- **Gap:** None. This site demonstrates the proper pattern that other sites should follow.
- **Risk:** **LOW** -- Correctly handles constraint flags through the full clear-flip-remark-sweep cycle.
- **Phase 2 action needed:** NO. This is the reference implementation for correct flag handling during edge flips.

### Site 4: ConstraintProcessor.RecursiveRestoreDelaunay()

- **File:** `Tinfour.Core/Standard/ConstraintProcessor.cs` (line 676)
- **Trigger:** Called from `FillCavity()` (line 523) which is called from `ProcessConstraint()` (lines 421-422) after tunneling through the triangulation to insert a constraint edge. The cavity on each side of the new constraint edge is filled with triangles, and then `RecursiveRestoreDelaunay` restores local Delaunay conformity on the new fill edges.
- **What it does:** Performs an inline edge flip (lines 694-700): reassigns vertices with `n.SetVertices(t, c)`, rewires forward links. Recursively processes all 4 surrounding edges.
- **Constraint flag operations performed:** NONE. The only check is `n.IsConstrained()` at line 678, which prevents flipping constrained (border) edges.
- **Constraint flag operations missing:** No `ClearConstraintRegionFlags`, no `SweepForConstraintAssignments`, no interior index propagation.
- **Timing analysis:** This method runs during Phase 3 of `AddConstraints()` (constraint processing), which is BEFORE Phase 5 (flood fill). At this point:
  - Border flags (`ConstraintRegionBorderFlag`) exist on constraint edges (set by `SetConstrained` in `ProcessConstraint`).
  - Interior flags (`ConstraintRegionInteriorFlag`) have NOT been set yet (flood fill hasn't happened).
  - The `IsConstrained()` guard at line 678 prevents flipping border edges (since `SetConstraintBorderIndex` also sets `ConstraintEdgeFlag`).
  - Non-constrained edges carry no region flags at this stage, so there is nothing to go stale.
- **Gap:** The method has zero flag handling, but this is masked by execution timing. If this method were ever called AFTER flood fill, it would produce the same stale-flag problem as FlipEdge/ExtendTin.
- **Risk:** **MEDIUM** -- Currently safe because it runs before flood fill, but the safety is implicit and fragile. Any refactoring that changes the execution order of AddConstraints phases, or any reuse of this method in a post-flood-fill context, would silently introduce the constraint escape bug. The lack of defensive flag handling is a code smell.
- **Phase 2 action needed:** RECOMMENDED (not required). Adding `ClearConstraintRegionFlags()` and a post-flip sweep would make the method robust regardless of call-site timing. At minimum, add a comment documenting the timing dependency.

### Site 5: EdgePool.SplitEdge()

- **File:** `Tinfour.Core/Edge/EdgePool.cs` (line 497)
- **Trigger:** Called from `IncrementalTin.RestoreConformity()` constrained-edge split path (line 1596), and from `IncrementalTin.SplitEdge()` (line 887).
- **What it does:** Splits edge `e(a,b)` by inserting vertex `m`, producing `p(a,m)` (new) and `e(m,b)` (modified original). Rewires forward/reverse links. Adjusts the new edge's dual index.
- **Constraint flag operations performed:**
  1. **Dual index copy** (line 529): `p._dual._index = b._dual._index` -- copies the entire constraint bitfield from the original edge's dual to the new edge's dual. This preserves border, interior, line, and synthetic flags.
  2. **Border propagation** (lines 535-542): If the original edge `e` is a region border, explicitly copies the border index to the new edge `p` via `SetConstraintBorderIndex`.
  3. **Line constraint mapping** (lines 545-548): If the original edge is a line constraint member, copies the linear constraint map entry to the new edge.
- **Constraint flag operations missing:** The method does not set interior flags on newly created connecting edges (cm, dm) -- but that is correctly delegated to the caller (Sites 6 and 7).
- **Gap:** None for the split operation itself. The dual index copy at line 529 is a bulk copy that captures all flags. The explicit border propagation at lines 535-542 is a belt-and-suspenders check.
- **Risk:** **LOW** -- Constraint flags are correctly propagated to both halves of the split edge.
- **Phase 2 action needed:** NO. The split itself is correct. Callers are responsible for handling connecting edges.

### Site 6: IncrementalTin.SplitEdge()

- **File:** `Tinfour.Core/Standard/IncrementalTin.cs` (line 804)
- **Trigger:** Called from external code (e.g., Ruppert refinement) to split an edge at a parametric position. Handles both interior edges and perimeter edges (with ghost triangles).
- **What it does:** Creates a new vertex at the split point, calls `_edgePool.SplitEdge()` to split the edge, then creates connecting edges (cm, dm) to opposite vertices and wires up the topology for 4 new triangles (or 2 triangles + 2 ghost triangles for perimeter edges).
- **Constraint flag operations performed:**
  1. **Constraint index detection** (lines 853-877): Determines the constraint region index for each side (c-side and d-side) by checking if the split edge is interior (use its index for both sides) or border (check neighboring edges bc, ca, ad, db for interior membership).
  2. **Border preservation** (lines 881-897): Remembers if the edge was a border before splitting, and ensures BOTH halves (am and mb) retain border status with the correct index after the split.
  3. **Interior propagation to connecting edges** (lines 913-914, 967): Sets `SetConstraintRegionInteriorIndex` on the connecting edges cm and dm using the detected constraint indices.
  4. **Vertex status** (lines 831-839): Marks the new vertex as synthetic and optionally as constrained.
- **Constraint flag operations missing:** None identified. This site handles all cases: interior edges, border edges, perimeter edges with ghost triangles.
- **Gap:** None.
- **Risk:** **LOW** -- Comprehensive constraint flag handling.
- **Phase 2 action needed:** NO.

### Site 7: IncrementalTin.RestoreConformity() -- Constrained-Edge Split Path

- **File:** `Tinfour.Core/Standard/IncrementalTin.cs` (lines 1566-1670)
- **Trigger:** Same as Site 3 (RestoreConformity), but enters the split path when `ab.IsConstrained()` is true and the Delaunay criterion is violated. This path subdivides the constrained edge rather than flipping it.
- **What it does:** Creates a midpoint vertex `m`, calls `_edgePool.SplitEdge(ab, m)` to split the constrained edge, then creates connecting edges (cm, dm) to opposite vertices (c, d) and wires up 4 new triangles.
- **Constraint flag operations performed:**
  1. **Z interpolation** (lines 1577-1587): Uses the pre-built TIN interpolator (if available) to compute the Z value for the midpoint, falling back to linear interpolation.
  2. **Edge split** (line 1596): Delegates to `EdgePool.SplitEdge` which preserves constraint flags on both halves (Site 5).
  3. **Interior propagation for border edges** (lines 1624-1655): When the split edge is a border, checks neighboring edges (bc, ca for c-side; ad, db for d-side) for interior membership and propagates the constraint index to connecting edges cm and dm.
  4. **Interior propagation for interior edges** (lines 1656-1665): When the split edge is interior, propagates the same interior index to both cm and dm.
  5. **Recursive conformity check** (lines 1668-1669): Recursively calls RestoreConformity on both halves of the split edge.
- **Constraint flag operations missing:** None identified.
- **Gap:** None.
- **Risk:** **LOW** -- Properly propagates constraint flags to all new edges created during the split.
- **Phase 2 action needed:** NO.

## Cross-Reference: Constraint Flag Methods

### Flag Storage

All constraint flags are stored in the `_index` field of `QuadEdgePartner` (the dual edge). The primary `QuadEdge` delegates all constraint operations to its dual.

| Flag | Bit | Constant |
|------|-----|----------|
| Constrained edge | bit 31 (sign bit) | `ConstraintEdgeFlag` |
| Region border | bit 30 | `ConstraintRegionBorderFlag` |
| Region interior | bit 29 | `ConstraintRegionInteriorFlag` |
| Line member | bit 28 | `ConstraintLineMemberFlag` |
| Synthetic | bit 27 | `SyntheticEdgeFlag` |
| Region member (composite) | bits 29-30 | `ConstraintRegionMemberFlags` |

Lower bits (0-14) store the constraint index for region/polygon constraints (up to 32,766).
Upper bits (15-26) store the constraint index for line constraints (up to 4,094).

### Query Methods

| Method | File | What it checks |
|--------|------|---------------|
| `IsConstrained()` | `QuadEdgePartner.cs` line 170 | `_index < 0` (sign bit = ConstraintEdgeFlag) |
| `IsConstraintRegionBorder()` | `QuadEdgePartner.cs` line 195 | `_index & ConstraintRegionBorderFlag` |
| `IsConstraintRegionInterior()` | `QuadEdgePartner.cs` line 206 | `_index & ConstraintRegionInteriorFlag` |
| `IsConstraintRegionMember()` | `QuadEdgePartner.cs` line 217 | `_index & ConstraintRegionMemberFlags` |
| `IsConstraintLineMember()` | `QuadEdgePartner.cs` line 181 | `_index & ConstraintLineMemberFlag` |
| `GetConstraintBorderIndex()` | `QuadEdgePartner.cs` line 89 | Extracts lower index if border flag set |
| `GetConstraintRegionInteriorIndex()` | `QuadEdgePartner.cs` line 138 | Extracts lower index if interior flag set |

### Mutation Methods

| Method | File | What it does |
|--------|------|-------------|
| `SetConstraintBorderIndex(idx)` | `QuadEdgePartner.cs` line 239 | Sets border flag + edge flag + packs lower index |
| `SetConstraintRegionInteriorIndex(idx)` | `QuadEdgePartner.cs` line 327 | Sets interior flag + packs lower index. **Will not overwrite border** (line 337). |
| `SetConstraintLineIndex(idx)` | `QuadEdgePartner.cs` line 284 | Sets line member flag + edge flag + packs upper index |
| `SetConstraintLineMemberFlag()` | `QuadEdgePartner.cs` line 306 | Sets only the line member bit |
| `ClearConstraintRegionFlags()` | `QuadEdgePartner.cs` line 383 | Clears border + interior flags + lower index bits. Preserves edge flag and line bits. |

### Propagation Methods

| Method | File | What it does |
|--------|------|-------------|
| `SweepForConstraintAssignments(ab)` | `IncrementalTin.cs` line 1737 | Starting from edge `ab`, if it's a region member, propagates interior flags to all edges in the pinwheel around vertex A. Respects border precedence. |
| `PropagateConstraintRegionMembership(pStart, idx)` | `IncrementalTin.cs` line 1430 | After vertex insertion inside a constraint region, marks all edges radiating from the new vertex as interior members. Handles border crossing. |
| `FloodFillConstrainedRegion(constraint, visited, edges)` | `ConstraintProcessor.cs` line 52 | BFS from border edges inward, marking all interior edges with the constraint index. Run once during `AddConstraints()` Phase 5. |

## Findings and Recommendations

### Critical Issues (Phase 2 Priority)

**1. EdgePool.FlipEdge + ExtendTin (Sites 1 + 2): Stale constraint flags after hull extension**

This is the most dangerous gap. When a vertex is inserted outside the convex hull after constraints have been applied:
1. `ExtendTin()` is called
2. It calls `_edgePool.FlipEdge()` in a loop
3. `FlipEdge` reassigns edge vertices but does not touch constraint flags
4. Edges that were interior to a constraint region before the flip may now connect different vertices outside the region, but still carry the interior flag
5. Ruppert refinement trusts these flags and may refine triangles that are not actually inside the constraint region

**Locking analysis:** After `AddConstraints()` returns, `_lockedDueToConstraints` is set to `true` but `_isLocked` remains `false`. The `Add()` method only checks `_isLocked`, meaning vertex insertion (including the `ExtendTin` path) is NOT blocked after constraints are added. The gap is live, not theoretical.

**The misleading comment in FlipEdge (lines 305-313)** claims the caller is RestoreConformity and advises calling `SweepForConstraintAssignments`. In reality:
- RestoreConformity does NOT call EdgePool.FlipEdge (it does its own inline flip with proper handling)
- The only caller, ExtendTin, does NOT sweep
- The comment creates a false sense of safety

**Recommendation:** Phase 2 should add `ClearConstraintRegionFlags()` to flipped edges inside `FlipEdge()` and add `SweepForConstraintAssignments` (or equivalent) after the flip loop in `ExtendTin()`. The misleading comment should be corrected or removed.

**2. RecursiveRestoreDelaunay (Site 4): Timing-dependent safety**

Currently safe because it runs before flood fill (so no interior flags exist to go stale), but the safety is implicit. The method contains zero defensive flag handling. Any future change that reorders the AddConstraints phases or reuses this method in a post-flood-fill context would silently reintroduce the constraint escape bug.

**Recommendation:** Add defensive `ClearConstraintRegionFlags()` call and/or document the timing dependency. Lower priority than Sites 1+2 since it's currently not producing incorrect results.

### Safe Sites

Sites 3, 5, 6, and 7 demonstrate correct constraint flag handling patterns:
- **RestoreConformity flip path** (Site 3): The gold standard -- clear flags, flip, re-mark from neighbors, sweep.
- **EdgePool.SplitEdge** (Site 5): Bulk-copies constraint bitfield to new edge half via dual index copy.
- **IncrementalTin.SplitEdge** (Site 6): Full constraint detection and propagation for both border and interior cases.
- **RestoreConformity split path** (Site 7): Explicit border/interior propagation to connecting edges.

### Phase 2 Repair Priority

1. **Sites 1+2 (FlipEdge + ExtendTin):** Fix immediately. This is the primary constraint escape vector.
2. **Site 4 (RecursiveRestoreDelaunay):** Add defensive handling. Lower priority but eliminates a latent risk.
3. **Sites 3, 5, 6, 7:** No changes needed.
