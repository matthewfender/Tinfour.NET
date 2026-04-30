# Architecture

**Analysis Date:** 2026-04-30

## Pattern Overview

**Overall:** Faithful C# port of Java Tinfour library with incremental Delaunay triangulation using quad-edge data structure.

**Key Characteristics:**
- Incremental vertex insertion with Bowyer-Watson algorithm
- Quad-edge representation for topological integrity
- Stochastic Lawson's Walk for point location
- Constrained Delaunay Triangulation with conformity restoration
- Multiple interpolation methods (Triangular Facet, Natural Neighbor, IDW)
- Thread-safe read operations after locking
- Memory-efficient edge pooling and struct optimizations

## Layers

**Triangulation Core:**
- Purpose: Build and maintain the constrained Delaunay triangulation (CDT)
- Location: `Tinfour.Core/Standard/IncrementalTin.cs`, `Tinfour.Core/Edge/`
- Contains: TIN construction, edge management, triangle operations
- Depends on: Geometric operations, bootstrap utilities, edge pool
- Used by: All interpolation, analysis, and constraint processing

**Edge & Topology:**
- Purpose: Manage quad-edge data structure and topological relationships
- Location: `Tinfour.Core/Edge/QuadEdge.cs`, `Tinfour.Core/Edge/EdgePool.cs`
- Contains: Quad-edge representation, dual navigation, constraint metadata
- Depends on: Vertex types, common structures
- Used by: IncrementalTin, constraint processors, analysis operations

**Geometric Operations:**
- Purpose: Implement robust geometric predicates and calculations
- Location: `Tinfour.Core/Common/GeometricOperations.cs`, `Tinfour.Core/Common/DoubleDouble.cs`
- Contains: Circumcircle calculations, orientation tests, in-circle tests, extended precision arithmetic
- Depends on: Vertex, circumcircle utilities
- Used by: Triangulation, constraint processing, bootstrap

**Data Structures & Common:**
- Purpose: Core types and utilities shared across library
- Location: `Tinfour.Core/Common/`
- Contains: Vertex, Circumcircle, Thresholds, utilities (HilbertSort, BarycentricCoordinates)
- Depends on: System classes only
- Used by: All layers

**Constraint Processing:**
- Purpose: Add and integrate linear and polygon constraints into TIN
- Location: `Tinfour.Core/Standard/ConstraintProcessor.cs`
- Contains: Constraint addition logic, conformity restoration, region assignment
- Depends on: IncrementalTin, edge operations, geometric operations
- Used by: IncrementalTin.AddConstraints()

**Point Location:**
- Purpose: Efficiently locate containing triangle for a query point
- Location: `Tinfour.Core/Common/StochasticLawsonsWalk.cs`
- Contains: Stochastic walk algorithm, bootstrap operations
- Depends on: Geometric operations, vertex structures
- Used by: Interpolation, nearest neighbor operations

**Interpolation:**
- Purpose: Compute Z values at arbitrary XY locations
- Location: `Tinfour.Core/Interpolation/`
- Contains: Three interpolation methods (Triangular Facet, Natural Neighbor, IDW), rasterizer
- Depends on: IncrementalTin, navigators, raster data types
- Used by: TinRasterizer, client applications

**Refinement:**
- Purpose: Improve mesh quality using Ruppert's algorithm
- Location: `Tinfour.Core/Refinement/RuppertRefiner.cs`
- Contains: Delaunay refinement, Steiner point insertion, constraint segment handling
- Depends on: IncrementalTin, geometric operations, interpolation
- Used by: Quality improvement workflows

**Analysis & Utilities:**
- Purpose: Extract contours, Voronoi diagrams, and derived data
- Location: `Tinfour.Core/Contour/`, `Tinfour.Core/Voronoi/`, `Tinfour.Core/Utils/`
- Contains: Isoline extraction, Voronoi tessellation, boundary extraction, triangle collection
- Depends on: IncrementalTin, edge operations
- Used by: Analysis workflows

**Serialization:**
- Purpose: Persist and restore TIN state
- Location: `Tinfour.Core/Serialization/TinSerializer.cs`
- Contains: Binary serialization format, TIN save/load operations
- Depends on: IncrementalTin, vertex, edge structures
- Used by: Visualizer, data persistence

## Data Flow

**TIN Construction:**

1. Create IncrementalTin instance
2. Add vertices via `Add()` or `AddSorted()` (Hilbert ordering)
3. Bootstrap when 3+ non-collinear vertices exist
4. Insert remaining vertices using Bowyer-Watson (flip edges to restore Delaunay property)
5. Lock TIN when complete for read-only access

**Constraint Addition:**

1. Prepare constraint list (LinearConstraint or PolygonConstraint)
2. Call `AddConstraints(constraints, restoreConformity: true)`
3. ConstraintProcessor inserts constraint edges
4. Flips edges to ensure constraint conformity
5. Optionally restores Delaunay conformity in non-constrained regions
6. Marks edges and regions with constraint metadata

**Interpolation Query:**

1. Create interpolator via InterpolatorFactory (TriangularFacet, NaturalNeighbor, or IDW)
2. Locate containing triangle using IncrementalTinNavigator
3. Extract vertex Z values and positions
4. Apply interpolation method to compute Z at query point
5. Return interpolated value or NaN if outside bounds/constrained region

**Rasterization:**

1. Create TinRasterizer with interpolation method and options
2. Iterate over raster grid cells
3. For each cell, compute XY bounds and center
4. Create interpolator for cell (thread-local in parallel operation)
5. Query Z values at grid points
6. Store results in chosen RasterData format (Float64, Float32, Int16Scaled)

**State Management:**

- Mutable during construction: IncrementalTin accumulates vertices and edges
- Lock point: `tin.Lock()` transitions to read-only
- Locked state: Immutable for multiple readers, safe for parallel interpolation
- Constraint integration: Constrains topology but doesn't affect lock state
- Z interpolation: Read-only access using existing TIN geometry

## Key Abstractions

**IIncrementalTin:**
- Purpose: Public interface for TIN operations
- Examples: `Tinfour.Core/Common/IIncrementalTin.cs`
- Pattern: Factory pattern via IncrementalTin class; enables mocking and multiple implementations

**IQuadEdge:**
- Purpose: Abstract edge topology navigation
- Examples: `Tinfour.Core/Common/IQuadEdge.cs`, `Tinfour.Core/Edge/QuadEdge.cs`, `Tinfour.Core/Edge/QuadEdgePartner.cs`
- Pattern: Dual-edge representation; forward/reverse navigation; constraint/region metadata

**IInterpolatorOverTin:**
- Purpose: Polymorphic interpolation strategy
- Examples: `Tinfour.Core/Interpolation/TriangularFacetInterpolator.cs`, `NaturalNeighborInterpolator.cs`, `InverseDistanceWeightingInterpolator.cs`
- Pattern: Strategy pattern; factory creation via InterpolatorFactory

**IConstraint:**
- Purpose: Polymorphic constraint types
- Examples: `LinearConstraint`, `PolygonConstraint`
- Pattern: Hierarchy with shared interface; constraint list management

**IIncrementalTinNavigator:**
- Purpose: Encapsulate triangle location and navigation
- Examples: `IncrementalTinNavigator.cs`
- Pattern: Encapsulates Lawson's Walk and edge traversal logic

## Entry Points

**Triangle Building:**
- Location: `Tinfour.Core/Standard/IncrementalTin.cs`
- Triggers: Application creates instance and calls `Add(vertices)` or `Add(vertices, VertexOrder.Hilbert)`
- Responsibilities: Accept vertex stream, maintain Delaunay property, provide edge pool management

**Constraint Integration:**
- Location: `Tinfour.Core/Standard/IncrementalTin.cs` → `ConstraintProcessor.cs`
- Triggers: Application calls `tin.AddConstraints(constraints, restoreConformity: true)`
- Responsibilities: Integrate linear/polygon constraints, restore conformity

**Interpolation Query:**
- Location: `Tinfour.Core/Interpolation/InterpolatorFactory.cs`
- Triggers: Application creates interpolator via `InterpolatorFactory.Create(tin, type, options)`
- Responsibilities: Route to appropriate interpolator implementation, apply options

**Rasterization:**
- Location: `Tinfour.Core/Interpolation/TinRasterizer.cs`
- Triggers: Application calls `rasterizer.CreateRaster(width, height, rasterType)`
- Responsibilities: Iterate grid, interpolate Z values, return RasterResult with statistics

**Refinement:**
- Location: `Tinfour.Core/Refinement/RuppertRefiner.cs`
- Triggers: Application creates refiner with options and calls `Refine(tin)`
- Responsibilities: Insert Steiner points, enforce quality metrics, handle constraints

## Error Handling

**Strategy:** Exceptions for invalid state, geometric failures, and constraint violations; null-safe using NullVertex pattern.

**Patterns:**
- `ArgumentException`: Invalid parameters (e.g., unknown interpolation type)
- `InvalidOperationException`: Operations on unbootstrapped TIN
- `GeometricException` (implied): Degenerate configurations (collinear points, etc.)
- NullVertex sentinel: Represents null/ghost vertices without null-reference exceptions
- NaN values: Indicate out-of-bounds or unconstrained region interpolation

## Cross-Cutting Concerns

**Logging:** Console output for diagnostics (DiagnosticStochasticLawsonsWalk, MeshValidator)

**Validation:** MeshValidator provides topological correctness checks; TriangleCount provides statistics

**Authentication:** Not applicable

**Thread Safety:** Immutable after `Lock()` call; Parallel.ForEach safe for shared locked TIN with thread-local interpolators
