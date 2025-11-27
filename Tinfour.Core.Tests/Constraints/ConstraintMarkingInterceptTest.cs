namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Surgical debugging to trace exactly when edge 300-7 gets marked as constrained.
///     This test intercepts the SetConstrained calls to identify the exact moment
///     when the wrong edge gets marked.
/// </summary>
public class ConstraintMarkingInterceptTest
{
    private readonly List<(string operation, int edgeIndex, int vertexA, int vertexB, int constraintIndex)> _markingHistory = new();

    private readonly ITestOutputHelper _output;

    public ConstraintMarkingInterceptTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void InterceptConstraintMarkingOperations()
    {
        this._output.WriteLine("=== INTERCEPT CONSTRAINT MARKING OPERATIONS ===");

        // Create the exact scenario but with custom SetConstrained interceptor
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

        // Add constraint vertices
        var inset = Math.Min(xSpace, ySpace) * 0.1;
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(inset * 2, inset * 2, 0, 200), // (60, 60)
                                         new Vertex(width - inset * 2, height - inset * 2, 0, 201), // (740, 540)
                                         new Vertex(width - inset * 2, inset * 2, 0, 300), // (740, 60)  
                                         new Vertex(inset * 2, height - inset * 2, 0, 301) // (60, 540)
                                     };

        foreach (var vertex in constraintVertices) tin.Add(vertex);

        // Instrument the TIN to capture edge marking operations
        this.InstrumentEdgeMarkingOperations(tin);

        // First add diagonal 1
        var diagonal1 = new LinearConstraint([constraintVertices[0], constraintVertices[1]]); // 200->201
        this._output.WriteLine($"=== PROCESSING DIAGONAL 1 (index {diagonal1.GetConstraintIndex()}) ===");

        this._markingHistory.Clear();
        tin.AddConstraints([diagonal1], true);

        this._output.WriteLine($"Diagonal 1 marked {this._markingHistory.Count} edges:");
        foreach (var (op, edgeIdx, vA, vB, cIdx) in this._markingHistory)
            this._output.WriteLine($"  {op}: Edge {edgeIdx} ({vA}-{vB}) with constraint {cIdx}");

        // Now add diagonal 2 with detailed interception
        var diagonal2 = new LinearConstraint([constraintVertices[2], constraintVertices[3]]); // 300->301
        this._output.WriteLine($"\n=== PROCESSING DIAGONAL 2 (index {diagonal2.GetConstraintIndex()}) ===");

        this._markingHistory.Clear();
        tin.AddConstraints([diagonal2], true);

        this._output.WriteLine($"Diagonal 2 marked {this._markingHistory.Count} edges:");
        foreach (var (op, edgeIdx, vA, vB, cIdx) in this._markingHistory)
        {
            this._output.WriteLine($"  {op}: Edge {edgeIdx} ({vA}-{vB}) with constraint {cIdx}");

            if ((vA == 300 && vB == 7) || (vA == 7 && vB == 300))
            {
                this._output.WriteLine(
                    $"  *** FOUND THE PROBLEMATIC MARKING! Edge 300-7 marked with constraint {cIdx} ***");
                this._output.WriteLine(
                    $"  This is INCORRECT - constraint {cIdx} should be for segment 300->301, not 300->7");
            }
        }

        // Verify the final state
        var edge300to7 = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == 300 && e.GetB().GetIndex() == 7)
            || (e.GetA().GetIndex() == 7 && e.GetB().GetIndex() == 300));

        if (edge300to7 != null)
            this._output.WriteLine(
                $"\nFINAL STATE: Edge 300-7 is constrained: {edge300to7.IsConstrained()}, Line: {edge300to7.GetConstraintLineIndex()}");
    }

    [Fact]
    public void TraceConstraintSegmentProcessing()
    {
        this._output.WriteLine("=== TRACE CONSTRAINT SEGMENT PROCESSING ===");

        // This test focuses on understanding how constraint segments are processed
        // and where the logic might go wrong for multiple constraints
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

        // Add constraint vertices
        var inset = Math.Min(xSpace, ySpace) * 0.1;
        var vertex200 = new Vertex(inset * 2, inset * 2, 0, 200); // (60, 60)
        var vertex201 = new Vertex(width - inset * 2, height - inset * 2, 0, 201); // (740, 540)
        var vertex300 = new Vertex(width - inset * 2, inset * 2, 0, 300); // (740, 60)  
        var vertex301 = new Vertex(inset * 2, height - inset * 2, 0, 301); // (60, 540)

        tin.Add(vertex200);
        tin.Add(vertex201);
        tin.Add(vertex300);
        tin.Add(vertex301);

        this._output.WriteLine("Base TIN with constraint vertices created");

        // Analyze the edge structure before any constraints
        this._output.WriteLine("\nEdge structure before constraints:");
        this.LogRelevantEdges(tin, "BEFORE");

        // Create and add constraints one at a time with detailed analysis
        var diagonal1 = new LinearConstraint([vertex200, vertex201]);
        this._output.WriteLine(
            $"\nDiagonal 1: {vertex200.GetIndex()}({vertex200.X:F0},{vertex200.Y:F0}) -> {vertex201.GetIndex()}({vertex201.X:F0},{vertex201.Y:F0})");

        // Capture edge state before diagonal 1
        var edgesBefore1 = this.CaptureEdgeState(tin);
        tin.AddConstraints([diagonal1], true);
        var edgesAfter1 = this.CaptureEdgeState(tin);

        this._output.WriteLine("Changes after diagonal 1:");
        this.CompareEdgeStates(edgesBefore1, edgesAfter1);

        var diagonal2 = new LinearConstraint([vertex300, vertex301]);
        this._output.WriteLine(
            $"\nDiagonal 2: {vertex300.GetIndex()}({vertex300.X:F0},{vertex300.Y:F0}) -> {vertex301.GetIndex()}({vertex301.X:F0},{vertex301.Y:F0})");

        // Capture edge state before diagonal 2
        var edgesBefore2 = this.CaptureEdgeState(tin);
        tin.AddConstraints([diagonal2], true);
        var edgesAfter2 = this.CaptureEdgeState(tin);

        this._output.WriteLine("Changes after diagonal 2:");
        this.CompareEdgeStates(edgesBefore2, edgesAfter2);

        // Focus on the problematic edge
        var problematicEdge = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == 300 && e.GetB().GetIndex() == 7)
            || (e.GetA().GetIndex() == 7 && e.GetB().GetIndex() == 300));

        if (problematicEdge != null)
        {
            this._output.WriteLine("\n=== PROBLEMATIC EDGE ANALYSIS ===");
            this._output.WriteLine(
                $"Edge {problematicEdge.GetIndex()}: {problematicEdge.GetA().GetIndex()}-{problematicEdge.GetB().GetIndex()}");
            this._output.WriteLine($"Constrained: {problematicEdge.IsConstrained()}");
            this._output.WriteLine($"Line index: {problematicEdge.GetConstraintLineIndex()}");
            this._output.WriteLine("This edge should NOT be constrained for diagonal 2 (300->301)");
        }
    }

    private Dictionary<int, (int vA, int vB, bool constrained, int lineIndex)> CaptureEdgeState(IncrementalTin tin)
    {
        var state = new Dictionary<int, (int, int, bool, int)>();

        foreach (var edge in tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()))
            state[edge.GetIndex()] = (edge.GetA().GetIndex(), edge.GetB().GetIndex(), edge.IsConstrained(),
                                         edge.GetConstraintLineIndex());

        return state;
    }

    private void CompareEdgeStates(
        Dictionary<int, (int vA, int vB, bool constrained, int lineIndex)> before,
        Dictionary<int, (int vA, int vB, bool constrained, int lineIndex)> after)
    {
        var changes = new List<string>();

        foreach (var (edgeId, afterState) in after)
            if (before.TryGetValue(edgeId, out var beforeState))
            {
                if (beforeState.constrained != afterState.constrained || beforeState.lineIndex != afterState.lineIndex)
                    changes.Add(
                        $"  Edge {edgeId} ({afterState.vA}-{afterState.vB}): "
                        + $"constrained {beforeState.constrained}->{afterState.constrained}, "
                        + $"line {beforeState.lineIndex}->{afterState.lineIndex}");
            }
            else
            {
                changes.Add(
                    $"  Edge {edgeId} ({afterState.vA}-{afterState.vB}): NEW EDGE, "
                    + $"constrained={afterState.constrained}, line={afterState.lineIndex}");
            }

        if (changes.Count == 0) this._output.WriteLine("  No changes detected");
        else
            foreach (var change in changes)
                this._output.WriteLine(change);
    }

    /// <summary>
    ///     This would ideally intercept SetConstrained calls, but since we can't easily
    ///     monkey-patch the constraint processor, we'll use a different approach.
    /// </summary>
    private void InstrumentEdgeMarkingOperations(IncrementalTin tin)
    {
        // Since we can't easily intercept the internal SetConstrained calls,
        // we'll capture the state before and after each constraint addition
        // and diff the edges to see which ones changed
    }

    private void LogRelevantEdges(IncrementalTin tin, string phase)
    {
        var edges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).ToList();
        var constraintVertexIds = new[] { 200, 201, 300, 301, 4, 7 }; // Include center and vertex 7

        var relevantEdges = edges.Where((IQuadEdge e) =>
                constraintVertexIds.Contains(e.GetA().GetIndex()) || constraintVertexIds.Contains(e.GetB().GetIndex()))
            .ToList();

        this._output.WriteLine($"{phase}: {relevantEdges.Count} relevant edges:");
        foreach (var edge in relevantEdges)
        {
            var va = edge.GetA();
            var vb = edge.GetB();
            var constraintInfo = edge.IsConstrained() ? $" [Line:{edge.GetConstraintLineIndex()}]" : string.Empty;
            this._output.WriteLine($"  Edge {edge.GetIndex()}: {va.GetIndex()}-{vb.GetIndex()}{constraintInfo}");
        }
    }
}