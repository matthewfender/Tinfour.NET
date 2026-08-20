/*
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
using Tinfour.Core.Utils;

using Xunit;

/// <summary>
///     Regression tests for ReefMaster #528: refinement must work across ALL constraint
///     regions when a shoreline fragments into multiple constraint polygons.
///     Two defects are pinned:
///     <list type="bullet">
///         <item>RuppertRefiner cached only the FIRST non-hole polygon constraint for
///         geometric containment, rejecting Steiner points in regions 2..N as
///         "outside" (fixed by the multi-polygon cache).</item>
///         <item>The bad-triangle queue silently dropped entries whose edge pair was
///         recycled by flip cascades from insertions in ANOTHER region — the region
///         added second retained its skinny triangles unrefined (fixed by re-evaluating
///         the recycled pair's current occupant, plus bounded re-scans in Refine()).</item>
///     </list>
/// </summary>
public class RuppertMultiPolygonContainmentTests
{
    /// <summary>
    ///     Builds a TIN containing two disjoint constrained square regions, each seeded
    ///     with deliberately skinny triangles (a base row of points plus one high apex,
    ///     deep inside the region so the fix path is a containment-gated circumcenter or
    ///     offcenter insertion rather than an ungated encroachment midpoint split).
    /// </summary>
    private static IncrementalTin CreateTwoRegionTin(
        bool swapConstraintOrder, out PolygonConstraint regionA, out PolygonConstraint regionB)
    {
        var tin = new IncrementalTin(1.0);
        var vertices = new List<IVertex>();

        // Region A occupies x in [0,60], region B x in [100,160].
        foreach (var xBase in new[] { 0.0, 100.0 })
        {
            for (var i = 0; i < 5; i++)
            {
                vertices.Add(new Vertex(xBase + 20 + i * 6, 25.0, -5.0));
            }

            vertices.Add(new Vertex(xBase + 32, 55.0, -6.0));
        }

        tin.Add(vertices);

        regionA = new PolygonConstraint(SquareVertices(0, 0, 60, 60));
        regionB = new PolygonConstraint(SquareVertices(100, 0, 160, 60));

        tin.AddConstraints(
            swapConstraintOrder
                ? new IConstraint[] { regionB, regionA }
                : new IConstraint[] { regionA, regionB },
            true);
        return tin;
    }

    private static List<IVertex> SquareVertices(double minX, double minY, double maxX, double maxY) =>
        new()
        {
            new Vertex(minX, minY, 0),
            new Vertex(maxX, minY, 0),
            new Vertex(maxX, maxY, 0),
            new Vertex(minX, maxY, 0),
        };

    private static double MinAngleDeg(IVertex a, IVertex b, IVertex c)
    {
        static double Len(IVertex p, IVertex q) =>
            Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
        double la = Len(b, c), lb = Len(a, c), lc = Len(a, b);
        static double Angle(double opp, double s1, double s2) =>
            Math.Acos(Math.Clamp((s1 * s1 + s2 * s2 - opp * opp) / (2 * s1 * s2), -1, 1)) * 180 / Math.PI;
        return Math.Min(Angle(la, lb, lc), Math.Min(Angle(lb, la, lc), Angle(lc, la, lb)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Refine_TwoDisjointConstraintRegions_RefinesBothRegardlessOfOrder(bool swapConstraintOrder)
    {
        var tin = CreateTwoRegionTin(swapConstraintOrder, out _, out _);

        var options = new RuppertOptions(28.0)
        {
            MaxIterations = 2000,
            MinimumTriangleArea = 0.01,
            RefineOnlyInsideConstraints = true,
            AddBoundingBoxConstraint = false,
        };

        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        // Pre-fix, the region whose constraint was added SECOND kept its skinny
        // triangles: its queued bad-triangle entries were dropped when flip cascades
        // from the first region's insertions recycled their edge pairs.
        var syntheticInA = 0;
        var syntheticInB = 0;
        foreach (var v in tin.GetVertices())
        {
            if (!v.IsSynthetic())
            {
                continue;
            }

            if (v.X < 80)
            {
                syntheticInA++;
            }
            else
            {
                syntheticInB++;
            }
        }

        var badA = 0;
        var badB = 0;
        foreach (var t in tin.GetTriangles())
        {
            var va = t.GetVertexA();
            var vb = t.GetVertexB();
            var vc = t.GetVertexC();
            if (va == null || vb == null || vc == null) continue;
            var isInterior = t.GetEdgeA().IsConstraintRegionInterior() ||
                             t.GetEdgeB().IsConstraintRegionInterior() ||
                             t.GetEdgeC().IsConstraintRegionInterior();
            if (!isInterior) continue;
            if (MinAngleDeg(va, vb, vc) < 28.0)
            {
                if ((va.X + vb.X + vc.X) / 3 < 80) badA++; else badB++;
            }
        }

        Assert.True(syntheticInA >= 10, $"region A must be substantially refined (got {syntheticInA} Steiner points)");
        Assert.True(syntheticInB >= 10, $"region B must be substantially refined (got {syntheticInB} Steiner points)");
        Assert.True(badA <= 8, $"region A must converge (got {badA} remaining bad triangles)");
        Assert.True(badB <= 8, $"region B must converge (got {badB} remaining bad triangles; pre-fix this was 12 with near-zero insertions)");
    }

    [Fact]
    public void Refine_TwoDisjointConstraintRegions_NoLeaksAgainstEitherPolygon()
    {
        var tin = CreateTwoRegionTin(false, out var regionA, out var regionB);

        var options = new RuppertOptions(28.0)
        {
            MaxIterations = 2000,
            MinimumTriangleArea = 0.01,
            RefineOnlyInsideConstraints = true,
            AddBoundingBoxConstraint = false,
        };

        var refiner = new RuppertRefiner(tin, options);
        refiner.Refine();

        // Multi-constraint leak detection: a Steiner point is leaked only if it is
        // outside BOTH polygons. The single-constraint overload would falsely flag
        // every region-B point as leaked from region A.
        var report = ConstraintLeakDetector.Detect(tin, new IConstraint[] { regionA, regionB });
        Assert.Equal(0, report.LeakedCount);
        Assert.True(report.TotalSteinerPoints > 0, "refinement should have inserted Steiner points");
    }
}
