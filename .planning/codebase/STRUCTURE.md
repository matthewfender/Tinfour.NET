# Codebase Structure

**Analysis Date:** 2026-04-30

## Directory Layout

```
Tinfour.NET/
├── .planning/              # GSD planning documents
├── .github/                # GitHub workflows
├── doc/                    # Architecture and design documentation
│   ├── architecture/       # Architecture guides
│   ├── design/             # Design decisions
│   ├── development/        # Development guides
│   └── getting-started/    # Usage guides
├── Tinfour.Core/           # Main triangulation library (net8.0)
│   ├── Common/             # Core types and utilities
│   ├── Contour/            # Isoline/contour extraction
│   ├── Diagnostics/        # Validation and diagnostics
│   ├── Edge/               # Quad-edge topological structure
│   ├── Interpolation/      # Interpolation methods and rasterization
│   ├── Refinement/         # Ruppert's refinement algorithm
│   ├── Serialization/      # TIN persistence (binary format)
│   ├── Standard/           # IncrementalTin implementation
│   ├── Utils/              # Utility algorithms
│   └── Voronoi/            # Voronoi diagram generation
├── Tinfour.Core.Tests/     # Unit tests (xUnit)
│   ├── Common/             # Tests for common types
│   ├── Constraints/        # Tests for constraint handling
│   ├── Contour/            # Tests for contour generation
│   ├── Edge/               # Tests for edge operations
│   ├── Interpolation/      # Tests for interpolation methods
│   ├── Refinement/         # Tests for refinement algorithm
│   ├── Serialization/      # Tests for TIN serialization
│   ├── Standard/           # Tests for IncrementalTin
│   ├── Topology/           # Tests for topological correctness
│   ├── Utils/              # Tests for utilities
│   └── Voronoi/            # Tests for Voronoi operations
├── Tinfour.Benchmarks/     # Performance benchmarks (BenchmarkDotNet)
├── Tinfour.PrecisionEval/  # Precision evaluation tools
├── Tinfour.Visualiser/     # Avalonia-based visualization application
│   ├── Tinfour.Visualiser/       # Cross-platform library
│   │   ├── Controls/             # Custom Avalonia controls
│   │   ├── Services/             # Business logic (TIN I/O, etc.)
│   │   ├── ViewModels/           # MVVM view models
│   │   └── Views/                # Avalonia XAML views
│   └── Tinfour.Visualiser.Desktop/  # Desktop entry point
├── Tinfour.Net.sln         # Solution file
└── README.md               # Project overview
```

## Directory Purposes

**Tinfour.Core (Main Library):**
- Purpose: Constrained Delaunay triangulation, interpolation, and analysis
- Contains: 86 C# files organized by functional area
- Key files: `IncrementalTin.cs`, `QuadEdge.cs`, `TriangularFacetInterpolator.cs`

**Tinfour.Core.Tests:**
- Purpose: Comprehensive xUnit test suite
- Contains: Unit and integration tests mirroring Core structure
- Key patterns: Arrange-Act-Assert, fixtures for test data

**Tinfour.Benchmarks:**
- Purpose: Performance profiling and optimization validation
- Contains: BenchmarkDotNet suites for triangulation, interpolation, rasterization
- Run: `dotnet run -c Release` from directory

**Tinfour.PrecisionEval:**
- Purpose: Numerical precision evaluation and validation
- Contains: Test harness for precision-sensitive operations
- Key focus: Extended precision arithmetic validation

**Tinfour.Visualiser:**
- Purpose: Cross-platform desktop visualization of TIN
- Contains: Avalonia UI, MVVM services, custom controls for triangle visualization
- Key dependency: Avalonia, SkiaSharp for rendering

## Key File Locations

**Entry Points:**

- `Tinfour.Core/Standard/IncrementalTin.cs`: Main TIN construction class, implements IIncrementalTin
- `Tinfour.Core/Interpolation/InterpolatorFactory.cs`: Factory for creating interpolators by type
- `Tinfour.Core/Interpolation/TinRasterizer.cs`: Rasterization engine entry point
- `Tinfour.Core/Refinement/RuppertRefiner.cs`: Mesh refinement entry point

**Configuration:**

- `Tinfour.Net.sln`: Solution file defining projects and dependencies
- `Tinfour.Core/Tinfour.Core.csproj`: Library configuration (net8.0, nullable, Clipper2 dependency)
- `Directory.Packages.props`: Centralized NuGet package versions

**Core Logic:**

- `Tinfour.Core/Common/GeometricOperations.cs`: Robust geometric predicates (480+ lines)
- `Tinfour.Core/Common/DoubleDouble.cs`: Extended precision arithmetic for robustness
- `Tinfour.Core/Edge/EdgePool.cs`: Memory management for quad-edge structures
- `Tinfour.Core/Common/StochasticLawsonsWalk.cs`: Point location algorithm
- `Tinfour.Core/Standard/ConstraintProcessor.cs`: Constraint integration logic

**Interpolation Methods:**

- `Tinfour.Core/Interpolation/TriangularFacetInterpolator.cs`: Linear barycentric interpolation
- `Tinfour.Core/Interpolation/NaturalNeighborInterpolator.cs`: Sibson's method (700+ lines)
- `Tinfour.Core/Interpolation/InverseDistanceWeightingInterpolator.cs`: Distance-weighted interpolation

**Analysis & Utilities:**

- `Tinfour.Core/Contour/ContourBuilderForTin.cs`: Isoline extraction
- `Tinfour.Core/Voronoi/BoundedVoronoiDiagram.cs`: Voronoi tessellation
- `Tinfour.Core/Serialization/TinSerializer.cs`: Binary TIN persistence
- `Tinfour.Core/Utils/HilbertSort.cs`: Hilbert curve ordering for insertion
- `Tinfour.Core/Utils/BarycentricCoordinates.cs`: Barycentric coordinate calculation

**Testing:**

- `Tinfour.Core.Tests/Standard/IncrementalTinTests.cs`: Core TIN functionality tests
- `Tinfour.Core.Tests/Interpolation/`: Interpolation method tests
- `Tinfour.Core.Tests/Constraints/`: Constraint handling tests
- `Tinfour.Core.Tests/Topology/`: Topological correctness validation

## Naming Conventions

**Files:**

- `I[Name].cs`: Interface definitions (e.g., `IIncrementalTin.cs`, `IQuadEdge.cs`)
- `[Name]Tests.cs`: Unit test files (e.g., `IncrementalTinTests.cs`)
- `[Name]Benchmarks.cs`: Performance benchmark files (e.g., `InterpolationBenchmarks.cs`)
- `[Name].cs`: Implementation classes matching interface names
- `[Descriptor][Feature].cs`: Composite names (e.g., `EdgePool.cs`, `NearestNeighborPointCollector.cs`)

**Directories:**

- Domain-focused: `Common/`, `Edge/`, `Interpolation/`, `Contour/`, `Voronoi/`, `Utils/`
- Functional area: `Standard/`, `Refinement/`, `Serialization/`, `Diagnostics/`
- Test mirrors: `Tinfour.Core.Tests/Common/`, `Tinfour.Core.Tests/Edge/`, etc.

**Namespaces:**

- Root: `Tinfour.Core`
- Layered: `Tinfour.Core.Common`, `Tinfour.Core.Edge`, `Tinfour.Core.Interpolation`, etc.
- Test namespaces: `Tinfour.Core.Tests.Standard`, `Tinfour.Core.Tests.Interpolation`
- Visualizer: `Tinfour.Visualiser` (separate tree)

## Where to Add New Code

**New Feature in Triangulation Layer:**
- Primary code: `Tinfour.Core/Standard/` (if modifying IncrementalTin) or `Tinfour.Core/Common/` (if shared utility)
- Tests: `Tinfour.Core.Tests/Standard/` or `Tinfour.Core.Tests/Common/`
- Example: Constraint handling added to `Tinfour.Core/Standard/ConstraintProcessor.cs`

**New Interpolation Method:**
- Implementation: `Tinfour.Core/Interpolation/[MethodName]Interpolator.cs`
- Implement: `IInterpolatorOverTin` interface
- Register: Add case to `InterpolatorFactory.Create()` method in `Tinfour.Core/Interpolation/InterpolatorFactory.cs`
- Tests: `Tinfour.Core.Tests/Interpolation/[MethodName]InterpolatorTests.cs`

**New Analysis Feature:**
- Implementation: `Tinfour.Core/[FeatureArea]/[ClassName].cs` (e.g., Contour, Voronoi, Utils)
- Depends on: Query TIN via IIncrementalTin interface
- Tests: `Tinfour.Core.Tests/[FeatureArea]/` mirror
- Example: ContourBuilderForTin in `Tinfour.Core/Contour/`

**Shared Utilities:**
- Location: `Tinfour.Core/Utils/` for algorithms (HilbertSort, BarycentricCoordinates)
- Location: `Tinfour.Core/Common/` for core types and predicates
- Tests: `Tinfour.Core.Tests/Utils/` or `Tinfour.Core.Tests/Common/`

**Visualization Components:**
- UI: `Tinfour.Visualiser/Tinfour.Visualiser/Views/` (XAML files)
- Logic: `Tinfour.Visualiser/Tinfour.Visualiser/ViewModels/` (MVVM)
- Services: `Tinfour.Visualiser/Tinfour.Visualiser/Services/` (TIN I/O, rendering)
- Controls: `Tinfour.Visualiser/Tinfour.Visualiser/Controls/` (custom Avalonia controls)

## Special Directories

**bin/ and obj/:**
- Purpose: Build output (compiled assemblies, metadata)
- Generated: Yes
- Committed: No (in .gitignore)

**.planning/codebase/:**
- Purpose: GSD planning and analysis documents (this ARCHITECTURE.md, STRUCTURE.md)
- Generated: No (hand-written by analysis tools)
- Committed: Yes

**doc/:**
- Purpose: Architecture documentation, design decisions, usage guides
- Generated: No (maintained manually)
- Committed: Yes
- Key files: `doc/architecture/overview.md` (detailed architecture guide)

**.vs/ and .git/:**
- Purpose: IDE cache (Visual Studio) and version control
- Generated: Yes (IDE/git auto-generated)
- Committed: No

**Assets/ (in Visualiser):**
- Purpose: Test data and resources for visualization
- Generated: No (test fixtures)
- Committed: Yes
- Contents: CSV files for test datasets

## File Organization Patterns

**Per-Namespace Folder Organization:**
Each namespace `Tinfour.Core.X` maps directly to folder `Tinfour.Core/X/`. This ensures:
- Clear mapping between files and namespaces
- Easy location of related functionality
- Mirrored test structure (e.g., `Tinfour.Core.Tests/Common/` mirrors `Tinfour.Core/Common/`)

**Interface + Implementation Pattern:**
- Interface: `Tinfour.Core/Common/I[Name].cs` (e.g., `IIncrementalTin.cs`)
- Implementation: `Tinfour.Core/[Location]/[Name].cs` (e.g., `Tinfour.Core/Standard/IncrementalTin.cs`)
- Multiple implementations: Separate files (e.g., `QuadEdge.cs` + `QuadEdgePartner.cs`)

**Factory Pattern:**
- Factory class: `Tinfour.Core/Interpolation/InterpolatorFactory.cs`
- Created types: Concrete interpolator implementations in same folder
- Single responsibility: Factory routes to correct implementation type

**Utility Collections:**
- Algorithms: `Tinfour.Core/Utils/` (HilbertSort, BarycentricCoordinates, etc.)
- Core predicates: `Tinfour.Core/Common/` (GeometricOperations, Thresholds)
- No dedicated "Helpers" folder; utilities live in logical domain
