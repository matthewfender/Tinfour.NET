# Perimeter Topology Issue - Constraints Sharing Vertices with Perimeter

**Document Version:** 1.0
**Date:** December 18, 2025
**Author:** Claude Code
**Status:** Investigation In Progress
**Related Documents:**
- [RUPPERT_REFINEMENT_FIX_PLAN.md](RUPPERT_REFINEMENT_FIX_PLAN.md)
- [RUPPERT_REFINEMENT_ADDENDUM.md](RUPPERT_REFINEMENT_ADDENDUM.md)

---

## Executive Summary

There is a topology corruption issue that manifests when **polygon constraints share vertices with the TIN perimeter** (convex hull). This is a common scenario when triangulating features like water bodies where the constraint polygon touches the edge of the data extent.

The issue causes:
1. **Contour traversal timeouts** - The contour builder cannot circumnavigate the perimeter
2. **GetPerimeter() infinite loops** - The ghost edge traversal never completes
3. **Downstream analysis failures** - Any operation requiring perimeter traversal fails

The problem may exist:
- After adding constraints (before refinement)
- After Ruppert's refinement (confirmed)
- Or both

Previous fixes addressed constraint leakage but may have introduced or exposed this perimeter topology issue. Per advice from the Java author, the constraint propagation fix may be correct and Ruppert's implementation needs adjustment.

---

## Problem Description

### Symptom 1: Contour Builder Timeout

When generating contours on a TIN where constraints share vertices with the perimeter, the `ContourBuilderForTin` times out or hangs. The contour traversal code cannot complete a loop around the perimeter.

**Location:** `ContourBuilderForTin.cs` - perimeter link traversal

### Symptom 2: GetPerimeter() Infinite Loop

The `GetPerimeter()` method uses a do-while loop that traverses ghost edges:

```csharp
// IncrementalTin.cs:521-534
do
{
    if (++iterations > maxIterations)
    {
        // Safety limit hit - topology corrupted
        throw new InvalidOperationException(...);
    }
    perimeter.Add(s.GetDual());
    s = s.GetForward().GetForward().GetDual().GetReverse();
}
while (s != s0);
```

When topology is corrupted, `s != s0` is always true, causing infinite iteration.

### Root Cause Hypothesis

The issue appears to occur when:
1. A polygon constraint vertex coincides with a convex hull (perimeter) vertex
2. Edge operations (split, flip) during constraint processing or refinement corrupt the ghost edge linkage
3. The ghost edge traversal path becomes discontinuous

---

## Affected Scenarios

### Common Use Case: Water Body Constraints

```
     Perimeter (Convex Hull)
    /                        \
   *--------------------------*
   |                          |
   |    * * * * * * * *       |  <- Water body constraint
   |   *               *      |     shares vertex with perimeter
   |  *                 *     |
   | *                   *----*  <- SHARED VERTEX
   |  *                 *     |
   |   *               *      |
   |    * * * * * * * *       |
   |                          |
   *--------------------------*
```

When a water body polygon touches the edge of the survey area, constraint vertices coincide with perimeter vertices.

---

## Technical Analysis

### Ghost Edge Structure

The TIN uses ghost triangles to represent the infinite exterior. Each perimeter edge has a dual ghost edge connecting to the "ghost vertex" (null vertex):

```
        Real Vertex A -------- Real Vertex B
               \                    /
                \   Ghost Edge     /
                 \       |        /
                  \      |       /
                   \     |      /
                    Ghost Vertex (null)
```

### Perimeter Traversal Pattern

The Java implementation (line 1683-1689):
```java
do {
    pList.add(s.getDual());
    s = s.getForward();
    s = s.getForward();
    s = s.getDual();
    s = s.getReverse();
} while (s != s0);
```

This traverses: forward → forward → dual → reverse

The C# implementation (line 532):
```csharp
s = s.GetForward().GetForward().GetDual().GetReverse();
```

These should be equivalent, but corruption of any link in the chain breaks traversal.

### Potential Corruption Points

1. **EdgePool.SplitEdge()** - When splitting a perimeter edge
2. **RestoreConformity()** - When restoring Delaunay after splits
3. **Add() vertex insertion** - When inserting near perimeter edges
4. **ProcessConstraint()** - When constraint edge coincides with perimeter edge

---

## Investigation Plan

### Phase 1: Create Minimal Reproduction

Create the simplest possible TIN configuration that exhibits the issue:

```csharp
// Minimal test case: Square with constraint sharing one perimeter edge
var vertices = new List<Vertex>
{
    new(0, 0, 0, 0),  // Perimeter vertex 0
    new(10, 0, 0, 1), // Perimeter vertex 1 - shared with constraint
    new(10, 10, 0, 2),// Perimeter vertex 2
    new(0, 10, 0, 3), // Perimeter vertex 3
    new(5, 5, 0, 4),  // Interior vertex
};

// Constraint shares vertices 1 and 2 with perimeter
var constraintVertices = new List<Vertex>
{
    new(10, 0, 0, 100),   // Same location as vertex 1
    new(10, 10, 0, 101),  // Same location as vertex 2
    new(7, 5, 0, 102),    // Interior constraint vertex
};
```

### Phase 2: Validate Perimeter Before and After Operations

Add validation that checks:
1. Ghost edge count matches perimeter edge count
2. Perimeter traversal completes in expected iterations
3. Perimeter forms a closed loop (area > 0)
4. All ghost edges connect to null vertex

### Phase 3: Identify Corruption Point

Add instrumentation to track:
- When perimeter edges are split
- When constraint edges coincide with perimeter
- Forward/reverse link integrity after each operation

---

## Proposed Unit Tests

### Test 1: Basic Perimeter Integrity

```csharp
[Fact]
public void Perimeter_AfterTriangulation_ShouldBeTraversable()
{
    // Simple TIN with no constraints
    var tin = CreateSimpleTin();

    var perimeter = tin.GetPerimeter();

    Assert.True(perimeter.Count >= 3);
    Assert.True(CalculatePerimeterArea(perimeter) > 0);
}
```

### Test 2: Perimeter with Coincident Constraint Vertex

```csharp
[Fact]
public void Perimeter_WithConstraintSharingVertex_ShouldBeTraversable()
{
    // TIN where constraint vertex coincides with perimeter vertex
    var tin = CreateTinWithPerimeterConstraint();

    var perimeter = tin.GetPerimeter();

    Assert.True(perimeter.Count >= 3);
    Assert.True(CalculatePerimeterArea(perimeter) > 0);
}
```

### Test 3: Perimeter After Refinement

```csharp
[Fact]
public void Perimeter_AfterRuppertRefinement_ShouldBeTraversable()
{
    var tin = CreateTinWithPerimeterConstraint();

    var refiner = new RuppertRefiner(tin, options);
    refiner.Refine();

    var perimeter = tin.GetPerimeter();

    Assert.True(perimeter.Count >= 3);
    Assert.True(CalculatePerimeterArea(perimeter) > 0);
}
```

### Test 4: Perimeter Link Continuity

```csharp
[Fact]
public void Perimeter_ForwardReverseLinks_ShouldBeConsistent()
{
    var tin = CreateTinWithPerimeterConstraint();

    foreach (var edge in tin.GetPerimeter())
    {
        var dual = edge.GetDual();
        Assert.True(dual.GetB().IsNullVertex()); // Ghost side

        // Verify forward/reverse consistency
        var next = dual.GetForward();
        Assert.Equal(dual, next.GetReverse());
    }
}
```

---

## Helper Methods for Validation

```csharp
/// <summary>
/// Validates perimeter topology integrity.
/// </summary>
public static class PerimeterValidator
{
    public static bool ValidatePerimeter(IIncrementalTin tin, out string error)
    {
        error = string.Empty;

        if (!tin.IsBootstrapped())
        {
            error = "TIN is not bootstrapped";
            return false;
        }

        // Count ghost edges
        var ghostEdgeCount = 0;
        foreach (var edge in tin.GetEdges())
        {
            if (edge.GetB().IsNullVertex())
                ghostEdgeCount++;
        }

        // Traverse perimeter
        var perimeter = new List<IQuadEdge>();
        var ghostEdge = GetStartingGhostEdge(tin);
        if (ghostEdge == null)
        {
            error = "No starting ghost edge found";
            return false;
        }

        var s0 = ghostEdge.GetReverse();
        var s = s0;
        var iterations = 0;
        var maxIterations = ghostEdgeCount * 2 + 100;

        do
        {
            if (++iterations > maxIterations)
            {
                error = $"Perimeter traversal exceeded {maxIterations} iterations - infinite loop detected";
                return false;
            }

            perimeter.Add(s.GetDual());
            s = s.GetForward().GetForward().GetDual().GetReverse();
        }
        while (s != s0);

        // Verify count matches
        if (perimeter.Count != ghostEdgeCount)
        {
            error = $"Perimeter edge count ({perimeter.Count}) doesn't match ghost edge count ({ghostEdgeCount})";
            return false;
        }

        // Verify positive area (CCW winding)
        var area = CalculateSignedArea(perimeter);
        if (area <= 0)
        {
            error = $"Perimeter has non-positive area ({area}) - wrong winding or degenerate";
            return false;
        }

        return true;
    }

    private static double CalculateSignedArea(List<IQuadEdge> perimeter)
    {
        var sum = 0.0;
        foreach (var edge in perimeter)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2.0;
    }
}
```

---

## Known Issues from Previous Work

### GetPerimeter() Safety Limit (RUPPERT_REFINEMENT_FIX_PLAN Appendix H)

A safety limit was added to prevent infinite loops:
```csharp
var maxIterations = _edgePool.Size() * 2 + 1000;
```

This is a workaround, not a fix. The root cause needs investigation.

### Constraint Leakage at High Angles (RUPPERT_REFINEMENT_ADDENDUM)

When running Ruppert refinement with angles 25°+, constraints can "leak". This may be related to perimeter topology issues when constraints touch the perimeter.

### PropagateConstraintRegionMembership Issues

The Java `restoreConformity` does NOT perform constraint region propagation - only topology operations. The C# implementation added propagation which may cause issues.

---

## Next Steps

1. **Create minimal reproduction test** - Simplest case that exhibits the issue
2. **Add perimeter validation** - `PerimeterValidator` class with comprehensive checks
3. **Instrument edge operations** - Log when perimeter edges are modified
4. **Compare with Java** - Verify edge-by-edge that operations match
5. **Fix root cause** - Once identified, implement proper fix
6. **Verify downstream** - Ensure contour generation works after fix

---

## Test File Locations

- `Tinfour.Core.Tests/Topology/PerimeterTopologyTests.cs` (to be created)
- `Tinfour.Core.Tests/Constraints/PerimeterConstraintTests.cs` (to be created)

---

## Reproduction Found

### Test Case: `ContourBuilder_LargeTinWithPerimeterConstraint_ShouldComplete`

**Location:** `Tinfour.Core.Tests/Topology/PerimeterTopologyTests.cs`

**Configuration:**
- 200+ random vertices in 100x100 area
- Corner vertices at (0,0), (100,0), (100,100), (0,100)
- Polygon constraint touching perimeter: vertices at (100,40), (100,60), (90,60), (85,50), (90,40)
- Ruppert refinement with 25° minimum angle

**Result:**
```
Vertices before Ruppert: 219
Vertices after Ruppert: 298
Perimeter valid: False
Error: GetPerimeter() exceeded 2780 iterations - ghost edge topology is corrupted
```

**Key Finding:** The perimeter topology is corrupted **after** Ruppert refinement, not during initial constraint processing. The constraint with vertices on the perimeter edge causes ghost edge linkage to break during refinement.

### Smaller Cases That Pass

Interestingly, simpler cases with the same pattern pass:
- `MinimalCase_SquareWithEdgeConstraint` - 5 vertices, simple constraint
- `ContourBuilder_ConstraintEdgeOnPerimeter_ShouldComplete` - simple case with Ruppert
- `Perimeter_AfterRuppert_WaterBody_ShouldBeTraversable` - moderate complexity

This suggests the bug is triggered by specific edge configurations or occurs only after sufficient refinement iterations.

---

## Root Cause Analysis - RESOLVED

### The Problem

When `IncrementalTin.SplitEdge()` splits a perimeter edge (an edge with a ghost triangle on one side), the ghost triangle wiring was incorrect. This caused subsequent splits on the same perimeter segment to fail to detect the ghost side.

### Key Insight

When splitting edge A→B with ghost vertex on one side:

**BEFORE Split:**
```
                A ────────────────── B
               /                      \
              /    Ghost Triangle      \
             /     (exterior)           \
            ghost ←─────────────────── ghost
```

The ghost triangle has edges: `ab → bc → ca` where:
- `ab` = A→B (the edge being split)
- `bc` = B→ghost
- `ca` = ghost→A

**AFTER Split (need TWO ghost triangles):**
```
                A ──────── M ──────── B
               /          │           \
              /   Ghost   │   Ghost    \
             /   Tri 1    │   Tri 2     \
            ghost ←───────┴──────────── ghost
```

We need:
- **Ghost Triangle 1 (AMc):** `am → ghostM → ca` (A→M, M→ghost, ghost→A)
- **Ghost Triangle 2 (MBc):** `mb → bc → mGhost` (M→B, B→ghost, ghost→M)

### The Bug

The original wiring code was:
```csharp
// WRONG - ghostM and mGhost were swapped in the triangles
mb.SetForward(bc);
bc.SetForward(ghostM);  // Should be mGhost!
ghostM.SetForward(mb);

mGhost.SetForward(ca);  // Should be ghostM!
ca.SetForward(am);
am.SetForward(mGhost);
```

This created invalid triangles where `am.GetForward().GetB()` returned M instead of ghost, causing subsequent `SplitEdge` calls to NOT detect the perimeter edge (since neither c nor d appeared to be a ghost vertex).

### The Fix

Corrected wiring in `IncrementalTin.SplitEdge()`:

```csharp
// Ghost Triangle MBc: M→B→ghost→M
mb.SetForward(bc);      // M→B followed by B→ghost
bc.SetForward(mGhost);  // B→ghost followed by ghost→M
mGhost.SetForward(mb);  // ghost→M followed by M→B

// Ghost Triangle AMc: A→M→ghost→A
am.SetForward(ghostM);  // A→M followed by M→ghost
ghostM.SetForward(ca);  // M→ghost followed by ghost→A
ca.SetForward(am);      // ghost→A followed by A→M
```

### Diagram: Ghost Triangle Wiring

```
    BEFORE SPLIT                         AFTER SPLIT (CORRECT)
    ============                         ====================

         A ────────── B                       A ───── M ───── B
          \          /                         \      │      /
           \   bc   /                           \     │     /
            \  ↓   /                        am   \ ghostM  /   mb
             \ ↓  /                          ↓    \  ↓    /    ↓
              ghost                           ↓    \ ↓   /     ↓
               ↑                              ↓     \↓  /      ↓
               │                              ↓    ghost       ↓
               ca                              ↓   ↗   ↖      ↓
                                               ↓  /     \     ↓
                                               ca←       →mGhost
                                                          ↘   ↙
                                                            bc

    Triangle ABc:                        Triangle AMc:        Triangle MBc:
    ab → bc → ca                         am → ghostM → ca     mb → bc → mGhost
```

### Verification

After the fix:
- First perimeter split: `(100,60)→(100,40)` at M=(100,50) - detected as PERIMETER
- Second perimeter split: `(100,60)→(100,50)` at M=(100,55) - NOW detected as PERIMETER (was failing before)
- All 33 perimeter topology tests pass
- All 24 Ruppert refinement tests pass

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Dec 18, 2025 | Initial investigation document |
| 1.1 | Dec 18, 2025 | Added reproduction test case finding |
| 1.2 | Dec 18, 2025 | **RESOLVED** - Fixed ghost triangle wiring in SplitEdge |

---

*End of Document*
