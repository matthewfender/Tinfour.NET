namespace Tinfour.Core.Tests.Standard;

using global::Tinfour.Core.Common;
using global::Tinfour.Core.Interpolation;
using global::Tinfour.Core.Standard;
using Xunit;

/// <summary>
///     Tests for the AddConstraints per-phase timing attribution (ticket 826) and for the
///     Hilbert-ordered transient interpolation-TIN build it accompanies: the copy-TIN is
///     now built in Hilbert order with preallocation, which must not change the
///     interpolation surface.
/// </summary>
public class AddConstraintsTimingsTests
{
    [Fact]
    public void LastAddConstraintsTimings_IsNull_BeforeConstraintsAreAdded()
    {
        using var tin = BuildGridTin();

        Assert.Null(tin.LastAddConstraintsTimings);
    }

    [Fact]
    public void AddConstraints_WithPreInterpolation_PopulatesAllPhases()
    {
        using var tin = BuildGridTin(out var dataVertexCount);

        tin.AddConstraints(
            new List<IConstraint> { CreateNaNSquareConstraint() },
            restoreConformity: true,
            preInterpolateZ: true,
            InterpolationType.TriangularFacet);

        var timings = tin.LastAddConstraintsTimings;
        Assert.NotNull(timings);

        // All-NaN constraint => no vertices seeded in phase 0, so the interpolation TIN
        // re-triangulates exactly the original data vertices.
        Assert.Equal(dataVertexCount, timings!.InterpolationTinVertexCount);

        Assert.True(timings.SeedConstraintVertices >= TimeSpan.Zero);
        Assert.True(timings.InterpolationTinBuild > TimeSpan.Zero);
        Assert.True(timings.InterpolateAndInsertVertices >= TimeSpan.Zero);
        Assert.True(timings.ProcessConstraints >= TimeSpan.Zero);
        Assert.True(timings.RestoreConformity >= TimeSpan.Zero);
        Assert.True(timings.FloodFill >= TimeSpan.Zero);
        Assert.True(timings.RestoreConformitySyntheticVertices >= 0);

        var phaseSum = timings.SeedConstraintVertices
                       + timings.InterpolationTinBuild
                       + timings.InterpolateAndInsertVertices
                       + timings.ProcessConstraints
                       + timings.RestoreConformity
                       + timings.FloodFill;
        Assert.True(timings.Total >= phaseSum, "Total must cover the sum of the measured phases");
    }

    [Fact]
    public void AddConstraints_WithoutPreInterpolation_ReportsNoInterpolationTin()
    {
        using var tin = BuildGridTin();

        tin.AddConstraints(
            new List<IConstraint> { CreateRealZSquareConstraint() },
            restoreConformity: false,
            preInterpolateZ: false);

        var timings = tin.LastAddConstraintsTimings;
        Assert.NotNull(timings);
        Assert.Equal(TimeSpan.Zero, timings!.SeedConstraintVertices);
        Assert.Equal(TimeSpan.Zero, timings.InterpolationTinBuild);
        Assert.Equal(0, timings.InterpolationTinVertexCount);
        Assert.Equal(TimeSpan.Zero, timings.RestoreConformity);
        Assert.Equal(0, timings.RestoreConformitySyntheticVertices);
    }

    /// <summary>
    ///     Semantics pin for the Hilbert-ordered copy-TIN change: for the same vertex set,
    ///     a TIN built in input order and one built in Hilbert order define the same
    ///     Delaunay surface, so facet interpolation must agree everywhere. This is the
    ///     property AddConstraints now relies on when it re-triangulates its vertex
    ///     snapshot in Hilbert order.
    /// </summary>
    [Fact]
    public void FacetInterpolation_IsIdentical_ForUnsortedAndHilbertBuilds()
    {
        var vertices = CreateJitteredGridVertices();

        using var unsortedTin = new IncrementalTin(1.0);
        unsortedTin.Add(vertices);

        using var hilbertTin = new IncrementalTin(1.0);
        hilbertTin.PreAllocateForVertices(vertices.Count);
        hilbertTin.Add(vertices, VertexOrder.Hilbert);

        var unsortedInterpolator = InterpolatorFactory.Create(unsortedTin, InterpolationType.TriangularFacet);
        var hilbertInterpolator = InterpolatorFactory.Create(hilbertTin, InterpolationType.TriangularFacet);

        for (var x = 2.5; x < 100.0; x += 7.3)
        for (var y = 2.5; y < 100.0; y += 7.3)
        {
            var expected = unsortedInterpolator.Interpolate(x, y, null);
            var actual = hilbertInterpolator.Interpolate(x, y, null);
            Assert.Equal(expected, actual, 12);
        }
    }

    private static IncrementalTin BuildGridTin()
    {
        return BuildGridTin(out _);
    }

    private static IncrementalTin BuildGridTin(out int vertexCount)
    {
        var vertices = CreateJitteredGridVertices();
        var tin = new IncrementalTin(1.0);
        tin.Add(vertices, VertexOrder.Hilbert);
        vertexCount = vertices.Count;
        return tin;
    }

    /// <summary>
    ///     Jittered 20x20 grid over [0,100]^2 with a sloped surface. Jitter keeps points in
    ///     general position (no co-circular quads), so the Delaunay triangulation — and
    ///     therefore facet interpolation — is order-independent.
    /// </summary>
    private static List<IVertex> CreateJitteredGridVertices()
    {
        var rnd = new Random(4242);
        var vertices = new List<IVertex>();
        for (var i = 0; i < 20; i++)
        for (var j = 0; j < 20; j++)
        {
            var x = i * 5.0 + (rnd.NextDouble() - 0.5) * 1.5;
            var y = j * 5.0 + (rnd.NextDouble() - 0.5) * 1.5;
            vertices.Add(new Vertex(x, y, 0.1 * x + 0.05 * y, vertices.Count));
        }

        return vertices;
    }

    private static PolygonConstraint CreateNaNSquareConstraint()
    {
        // CCW square, off-grid, all vertices NaN so pre-interpolation fills every one.
        return new PolygonConstraint(new List<IVertex>
        {
            new Vertex(22.2, 22.2, double.NaN, 2001),
            new Vertex(77.7, 22.2, double.NaN, 2002),
            new Vertex(77.7, 77.7, double.NaN, 2003),
            new Vertex(22.2, 77.7, double.NaN, 2004),
        });
    }

    private static PolygonConstraint CreateRealZSquareConstraint()
    {
        return new PolygonConstraint(new List<IVertex>
        {
            new Vertex(22.2, 22.2, 1.0, 2001),
            new Vertex(77.7, 22.2, 1.0, 2002),
            new Vertex(77.7, 77.7, 1.0, 2003),
            new Vertex(22.2, 77.7, 1.0, 2004),
        });
    }
}
