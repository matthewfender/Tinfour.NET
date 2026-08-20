/*
 * Copyright 2026 G.W. Lucas / M. Fender
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

namespace Tinfour.Core.Tests.Contour;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Regression tests for RM ticket 501: the trans-vertex sweep in
///     <see cref="ContourBuilderForTin"/> pinwheeled forever when a contour walk entered
///     a vertex whose ring offered no exit transition. That happens when vertices sit
///     exactly on a contour level (a plateau - common when a small depth range quantised
///     to float32 puts many vertices exactly on a level) and NaN-valuated neighbours
///     (e.g. smoothing/ghost artefacts or NaN-filled vertices) break every remaining
///     below-to-above exit pair. The builder now abandons such walks (reported via
///     <see cref="ContourBuilderForTin.AbandonedContourCount"/>) instead of hanging,
///     and supports cooperative cancellation.
/// </summary>
public class ContourWalkTerminationTests
{
    /// <summary>
    ///     Builds the seeded random plateau TIN used by the regression tests: a 10x10
    ///     jittered grid whose z values are 4, 6 or (60% of the time) exactly 5.0.
    ///     With the NaN-poisoning valuator, seeds 0-7 all made the pre-fix builder
    ///     pinwheel forever at level 5.0 (verified by an 8-second watchdog probe).
    /// </summary>
    private static IncrementalTin BuildPlateauTin(int seed)
    {
        var rng = new Random(seed);
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();
        var index = 0;
        for (var r = 0; r < 10; r++)
        {
            for (var c = 0; c < 10; c++)
            {
                var x = c * 10.0 + rng.NextDouble() * 2.0;
                var y = r * 10.0 + rng.NextDouble() * 2.0;
                var roll = rng.Next(10);
                double z = roll switch
                {
                    0 or 1 => 4.0,
                    2 or 3 => 6.0,
                    _ => 5.0,
                };
                vertices.Add(new Vertex(x, y, z, index++));
            }
        }

        tin.Add(vertices);
        return tin;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NaNPoisonedPlateau_CompletesInsteadOfHanging(int seed)
    {
        var tin = BuildPlateauTin(seed);
        var valuator = new IndexPoisoningValuator(poisonEvery: 7);

        // Pre-fix this construction never returned. Run it on a worker so a regression
        // fails the test instead of hanging the whole suite.
        var task = Task.Run(() =>
        {
            var builder = new ContourBuilderForTin(tin, valuator, new[] { 5.0 });
            return (builder.AbandonedContourCount, builder.GetContours());
        });

        Assert.True(task.Wait(TimeSpan.FromSeconds(30)), "contour construction did not terminate");

        var (abandoned, contours) = task.Result;

        // The degenerate walks must be detected (this is what previously hung) and the
        // remaining well-formed contours must still be produced.
        Assert.True(abandoned > 0, "expected at least one abandoned degenerate walk");
        Assert.NotEmpty(contours);
    }

    [Fact]
    public void CleanSurface_AbandonsNothing()
    {
        // Same plateau surface but with the default valuator (no NaN poisoning): every
        // walk has a well-defined continuation and the guards must never fire.
        var tin = BuildPlateauTin(0);

        var builder = new ContourBuilderForTin(tin, null, new[] { 5.0 });

        Assert.Equal(0, builder.AbandonedContourCount);
    }

    [Fact]
    public void PreCancelledToken_ThrowsOperationCanceled()
    {
        var tin = BuildPlateauTin(0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            _ = new ContourBuilderForTin(tin, null, new[] { 5.0 }, cancellationToken: cts.Token));
    }

    private sealed class IndexPoisoningValuator(int poisonEvery) : IVertexValuator
    {
        public double Value(IVertex v)
        {
            if (v.GetIndex() % poisonEvery == 0) return double.NaN;
            return v.GetZ();
        }
    }
}
