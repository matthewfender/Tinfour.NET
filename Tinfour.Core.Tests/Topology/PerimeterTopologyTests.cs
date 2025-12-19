/*
 * Tests to verify perimeter (convex hull) topology integrity,
 * especially when constraints share vertices with the perimeter.
 *
 * This addresses the issue where contour traversal times out when
 * polygon constraints touch the edge of the TIN.
 */

namespace Tinfour.Core.Tests.Topology;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

public class PerimeterTopologyTests
{
    private readonly ITestOutputHelper _output;

    public PerimeterTopologyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Helper Methods

    /// <summary>
    ///     Validates perimeter topology integrity using safe traversal with iteration limit.
    /// </summary>
    private (bool isValid, string error) ValidatePerimeter(IIncrementalTin tin)
    {
        if (!tin.IsBootstrapped())
            return (false, "TIN is not bootstrapped");

        // Count ghost edges (edges where B is null vertex)
        var ghostEdges = new List<IQuadEdge>();
        foreach (var edge in tin.GetEdges())
            if (edge.GetB().IsNullVertex())
                ghostEdges.Add(edge);

        if (ghostEdges.Count == 0)
            return (false, "No ghost edges found");

        // Use safe traversal instead of GetPerimeter to avoid infinite loops
        var (perimeter, iterations, completed) = SafeTraversePerimeter(tin, ghostEdges);

        if (!completed)
            return (false, $"Perimeter traversal did not complete after {iterations} iterations - infinite loop detected");

        if (perimeter.Count == 0)
            return (false, "Perimeter traversal returned empty list");

        // Verify count matches
        if (perimeter.Count != ghostEdges.Count)
            return (false,
                $"Perimeter edge count ({perimeter.Count}) doesn't match ghost edge count ({ghostEdges.Count})");

        // Verify positive area (CCW winding)
        var area = CalculateSignedArea(perimeter);
        if (area <= 0)
            return (false, $"Perimeter has non-positive area ({area:F6}) - wrong winding or degenerate");

        // Verify all perimeter edges have valid vertices
        foreach (var edge in perimeter)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            if (a.IsNullVertex())
                return (false, $"Perimeter edge {edge.GetIndex()} has null A vertex");

            if (b.IsNullVertex())
                return (false, $"Perimeter edge {edge.GetIndex()} has null B vertex");
        }

        return (true, string.Empty);
    }

    /// <summary>
    ///     Calculates the signed area of a polygon from a list of edges.
    /// </summary>
    private static double CalculateSignedArea(IList<IQuadEdge> perimeter)
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

    /// <summary>
    /// Safe perimeter traversal with iteration limit - returns partial results instead of hanging.
    /// </summary>
    private (List<IQuadEdge> edges, int iterations, bool completed) SafeTraversePerimeter(
        IIncrementalTin tin, List<IQuadEdge>? ghostEdges = null)
    {
        var result = new List<IQuadEdge>();

        if (!tin.IsBootstrapped())
            return (result, 0, false);

        // Get ghost edges if not provided
        if (ghostEdges == null)
        {
            ghostEdges = new List<IQuadEdge>();
            foreach (var edge in tin.GetEdges())
                if (edge.GetB().IsNullVertex())
                    ghostEdges.Add(edge);
        }

        if (ghostEdges.Count == 0)
            return (result, 0, false);

        // Find starting ghost edge
        var startGhost = ghostEdges[0];
        var s0 = startGhost.GetReverse();
        var s = s0;

        // Limit iterations to prevent infinite loops
        var maxIterations = ghostEdges.Count * 2 + 100;
        var iterations = 0;

        do
        {
            if (++iterations > maxIterations)
                return (result, iterations, false); // Did not complete - infinite loop detected

            result.Add(s.GetDual());
            s = s.GetForward().GetForward().GetDual().GetReverse();
        }
        while (s != s0);

        return (result, iterations, true);
    }

    /// <summary>
    ///     Creates a simple TIN with vertices in a square.
    /// </summary>
    private static IncrementalTin CreateSimpleTin(int vertexCount = 10)
    {
        var tin = new IncrementalTin();
        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < vertexCount; i++)
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10, i));

        tin.Add(vertices);
        return tin;
    }

    /// <summary>
    ///     Creates a TIN where a constraint vertex coincides with a perimeter vertex.
    /// </summary>
    private static IncrementalTin CreateTinWithPerimeterConstraint()
    {
        var tin = new IncrementalTin();

        // Create vertices forming a convex hull
        // The rightmost vertex will be shared with the constraint
        var vertices = new List<Vertex>
        {
            new(0, 0, 0, 0),
            new(100, 0, 0, 1), // Perimeter vertex - will be shared
            new(100, 100, 0, 2), // Perimeter vertex - will be shared
            new(0, 100, 0, 3),
            new(50, 50, 5, 4) // Interior vertex
        };
        tin.Add(vertices);

        // Constraint that shares vertices with perimeter (right edge)
        var constraintVertices = new List<Vertex>
        {
            new(100, 0, 0, 100), // Same location as vertex 1
            new(100, 100, 0, 101), // Same location as vertex 2
            new(70, 50, 3, 102) // Interior constraint vertex
        };

        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        return tin;
    }

    /// <summary>
    ///     Creates a more complex TIN simulating a water body touching the survey edge.
    /// </summary>
    private static IncrementalTin CreateWaterBodyTouchingPerimeter()
    {
        var tin = new IncrementalTin();

        // Create main terrain vertices
        var vertices = new List<Vertex>();
        var rand = new Random(42);
        for (var i = 0; i < 50; i++) vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, 10, i));

        // Add some vertices at the edges to define the perimeter clearly
        vertices.Add(new Vertex(0, 0, 10, 100));
        vertices.Add(new Vertex(100, 0, 10, 101));
        vertices.Add(new Vertex(100, 100, 10, 102));
        vertices.Add(new Vertex(0, 100, 10, 103));

        tin.Add(vertices);

        // Water body polygon that touches the right edge of the survey
        var waterBody = new List<Vertex>
        {
            new(85, 30, 0, 200),
            new(100, 30, 0, 201), // On perimeter
            new(100, 70, 0, 202), // On perimeter
            new(85, 70, 0, 203),
            new(80, 50, 0, 204)
        };

        var constraint = new PolygonConstraint(waterBody);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        return tin;
    }

    #endregion

    #region Basic Perimeter Tests

    /// <summary>
    ///     Test that a simple TIN has valid perimeter topology.
    /// </summary>
    [Fact]
    public void Perimeter_SimpleTin_ShouldBeTraversable()
    {
        var tin = CreateSimpleTin();

        var (isValid, error) = ValidatePerimeter(tin);

        _output.WriteLine($"Perimeter validation: {(isValid ? "PASS" : "FAIL")}");
        if (!isValid) _output.WriteLine($"Error: {error}");

        var perimeter = tin.GetPerimeter();
        _output.WriteLine($"Perimeter edge count: {perimeter.Count}");

        Assert.True(isValid, error);
    }

    /// <summary>
    ///     Test perimeter after adding a constraint that shares vertices with perimeter.
    /// </summary>
    [Fact]
    public void Perimeter_WithSharedConstraintVertex_ShouldBeTraversable()
    {
        var tin = CreateTinWithPerimeterConstraint();

        var (isValid, error) = ValidatePerimeter(tin);

        _output.WriteLine($"Perimeter validation: {(isValid ? "PASS" : "FAIL")}");
        if (!isValid) _output.WriteLine($"Error: {error}");

        var perimeter = tin.GetPerimeter();
        _output.WriteLine($"Perimeter edge count: {perimeter.Count}");

        Assert.True(isValid, error);
    }

    /// <summary>
    ///     Test perimeter with water body constraint touching survey edge.
    /// </summary>
    [Fact]
    public void Perimeter_WaterBodyTouchingEdge_ShouldBeTraversable()
    {
        var tin = CreateWaterBodyTouchingPerimeter();

        var (isValid, error) = ValidatePerimeter(tin);

        _output.WriteLine($"Perimeter validation: {(isValid ? "PASS" : "FAIL")}");
        if (!isValid) _output.WriteLine($"Error: {error}");

        var perimeter = tin.GetPerimeter();
        _output.WriteLine($"Perimeter edge count: {perimeter.Count}");

        Assert.True(isValid, error);
    }

    #endregion

    #region Perimeter After Ruppert Refinement

    /// <summary>
    ///     Test perimeter topology after Ruppert refinement with constraint sharing vertices.
    /// </summary>
    [Fact]
    public void Perimeter_AfterRuppert_WithSharedVertex_ShouldBeTraversable()
    {
        var tin = CreateTinWithPerimeterConstraint();

        // Validate before refinement
        var (validBefore, errorBefore) = ValidatePerimeter(tin);
        _output.WriteLine($"Before Ruppert: {(validBefore ? "PASS" : "FAIL")}");
        if (!validBefore) _output.WriteLine($"  Error: {errorBefore}");
        Assert.True(validBefore, $"Perimeter invalid BEFORE Ruppert: {errorBefore}");

        // Run Ruppert refinement - use MinimumTriangleArea to limit refinement on small test terrains
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            MinimumTriangleArea = 5.0 // Prevent excessive refinement on small test terrain
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"After Ruppert: {tin.GetVertices().Count} vertices");

        // Validate after refinement
        var (validAfter, errorAfter) = ValidatePerimeter(tin);
        _output.WriteLine($"After Ruppert: {(validAfter ? "PASS" : "FAIL")}");
        if (!validAfter) _output.WriteLine($"  Error: {errorAfter}");

        Assert.True(validAfter, $"Perimeter invalid AFTER Ruppert: {errorAfter}");
    }

    /// <summary>
    ///     Test perimeter topology after Ruppert refinement with water body touching edge.
    /// </summary>
    [Fact]
    public void Perimeter_AfterRuppert_WaterBody_ShouldBeTraversable()
    {
        var tin = CreateWaterBodyTouchingPerimeter();

        // Validate before refinement
        var (validBefore, errorBefore) = ValidatePerimeter(tin);
        _output.WriteLine($"Before Ruppert: {(validBefore ? "PASS" : "FAIL")}");
        if (!validBefore) _output.WriteLine($"  Error: {errorBefore}");
        Assert.True(validBefore, $"Perimeter invalid BEFORE Ruppert: {errorBefore}");

        // Run Ruppert refinement - use MinimumTriangleArea to limit refinement on small test terrains
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            MinimumTriangleArea = 5.0 // Prevent excessive refinement on small test terrain
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"After Ruppert: {tin.GetVertices().Count} vertices");

        // Validate after refinement
        var (validAfter, errorAfter) = ValidatePerimeter(tin);
        _output.WriteLine($"After Ruppert: {(validAfter ? "PASS" : "FAIL")}");
        if (!validAfter) _output.WriteLine($"  Error: {errorAfter}");

        Assert.True(validAfter, $"Perimeter invalid AFTER Ruppert: {errorAfter}");
    }

    #endregion

    #region Edge Link Consistency Tests

    /// <summary>
    ///     Test that all perimeter edges have consistent forward/reverse links.
    /// </summary>
    [Fact]
    public void PerimeterEdges_ForwardReverseLinks_ShouldBeConsistent()
    {
        var tin = CreateTinWithPerimeterConstraint();

        var perimeter = tin.GetPerimeter();
        var errors = new List<string>();

        foreach (var edge in perimeter)
        {
            // The perimeter edges are the interior side - their dual points outward to ghost
            var dual = edge.GetDual();

            // The dual's B vertex should be ghost (null vertex)
            // NOTE: GetPerimeter returns edges where the DUAL has null B vertex
            // So edge.GetDual().GetB() should be null
            if (!dual.GetB().IsNullVertex())
            {
                // This is expected - perimeter edges are interior facing
                // The dual should have A = real vertex, B = ghost vertex
                // But wait, let's check the actual structure...
            }

            // Check forward/reverse consistency
            var forward = edge.GetForward();
            var reverse = edge.GetReverse();

            if (forward.GetReverse() != edge)
                errors.Add($"Edge {edge.GetIndex()}: forward.reverse != self");

            if (reverse.GetForward() != edge)
                errors.Add($"Edge {edge.GetIndex()}: reverse.forward != self");

            // Also verify the edge's vertices are valid (not ghost)
            if (edge.GetA().IsNullVertex())
                errors.Add($"Edge {edge.GetIndex()}: A vertex is ghost (should be real)");

            if (edge.GetB().IsNullVertex())
                errors.Add($"Edge {edge.GetIndex()}: B vertex is ghost (should be real)");
        }

        foreach (var error in errors.Take(10)) _output.WriteLine(error);

        Assert.Empty(errors);
    }

    /// <summary>
    ///     Test perimeter edge link consistency after Ruppert refinement.
    /// </summary>
    [Fact]
    public void PerimeterEdges_AfterRuppert_LinksConsistent()
    {
        var tin = CreateWaterBodyTouchingPerimeter();

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        var perimeter = tin.GetPerimeter();
        var errors = new List<string>();

        foreach (var edge in perimeter)
        {
            var forward = edge.GetForward();
            var reverse = edge.GetReverse();

            if (forward.GetReverse() != edge)
                errors.Add($"Edge {edge.GetIndex()}: forward.reverse != self");

            if (reverse.GetForward() != edge)
                errors.Add($"Edge {edge.GetIndex()}: reverse.forward != self");

            // Verify the edge's vertices are valid (not ghost)
            if (edge.GetA().IsNullVertex())
                errors.Add($"Edge {edge.GetIndex()}: A vertex is ghost (should be real)");

            if (edge.GetB().IsNullVertex())
                errors.Add($"Edge {edge.GetIndex()}: B vertex is ghost (should be real)");
        }

        foreach (var error in errors.Take(10)) _output.WriteLine(error);

        Assert.Empty(errors);
    }

    #endregion

    #region Minimal Reproduction Tests

    /// <summary>
    ///     Minimal test: 4 vertices forming a square with constraint on one edge.
    ///     This is the simplest case that might trigger the perimeter issue.
    /// </summary>
    [Fact]
    public void MinimalCase_SquareWithEdgeConstraint()
    {
        var tin = new IncrementalTin();

        // Simple square
        var vertices = new List<Vertex>
        {
            new(0, 0, 0, 0),
            new(10, 0, 0, 1),
            new(10, 10, 0, 2),
            new(0, 10, 0, 3)
        };
        tin.Add(vertices);

        _output.WriteLine("=== BEFORE CONSTRAINT ===");
        var (validBefore, errorBefore) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {validBefore}, Error: {errorBefore}");
        _output.WriteLine($"Perimeter count: {tin.GetPerimeter().Count}");

        // Constraint along the right edge (shares vertices 1 and 2)
        var constraintVertices = new List<Vertex>
        {
            new(10, 0, 0, 100), // Same as vertex 1
            new(10, 10, 0, 101), // Same as vertex 2
            new(7, 5, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine("=== AFTER CONSTRAINT ===");
        var (validAfter, errorAfter) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {validAfter}, Error: {errorAfter}");
        _output.WriteLine($"Perimeter count: {tin.GetPerimeter().Count}");
        _output.WriteLine($"Vertex count: {tin.GetVertices().Count}");

        Assert.True(validBefore, $"Before constraint: {errorBefore}");
        Assert.True(validAfter, $"After constraint: {errorAfter}");
    }

    /// <summary>
    ///     Minimal test: Triangle constraint sharing one edge with perimeter.
    /// </summary>
    [Fact]
    public void MinimalCase_TriangleConstraintOnPerimeter()
    {
        var tin = new IncrementalTin();

        // Simple square with interior point
        var vertices = new List<Vertex>
        {
            new(0, 0, 0, 0),
            new(10, 0, 0, 1),
            new(10, 10, 0, 2),
            new(0, 10, 0, 3),
            new(5, 5, 5, 4) // Interior
        };
        tin.Add(vertices);

        // Triangle constraint with one edge on perimeter
        var constraintVertices = new List<Vertex>
        {
            new(0, 0, 0, 100), // Same as vertex 0
            new(10, 0, 0, 101), // Same as vertex 1
            new(5, 3, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        var (isValid, error) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {isValid}, Error: {error}");

        Assert.True(isValid, error);
    }

    /// <summary>
    ///     Minimal test with Ruppert refinement on the simplest case.
    /// </summary>
    [Fact]
    public void MinimalCase_SquareWithConstraint_ThenRuppert()
    {
        var tin = new IncrementalTin();

        var vertices = new List<Vertex>
        {
            new(0, 0, 0, 0),
            new(10, 0, 0, 1),
            new(10, 10, 0, 2),
            new(0, 10, 0, 3),
            new(5, 5, 5, 4)
        };
        tin.Add(vertices);

        var constraintVertices = new List<Vertex>
        {
            new(10, 0, 0, 100),
            new(10, 10, 0, 101),
            new(7, 5, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine("=== BEFORE RUPPERT ===");
        var (validBefore, errorBefore) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {validBefore}");
        if (!validBefore) _output.WriteLine($"Error: {errorBefore}");

        // Run Ruppert - use MinimumTriangleArea to limit refinement on small test terrains
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 20, // Lower angle to reduce iterations
            MaxIterations = 100,
            MinimumTriangleArea = 5.0 // Prevent excessive refinement on small test terrain
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("=== AFTER RUPPERT ===");
        _output.WriteLine($"Vertices: {tin.GetVertices().Count}");

        var (validAfter, errorAfter) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {validAfter}");
        if (!validAfter) _output.WriteLine($"Error: {errorAfter}");

        Assert.True(validBefore, $"Before Ruppert: {errorBefore}");
        Assert.True(validAfter, $"After Ruppert: {errorAfter}");
    }

    #endregion

    #region Detailed Diagnostic Tests

    /// <summary>
    ///     Detailed diagnostic to find exactly where perimeter breaks.
    /// </summary>
    [Fact]
    public void DiagnosePerimeterBreakpoint()
    {
        var tin = CreateWaterBodyTouchingPerimeter();

        _output.WriteLine("=== INITIAL STATE ===");
        var perimeter1 = tin.GetPerimeter();
        _output.WriteLine($"Perimeter edges: {perimeter1.Count}");

        // Count ghost edges manually
        var ghostCount = 0;
        foreach (var edge in tin.GetEdges())
            if (edge.GetB().IsNullVertex())
            {
                ghostCount++;
                if (ghostCount <= 10)
                {
                    var dual = edge.GetDual();
                    _output.WriteLine(
                        $"  Ghost edge {edge.GetIndex()}: A=({edge.GetA().X:F1},{edge.GetA().Y:F1}), " +
                        $"Dual={dual.GetIndex()}, DualA=({dual.GetA().X:F1},{dual.GetA().Y:F1}), DualB=({dual.GetB().X:F1},{dual.GetB().Y:F1})");
                }
            }

        _output.WriteLine($"Total ghost edges: {ghostCount}");

        // Run Ruppert with verbose output
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);

        _output.WriteLine("\n=== RUNNING RUPPERT ===");
        refiner.Refine();
        _output.WriteLine($"Vertices after: {tin.GetVertices().Count}");

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        try
        {
            var perimeter2 = tin.GetPerimeter();
            _output.WriteLine($"Perimeter edges: {perimeter2.Count}");
        }
        catch (InvalidOperationException ex)
        {
            _output.WriteLine($"GetPerimeter() FAILED: {ex.Message}");
        }

        // Count ghost edges after
        var ghostCountAfter = 0;
        foreach (var edge in tin.GetEdges())
            if (edge.GetB().IsNullVertex())
                ghostCountAfter++;

        _output.WriteLine($"Ghost edges after: {ghostCountAfter}");

        var (isValid, error) = ValidatePerimeter(tin);
        _output.WriteLine($"Validation: {(isValid ? "PASS" : "FAIL")} - {error}");

        Assert.True(isValid, error);
    }

    #endregion

    #region Detailed Diagnostic Tests

    /// <summary>
    ///     Find the absolute minimum vertex count that triggers the issue.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(30)]
    public void DIAGNOSTIC_FindMinimumVertexCount(int vertexCount)
    {
        var tin = new IncrementalTin();

        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < vertexCount; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        // Add corner vertices
        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        // Constraint touching perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000),
            new(100, 60, 0, 2001),
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"Initial vertices: {tin.GetVertices().Count}");

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            InterpolateZ = true,
            MinimumTriangleArea = 5.0 // 100x100 terrain
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"Final vertices: {tin.GetVertices().Count}");

        var (isValid, error) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {isValid}, Error: {error}");

        Assert.True(isValid, $"vertexCount={vertexCount}: {error}");
    }

    /// <summary>
    ///     Extremely minimal case - just corner vertices + constraint.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_MinimalCornersPlusConstraint()
    {
        var tin = new IncrementalTin();

        // Just the 4 corners
        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1),
            new(100, 100, 10, 2),
            new(0, 100, 10, 3)
        };
        tin.Add(vertices);

        _output.WriteLine($"After adding corners: {tin.GetVertices().Count} vertices");
        var (valid1, err1) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid1}");

        // Constraint touching perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 100),
            new(100, 60, 0, 101),
            new(90, 50, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"After constraint: {tin.GetVertices().Count} vertices");
        var (valid2, err2) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid2}, Error: {err2}");

        // Run Ruppert
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 100,
            InterpolateZ = true,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"After Ruppert: {tin.GetVertices().Count} vertices");
        var (valid3, err3) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid3}, Error: {err3}");

        Assert.True(valid3, err3);
    }

    /// <summary>
    ///     Step through Ruppert one iteration at a time to find corruption point.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_StepThroughRuppert()
    {
        var tin = new IncrementalTin();

        // Small vertex set
        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < 20; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000),
            new(100, 60, 0, 2001),
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"Initial: {tin.GetVertices().Count} vertices");
        var (valid0, _) = ValidatePerimeter(tin);
        _output.WriteLine($"Initial perimeter valid: {valid0}");
        Assert.True(valid0, "Initial perimeter should be valid");

        // Run Ruppert with very low max iterations to step through
        for (var step = 1; step <= 50; step++)
        {
            var options = new RuppertOptions
            {
                MinimumAngleDegrees = 25,
                MaxIterations = step,
                InterpolateZ = true,
                MinimumTriangleArea = 5.0
            };

            // Create fresh TIN for each step
            var stepTin = new IncrementalTin();
            stepTin.Add(vertices);
            stepTin.AddConstraints(new List<IConstraint> { new PolygonConstraint(constraintVertices) }, true);

            var refiner = new RuppertRefiner(stepTin, options);
            refiner.Refine();

            var (isValid, error) = ValidatePerimeter(stepTin);
            var vertexCount = stepTin.GetVertices().Count;

            if (!isValid)
            {
                _output.WriteLine($"CORRUPTION at step {step}: {vertexCount} vertices, Error: {error}");
                Assert.Fail($"Perimeter corrupted at step {step}: {error}");
                return;
            }

            if (step % 10 == 0)
                _output.WriteLine($"Step {step}: {vertexCount} vertices - OK");
        }

        _output.WriteLine("All 50 steps passed");
    }

    /// <summary>
    ///     Detailed edge analysis before and after each Ruppert iteration.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_DetailedEdgeAnalysis()
    {
        var tin = new IncrementalTin();

        // Very small set
        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1),
            new(100, 100, 10, 2),
            new(0, 100, 10, 3),
            new(50, 50, 15, 4) // One interior point
        };
        tin.Add(vertices);

        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 100),
            new(100, 60, 0, 101),
            new(85, 50, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine("=== INITIAL STATE ===");
        DumpPerimeterState(tin);

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 50,
            InterpolateZ = true,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        DumpPerimeterState(tin);

        var (isValid, error) = ValidatePerimeter(tin);
        Assert.True(isValid, error);
    }

    /// <summary>
    ///     Create a skinny triangle ON the perimeter edge that will force a split.
    ///     This is the most likely scenario to trigger the corruption.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_SkinnyTriangleOnPerimeterEdge()
    {
        var tin = new IncrementalTin();

        // Create a configuration where there's a very skinny triangle
        // with one edge on the perimeter (x=100 edge)
        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1), // Perimeter corner
            new(100, 100, 10, 2), // Perimeter corner
            new(0, 100, 10, 3),
            // Add a vertex very close to the right edge to create skinny triangle
            new(99, 50, 10, 4) // This creates skinny triangles along x=100 edge
        };
        tin.Add(vertices);

        _output.WriteLine("=== INITIAL (no constraint) ===");
        DumpPerimeterState(tin);
        var (valid1, err1) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid1}");
        Assert.True(valid1, $"Initial perimeter invalid: {err1}");

        // Add constraint that touches the perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 30, 0, 100), // On perimeter
            new(100, 70, 0, 101), // On perimeter
            new(95, 50, 0, 102) // Interior
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine("\n=== AFTER CONSTRAINT ===");
        DumpPerimeterState(tin);
        var (valid2, err2) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid2}");
        Assert.True(valid2, $"Perimeter invalid after constraint: {err2}");

        // Run Ruppert - this should trigger edge splits on skinny triangles
        // For small test TINs (100x100), use a larger min triangle area to prevent excessive refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 50,
            InterpolateZ = true,
            MinimumTriangleArea = 10.0 // Large area threshold for small test terrain
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        DumpPerimeterState(tin);
        var (valid3, err3) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid3}");

        Assert.True(valid3, $"Perimeter invalid after Ruppert: {err3}");
    }

    /// <summary>
    ///     Create an even skinnier triangle that MUST be split.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_ForcedPerimeterEdgeSplit()
    {
        var tin = new IncrementalTin();

        // Create a very skinny triangle where one edge is ON the perimeter
        // The triangle has vertices: (100,40), (100,60), (99.5, 50)
        // This creates a very small angle at (99.5, 50)
        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1),
            new(100, 100, 10, 2),
            new(0, 100, 10, 3),
            new(99.5, 50, 10, 4) // Very close to right edge - creates skinny triangle
        };
        tin.Add(vertices);

        _output.WriteLine("=== INITIAL ===");
        _output.WriteLine($"Vertices: {tin.GetVertices().Count}");

        // List all triangles
        _output.WriteLine("Triangles:");
        foreach (var tri in tin.GetTriangles())
        {
            var a = tri.GetVertexA();
            var b = tri.GetVertexB();
            var c = tri.GetVertexC();
            if (!a.IsNullVertex() && !b.IsNullVertex() && !c.IsNullVertex())
            {
                // Calculate minimum angle
                var minAngle = CalculateMinAngle(a.X, a.Y, b.X, b.Y, c.X, c.Y);
                _output.WriteLine($"  ({a.X:F1},{a.Y:F1})-({b.X:F1},{b.Y:F1})-({c.X:F1},{c.Y:F1}) minAngle={minAngle:F1}°");
            }
        }

        var (valid1, err1) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid1}");

        // NO constraint for this test - just test if edge split on perimeter works
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 20, // Lower threshold
            MaxIterations = 10,
            InterpolateZ = true,
            RefineOnlyInsideConstraints = false, // Refine ALL triangles
            AddBoundingBoxConstraint = false,
            MinimumTriangleArea = 5.0
        };

        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        _output.WriteLine($"Vertices: {tin.GetVertices().Count}");

        var (valid2, err2) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid2}, Error: {err2}");

        Assert.True(valid2, $"Perimeter invalid after Ruppert: {err2}");
    }

    /// <summary>
    ///     Test splitting a single perimeter edge directly (bypassing Ruppert).
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_DirectPerimeterEdgeSplit()
    {
        var tin = new IncrementalTin();

        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1),
            new(100, 100, 10, 2),
            new(0, 100, 10, 3)
        };
        tin.Add(vertices);

        _output.WriteLine("=== INITIAL ===");
        DumpPerimeterState(tin);
        var (valid1, _) = ValidatePerimeter(tin);
        Assert.True(valid1, "Initial perimeter should be valid");

        // Find a perimeter edge and split it directly
        var perimeter = tin.GetPerimeter();
        _output.WriteLine($"\nFound {perimeter.Count} perimeter edges");

        // Find edge on x=100 (right side)
        IQuadEdge? targetEdge = null;
        foreach (var edge in perimeter)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            if (Math.Abs(a.X - 100) < 0.1 && Math.Abs(b.X - 100) < 0.1)
            {
                targetEdge = edge;
                _output.WriteLine($"Found right-side perimeter edge: {edge.GetIndex()} from ({a.X},{a.Y}) to ({b.X},{b.Y})");
                break;
            }
        }

        if (targetEdge == null)
        {
            _output.WriteLine("Could not find right-side perimeter edge");
            return;
        }

        // Add a vertex on this edge to force a split
        var midX = (targetEdge.GetA().X + targetEdge.GetB().X) / 2;
        var midY = (targetEdge.GetA().Y + targetEdge.GetB().Y) / 2;
        var newVertex = new Vertex(midX, midY, 10, 999);

        _output.WriteLine($"\nAdding vertex at ({midX}, {midY})");
        tin.Add(new List<Vertex> { newVertex });

        _output.WriteLine("\n=== AFTER ADDING VERTEX ON PERIMETER ===");
        DumpPerimeterState(tin);

        var (valid2, err2) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid2}");

        Assert.True(valid2, $"Perimeter invalid after adding vertex: {err2}");
    }

    /// <summary>
    ///     Test with constraint that has an edge exactly ON the perimeter.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_ConstraintEdgeOnPerimeter()
    {
        var tin = new IncrementalTin();

        var vertices = new List<Vertex>
        {
            new(0, 0, 10, 0),
            new(100, 0, 10, 1),
            new(100, 100, 10, 2),
            new(0, 100, 10, 3),
            new(50, 50, 15, 4)
        };
        tin.Add(vertices);

        _output.WriteLine("=== INITIAL ===");
        var (valid1, _) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid1}");

        // Constraint with TWO vertices on the right perimeter edge
        // This means one constraint edge lies exactly on the perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 30, 0, 100), // On perimeter
            new(100, 70, 0, 101), // On perimeter - edge (100,30)-(100,70) is ON perimeter
            new(90, 50, 0, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine("\n=== AFTER CONSTRAINT ===");
        DumpPerimeterState(tin);
        var (valid2, err2) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid2}");

        // Now run Ruppert
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 50,
            InterpolateZ = true,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        DumpPerimeterState(tin);
        var (valid3, err3) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {valid3}");

        Assert.True(valid3, $"Perimeter invalid after Ruppert: {err3}");
    }

    private static double CalculateMinAngle(double ax, double ay, double bx, double by, double cx, double cy)
    {
        // Calculate the three angles of the triangle
        var ab = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        var bc = Math.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by));
        var ca = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));

        // Angle at A
        var cosA = (ab * ab + ca * ca - bc * bc) / (2 * ab * ca);
        var angleA = Math.Acos(Math.Clamp(cosA, -1, 1)) * 180 / Math.PI;

        // Angle at B
        var cosB = (ab * ab + bc * bc - ca * ca) / (2 * ab * bc);
        var angleB = Math.Acos(Math.Clamp(cosB, -1, 1)) * 180 / Math.PI;

        // Angle at C
        var angleC = 180 - angleA - angleB;

        return Math.Min(angleA, Math.Min(angleB, angleC));
    }

    /// <summary>
    ///     Dumps detailed perimeter state for debugging.
    /// </summary>
    private void DumpPerimeterState(IIncrementalTin tin)
    {
        _output.WriteLine($"Total vertices: {tin.GetVertices().Count}");
        _output.WriteLine($"Total edges: {tin.GetEdges().Count}");

        // Count ghost edges
        var ghostEdges = new List<IQuadEdge>();
        foreach (var edge in tin.GetEdges())
            if (edge.GetB().IsNullVertex())
                ghostEdges.Add(edge);

        _output.WriteLine($"Ghost edges: {ghostEdges.Count}");

        // Dump ghost edge details
        foreach (var ghost in ghostEdges.Take(20))
        {
            var dual = ghost.GetDual();
            var a = ghost.GetA();
            _output.WriteLine($"  Ghost {ghost.GetIndex()}: A=({a.X:F1},{a.Y:F1}) " +
                              $"Dual={dual.GetIndex()} DualA=({dual.GetA().X:F1},{dual.GetA().Y:F1}) " +
                              $"DualB=({dual.GetB().X:F1},{dual.GetB().Y:F1})");
        }

        // DON'T call GetPerimeter() here as it can hang - use SafeTraversePerimeter instead
        var (perimeterEdges, iterations, completed) = SafeTraversePerimeter(tin, ghostEdges);
        _output.WriteLine($"Perimeter traversal: {perimeterEdges.Count} edges, {iterations} iterations, completed={completed}");

        if (perimeterEdges.Count > 0)
        {
            foreach (var p in perimeterEdges.Take(20))
                _output.WriteLine($"  Perimeter {p.GetIndex()}: " +
                                  $"A=({p.GetA().X:F1},{p.GetA().Y:F1}) -> " +
                                  $"B=({p.GetB().X:F1},{p.GetB().Y:F1}) " +
                                  $"Border={p.IsConstraintRegionBorder()} " +
                                  $"Constrained={p.IsConstrained()}");
        }

        // Check for constraint edges on perimeter
        _output.WriteLine("\nConstraint edges:");
        var constraintCount = 0;
        foreach (var edge in tin.GetEdges())
        {
            if (edge.IsConstrained() && constraintCount < 20)
            {
                var a = edge.GetA();
                var b = edge.GetB();
                if (!a.IsNullVertex() && !b.IsNullVertex())
                {
                    _output.WriteLine($"  Constrained {edge.GetIndex()}: " +
                                      $"({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1}) " +
                                      $"Border={edge.IsConstraintRegionBorder()}");
                    constraintCount++;
                }
            }
        }
    }

    #endregion

    #region Issue Reproduction Tests

    /// <summary>
    ///     FAILING TEST: This reproduces the perimeter topology corruption issue.
    ///     The test creates a TIN with a constraint touching the perimeter,
    ///     runs Ruppert refinement, and verifies perimeter integrity.
    /// </summary>
    [Fact]
    public void ISSUE_PerimeterCorruption_LargeTinWithPerimeterConstraint()
    {
        var tin = new IncrementalTin();

        // Create terrain with random vertices
        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < 200; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + Math.Sin(x / 10) * 5 + Math.Cos(y / 10) * 3 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        // Add corner vertices to ensure perimeter is defined
        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        // Add constraint that shares vertices with perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000), // On perimeter
            new(100, 60, 0, 2001), // On perimeter
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"Vertices before Ruppert: {tin.GetVertices().Count}");

        // Validate perimeter BEFORE Ruppert
        var (validBefore, errorBefore) = ValidatePerimeter(tin);
        _output.WriteLine($"Before Ruppert - Perimeter valid: {validBefore}");
        if (!validBefore) _output.WriteLine($"  Error: {errorBefore}");
        Assert.True(validBefore, $"Perimeter invalid BEFORE Ruppert: {errorBefore}");

        // Run Ruppert refinement - use MinimumTriangleArea to limit refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true,
            MinimumTriangleArea = 2.0 // Larger terrain, allow smaller triangles
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"Vertices after Ruppert: {tin.GetVertices().Count}");

        // Validate perimeter AFTER Ruppert - THIS IS WHERE IT FAILS
        var (validAfter, errorAfter) = ValidatePerimeter(tin);
        _output.WriteLine($"After Ruppert - Perimeter valid: {validAfter}");
        if (!validAfter) _output.WriteLine($"  Error: {errorAfter}");

        Assert.True(validAfter, $"Perimeter invalid AFTER Ruppert: {errorAfter}");
    }

    /// <summary>
    ///     Try to find the minimum vertex count that triggers the issue.
    /// </summary>
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(200)]
    public void ISSUE_PerimeterCorruption_VariousVertexCounts(int vertexCount)
    {
        var tin = new IncrementalTin();

        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < vertexCount; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + Math.Sin(x / 10) * 5 + Math.Cos(y / 10) * 3 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        // Add corner vertices
        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        // Constraint touching perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000),
            new(100, 60, 0, 2001),
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true,
            MinimumTriangleArea = 2.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"vertexCount={vertexCount}, final vertices={tin.GetVertices().Count}");

        var (isValid, error) = ValidatePerimeter(tin);
        _output.WriteLine($"Valid: {isValid}");
        if (!isValid) _output.WriteLine($"Error: {error}");

        Assert.True(isValid, $"vertexCount={vertexCount}: {error}");
    }

    #endregion

    #region Contour Traversal Tests

    /// <summary>
    ///     Test that contour builder can complete when constraint shares vertices with perimeter.
    ///     This is the scenario that causes timeouts in real-world usage.
    /// </summary>
    [Fact]
    public void ContourBuilder_WithPerimeterConstraint_ShouldComplete()
    {
        var tin = CreateWaterBodyTouchingPerimeter();

        // Run Ruppert refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 500,
            InterpolateZ = true,
            MinimumTriangleArea = 5.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"Vertices after Ruppert: {tin.GetVertices().Count}");

        // Generate contour values based on Z range
        var minZ = double.MaxValue;
        var maxZ = double.MinValue;
        foreach (var v in tin.GetVertices())
        {
            if (v.IsNullVertex()) continue;
            var z = v.GetZ();
            if (!double.IsNaN(z))
            {
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        _output.WriteLine($"Z range: {minZ:F2} to {maxZ:F2}");

        // Create contour levels
        var contourLevels = new List<double>();
        var step = (maxZ - minZ) / 5.0;
        for (var z = minZ + step; z < maxZ; z += step)
            contourLevels.Add(z);

        if (contourLevels.Count == 0)
        {
            _output.WriteLine("No contour levels to test (flat terrain)");
            return;
        }

        _output.WriteLine($"Contour levels: {string.Join(", ", contourLevels.Select(z => z.ToString("F2")))}");

        // Try to build contours - this is where the timeout occurs
        var timeout = TimeSpan.FromSeconds(5);
        var cts = new CancellationTokenSource(timeout);

        Exception? caughtException = null;
        var completed = false;

        var task = Task.Run(() =>
        {
            try
            {
                var builder = new ContourBuilderForTin(
                    tin,
                    null,
                    contourLevels.ToArray(),
                    false,
                    false);
                completed = true;
                return builder.GetContours();
            }
            catch (Exception ex)
            {
                caughtException = ex;
                return new List<Contour>();
            }
        }, cts.Token);

        try
        {
            task.Wait(timeout);
        }
        catch (AggregateException)
        {
            // Task was cancelled or timed out
        }

        if (!completed && caughtException == null)
        {
            _output.WriteLine("TIMEOUT: Contour builder did not complete within 5 seconds");
            Assert.Fail("Contour builder timed out - perimeter topology issue detected");
        }
        else if (caughtException != null)
        {
            _output.WriteLine($"Exception: {caughtException.Message}");
            throw caughtException;
        }
        else
        {
            var contours = task.Result;
            _output.WriteLine($"Contours built: {contours.Count}");
            Assert.True(true, "Contour builder completed successfully");
        }
    }

    /// <summary>
    ///     Test contour traversal with larger TIN that's more likely to trigger issues.
    /// </summary>
    [Fact]
    public void ContourBuilder_LargeTinWithPerimeterConstraint_ShouldComplete()
    {
        var tin = new IncrementalTin();

        // Create larger terrain
        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < 200; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + Math.Sin(x / 10) * 5 + Math.Cos(y / 10) * 3 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        // Add corner vertices to ensure perimeter is defined
        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        // Add constraint that shares vertices with perimeter
        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000), // On perimeter
            new(100, 60, 0, 2001), // On perimeter
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"Vertices before Ruppert: {tin.GetVertices().Count}");

        // Run Ruppert refinement - use MinimumTriangleArea to limit refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true,
            MinimumTriangleArea = 2.0
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"Vertices after Ruppert: {tin.GetVertices().Count}");

        // Validate perimeter first
        var (isValid, error) = ValidatePerimeter(tin);
        _output.WriteLine($"Perimeter valid: {isValid}");
        if (!isValid) _output.WriteLine($"  Error: {error}");

        Assert.True(isValid, $"Perimeter invalid: {error}");

        // Create contour levels
        var contourLevels = new[] { 8.0, 10.0, 12.0, 14.0 };

        // Build contours
        var timeout = TimeSpan.FromSeconds(10);
        var completed = false;

        var task = Task.Run(() =>
        {
            var builder = new ContourBuilderForTin(
                tin,
                null,
                contourLevels,
                false,
                false);
            completed = true;
            return builder.GetContours();
        });

        var success = task.Wait(timeout);

        if (!success || !completed)
        {
            _output.WriteLine("TIMEOUT: Contour builder did not complete");
            Assert.Fail("Contour builder timed out - perimeter topology issue detected");
        }

        var contours = task.Result;
        _output.WriteLine($"Contours built: {contours.Count}");
    }

    /// <summary>
    ///     Specific test case: constraint edge exactly on perimeter edge.
    /// </summary>
    [Fact]
    public void ContourBuilder_ConstraintEdgeOnPerimeter_ShouldComplete()
    {
        var tin = new IncrementalTin();

        // Simple square TIN
        var vertices = new List<Vertex>
        {
            new(0, 0, 0, 0),
            new(10, 0, 0, 1),
            new(10, 10, 0, 2),
            new(0, 10, 0, 3),
            new(5, 5, 5, 4)
        };
        tin.Add(vertices);

        // Constraint with one edge exactly on the perimeter (bottom edge)
        var constraintVertices = new List<Vertex>
        {
            new(0, 0, 0, 100), // Same as vertex 0
            new(10, 0, 0, 101), // Same as vertex 1
            new(5, 3, 2, 102)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        _output.WriteLine($"Vertices: {tin.GetVertices().Count}");

        // Validate perimeter
        var (isValid, error) = ValidatePerimeter(tin);
        Assert.True(isValid, $"Perimeter invalid after constraint: {error}");

        // Run Ruppert
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 20,
            MaxIterations = 100,
            InterpolateZ = true,
            MinimumTriangleArea = 0.5 // Small test terrain (10x10)
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"Vertices after Ruppert: {tin.GetVertices().Count}");

        // Validate perimeter after refinement
        var (isValidAfter, errorAfter) = ValidatePerimeter(tin);
        Assert.True(isValidAfter, $"Perimeter invalid after Ruppert: {errorAfter}");

        // Build contours
        var contourLevels = new[] { 1.0, 2.0, 3.0, 4.0 };
        var builder = new ContourBuilderForTin(
            tin,
            null,
            contourLevels,
            false,
            false);
        var contours = builder.GetContours();

        _output.WriteLine($"Contours built: {contours.Count}");
    }

    /// <summary>
    ///     Step through Ruppert ONE vertex at a time using RefineOnce() to find exact corruption point.
    /// </summary>
    [Fact]
    public void DIAGNOSTIC_StepThroughRuppertOneVertexAtATime()
    {
        var tin = new IncrementalTin();

        // Use the same setup as the failing test
        var rand = new Random(42);
        var vertices = new List<Vertex>();
        for (var i = 0; i < 200; i++)
        {
            var x = rand.NextDouble() * 100;
            var y = rand.NextDouble() * 100;
            var z = 10 + Math.Sin(x / 10) * 5 + Math.Cos(y / 10) * 3 + rand.NextDouble();
            vertices.Add(new Vertex(x, y, z, i));
        }

        vertices.Add(new Vertex(0, 0, 10, 1000));
        vertices.Add(new Vertex(100, 0, 10, 1001));
        vertices.Add(new Vertex(100, 100, 10, 1002));
        vertices.Add(new Vertex(0, 100, 10, 1003));

        tin.Add(vertices);

        var constraintVertices = new List<Vertex>
        {
            new(100, 40, 0, 2000),
            new(100, 60, 0, 2001),
            new(90, 60, 0, 2002),
            new(85, 50, 0, 2003),
            new(90, 40, 0, 2004)
        };
        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        var initialCount = tin.GetVertices().Count;
        _output.WriteLine($"Initial vertices: {initialCount}");

        var (validInitial, _) = ValidatePerimeter(tin);
        _output.WriteLine($"Initial perimeter valid: {validInitial}");
        Assert.True(validInitial, "Initial perimeter should be valid");

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true,
            MinimumTriangleArea = 2.0
        };
        var refiner = new RuppertRefiner(tin, options);

        var step = 0;
        IVertex? insertedVertex;
        while ((insertedVertex = refiner.RefineOnce()) != null && step < 100)
        {
            step++;
            var currentCount = tin.GetVertices().Count;

            var (isValid, error) = ValidatePerimeter(tin);
            if (!isValid)
            {
                _output.WriteLine($"*** CORRUPTION at step {step} ***");
                _output.WriteLine($"  Vertices: {currentCount} (added {currentCount - initialCount})");
                _output.WriteLine($"  Last inserted vertex: ({insertedVertex.X:F2}, {insertedVertex.Y:F2})");
                _output.WriteLine($"  Error: {error}");

                // Dump edges involving the inserted vertex
                _output.WriteLine($"\nEdges involving ({insertedVertex.X:F2}, {insertedVertex.Y:F2}):");
                foreach (var e in tin.GetEdges())
                {
                    var a = e.GetA();
                    var b = e.GetB();
                    if ((Math.Abs(a.X - insertedVertex.X) < 0.01 && Math.Abs(a.Y - insertedVertex.Y) < 0.01) ||
                        (Math.Abs(b.X - insertedVertex.X) < 0.01 && Math.Abs(b.Y - insertedVertex.Y) < 0.01))
                    {
                        var fwd = e.GetForward();
                        var rev = e.GetReverse();
                        var dual = e.GetDual();
                        var dualFwd = dual.GetForward();
                        var dualRev = dual.GetReverse();
                        _output.WriteLine($"  Edge {e.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) " +
                            $"constrained={e.IsConstrained()} border={e.IsConstraintRegionBorder()}");
                        _output.WriteLine($"    Forward.B=({fwd.GetB().X},{fwd.GetB().Y}) isNull={fwd.GetB().IsNullVertex()}");
                        _output.WriteLine($"    Reverse.A=({rev.GetA().X},{rev.GetA().Y}) isNull={rev.GetA().IsNullVertex()}");
                        _output.WriteLine($"    Dual: ({dual.GetA().X},{dual.GetA().Y})->({dual.GetB().X},{dual.GetB().Y})");
                        _output.WriteLine($"    DualForward.B=({dualFwd.GetB().X},{dualFwd.GetB().Y}) isNull={dualFwd.GetB().IsNullVertex()}");
                    }
                }

                // Dump perimeter state
                _output.WriteLine("\nPerimeter state at corruption:");
                DumpPerimeterState(tin);

                Assert.Fail($"Perimeter corrupted at step {step} after inserting vertex at ({insertedVertex.X:F2}, {insertedVertex.Y:F2}): {error}");
                return;
            }

            if (step <= 10 || step % 10 == 0)
            {
                _output.WriteLine($"Step {step}: {currentCount} vertices, valid=True, last=({insertedVertex.X:F1},{insertedVertex.Y:F1})");
            }
        }

        _output.WriteLine($"Completed {step} steps without corruption, final vertices: {tin.GetVertices().Count}");
    }

    #endregion
}
