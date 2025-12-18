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

namespace Tinfour.Core.Tests.Edge;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests to diagnose constraint bit manipulation issues.
/// </summary>
public class ConstraintBitManipulationTest
{
    private readonly ITestOutputHelper _output;

    public ConstraintBitManipulationTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void SetAndGetConstraintBorderIndex_ShouldRoundTrip()
    {
        // Create a QuadEdge
        var edge = new QuadEdge(0);
        var partner = (QuadEdgePartner)edge.GetDual();

        // Test various constraint indices (border index now uses lower 15 bits, max 32766)
        var testIndices = new[] { 0, 1, 5, 100, 1000, 10000, 32766 };

        foreach (var expectedIndex in testIndices)
        {
            // Set the constraint border index
            partner.SetConstraintBorderIndex(expectedIndex);

            // Verify the border flag is set
            Assert.True(partner.IsConstraintRegionBorder(),
                $"Border flag should be set for index {expectedIndex}");

            // Get the constraint border index
            var actualIndex = partner.GetConstraintBorderIndex();

            this._output.WriteLine($"Set {expectedIndex}, Got {actualIndex}");

            Assert.Equal(expectedIndex, actualIndex);
        }
    }

    [Fact]
    public void EdgeInTin_SetAndGetConstraintBorderIndex()
    {
        var tin = new IncrementalTin();

        // Create a simple TIN
        var vertices = new IVertex[]
        {
            new Vertex(0, 0, 0),
            new Vertex(10, 0, 0),
            new Vertex(5, 10, 0)
        };

        tin.Add(vertices);

        // Get an edge
        var edges = tin.GetEdges();
        Assert.True(edges.Count > 0, "Should have edges");

        var edge = edges[0];

        // Set constraint border index
        edge.SetConstraintBorderIndex(0);

        this._output.WriteLine($"Edge type: {edge.GetType().Name}");
        this._output.WriteLine($"Dual type: {edge.GetDual().GetType().Name}");
        this._output.WriteLine($"IsConstraintRegionBorder: {edge.IsConstraintRegionBorder()}");
        this._output.WriteLine($"GetConstraintBorderIndex: {edge.GetConstraintBorderIndex()}");

        Assert.True(edge.IsConstraintRegionBorder(), "Border flag should be set");
        Assert.Equal(0, edge.GetConstraintBorderIndex());
    }

    [Fact]
    public void FullConstraintWorkflow_ShouldSetBorderIndex()
    {
        var tin = new IncrementalTin();

        // Create a 5x5 grid
        var vertices = new List<IVertex>();
        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 5; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        // Create a simple square constraint
        var constraintVertices = new IVertex[]
        {
            new Vertex(1, 1, 0),
            new Vertex(3, 1, 0),
            new Vertex(3, 3, 0),
            new Vertex(1, 3, 0)
        };

        var constraint = new PolygonConstraint(constraintVertices);

        // Add the constraint
        tin.AddConstraints(new[] { constraint }, true);

        var constraintIndex = constraint.GetConstraintIndex();
        this._output.WriteLine($"Constraint index: {constraintIndex}");

        // Find border edges
        var borderEdges = tin.GetEdges()
            .Where(e => !e.GetB().IsNullVertex() && e.IsConstraintRegionBorder())
            .ToList();

        this._output.WriteLine($"Border edges found: {borderEdges.Count}");

        foreach (var edge in borderEdges)
        {
            var borderIdx = edge.GetConstraintBorderIndex();
            this._output.WriteLine($"Edge {edge.GetIndex()}: BorderIdx={borderIdx}, " +
                                   $"A=({edge.GetA().X},{edge.GetA().Y}), B=({edge.GetB().X},{edge.GetB().Y})");

            // The border index should match the constraint index
            Assert.Equal(constraintIndex, borderIdx);
        }

        // We should have border edges
        Assert.True(borderEdges.Count > 0, "Should have border edges");
    }
}
