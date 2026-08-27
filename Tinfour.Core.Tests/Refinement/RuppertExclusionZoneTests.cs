/*
 * Copyright 2026 Gary W. Lucas.
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

namespace Tinfour.Core.Tests.Refinement;

using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Xunit;

/// <summary>
/// Tests for <see cref="RuppertOptions.ExclusionZones"/>: refinement must never insert a
/// vertex inside an exclusion zone, and zone-interior triangles are left coarse, while
/// refinement proceeds normally elsewhere (#1274).
/// </summary>
public class RuppertExclusionZoneTests
{
    [Fact]
    public void Refine_WithExclusionZone_InsertsNoVerticesInsideZone()
    {
        var tin = CreateSparseConstrainedTin();

        var options = new RuppertOptions(25.0)
        {
            RefineOnlyInsideConstraints = true,
            MaxIterations = 10_000,
            // Right half of the square is excluded
            ExclusionZones =
            [
                new List<(double X, double Y)> { (50, -10), (110, -10), (110, 110), (50, 110) }
            ]
        };

        var inserted = RunToCompletion(tin, options);

        Assert.NotEmpty(inserted); // refinement must still run on the non-excluded side
        Assert.All(inserted, v => Assert.False(
            v.X > 50.0 && v.X < 110.0 && v.Y > -10.0 && v.Y < 110.0,
            $"vertex ({v.X:F2},{v.Y:F2}) was inserted inside the exclusion zone"));
    }

    [Fact]
    public void Refine_WithExclusionZoneHole_AllowsInsertionsInsideHole()
    {
        // The hole needs its own skinny cluster: candidates for triangles at its rim are
        // rejected when they land in the surrounding excluded ring, so refinability must
        // come from geometry whose Steiner points stay local to the hole.
        var tin = CreateSparseConstrainedTin((65, 50), (70, 50.8), (75, 50.4), (68, 62), (73, 38));

        var options = new RuppertOptions(25.0)
        {
            RefineOnlyInsideConstraints = true,
            MaxIterations = 10_000,
            // Right half excluded, but with an even-odd hole ring restoring (60..90, 30..70)
            ExclusionZones =
            [
                new List<(double X, double Y)> { (50, -10), (110, -10), (110, 110), (50, 110) },
                new List<(double X, double Y)> { (60, 30), (90, 30), (90, 70), (60, 70) }
            ]
        };

        var inserted = RunToCompletion(tin, options);

        // Nothing in the zone-minus-hole area…
        Assert.All(inserted, v => Assert.False(
            InZoneRing(v) && !InHoleRing(v),
            $"vertex ({v.X:F2},{v.Y:F2}) was inserted inside the exclusion zone (outside the hole)"));

        // …but the hole is refinable again.
        Assert.Contains(inserted, InHoleRing);

        static bool InZoneRing(IVertex v) => v.X > 50.0 && v.X < 110.0 && v.Y > -10.0 && v.Y < 110.0;
        static bool InHoleRing(IVertex v) => v.X > 60.0 && v.X < 90.0 && v.Y > 30.0 && v.Y < 70.0;
    }

    [Fact]
    public void Refine_WithoutExclusionZones_RefinesBothHalves()
    {
        // Control: the same TIN without zones inserts vertices on both sides, proving the
        // zone tests above are constraining behaviour rather than observing an accident.
        var tin = CreateSparseConstrainedTin();
        var options = new RuppertOptions(25.0)
        {
            RefineOnlyInsideConstraints = true,
            MaxIterations = 10_000
        };

        var inserted = RunToCompletion(tin, options);

        Assert.Contains(inserted, v => v.X < 50.0);
        Assert.Contains(inserted, v => v.X > 50.0);
    }

    private static List<IVertex> RunToCompletion(IIncrementalTin tin, RuppertOptions options)
    {
        var refiner = new RuppertRefiner(tin, options);
        var inserted = new List<IVertex>();
        var seen = new HashSet<IVertex>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < 10_000; i++)
        {
            var v = refiner.RefineOnce();
            if (v == null)
                break;

            // RefineOnce may return a previously inserted vertex as a keep-going signal
            if (seen.Add(v))
                inserted.Add(v);
        }

        return inserted;
    }

    /// <summary>
    /// A 100×100 constrained square with deliberately skinny interior geometry on both
    /// halves (clustered points near the bottom edge), so both halves demand refinement.
    /// </summary>
    private static IIncrementalTin CreateSparseConstrainedTin(params (double X, double Y)[] extraPoints)
    {
        var tin = new IncrementalTin();

        var v1 = new Vertex(0, 0, 0, 0);
        var v2 = new Vertex(100, 0, 0, 1);
        var v3 = new Vertex(100, 100, 0, 2);
        var v4 = new Vertex(0, 100, 0, 3);
        tin.Add(v1);
        tin.Add(v2);
        tin.Add(v3);
        tin.Add(v4);

        // Clustered off-axis points create skinny triangles in both halves
        var index = 4;
        foreach (var (x, y) in new (double, double)[]
                 {
                     (20, 2), (25, 3), (30, 2.5), (70, 2), (75, 3), (80, 2.5),
                     (22, 60), (78, 60)
                 })
        {
            tin.Add(new Vertex(x, y, 0, index++));
        }

        foreach (var (x, y) in extraPoints)
        {
            tin.Add(new Vertex(x, y, 0, index++));
        }

        var boundary = new List<IVertex> { v1, v2, v3, v4 };
        var constraint = new PolygonConstraint(boundary, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }
}
