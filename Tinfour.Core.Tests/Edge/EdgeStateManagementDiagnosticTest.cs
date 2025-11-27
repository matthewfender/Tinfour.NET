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
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Diagnostic test to investigate and fix the edge state management issue
///     in constraint processing for complex TINs.
///     Key finding: The issue is NOT related to constraint size, but rather
///     to TIN complexity. The same constraint works fine in simple TINs but
///     fails when the TIN reaches sufficient complexity/density.
/// </summary>
public class EdgeStateManagementDiagnosticTest
{
    private readonly ITestOutputHelper _output;

    public EdgeStateManagementDiagnosticTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void AnalyzeSegmentProcessingPattern_DetailedBreakdown()
    {
        // Create a TIN that should trigger the edge state issue and analyze exactly 
        // which segments succeed vs fail
        var tin = new IncrementalTin();

        // Create 12x12 grid - should be complex enough to trigger issue
        var vertices = new List<IVertex>();
        for (var i = 0; i < 12; i++)
        for (var j = 0; j < 12; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        this._output.WriteLine($"Created 12x12 TIN with {vertices.Count} vertices");

        // Create octagon constraint (8 segments) - good test case
        var centerX = 6.0;
        var centerY = 6.0;
        var radius = 2.0;
        var octVertices = new List<IVertex>();

        for (var i = 0; i < 8; i++)
        {
            var angle = 2.0 * Math.PI * i / 8;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            octVertices.Add(new Vertex(x, y, 0));
        }

        this._output.WriteLine($"\nOctagon vertices ({octVertices.Count}):");
        for (var i = 0; i < octVertices.Count; i++)
        {
            var v = octVertices[i];
            this._output.WriteLine(
                $"  Segment {i}: ({v.X:F2}, {v.Y:F2}) -> ({octVertices[(i + 1) % octVertices.Count].X:F2}, {octVertices[(i + 1) % octVertices.Count].Y:F2})");
        }

        var constraint = new PolygonConstraint(octVertices);

        // Process constraint
        var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

        if (exception != null) this._output.WriteLine($"\nException: {exception.Message}");

        // Analyze which segments were successfully processed
        var constraintIndex = constraint.GetConstraintIndex();
        var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where((IQuadEdge e) =>
            e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

        this._output.WriteLine("\n=== SEGMENT ANALYSIS ===");
        this._output.WriteLine($"Constraint index: {constraintIndex}");
        this._output.WriteLine($"Total border edges marked: {borderEdges.Count} (expected: 8)");
        this._output.WriteLine($"Success rate: {borderEdges.Count * 100.0 / 8:F1}%");

        // Try to map border edges back to original segments
        this._output.WriteLine("\nBorder edges found:");
        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  ({a.X:F2},{a.Y:F2}) -> ({b.X:F2},{b.Y:F2})");
        }

        // Based on the pattern we've observed, we expect only the first 2-3 segments 
        // to be processed successfully
        Assert.True(borderEdges.Count >= 2, $"Should have at least 2 segments processed, found {borderEdges.Count}");

        // This assertion should fail, demonstrating the issue
        if (exception == null)
            Assert.True(
                borderEdges.Count >= 8,
                $"All 8 octagon segments should be marked as border edges, found {borderEdges.Count}");
    }

    [Fact]
    public void CompareSimpleVsComplexTIN_SameConstraint()
    {
        // Direct comparison: same constraint on simple vs complex TIN
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(3, 3, 0), new Vertex(5, 3, 0), new Vertex(5, 5, 0),
                                         new Vertex(3, 5, 0)
                                     };

        this._output.WriteLine("Testing square constraint on simple vs complex TIN...\n");
        {
            // Test 1: Simple TIN (7x7 = 49 vertices)
            var tin = new IncrementalTin();
            var vertices = new List<IVertex>();
            for (var i = 0; i < 7; i++)
            for (var j = 0; j < 7; j++)
                vertices.Add(new Vertex(i, j, 0));

            tin.Add(vertices);

            var constraint = new PolygonConstraint(constraintVertices);
            tin.AddConstraints(new[] { constraint }, true);

            var borderEdges = tin.GetEdges().Where((IQuadEdge e) =>
                !e.GetB().IsNullVertex() && e.IsConstraintRegionBorder()
                                         && e.GetConstraintBorderIndex() == constraint.GetConstraintIndex()).ToList();

            this._output.WriteLine($"Simple TIN (7x7, {vertices.Count} vertices):");
            this._output.WriteLine($"  Border edges marked: {borderEdges.Count}/4");
            this._output.WriteLine($"  Success: {borderEdges.Count >= 4}");
        }
        {
            // Test 2: Complex TIN (20x20 = 400 vertices)
            var tin = new IncrementalTin();
            var vertices = new List<IVertex>();
            for (var i = 0; i < 20; i++)
            for (var j = 0; j < 20; j++)
                vertices.Add(new Vertex(i, j, 0));

            tin.Add(vertices);

            var constraint = new PolygonConstraint(constraintVertices);

            var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

            var borderEdges = new List<IQuadEdge>();
            if (exception == null)
                borderEdges = tin.GetEdges().Where((IQuadEdge e) =>
                        !e.GetB().IsNullVertex() && e.IsConstraintRegionBorder()
                                                 && e.GetConstraintBorderIndex() == constraint.GetConstraintIndex())
                    .ToList();

            this._output.WriteLine($"\nComplex TIN (20x20, {vertices.Count} vertices):");
            this._output.WriteLine($"  Exception: {exception?.GetType().Name ?? "None"}");
            this._output.WriteLine($"  Border edges marked: {borderEdges.Count}/4");
            this._output.WriteLine($"  Success: {borderEdges.Count >= 4}");

            if (exception != null) this._output.WriteLine($"  Exception message: {exception.Message}");
        }
    }

    [Fact]
    public void DiagnoseEdgeStateCorruption_WithDetailedLogging()
    {
        // Create a moderately complex TIN that should trigger the edge state issue
        var tin = new IncrementalTin();

        // 15x15 grid should be complex enough to trigger the issue
        var vertices = new List<IVertex>();
        for (var i = 0; i < 15; i++)
        for (var j = 0; j < 15; j++)
            vertices.Add(new Vertex(i, j, 0));

        tin.Add(vertices);

        this._output.WriteLine($"Created 15x15 TIN with {vertices.Count} vertices");

        // Create hexagon constraint (6 segments) - should trigger the issue
        var centerX = 7.0;
        var centerY = 7.0;
        var radius = 2.5;
        var hexVertices = new List<IVertex>();

        for (var i = 0; i < 6; i++)
        {
            var angle = 2.0 * Math.PI * i / 6;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            hexVertices.Add(new Vertex(x, y, 0));
        }

        this._output.WriteLine($"Hexagon vertices ({hexVertices.Count}):");
        for (var i = 0; i < hexVertices.Count; i++)
        {
            var v = hexVertices[i];
            this._output.WriteLine($"  {i}: ({v.X:F2}, {v.Y:F2})");
        }

        var constraint = new PolygonConstraint(hexVertices);

        // Add constraint and capture any exception details
        Exception? caughtException = null;
        try
        {
            tin.AddConstraints(new[] { constraint }, true);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            this._output.WriteLine($"Exception during constraint processing: {ex.GetType().Name}");
            this._output.WriteLine($"Message: {ex.Message}");
            this._output.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        // Analyze results regardless of exception
        var constraintIndex = constraint.GetConstraintIndex();
        var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

        var borderEdges = allEdges.Where((IQuadEdge e) =>
            e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

        this._output.WriteLine("\n=== CONSTRAINT PROCESSING RESULTS ===");
        this._output.WriteLine($"Constraint index: {constraintIndex}");
        this._output.WriteLine($"Border edges marked: {borderEdges.Count} (expected: 6)");
        this._output.WriteLine($"Exception occurred: {caughtException != null}");

        // List all border edges found
        this._output.WriteLine("\nBorder edges found:");
        foreach (var edge in borderEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            this._output.WriteLine($"  ({a.X:F2},{a.Y:F2}) -> ({b.X:F2},{b.Y:F2}), Index: {edge.GetIndex()}");
        }

        // Key assertion: We should get at least some border edges marked, 
        // but based on the pattern we've observed, we expect only 2-3
        if (caughtException == null)
        {
            Assert.True(
                borderEdges.Count >= 2,
                $"Should have at least 2 border edges marked, found {borderEdges.Count}");

            // This will likely fail, demonstrating the issue
            Assert.True(
                borderEdges.Count >= 6,
                $"Should have all 6 border edges marked for hexagon, found {borderEdges.Count}");
        }
    }

    [Fact]
    public void DiagnoseTINComplexityThreshold_SameConstraintDifferentDensities()
    {
        // Test the same constraint on TINs of increasing complexity
        // to identify exactly when the edge state management breaks
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(5, 5, 0), // Pentagon constraint
                                         new Vertex(7, 5, 0), new Vertex(8, 7, 0), new Vertex(6, 9, 0),
                                         new Vertex(4, 7, 0)
                                     };

        var constraint = new PolygonConstraint(constraintVertices);

        // Test on progressively more complex TINs
        var gridSizes = new[] { 7, 10, 15, 20, 25 }; // 49, 100, 225, 400, 625 vertices

        foreach (var gridSize in gridSizes)
        {
            this._output.WriteLine($"\n=== Testing {gridSize}x{gridSize} grid ({gridSize * gridSize} vertices) ===");

            var tin = new IncrementalTin();

            // Create grid of specified density
            var vertices = new List<IVertex>();
            for (var i = 0; i < gridSize; i++)
            for (var j = 0; j < gridSize; j++)
                vertices.Add(new Vertex(i, j, 0));

            tin.Add(vertices);

            var triangleCountBefore = tin.CountTriangles();
            var edgeCountBefore = tin.GetEdges().Count;

            this._output.WriteLine(
                $"TIN stats: {vertices.Count} vertices, {triangleCountBefore.ValidTriangles} triangles, {edgeCountBefore} edges");

            // Apply the same constraint
            var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, true));

            if (exception != null)
            {
                this._output.WriteLine($"EXCEPTION at grid size {gridSize}: {exception.Message}");
                continue;
            }

            // Analyze constraint edge marking results
            var constraintIndex = constraint.GetConstraintIndex();
            var allEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();

            var borderEdges = allEdges.Where((IQuadEdge e) =>
                e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == constraintIndex).ToList();

            this._output.WriteLine($"Constraint index: {constraintIndex}");
            this._output.WriteLine($"Border edges marked: {borderEdges.Count} (expected: 5)");
            this._output.WriteLine($"Success rate: {borderEdges.Count}/5 ({borderEdges.Count * 100.0 / 5:F1}%)");

            // List the border edges found
            foreach (var edge in borderEdges)
            {
                var a = edge.GetA();
                var b = edge.GetB();
                this._output.WriteLine($"  Border: ({a.X:F1},{a.Y:F1}) -> ({b.X:F1},{b.Y:F1})");
            }

            // Reset constraint index for next iteration
            constraint.SetConstraintIndex(null, -1);
        }
    }
}