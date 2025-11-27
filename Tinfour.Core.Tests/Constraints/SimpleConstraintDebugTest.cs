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
///     Specific test to debug the constraint processing with detailed output
///     to understand exactly where and why segment processing fails.
/// </summary>
public class SimpleConstraintDebugTest
{
    private readonly ITestOutputHelper _output;

    public SimpleConstraintDebugTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void CompareTINDensities_SameConstraint()
    {
        // Test the same triangle constraint on different TIN densities to find the threshold
        var constraintVertices = new IVertex[] { new Vertex(3, 3, 0), new Vertex(6, 3, 0), new Vertex(4.5, 6, 0) };

        var gridSizes = new[] { 8, 10, 12, 15 };

        foreach (var gridSize in gridSizes)
        {
            this._output.WriteLine($"\n=== Testing triangle on {gridSize}x{gridSize} grid ===");

            var tin = new IncrementalTin();
            var vertices = new List<IVertex>();
            for (var i = 0; i < gridSize; i++)
            for (var j = 0; j < gridSize; j++)
                vertices.Add(new Vertex(i, j, 0));

            tin.Add(vertices);

            var constraint = new PolygonConstraint(constraintVertices);
            var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

            if (exception != null)
            {
                this._output.WriteLine($"Exception at grid size {gridSize}: {exception.Message}");
                continue;
            }

            var constraintIndex = constraint.GetConstraintIndex();
            var borderEdges = tin.GetEdges().Where((IQuadEdge e) =>
                !e.GetB().IsNullVertex() && e.IsConstraintRegionBorder()
                                         && e.GetConstraintBorderIndex() == constraintIndex).ToList();

            this._output.WriteLine(
                $"Grid {gridSize}x{gridSize} ({vertices.Count} vertices): {borderEdges.Count}/3 segments processed");

            // Reset constraint for next iteration
            constraint.SetConstraintIndex(null, -1);
        }
    }

    [Fact]
    public void DebugComplexPentagonConstraint_StepByStep()
    {
        // Now test a more complex case that should fail
        var tin = new IncrementalTin();

        // Create a 12x12 grid to trigger the issue
        var vertices = new List<IVertex>();
        for (var i = 0; i < 12; i++)
        for (var j = 0; j < 12; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        this._output.WriteLine($"Created 12x12 TIN with {vertices.Count} vertices");

        // Create pentagon constraint (5 segments)
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(6, 6, 0), // Start at center-ish
                                         new Vertex(8, 6, 0), // Segment 0: East
                                         new Vertex(9, 8, 0), // Segment 1: Northeast
                                         new Vertex(7, 9, 0), // Segment 2: Northwest
                                         new Vertex(5, 8, 0), // Segment 3: Southwest
                                         new Vertex(5, 6, 0) // Segment 4: South (back to near start)
                                     };

        this._output.WriteLine("Pentagon constraint vertices:");
        for (var i = 0; i < constraintVertices.Length - 1; i++)
        {
            var v = constraintVertices[i];
            var next = constraintVertices[i + 1];
            this._output.WriteLine($"  Segment {i}: ({v.X:F1}, {v.Y:F1}) -> ({next.X:F1}, {next.Y:F1})");
        }

        var constraint = new PolygonConstraint(constraintVertices);

        // Process constraint and analyze results
        var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

        if (exception != null) this._output.WriteLine($"Exception: {exception.Message}");

        var constraintIndex = constraint.GetConstraintIndex();
        var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where((IQuadEdge e) =>
            e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

        this._output.WriteLine("\n=== COMPLEX CONSTRAINT RESULTS ===");
        this._output.WriteLine($"Constraint index: {constraintIndex}");
        this._output.WriteLine($"Border edges marked: {borderEdges.Count} (expected: 5)");

        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  Border: ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1})");
        }

        // Map border edges to original segments
        this._output.WriteLine("\nSegment analysis:");
        for (var i = 0; i < constraintVertices.Length - 1; i++)
        {
            var v0 = constraintVertices[i];
            var v1 = constraintVertices[i + 1];

            var matchingEdges = borderEdges.Where((IQuadEdge e) =>
                (this.IsNear(e.GetA(), v0) && this.IsNear(e.GetB(), v1))
                || (this.IsNear(e.GetA(), v1) && this.IsNear(e.GetB(), v0))).ToList();

            this._output.WriteLine(
                $"  Segment {i} ({v0.X:F1},{v0.Y:F1})->({v1.X:F1},{v1.Y:F1}): {matchingEdges.Count} edge(s) found");
        }

        // This will likely fail, showing which segments are not processed
        Assert.True(borderEdges.Count >= 3, $"Should have at least 3 segments processed, found {borderEdges.Count}");
    }

    [Fact]
    public void DebugSimpleSquareConstraint_StepByStep()
    {
        // Start with a simple case that we know works
        var tin = new IncrementalTin();

        // Create a simple 7x7 grid
        var vertices = new List<IVertex>();
        for (var i = 0; i < 7; i++)
        for (var j = 0; j < 7; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        this._output.WriteLine($"Created 7x7 TIN with {vertices.Count} vertices");

        // Create simple square constraint (4 segments)
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(2, 2, 0), new Vertex(4, 2, 0), new Vertex(4, 4, 0),
                                         new Vertex(2, 4, 0)
                                     };

        this._output.WriteLine("Square constraint vertices:");
        for (var i = 0; i < constraintVertices.Length; i++)
        {
            var v = constraintVertices[i];
            var next = constraintVertices[(i + 1) % constraintVertices.Length];
            this._output.WriteLine($"  Segment {i}: ({v.X:F1}, {v.Y:F1}) -> ({next.X:F1}, {next.Y:F1})");
        }

        var constraint = new PolygonConstraint(constraintVertices);

        // Process constraint and analyze results
        tin.AddConstraints(new[] { constraint }, true);

        var constraintIndex = constraint.GetConstraintIndex();
        var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where((IQuadEdge e) =>
            e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

        this._output.WriteLine("\n=== SIMPLE CONSTRAINT RESULTS ===");
        this._output.WriteLine($"Constraint index: {constraintIndex}");
        this._output.WriteLine($"Border edges marked: {borderEdges.Count} (expected: 4)");

        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  Border: ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1})");
        }

        Assert.True(borderEdges.Count >= 4, $"Simple square should work, found {borderEdges.Count} border edges");
    }

    [Fact]
    public void DebugSimpleTriangleConstraint_Baseline()
    {
        // Test the simplest possible polygon constraint
        var tin = new IncrementalTin();

        // Create a moderate 10x10 grid
        var vertices = new List<IVertex>();
        for (var i = 0; i < 10; i++)
        for (var j = 0; j < 10; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        this._output.WriteLine($"Created 10x10 TIN with {vertices.Count} vertices");

        // Create triangle constraint (3 segments)
        var constraintVertices = new IVertex[] { new Vertex(3, 3, 0), new Vertex(6, 3, 0), new Vertex(4.5, 6, 0) };

        this._output.WriteLine("Triangle constraint vertices:");
        for (var i = 0; i < constraintVertices.Length; i++)
        {
            var v = constraintVertices[i];
            var next = constraintVertices[(i + 1) % constraintVertices.Length];
            this._output.WriteLine($"  Segment {i}: ({v.X:F1}, {v.Y:F1}) -> ({next.X:F1}, {next.Y:F1})");
        }

        var constraint = new PolygonConstraint(constraintVertices);

        var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

        if (exception != null) this._output.WriteLine($"Exception: {exception.Message}");

        var constraintIndex = constraint.GetConstraintIndex();
        var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where((IQuadEdge e) =>
            e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

        this._output.WriteLine("\n=== TRIANGLE CONSTRAINT RESULTS ===");
        this._output.WriteLine($"Constraint index: {constraintIndex}");
        this._output.WriteLine($"Border edges marked: {borderEdges.Count} (expected: 3)");

        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  Border: ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1})");
        }

        if (exception == null)
            Assert.True(
                borderEdges.Count >= 3,
                $"Triangle should have all 3 segments processed, found {borderEdges.Count}");
    }

    private bool IsNear(IVertex a, IVertex b, double tolerance = 0.1)
    {
        return Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
    }
}