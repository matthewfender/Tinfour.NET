# Ruppert's Refinement Algorithm - Comprehensive Fix Plan

**Document Version:** 1.0
**Date:** December 2025
**Author:** Analysis by Claude Code
**Status:** Proposed

---

## Executive Summary

This document provides a comprehensive analysis of issues in the Ruppert's Delaunay Refinement implementation for the Tinfour.NET CDT project. Three major symptoms have been observed:

1. **Constraint Leakage**: Refinement "leaks" through inside or outside edges of donut-shaped constrained regions
2. **Premature Termination**: Algorithm stops when a vertex for insertion is "too close" to another
3. **Excessive Triangle Creation**: Triangle count explodes from ~600 to >200,000 in some cases

This plan identifies **12 distinct issues** across **6 code areas**, with prioritized fixes and a verification strategy.

---

## Table of Contents

1. [Issue Analysis Summary](#1-issue-analysis-summary)
2. [Root Cause Analysis](#2-root-cause-analysis)
3. [Fix Checklist](#3-fix-checklist)
4. [Detailed Fix Specifications](#4-detailed-fix-specifications)
5. [Testing Strategy](#5-testing-strategy)
6. [Implementation Order](#6-implementation-order)
7. [Risk Assessment](#7-risk-assessment)

---

## 1. Issue Analysis Summary

### 1.1 Symptoms to Root Causes Mapping

| Symptom | Primary Root Causes | Secondary Causes |
|---------|---------------------|------------------|
| Constraint Leakage | Issues #1, #2, #5, #6 | Issues #7, #8 |
| Premature Termination | Issues #3, #4 | Issue #9 |
| Excessive Triangles | Issues #10, #11, #12 | Issues #3, #4 |

### 1.2 Affected Files

| File | Issues | Severity |
|------|--------|----------|
| `RuppertRefiner.cs` | #3, #4, #9, #10, #11, #12 | HIGH |
| `IncrementalTin.cs` | #1, #2, #5, #6, #7, #8 | HIGH |
| `EdgePool.cs` | #6 | MEDIUM |
| `ConstraintProcessor.cs` | #1 | HIGH |
| `QuadEdgePartner.cs` | #2 | MEDIUM |

---

## 2. Root Cause Analysis

### Issue #1: Hole Constraints Skip Flood Fill Without Verification
**File:** `ConstraintProcessor.cs:64-68`
**Severity:** HIGH
**Category:** Constraint Leakage

**Problem:**
```csharp
if (constraint is PolygonConstraint poly && poly.IsHole())
{
    Debug.WriteLine($"Skipping flood fill for hole constraint index {constraintIndex}");
    return;  // Hole skipped - relies on outer polygon to mark donut area
}
```

The hole constraint skips flood fill entirely, assuming the outer polygon's flood fill will:
1. Mark the donut area as interior
2. Stop at the hole's border edges

**Failure Mode:** If the outer polygon is processed AFTER the hole, or if border edges aren't properly blocking flood fill, edges inside the hole can inherit interior status from the outer region.

---

### Issue #2: Border Edges Cannot Be Reclassified as Interior
**File:** `QuadEdgePartner.cs:340-343`
**Severity:** MEDIUM
**Category:** Constraint Leakage

**Problem:**
```csharp
public override void SetConstraintRegionInteriorIndex(int constraintIndex)
{
    // Border edges take precedence - don't overwrite them with interior status
    if (IsConstraintRegionBorder())
    {
        return;  // Silent return - no way to fix misclassification!
    }
    // ... set interior index ...
}
```

Once an edge is marked as `BORDER`, it cannot be changed to `INTERIOR`. If an edge is incorrectly marked as border during initial constraint processing, there's no recovery mechanism.

---

### Issue #3: Overly Strict Proximity Tolerances
**File:** `RuppertRefiner.cs:71-72, 887-902`
**Severity:** HIGH
**Category:** Premature Termination

**Problem:**
```csharp
private const double NearVertexRelTol = 1e-9;   // Extremely tight!
private const double NearEdgeRelTol = 1e-9;

// Later...
var localScale = Math.Max(1e-12, len);
var nearVertexTol = NearVertexRelTol * localScale;  // 1e-9 * edge_length

if (_lastInsertedVertex != null && _lastInsertedVertex.GetDistance(off.X, off.Y) <= nearVertexTol)
{
    System.Diagnostics.Debug.WriteLine($"Too close to last inserted vertex, returning null");
    return null;  // Algorithm may terminate prematurely
}
```

For an edge of length 1.0, the tolerance is 1 nanometer. This is excessively strict and causes premature rejection of valid Steiner points.

---

### Issue #4: Failed Vertex Insertion Returns Null Without Re-queuing Triangle
**File:** `RuppertRefiner.cs:891-901`
**Severity:** HIGH
**Category:** Premature Termination

**Problem:**
When `InsertOffcenterOrSplit` returns `null`:
1. The bad triangle was already dequeued from `_badTriangles`
2. It is never re-added to the queue
3. The main loop interprets `null` as "refinement complete" and may terminate

**Control Flow:**
```
RefineOnce() → NextBadTriangleFromQueue() [dequeues triangle]
            → InsertOffcenterOrSplit() [returns null]
            → returns null
Refine()    → if (v == null) return true;  // "Success" but triangle lost!
```

---

### Issue #5: Edge Flipping Does Not Update Constraint Flags
**File:** `EdgePool.cs:272-323` (FlipEdge method)
**Severity:** CRITICAL
**Category:** Constraint Leakage

**Problem:**
```csharp
public void FlipEdge(IQuadEdge edge)
{
    // ... extensive topology rewiring ...
    e.SetVertices(c, d);  // Endpoints changed
    // ... more topology ...
    // NO CONSTRAINT FLAG UPDATE!
}
```

When an edge is flipped during Delaunay restoration:
- Its endpoints are swapped
- Its role in the triangulation changes
- But constraint flags (border/interior) remain unchanged

An edge originally inside a constraint region may be flipped to outside, but still carries the `INTERIOR` flag.

---

### Issue #6: Inconsistent Constraint Propagation During Edge Splitting
**File:** `IncrementalTin.cs:700-747`, `EdgePool.cs:519-539`
**Severity:** HIGH
**Category:** Constraint Leakage

**Problem in IncrementalTin.SplitEdge:**
```csharp
if (ab.IsConstraintRegionInterior())
{
    var con = GetRegionConstraint(ab);
    if (con != null)  // What if null?
    {
        var idx = con.GetConstraintIndex();
        constraintIndexForC = idx;
        constraintIndexForD = idx;
    }
    // If con is null, indices stay -1 → silent failure
}
```

**Problem in EdgePool.SplitEdge:**
```csharp
if ((e.GetIndex() & 1) != 0 && e.IsConstraintRegionBorder())
{
    // This condition can never be true!
    // SplitEdge always receives base reference (even index)
}
```

---

### Issue #7: SweepForConstraintAssignments Only Checks Immediate Pinwheel
**File:** `IncrementalTin.cs:1523-1569`
**Severity:** MEDIUM
**Category:** Constraint Leakage

**Problem:**
The sweep only processes edges in the immediate pinwheel around vertex A, without recursive propagation to adjacent triangles that may also need constraint updates.

---

### Issue #8: Hole vs Solid Border Distinction Lost After Marking
**File:** `IncrementalTin.cs:716-743`
**Severity:** MEDIUM
**Category:** Constraint Leakage

**Problem:**
When splitting a border edge, the code determines which side is "inside" by checking if adjacent edges are marked as interior:
```csharp
if (bc.IsConstraintRegionInterior())
{
    constraintIndexForC = bc.GetConstraintRegionInteriorIndex();
}
```

But this doesn't distinguish:
- Interior of solid polygon (should propagate)
- Exterior of hole (the donut area - should propagate)
- Interior of hole (should NOT propagate)

---

### Issue #9: No Fallback Strategy When Vertex Insertion Fails
**File:** `RuppertRefiner.cs:891-906`
**Severity:** MEDIUM
**Category:** Premature Termination

**Problem:**
When a Steiner point is rejected, the algorithm has no alternative strategy:
- Could try circumcenter instead of offcenter
- Could try a smaller perturbation
- Could mark the triangle as "deferred" and try later

Currently, the triangle is simply lost.

---

### Issue #10: No Deduplication in Bad Triangle Queue
**File:** `RuppertRefiner.cs:1080-1082`
**Severity:** HIGH
**Category:** Excessive Triangles

**Problem:**
```csharp
foreach (var e in seed.GetPinwheel())
{
    var t = new SimpleTriangle(e);
    var p = TriangleBadPriority(t);
    if (p > 0.0)
        _badTriangles.Enqueue(new BadTri(e, p), -p);  // No dedup check!
}
```

The same triangle can be added to the queue multiple times through different vertex insertions. While validation happens on dequeue, this bloats the queue.

---

### Issue #11: UpdateBadTrianglesAroundVertex Called Multiple Times
**File:** `RuppertRefiner.cs:976-978`
**Severity:** MEDIUM
**Category:** Excessive Triangles

**Problem:**
In `SplitSegmentSmart`:
```csharp
UpdateBadTrianglesAroundVertex(a, null);  // Original endpoint A
UpdateBadTrianglesAroundVertex(b, null);  // Original endpoint B
UpdateBadTrianglesAroundVertex(v, null);  // New midpoint
```

This causes overlapping triangle regions to be scanned multiple times, with each scan potentially adding duplicates.

---

### Issue #12: TriangleBadPriority May Reject Valid Interior Triangles
**File:** `RuppertRefiner.cs:518-530`
**Severity:** MEDIUM
**Category:** Excessive Triangles (indirect)

**Problem:**
```csharp
if (!aIsMember || !bIsMember || !cIsMember)
{
    return 0.0;  // Reject if ANY edge lacks membership
}
```

If constraint flags become stale (Issue #5), valid interior triangles may be incorrectly rejected. This causes the algorithm to work around them, generating excessive triangles in neighboring regions.

---

## 3. Fix Checklist

### Critical Priority (Fix First)
- [x] **FIX-02**: Increase proximity tolerances to reasonable values (1e-9 → 1e-6)
- [x] **FIX-03**: Re-queue triangles when vertex insertion fails
- [x] **FIX-05**: Add deduplication to bad triangle queue

### High Priority
- [x] **FIX-04**: ~~Update constraint flags during edge flipping~~ REVISED: Don't clear flags on flip; let SweepForConstraintAssignments handle propagation
- [x] **FIX-06**: Handle null constraint in GetRegionConstraint gracefully
- [x] **FIX-08**: Remove unreachable partner edge condition in EdgePool.SplitEdge
- [ ] **FIX-01**: Add geometric point-in-polygon verification for hole constraints (DEFERRED - tests passing)
- [ ] **FIX-07**: Add hole-aware constraint propagation during edge splitting (DEFERRED - tests passing)

### Medium Priority
- [ ] **FIX-09**: Add fallback insertion strategies
- [ ] **FIX-10**: Extend SweepForConstraintAssignments to recursive propagation
- [ ] **FIX-11**: Allow border-to-interior reclassification with explicit API
- [ ] **FIX-12**: Optimize UpdateBadTrianglesAroundVertex to avoid overlapping scans

### Implementation Notes (December 2025)

**Implemented Fixes:**
1. **FIX-02**: Changed `NearVertexRelTol` and `NearEdgeRelTol` from `1e-9` to `1e-6` in `RuppertRefiner.cs`
2. **FIX-03**: Added re-queuing logic in `RefineOnce()` when `InsertOffcenterOrSplit` returns null but triangle is still bad
3. **FIX-05**: Added `_inBadTriangleQueue` HashSet for deduplication, created `EnqueueBadTriangle()` helper method
4. **FIX-04**: Added `ClearConstraintRegionFlags()` to `IQuadEdge`/`QuadEdge`/`QuadEdgePartner`, but decided NOT to clear flags on flip (causes issues with constraint propagation)
5. **FIX-06**: Added fallback to use edge's interior index directly when `GetRegionConstraint` returns null in `SplitEdge`
6. **FIX-08**: Simplified border constraint handling in `EdgePool.SplitEdge` by removing unreachable partner edge condition

**Test Results:** All Ruppert refinement tests pass, including donut constraint leakage tests.

---

## 4. Detailed Fix Specifications

### FIX-01: Geometric Verification for Hole Constraints
**File:** `ConstraintProcessor.cs`

**Current Code:**
```csharp
if (constraint is PolygonConstraint poly && poly.IsHole())
{
    return;  // Skip flood fill
}
```

**Proposed Change:**
```csharp
if (constraint is PolygonConstraint poly && poly.IsHole())
{
    // Verify that all edges inside the hole are NOT marked as interior
    // Use geometric point-in-polygon test against the hole boundary
    VerifyHoleInteriorIsUnmarked(poly, edgesForConstraint);
    return;
}
```

**New Method:**
```csharp
private void VerifyHoleInteriorIsUnmarked(PolygonConstraint hole, List<IQuadEdge> borderEdges)
{
    // For each edge adjacent to the hole border on the interior side,
    // verify it is not marked as constraint region member
    // If marked, clear the marking
}
```

---

### FIX-02: Increase Proximity Tolerances
**File:** `RuppertRefiner.cs`

**Current Code:**
```csharp
private const double NearVertexRelTol = 1e-9;
private const double NearEdgeRelTol = 1e-9;
```

**Proposed Change:**
```csharp
private const double NearVertexRelTol = 1e-6;  // 1 part per million
private const double NearEdgeRelTol = 1e-6;
```

**Rationale:** For typical mesh sizes (edge length ~1-100 units), 1e-6 * edge_length provides micrometer-level tolerance, which is appropriate for computational geometry.

---

### FIX-03: Re-queue Triangles on Insertion Failure
**File:** `RuppertRefiner.cs`

**Current Code:**
```csharp
private IVertex? InsertOffcenterOrSplit(SimpleTriangle tri)
{
    // ...
    if (_lastInsertedVertex != null && _lastInsertedVertex.GetDistance(off.X, off.Y) <= nearVertexTol)
    {
        System.Diagnostics.Debug.WriteLine($"Too close to last inserted vertex, returning null");
        return null;
    }
    // ...
}
```

**Proposed Change:**
```csharp
private IVertex? InsertOffcenterOrSplit(SimpleTriangle tri, out bool shouldRequeue)
{
    shouldRequeue = false;
    // ...
    if (_lastInsertedVertex != null && _lastInsertedVertex.GetDistance(off.X, off.Y) <= nearVertexTol)
    {
        System.Diagnostics.Debug.WriteLine($"Too close to last inserted vertex, will retry later");
        shouldRequeue = true;
        return null;
    }
    // ...
}
```

**Update RefineOnce:**
```csharp
public IVertex? RefineOnce()
{
    // ...
    var bad = NextBadTriangleFromQueue();
    if (bad != null)
    {
        var result = InsertOffcenterOrSplit(bad, out bool shouldRequeue);
        if (result == null && shouldRequeue)
        {
            // Re-add with lower priority (will be retried after other triangles)
            var p = TriangleBadPriority(bad);
            if (p > 0.0)
                _badTriangles.Enqueue(new BadTri(bad.GetEdgeA(), p * 0.9), -p * 0.9);
        }
        return result;
    }
    // ...
}
```

---

### FIX-04: Update Constraint Flags During Edge Flipping
**File:** `EdgePool.cs`

**Current Code:**
```csharp
public void FlipEdge(IQuadEdge edge)
{
    // ... topology changes ...
    e.SetVertices(c, d);
    // ... more topology ...
    // No constraint update
}
```

**Proposed Change:**
```csharp
public void FlipEdge(IQuadEdge edge)
{
    // ... topology changes ...
    e.SetVertices(c, d);
    // ... more topology ...

    // Clear constraint flags - they're now stale
    // The caller (RestoreConformity/RestoreDelaunay) should re-sweep
    ClearConstraintFlags(e);
}

private void ClearConstraintFlags(IQuadEdge edge)
{
    var partner = (QuadEdgePartner)edge.GetDual();
    // Clear interior/border flags but preserve line member flag
    partner.ClearRegionFlags();
}
```

**New Method in QuadEdgePartner:**
```csharp
public void ClearRegionFlags()
{
    _index &= ~(ConstraintRegionBorderFlag | ConstraintRegionInteriorFlag);
    // Keep ConstraintEdgeFlag if still constrained
    // Keep ConstraintLineMemberFlag
}
```

---

### FIX-05: Add Deduplication to Bad Triangle Queue
**File:** `RuppertRefiner.cs`

**Add Field:**
```csharp
private readonly HashSet<int> _inBadTriangleQueue;  // Track by edge base index
```

**Update Constructor:**
```csharp
_inBadTriangleQueue = new HashSet<int>();
```

**Update Enqueue Logic:**
```csharp
private void EnqueueBadTriangle(IQuadEdge repEdge, double priority)
{
    var baseIdx = repEdge.GetBaseIndex();
    if (!_inBadTriangleQueue.Contains(baseIdx))
    {
        _inBadTriangleQueue.Add(baseIdx);
        _badTriangles.Enqueue(new BadTri(repEdge, priority), -priority);
    }
}

// Update NextBadTriangleFromQueue to remove from set:
private SimpleTriangle? NextBadTriangleFromQueue()
{
    while (_badTriangles.Count > 0)
    {
        var bt = _badTriangles.Dequeue();
        _inBadTriangleQueue.Remove(bt.RepEdge.GetBaseIndex());
        // ... rest of validation ...
    }
}
```

---

### FIX-06: Handle Null Constraint Gracefully
**File:** `IncrementalTin.cs`

**Current Code:**
```csharp
if (ab.IsConstraintRegionInterior())
{
    var con = GetRegionConstraint(ab);
    if (con != null)
    {
        // ... use con ...
    }
    // Silent failure if con is null
}
```

**Proposed Change:**
```csharp
if (ab.IsConstraintRegionInterior())
{
    var con = GetRegionConstraint(ab);
    if (con != null)
    {
        var idx = con.GetConstraintIndex();
        constraintIndexForC = idx;
        constraintIndexForD = idx;
    }
    else
    {
        // Fallback: use the interior index directly from the edge
        var idx = ab.GetConstraintRegionInteriorIndex();
        if (idx >= 0)
        {
            constraintIndexForC = idx;
            constraintIndexForD = idx;
        }
        Debug.WriteLine($"Warning: GetRegionConstraint returned null for interior edge, using index {idx}");
    }
}
```

---

### FIX-07: Hole-Aware Constraint Propagation
**File:** `IncrementalTin.cs`

**Proposed Addition to SplitEdge:**
```csharp
// After determining constraintIndexForC and constraintIndexForD
// Verify the constraint isn't a hole before propagating interior status
if (constraintIndexForC >= 0 && constraintIndexForC < _constraintList.Count)
{
    var con = _constraintList[constraintIndexForC];
    if (con is PolygonConstraint poly && poly.IsHole())
    {
        // This is a hole constraint - the interior should NOT be marked
        // Check if the c-side is geometrically inside the hole
        var cCentroid = GetTriangleCentroid(m, b, c);  // New triangle after split
        if (IsPointInsidePolygon(cCentroid, poly))
        {
            constraintIndexForC = -1;  // Don't mark as interior
        }
    }
}
// Same for constraintIndexForD...
```

---

### FIX-08: Remove Unreachable Condition
**File:** `EdgePool.cs`

**Current Code:**
```csharp
if ((e.GetIndex() & 1) != 0 && e.IsConstraintRegionBorder())
{
    p.SetConstraintBorderIndex(e.GetConstraintBorderIndex());
    q.SetConstraintBorderIndex(b.GetConstraintBorderIndex());
}
else if (e.IsConstraintRegionBorder())
```

**Proposed Change:**
```csharp
// SplitEdge always receives base reference (even index), so simplify:
if (e.IsConstraintRegionBorder())
{
    var borderIdx = e.GetConstraintBorderIndex();
    if (borderIdx >= 0)
    {
        p.SetConstraintBorderIndex(borderIdx);
        // Also set on the dual if needed
        var baseBorderIdx = b.GetConstraintBorderIndex();
        if (baseBorderIdx >= 0)
        {
            q.SetConstraintBorderIndex(baseBorderIdx);
        }
    }
}
```

---

## 5. Testing Strategy

### 5.1 Unit Tests to Add/Update

| Test | Purpose | File |
|------|---------|------|
| `DonutConstraint_AfterRefinement_ShouldNotLeakOutside` | Verify no interior edges outside outer ring | `RuppertConstraintLeakageTest.cs` |
| `DonutConstraint_AfterRefinement_ShouldNotLeakIntoHole` | Verify no interior edges inside hole | `RuppertConstraintLeakageTest.cs` |
| `Refinement_WithTightClusters_ShouldNotTerminatePrematurely` | Verify completion with clustered vertices | New test |
| `Refinement_TriangleCount_ShouldBeReasonable` | Verify O(n) triangle growth | New test |
| `EdgeFlip_ShouldClearStaleConstraintFlags` | Verify flag clearing on flip | `EdgePoolTests.cs` |
| `SplitEdge_WithHoleBorder_ShouldNotPropagateToHoleInterior` | Verify hole handling | New test |

### 5.2 Integration Tests

| Scenario | Verification |
|----------|--------------|
| Simple circle constraint | Interior edges stay inside circle |
| Donut (outer + hole) | Interior edges stay in annulus |
| Multiple nested holes | Each hole properly excluded |
| High-angle refinement (45°) | Completes without excessive triangles |
| Dense vertex clusters | Completes without premature termination |

### 5.3 Regression Tests

Run the visualizer with:
- 2749 vertices, 33° Ruppert refinement, donut constraint
- Verify no "leakage" triangles
- Verify triangle count is O(n log n) not O(n²)

---

## 6. Implementation Order

### Phase 1: Critical Fixes (Unblock Development)
1. FIX-02: Increase tolerances → Immediate improvement in termination
2. FIX-03: Re-queue on failure → Prevent lost triangles
3. FIX-05: Queue deduplication → Reduce excessive processing

### Phase 2: Constraint Integrity
4. FIX-04: Edge flip constraint clearing → Prevent stale flags
5. FIX-06: Null constraint handling → Prevent silent failures
6. FIX-08: Remove dead code → Clean up

### Phase 3: Hole Handling
7. FIX-01: Hole verification → Primary leakage fix
8. FIX-07: Hole-aware propagation → Prevent propagation into holes

### Phase 4: Polish
9. FIX-09: Fallback strategies → Improve robustness
10. FIX-10: Recursive sweep → Complete constraint propagation
11. FIX-11: Border reclassification API → Future flexibility
12. FIX-12: Optimize scans → Performance improvement

---

## 7. Risk Assessment

| Fix | Risk Level | Mitigation |
|-----|------------|------------|
| FIX-01 | MEDIUM | May slow constraint processing; benchmark before/after |
| FIX-02 | LOW | Conservative change; test with edge cases |
| FIX-03 | LOW | Additive change; existing behavior preserved |
| FIX-04 | HIGH | Affects all edge flips; extensive testing required |
| FIX-05 | LOW | Additive change; only affects queue management |
| FIX-06 | LOW | Fallback behavior; improves robustness |
| FIX-07 | MEDIUM | Requires geometric testing; verify correctness |
| FIX-08 | LOW | Removing dead code; no behavioral change |
| FIX-09 | MEDIUM | New behavior paths; comprehensive testing |
| FIX-10 | MEDIUM | May cause over-propagation; careful bounds |
| FIX-11 | LOW | New API only; opt-in usage |
| FIX-12 | LOW | Performance only; verify equivalence |

---

## Appendix A: Code References

### Key Methods by Issue

| Issue | Method | File:Line |
|-------|--------|-----------|
| #1 | FloodFillConstrainedRegion | ConstraintProcessor.cs:52-77 |
| #2 | SetConstraintRegionInteriorIndex | QuadEdgePartner.cs:330-352 |
| #3,#4 | InsertOffcenterOrSplit | RuppertRefiner.cs:798-910 |
| #5 | FlipEdge | EdgePool.cs:272-323 |
| #6 | SplitEdge | IncrementalTin.cs:644-818, EdgePool.cs:487-549 |
| #7 | SweepForConstraintAssignments | IncrementalTin.cs:1523-1569 |
| #8 | SplitEdge (border handling) | IncrementalTin.cs:716-747 |
| #10,#11 | UpdateBadTrianglesAroundVertex | RuppertRefiner.cs:1024-1084 |
| #12 | TriangleBadPriority | RuppertRefiner.cs:501-601 |

---

## Appendix B: Constraint Flag Architecture

```
QuadEdge (base, even index)
    _index = geometric edge index (e.g., 0, 2, 4, ...)
    _dual → QuadEdgePartner

QuadEdgePartner (dual, odd index)
    _index = constraint flags + indices
        Bit 31: ConstraintEdgeFlag (indicates any constraint)
        Bit 30: ConstraintRegionBorderFlag
        Bit 29: ConstraintRegionInteriorFlag
        Bit 28: ConstraintLineMemberFlag
        Bit 27: SyntheticEdgeFlag
        Bit 26: Reserved
        Bits 13-25: Line constraint index (13 bits, max 8190)
        Bits 0-12: Region constraint index (13 bits, max 8190)
    _dual → QuadEdge (base)
```

---

## Appendix C: Test Case Specifications

### Donut Constraint Test Parameters
```csharp
// Outer ring
centerX = 0, centerY = 0
outerRadius = 30.0
numOuterPoints = 32

// Inner hole
innerRadius = 15.0
numInnerPoints = 32

// Refinement
minAngleDegrees = 30.0 to 33.0
maxIterations = 500-1000
minTriangleArea = 0.1
```

### Expected Results
- Total triangles: O(n) where n = input vertices + constraint vertices
- Interior edges: All within annulus (15 ≤ distance ≤ 30)
- Border edges: On constraint boundaries only
- No edges marked interior with centroid outside annulus

---

*End of Document*
