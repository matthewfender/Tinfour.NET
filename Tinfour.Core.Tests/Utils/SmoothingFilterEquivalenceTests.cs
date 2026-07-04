namespace Tinfour.Core.Tests.Utils;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;

using Xunit;

/// <summary>
///     Equivalence tests for the ticket-829 parallelized <see cref="SmoothingFilter"/>: for
///     identical inputs the production filter must produce output identical to
///     <see cref="LegacySmoothingFilter"/> (the frozen pre-829 sequential implementation) —
///     bit-identical smoothed Z for every vertex, and the same MinZ/MaxZ/VertexCount.
/// </summary>
public class SmoothingFilterEquivalenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(25)]
    public void SmoothSurface_VariousPassCounts_MatchesLegacyExactly(int passes)
    {
        var tin = BuildSyntheticTin(40, 40, seed: 42);

        AssertEquivalent(tin, passes);
    }

    [Fact]
    public void ConstrainedTin_ConstraintMembersUnsmoothed_MatchesLegacyExactly()
    {
        var tin = BuildSyntheticTin(30, 30, seed: 7);

        // A polygon constraint through the interior: constraint members must keep their
        // original Z, and no interior vertex may pull values across the constraint boundary.
        var square = new PolygonConstraint();
        square.Add(new Vertex(45, 45, -5.0, 9001));
        square.Add(new Vertex(180, 45, -5.0, 9002));
        square.Add(new Vertex(180, 180, -5.0, 9003));
        square.Add(new Vertex(45, 180, -5.0, 9004));
        square.Complete();
        tin.AddConstraints(new List<IConstraint> { square }, restoreConformity: true);

        var filter = AssertEquivalent(tin, passes: 6);

        // Constraint members pass their original Z through unchanged.
        foreach (var v in tin.GetVertices())
        {
            if (v is Vertex vertex && vertex.IsConstraintMember())
                Assert.Equal(vertex.GetZ(), filter.Value(vertex));
        }
    }

    [Fact]
    public void NaNZValues_SkippedInAccumulation_MatchesLegacyExactly()
    {
        // NaN Z on scattered interior vertices exercises the skip-invalid-Z path inside the
        // smoothing pass accumulation (and NaN propagation into MinZ/MaxZ is excluded by both
        // implementations' comparisons).
        var tin = BuildSyntheticTin(25, 25, seed: 11, poisonEvery: 37);

        AssertEquivalent(tin, passes: 6);
    }

    [Fact]
    public void PerimeterVertices_Unsmoothed_MatchesLegacyExactly()
    {
        // Small TIN: a large fraction of vertices are on the perimeter (no valid pinwheel).
        var tin = BuildSyntheticTin(5, 5, seed: 3);

        var filter = AssertEquivalent(tin, passes: 6);
        Assert.True(filter.VertexCount > 0);
    }

    [Fact]
    public void RepeatedConstruction_IsDeterministic()
    {
        // The parallel schedule must not affect the output: two constructions over the same
        // TIN must agree bit-for-bit.
        var tin = BuildSyntheticTin(40, 40, seed: 99);

        var first = new SmoothingFilter(tin, 6);
        var second = new SmoothingFilter(tin, 6);

        Assert.Equal(first.VertexCount, second.VertexCount);
        Assert.Equal(first.MinZ, second.MinZ);
        Assert.Equal(first.MaxZ, second.MaxZ);
        foreach (var v in tin.GetVertices())
            Assert.Equal(first.Value(v), second.Value(v));
    }

    [Fact]
    public void Value_NullAndForeignVertices_MatchesLegacyBehaviour()
    {
        var tin = BuildSyntheticTin(10, 10, seed: 5);
        var filter = new SmoothingFilter(tin, 6);

        Assert.True(double.IsNaN(filter.Value(null!)));

        // A vertex the filter has never seen falls back to its own Z.
        var foreign = new Vertex(1e6, 1e6, -42.5, 123456);
        Assert.Equal(-42.5, filter.Value(foreign));
    }

    [Fact]
    public void Timings_ArePopulated()
    {
        var tin = BuildSyntheticTin(20, 20, seed: 21);
        var filter = new SmoothingFilter(tin, 6);

        Assert.NotNull(filter.Timings);
        Assert.Equal(6, filter.Timings.Passes.Count);
        Assert.Equal(filter.VertexCount, filter.Timings.VertexCount);
        Assert.InRange(filter.Timings.SmoothedVertexCount, 1, filter.Timings.VertexCount);
        Assert.True(filter.Timings.Total > TimeSpan.Zero);
    }

    [Fact]
    public void NPassesBelowOne_Throws()
    {
        var tin = BuildSyntheticTin(5, 5, seed: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new SmoothingFilter(tin, 0));
    }

    private static SmoothingFilter AssertEquivalent(IIncrementalTin tin, int passes)
    {
        var legacy = new LegacySmoothingFilter(tin, passes);
        var current = new SmoothingFilter(tin, passes);

        // Guard against a vacuous pass: every fixture is designed to smooth something.
        Assert.True(legacy.VertexCount > 0);

        Assert.Equal(legacy.VertexCount, current.VertexCount);
        Assert.Equal(legacy.MinZ, current.MinZ);
        Assert.Equal(legacy.MaxZ, current.MaxZ);

        var vertices = tin.GetVertices();
        Assert.NotEmpty(vertices);
        foreach (var v in vertices)
        {
            // Assert.Equal on doubles without tolerance is an exact comparison
            // (and treats two NaNs as equal), which is what bit-identical demands.
            Assert.Equal(legacy.Value(v), current.Value(v));
        }

        return current;
    }

    /// <summary>
    ///     Builds a jittered-grid TIN with a smooth bowl surface. Optionally poisons every
    ///     n-th vertex Z with NaN.
    /// </summary>
    private static IncrementalTin BuildSyntheticTin(int cols, int rows, int seed, int poisonEvery = 0)
    {
        var rnd = new Random(seed);
        var vertices = new List<IVertex>(cols * rows);
        var index = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = c * 7.5 + (rnd.NextDouble() - 0.5) * 3.0;
                var y = r * 7.5 + (rnd.NextDouble() - 0.5) * 3.0;
                var dx = c - cols / 2.0;
                var dy = r - rows / 2.0;
                var z = -(5.0 + 0.05 * (dx * dx + dy * dy) + 1.5 * Math.Sin(x / 11.0) * Math.Cos(y / 13.0));
                if (poisonEvery > 0 && index % poisonEvery == poisonEvery - 1)
                    z = double.NaN;
                vertices.Add(new Vertex(x, y, z, index++));
            }
        }

        var tin = new IncrementalTin();
        tin.Add(vertices);
        return tin;
    }
}
