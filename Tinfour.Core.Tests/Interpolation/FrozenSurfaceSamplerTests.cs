namespace Tinfour.Core.Tests.Interpolation;

using global::Tinfour.Core.Common;
using global::Tinfour.Core.Interpolation;
using global::Tinfour.Core.Standard;
using Xunit;

/// <summary>
///     Identity tests for the ticket-#800 frozen surface sampler, which
///     replaces the AddConstraints copy-TIN (a full re-triangulation of every
///     vertex) as the constraint-Z draping surface.
/// </summary>
/// <remarks>
///     <para>
///         Two identity bars are pinned here:
///     </para>
///     <para>
///         <b>Bit-for-bit vs the pre-mutation TIN.</b> The sampler snapshots
///         the primary store's own arrays, so it has identical handles,
///         topology, and coordinates — its walks and facet evaluations must
///         match a <see cref="TriangularFacetInterpolator" /> over the primary
///         TIN exactly, including the walk seed and randomization sequence.
///     </para>
///     <para>
///         <b>Storage-precision vs the legacy copy-TIN.</b> The legacy path
///         re-triangulated the vertices into a structurally different TIN, so
///         its stochastic walk occasionally terminates on a different rotation
///         of the same containing triangle, changing the facet arithmetic in
///         the last ulp. That last-ulp noise is inherent to the legacy path
///         itself (its pre-#826 random-order and post-#826 Hilbert-order
///         copies differed the same way) and is absorbed by Z storage:
///         every draped value lands in <c>Vertex._z</c>, which is a float.
///         Sampler-vs-legacy results are therefore asserted equal after float
///         rounding and within 2 ulps in double.
///     </para>
/// </remarks>
public class FrozenSurfaceSamplerTests
{
    private const double DomainSize = 1000.0;

    // ---------------------------------------------------------------
    // Bit-for-bit identity against the pre-mutation primary TIN
    // ---------------------------------------------------------------

    [Fact]
    public void Interpolate_MatchesFacetInterpolatorOverPreMutationTin_BitForBit()
    {
        var tin = BuildWavySurfaceTin(5000, sceneSeed: 42, withCoincidentDuplicates: true);
        var sampler = tin.CreateFrozenSurfaceSampler();
        var primary = new TriangularFacetInterpolator(tin);
        var primaryNavigator = tin.GetNavigator();

        // Queries span the hull and a band outside it (exercising the NaN +
        // nearest-vertex fallback used for off-hull constraint vertices).
        var rnd = new Random(1234);
        var interior = 0;
        var exterior = 0;
        for (var i = 0; i < 20000; i++)
        {
            var x = rnd.NextDouble() * (DomainSize + 200.0) - 100.0;
            var y = rnd.NextDouble() * (DomainSize + 200.0) - 100.0;

            var expected = primary.Interpolate(x, y, null);
            var actual = sampler.Interpolate(x, y, null);
            AssertSameBits(expected, actual, $"Interpolate({x}, {y})");

            if (double.IsNaN(expected))
            {
                exterior++;
                var expectedNearest = primaryNavigator.GetNearestVertex(x, y);
                var actualNearest = sampler.GetNearestVertex(x, y);
                Assert.NotNull(expectedNearest);
                Assert.NotNull(actualNearest);
                Assert.Same(expectedNearest, actualNearest);
            }
            else
            {
                interior++;
            }
        }

        // Sanity: the query band genuinely exercised both paths.
        Assert.True(interior > 1000, $"expected a substantial interior sample, got {interior}");
        Assert.True(exterior > 1000, $"expected a substantial exterior sample, got {exterior}");
    }

    [Fact]
    public void Interpolate_MatchesPreMutationTin_AtExactDataVertexPositions()
    {
        var tin = BuildWavySurfaceTin(2000, sceneSeed: 7, withCoincidentDuplicates: true);
        var sampler = tin.CreateFrozenSurfaceSampler();
        var primary = new TriangularFacetInterpolator(tin);

        // Querying exactly at a data vertex takes the vertex-tolerance early-out;
        // both surfaces must return the same vertex Z (merger groups included).
        foreach (var v in tin.GetVertices().Take(500))
        {
            var expected = primary.Interpolate(v.X, v.Y, null);
            var actual = sampler.Interpolate(v.X, v.Y, null);
            AssertSameBits(expected, actual, $"vertex query at ({v.X}, {v.Y})");
        }
    }

    [Fact]
    public void Interpolate_MatchesPreMutationTin_OnPerimeterEdgeMidpoints()
    {
        var tin = BuildWavySurfaceTin(2000, sceneSeed: 11, withCoincidentDuplicates: false);
        var sampler = tin.CreateFrozenSurfaceSampler();
        var primary = new TriangularFacetInterpolator(tin);

        // Midpoints of convex-hull edges exercise the perimeter (ghost-triangle)
        // branch of the facet evaluation: linear interpolation along the edge
        // when the query is within the precision threshold of the edge line.
        foreach (var edge in tin.GetPerimeter())
        {
            var a = edge.GetA();
            var b = edge.GetB();
            if (a.IsNullVertex() || b.IsNullVertex()) continue;

            var mx = (a.X + b.X) * 0.5;
            var my = (a.Y + b.Y) * 0.5;
            var expected = primary.Interpolate(mx, my, null);
            var actual = sampler.Interpolate(mx, my, null);
            AssertSameBits(expected, actual, $"perimeter midpoint ({mx}, {my})");
        }
    }

    [Fact]
    public void Sampler_IsUnaffectedByPrimaryTinMutation()
    {
        var tin = BuildWavySurfaceTin(2000, sceneSeed: 23, withCoincidentDuplicates: false);
        var sampler = tin.CreateFrozenSurfaceSampler();
        var primary = new TriangularFacetInterpolator(tin);

        // Record the pre-mutation answers, then mutate the primary TIN heavily
        // (this is exactly what constraint processing does after the snapshot).
        var rnd = new Random(555);
        var queries = new List<(double X, double Y, double Expected)>();
        for (var i = 0; i < 2000; i++)
        {
            var x = rnd.NextDouble() * DomainSize;
            var y = rnd.NextDouble() * DomainSize;
            queries.Add((x, y, primary.Interpolate(x, y, null)));
        }

        var mutator = new Random(556);
        var extra = new List<IVertex>();
        for (var i = 0; i < 5000; i++)
        {
            extra.Add(new Vertex(
                mutator.NextDouble() * DomainSize,
                mutator.NextDouble() * DomainSize,
                99.0,
                100000 + i));
        }

        tin.Add(extra);

        foreach (var (x, y, expected) in queries)
            AssertSameBits(expected, sampler.Interpolate(x, y, null), $"post-mutation Interpolate({x}, {y})");
    }

    // ---------------------------------------------------------------
    // Storage-precision identity against the legacy copy-TIN surface
    // ---------------------------------------------------------------

    [Fact]
    public void Interpolate_MatchesLegacyCopyTinSurface_AtStoragePrecision()
    {
        var tin = BuildWavySurfaceTin(5000, sceneSeed: 42, withCoincidentDuplicates: true);
        var sampler = tin.CreateFrozenSurfaceSampler();
        var (legacy, legacyNavigator) = BuildLegacySurface(tin);

        var rnd = new Random(4321);
        for (var i = 0; i < 20000; i++)
        {
            var x = rnd.NextDouble() * (DomainSize + 200.0) - 100.0;
            var y = rnd.NextDouble() * (DomainSize + 200.0) - 100.0;

            var expected = legacy.Interpolate(x, y, null);
            var actual = sampler.Interpolate(x, y, null);
            AssertStorageEquivalent(expected, actual, $"Interpolate({x}, {y})");

            if (double.IsNaN(expected))
            {
                var expectedNearest = legacyNavigator.GetNearestVertex(x, y);
                var actualNearest = sampler.GetNearestVertex(x, y);
                Assert.NotNull(expectedNearest);
                Assert.NotNull(actualNearest);
                AssertStorageEquivalent(
                    expectedNearest!.GetZ(),
                    actualNearest!.GetZ(),
                    $"GetNearestVertex({x}, {y}).Z");
            }
        }
    }

    // ---------------------------------------------------------------
    // End-to-end: AddConstraints drapes exactly what the legacy surface
    // would have draped
    // ---------------------------------------------------------------

    [Fact]
    public void AddConstraints_FacetPath_DrapesNaNVertices_ExactlyAsLegacySurfaceWould()
    {
        var vertices = BuildWavySurfaceVertices(3000, sceneSeed: 77, withCoincidentDuplicates: false);

        // TIN A runs the real AddConstraints (sampler path). TIN B is an
        // identical twin used to replay the legacy pre-interpolation sequence:
        // phase 0 (seed real-Z constraint vertices), snapshot surface, then
        // interpolate the NaN vertices in constraint order.
        var tinA = new IncrementalTin();
        tinA.Add(vertices, VertexOrder.Hilbert);
        var tinB = new IncrementalTin();
        tinB.Add(vertices, VertexOrder.Hilbert);

        foreach (var c in BuildMixedConstraints())
        foreach (var v in c.GetVertices())
        {
            if (!double.IsNaN(v.GetZ()))
                tinB.Add(v);
        }

        var (legacy, legacyNavigator) = BuildLegacySurface(tinB);
        var expectedZ = new Dictionary<(double X, double Y), double>();
        foreach (var c in BuildMixedConstraints())
        foreach (var v in c.GetVertices())
        {
            if (!double.IsNaN(v.GetZ())) continue;

            var z = legacy.Interpolate(v.X, v.Y, null);
            if (double.IsNaN(z))
            {
                var nearest = legacyNavigator.GetNearestVertex(v.X, v.Y);
                if (nearest != null && !double.IsNaN(nearest.GetZ()))
                    z = nearest.GetZ();
            }

            expectedZ[(v.X, v.Y)] = z;
        }

        Assert.True(expectedZ.Count >= 10, "scene must contain a meaningful number of NaN constraint vertices");

        tinA.AddConstraints(
            BuildMixedConstraints(),
            restoreConformity: true,
            preInterpolateZ: true,
            InterpolationType.TriangularFacet);

        var byPosition = tinA.GetVertices()
            .Where(v => expectedZ.ContainsKey((v.X, v.Y)))
            .ToDictionary(v => (v.X, v.Y), v => v);
        foreach (var (key, zExpected) in expectedZ)
        {
            Assert.True(byPosition.TryGetValue(key, out var vActual), $"constraint vertex at {key} not found in TIN");

            // Draped values are stored through Vertex._z (a float), so the
            // legacy-replayed expectation is compared at storage precision.
            AssertSameBits((float)zExpected, (float)vActual!.GetZ(), $"draped Z at {key}");
            Assert.True(((Vertex)vActual).HasInterpolatedZ(), $"draped vertex at {key} must carry the no-depth flag");
        }
    }

    [Fact]
    public void AddConstraints_FacetPath_IsDeterministic_AcrossIdenticalRuns()
    {
        static List<(double X, double Y, double Z)> RunOnce()
        {
            var tin = new IncrementalTin();
            tin.Add(BuildWavySurfaceVertices(3000, sceneSeed: 99, withCoincidentDuplicates: false), VertexOrder.Hilbert);
            tin.AddConstraints(
                BuildMixedConstraints(),
                restoreConformity: true,
                preInterpolateZ: true,
                InterpolationType.TriangularFacet);
            return tin.GetVertices()
                .Select(v => (v.X, v.Y, v.GetZ()))
                .OrderBy(t => t.X).ThenBy(t => t.Y).ThenBy(t => t.Item3)
                .ToList();
        }

        var first = RunOnce();
        var second = RunOnce();

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].X, second[i].X);
            Assert.Equal(first[i].Y, second[i].Y);
            AssertSameBits(first[i].Z, second[i].Z, $"vertex {i} Z");
        }
    }

    [Fact]
    public void AddConstraints_FacetPath_SplitVerticesDrapeOntoThePlaneSurface()
    {
        // Tilted plane z = 0.1 * x: every draped value (pre-interpolated
        // boundary vertices AND conformity split points on no-depth edges)
        // must lie on the plane. This pins the retained sampler serving
        // ComputeSplitZ during RestoreConformity, after the primary TIN has
        // been mutated by constraint tunneling.
        const double slope = 0.1;
        var tin = new IncrementalTin(1.0);
        var verts = new List<IVertex>();
        for (var x = 0.0; x <= 100.0; x += 5.0)
        for (var y = 0.0; y <= 100.0; y += 5.0)
            verts.Add(new Vertex(x, y, x * slope, verts.Count));
        tin.Add(verts, VertexOrder.Hilbert);

        var boundary = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(12.3, 12.3, double.NaN, 5001),
            new Vertex(87.7, 13.1, double.NaN, 5002),
            new Vertex(88.4, 86.9, double.NaN, 5003),
            new Vertex(11.6, 87.7, double.NaN, 5004),
        });

        tin.AddConstraints(
            new List<IConstraint> { boundary },
            restoreConformity: true,
            preInterpolateZ: true,
            InterpolationType.TriangularFacet);

        var flagged = tin.GetVertices()
            .OfType<Vertex>()
            .Where(v => v.HasInterpolatedZ())
            .ToList();
        Assert.True(flagged.Count >= 4, "expected draped boundary vertices (and usually splits) to be flagged");
        foreach (var v in flagged)
            Assert.True(
                Math.Abs(v.GetZ() - v.X * slope) < 1e-6,
                $"draped vertex ({v.X}, {v.Y}) z={v.GetZ()} deviates from the plane value {v.X * slope}");
    }

    [Fact]
    public void AddConstraints_NonFacetType_StillDrapesViaLegacyCopyTinPath()
    {
        // NaturalNeighbor keeps the legacy copy-TIN path; this pins that the
        // #800 rewiring did not disturb it.
        var tin = new IncrementalTin();
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 10));
        tin.Add(new Vertex(0, 10, 10));
        tin.Add(new Vertex(10, 10, 20));

        var constraint = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(4, 4, double.NaN),
            new Vertex(6, 4, double.NaN),
            new Vertex(5, 6, double.NaN),
        });

        tin.AddConstraints(
            new List<IConstraint> { constraint },
            restoreConformity: true,
            preInterpolateZ: true,
            InterpolationType.NaturalNeighbor);

        foreach (var (x, y) in new[] { (4.0, 4.0), (6.0, 4.0), (5.0, 6.0) })
        {
            var v = tin.GetVertices().First(vv => vv.X == x && vv.Y == y);
            Assert.False(double.IsNaN(v.GetZ()), $"NaN constraint vertex ({x},{y}) must be draped by the legacy path");
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static void AssertSameBits(double expected, double actual, string context)
    {
        if (double.IsNaN(expected) && double.IsNaN(actual)) return;
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{context}: expected {expected:R} (0x{BitConverter.DoubleToInt64Bits(expected):X16}) "
            + $"but got {actual:R} (0x{BitConverter.DoubleToInt64Bits(actual):X16})");
    }

    private static void AssertSameBits(float expected, float actual, string context)
    {
        if (float.IsNaN(expected) && float.IsNaN(actual)) return;
        Assert.True(
            BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual),
            $"{context}: expected {expected:R} but got {actual:R} at float storage precision");
    }

    /// <summary>
    ///     Asserts the values are equal at the precision that reaches any
    ///     consumer — identical after float rounding (all draped Z lands in
    ///     the float <c>Vertex._z</c>) — and within 2 ulps in double, which
    ///     bounds the legacy path's own walk-termination noise while still
    ///     failing loudly on any real defect (a wrong triangle or plane is
    ///     billions of ulps away).
    /// </summary>
    private static void AssertStorageEquivalent(double expected, double actual, string context)
    {
        if (double.IsNaN(expected) && double.IsNaN(actual)) return;

        AssertSameBits((float)expected, (float)actual, context);

        var ulpDistance = Math.Abs(
            BitConverter.DoubleToInt64Bits(expected) - BitConverter.DoubleToInt64Bits(actual));
        Assert.True(
            ulpDistance <= 2,
            $"{context}: expected {expected:R} but got {actual:R} ({ulpDistance} ulps apart)");
    }

    /// <summary>
    ///     Reconstructs the legacy copy-TIN interpolation surface exactly as
    ///     the pre-#800 AddConstraints built it: GetVertices() snapshot,
    ///     Hilbert-ordered re-triangulation with preallocation, a facet
    ///     interpolator over the copy, and a separate navigator for the
    ///     nearest-vertex fallback.
    /// </summary>
    private static (TriangularFacetInterpolator Interpolator, IIncrementalTinNavigator Navigator)
        BuildLegacySurface(IncrementalTin tin)
    {
        var originalVertices = tin.GetVertices();
        var copy = new IncrementalTin(tin.GetNominalPointSpacing());
        copy.PreAllocateForVertices(originalVertices.Count);
        copy.Add(originalVertices, VertexOrder.Hilbert);
        return (new TriangularFacetInterpolator(copy), copy.GetNavigator());
    }

    private static double SurfaceZ(double x, double y)
    {
        return Math.Sin(x * 0.011) * 10.0 + Math.Cos(y * 0.013) * 8.0 + x * 0.002 + y * 0.001;
    }

    private static List<IVertex> BuildWavySurfaceVertices(int count, int sceneSeed, bool withCoincidentDuplicates)
    {
        var rnd = new Random(sceneSeed);
        var vertices = new List<IVertex>(count + 2);
        for (var i = 0; i < count; i++)
        {
            var x = rnd.NextDouble() * DomainSize;
            var y = rnd.NextDouble() * DomainSize;
            vertices.Add(new Vertex(x, y, SurfaceZ(x, y), i));
        }

        if (withCoincidentDuplicates)
        {
            // Exact coincident samples form VertexMergerGroups; the sampler
            // shares vertex instances with the TIN, so group resolution must
            // flow through identically.
            vertices.Add(new Vertex(vertices[10].X, vertices[10].Y, SurfaceZ(vertices[10].X, vertices[10].Y) + 1.5, count));
            vertices.Add(new Vertex(vertices[20].X, vertices[20].Y, SurfaceZ(vertices[20].X, vertices[20].Y) - 2.5, count + 1));
        }

        return vertices;
    }

    private static IncrementalTin BuildWavySurfaceTin(int count, int sceneSeed, bool withCoincidentDuplicates)
    {
        var tin = new IncrementalTin();
        tin.Add(BuildWavySurfaceVertices(count, sceneSeed, withCoincidentDuplicates), VertexOrder.Hilbert);
        return tin;
    }

    /// <summary>
    ///     Constraints mirroring the RM usage: an in-hull no-depth boundary
    ///     polygon (all NaN), a mixed polygon (real-Z shoreline corners plus
    ///     NaN boundary vertices), an off-hull polygon (nearest-vertex
    ///     fallback), and a linear constraint with NaN vertices. Rebuilt fresh
    ///     on every call because AddConstraints consumes/mutates instances.
    /// </summary>
    private static List<IConstraint> BuildMixedConstraints()
    {
        var noDepth = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(101.3, 102.7, double.NaN, 9001),
            new Vertex(402.9, 108.1, double.NaN, 9002),
            new Vertex(405.7, 411.9, double.NaN, 9003),
            new Vertex(104.1, 408.3, double.NaN, 9004),
        });

        var mixed = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(553.3, 551.1, 0.0, 9101),
            new Vertex(901.7, 553.9, double.NaN, 9102),
            new Vertex(903.9, 902.3, double.NaN, 9103),
            new Vertex(551.9, 904.7, 0.0, 9104),
        });

        var offHull = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(1100.0, 1100.0, double.NaN, 9201),
            new Vertex(1150.0, 1100.0, double.NaN, 9202),
            new Vertex(1100.0, 1150.0, double.NaN, 9203),
        });

        var line = new LinearConstraint(new List<IVertex>
        {
            new Vertex(201.1, 703.3, double.NaN, 9301),
            new Vertex(455.5, 655.7, double.NaN, 9302),
            new Vertex(707.7, 201.9, double.NaN, 9303),
        });

        return new List<IConstraint> { noDepth, mixed, offHull, line };
    }
}
