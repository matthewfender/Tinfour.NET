/*
 * Copyright 2026 Matt Fender.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Tinfour.Core.Tests.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Covers the caller-provided <see cref="IRasterData"/> sink overload of
///     <see cref="TinRasterizer.CreateRaster(IRasterData, ValueTuple{double, double, double, double}, CancellationToken)"/>
///     and the scratch-buffer reuse inside <see cref="NaturalNeighborInterpolator"/>
///     (both added to cut per-cell allocation churn on large rasters).
/// </summary>
public class RasterSinkAndScratchReuseTests
{
    private static IncrementalTin CreateTestTin()
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0, 0),
                               new Vertex(10, 0, 10, 1),
                               new Vertex(10, 10, 20, 2),
                               new Vertex(0, 10, 30, 3),
                               new Vertex(5, 5, 15, 4),
                               new Vertex(2, 8, 22, 5),
                               new Vertex(8, 2, 8, 6)
                           };
        vertices.ForEach((IVertex v) => tin.Add(v));
        return tin;
    }

    [Fact]
    public void CreateRaster_SinkOverload_MatchesDataTypeOverload()
    {
        // Arrange
        using var tin = CreateTestTin();
        var bounds = (Left: 0.0, Top: 0.0, Width: 10.0, Height: 10.0);
        var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);

        // Act - reference path allocates its own Float32 grid
        var reference = rasterizer.CreateRaster(40, 40, RasterDataType.Float32, bounds);

        // Sink path writes into a caller-provided grid of the same dimensions
        var sink = new Float32RasterData(reference.Width, reference.Height);
        var viaSink = rasterizer.CreateRaster(sink, bounds);

        // Assert
        Assert.Same(sink, viaSink.RasterData);
        Assert.Equal(reference.Width, viaSink.Width);
        Assert.Equal(reference.Height, viaSink.Height);
        Assert.Equal(reference.NoDataCount, viaSink.NoDataCount);
        Assert.Equal(reference.CellWidth, viaSink.CellWidth, 12);
        Assert.Equal(reference.CellHeight, viaSink.CellHeight, 12);

        for (var y = 0; y < reference.Height; y++)
        for (var x = 0; x < reference.Width; x++)
        {
            var expected = reference.RasterData.GetValue(x, y);
            var actual = viaSink.RasterData.GetValue(x, y);
            if (double.IsNaN(expected))
                Assert.True(double.IsNaN(actual), $"Cell ({x},{y}) expected NaN, got {actual}");
            else
                Assert.Equal(expected, actual, 6);
        }
    }

    [Fact]
    public void CreateRaster_SinkOverload_NullSink_Throws()
    {
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);

        Assert.Throws<ArgumentNullException>(
            () => rasterizer.CreateRaster(null!, (0.0, 0.0, 10.0, 10.0)));
    }

    [Fact]
    public void Interpolate_WarmInstance_AgreesWithFreshInstance()
    {
        // The interpolator reuses internal scratch buffers between calls; a warm instance
        // that has served many queries must agree with a fresh instance answering a single
        // query. Comparison uses a tight tolerance rather than exact equality: the
        // navigator's cached search edge (pre-existing behaviour) can rotate the envelope
        // traversal order between instances, perturbing floating-point summation at the
        // ulp level, whereas scratch corruption would produce grossly wrong values.
        using var tin = CreateTestTin();
        var warm = new NaturalNeighborInterpolator(tin);

        for (var y = 1; y <= 9; y += 2)
        for (var x = 1; x <= 9; x += 2)
        {
            var warmResult = warm.Interpolate(x + 0.25, y + 0.25, null);
            var freshResult = new NaturalNeighborInterpolator(tin).Interpolate(x + 0.25, y + 0.25, null);
            Assert.Equal(freshResult, warmResult, 9);
        }
    }

    [Fact]
    public void GetBowyerWatsonEnvelope_ReturnsIndependentListPerCall()
    {
        // Public contract: callers own the returned list; an intervening
        // Interpolate call (which uses the internal scratch) must not mutate it.
        using var tin = CreateTestTin();
        var interpolator = new NaturalNeighborInterpolator(tin);

        var envelope1 = interpolator.GetBowyerWatsonEnvelope(4, 4);
        var snapshot = new List<IQuadEdge>(envelope1);

        interpolator.Interpolate(6.5, 6.5, null);
        var envelope2 = interpolator.GetBowyerWatsonEnvelope(6.5, 6.5);

        Assert.NotSame(envelope1, envelope2);
        Assert.Equal(snapshot.Count, envelope1.Count);
        for (var i = 0; i < snapshot.Count; i++)
            Assert.True(snapshot[i].Equals(envelope1[i]), $"Envelope entry {i} was mutated");
    }

    [Fact]
    public void GetSibsonCoordinates_ReturnsExactLengthArrayPerCall()
    {
        // Public contract: a fresh, exact-length weights array per call, summing to 1.
        using var tin = CreateTestTin();
        var interpolator = new NaturalNeighborInterpolator(tin);

        var envelope = interpolator.GetBowyerWatsonEnvelope(4, 4);
        Assert.True(envelope.Count >= 3);

        var w1 = interpolator.GetSibsonCoordinates(envelope, 4, 4);
        interpolator.Interpolate(6.5, 6.5, null); // exercises the scratch path
        var w2 = interpolator.GetSibsonCoordinates(envelope, 4, 4);

        Assert.NotNull(w1);
        Assert.NotNull(w2);
        Assert.NotSame(w1, w2);
        Assert.Equal(envelope.Count, w1!.Length);
        Assert.Equal(1.0, w1.Sum(), 10);
        Assert.Equal(w1, w2);
    }
}
