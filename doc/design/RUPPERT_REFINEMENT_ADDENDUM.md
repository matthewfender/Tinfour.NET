# Ruppert's Refinement Algorithm - Addendum

**Document Version:** 1.0
**Date:** December 17, 2025
**Author:** Claude Code
**Status:** Active Development
**Parent Documents:**
- [RUPPERT_REFINEMENT_IMPLEMENTATION_PLAN.md](RUPPERT_REFINEMENT_IMPLEMENTATION_PLAN.md)
- [RUPPERT_REFINEMENT_FIX_PLAN.md](RUPPERT_REFINEMENT_FIX_PLAN.md)

---

## Overview

This addendum documents recent changes, findings, and known issues discovered during ongoing development of the Ruppert refinement implementation in Tinfour.NET.

---

## Recent Changes (December 17, 2025)

### 1. ContourBuilderForTin - ConstrainedRegionsOnly Option

**Feature:** Added ability to generate contours only within constrained regions.

**Files Modified:** `Tinfour.Core/Contour/ContourBuilderForTin.cs`

**Changes:**
- Added `_constrainedRegionsOnly` field
- Added constructor parameter `constrainedRegionsOnly` with default value `true`
- Added helper method `IsTriangleInConstrainedRegion(IQuadEdge edge)` to check if a triangle is within a constrained region
- Added filtering in `BuildClosedLoopContours()` and `BuildOpenContours()` to skip edges not in constrained regions when the option is enabled

**Usage:**
```csharp
var builder = new ContourBuilderForTin(
    tin,
    vertexValuator,
    zContour,
    buildRegions: true,
    constrainedRegionsOnly: true  // Default is now true
);
```

### 4. RuppertOptions - InterpolationType Property

**Feature:** Added ability to configure the interpolation method used for computing Z values of newly inserted Steiner points.

**File Modified:** `Tinfour.Core/Refinement/RuppertOptions.cs`

**Property Added:**
```csharp
/// <summary>
///     Gets or sets the interpolation method to use for computing Z values of new vertices.
/// </summary>
public InterpolationType InterpolationType { get; set; } = InterpolationType.TriangularFacet;
```

**Options:**
- `InterpolationType.TriangularFacet` (default): Fast linear interpolation within triangles. Provides C0 continuity.
- `InterpolationType.NaturalNeighbor`: Slower but provides smoother C1 continuity (except at data vertices). Better for 3D visualization and terrain modeling.

---

## Known Issues

### 1. Constraint Leakage at High Angles (25°+)

**Status:** Active issue - under investigation

**Symptom:** When running Ruppert refinement with minimum angles of 25° or higher on certain constraint configurations (particularly donut-shaped constraints), refinement "escapes" the constrained region and fills the entire TIN with triangles.

**History:**
- December 17, 2025: Attempted fix using `PropagateConstraintRegionMembership` in `SplitEdge` and `RestoreConformity` - this made the problem worse and was reverted

**Potential Causes:**
1. Edge cases in constraint propagation during ghost edge handling
2. Border edge semantics for hole constraints vs. solid constraints
3. Issue in `SplitEdge` or `RestoreConformity` constraint propagation logic

**Workarounds:**
- Use the new `constrainedRegionsOnly` option in ContourBuilderForTin to filter contours to constrained regions
- Use very low minimum angles (< 25°) which may be more stable (not fully tested)

**Notes:**
- The Java `restoreConformity` method does NOT perform any constraint region propagation - it only splits the edge and creates topology. The C# implementation adds constraint propagation which may be incorrect.

### 2. Perimeter Contour Jumps

**Status:** May be related to constraint propagation, pending testing after recent fixes

**Symptom:** Contour lines can occasionally make large jumps across unconstrained regions when the contour originates from the perimeter of the TIN.

**Notes:**
- The `constrainedRegionsOnly` option mitigates this by filtering out triangles not in constrained regions

### 3. GetPerimeter() Performance After Heavy Refinement

**Status:** Documented in FIX_PLAN Appendix H

**Symptom:** After heavy refinement (tens of thousands of vertex insertions), the `GetPerimeter()` method can experience an infinite loop or very slow performance.

**Current Mitigation:** Safety limit added to prevent infinite loops:
```csharp
var maxIterations = _edgePool.Size() * 2 + 1000;
```

**Root Cause:** The ghost edge traversal pattern may encounter inconsistencies in edge topology after heavy refinement. This is a symptom of deeper edge management issues that warrant further investigation.

---

## Architecture Notes

### Constraint Region Propagation Flow

The constraint region membership is established and maintained through several mechanisms:

1. **Initial Establishment** (`ConstraintProcessor.cs`):
   - Border edges are marked during `ProcessConstraint()`
   - Interior edges are marked via flood fill from border edges

2. **Propagation During Vertex Insertion** (`IncrementalTin.cs:InsertVertex()`):
   - When inserting a vertex inside a constraint region, `PropagateConstraintRegionMembership()` is called
   - Uses pinwheel traversal to mark all edges radiating from the new vertex

3. **Propagation During Edge Splitting** (`IncrementalTin.cs:SplitEdge()`):
   - **NEW:** Now calls `PropagateConstraintRegionMembership()` after split
   - Also manually sets indices on `cm` and `dm` edges for border edge cases

4. **Propagation During Delaunay Restoration** (`IncrementalTin.cs:RestoreConformity()`):
   - When a constrained edge is split to restore Delaunay criterion
   - **NEW:** Now calls `PropagateConstraintRegionMembership()` after split

### PropagateConstraintRegionMembership Method

This method (lines 1390-1426 in `IncrementalTin.cs`) performs pinwheel traversal around a vertex to propagate constraint region membership:

```csharp
private void PropagateConstraintRegionMembership(QuadEdge pStart, int constraintIndex)
{
    // Handle pStart specially - if it's a border edge, get the constraint for the left side
    var currentConstraintIndex = constraintIndex;
    if (pStart.IsConstraintRegionBorder())
    {
        var con = GetBorderConstraint(pStart);
        currentConstraintIndex = con?.GetConstraintIndex() ?? -1;
    }

    // Mark pStart if appropriate
    if (currentConstraintIndex >= 0 && !pStart.IsConstrained() && !pStart.IsConstraintRegionMember())
    {
        pStart.SetConstraintRegionInteriorIndex(currentConstraintIndex);
    }

    // Iterate around the pinwheel
    foreach (var e in pStart.GetPinwheel())
    {
        if (e.IsConstraintRegionBorder())
        {
            var con = GetBorderConstraint(e);
            currentConstraintIndex = con?.GetConstraintIndex() ?? -1;
        }
        if (currentConstraintIndex >= 0 && !e.IsConstrained() && !e.IsConstraintRegionMember())
        {
            e.SetConstraintRegionInteriorIndex(currentConstraintIndex);
        }
    }
}
```

---

## Testing Recommendations

### Constraint Propagation Tests

1. **Donut Constraint at Various Angles**
   - Test with 20°, 25°, 30°, 33° minimum angles
   - Verify no edges marked as interior appear outside the outer ring
   - Verify no edges marked as interior appear inside the hole

2. **Perimeter Edge Splitting**
   - Create constraints that touch the convex hull perimeter
   - Verify constraint propagation works correctly when splitting perimeter edges

3. **Contour Generation**
   - Generate contours with and without `constrainedRegionsOnly` option
   - Verify contours stay within constrained regions when option is enabled

### Performance Tests

1. **Large Vertex Counts**
   - Test with 10k, 50k, 100k initial vertices
   - Measure time and memory usage
   - Verify `GetPerimeter()` completes in reasonable time

---

## Future Work

### Pending Fixes from FIX_PLAN

- **FIX-01**: Add geometric point-in-polygon verification for hole constraints (Clipper2 added, implementation pending)
- **FIX-07**: Add hole-aware constraint propagation during edge splitting
- **FIX-09**: Add fallback insertion strategies
- **FIX-10**: Extend SweepForConstraintAssignments to recursive propagation

### Smoothing Filter Port

A smoothing filter implementation is planned for a future branch. The Java reference implementation is in:
- `SmoothingFilter.java`
- `SmoothingFilterInitializer.java`

Key features:
- Low-pass filter using barycentric coordinates
- Page-based storage for large datasets
- Preserves constraint member vertices (not smoothed)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Dec 17, 2025 | Initial addendum documenting constraint propagation fixes and ContourBuilderForTin changes |

---

*End of Document*
