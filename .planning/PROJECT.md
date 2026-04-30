# Improve Ruppert's Refinement Constraint Handling

## What This Is

Fix Ruppert's refinement algorithm in Tinfour.NET to prevent mesh "escape" beyond constrained regions, and add convex hull constraint generation as a better alternative to synthetic rectangular constraints for unbounded data. This is a correctness and usability improvement to the existing constrained Delaunay triangulation library.

## Core Value

Ruppert's refinement must never produce Steiner points or refined triangles outside the constrained region boundary.

## Requirements

### Validated

- ✓ Incremental Delaunay triangulation via Bowyer-Watson — existing
- ✓ Quad-edge data structure with constraint metadata (border/interior/member flags) — existing
- ✓ Constrained Delaunay triangulation with conformity restoration — existing
- ✓ Ruppert's refinement with Steiner point insertion — existing
- ✓ Constraint region flood fill marking (border/interior edges) — existing
- ✓ `RefineOnlyInsideConstraints` flag to limit refinement to constraint regions — existing
- ✓ Multiple interpolation methods (Triangular Facet, Natural Neighbor, IDW) — existing
- ✓ Polygon and linear constraint support — existing

### Active

- [ ] Fix Ruppert's refinement escape: refinement must not produce geometry outside constraint boundaries
- [ ] Diagnose root cause of constraint region flag corruption/loss during Steiner point insertion and edge flipping
- [ ] Ensure constraint region flags (border/interior) propagate correctly when edges are split or flipped during refinement
- [ ] Add convex hull constraint generation: compute convex hull of input points and add as polygon constraint with no-Z vertices
- [ ] Convex hull constraint should be usable as a drop-in replacement for synthetic rectangular constraints
- [ ] Regression tests that verify refinement stays within constraint boundaries for trail data
- [ ] Regression tests that verify refinement stays within constraint boundaries for complex scenarios

### Out of Scope

- Extrapolation beyond convex hull — planned future work, does not affect this milestone
- Performance optimization of Ruppert's refinement — separate concern unless directly related to the fix
- UI/visualizer changes — fix is in the core library
- Changes to interpolation methods — not related to constraint handling

## Context

- **Observed behavior:** Ruppert's refinement occasionally "escapes" constrained regions, growing the mesh outward from certain areas until it fizzles out. This produces Steiner points and triangles well beyond the input constraint boundary.
- **Trigger patterns:** More common with synthetic rectangular constraints around unbounded data; less common with fully defined shoreline boundaries. The rectangle creates large empty regions between data and constraint boundary that may exacerbate the issue.
- **Suspected root causes:** Edge flag propagation during Steiner point insertion, encroached constraint edge splits not preserving border flags, or flood fill running once at setup and not accounting for topology changes during refinement.
- **Key code paths:** `RuppertRefiner.cs` (refinement loop, `TriangleBadPriority` checks), `ConstraintProcessor.cs` (flood fill), `QuadEdgePartner.cs` (constraint flags), `IncrementalTin.cs` (constraint integration).
- **Test data:** `RuppertsTestData/TestTrail.csv` — WGS84 lat/lon/depth bathymetry trail. Additional complex scenario data expected from user.
- **Convex hull motivation:** Using the convex hull of data points as the artificial constraint is much better than a rectangle — it shares vertices with genuine points, avoids refining large empty areas, and everything outside the hull is ultimately declared no-data anyway.

## Constraints

- **Tech stack**: .NET 8.0, C# 12, xUnit for tests — must stay consistent with existing project
- **Compatibility**: Fix must not break existing constraint handling for genuine polygon/shoreline constraints
- **Data format**: Test data is WGS84 (lat/lon) — may need UTM conversion for simplicity in geometric operations

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Use convex hull instead of rectangle for artificial constraints | Tighter boundary, shares vertices with data, avoids wasted refinement in empty areas | — Pending |
| Constraint vertices have no Z values | Artificial boundary markers only, not data points | — Pending |
| Fix root cause in edge flag propagation rather than adding post-hoc guards | Addressing symptoms would leave latent bugs for other constraint geometries | — Pending |

---
*Last updated: 2026-04-30 after initialization*
