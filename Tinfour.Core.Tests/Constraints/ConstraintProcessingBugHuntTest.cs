namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Focused investigation to trace exactly where and why edge 300-7 gets marked
///     as constrained during processing of constraint 300?301.
/// </summary>
public class ConstraintProcessingBugHuntTest
{
    private readonly ITestOutputHelper _output;

    public ConstraintProcessingBugHuntTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void IsolateConstraint300To301Only()
    {
        this._output.WriteLine("=== ISOLATE CONSTRAINT 300?301 ONLY ===");

        // Test ONLY the problematic constraint in isolation to see if it still produces the wrong result
        var tin = this.CreateBaseTin();

        // Add ONLY the constraint vertices for 300?301
        var vertex300 = new Vertex(740, 60, 0, 300);
        var vertex301 = new Vertex(60, 540, 0, 301);

        tin.Add(vertex300);
        tin.Add(vertex301);

        this._output.WriteLine(
            $"Added only constraint vertices: 300({vertex300.X:F0},{vertex300.Y:F0}) and 301({vertex301.X:F0},{vertex301.Y:F0})");

        // Check edge 300-7 before
        var edge300_7_before = this.FindEdge(tin, 300, 7);
        this._output.WriteLine(
            $"BEFORE constraint: Edge 300-7 exists: {edge300_7_before != null}, constrained: {edge300_7_before?.IsConstrained() ?? false}");

        // Process ONLY the 300?301 constraint
        var constraint = new LinearConstraint([vertex300, vertex301]);
        tin.AddConstraints([constraint], true);

        // Check edge 300-7 after
        var edge300_7_after = this.FindEdge(tin, 300, 7);
        this._output.WriteLine(
            $"AFTER constraint: Edge 300-7 exists: {edge300_7_after != null}, constrained: {edge300_7_after?.IsConstrained() ?? false}");

        if (edge300_7_after?.IsConstrained() == true)
        {
            this._output.WriteLine(
                "?? BUG CONFIRMED IN ISOLATION: Edge 300-7 incorrectly marked even with NO OTHER CONSTRAINTS");
            this._output.WriteLine(
                "This proves the bug is in the fundamental constraint processing, not constraint interaction");
        }
        else
        {
            this._output.WriteLine("? In isolation, edge 300-7 is NOT incorrectly marked");
            this._output.WriteLine("This suggests the bug IS related to multiple constraints");
        }

        // Show all constrained edges
        var constrainedEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex() && e.IsConstrained()).ToList();

        this._output.WriteLine($"\nAll constrained edges: {constrainedEdges.Count}");
        foreach (var edge in constrainedEdges)
            this._output.WriteLine(
                $"  Edge {edge.GetIndex()}: {edge.GetA().GetIndex()}-{edge.GetB().GetIndex()} Line:{edge.GetConstraintLineIndex()}");
    }

    [Fact]
    public void TraceExactConstraintProcessing()
    {
        this._output.WriteLine("=== TRACE EXACT CONSTRAINT PROCESSING BUG ===");

        // Create the exact problematic scenario
        var tin = this.CreateBaseTin();

        // Add first constraint (this works correctly)
        var constraint1 = new LinearConstraint(
            [
                new Vertex(60, 60, 0, 200), // 200?201
                new Vertex(740, 540, 0, 201)
            ]);

        foreach (var v in constraint1.GetVertices()) tin.Add(v);

        tin.AddConstraints([constraint1], true);
        this._output.WriteLine("Constraint 1 (200?201) processed successfully");

        // Capture edge state after first constraint
        var edgesAfterFirst = this.CaptureEdgeStates(tin);
        var constrainedAfterFirst = edgesAfterFirst.Where(((int edgeIndex, int vA, int vB, int lineIndex) e) => e.lineIndex >= 0).ToList();
        this._output.WriteLine($"After constraint 1: {constrainedAfterFirst.Count} constrained edges");
        foreach (var edge in constrainedAfterFirst)
            this._output.WriteLine($"  Edge {edge.edgeIndex}: {edge.vA}-{edge.vB} Line:{edge.lineIndex}");

        // Now add the second constraint vertices
        var vertex300 = new Vertex(740, 60, 0, 300); // The constraint start
        var vertex301 = new Vertex(60, 540, 0, 301); // The constraint end

        tin.Add(vertex300);
        tin.Add(vertex301);

        this._output.WriteLine(
            $"\nAdded constraint vertices: 300({vertex300.X:F0},{vertex300.Y:F0}) and 301({vertex301.X:F0},{vertex301.Y:F0})");

        // Check if edge 300-7 exists before constraint processing
        var edge300_7_before = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == 300 && e.GetB().GetIndex() == 7)
            || (e.GetA().GetIndex() == 7 && e.GetB().GetIndex() == 300));

        if (edge300_7_before != null)
            this._output.WriteLine(
                $"BEFORE constraint 2: Edge 300-7 exists (edge {edge300_7_before.GetIndex()}) and is constrained: {edge300_7_before.IsConstrained()}");
        else this._output.WriteLine("BEFORE constraint 2: Edge 300-7 does NOT exist");

        // Create and process the problematic constraint
        var constraint2 = new LinearConstraint([vertex300, vertex301]);
        this._output.WriteLine(
            $"\nProcessing constraint 2: {vertex300.GetIndex()}({vertex300.X:F0},{vertex300.Y:F0}) ? {vertex301.GetIndex()}({vertex301.X:F0},{vertex301.Y:F0})");

        // THIS IS WHERE THE BUG OCCURS
        tin.AddConstraints([constraint2], true);

        // Check edge 300-7 after constraint processing
        var edge300_7_after = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == 300 && e.GetB().GetIndex() == 7)
            || (e.GetA().GetIndex() == 7 && e.GetB().GetIndex() == 300));

        if (edge300_7_after != null)
        {
            this._output.WriteLine(
                $"AFTER constraint 2: Edge 300-7 (edge {edge300_7_after.GetIndex()}) is constrained: {edge300_7_after.IsConstrained()}, Line: {edge300_7_after.GetConstraintLineIndex()}");

            if (edge300_7_after.IsConstrained()
                && edge300_7_after.GetConstraintLineIndex() == constraint2.GetConstraintIndex())
            {
                this._output.WriteLine(
                    $"?? BUG CONFIRMED: Edge 300-7 has been INCORRECTLY marked with constraint index {constraint2.GetConstraintIndex()}");
                this._output.WriteLine("This edge has NOTHING to do with constraint 300?301!");
            }
        }

        // Show all constrained edges after constraint 2
        var edgesAfterSecond = this.CaptureEdgeStates(tin);
        var constrainedAfterSecond = edgesAfterSecond.Where(((int edgeIndex, int vA, int vB, int lineIndex) e) => e.lineIndex >= 0).ToList();
        this._output.WriteLine($"\nAfter constraint 2: {constrainedAfterSecond.Count} constrained edges");
        foreach (var edge in constrainedAfterSecond)
        {
            this._output.WriteLine($"  Edge {edge.edgeIndex}: {edge.vA}-{edge.vB} Line:{edge.lineIndex}");

            if ((edge.vA == 300 && edge.vB == 7) || (edge.vA == 7 && edge.vB == 300))
                this._output.WriteLine(
                    "    ^^^ THIS IS THE INCORRECT EDGE! Should not be constrained for constraint 300?301");
        }

        // Analyze what SHOULD have happened
        this._output.WriteLine("\n=== EXPECTED vs ACTUAL ===");
        this._output.WriteLine(
            "EXPECTED: Constraint 300?301 should create edges that form a path from vertex 300 to vertex 301");
        this._output.WriteLine(
            $"ACTUAL: Edge 300-7 gets marked, but vertex 7 is at ({this.GetVertexCoords(tin, 7)}) which is NOT on the path to vertex 301");

        // Check if there's a valid path from 300 to 301
        var edgesFrom300 = constrainedAfterSecond.Where(((int edgeIndex, int vA, int vB, int lineIndex) e) => e.vA == 300 || e.vB == 300).ToList();
        var edgesTo301 = constrainedAfterSecond.Where(((int edgeIndex, int vA, int vB, int lineIndex) e) => e.vA == 301 || e.vB == 301).ToList();

        this._output.WriteLine($"\nEdges involving vertex 300: {edgesFrom300.Count}");
        foreach (var edge in edgesFrom300)
        {
            var otherVertex = edge.vA == 300 ? edge.vB : edge.vA;
            this._output.WriteLine($"  300 ? {otherVertex}");
        }

        this._output.WriteLine($"\nEdges involving vertex 301: {edgesTo301.Count}");
        foreach (var edge in edgesTo301)
        {
            var otherVertex = edge.vA == 301 ? edge.vB : edge.vA;
            this._output.WriteLine($"  301 ? {otherVertex}");
        }
    }

    private List<(int edgeIndex, int vA, int vB, int lineIndex)> CaptureEdgeStates(IncrementalTin tin)
    {
        return tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).Select((IQuadEdge e) =>
            (e.GetIndex(), e.GetA().GetIndex(), e.GetB().GetIndex(), e.GetConstraintLineIndex())).ToList();
    }

    private IncrementalTin CreateBaseTin()
    {
        var vertices = new List<IVertex>();
        int rows = 3, cols = 3;
        double width = 800, height = 600;
        var xSpace = width / (cols - 1);
        var ySpace = height / (rows - 1);

        for (var i = 0; i < cols; i++)
        for (var j = 0; j < rows; j++)
        {
            var x = i * xSpace;
            var y = j * ySpace;
            var z = (i + j) * 0.5;
            vertices.Add(new Vertex(x, y, z, i * rows + j));
        }

        var tin = new IncrementalTin(Math.Min(xSpace, ySpace) / 2);
        tin.Add(vertices);
        return tin;
    }

    private IQuadEdge? FindEdge(IncrementalTin tin, int vertexA, int vertexB)
    {
        return tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == vertexA && e.GetB().GetIndex() == vertexB)
            || (e.GetA().GetIndex() == vertexB && e.GetB().GetIndex() == vertexA));
    }

    private string GetVertexCoords(IncrementalTin tin, int vertexIndex)
    {
        var vertex = tin.GetVertices().FirstOrDefault((IVertex v) => v.GetIndex() == vertexIndex);
        return vertex != null ? $"{vertex.X:F0},{vertex.Y:F0}" : "NOT FOUND";
    }
}