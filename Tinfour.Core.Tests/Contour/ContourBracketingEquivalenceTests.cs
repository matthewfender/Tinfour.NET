namespace Tinfour.Core.Tests.Contour;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Equivalence tests for the ticket-827 single bracketing sweep in
///     <see cref="ContourBuilderForTin"/>: for identical inputs the production builder must
///     produce output identical to <see cref="LegacyContourBuilderForTin"/> (the frozen
///     pre-827 per-level full-TIN sweep) — same contours in the same order with bit-identical
///     geometry, and the same regions.
/// </summary>
public class ContourBracketingEquivalenceTests
{
    [Fact]
    public void SmoothSurface_ManyLevels_MatchesLegacyExactly()
    {
        var tin = BuildSyntheticBathymetryTin(40, 40, seed: 42);
        var levels = new[] { -18.0, -16, -14, -12, -10, -8, -6, -4, -2, -1, -0.5 };

        AssertEquivalent(tin, null, levels, buildRegions: true);
    }

    [Fact]
    public void LevelsOutsideDataRange_MatchesLegacyExactly()
    {
        var tin = BuildSyntheticBathymetryTin(20, 20, seed: 7);

        // Bracketing must handle levels entirely below, inside, and above the data range.
        var levels = new[] { -1000.0, -10, 1000 };

        AssertEquivalent(tin, null, levels, buildRegions: true);
    }

    [Fact]
    public void PlateauExactlyOnLevel_MatchesLegacyExactly()
    {
        // A flat plateau whose z exactly equals a contour level exercises the on-level
        // (zA == z && zB == z) seed case and the through-vertex transitions.
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();
        var index = 0;
        for (var r = 0; r < 25; r++)
        {
            for (var c = 0; c < 25; c++)
            {
                // Bowl shape clamped to a flat top at exactly 5.0
                var dx = c - 12.0;
                var dy = r - 12.0;
                var z = Math.Min(5.0, 0.1 * (dx * dx + dy * dy) - 8.0);
                vertices.Add(new Vertex(c * 10.0, r * 10.0, z, index++));
            }
        }

        tin.Add(vertices);

        var levels = new[] { -6.0, -3, 0, 5.0 };

        AssertEquivalent(tin, null, levels, buildRegions: true);
    }

    [Fact]
    public void NaNValuator_BrokenContourSemantics_MatchLegacyExactly()
    {
        // Ticket-777 semantics: a vertex whose valuated z is NaN silently discards the whole
        // contour crossing it. The bracketing sweep must preserve that behaviour exactly
        // (NaN endpoints produce no candidates; FollowContour behaviour is untouched).
        var tin = BuildSyntheticBathymetryTin(25, 25, seed: 11);
        var poisoned = new NaNPoisoningValuator(poisonEvery: 97);

        var levels = new[] { -16.0, -12, -8, -4, -2 };

        AssertEquivalent(tin, poisoned, levels, buildRegions: false);
    }

    [Fact]
    public void ConstrainedRegionsOnly_MatchesLegacyExactly()
    {
        var tin = BuildSyntheticBathymetryTin(25, 25, seed: 3);

        // A polygon constraint covering part of the surface; contours restricted to it.
        var square = new PolygonConstraint();
        square.Add(new Vertex(40, 40, 0, 9001));
        square.Add(new Vertex(200, 40, 0, 9002));
        square.Add(new Vertex(200, 200, 0, 9003));
        square.Add(new Vertex(40, 200, 0, 9004));
        square.Complete();
        tin.AddConstraints(new List<IConstraint> { square }, restoreConformity: true);

        var levels = new[] { -16.0, -12, -8, -4 };

        AssertEquivalent(tin, null, levels, buildRegions: false, constrainedRegionsOnly: true);
    }

    private static void AssertEquivalent(
        IIncrementalTin tin,
        IVertexValuator? valuator,
        double[] levels,
        bool buildRegions,
        bool constrainedRegionsOnly = false)
    {
        var legacy = new LegacyContourBuilderForTin(tin, valuator, levels, buildRegions, constrainedRegionsOnly);
        var current = new ContourBuilderForTin(tin, valuator, levels, buildRegions, constrainedRegionsOnly);

        var legacyContours = legacy.GetContours();
        var currentContours = current.GetContours();

        // Guard against a vacuous pass: every fixture is designed to produce contours.
        Assert.NotEmpty(legacyContours);

        Assert.Equal(legacyContours.Count, currentContours.Count);
        for (var i = 0; i < legacyContours.Count; i++)
        {
            var l = legacyContours[i];
            var c = currentContours[i];
            Assert.Equal(l.GetZ(), c.GetZ());
            Assert.Equal(l.IsClosed(), c.IsClosed());
            Assert.Equal(l.GetLeftIndex(), c.GetLeftIndex());
            Assert.Equal(l.GetRightIndex(), c.GetRightIndex());
            Assert.Equal(l.Size(), c.Size());
            Assert.Equal(l.GetXY(), c.GetXY()); // bit-identical geometry, same point order
        }

        var legacyRegions = legacy.GetRegions();
        var currentRegions = current.GetRegions();
        Assert.Equal(legacyRegions.Count, currentRegions.Count);
        for (var i = 0; i < legacyRegions.Count; i++)
        {
            Assert.Equal(legacyRegions[i].GetAbsoluteArea(), currentRegions[i].GetAbsoluteArea());
            Assert.Equal(legacyRegions[i].GetRegionType(), currentRegions[i].GetRegionType());
            Assert.Equal(legacyRegions[i].GetXY(), currentRegions[i].GetXY());
        }
    }

    /// <summary>
    ///     Builds a jittered-grid TIN with a smooth multi-basin bathymetry-like surface
    ///     (depths roughly -20..0) so that contour lines at several levels form a mix of
    ///     closed interior loops and open perimeter-terminated lines.
    /// </summary>
    private static IncrementalTin BuildSyntheticBathymetryTin(int rows, int cols, int seed)
    {
        var random = new Random(seed);
        var vertices = new List<IVertex>(rows * cols);
        var index = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = c * 10.0 + (random.NextDouble() - 0.5) * 4.0;
                var y = r * 10.0 + (random.NextDouble() - 0.5) * 4.0;
                var z = -10.0
                        - 8.0 * Math.Sin(x * 0.021) * Math.Cos(y * 0.017)
                        + 2.0 * Math.Sin(x * 0.09) * Math.Sin(y * 0.083);
                vertices.Add(new Vertex(x, y, z, index++));
            }
        }

        var tin = new IncrementalTin();
        tin.Add(vertices, VertexOrder.Hilbert);
        Assert.True(tin.IsBootstrapped());
        return tin;
    }

    /// <summary>
    ///     A valuator that returns NaN for every Nth vertex (by vertex index), used to pin
    ///     the ticket-777 broken-contour discard semantics.
    /// </summary>
    private sealed class NaNPoisoningValuator : IVertexValuator
    {
        private readonly int _poisonEvery;

        public NaNPoisoningValuator(int poisonEvery)
        {
            _poisonEvery = poisonEvery;
        }

        public double Value(IVertex v)
        {
            if (v.IsNullVertex()) return double.NaN;
            if (v.GetIndex() % _poisonEvery == 0) return double.NaN;
            return v.GetZ();
        }
    }
}
