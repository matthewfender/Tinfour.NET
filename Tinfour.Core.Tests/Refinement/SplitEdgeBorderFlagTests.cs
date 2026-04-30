/*
 * Copyright 2025 M. Fender
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
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests that verify SplitEdge correctly preserves ConstraintRegionBorder
///     flags on both halves when splitting a perimeter constraint edge.
///     This is the FIX-01 verification test for ghost-side SplitEdge border flag preservation.
/// </summary>
public class SplitEdgeBorderFlagTests
{
    private readonly ITestOutputHelper _output;

    public SplitEdgeBorderFlagTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Splits a perimeter constraint edge at the midpoint and verifies that
    ///     both resulting halves retain the ConstraintRegionBorder flag and the
    ///     same border index as the original edge. Also verifies ghost triangle
    ///     forward-link chains close properly around the new split vertex.
    /// </summary>
    [Fact]
    public void SplitPerimeterConstraintEdge_PreservesBorderFlagsOnBothHalves()
    {
        // Setup: Create a small TIN with 4 corner vertices forming a square
        // plus one interior vertex to ensure a valid triangulation.
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(100, 0, 0.0, 1);
        var v2 = new Vertex(100, 100, 0.0, 2);
        var v3 = new Vertex(0, 100, 0.0, 3);
        var v4 = new Vertex(50, 50, 5.0, 4);

        tin.Add(new IVertex[] { v0, v1, v2, v3, v4 });

        // Add a polygon constraint using the 4 corner vertices.
        // This makes the perimeter edges into constraint borders.
        var rectVertices = new IVertex[] { v0, v1, v2, v3 };
        var constraint = new PolygonConstraint(rectVertices, definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Find a constraint border edge on the TIN perimeter (one side is ghost)
        var perimeter = tin.GetPerimeter();
        Assert.True(perimeter.Count > 0, "Perimeter should have edges");

        IQuadEdge? borderEdge = null;
        foreach (var edge in perimeter)
        {
            if (edge.IsConstraintRegionBorder())
            {
                borderEdge = edge;
                break;
            }
        }

        Assert.NotNull(borderEdge);
        _output.WriteLine($"Found border edge: A=({borderEdge.GetA().X}, {borderEdge.GetA().Y}) -> " +
                          $"B=({borderEdge.GetB().X}, {borderEdge.GetB().Y})");

        // Record original state
        var originalBorderIndex = borderEdge.GetConstraintBorderIndex();
        var originalA = borderEdge.GetA();
        var originalB = borderEdge.GetB();

        _output.WriteLine($"Original border index: {originalBorderIndex}");
        _output.WriteLine($"Original A: ({originalA.X}, {originalA.Y}), B: ({originalB.X}, {originalB.Y})");
        Assert.True(originalBorderIndex >= 0, "Border index should be non-negative");

        // Split the edge at the midpoint
        var splitVertex = tin.SplitEdge(borderEdge, 0.5, double.NaN);
        Assert.NotNull(splitVertex);
        _output.WriteLine($"Split vertex: ({splitVertex.X}, {splitVertex.Y})");

        // After split, re-walk perimeter to find both half-edges incident to the split vertex.
        // The input edge reference now points to m->b (EdgePool.SplitEdge calls e.SetA(m)).
        // The new a->m edge was allocated internally.
        var newPerimeter = tin.GetPerimeter();
        var halves = newPerimeter
            .Where(e => VertexEquals(e.GetA(), splitVertex) || VertexEquals(e.GetB(), splitVertex))
            .ToList();

        _output.WriteLine($"Half-edges incident to split vertex: {halves.Count}");
        foreach (var h in halves)
        {
            _output.WriteLine($"  Edge: A=({h.GetA().X}, {h.GetA().Y}) -> B=({h.GetB().X}, {h.GetB().Y}), " +
                              $"IsBorder={h.IsConstraintRegionBorder()}, BorderIdx={h.GetConstraintBorderIndex()}");
        }

        Assert.Equal(2, halves.Count);

        // Identify which half is a->m and which is m->b
        var amEdge = halves.FirstOrDefault(e =>
            VertexEquals(e.GetA(), originalA) && VertexEquals(e.GetB(), splitVertex));
        var mbEdge = halves.FirstOrDefault(e =>
            VertexEquals(e.GetA(), splitVertex) && VertexEquals(e.GetB(), originalB));

        Assert.NotNull(amEdge);
        Assert.NotNull(mbEdge);

        // Core assertions: both halves must be constraint region borders
        Assert.True(amEdge.IsConstraintRegionBorder(),
            "a->m edge should be ConstraintRegionBorder after split");
        Assert.True(mbEdge.IsConstraintRegionBorder(),
            "m->b edge should be ConstraintRegionBorder after split");

        // Both halves must carry the same border index as the original
        Assert.Equal(originalBorderIndex, amEdge.GetConstraintBorderIndex());
        Assert.Equal(originalBorderIndex, mbEdge.GetConstraintBorderIndex());

        // Verify ghost triangle forward-link chains close properly around the split vertex.
        // Each ghost triangle must satisfy: e -> e.Forward -> e.Forward.Forward -> e
        VerifyTriangleForwardChain(amEdge, "a->m side");
        VerifyTriangleForwardChain(mbEdge, "m->b side");

        // Also check the dual side (ghost triangles are on the dual)
        VerifyTriangleForwardChain(amEdge.GetDual(), "dual of a->m");
        VerifyTriangleForwardChain(mbEdge.GetDual(), "dual of m->b");

        _output.WriteLine("All border flag and topology assertions passed.");
    }

    /// <summary>
    ///     After splitting a perimeter constraint edge, verifies that connecting
    ///     edges from the split midpoint to interior-side opposite vertices carry
    ///     ConstraintRegionInterior flags if they are inside the constraint region.
    /// </summary>
    [Fact]
    public void SplitPerimeterConstraintEdge_InteriorFlagsPropagateToConnectingEdges()
    {
        // Same setup as above
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(100, 0, 0.0, 1);
        var v2 = new Vertex(100, 100, 0.0, 2);
        var v3 = new Vertex(0, 100, 0.0, 3);
        var v4 = new Vertex(50, 50, 5.0, 4);

        tin.Add(new IVertex[] { v0, v1, v2, v3, v4 });

        var rectVertices = new IVertex[] { v0, v1, v2, v3 };
        var constraint = new PolygonConstraint(rectVertices, definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Find a constraint border edge on the perimeter
        var perimeter = tin.GetPerimeter();
        IQuadEdge? borderEdge = null;
        foreach (var edge in perimeter)
        {
            if (edge.IsConstraintRegionBorder())
            {
                borderEdge = edge;
                break;
            }
        }

        Assert.NotNull(borderEdge);

        var originalA = borderEdge.GetA();
        var originalB = borderEdge.GetB();

        // Split the edge
        var splitVertex = tin.SplitEdge(borderEdge, 0.5, double.NaN);
        Assert.NotNull(splitVertex);

        // Find all edges incident to the split vertex (not just perimeter ones)
        var allEdges = tin.GetEdges();
        var incidentEdges = allEdges
            .Where(e => VertexEquals(e.GetA(), splitVertex) || VertexEquals(e.GetB(), splitVertex))
            .ToList();

        _output.WriteLine($"Total edges incident to split vertex: {incidentEdges.Count}");

        // Separate into perimeter edges (border) and interior edges
        var interiorConnectors = new List<IQuadEdge>();
        var perimeterEdges = new List<IQuadEdge>();

        foreach (var e in incidentEdges)
        {
            if (e.IsConstraintRegionBorder())
            {
                perimeterEdges.Add(e);
            }
            else
            {
                // These are connecting edges from split vertex to interior-side opposite vertices
                interiorConnectors.Add(e);
            }
        }

        _output.WriteLine($"Perimeter (border) edges: {perimeterEdges.Count}");
        _output.WriteLine($"Interior connecting edges: {interiorConnectors.Count}");

        // Log interior edge details
        foreach (var e in interiorConnectors)
        {
            var a = e.GetA();
            var b = e.GetB();
            var isInterior = e.IsConstraintRegionInterior();
            _output.WriteLine($"  Interior edge: A=({a.X}, {a.Y}) -> B=({b.X}, {b.Y}), " +
                              $"IsInterior={isInterior}");
        }

        // Check that interior-side connecting edges carry the interior flag.
        // When a perimeter border edge is split, new edges from the split vertex
        // toward interior-side vertices should be marked as interior.
        // Note: Not all edges incident to m will be interior -- ghost-side edges won't be.
        var hasInteriorConnectors = interiorConnectors.Any(e => e.IsConstraintRegionInterior());

        _output.WriteLine($"Has interior connecting edges: {hasInteriorConnectors}");

        // At minimum, the interior-side connecting edge(s) should exist and be flagged
        Assert.True(hasInteriorConnectors,
            "At least one connecting edge from split vertex to interior should have " +
            "ConstraintRegionInterior flag");

        _output.WriteLine("Interior flag propagation assertions passed.");
    }

    /// <summary>
    ///     Verifies that the forward-link chain starting from the given edge
    ///     closes back to itself in exactly 3 steps (forming a triangle).
    /// </summary>
    private void VerifyTriangleForwardChain(IQuadEdge startEdge, string label)
    {
        var e1 = startEdge.GetForward();
        var e2 = e1.GetForward();
        var e3 = e2.GetForward();

        // A well-formed triangle forward chain must close in 3 steps
        Assert.True(ReferenceEquals(startEdge, e3),
            $"Forward-link chain for '{label}' does not close in 3 steps. " +
            $"Start edge A=({startEdge.GetA().X}, {startEdge.GetA().Y}) -> " +
            $"B=({startEdge.GetB().X}, {startEdge.GetB().Y})");
    }

    /// <summary>
    ///     Compares two vertices by reference equality (preferred for TIN vertices)
    ///     or by coordinate proximity as fallback.
    /// </summary>
    private static bool VertexEquals(IVertex a, IVertex b)
    {
        if (ReferenceEquals(a, b)) return true;
        // Fallback: coordinate comparison for vertices that may have been re-created
        const double eps = 1e-10;
        return Math.Abs(a.X - b.X) < eps && Math.Abs(a.Y - b.Y) < eps;
    }
}
