/*
 * Copyright 2025 G.W. Lucas
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

namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for donut-style constraints (outer ring with inner hole).
/// </summary>
public class DonutConstraintTest
{
    private readonly ITestOutputHelper _output;

    public DonutConstraintTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void SimpleTriangle_CCW_ShouldFillInside()
    {
        // Create a TIN with just 4 corner vertices
        var tin = new IncrementalTin();
        var corners = new IVertex[]
        {
            new Vertex(0, 0, 0),
            new Vertex(10, 0, 0),
            new Vertex(10, 10, 0),
            new Vertex(0, 10, 0)
        };
        tin.Add(corners);

        // Create a CCW triangle constraint in the middle
        // Interior should be on LEFT of each edge
        var constraintVertices = new IVertex[]
        {
            new Vertex(3, 3, 0),
            new Vertex(7, 3, 0),
            new Vertex(5, 7, 0)
        };

        foreach (var v in constraintVertices)
            tin.Add(v);

        var constraint = new PolygonConstraint(constraintVertices);

        this._output.WriteLine($"Triangle constraint vertices (CCW):");
        for (var i = 0; i < constraintVertices.Length; i++)
        {
            var v = constraintVertices[i];
            var next = constraintVertices[(i + 1) % constraintVertices.Length];
            this._output.WriteLine($"  Edge {i}: ({v.X},{v.Y}) -> ({next.X},{next.Y})");
        }

        // Add constraint
        tin.AddConstraints(new[] { constraint }, true);

        var constraintIndex = constraint.GetConstraintIndex();
        this._output.WriteLine($"\nConstraint index: {constraintIndex}");

        // Count triangles by region
        var allEdges = tin.GetEdges().Where(e => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where(e => e.IsConstraintRegionBorder()).ToList();
        var interiorEdges = allEdges.Where(e => e.IsConstraintRegionInterior()).ToList();

        this._output.WriteLine($"\nBorder edges: {borderEdges.Count}");
        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1}), BorderIdx={edge.GetConstraintBorderIndex()}");
        }

        this._output.WriteLine($"\nInterior edges: {interiorEdges.Count}");
        foreach (var edge in interiorEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            // Check if this edge is INSIDE the triangle constraint
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            var isInsideTriangle = IsPointInTriangle(midX, midY, constraintVertices);
            var status = isInsideTriangle ? "INSIDE" : "OUTSIDE";
            this._output.WriteLine($"  ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1}), InteriorIdx={edge.GetConstraintRegionInteriorIndex()}, {status}");
        }

        // Test: All interior edges should be INSIDE the triangle, not OUTSIDE
        var outsideInteriorCount = interiorEdges.Count(e =>
        {
            var a = e.GetA();
            var b = e.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            return !IsPointInTriangle(midX, midY, constraintVertices);
        });

        this._output.WriteLine($"\nInterior edges that are OUTSIDE the constraint: {outsideInteriorCount}");

        // Also check what triangles exist in the TIN
        this._output.WriteLine($"\nAll triangles in TIN:");
        foreach (var edge in allEdges.Take(20))
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var c = edge.GetForward().GetB();
            if (!c.IsNullVertex())
            {
                this._output.WriteLine($"  Triangle: ({a.X:F1},{a.Y:F1}), ({b.X:F1},{b.Y:F1}), ({c.X:F1},{c.Y:F1})");
            }
        }

        Assert.Equal(0, outsideInteriorCount);
        Assert.True(borderEdges.Count >= 3, $"Should have at least 3 border edges, found {borderEdges.Count}");
    }

    private static bool IsPointInTriangle(double px, double py, IVertex[] triangle)
    {
        var v0 = triangle[0];
        var v1 = triangle[1];
        var v2 = triangle[2];

        var d1 = Sign(px, py, v0.X, v0.Y, v1.X, v1.Y);
        var d2 = Sign(px, py, v1.X, v1.Y, v2.X, v2.Y);
        var d3 = Sign(px, py, v2.X, v2.Y, v0.X, v0.Y);

        var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private static double Sign(double p1x, double p1y, double p2x, double p2y, double p3x, double p3y)
    {
        return (p1x - p3x) * (p2y - p3y) - (p2x - p3x) * (p1y - p3y);
    }
}
