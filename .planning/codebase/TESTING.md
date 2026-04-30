# Testing Patterns

**Analysis Date:** 2026-04-30

## Test Framework

**Runner:**
- xunit 2.x
- Config: `Tinfour.Core.Tests/Tinfour.Core.Tests.csproj`
- Microsoft.NET.Test.Sdk for test infrastructure

**Assertion Library:**
- xunit's built-in Assert class
- No external assertion libraries (Fluent, Shouldly, etc.)

**Run Commands:**
```bash
dotnet test                    # Run all tests
dotnet test --watch           # Watch mode (if supported)
dotnet test --logger:trx      # Test Results Format output
dotnet test Tinfour.Core.Tests # Run specific project
```

**Code Coverage:**
- coverlet.collector NuGet package installed
- Coverage report generation: `dotnet test /p:CollectCoverage=true`
- No coverage thresholds enforced (CI/CD agnostic)

## Test File Organization

**Location:**
- Parallel structure: Tests co-located in separate test project
- `Tinfour.Core.Tests` project mirrors `Tinfour.Core` structure
- Directory mapping: `Tinfour.Core/Common/` -> `Tinfour.Core.Tests/Common/`

**Naming:**
- Class: `{ComponentName}Tests` (e.g., `BootstrapUtilityTests`, `CircumcircleTests`, `LinearConstraintTests`)
- Test methods: `{MethodName}_{Condition}_{ExpectedResult}` (AAA pattern)
- Examples: 
  - `Constructor_WithValidThresholds_ShouldSucceed()`
  - `Bootstrap_WithTooFewVertices_ShouldReturnNull()`
  - `Compute_WithCollinearPoints_ShouldReturnInfiniteCircumcircle()`
  - `TestInput_WithNullInput_ShouldReturnInsufficientPointSet()`

**Structure:**
```
Tinfour.Core.Tests/
├── BootstrapUtilityTests.cs
├── Common/
│   ├── CircumcircleTests.cs
│   ├── CoordinatePairTests.cs
│   ├── DoubleDoubleTests.cs
│   ├── GeometricOperationsTests.cs
│   ├── LinearUnitsTests.cs
│   ├── PinwheelIteratorTests.cs
│   ├── SimpleTriangleTests.cs
│   ├── TriangleCountTests.cs
│   ├── VertexTests.cs
│   └── ... (other Common tests)
├── Constraints/
│   ├── ConstrainedDelaunayTriangulationTests.cs
│   ├── ConstraintEdgeInsertionTests.cs
│   ├── LinearConstraintTests.cs
│   ├── PolygonConstraintTests.cs
│   └── ... (other Constraint tests)
└── ... (other test categories)
```

## Test Structure

**Suite Organization (Arrange-Act-Assert):**
```csharp
public class BootstrapUtilityTests
{
    [Fact]
    public void Constructor_WithValidThresholds_ShouldSucceed()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);

        // Act & Assert
        var utility = new BootstrapUtility(thresholds);
        Assert.NotNull(utility);
    }

    [Fact]
    public void Bootstrap_WithTooFewVertices_ShouldReturnNull()
    {
        // Arrange
        var thresholds = new Thresholds(1.0);
        var utility = new BootstrapUtility(thresholds);
        var vertices = new List<Vertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(1, 1, 1)
        };

        // Act
        var result = utility.Bootstrap(vertices);

        // Assert
        Assert.Null(result);
    }
}
```

**Patterns:**
- Each test is a `[Fact]` method (xunit attribute)
- Arrange-Act-Assert (AAA) separation with comments for clarity
- When appropriate, combine Act & Assert: `// Act & Assert`
- One assertion focus per test, but multiple asserts allowed for related checks
- Test names document expected behavior clearly

## Mocking

**Framework:** None detected. Tests use real objects.

**Pattern:**
- No mocking library (Moq, NSubstitute) found
- Tests instantiate real objects: `new Thresholds(1.0)`, `new Vertex(0, 0, 0)`, `new GeometricOperations(thresholds)`
- Constructor dependencies injected normally: `new BootstrapUtility(thresholds)`

**What to Mock:**
- Based on observed patterns: Don't mock. Use real implementations.
- This is appropriate for computational geometry library where behavior is deterministic
- External dependencies (if any) are managed through constructors

**What NOT to Mock:**
- Core domain objects: `Vertex`, `Thresholds`, `GeometricOperations`
- These should be tested with real instances to verify correct behavior
- No artificial seams introduced; tests work with actual library code

## Fixtures and Factories

**Test Data:**
```csharp
// Inline vertex creation (most common)
var vertices = new List<Vertex>
{
    new Vertex(0, 0, 0),
    new Vertex(1, 0, 0),
    new Vertex(0.5, 1, 0)
};

// Geometric shapes used repeatedly
// Right triangle: vertices at (0,0), (3,0), (0,4)
var a = new Vertex(0, 0, 0);
var b = new Vertex(3, 0, 0);
var c = new Vertex(0, 4, 0);

// Equilateral triangle centered at origin
var a = new Vertex(1, 0, 0);
var b = new Vertex(-0.5, Math.Sqrt(3) / 2, 0);
var c = new Vertex(-0.5, -Math.Sqrt(3) / 2, 0);

// Grid of vertices (for larger datasets)
var vertices = new List<Vertex>();
for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        vertices.Add(new Vertex(i, j, 0));
    }
}

// Random vertices with fixed seed for reproducibility
var random = new Random(42);
for (int i = 0; i < 100; i++)
{
    vertices.Add(new Vertex(random.NextDouble() * 10, random.NextDouble() * 10, 0));
}
```

**Location:**
- Inline within test methods, no separate fixture files
- Test-specific data created with comments explaining geometry

## Assertion Patterns

**Common Assertions:**
- `Assert.NotNull(utility)` - Verify object creation
- `Assert.Null(result)` - Verify null return for invalid cases
- `Assert.Equal(expected, actual)` - Value equality
- `Assert.Equal(expected, actual, tolerance)` - Floating point comparison with precision
- `Assert.True(condition)` - Boolean assertions
- `Assert.False(condition)` - Negative boolean assertions
- `Assert.Throws<ExceptionType>(() => ...)` - Exception verification
- `Assert.Empty(collection)` - Collection empty check
- `Assert.Contains(item, collection)` - Collection membership
- `Assert.All(collection, assertion)` - Bulk assertion
- `Assert.Equal(expectedCount, actualCount)` - Array/list length

**Floating Point Comparison:**
```csharp
// Tolerance as decimal places (2 = 0.01 tolerance)
Assert.Equal(1.5, circumcircle.GetX(), 3);

// Direct equality for integers
Assert.Equal(3, result.Length);
Assert.Equal(int.MinValue, int.MinValue);
```

## Coverage

**Requirements:** Not enforced. No build-time coverage thresholds detected.

**View Coverage:**
```bash
dotnet test /p:CollectCoverage=true
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

**Test Coverage Gaps Observed:**
- Some test files commented out in `.csproj`: `EdgePoolTests.cs`, `StochasticLawsonsWalkTests.cs`
- Indicates incomplete test coverage for edge management and stochastic walks
- Complex algorithms like Ruppert refinement have basic tests but may have edge case gaps

## Test Types

**Unit Tests:**
- Scope: Individual components (Vertex, LinearConstraint, BootstrapUtility)
- Approach: Direct instantiation, method invocation, assertion of results
- Example: `BootstrapUtilityTests` tests the bootstrap algorithm in isolation
- Examples of test methods from `BootstrapUtilityTests`:
  ```csharp
  [Fact]
  public void Bootstrap_WithValidTriangle_ShouldReturnThreeVertices()
  public void Bootstrap_WithCollinearVertices_ShouldReturnNull()
  public void Bootstrap_WithCoincidentVertices_ShouldReturnNull()
  public void Bootstrap_ShouldEnsurePositiveOrientation()
  public void TestInput_WithValidPoints_ShouldReturnValid()
  public void TestInput_WithCollinearPoints_ShouldReturnCollinearPointSet()
  ```

**Integration Tests:**
- Scope: Multiple components working together (e.g., TIN construction with constraints)
- Framework: xunit (same as unit tests)
- Location: `Constraints/` and `Triangulation/` test folders
- Examples: `ConstrainedDelaunayTriangulationTests`, `ConstraintEdgeInsertionTests`

**E2E Tests:**
- Framework: Not found. No end-to-end automation framework detected.
- Component-level integration is as far as testing reaches
- Visualizer application (`Tinfour.Visualiser`) provides manual visual verification

## Async Testing

**Pattern:** Not used. All tests are synchronous.
- No async/await in test code observed
- Computational geometry operations are CPU-bound, not I/O-bound
- No async patterns in main library code

## Error Testing

**Pattern:**
```csharp
[Fact]
public void Constructor_WithNullThresholds_ShouldThrow()
{
    // Arrange, Act & Assert
    Assert.Throws<ArgumentNullException>(() => new BootstrapUtility(null!));
}

[Fact]
public void Bootstrap_WithNullList_ShouldReturnNull()
{
    // Arrange
    var thresholds = new Thresholds(1.0);
    var utility = new BootstrapUtility(thresholds);

    // Act
    var result = utility.Bootstrap(null!);

    // Assert
    Assert.Null(result);
}
```

**Error Handling Verification:**
- `Assert.Throws<ExceptionType>()` for exception cases
- Null handling tests verify graceful degradation: `ShouldReturnNull()` patterns
- Input validation tested extensively: collinear points, coincident vertices, insufficient data
- Exception messages documented in XML comments on methods being tested

## Test Data Approaches

**Geometric Test Data:**
- Use well-known geometric shapes: right triangles, equilateral triangles, squares
- Expected results calculated and documented in comments:
  ```csharp
  // Right triangle with vertices at (0,0), (3,0), (0,4)
  // Expected circumcenter at (1.5, 2) with radius 2.5
  ```

**Random Data Generation:**
- Fixed seeds for reproducibility: `new Random(42)`
- Large datasets created programmatically: 100+ vertex grids
- Stress tests verify algorithm behavior across data scales

**Edge Cases:**
- Collinear points (degenerate triangles)
- Coincident points (same coordinate)
- Null/NaN values
- Empty or insufficient point sets
- Negative orientations (requiring vertex swapping)

## Test Organization Best Practices

**Per file (from observed patterns):**

1. **One test class per component** (e.g., `BootstrapUtilityTests` for `BootstrapUtility`)
2. **Group related tests together** within the class
3. **AAA pattern consistently applied** with comment sections
4. **Descriptive test names** following `{Method}_{Condition}_{Result}` convention
5. **Minimal setup** - most tests create needed objects inline
6. **No test interdependencies** - each test is fully independent
7. **Assertions focused** - typically 1-3 related assertions per test

**Test file locations follow source structure exactly**, making navigation predictable.

---

*Testing analysis: 2026-04-30*
