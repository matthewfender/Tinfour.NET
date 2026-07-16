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

namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;
using Xunit;

/// <summary>
///     Regression tests for the point-location walk's exterior-classification contract
///     (ReefMaster issue #977): a walk must never report an interior target as exterior,
///     and an exterior report must name a hull edge whose sector genuinely contains the
///     target. Violations previously caused vertex insertions into far-away ghost
///     triangles, stitching giant triangles across the TIN interior (double-covered
///     surface, crossing contours).
/// </summary>
public class StochasticLawsonsWalkRobustnessTests
{
    private const int GridSize = 20;

    /// <summary>
    ///     Builds a deterministic jittered-grid TIN spanning [0, GridSize-1]^2.
    /// </summary>
    private static IncrementalTin CreateTin()
    {
        var tin = new IncrementalTin(1.0);
        var vertices = new List<IVertex>();
        var seed = 12345L;
        var index = 0;
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                // XORShift jitter, deterministic across runs.
                seed ^= seed << 21;
                seed ^= (long)((ulong)seed >> 35);
                seed ^= seed << 4;
                var jx = (seed & 0xFFFF) / (double)0xFFFF * 0.4 - 0.2;
                var jy = ((seed >> 16) & 0xFFFF) / (double)0xFFFF * 0.4 - 0.2;
                vertices.Add(new Vertex(col + jx, row + jy, Math.Sin(col * 0.5) + Math.Cos(row * 0.5), index++));
            }
        }

        Assert.True(tin.Add(vertices));
        return tin;
    }

    private static bool TriangleContains(IQuadEdge edge, double x, double y)
    {
        var a = edge.GetA();
        var b = edge.GetB();
        var c = edge.GetForward().GetB();
        if (a == null || b == null || c == null || c.IsNullVertex()) return false;

        const double eps = 1e-9;
        return Cross(a.X, a.Y, b.X, b.Y, x, y) >= -eps &&
               Cross(b.X, b.Y, c.X, c.Y, x, y) >= -eps &&
               Cross(c.X, c.Y, a.X, a.Y, x, y) >= -eps;
    }

    private static double Cross(double aX, double aY, double bX, double bY, double x, double y)
    {
        return ((bX - aX) * (y - aY)) - ((bY - aY) * (x - aX));
    }

    [Fact]
    public void Walk_InteriorTarget_FindsContainingTriangleFromEveryStartEdge()
    {
        var tin = CreateTin();
        var walker = new StochasticLawsonsWalk(1.0);
        var targets = new (double X, double Y)[]
        {
            (GridSize / 2.0, GridSize / 2.0),
            (1.3, 1.7),
            (GridSize - 2.2, 1.4),
            (1.6, GridSize - 2.3),
            (GridSize - 1.8, GridSize - 1.9)
        };

        foreach (var (x, y) in targets)
        {
            foreach (var edge in tin.GetEdgeIterator())
            {
                foreach (var start in new[] { edge, edge.GetDual() })
                {
                    var result = walker.FindAnEdgeFromEnclosingTriangle(start, x, y);
                    var apex = result.GetForward().GetB();
                    Assert.False(
                        apex == null || apex.IsNullVertex(),
                        $"walk from edge {start.GetIndex()} classified interior point ({x},{y}) as exterior");
                    Assert.True(
                        TriangleContains(result, x, y),
                        $"walk from edge {start.GetIndex()} returned a triangle not containing ({x},{y})");
                }
            }
        }
    }

    [Fact]
    public void Walk_ExteriorTarget_ReturnsSubtendingHullEdge()
    {
        var tin = CreateTin();
        var walker = new StochasticLawsonsWalk(1.0);

        // Points outside the hull: beyond each side and beyond a corner.
        var targets = new (double X, double Y)[]
        {
            (GridSize / 2.0, -3.0),
            (GridSize / 2.0, GridSize + 2.0),
            (-3.0, GridSize / 2.0),
            (GridSize + 2.0, GridSize / 2.0),
            (GridSize + 2.0, GridSize + 2.0)
        };

        // Spoke (real->ghost) starts are excluded: a walk started on a spoke returns the
        // spoke itself (pre-existing behaviour routed to the caller's hull-extension
        // handling); the exterior-sector contract applies to walks from real edges.
        var starts = tin.GetEdgeIterator()
            .Where(e => e.GetB() != null && !e.GetB().IsNullVertex())
            .Take(64)
            .ToList();
        foreach (var (x, y) in targets)
        {
            foreach (var start in starts)
            {
                var result = walker.FindAnEdgeFromEnclosingTriangle(start, x, y);
                var apex = result.GetForward().GetB();
                Assert.True(
                    apex == null || apex.IsNullVertex(),
                    $"walk from edge {start.GetIndex()} failed to classify exterior point ({x},{y}) as exterior; " +
                    $"result {result.GetIndex()} A=({result.GetA()?.X:F2},{result.GetA()?.Y:F2}) " +
                    $"B=({result.GetB()?.X:F2},{result.GetB()?.Y:F2}) apex=({apex?.X:F2},{apex?.Y:F2})");

                // The reported hull edge's sector must contain the target: the target lies
                // on the ghost side, and either projects onto the edge span (with the
                // walk's documented tolerance of half an edge length beyond each endpoint)
                // or sits in the corner wedge just beyond one of the edge's endpoints.
                var a = result.GetA();
                var b = result.GetB();
                var solidApex = result.GetDual().GetForward().GetB();
                var hTarget = Cross(a.X, a.Y, b.X, b.Y, x, y);
                var hApex = Cross(a.X, a.Y, b.X, b.Y, solidApex.X, solidApex.Y);
                Assert.True(
                    hTarget == 0 || hTarget > 0 != hApex > 0,
                    $"exterior point ({x},{y}) is on the solid side of the reported hull edge");

                var tX = b.X - a.X;
                var tY = b.Y - a.Y;
                var len2 = (tX * tX) + (tY * tY);
                var dot = ((x - a.X) * tX) + ((y - a.Y) * tY);
                var subtends = dot >= -0.5 * len2 && dot <= 1.5 * len2;
                var nearerEndpoint = dot > len2 ? b : a;
                var wedge = !subtends &&
                            Math.Sqrt(
                                ((x - nearerEndpoint.X) * (x - nearerEndpoint.X)) +
                                ((y - nearerEndpoint.Y) * (y - nearerEndpoint.Y))) < GridSize;
                Assert.True(
                    subtends || wedge,
                    $"reported hull edge does not subtend exterior point ({x},{y}) and is not a corner wedge");
            }
        }
    }

    [Fact]
    public void Walk_FromDeallocatedStartHandle_RecoversAndLocatesInteriorTarget()
    {
        var tin = CreateTin();

        // Deallocate a pair to obtain a freed handle, then walk from it. A caller-cached
        // search edge can be freed by later TIN mutations; the walk must restart from
        // allocated storage instead of wandering NaN coordinates.
        var pool = tin.GetEdgePoolInternal();
        var store = pool.Store;
        var victim = pool.AllocateEdgeHandle(
            new Vertex(0.1, 0.1, 0, 90001), new Vertex(0.2, 0.2, 0, 90002));
        pool.DeallocateEdgeHandle(victim);
        Assert.False(store.IsAllocated(victim));

        var walker = new StochasticLawsonsWalk(1.0);
        var x = GridSize / 2.0;
        var y = GridSize / 2.0;
        var result = store.Wrap(walker.FindAnEdgeFromEnclosingTriangle(store, victim, x, y));

        var apex = result.GetForward().GetB();
        Assert.False(apex == null || apex.IsNullVertex(), "walk from freed handle classified interior point as exterior");
        Assert.True(TriangleContains(result, x, y), "walk from freed handle returned a non-containing triangle");
    }
}
