/*
 * Tests to verify TIN structural integrity after Ruppert refinement.
 */

namespace Tinfour.Core.Tests.Refinement;

using System.Diagnostics;
using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Xunit;
using Xunit.Abstractions;

public class TinIntegrityTests
{
    private readonly ITestOutputHelper _output;

    public TinIntegrityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Verifies that all edges have valid forward/reverse linkages.
    /// </summary>
    [Fact]
    public void RuppertRefinement_EdgeLinkageIntegrity()
    {
        // Create a TIN with a simple polygon constraint
        var tin = new IncrementalTin();

        // Add vertices covering a larger area
        var vertices = new List<IVertex>();
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10));
        }
        tin.Add(vertices);

        // Add a square polygon constraint
        var constraintVertices = new IVertex[]
        {
            new Vertex(20, 20, 5),
            new Vertex(80, 20, 5),
            new Vertex(80, 80, 5),
            new Vertex(20, 80, 5)
        };
        var constraint = new PolygonConstraint(constraintVertices, true);
        tin.AddConstraints(new[] { constraint }, true);

        _output.WriteLine($"Before Ruppert: {tin.GetVertices().Count} vertices, {CountEdges(tin)} edges, {CountTriangles(tin)} triangles");

        // Verify integrity before refinement
        var (validBefore, errorsBefore) = VerifyEdgeLinkageIntegrity(tin);
        _output.WriteLine($"Before Ruppert integrity: {(validBefore ? "PASS" : "FAIL")}");
        foreach (var error in errorsBefore.Take(10))
        {
            _output.WriteLine($"  {error}");
        }
        Assert.True(validBefore, $"Edge linkage integrity failed BEFORE Ruppert: {string.Join("; ", errorsBefore.Take(5))}");

        // Run Ruppert refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine($"After Ruppert: {tin.GetVertices().Count} vertices, {CountEdges(tin)} edges, {CountTriangles(tin)} triangles");

        // Verify integrity after refinement
        var (validAfter, errorsAfter) = VerifyEdgeLinkageIntegrity(tin);
        _output.WriteLine($"After Ruppert integrity: {(validAfter ? "PASS" : "FAIL")}");
        foreach (var error in errorsAfter.Take(10))
        {
            _output.WriteLine($"  {error}");
        }
        Assert.True(validAfter, $"Edge linkage integrity failed AFTER Ruppert: {string.Join("; ", errorsAfter.Take(5))}");
    }

    /// <summary>
    /// Verifies that all triangles are reachable via navigation from any starting edge.
    /// </summary>
    [Fact]
    public void RuppertRefinement_AllTrianglesReachable()
    {
        // Create a TIN with a simple polygon constraint
        var tin = new IncrementalTin();

        var vertices = new List<IVertex>();
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10));
        }
        tin.Add(vertices);

        var constraintVertices = new IVertex[]
        {
            new Vertex(20, 20, 5),
            new Vertex(80, 20, 5),
            new Vertex(80, 80, 5),
            new Vertex(20, 80, 5)
        };
        var constraint = new PolygonConstraint(constraintVertices, true);
        tin.AddConstraints(new[] { constraint }, true);

        // Run Ruppert refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        // Get all triangles via iterator
        var allTriangles = tin.GetTriangles().ToList();
        _output.WriteLine($"Total triangles from iterator: {allTriangles.Count}");

        // Try to reach each triangle via navigation
        var navigator = new IncrementalTinNavigator(tin);
        var reachableCount = 0;
        var unreachableCount = 0;
        var unreachableCentroids = new List<(double x, double y)>();

        foreach (var tri in allTriangles)
        {
            // Calculate centroid
            var cx = (tri.GetVertexA().X + tri.GetVertexB().X + tri.GetVertexC().X) / 3.0;
            var cy = (tri.GetVertexA().Y + tri.GetVertexB().Y + tri.GetVertexC().Y) / 3.0;

            // Try to navigate to this point
            var edge = navigator.GetNeighborEdge(cx, cy);

            // Check if we actually landed in the right triangle
            var foundTri = new SimpleTriangle(edge);
            if (TrianglesMatch(tri, foundTri))
            {
                reachableCount++;
            }
            else
            {
                unreachableCount++;
                if (unreachableCentroids.Count < 10)
                {
                    unreachableCentroids.Add((cx, cy));
                }
            }
        }

        _output.WriteLine($"Reachable triangles: {reachableCount}");
        _output.WriteLine($"Unreachable triangles: {unreachableCount}");

        foreach (var (x, y) in unreachableCentroids)
        {
            _output.WriteLine($"  Unreachable centroid: ({x:F2}, {y:F2})");
        }

        Assert.Equal(0, unreachableCount);
    }

    /// <summary>
    /// Tests that the rasterizer can interpolate all points in constrained regions.
    /// </summary>
    [Fact]
    public void RuppertRefinement_RasterizerReachesAllConstrainedTriangles()
    {
        // Create a TIN with a simple polygon constraint
        var tin = new IncrementalTin();

        var vertices = new List<IVertex>();
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10));
        }
        tin.Add(vertices);

        var constraintVertices = new IVertex[]
        {
            new Vertex(20, 20, 5),
            new Vertex(80, 20, 5),
            new Vertex(80, 80, 5),
            new Vertex(20, 80, 5)
        };
        var constraint = new PolygonConstraint(constraintVertices, true);
        tin.AddConstraints(new[] { constraint }, true);

        // Count constrained triangles BEFORE refinement
        var constrainedTrianglesBefore = CountConstrainedTriangles(tin);
        _output.WriteLine($"Constrained triangles before Ruppert: {constrainedTrianglesBefore}");

        // Run Ruppert refinement with Z interpolation enabled for rasterization
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true  // Required for rasterization - interpolates Z values for new vertices
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        // Count constrained triangles AFTER refinement
        var constrainedTrianglesAfter = CountConstrainedTriangles(tin);
        _output.WriteLine($"Constrained triangles after Ruppert: {constrainedTrianglesAfter}");

        // Use the rasterizer with constraint regions only
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet, constrainedRegionsOnly: true);
        var result = rasterizer.CreateRaster(50, 50);

        // Count non-NaN values
        int validPixels = 0;
        int nanPixels = 0;
        for (int y = 0; y < 50; y++)
        {
            for (int x = 0; x < 50; x++)
            {
                if (double.IsNaN(result.Data[x, y]))
                    nanPixels++;
                else
                    validPixels++;
            }
        }

        _output.WriteLine($"Valid pixels: {validPixels}");
        _output.WriteLine($"NaN pixels: {nanPixels}");
        _output.WriteLine($"NoData count from result: {result.NoDataCount}");

        // The constraint covers approximately 60x60 / 100x100 = 36% of the area
        // So we should have around 36% of 2500 = 900 valid pixels
        // Allow some tolerance for boundary effects
        Assert.True(validPixels > 500, $"Expected more than 500 valid pixels in constrained region, got {validPixels}");
    }

    private (bool isValid, List<string> errors) VerifyEdgeLinkageIntegrity(IIncrementalTin tin)
    {
        var errors = new List<string>();

        foreach (var edge in tin.GetEdges())
        {
            var forward = edge.GetForward();
            var reverse = edge.GetReverse();
            var dual = edge.GetDual();

            // Check forward link reciprocity
            if (forward != null)
            {
                var forwardReverse = forward.GetReverse();
                if (forwardReverse != edge)
                {
                    errors.Add($"Edge {edge.GetIndex()}: forward.reverse ({forwardReverse?.GetIndex()}) != self");
                }
            }

            // Check reverse link reciprocity
            if (reverse != null)
            {
                var reverseForward = reverse.GetForward();
                if (reverseForward != edge)
                {
                    errors.Add($"Edge {edge.GetIndex()}: reverse.forward ({reverseForward?.GetIndex()}) != self");
                }
            }

            // Check that triangles close properly (e -> f -> r -> e)
            if (forward != null)
            {
                var ff = forward.GetForward();
                if (ff != null)
                {
                    var fff = ff.GetForward();
                    if (fff != edge)
                    {
                        // Not a closed triangle - might be OK for ghost edges
                        var a = edge.GetA();
                        var b = edge.GetB();
                        var c = forward.GetB();
                        if (!a.IsNullVertex() && !b.IsNullVertex() && !c.IsNullVertex())
                        {
                            errors.Add($"Edge {edge.GetIndex()}: triangle doesn't close (e->f->ff->? = {fff?.GetIndex()})");
                        }
                    }
                }
            }
        }

        return (errors.Count == 0, errors);
    }

    private bool TrianglesMatch(SimpleTriangle t1, SimpleTriangle t2)
    {
        var vertices1 = new HashSet<IVertex> { t1.GetVertexA(), t1.GetVertexB(), t1.GetVertexC() };
        var vertices2 = new HashSet<IVertex> { t2.GetVertexA(), t2.GetVertexB(), t2.GetVertexC() };
        return vertices1.SetEquals(vertices2);
    }

    private int CountEdges(IIncrementalTin tin)
    {
        return tin.GetEdges().Count;
    }

    private int CountTriangles(IIncrementalTin tin)
    {
        return tin.GetTriangles().Count();
    }

    private int CountConstrainedTriangles(IIncrementalTin tin)
    {
        int count = 0;
        foreach (var tri in tin.GetTriangles())
        {
            if (tri.GetEdgeA().IsConstraintRegionInterior() ||
                tri.GetEdgeB().IsConstraintRegionInterior() ||
                tri.GetEdgeC().IsConstraintRegionInterior())
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Detailed analysis of constraint membership issue.
    /// </summary>
    [Fact]
    public void RuppertRefinement_ConstraintMembershipAnalysis()
    {
        var tin = new IncrementalTin();

        var vertices = new List<IVertex>();
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10));
        }
        tin.Add(vertices);

        var constraintVertices = new IVertex[]
        {
            new Vertex(20, 20, 5),
            new Vertex(80, 20, 5),
            new Vertex(80, 80, 5),
            new Vertex(20, 80, 5)
        };
        var constraint = new PolygonConstraint(constraintVertices, true);
        tin.AddConstraints(new[] { constraint }, true);

        _output.WriteLine("=== BEFORE RUPPERT ===");
        AnalyzeConstraintMembership(tin);

        // Run Ruppert refinement
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        _output.WriteLine("\n=== AFTER RUPPERT ===");
        AnalyzeConstraintMembership(tin);

        // Test navigation to specific points inside the constraint region
        _output.WriteLine("\n=== NAVIGATION TESTS ===");
        var navigator = new IncrementalTinNavigator(tin);
        var testPoints = new[] { (50.0, 50.0), (30.0, 30.0), (70.0, 70.0), (25.0, 50.0), (75.0, 50.0) };

        foreach (var (x, y) in testPoints)
        {
            var edge = navigator.GetNeighborEdge(x, y);
            var edgeFwd = edge.GetForward();
            var edgeRev = edge.GetReverse();

            var isInterior = edge.IsConstraintRegionInterior() ||
                             edgeFwd.IsConstraintRegionInterior() ||
                             edgeRev.IsConstraintRegionInterior();

            _output.WriteLine($"Point ({x}, {y}): edge={edge.GetIndex()}, " +
                              $"interior={isInterior}, " +
                              $"e.interior={edge.IsConstraintRegionInterior()}, " +
                              $"ef.interior={edgeFwd.IsConstraintRegionInterior()}, " +
                              $"er.interior={edgeRev.IsConstraintRegionInterior()}");

            // Also check border status
            var isBorder = edge.IsConstraintRegionBorder() ||
                           edgeFwd.IsConstraintRegionBorder() ||
                           edgeRev.IsConstraintRegionBorder();
            _output.WriteLine($"  border: e={edge.IsConstraintRegionBorder()}, ef={edgeFwd.IsConstraintRegionBorder()}, er={edgeRev.IsConstraintRegionBorder()}");

            // Check member status
            var isMember = edge.IsConstraintRegionMember() ||
                           edgeFwd.IsConstraintRegionMember() ||
                           edgeRev.IsConstraintRegionMember();
            _output.WriteLine($"  member: e={edge.IsConstraintRegionMember()}, ef={edgeFwd.IsConstraintRegionMember()}, er={edgeRev.IsConstraintRegionMember()}");
        }

        // Assertion: at least point (50,50) should be found as interior
        {
            var edge = navigator.GetNeighborEdge(50, 50);
            var edgeFwd = edge.GetForward();
            var edgeRev = edge.GetReverse();
            var isInterior = edge.IsConstraintRegionInterior() ||
                             edgeFwd.IsConstraintRegionInterior() ||
                             edgeRev.IsConstraintRegionInterior();
            Assert.True(isInterior, "Point (50,50) should be in interior of constraint region");
        }

        // Check edge index distribution
        _output.WriteLine("\n=== EDGE INDEX ANALYSIS ===");
        var edgeIndices = tin.GetEdges().Select(e => e.GetIndex()).OrderBy(i => i).ToList();
        _output.WriteLine($"Edge index range: {edgeIndices.First()} to {edgeIndices.Last()}");

        // Check which indices are interior
        var interiorEdgeIndices = tin.GetEdges()
            .Where(e => e.IsConstraintRegionInterior())
            .Select(e => e.GetIndex())
            .OrderBy(i => i)
            .ToList();
        if (interiorEdgeIndices.Any())
        {
            _output.WriteLine($"Interior edge index range: {interiorEdgeIndices.First()} to {interiorEdgeIndices.Last()}");
            _output.WriteLine($"Interior edges with index < 500: {interiorEdgeIndices.Count(i => i < 500)}");
            _output.WriteLine($"Interior edges with index >= 500: {interiorEdgeIndices.Count(i => i >= 500)}");
        }
    }

    private void AnalyzeConstraintMembership(IIncrementalTin tin)
    {
        int totalTriangles = 0;
        int trianglesWithInteriorEdge = 0;
        int trianglesWithBorderEdge = 0;
        int trianglesWithMemberEdge = 0;
        int trianglesNoConstraintEdges = 0;

        int totalEdges = 0;
        int interiorEdges = 0;
        int borderEdges = 0;
        int memberEdges = 0;

        foreach (var edge in tin.GetEdges())
        {
            totalEdges++;
            if (edge.IsConstraintRegionInterior()) interiorEdges++;
            if (edge.IsConstraintRegionBorder()) borderEdges++;
            if (edge.IsConstraintRegionMember()) memberEdges++;
        }

        foreach (var tri in tin.GetTriangles())
        {
            totalTriangles++;
            var ea = tri.GetEdgeA();
            var eb = tri.GetEdgeB();
            var ec = tri.GetEdgeC();

            bool hasInterior = ea.IsConstraintRegionInterior() || eb.IsConstraintRegionInterior() || ec.IsConstraintRegionInterior();
            bool hasBorder = ea.IsConstraintRegionBorder() || eb.IsConstraintRegionBorder() || ec.IsConstraintRegionBorder();
            bool hasMember = ea.IsConstraintRegionMember() || eb.IsConstraintRegionMember() || ec.IsConstraintRegionMember();

            if (hasInterior) trianglesWithInteriorEdge++;
            if (hasBorder) trianglesWithBorderEdge++;
            if (hasMember) trianglesWithMemberEdge++;
            if (!hasMember) trianglesNoConstraintEdges++;
        }

        _output.WriteLine($"Total triangles: {totalTriangles}");
        _output.WriteLine($"  with interior edge: {trianglesWithInteriorEdge}");
        _output.WriteLine($"  with border edge: {trianglesWithBorderEdge}");
        _output.WriteLine($"  with member edge: {trianglesWithMemberEdge}");
        _output.WriteLine($"  no constraint edges: {trianglesNoConstraintEdges}");

        _output.WriteLine($"Total edges: {totalEdges}");
        _output.WriteLine($"  interior: {interiorEdges}");
        _output.WriteLine($"  border: {borderEdges}");
        _output.WriteLine($"  member: {memberEdges}");
    }

    /// <summary>
    /// Detailed diagnostic comparing navigator results with direct triangle lookup.
    /// </summary>
    [Fact]
    public void RuppertRefinement_InterpolatorDiagnostic()
    {
        var tin = new IncrementalTin();

        var vertices = new List<IVertex>();
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            vertices.Add(new Vertex(rand.NextDouble() * 100, rand.NextDouble() * 100, rand.NextDouble() * 10));
        }
        tin.Add(vertices);

        var constraintVertices = new IVertex[]
        {
            new Vertex(20, 20, 5),
            new Vertex(80, 20, 5),
            new Vertex(80, 80, 5),
            new Vertex(20, 80, 5)
        };
        var constraint = new PolygonConstraint(constraintVertices, true);
        tin.AddConstraints(new[] { constraint }, true);

        // Run Ruppert refinement with Z interpolation enabled
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 25,
            MaxIterations = 1000,
            InterpolateZ = true  // Required for rasterization - interpolates Z values for new vertices
        };
        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        // Create interpolator with constrained regions only
        var interpolator = new TriangularFacetInterpolator(tin, constrainedRegionsOnly: true);

        // Sample grid of points
        int validCount = 0;
        int nanCount = 0;
        int outsideConstraint = 0;

        _output.WriteLine("=== SAMPLE POINT ANALYSIS ===");
        _output.WriteLine("Sampling 10x10 grid from (25,25) to (75,75)...");

        var navigator = new IncrementalTinNavigator(tin);

        for (int iy = 0; iy < 10; iy++)
        {
            for (int ix = 0; ix < 10; ix++)
            {
                double x = 25 + ix * 5;
                double y = 25 + iy * 5;

                // Check what the interpolator returns
                double value = interpolator.Interpolate(x, y, null);
                bool isValid = !double.IsNaN(value);

                // Check what the navigator finds
                var edge = navigator.GetNeighborEdge(x, y);
                var edgeFwd = edge.GetForward();
                var edgeRev = edge.GetReverse();

                bool hasInterior = edge.IsConstraintRegionInterior() ||
                                   edgeFwd.IsConstraintRegionInterior() ||
                                   edgeRev.IsConstraintRegionInterior();

                if (isValid)
                    validCount++;
                else
                    nanCount++;

                // Point is clearly inside constraint (25-75 vs 20-80)
                if (!isValid && hasInterior)
                {
                    // Navigator found an interior triangle but interpolator returned NaN - this is a bug!
                    _output.WriteLine($"BUG: Point ({x},{y}): interpolator=NaN but navigator found interior triangle");
                    _output.WriteLine($"  edge={edge.GetIndex()}, fwd={edgeFwd.GetIndex()}, rev={edgeRev.GetIndex()}");
                    _output.WriteLine($"  interior: e={edge.IsConstraintRegionInterior()}, f={edgeFwd.IsConstraintRegionInterior()}, r={edgeRev.IsConstraintRegionInterior()}");
                }
                else if (!isValid && !hasInterior)
                {
                    outsideConstraint++;
                    // This point is inside the constraint bounds but navigator didn't find interior - check why
                    _output.WriteLine($"Point ({x},{y}): no interior edge found");
                    _output.WriteLine($"  edge={edge.GetIndex()}, fwd={edgeFwd.GetIndex()}, rev={edgeRev.GetIndex()}");
                    _output.WriteLine($"  v0={edge.GetA()?.GetX():F1},{edge.GetA()?.GetY():F1}");
                    _output.WriteLine($"  v1={edge.GetB()?.GetX():F1},{edge.GetB()?.GetY():F1}");
                    _output.WriteLine($"  v2={edgeFwd.GetB()?.GetX():F1},{edgeFwd.GetB()?.GetY():F1}");
                }
            }
        }

        _output.WriteLine($"\nSummary: valid={validCount}, NaN={nanCount}, outsideConstraint={outsideConstraint}");

        // Most points should be valid (we're sampling inside the 20-80 constraint from 25-75)
        Assert.True(validCount > 80, $"Expected most points to be valid, got {validCount}/100");
    }
}
