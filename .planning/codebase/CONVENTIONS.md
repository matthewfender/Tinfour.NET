# Coding Conventions

**Analysis Date:** 2026-04-30

## Naming Patterns

**Files:**
- PascalCase for all file names: `Vertex.cs`, `BootstrapUtility.cs`, `LinearConstraint.cs`
- Test files follow pattern: `{ClassName}Tests.cs` (e.g., `BootstrapUtilityTests.cs`, `CircumcircleTests.cs`)
- Namespace directories match file locations: `Common/`, `Constraints/`, `Standard/`, `Interpolation/`, `Edge/`, `Refinement/`, `Contour/`, `Voronoi/`, `Diagnostics/`, `Serialization/`

**Classes and Interfaces:**
- PascalCase for all class names: `Vertex`, `LinearConstraint`, `BootstrapUtility`
- Interface names start with I: `IVertex`, `IConstraint`, `IQuadEdge`, `ILinearConstraint`, `IPolygonConstraint`, `IIncrementalTin`

**Methods:**
- PascalCase for public methods: `GetX()`, `GetZ()`, `WithIndex()`, `IsNullVertex()`, `Contains()`
- Both Getter pattern (`GetX()`) and Property pattern (`X`) are used depending on context
- Methods that check for conditions use `Is` prefix: `IsNullVertex()`, `IsNull()`, `IsSynthetic()`, `IsConstraintMember()`, `IsWithheld()`
- Methods that return modified copies use `With` prefix: `WithIndex()`, `WithSynthetic()`, `WithConstraintMember()`, `WithWithheld()`, `WithStatus()`, `WithAuxiliaryIndex()`
- Property accessor methods use Get prefix: `GetX()`, `GetY()`, `GetZ()`, `GetIndex()`, `GetLabel()`, `GetDistance()`, `GetStatus()`, `GetAuxiliaryIndex()`

**Properties:**
- PascalCase for public properties: `X`, `Y` (coordinates), `NullVertex`, `Null`
- Auto-properties with get-only for immutable data: `public double X { get; }`, `public double Y { get; }`
- Nullable properties use `?`: `private object? _applicationData`, `private double? _defaultZ`, `private IQuadEdge? _linkingEdge`

**Fields and Variables:**
- Private fields use camelCase with underscore prefix: `_nullVertex`, `_z`, `_index`, `_status`, `_auxiliary`, `_geometricOps`, `_random`, `_triangleMinAreaThreshold`
- Local variables use camelCase: `vertices`, `testVertices`, `area`, `unique`, `index`, `attempts`
- Constant fields use UPPER_SNAKE_CASE or PascalCase: `BitSynthetic`, `BitConstraint`, `BitWithheld`, `BitNull`, `NTrialMax`, `NTrialMin`, `TrialFactor`, `MinAreaFactor`
- Static readonly fields use camelCase with underscore: `_nullVertex`, `MinAreaFactor`

**Parameters:**
- camelCase: `x`, `y`, `z`, `index`, `applicationData`, `vertices`, `thresholds`, `linkingEdge`

## Code Style

**Formatting:**
- 4-space indentation (standard C# convention)
- Opening braces on same line: `public void Method() {`
- Max line length: No hard limit observed but generally kept reasonable (~100-120 characters)
- No explicit formatter config found (editorconfig), relies on Visual Studio defaults

**Linting:**
- No explicit linter config found (no .eslintrc equivalent)
- Project uses nullable reference types enabled: `<Nullable>enable</Nullable>` in `.csproj`
- Target framework: .NET 8.0
- Implicit usings enabled: `<ImplicitUsings>enable</ImplicitUsings>`

**Suppressed Warnings:**
- `CS1591`: Missing XML comment for publicly visible type/member
- `CS1573`, `CS1587`, `CS1570`: XML documentation warnings
- `CS8602`, `CS8604`: Nullable dereference warnings (suppressed to allow porting code patterns)
- `CS0618`: Obsolete member warnings (for backward compatibility)
- `CS8600`, `CS8605`, `CS8625`: Null type assignment/conversion warnings
- `CA2013`: Do not use ReferenceEquals (suppressed for null vertex handling)

## Import Organization

**Order:**
1. Using statements for System namespaces: `using System.Runtime.CompilerServices;`
2. Blank line
3. Namespace declaration: `namespace Tinfour.Core.Common;`
4. Blank line
5. Other using statements (if present): `using Xunit;` in test files

**Path Aliases:**
- No path aliases detected. All imports use fully qualified names or global usings.

**Global Usings (Implicit Usings Enabled):**
- Standard .NET runtime types automatically available through implicit usings
- Test projects import `using Xunit;` explicitly

## Exception Handling

**Patterns:**
- Validate null parameters at method entry: `ArgumentNullException.ThrowIfNull(thresholds);` (modern C# pattern)
- Explicit exception throwing for validation: `throw new ArgumentNullException()`, `throw new ArgumentOutOfRangeException()`
- Constructor validation example from `Vertex.WithAuxiliaryIndex()`:
  ```csharp
  if ((auxiliaryIndex & 0xffffff00) != 0)
      throw new ArgumentOutOfRangeException(
          nameof(auxiliaryIndex),
          "Auxiliary index out of valid range [0..255]");
  ```
- Exception documentation in XML comments: `/// <exception cref="ArgumentOutOfRangeException">If the auxiliaryIndex is outside the valid range [0..255]</exception>`
- No try-catch blocks observed in core business logic; exceptions bubble up naturally
- Tests verify exception throwing with `Assert.Throws<ArgumentNullException>()` pattern

## Logging

**Framework:** Not detected. No logging framework (ILogger, Serilog, NLog) found in core library.

**Patterns:**
- No logging detected in Tinfour.Core (pure computation library)
- Applications using the library would implement their own logging
- Progress/cancellation through `IMonitorWithCancellation` interface rather than event logging

## Comments

**When to Comment:**
- File headers with copyright and license (Apache 2.0)
- Revision history blocks documenting date, author, changes
- Public API documentation (method descriptions, parameter docs)
- Complex algorithms or non-obvious mathematical operations
- Notes about Java-to-C# porting decisions: "TEMPORARY: Changed from struct to class to test reference equality issues."

**JSDoc/TSDoc Style (C# XML Comments):**
All public members use XML documentation comments:
```csharp
/// <summary>
///     Represents a point in a connected network on a planar surface.
/// </summary>
/// <remarks>
///     TEMPORARY: Changed from struct to class to test reference equality issues.
///     Original comment: This struct is intentionally implemented with memory efficiency in mind.
/// </remarks>
/// <param name="x">The coordinate on the surface on which the vertex is defined</param>
/// <param name="z">The data value (z coordinate of the surface)</param>
/// <returns>A valid, non-empty string.</returns>
/// <exception cref="ArgumentOutOfRangeException">If the auxiliaryIndex is outside the valid range [0..255]</exception>
```

**Multi-line Comment Blocks:**
```csharp
/*
 * -----------------------------------------------------------------------
 *
 * Revision History:
 * Date     Name        Description
 * ------   ---------   -------------------------------------------------
 * 02/2014  G. Lucas    Created
 * 08/2025 M.Fender    Ported to C# with special null vertex handling
 *
 * Notes:
 * In Java, Vertex was a class and could be null. In C#, we use a struct
 * for memory efficiency but provide a special NullVertex constant to
 * represent null vertices (ghost vertices) in the triangulation.
 * ----------------------------------------------------------------------- */
```

## Function Design

**Size:** Methods generally keep focused responsibilities. Example methods range from 5-30 lines for simple operations to 100+ lines for complex algorithms like `IncrementalTin.Add()` or constraint processing.

**Parameters:** 
- Keep parameter count low (2-4 common, up to 8 for complex algorithms)
- Use objects/structs to bundle related parameters when needed
- Fluent builder pattern used for configuration: `new LinearConstraint(vertices).SetApplicationData(appData)`

**Return Values:**
- Void for operations that modify state in place
- Return type for computed results: `double GetX()`, `string GetLabel()`, `IVertex[]? Bootstrap()`
- Nullable returns `T?` indicate operations that may fail: `Bootstrap()` returns `IVertex[]?`
- Multiple return values through out parameters: `Bootstrap(IList<IVertex> list)` with separate success checks

**Access Modifiers:**
- `public` for library API
- `private` for internal implementation (most fields)
- `internal` for serialization and cross-assembly support
- `sealed` used on concrete implementations to prevent inheritance: `public sealed class Vertex : IVertex`

## Module Design

**Exports:**
- Namespace-based organization: `Tinfour.Core.Common`, `Tinfour.Core.Constraints`, `Tinfour.Core.Standard`
- Public interfaces define contracts: `IVertex`, `IConstraint`, `IQuadEdge`, `ILinearConstraint`
- Concrete classes implement interfaces: `Vertex : IVertex`, `LinearConstraint : ILinearConstraint`

**Barrel Files:** Not used. Direct imports from specific classes required.

**Immutability Patterns:**
- Vertex designed as immutable with `With*` methods returning new instances:
  ```csharp
  public Vertex WithIndex(int index) { return new Vertex(...); }
  public Vertex WithSynthetic(bool synthetic) { return new Vertex(...); }
  ```
- Coordinate properties read-only: `public double X { get; }`, `public double Y { get; }`
- Private mutable fields with no external setters ensure encapsulation

**Struct vs Class Design:**
- Vertex changed from struct to class for reference equality semantics
- CoordinatePair remains struct for lightweight coordinate transfer
- Policy: Use structs for simple value types (coordinates), classes for entities with identity
- Justification in comments explaining Java-to-C# port decisions

## Nullability Handling

**Strategy:**
- Nullable reference types enabled project-wide: `<Nullable>enable</Nullable>`
- Special null vertex constant `Vertex.Null` represents ghost vertices (Java port pattern)
- Methods check nullability: `if (list == null || list.Count < 3) return null;`
- XML docs explicitly state null behavior: "If the z value is Double.NaN then the vertex will be treated as a null data value"
- Extension methods for null testing: `IsEffectivelyNull()`, `OrNull()`

---

*Convention analysis: 2026-04-30*
