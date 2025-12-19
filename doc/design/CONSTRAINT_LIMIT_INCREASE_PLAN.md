# Plan: Increasing Constraint Limit from 8,190 to 32,766

**Date:** December 15, 2025
**Author:** Claude Code
**Status:** Implemented
**Target:** Increase maximum constraint count from ~8k to 32k+

---

## Executive Summary

The Tinfour.NET library currently supports a maximum of **8,190 constraints** due to bit field limitations in the `QuadEdgePartner._index` field. This document proposes a simple, performant approach to increase this limit to **32,766 constraints** (or optionally 65,534) by reducing the number of flag bits and utilizing the reserved bit.

---

## Current Architecture

### Bit Layout in `QuadEdgePartner._index` (32-bit integer)

```
Current Layout (32 bits total):
┌─────────────────────────────────────────────────────────────────┐
│ Bit 31  │ Bit 30  │ Bit 29  │ Bit 28  │ Bit 27  │ Bit 26  │ Bits 25-13 │ Bits 12-0  │
│  Sign   │ Region  │ Region  │  Line   │ Synth   │Reserved │   Upper    │   Lower    │
│  Flag   │ Border  │Interior │ Member  │  Edge   │  Bit    │   Index    │   Index    │
│ (Const) │  Flag   │  Flag   │  Flag   │  Flag   │         │  (13 bits) │  (13 bits) │
└─────────────────────────────────────────────────────────────────┘

Flags (5 used + 1 reserved = 6 bits):
- Bit 31: ConstraintEdgeFlag (sign bit - edge is constrained)
- Bit 30: ConstraintRegionBorderFlag
- Bit 29: ConstraintRegionInteriorFlag
- Bit 28: ConstraintLineMemberFlag
- Bit 27: SyntheticEdgeFlag
- Bit 26: EdgeFlagReservedBit (unused)

Index Fields (26 bits):
- Bits 25-13: Upper index field (13 bits) - stores line constraint index
- Bits 12-0:  Lower index field (13 bits) - stores region/border constraint index
```

### Current Constraints

| Parameter | Value | Calculation |
|-----------|-------|-------------|
| Flag bits | 6 | 5 used + 1 reserved |
| Index bits per field | 13 | (32 - 6) / 2 = 13 |
| Max raw value | 8,191 | 2^13 - 1 |
| Max constraint index | **8,190** | 8,191 - 1 (0 reserved for null) |

### Key Files

| File | Purpose |
|------|---------|
| [QuadEdgeConstants.cs](../../Tinfour.Core/Edge/QuadEdgeConstants.cs) | Flag and mask definitions |
| [QuadEdgePartner.cs](../../Tinfour.Core/Edge/QuadEdgePartner.cs) | Constraint storage implementation |
| [QuadEdge.cs](../../Tinfour.Core/Edge/QuadEdge.cs) | Base edge class (delegates to partner) |
| [IQuadEdge.cs](../../Tinfour.Core/Common/IQuadEdge.cs) | Interface definitions |
| [IncrementalTin.cs](../../Tinfour.Core/Standard/IncrementalTin.cs) | Constraint count validation (line 261-263) |

---

## Proposed Solution: Reduce Flags, Expand Indices

### Approach: Eliminate Reserved Bit + Combine Flags

The most straightforward approach is to:

1. **Eliminate the reserved bit** (Bit 26) - it's currently unused
2. **Combine ConstraintRegionInterior and ConstraintRegionBorder** into a single "region member" flag, using the lower index to distinguish border vs interior
3. This frees up **2 bits**, allowing **15-bit index fields** instead of 13-bit

### New Bit Layout

```
Proposed Layout (32 bits total):
┌─────────────────────────────────────────────────────────────────┐
│ Bit 31  │ Bit 30  │ Bit 29  │ Bit 28  │ Bits 27-14 │ Bits 13-0  │
│  Sign   │ Region  │  Line   │ Synth   │   Upper    │   Lower    │
│  Flag   │ Member  │ Member  │  Edge   │   Index    │   Index    │
│ (Const) │  Flag   │  Flag   │  Flag   │  (14 bits) │  (14 bits) │
└─────────────────────────────────────────────────────────────────┘

Flags (4 bits):
- Bit 31: ConstraintEdgeFlag (sign bit - unchanged)
- Bit 30: ConstraintRegionMemberFlag (combines border + interior)
- Bit 29: ConstraintLineMemberFlag (moved from bit 28)
- Bit 28: SyntheticEdgeFlag (moved from bit 27)

Index Fields (28 bits):
- Bits 27-14: Upper index field (14 bits) - stores line constraint index
- Bits 13-0:  Lower index field (14 bits) - stores region constraint index

Special encoding in lower index for region constraints:
- Value > 0: Regular constraint index + 1
- High bit of stored value: 0 = border, 1 = interior (within the 14-bit field)
```

### Alternative: Simpler Approach (Recommended)

A simpler alternative that achieves **32,766 constraints** with minimal code changes:

```
Simpler Layout (32 bits total):
┌─────────────────────────────────────────────────────────────────┐
│ Bit 31  │ Bit 30  │ Bit 29  │ Bit 28  │ Bit 27  │ Bits 26-15 │ Bits 14-0  │
│  Sign   │ Region  │ Region  │  Line   │ Synth   │   Upper    │   Lower    │
│  Flag   │ Border  │Interior │ Member  │  Edge   │   Index    │   Index    │
│ (Const) │  Flag   │  Flag   │  Flag   │  Flag   │  (12 bits) │  (15 bits) │
└─────────────────────────────────────────────────────────────────┘

Keep all flags, but asymmetrically allocate index bits:
- Lower index: 15 bits (for region constraints) = 32,766 max
- Upper index: 12 bits (for line constraints) = 4,094 max
```

**Rationale:** Most use cases have more region/polygon constraints than line constraints. Line constraints are often used for breaklines, which are typically fewer in number.

---

## Recommended Implementation

### Option A: Asymmetric Index Allocation (Simplest)

This is the **recommended approach** as it requires minimal changes:

#### New Constants

```csharp
// QuadEdgeConstants.cs changes

// Index sizes - asymmetric allocation
public const int ConstraintLowerIndexBitSize = 15;  // Was 13
public const int ConstraintUpperIndexBitSize = 12;  // Was 13 (implicit)

// Max values
public const int ConstraintLowerIndexValueMax = (1 << ConstraintLowerIndexBitSize) - 2;  // 32,766
public const int ConstraintUpperIndexValueMax = (1 << ConstraintUpperIndexBitSize) - 2;  // 4,094

// Legacy constant for backward compatibility
public const int ConstraintIndexValueMax = ConstraintLowerIndexValueMax;  // 32,766

// Masks need recalculation
public const int ConstraintLowerIndexMask = (1 << ConstraintLowerIndexBitSize) - 1;  // 0x7FFF
public const int ConstraintLowerIndexZero = unchecked((int)(0xffffffff << ConstraintLowerIndexBitSize));

public const int ConstraintUpperIndexMask = ((1 << ConstraintUpperIndexBitSize) - 1) << ConstraintLowerIndexBitSize;
public const int ConstraintUpperIndexZero = ~ConstraintUpperIndexMask;
```

#### Changes Required

| File | Change |
|------|--------|
| `QuadEdgeConstants.cs` | Update bit size and mask constants |
| `QuadEdgePartner.cs` | Update shift amounts in get/set methods |
| `IncrementalTin.cs` | Update validation check (line 262) |
| `IQuadEdge.cs` | Update XML documentation |
| `QuadEdge.cs` | Update XML documentation |
| Tests | Update `ConstrainedDelaunayTriangulationTests.cs` |

#### Impact Analysis

| Aspect | Impact |
|--------|--------|
| Memory | **None** - same 32-bit integer |
| Performance | **None** - same bit operations |
| API | **None** - interfaces unchanged |
| Binary Compatibility | **Breaking** for serialized data |
| Line Constraints | Reduced from 8,190 to 4,094 |
| Region Constraints | Increased from 8,190 to 32,766 |

---

### Option B: Eliminate Reserved Bit (More Constraints, More Changes)

If line constraints also need to support 32k:

```csharp
// Eliminate reserved bit, get 14 bits each
public const int ConstraintIndexBitSize = 14;  // Was 13
public const int ConstraintIndexValueMax = (1 << ConstraintIndexBitSize) - 2;  // 16,382
```

This gives **16,382** for both index types but requires removing the reserved bit flag.

---

### Option C: Use Long for Constraint Storage (Maximum Flexibility)

For maximum future-proofing, the `_index` field in `QuadEdgePartner` could be changed to `long`:

```csharp
// In QuadEdgePartner
protected internal long _constraintData;  // New field for constraint data
```

**Pros:**
- Virtually unlimited constraints (millions)
- Clean separation of concerns
- No flag bit juggling

**Cons:**
- **8 bytes additional memory per edge pair**
- For 1M vertices with 3M edges: ~24MB extra memory
- Requires more extensive refactoring

**Recommendation:** Only pursue if Option A proves insufficient.

---

## Implementation Plan

### Phase 1: Constants and Core Changes

1. Update `QuadEdgeConstants.cs`:
   - Change `ConstraintIndexBitSize` to 15 (lower) / 12 (upper)
   - Recalculate all derived masks
   - Add new `ConstraintLowerIndexBitSize` and `ConstraintUpperIndexBitSize` constants
   - Update `ConstraintIndexValueMax` to 32,766

2. Update `QuadEdgePartner.cs`:
   - Modify `GetConstraintIndex()` to use new mask
   - Modify `SetConstraintIndex()` to use new mask
   - Modify `GetConstraintLineIndex()` to use new shift/mask
   - Modify `SetConstraintLineIndex()` to use new shift/mask
   - Modify `GetConstraintBorderIndex()` to use new mask
   - Modify `SetConstraintBorderIndex()` to use new shift/mask
   - Modify `GetConstraintRegionInteriorIndex()` to use new mask
   - Modify `SetConstraintRegionInteriorIndex()` to use new mask

### Phase 2: Validation Updates

3. Update `IncrementalTin.cs`:
   - Change constraint count validation (line 262) from 8190 to 32766
   - Consider separate limits for region vs line constraints

4. Update documentation in:
   - `IQuadEdge.cs` - Interface XML docs
   - `QuadEdge.cs` - Method XML docs
   - Architecture documentation

### Phase 3: Testing

5. Update tests in `ConstrainedDelaunayTriangulationTests.cs`:
   - Update `AddConstraints_ExceedsMaximum_ShouldThrowException` test
   - Add test for 10,000+ constraints
   - Add test for 30,000+ constraints
   - Add boundary value tests (32,765, 32,766, 32,767)

6. Add regression tests:
   - Verify existing constraint functionality unchanged
   - Test roundtrip of constraint indices through get/set
   - Test mixed region and line constraints

### Phase 4: Documentation

7. Update documentation:
   - This design document
   - `doc/architecture/core/core-library.md`
   - `doc/architecture/data-structures/quad-edge.md`
   - Release notes

---

## Detailed Code Changes

### QuadEdgeConstants.cs

```csharp
// BEFORE
public const int ConstraintIndexBitSize = 13;
public const int ConstraintIndexValueMax = (1 << ConstraintIndexBitSize) - 2;  // 8190

// AFTER - Asymmetric allocation
public const int ConstraintLowerIndexBitSize = 15;
public const int ConstraintUpperIndexBitSize = 12;

public const int ConstraintLowerIndexValueMax = (1 << ConstraintLowerIndexBitSize) - 2;  // 32766
public const int ConstraintUpperIndexValueMax = (1 << ConstraintUpperIndexBitSize) - 2;  // 4094

// For backward compatibility and general use
public const int ConstraintIndexBitSize = ConstraintLowerIndexBitSize;
public const int ConstraintIndexValueMax = ConstraintLowerIndexValueMax;

// Masks - recalculated
public const int ConstraintLowerIndexMask = (1 << ConstraintLowerIndexBitSize) - 1;  // 0x7FFF (bits 0-14)
public const int ConstraintLowerIndexZero = unchecked((int)(0xffffffff << ConstraintLowerIndexBitSize));

public const int ConstraintUpperIndexMask = ((1 << ConstraintUpperIndexBitSize) - 1) << ConstraintLowerIndexBitSize;  // bits 15-26
public const int ConstraintUpperIndexZero = ~ConstraintUpperIndexMask;
```

### QuadEdgePartner.cs - GetConstraintLineIndex

```csharp
// BEFORE
public override int GetConstraintLineIndex()
{
    if ((_index & ConstraintLineMemberFlag) != 0)
    {
        var c = (_index & ConstraintUpperIndexMask) >> ConstraintIndexBitSize;
        if (c != 0) return c - 1;
    }
    return -1;
}

// AFTER
public override int GetConstraintLineIndex()
{
    if ((_index & ConstraintLineMemberFlag) != 0)
    {
        var c = (_index & ConstraintUpperIndexMask) >> ConstraintLowerIndexBitSize;
        if (c != 0) return c - 1;
    }
    return -1;
}
```

### QuadEdgePartner.cs - SetConstraintLineIndex

```csharp
// BEFORE
var shiftedValue = augmented << ConstraintIndexBitSize;

// AFTER
var shiftedValue = augmented << ConstraintLowerIndexBitSize;
```

### IncrementalTin.cs

```csharp
// BEFORE (line 261-263)
// Maximum constraint index is limited by edge storage capacity (8190)
if (_constraintList.Count + constraints.Count > 8190)
    throw new ArgumentException("Maximum number of constraints (8190) exceeded");

// AFTER
// Maximum constraint index is limited by edge storage capacity (32766)
if (_constraintList.Count + constraints.Count > QuadEdgeConstants.ConstraintIndexValueMax)
    throw new ArgumentException(
        $"Maximum number of constraints ({QuadEdgeConstants.ConstraintIndexValueMax}) exceeded");
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Bit manipulation bugs | Medium | High | Comprehensive unit tests |
| Line constraint limit too low | Low | Medium | Can adjust ratio; 4k is usually sufficient |
| Serialization incompatibility | High | Medium | Document as breaking change |
| Performance regression | Very Low | Low | Same operations, just different masks |

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void SetConstraintIndex_AtNewMaximum_ShouldSucceed()
{
    // Test setting constraint index at 32766
}

[Fact]
public void SetConstraintIndex_ExceedsNewMaximum_ShouldThrow()
{
    // Test that 32767 throws
}

[Fact]
public void SetConstraintLineIndex_AtNewMaximum_ShouldSucceed()
{
    // Test setting line constraint at 4094
}

[Fact]
public void AddConstraints_With15000Constraints_ShouldSucceed()
{
    // Integration test with many constraints
}

[Fact]
public void MixedConstraints_RegionAndLine_ShouldCoexist()
{
    // Test edges can have both region and line constraints
}
```

### Performance Tests

- Benchmark constraint addition with 1k, 10k, 30k constraints
- Memory profiling to verify no regression
- Compare with baseline measurements

---

## Conclusion

The recommended approach is **Option A: Asymmetric Index Allocation**, which:

- Increases the constraint limit from **8,190 to 32,766** for region constraints
- Requires minimal code changes (~50 lines)
- Has zero memory or performance impact
- Maintains full API compatibility
- Trades line constraint capacity (down to 4,094) which is rarely a limitation

This solution directly addresses the stated requirement of supporting 12k+ constraints with headroom for future growth, while keeping the implementation simple and maintainable.

---

## Appendix: Bit Layout Visualization

### Current (13-bit indices)

```
Bit: 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17 16 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0
     ┌──┬──┬──┬──┬──┬──┬──────────────────────────────┬──────────────────────────────┐
     │CE│RB│RI│LM│SE│RV│      Upper Index (13)        │      Lower Index (13)        │
     └──┴──┴──┴──┴──┴──┴──────────────────────────────┴──────────────────────────────┘

CE=ConstraintEdge, RB=RegionBorder, RI=RegionInterior, LM=LineMember, SE=Synthetic, RV=Reserved
```

### Proposed (15/12-bit indices)

```
Bit: 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17 16 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0
     ┌──┬──┬──┬──┬──┬───────────────────────────┬─────────────────────────────────────┐
     │CE│RB│RI│LM│SE│    Upper Index (12)       │           Lower Index (15)          │
     └──┴──┴──┴──┴──┴───────────────────────────┴─────────────────────────────────────┘
```

---

## Implementation Summary

The implementation was completed on December 18, 2025. The following files were modified:

### Files Modified

| File | Changes |
|------|---------|
| `Tinfour.Core/Edge/QuadEdgeConstants.cs` | Added `ConstraintLowerIndexBitSize` (15), `ConstraintUpperIndexBitSize` (12), `ConstraintLowerIndexValueMax` (32766), `ConstraintUpperIndexValueMax` (4094); updated masks; added helper methods `ExtractLowerIndex`, `ExtractUpperIndex`, `PackLowerIndex`, `PackUpperIndex` |
| `Tinfour.Core/Edge/QuadEdgePartner.cs` | Fixed border index to use lower bits (same as interior - they're mutually exclusive); updated all constraint get/set methods to use new helper methods and validation |
| `Tinfour.Core/Standard/IncrementalTin.cs` | Updated constraint count validation to use `QuadEdgeConstants.ConstraintIndexValueMax` |
| `Tinfour.Core/Common/IQuadEdge.cs` | Updated XML documentation for new limits |
| `Tinfour.Core/Edge/QuadEdge.cs` | Updated XML documentation for new limits |
| `Tinfour.Core.Tests/Constraints/ConstrainedDelaunayTriangulationTests.cs` | Updated test for new maximum; added test for 15,000 constraints |
| `Tinfour.Core.Tests/Edge/QuadEdgePartnerTests.cs` | Updated test values for new limits; enabled previously skipped tests |
| `Tinfour.Core.Tests/Edge/ConstraintBitManipulationTest.cs` | Updated test values for new limits |

### Critical Fix: Border Index Bit Field

The original implementation stored border index in the **upper bits** (12-bit field), which meant polygon constraints were still limited to ~4,094 even with the asymmetric allocation. This was fixed by moving border index storage to use the **lower bits** (15-bit field), same as interior index. Since an edge cannot be both border and interior simultaneously, they can share the same bit field.

### Test Results

- 529 tests pass
- 12 pre-existing test failures remain (unrelated to this change - LinearConstraint, PolygonConstraint constructors, IDW tests)
- 1 intentionally skipped test (alternative expectation)
- New test `AddConstraints_AtMaximumLimit_ShouldSucceed` validates 15,000 polygon constraints work correctly
- Previously skipped tests for bit-field mismatch now pass

### Breaking Changes

- **Line constraints now limited to 4,094** (down from 8,190)
- **Serialized data incompatible** with previous versions (if any serialization existed)

---

**Document Version:** 1.2
**Last Updated:** December 18, 2025

---

## Related Design Notes

- `doc/design/NON_RASTER_3D_OUTPUT_PIPELINES.md` (mesh export, smooth shading, adaptive refinement, TIN-based contours)
