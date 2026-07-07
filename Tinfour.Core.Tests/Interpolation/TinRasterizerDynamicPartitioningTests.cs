namespace Tinfour.Core.Tests.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Equivalence tests for the ticket-830 dynamic row partitioning in
///     <see cref="TinRasterizer" />: the parallel row-granular fill must produce exactly the
///     values a serial single-interpolator per-cell loop produces, with a matching NoData
///     count, regardless of grid height divisibility by the core count.
/// </summary>
public class TinRasterizerDynamicPartitioningTests
{
    [Theory]
    [InlineData(64, 64, InterpolationType.TriangularFacet)]
    [InlineData(61, 67, InterpolationType.TriangularFacet)] // odd sizes: not divisible by any plausible core count
    [InlineData(8, 3, InterpolationType.TriangularFacet)] // fewer rows than cores
    [InlineData(61, 67, InterpolationType.NaturalNeighbor)]
    public void CreateRaster_MatchesSerialReference_CellByCell(int width, int height, InterpolationType interpolationType)
    {
        var tin = BuildTin(seed: 5);
        var options = new InterpolatorOptions { MaxInterpolationDistance = 40.0 };
        var rasterizer = new TinRasterizer(tin, interpolationType, options);

        // Padded bounds: outer cells are far from data and become NaN, exercising the
        // imbalanced all-NaN row shape the dynamic partitioning targets.
        var bounds = (Left: -100.0, Top: -100.0, Width: 400.0, Height: 400.0);

        var sink = new Float64RasterData(width, height);
        var result = rasterizer.CreateRaster(sink, bounds);

        // Serial reference: one interpolator, same per-cell computation in row order,
        // with the same per-row walk-state reset the production fill performs (rows are
        // pure functions of their coordinates, so serial == parallel bit-for-bit).
        var reference = new Float64RasterData(width, height);
        var interpolator = InterpolatorFactory.Create(tin, interpolationType, options);
        var cellWidth = bounds.Width / width;
        var cellHeight = bounds.Height / height;
        var referenceNoData = 0;
        for (var y = 0; y < height; y++)
        {
            interpolator.ResetForChangeToTin();
            for (var x = 0; x < width; x++)
            {
                var value = interpolator.Interpolate(
                    bounds.Left + (x + 0.5) * cellWidth,
                    bounds.Top + (y + 0.5) * cellHeight,
                    null);
                reference.SetValue(x, y, value);
                if (double.IsNaN(value)) referenceNoData++;
            }
        }

        Assert.True(referenceNoData > 0, "padded bounds must produce NaN cells for this fixture");
        Assert.True(referenceNoData < width * height, "fixture must also produce data cells");
        Assert.Equal(referenceNoData, result.NoDataCount);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Assert.Equal(reference.GetValue(x, y), sink.GetValue(x, y));
            }
        }
    }

    [Fact]
    public void CreateRaster_RepeatedRuns_AreBitIdentical()
    {
        // Run-to-run determinism is load-bearing: the RM campaign uses artifact SHA
        // equality as its regression oracle, so dynamic scheduling must not leak into
        // the output (that's what the per-row walk-state reset guarantees).
        var tin = BuildTin(seed: 21);
        var options = new InterpolatorOptions { MaxInterpolationDistance = 40.0 };
        var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor, options);
        var bounds = (Left: -100.0, Top: -100.0, Width: 400.0, Height: 400.0);

        var first = new Float64RasterData(97, 89);
        var second = new Float64RasterData(97, 89);
        var firstResult = rasterizer.CreateRaster(first, bounds);
        var secondResult = rasterizer.CreateRaster(second, bounds);

        Assert.Equal(firstResult.NoDataCount, secondResult.NoDataCount);
        for (var y = 0; y < 89; y++)
        {
            for (var x = 0; x < 97; x++)
            {
                Assert.Equal(first.GetValue(x, y), second.GetValue(x, y));
            }
        }
    }

    [Fact]
    public void CreateRaster_Cancelled_Throws()
    {
        var tin = BuildTin(seed: 9);
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);
        var bounds = (Left: 0.0, Top: 0.0, Width: 200.0, Height: 200.0);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            rasterizer.CreateRaster(new Float64RasterData(64, 64), bounds, cts.Token));
    }

    private static IncrementalTin BuildTin(int seed)
    {
        var random = new Random(seed);
        var vertices = new List<IVertex>();
        for (var i = 0; i < 400; i++)
        {
            var x = random.NextDouble() * 200.0;
            var y = random.NextDouble() * 200.0;
            var z = 5.0 + 3.0 * Math.Sin(x * 0.05) * Math.Cos(y * 0.04);
            vertices.Add(new Vertex(x, y, z, i));
        }

        var tin = new IncrementalTin();
        tin.Add(vertices, VertexOrder.Hilbert);
        Assert.True(tin.IsBootstrapped());
        return tin;
    }
}
