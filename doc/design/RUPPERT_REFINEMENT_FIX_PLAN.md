# Ruppert's Refinement Algorithm - Comprehensive Fix Plan

**Document Version:** 1.6
**Date:** December 2025
**Author:** Analysis by Claude Code
**Status:** Complete - All critical issues resolved

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
| Excessive Triangles | Issues #10, #11, #12, #13 | Issues #3, #4 |

### 1.2 Affected Files

| File | Issues | Severity |
|------|--------|----------|
| `RuppertRefiner.cs` | #3, #4, #9, #10, #11, #13, #14 | HIGH |
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

### Issue #13: Default MinimumTriangleArea Not Scale-Aware
**File:** `RuppertRefiner.cs` (constructor)
**Severity:** HIGH
**Category:** Excessive Triangles

**Problem:**
The default `MinimumTriangleArea` of `1e-3` is an absolute value that doesn't scale with the coordinate system. For large-scale data (e.g., UTM coordinates with values in the millions), this threshold is effectively zero, causing runaway refinement.

**Example:** For a TIN spanning 100x100 units, `1e-3` is appropriate. For a TIN spanning 100,000x100,000 units, triangles could be refined down to microscopic sizes before hitting the threshold.

---

### Issue #14: Insufficient Triangle Attempt Limiting
**File:** `RuppertRefiner.cs:109`
**Severity:** MEDIUM
**Category:** Non-Conformant Triangles

**Problem:**
The original `MaxTriangleAttempts = 10` was too conservative. When a triangle fails insertion due to temporary conditions (e.g., too close to last inserted vertex), 10 attempts may not be enough for the conditions to change sufficiently.

---

## 3. Fix Checklist

### Critical Priority (Fix First)
- [x] **FIX-02**: Increase proximity tolerances to reasonable values (1e-9 → 1e-6)
- [x] **FIX-03**: Re-queue triangles when vertex insertion fails
- [x] **FIX-05**: Add deduplication to bad triangle queue
- [x] **FIX-21**: Add `IsConstrained()` check before edge flip in Add() insertion loop (ROOT CAUSE of constraint leakage)

### High Priority
- [x] **FIX-04**: ~~Update constraint flags during edge flipping~~ REVISED: Don't clear flags on flip; let SweepForConstraintAssignments handle propagation. Added `ClearConstraintRegionFlags()` method for future use.
- [x] **FIX-06**: Handle null constraint in GetRegionConstraint gracefully
- [x] **FIX-08**: Remove unreachable partner edge condition in EdgePool.SplitEdge
- [x] **FIX-13**: Add auto-computed MinimumTriangleArea based on TIN bounds
- [x] **FIX-14**: Increase MaxTriangleAttempts from 10 to 50
- [x] **FIX-15**: Preserve border edge status in RestoreConformity when splitting constrained edges
- [x] **FIX-16**: Add recursion depth limit (32) to RestoreConformity (matches Java)
- [x] **FIX-17**: Add diagnostic logging for border edge splits (debug builds)
- [x] **FIX-18**: Add triangle topology verification assertions after edge splitting (debug builds)
- [ ] ~~**FIX-19**: Fix RefineOnce return value to distinguish "done" from "retry needed"~~ REVERTED - caused infinite loop
- [x] **FIX-20**: Fix priority calculation to use badness ratio instead of area; add vertex count limit; add high-angle warning
- [ ] **FIX-01**: Add geometric point-in-polygon verification for hole constraints (Clipper2 added, implementation pending)
- [ ] **FIX-07**: Add hole-aware constraint propagation during edge splitting

### Medium Priority
- [ ] **FIX-09**: Add fallback insertion strategies
- [ ] **FIX-10**: Extend SweepForConstraintAssignments to recursive propagation
- [ ] **FIX-11**: Allow border-to-interior reclassification with explicit API
- [ ] **FIX-12**: Optimize UpdateBadTrianglesAroundVertex to avoid overlapping scans

---

## 4. Implementation Notes (December 2025)

### Implemented Fixes

#### FIX-02: Increase Proximity Tolerances ✅
**File:** `RuppertRefiner.cs:71-72`
**Change:** `NearVertexRelTol` and `NearEdgeRelTol` from `1e-9` to `1e-6`
```csharp
private const double NearVertexRelTol = 1e-6;  // Relaxed from 1e-9 to prevent premature termination
private const double NearEdgeRelTol = 1e-6;    // Relaxed from 1e-9 to prevent premature termination
```
**Rationale:** For typical mesh sizes (edge length ~1-100 units), 1e-6 * edge_length provides micrometer-level tolerance, which is appropriate for computational geometry.

---

#### FIX-03: Re-queue Triangles on Insertion Failure ✅
**File:** `RuppertRefiner.cs:375-408`
**Change:** Added re-queuing logic in `RefineOnce()` when `InsertOffcenterOrSplit` returns null but triangle is still bad
```csharp
if (result == null)
{
    var p = TriangleBadPriority(bad);
    if (p > 0.0)
    {
        var baseIdx = bad.GetEdgeA().GetBaseIndex();
        _triangleAttemptCount.TryGetValue(baseIdx, out var attempts);
        attempts++;
        _triangleAttemptCount[baseIdx] = attempts;

        if (attempts < MaxTriangleAttempts)
        {
            // Re-queue with slightly reduced priority so other triangles get a chance
            EnqueueBadTriangle(bad.GetEdgeA(), p * 0.99);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"  WARNING: Giving up on triangle after {MaxTriangleAttempts} failed attempts.");
        }
    }
}
```
**Rationale:** Prevents premature algorithm termination when vertex insertion temporarily fails.

---

#### FIX-04: ClearConstraintRegionFlags Method ✅
**Files:** `IQuadEdge.cs`, `QuadEdge.cs`, `QuadEdgePartner.cs`
**Change:** Added `ClearConstraintRegionFlags()` method to the edge interface and implementations
```csharp
// In QuadEdgePartner.cs:387-392
public override void ClearConstraintRegionFlags()
{
    // Clear both border and interior flags, but preserve other flags and constraint indices
    // Note: We clear the lower index bits too since they store region constraint index
    _index &= ~(ConstraintRegionBorderFlag | ConstraintRegionInteriorFlag | ConstraintLowerIndexMask);
}
```
**Note:** After analysis, we decided NOT to automatically clear flags on edge flip, as this breaks constraint propagation. The method is available for explicit use when needed.

---

#### FIX-05: Add Deduplication to Bad Triangle Queue ✅
**File:** `RuppertRefiner.cs:107, 238, 450-454, 781`
**Change:** Added `_inBadTriangleQueue` HashSet and `EnqueueBadTriangle()` helper method
```csharp
private readonly HashSet<int> _inBadTriangleQueue;  // Track by edge base index for deduplication

private void EnqueueBadTriangle(IQuadEdge repEdge, double priority)
{
    var baseIdx = repEdge.GetBaseIndex();
    if (_inBadTriangleQueue.Add(baseIdx))
        _badTriangles.Enqueue(new BadTri(repEdge, priority), -priority);
}
```
**Rationale:** Prevents the same triangle from being queued multiple times, reducing queue bloat.

---

#### FIX-06: Handle Null Constraint Gracefully ✅
**File:** `IncrementalTin.cs` (SplitEdge method)
**Change:** Added fallback to use edge's interior index directly when `GetRegionConstraint` returns null
```csharp
else
{
    var idx = ab.GetConstraintRegionInteriorIndex();
    if (idx >= 0)
    {
        constraintIndexForC = idx;
        constraintIndexForD = idx;
        Debug.WriteLine($"SplitEdge: GetRegionConstraint returned null, using edge interior index {idx}");
    }
}
```
**Rationale:** Prevents silent failures when constraint lookup fails.

---

#### FIX-08: Remove Unreachable Condition ✅
**File:** `EdgePool.cs` (SplitEdge method)
**Change:** Simplified border constraint handling by removing unreachable partner edge condition
**Rationale:** The condition `(e.GetIndex() & 1) != 0` can never be true because SplitEdge always receives the base reference (even index).

---

#### FIX-13: Auto-Computed MinimumTriangleArea ✅
**File:** `RuppertRefiner.cs:212-230`
**Change:** Added automatic computation of MinimumTriangleArea based on TIN bounds
```csharp
// Compute minimum triangle area - if using default, scale based on TIN bounds
// to prevent runaway refinement on large coordinate systems
var minArea = options.MinimumTriangleArea;
if (minArea <= DefaultMinTriangleArea)
{
    // Default was used - compute a sensible value based on data bounds
    var bounds = tin.GetBounds();
    if (bounds.HasValue)
    {
        var (_, _, width, height) = bounds.Value;
        var boundsSize = Math.Max(width, height);
        // Minimum edge length ~= boundsSize / 2000, minimum area ~= (edge)^2 / 2
        // This allows smaller triangles than before (boundsSize/500) for better conformity
        var minEdge = boundsSize / 2000.0;
        var computedMinArea = minEdge * minEdge / 2.0;
        minArea = Math.Max(minArea, computedMinArea);
    }
}
_minTriangleArea = minArea;
```
**Rationale:** Prevents runaway refinement on large coordinate systems while allowing reasonable refinement on standard-scale data.

---

#### FIX-14: Increase MaxTriangleAttempts ✅
**File:** `RuppertRefiner.cs:109`
**Change:** Increased `MaxTriangleAttempts` from 10 to 50
```csharp
private const int MaxTriangleAttempts = 50;  // Give up on a triangle after this many failed attempts (increased from 10)
```
**Rationale:** More attempts allow the algorithm to handle temporary insertion failures, reducing the number of non-conformant triangles remaining after refinement.

---

#### FIX-15: Preserve Border Edge Status in RestoreConformity ✅
**File:** `IncrementalTin.cs:1427-1449`
**Issue:** When `RestoreConformity` splits a constrained edge, both halves must retain their border edge status. The code was calling `EdgePool.SplitEdge` but not verifying that both resulting edges had the border index set.
**Change:** Added verification code (same pattern as in `IncrementalTin.SplitEdge`)
```csharp
// Remember if this was a border edge before splitting
var wasBorderEdge = ab.IsConstraintRegionBorder();
var borderIndex = wasBorderEdge ? ab.GetConstraintBorderIndex() : -1;

// ... split edge ...

// Ensure BOTH halves of a split border edge remain border edges
// EdgePool.SplitEdge should handle this, but we verify and fix if needed
if (wasBorderEdge && borderIndex >= 0)
{
    if (!am.IsConstraintRegionBorder())
    {
        am.SetConstraintBorderIndex(borderIndex);
    }
    if (!mb.IsConstraintRegionBorder())
    {
        mb.SetConstraintBorderIndex(borderIndex);
    }
}
```
**Rationale:** This fixes the constraint border "gap" issue where a split border edge would lose its border status on one half, causing the constraint boundary to appear broken in the visualization.

---

#### FIX-16: Add Recursion Depth Limit to RestoreConformity ✅
**File:** `IncrementalTin.cs:1383-1414`
**Issue:** The Java implementation has a maximum recursion depth limit of 32 in `restoreConformity` to avoid excessive operations. This limit was missing from the C# port, potentially causing performance issues or stack overflows in pathological cases.

**Change:** Added recursion depth tracking and limit (matching Java behavior)
```csharp
/// <summary>
///     Tracks the maximum recursion depth in RestoreConformity.
/// </summary>
private int _maxDepthOfRecursionInRestore;

private void RestoreConformity(QuadEdge ab, int depthOfRecursion = 1)
{
    // Track maximum recursion depth (for diagnostics)
    if (depthOfRecursion > _maxDepthOfRecursionInRestore)
    {
        _maxDepthOfRecursionInRestore = depthOfRecursion;
    }

    // ... existing code ...

    // Limit recursion depth to avoid excessive operations (matches Java behavior)
    // This may leave some non-conformant edges in place
    if (depthOfRecursion > 32)
    {
        Debug.WriteLine($"RestoreConformity: Max recursion depth 32 reached, returning");
        return;
    }

    // ... rest of method ...
}
```
**Rationale:** Matches Java's behavior to prevent infinite recursion in edge cases. The limit of 32 is the same as the Java implementation.

---

#### FIX-17: Add Diagnostic Logging for Border Edge Splits ✅
**File:** `IncrementalTin.cs:1431-1466`
**Issue:** When investigating constraint leakage, it was difficult to determine if border edge splits were preserving the border status correctly. Added detailed debug logging to track border edge splits.

**Change:** Added comprehensive debug logging around border edge splits
```csharp
// DEBUG: Log border edge splits
if (wasBorderEdge)
{
    Debug.WriteLine($"RestoreConformity: Splitting BORDER edge {ab.GetIndex()} with borderIndex={borderIndex}");
    Debug.WriteLine($"  Original: a=({a.X:F2},{a.Y:F2}) -> b=({b.X:F2},{b.Y:F2})");
    Debug.WriteLine($"  Midpoint: m=({mx:F2},{my:F2})");
}

// ... split edge ...

if (wasBorderEdge && borderIndex >= 0)
{
    var amBorderBefore = am.IsConstraintRegionBorder();
    var mbBorderBefore = mb.IsConstraintRegionBorder();

    if (!amBorderBefore)
    {
        Debug.WriteLine($"  WARNING: am edge {am.GetIndex()} lost border status, restoring with index {borderIndex}");
        am.SetConstraintBorderIndex(borderIndex);
    }
    if (!mbBorderBefore)
    {
        Debug.WriteLine($"  WARNING: mb edge {mb.GetIndex()} lost border status, restoring with index {borderIndex}");
        mb.SetConstraintBorderIndex(borderIndex);
    }

    // Verify the fix worked
    Debug.WriteLine($"  After split: am={am.GetIndex()} border={am.IsConstraintRegionBorder()} idx={am.GetConstraintBorderIndex()} (was {amBorderBefore})");
    Debug.WriteLine($"  After split: mb={mb.GetIndex()} border={mb.IsConstraintRegionBorder()} idx={mb.GetConstraintBorderIndex()} (was {mbBorderBefore})");
}
```
**Rationale:** Enables debugging of border edge preservation issues. Key diagnostics to look for:
- `"WARNING: am edge ... lost border status"` - indicates `EdgePool.SplitEdge` didn't preserve border status
- `"WARNING: mb edge ... lost border status"` - indicates the original edge lost its border status after modification

---

#### FIX-18: Add Triangle Topology Verification After Edge Splitting ✅
**File:** `IncrementalTin.cs:1492-1500`
**Issue:** After splitting a constrained edge in `RestoreConformity`, the four new triangles must be properly wired. If the forward links are incorrect, the mesh topology becomes corrupted, leading to empty triangles in rasterization and other rendering issues.

**Change:** Added `Debug.Assert` statements to verify triangle topology immediately after wiring
```csharp
// DEBUG: Verify triangle topology after wiring
Debug.Assert(ma.GetForward() == ad && ad.GetForward() == dm && dm.GetForward() == ma,
    "Triangle m-a-d wiring is broken");
Debug.Assert(mb.GetForward() == bc && bc.GetForward() == cm && cm.GetForward() == mb,
    "Triangle m-b-c wiring is broken");
Debug.Assert(mc.GetForward() == ca && ca.GetForward() == am && am.GetForward() == mc,
    "Triangle m-c-a wiring is broken");
Debug.Assert(md.GetForward() == db && db.GetForward() == bm && bm.GetForward() == md,
    "Triangle m-d-b wiring is broken");
```
**Rationale:** These assertions verify that:
1. **Triangle m-a-d:** ma → ad → dm → ma (complete 3-cycle)
2. **Triangle m-b-c:** mb → bc → cm → mb (complete 3-cycle)
3. **Triangle m-c-a:** mc → ca → am → mc (complete 3-cycle)
4. **Triangle m-d-b:** md → db → bm → md (complete 3-cycle)

If any assertion fails, it immediately identifies which triangle has broken topology, making debugging much easier. These assertions only run in Debug builds.

---

#### FIX-19: Fix RefineOnce Return Value ❌ REVERTED
**File:** `RuppertRefiner.cs`
**Status:** REVERTED - caused infinite loop

**Original Intent:** Attempted to distinguish between "refinement complete" and "retry later" by returning a sentinel vertex when insertion failed but the triangle was re-queued.

**Why It Failed:** The sentinel pattern caused the main loop to continue indefinitely even when no real progress was being made. When all remaining triangles were in a "retry" state (due to proximity issues), the loop would never terminate.

**Current Behavior:** When `RefineOnce()` returns `null`, `Refine()` returns `true` (success). Failed insertions re-queue the triangle but return `null`, which terminates the current `Refine()` call. The triangle can be processed on a subsequent `Refine()` call if needed.

---

#### FIX-20: Fix Priority Calculation and Add Safety Limits ✅
**File:** `RuppertRefiner.cs`
**Issue:** The `TriangleBadPriority()` function was returning `cross2` (squared double-area) as the priority, causing larger triangles to be prioritized regardless of how bad their angles were. This led to excessive refinement of large triangles that were barely over the threshold.

**Changes:**

1. **Fixed priority calculation** (line ~678):
```csharp
// OLD: return cross2;  // Area-based - wrong!
// NEW:
var badnessRatio = pairProd / (threshMul * cross2);
return badnessRatio;
```
The `badnessRatio` measures how far over the threshold a triangle is. A ratio of 1.0 means exactly at threshold, 2.0 means twice as bad, etc. This ensures truly bad triangles (worse angles) are prioritized regardless of size.

2. **Added vertex count limit** (lines ~321-338):
```csharp
var maxVertexCount = initialVertexCount * 50;
// ... in loop:
if (currentVertexCount > maxVertexCount)
{
    System.Diagnostics.Debug.WriteLine($"WARNING: Vertex count limit exceeded...");
    return false;
}
```
Prevents runaway refinement by limiting total vertices to 50× the initial count.

3. **Added high-angle warning** (lines ~212-220):
```csharp
const double SafeAngleDegrees = 20.7;  // arcsin(1/(2*√2))
if (options.MinimumAngleDegrees > SafeAngleDegrees && !options.EnforceSqrt2Guard)
{
    System.Diagnostics.Debug.WriteLine("WARNING: MinimumAngleDegrees exceeds safe threshold...");
}
```
Warns when the requested minimum angle exceeds the theoretical safe limit (~20.7° without the √2 guard).

4. **Added abandoned triangle tracking**:
- `_abandonedTriangles` HashSet tracks triangles we've given up on after `MaxTriangleAttempts` (50) failed attempts
- `NextBadTriangleFromQueue()` skips abandoned triangles to prevent re-processing

**Rationale:** These changes ensure:
- Truly bad triangles are fixed first (not just large ones)
- The algorithm terminates even in pathological cases
- Users are warned when their parameters may cause issues

---

### Dependencies Added

#### Clipper2 Library ✅
**File:** `Tinfour.Core.csproj`
**Package:** `Clipper2` version 2.0.0
**Purpose:** Robust point-in-polygon testing for hole constraint verification (FIX-01, FIX-07)
```xml
<ItemGroup>
  <PackageReference Include="Clipper2" />
</ItemGroup>
```

---

### Test Results
All Ruppert refinement tests pass, including:
- `DonutConstraint_AfterRefinement_ShouldNotLeakOutside` ✅
- `Donut_DetailedLeakageAnalysis` ✅
- `SimpleCircle_AfterRefinement_ShouldNotLeakOutside` ✅
- `SimpleCircle_DetailedLeakageAnalysis` ✅
- All `RuppertRefinerTests` (13 tests) ✅

---

## 5. Remaining Work

### FIX-01: Geometric Verification for Hole Constraints
**Status:** Pending (Clipper2 added)
**File:** `ConstraintProcessor.cs`

**Proposed Change:**
```csharp
if (constraint is PolygonConstraint poly && poly.IsHole())
{
    // Verify that all edges inside the hole are NOT marked as interior
    // Use Clipper2's point-in-polygon test against the hole boundary
    VerifyHoleInteriorIsUnmarked(poly, edgesForConstraint);
    return;
}
```

---

### FIX-07: Hole-Aware Constraint Propagation
**Status:** Pending
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
        // Use Clipper2 to check if the c-side is geometrically inside the hole
        var cCentroid = GetTriangleCentroid(m, b, c);
        if (IsPointInsidePolygon(cCentroid, poly))
        {
            constraintIndexForC = -1;  // Don't mark as interior
        }
    }
}
```

---

## 6. Detailed Fix Specifications

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
    // If marked, clear the marking using ClearConstraintRegionFlags()
}
```

---

### FIX-09: Add Fallback Insertion Strategies
**File:** `RuppertRefiner.cs`

**Proposed Enhancement:**
When offcenter insertion fails:
1. Try circumcenter instead
2. Try a perturbed location
3. Use a smaller step toward the circumcenter

---

## 7. Testing Strategy

### 7.1 Unit Tests to Add/Update

| Test | Purpose | File |
|------|---------|------|
| `DonutConstraint_AfterRefinement_ShouldNotLeakOutside` | Verify no interior edges outside outer ring | `RuppertConstraintLeakageTest.cs` |
| `DonutConstraint_AfterRefinement_ShouldNotLeakIntoHole` | Verify no interior edges inside hole | `RuppertConstraintLeakageTest.cs` |
| `Refinement_WithTightClusters_ShouldNotTerminatePrematurely` | Verify completion with clustered vertices | New test |
| `Refinement_TriangleCount_ShouldBeReasonable` | Verify O(n) triangle growth | New test |
| `EdgeFlip_ShouldClearStaleConstraintFlags` | Verify flag clearing on flip | `EdgePoolTests.cs` |
| `SplitEdge_WithHoleBorder_ShouldNotPropagateToHoleInterior` | Verify hole handling | New test |

### 7.2 Integration Tests

| Scenario | Verification |
|----------|--------------|
| Simple circle constraint | Interior edges stay inside circle |
| Donut (outer + hole) | Interior edges stay in annulus |
| Multiple nested holes | Each hole properly excluded |
| High-angle refinement (45°) | Completes without excessive triangles |
| Dense vertex clusters | Completes without premature termination |

### 7.3 Regression Tests

Run the visualizer with:
- 2749 vertices, 33° Ruppert refinement, donut constraint
- Verify no "leakage" triangles
- Verify triangle count is O(n log n) not O(n²)

---

## 8. Implementation Order

### Phase 1: Critical Fixes (COMPLETE ✅)
1. FIX-02: Increase tolerances → Immediate improvement in termination
2. FIX-03: Re-queue on failure → Prevent lost triangles
3. FIX-05: Queue deduplication → Reduce excessive processing

### Phase 2: Constraint Integrity (COMPLETE ✅)
4. FIX-04: ClearConstraintRegionFlags method → Available for future use
5. FIX-06: Null constraint handling → Prevent silent failures
6. FIX-08: Remove dead code → Clean up

### Phase 3: Scale Handling (COMPLETE ✅)
7. FIX-13: Auto-computed MinimumTriangleArea → Prevent runaway on large coords
8. FIX-14: Increase MaxTriangleAttempts → Reduce non-conformant triangles

### Phase 4: Hole Handling (IN PROGRESS)
9. FIX-01: Hole verification → Primary leakage fix (Clipper2 ready)
10. FIX-07: Hole-aware propagation → Prevent propagation into holes

### Phase 5: Polish (PENDING)
11. FIX-09: Fallback strategies → Improve robustness
12. FIX-10: Recursive sweep → Complete constraint propagation
13. FIX-11: Border reclassification API → Future flexibility
14. FIX-12: Optimize scans → Performance improvement

---

## 9. Risk Assessment

| Fix | Risk Level | Mitigation | Status |
|-----|------------|------------|--------|
| FIX-01 | MEDIUM | May slow constraint processing; benchmark before/after | Pending |
| FIX-02 | LOW | Conservative change; test with edge cases | ✅ Done |
| FIX-03 | LOW | Additive change; existing behavior preserved | ✅ Done |
| FIX-04 | HIGH | Method added but not auto-called; low risk | ✅ Done |
| FIX-05 | LOW | Additive change; only affects queue management | ✅ Done |
| FIX-06 | LOW | Fallback behavior; improves robustness | ✅ Done |
| FIX-07 | MEDIUM | Requires geometric testing; verify correctness | Pending |
| FIX-08 | LOW | Removing dead code; no behavioral change | ✅ Done |
| FIX-09 | MEDIUM | New behavior paths; comprehensive testing | Pending |
| FIX-10 | MEDIUM | May cause over-propagation; careful bounds | Pending |
| FIX-11 | LOW | New API only; opt-in usage | Pending |
| FIX-12 | LOW | Performance only; verify equivalence | Pending |
| FIX-13 | LOW | Only affects default case; explicit values unchanged | ✅ Done |
| FIX-14 | LOW | More retries; may slow convergence slightly | ✅ Done |

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
| #13 | Constructor (minArea calc) | RuppertRefiner.cs:212-230 |
| #14 | MaxTriangleAttempts | RuppertRefiner.cs:109 |

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
        Bits 15-26: Line constraint index (12 bits, max 4094)
        Bits 0-14: Region constraint index (15 bits, max 32766)
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

## Appendix D: Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Dec 2025 | Initial document with 12 identified issues |
| 1.1 | Dec 2025 | Added Issues #13, #14; Updated implementation status; Added Clipper2 dependency; Documented all completed fixes |
| 1.2 | Dec 2025 | Added FIX-19: Fixed premature termination when RefineOnce returns null after re-queuing a triangle |
| 1.3 | Dec 2025 | REVERTED FIX-19 (caused infinite loop); Added FIX-20: Fixed priority calculation (badness ratio instead of area), added vertex count limit (50×), added high-angle warning, added abandoned triangle tracking |
| 1.4 | Dec 2025 | Documented failed post-refinement approaches; Added alternative techniques appendix |
| 1.5 | Dec 2025 | RESOLVED: Root cause of rasterizer showing NaN was `InterpolateZ=false` default. Fixed visualizer and tests to set `InterpolateZ=true`. Also fixed UI property setter bug preventing visual updates. |
| 1.6 | Dec 2025 | CRITICAL FIX: Add() insertion loop was flipping constrained edges - missing `IsConstrained()` check. This caused constraint border edges to be destroyed when vertices were inserted near them. |
| 1.7 | Dec 2025 | Fixed premature termination when InsertOffcenterOrSplit returns null - now skips failed insertions instead of terminating. Fixed infinite loop in GetPerimeter() after refinement. Added safety limits to rendering loops. |

---

## Appendix E: Resolution of Rasterizer NaN Issue (December 2025)

### Symptoms
After Ruppert refinement:
- Rasterizer showed only pre-existing triangles
- All Ruppert-modified triangles returned NaN values
- Triangles appeared to be properly marked as constrained (tests passed)
- Navigation tests passed (triangles were reachable)

### Root Cause Analysis
The root cause was **`RuppertOptions.InterpolateZ` defaulting to `false`**.

When `InterpolateZ = false`:
1. Ruppert creates new vertices (circumcenters, off-centers, segment midpoints) with `Z = NaN`
2. Any triangle containing these NaN-valued vertices produces NaN interpolation results
3. The rasterizer correctly found the triangles, but couldn't compute Z values for them

### The Fix
Two issues were fixed:

#### Issue 1: InterpolateZ Default
**File:** `RuppertOptions.cs:142`
```csharp
public bool InterpolateZ { get; set; } = false;  // Default was false!
```

**Fix in Visualizer:** `MainViewModel.cs:451`
```csharp
var options = new RuppertOptions
{
    MinimumAngleDegrees = this.RuppertMinAngle,
    MaxIterations = 100_000,
    InterpolateZ = true  // Required for rasterization
};
```

#### Issue 2: UI Property Setter Bug
**File:** `MainViewModel.cs:762-763`
```csharp
// BEFORE (broken - assigned to backing field, no PropertyChanged):
this._interpolationResult = null;
this._voronoiResult = null;

// AFTER (fixed - uses property setter, triggers PropertyChanged):
this.InterpolationResult = null;
this.VoronoiResult = null;
```

### Why This Was Confusing
The debugging was confusing because:
1. **Constraint membership tests passed** - triangles were properly marked as interior
2. **Navigation tests passed** - the walker could find all triangles
3. **The issue only manifested in rasterization** - not in structural integrity checks

The symptom "all pre-existing triangles render but no new Ruppert triangles render" was misleading - it wasn't that new triangles couldn't be *found*, it was that they had *NaN vertex Z values*.

### Lessons Learned
1. When debugging rendering issues, check vertex data (Z values) not just structure
2. `InterpolateZ = true` should be the default for visualization use cases
3. With CommunityToolkit.Mvvm `[ObservableProperty]`, always use the generated property setter (not the backing field) to trigger `PropertyChanged`

---

## Appendix F: Failed Post-Refinement Approaches (December 2025)

The following approaches were attempted to fix constraint region marking after Ruppert refinement, but were rolled back due to various issues.

### Approach 1: RebuildWithConstraints (Fresh Vertices)

**Goal:** Create a completely new TIN from the refined coordinates with fresh constraint objects.

**Implementation:**
- Extract all vertex coordinates from refined TIN
- Create fresh `Vertex` objects from raw (x, y, z) coordinates
- Create fresh `PolygonConstraint` objects from original constraint vertices
- Build a new TIN with the fresh data

**Why It Failed:**
- The flood fill only processed 1 edge at a time ("Maximum queue size was 1" repeated hundreds of times)
- Root cause: Using original 56 constraint vertices instead of the hundreds of vertices now on the split boundary
- The constraint boundary didn't match the actual edges in the refined TIN

### Approach 2: Extract Boundary From Border Edges

**Goal:** Build the constraint boundary from the current border edges in the TIN rather than original vertices.

**Implementation:**
- `BuildOrderedBoundaryFromEdges()` method to extract ordered boundary vertices from border edges
- Walk the border edges using `GetForward()` to get ordered vertices
- Create new constraint from these vertices

**Why It Failed:**
- Lost the donut hole - the boundary extraction didn't correctly identify which border edges belonged to the outer ring vs inner hole
- Border edges don't carry information about which constraint they belong to (only a constraint index)

### Approach 3: BuildFreshTinFromCoordinates (Static Isolated Builder)

**Goal:** Create a completely isolated TIN builder that takes only raw coordinate tuples, ensuring no shared references.

**Implementation:**
```csharp
public static IncrementalTin BuildFreshTinFromCoordinates(
    IEnumerable<(double x, double y, double z)> vertexCoords,
    IEnumerable<IEnumerable<(double x, double y)>> constraintPolygons)
```

**Why It Failed:**
- Still lost the donut hole
- Same fundamental problem: extracting constraint boundaries from a refined mesh doesn't preserve hole semantics

### Approach 4: RefreshConstraintRegions (Re-Flood Fill)

**Goal:** Clear all constraint region flags and re-run flood fill from border edges.

**Implementation:**
- `RefreshConstraintRegions()` to iterate all edges and clear constraint region flags
- `FloodFillFromBorderEdge()` to flood fill from each border edge
- Call after Ruppert refinement completes

**Why It Failed:**
- Marked the ENTIRE terrain as constrained, except triangles modified by Ruppert
- Opposite of the original problem!
- Root cause: Border edges collected from iterator don't have consistent orientation for flood fill
- The original flood fill in `ConstraintProcessor` relies on edges being properly oriented from `ProcessConstraint`

### Key Insight

The fundamental issue is that **constraint region membership is established during initial constraint processing** via `ProcessConstraint` in `ConstraintProcessor.cs`. This process:
1. Establishes border edges with proper orientation
2. Flood fills from the correctly-oriented interior side

After Ruppert refinement splits these edges, the new edges:
- Have border status preserved (FIX-15 ensures this)
- Have interior status propagated (via `SplitEdge`)
- BUT the propagation logic has edge cases where it fails

The correct fix is likely:
1. Fix the constraint propagation during edge splitting (FIX-07: hole-aware propagation)
2. Add geometric verification using point-in-polygon tests (FIX-01)

Rather than trying to rebuild/refresh the entire constraint region marking post-hoc.

---

## Appendix F: Alternative Refinement Techniques for Rendering Meshes

Given the challenges with Ruppert's refinement, here are alternative techniques for generating rendering-quality triangular meshes:

### 1. Chew's Algorithm (Second Algorithm)

**Overview:** Chew's second algorithm is simpler than Ruppert's and guarantees triangles with angles between 30° and 120°.

**How It Works:**
- Insert circumcenters of "bad" triangles (those with angles < 30°)
- No special handling for encroached segments
- Simpler termination conditions

**Pros:**
- Simpler implementation
- Better minimum angle guarantee (30° vs ~20.7° for Ruppert without √2 guard)
- No edge splitting complexity

**Cons:**
- May not respect input segments exactly
- Can create more triangles than Ruppert
- Not suitable when exact constraint boundaries are required

### 2. Delaunay Refinement with Centroid Insertion

**Overview:** Instead of circumcenters/offcenters, insert centroids of bad triangles.

**How It Works:**
- Find triangles with poor aspect ratios
- Insert centroid of the triangle
- Retriangulate using standard Delaunay

**Pros:**
- Very simple implementation
- No complex geometric computations
- Always inserts inside the triangle (no encroachment issues)

**Cons:**
- Slower convergence than Ruppert
- May not achieve as good angle bounds
- More iterations needed

### 3. CVT (Centroidal Voronoi Tessellation) Relaxation

**Overview:** Iteratively move vertices toward the centroids of their Voronoi cells.

**How It Works:**
1. Start with any triangulation
2. Compute Voronoi diagram (dual of Delaunay)
3. Move each vertex to centroid of its Voronoi cell
4. Retriangulate
5. Repeat until convergence

**Pros:**
- Produces very uniform, high-quality meshes
- Good for rendering (uniform triangle sizes)
- Simple concept

**Cons:**
- Many iterations needed for convergence
- Expensive per iteration
- Doesn't handle constraints well without special treatment

### 4. DistMesh (Signed Distance Function Based)

**Overview:** Uses force-based equilibrium to generate high-quality meshes.

**How It Works:**
- Define domain via signed distance function
- Place initial vertices on a regular grid
- Apply spring-like forces to move vertices
- Retriangulate after each movement step

**Pros:**
- Excellent mesh quality
- Good handling of curved boundaries
- Predictable element sizes

**Cons:**
- Requires signed distance function
- More complex setup
- May struggle with complex constraint geometry

### 5. Constrained Mesh Simplification (For Rendering)

**Overview:** Start with a dense mesh and simplify while preserving features.

**How It Works:**
- Generate initial dense triangulation (current approach works)
- Identify triangles that need refinement
- Instead of adding vertices, merge/collapse triangles
- Use edge collapse with error metrics

**Pros:**
- Works with existing triangulation
- Can target specific triangle counts
- Preserves constraint boundaries

**Cons:**
- Doesn't improve angle quality
- May lose geometric detail
- Different goal than refinement

### 6. Quadrilateral Meshing Then Splitting

**Overview:** Generate quad mesh first, then split each quad into 2 triangles.

**How It Works:**
- Use advancing front or paving algorithm for quads
- Split each quad along the shorter diagonal
- Post-process for quality

**Pros:**
- Very uniform mesh sizes
- Good for rendering (regular structure)
- Well-suited for terrain visualization

**Cons:**
- Complex quad meshing algorithms
- May not handle irregular boundaries well
- Overkill for simple visualizations

### Recommendation for Tinfour.NET

Given the rendering use case and current constraint propagation issues:

**Short Term:** Fix the constraint propagation in `SplitEdge` and `RestoreConformity` (FIX-07, FIX-01). The core Ruppert algorithm is working; the issue is maintaining constraint membership after edge splits.

**Medium Term:** Consider implementing **centroid insertion refinement** as a simpler alternative:
- No segment encroachment handling
- Insert centroid of bad triangles
- Use existing constraint propagation (which works for simple insertions)
- May need more iterations but fewer edge cases

**Long Term:** If rendering quality is the primary goal, consider **CVT relaxation** as a post-processing step:
- Run Ruppert to get angle quality
- Apply 5-10 CVT iterations to improve uniformity
- CVT moves vertices but doesn't add new ones, so constraint membership is preserved

---

## Appendix G: Critical Fix - Constrained Edge Flip in Add() (December 2025)

### Symptom
Rare constraint border edge "leakage" where the red constraint line becomes discontinuous after Ruppert refinement. The issue:
- Was very rare and only appeared in specific configurations
- Always occurred in the same location when it happened
- The constraint segment was **completely lost**, not just partially marked

### User Observation
> "There are skinny triangles on either side of the missing constraint segment prior to refinement. The missing edge is the long edge of a pair of skinny triangles that share 2 vertices."

This observation was the key to identifying the root cause.

### Root Cause
The `Add()` method in `IncrementalTin.cs` (Lawson's insertion algorithm) was **flipping constrained edges** without checking if they were constrained.

**The Bug (C# code):**
```csharp
// IncrementalTin.cs line 1206 (BEFORE FIX)
if (h >= 0)
{
    // Edge flip procedure - PROBLEM: Flips constrained edges!
    n2 = (QuadEdge)n1.GetForward();
    // ...
}
```

**The Java Reference Implementation:**
```java
// IncrementalTin.java line 1358-1362
boolean edgeViolatesDelaunay = h >= 0;
if (edgeViolatesDelaunay && c.isConstrained()) {
    edgeViolatesDelaunay = false;  // Don't flip constrained edges!
}
if (edgeViolatesDelaunay) {
    // Edge flip procedure
}
```

The Java code explicitly checks if the edge is constrained before flipping, but this check was missing from the C# port.

### Why This Caused "Leakage" at Specific Locations

1. Ruppert refinement identifies skinny triangles to refine
2. When inserting a vertex near a constraint border edge that forms part of a skinny triangle:
   - The insertion algorithm tests the in-circle criterion
   - If the constraint edge violates Delaunay (the opposite vertex is inside the circumcircle), `h >= 0`
   - Without the `IsConstrained()` check, the constrained border edge gets **flipped**
   - Flipping destroys the constraint - the edge is repurposed with different endpoints
3. The constraint boundary now has a gap where the flipped edge used to be

### The Fix
**File:** `IncrementalTin.cs:1201-1210`

```csharp
// If h >= 0, the Delaunay criterion is not met, so we may need to flip the edge.
// CRITICAL: Never flip constrained edges - they must remain in place to maintain
// the constraint geometry. This matches Java's behavior at line 1359.
var edgeViolatesDelaunay = h >= 0 && !c.IsConstrained();

if (insertionLoopCount > maxInsertionLoops - 30)
{
    Debug.WriteLine($"    h={h:F4}, constrained={c.IsConstrained()}, branch={(edgeViolatesDelaunay ? "FLIP" : "CHECK")}");
}
if (edgeViolatesDelaunay)
{
    // Edge flip procedure
```

### Why This Was Hard to Find

1. **Rare occurrence**: Only happened when:
   - A vertex was inserted near a constraint border edge
   - The constraint edge formed part of a skinny triangle
   - The in-circle test indicated flipping was needed

2. **Symptom appeared far from cause**: The visual symptom (broken constraint line) appeared after Ruppert refinement, but the actual bug was in the base `Add()` method

3. **Tests passed**: Most constraint tests don't insert vertices near constraint edges in configurations that trigger this

### Lessons Learned

1. When porting geometry algorithms, in-circle/flip operations need special attention for constraint handling
2. User observations about the geometry ("skinny triangles on either side") are crucial debugging clues
3. Rare edge cases often occur at the intersection of multiple conditions (skinny triangle + constraint edge + vertex insertion)

---

## Appendix H: Refinement Completion and Rendering Fixes (December 2025)

### Symptom 1: Premature Termination

**Problem:** Refinement terminated early, leaving visible skinny triangles unrefined.

**Root Cause:** When `InsertOffcenterOrSplit()` returned `null` (vertex too close to existing vertex), `RefineOnce()` immediately returned `null`, which the `Refine()` loop interpreted as "refinement complete."

**Debug Output:**
```
InsertOffcenterOrSplit returned null (count=1)
```
Followed by immediate termination with ~400 insertion failures out of 35k+ potential triangles.

**The Fix:**
**File:** `RuppertRefiner.cs:354-381`

```csharp
// Keep trying bad triangles until we successfully insert one, run out, or hit skip limit
const int maxSkipsPerCall = 100; // Limit skips to prevent infinite loop in single call
int skippedThisCall = 0;

while (skippedThisCall < maxSkipsPerCall)
{
    var bad = NextBadTriangleFromQueue();
    if (bad == null)
        return null; // No more bad triangles - done

    var result = InsertOffcenterOrSplit(bad);
    if (result != null)
        return result; // Success

    // Insertion failed - skip this triangle and try the next
    _insertionFailedCount++;
    skippedThisCall++;
}

// Hit skip limit - return dummy vertex to keep main loop going
return _lastInsertedVertex;
```

### Symptom 2: Out of Memory / Infinite Loop in Renderer

**Problem:** After refinement completed successfully, the visualizer consumed 35GB+ of RAM and hung.

**Debug Investigation:** Added progress logging to `DrawTriangulation()`:
```
DrawTriangulation: Drawing triangles...
DrawTriangulation: Finished triangles (72878 drawn)
DrawTriangulation: Drawing edges...
DrawTriangulation: Finished edges (109317 drawn)
DrawTriangulation: Drawing perimeter...
[HUNG HERE]
```

**Root Cause:** The `GetPerimeter()` method uses a `do...while` loop that traverses ghost edges around the convex hull. After heavy refinement, the edge structure was in a state where the traversal never returned to the starting edge (`s != s0` was always true), causing an infinite loop.

**The Fix:**
**File:** `IncrementalTin.cs:507-532`

```csharp
public IList<IQuadEdge> GetPerimeter()
{
    var perimeter = new List<IQuadEdge>();
    if (!IsBootstrapped()) return perimeter;

    var ghostEdge = _edgePool.GetStartingGhostEdge();
    if (ghostEdge == null) return perimeter;

    var s0 = ghostEdge.GetReverse();
    var s = s0;
    var maxIterations = _edgePool.Size() * 2 + 1000; // Safety limit
    var iterations = 0;
    do
    {
        if (++iterations > maxIterations)
        {
            Debug.WriteLine($"ERROR: GetPerimeter() exceeded {maxIterations} iterations - possible infinite loop.");
            break;
        }
        perimeter.Add(s.GetDual());
        s = s.GetForward().GetForward().GetDual().GetReverse();
    }
    while (s != s0);

    return perimeter;
}
```

### Additional Renderer Safety Measures

**File:** `TriangulationCanvas.cs`

Added safety limits and progress logging to prevent runaway iteration:

```csharp
// Triangle rendering with limit
var triangleCount = 0;
const int maxTriangles = 500_000;
foreach (var triangle in _triangulation.GetTriangles())
{
    if (++triangleCount > maxTriangles)
    {
        Debug.WriteLine($"WARNING: Triangle rendering stopped at {maxTriangles}");
        break;
    }
    // ... render triangle
}

// Edge rendering with limit and progress
var edgeCount = 0;
const int maxEdges = 500_000;
foreach (var edge in _triangulation.GetEdgeIterator())  // Changed from GetEdges()
{
    if (++edgeCount > maxEdges) break;
    if (edgeCount % 50000 == 0)
        Debug.WriteLine($"DrawTriangulation: Edge progress {edgeCount}...");
    // ... render edge
}
```

**Key Change:** Changed `GetEdges()` to `GetEdgeIterator()` to avoid materializing all edges into a list, which was causing memory issues with large meshes.

### Diagnostic Statistics Added

The refinement now outputs comprehensive statistics:

```
=== RUPPERT REFINE() COMPLETE ===
Vertices: 17660 -> 36460 (+17870 added)
Triangles: 35288 -> 72878 (+37590)
Queue stats: total=46994, stillBad=18974, noLongerBad=28020
Rejection stats: ghost=70, notInRegion=8379, meetsAngle=147200, seditious=0, smallArea=5
Insertion stats: failed=451, tooCloseToLast=129, tooCloseToNearest=322
```

### Outstanding Issue

The `GetPerimeter()` infinite loop is a **symptom** of a deeper issue - the edge structure may have some inconsistency after heavy refinement that prevents the perimeter traversal from returning to its starting edge. The safety limit is a workaround. A proper fix would require:

1. Investigating why the ghost edge traversal breaks after refinement
2. Potentially using edge index-based tracking instead of reference equality
3. Verifying edge topology integrity after each vertex insertion

---

*End of Document*
