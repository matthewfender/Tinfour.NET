namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Isolate the problematic diagonal constraint to see if the issue occurs
///     when processing only that single constraint.
/// </summary>
public class SingleConstraintIsolationTest
{
    private readonly ITestOutputHelper _output;

    public SingleConstraintIsolationTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void TestBothDiagonalsSequentially()
    {
        this._output.WriteLine("=== BOTH DIAGONALS SEQUENTIALLY TEST ===");

        // Set up the same grid
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

        // Add all constraint vertices
        var inset = Math.Min(xSpace, ySpace) * 0.1;
        var constraintVertices = new IVertex[]
                                     {
                                         new Vertex(inset * 2, inset * 2, 0, 200), // (60, 60)
                                         new Vertex(width - inset * 2, height - inset * 2, 0, 201), // (740, 540)
                                         new Vertex(width - inset * 2, inset * 2, 0, 300), // (740, 60)  
                                         new Vertex(inset * 2, height - inset * 2, 0, 301) // (60, 540)
                                     };

        foreach (var vertex in constraintVertices) tin.Add(vertex);

        // Create both diagonal constraints
        var diagonal1 = new LinearConstraint([constraintVertices[0], constraintVertices[1]]); // 200->201
        var diagonal2 = new LinearConstraint([constraintVertices[2], constraintVertices[3]]); // 300->301

        this._output.WriteLine($"Created diagonal 1: {diagonal1.GetConstraintIndex()} (200->201)");
        this._output.WriteLine($"Created diagonal 2: {diagonal2.GetConstraintIndex()} (300->301)");

        // Add diagonal 1 first
        this._output.WriteLine("\n=== ADDING DIAGONAL 1 ONLY ===");
        this.LogConstrainedEdges(tin, "BEFORE diagonal 1");

        tin.AddConstraints([diagonal1], true);

        this.LogConstrainedEdges(tin, "AFTER diagonal 1");
        this.AnalyzeSpecificEdge(tin, 300, 7);

        // Add diagonal 2 second
        this._output.WriteLine("\n=== ADDING DIAGONAL 2 ===");
        this.LogConstrainedEdges(tin, "BEFORE diagonal 2");

        tin.AddConstraints([diagonal2], true);

        this.LogConstrainedEdges(tin, "AFTER diagonal 2");
        this.AnalyzeSpecificEdge(tin, 300, 7);
    }

    [Fact]
    public void TestSingleProblematicDiagonal()
    {
        this._output.WriteLine("=== SINGLE PROBLEMATIC DIAGONAL TEST ===");

        // Set up the exact same grid
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

        this._output.WriteLine($"Base TIN: {tin.GetVertices().Count} vertices");

        // Add constraint vertices
        var inset = Math.Min(xSpace, ySpace) * 0.1;
        var vertex300 = new Vertex(width - inset * 2, inset * 2, 0, 300); // (740, 60)  
        var vertex301 = new Vertex(inset * 2, height - inset * 2, 0, 301); // (60, 540)

        tin.Add(vertex300);
        tin.Add(vertex301);

        this._output.WriteLine("Added constraint vertices 300 and 301");
        this._output.WriteLine($"Vertex 300: ({vertex300.X:F0}, {vertex300.Y:F0})");
        this._output.WriteLine($"Vertex 301: ({vertex301.X:F0}, {vertex301.Y:F0})");

        // Log edges before constraint
        this.LogConstrainedEdges(tin, "BEFORE constraint");

        // Create ONLY the problematic diagonal constraint
        var diagonal2 = new LinearConstraint([vertex300, vertex301]);
        this._output.WriteLine($"\nCreated constraint {diagonal2.GetConstraintIndex()}: vertex 300 -> vertex 301");

        // Add the single constraint
        tin.AddConstraints([diagonal2], true);

        this._output.WriteLine("\nAdded single constraint");

        // Log edges after constraint
        this.LogConstrainedEdges(tin, "AFTER constraint");

        // Analyze if edge 300-7 is marked
        this.AnalyzeSpecificEdge(tin, 300, 7);
    }

    private void AnalyzeSpecificEdge(IncrementalTin tin, int vertexA, int vertexB)
    {
        var edge = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex()).FirstOrDefault((IQuadEdge e) =>
            (e.GetA().GetIndex() == vertexA && e.GetB().GetIndex() == vertexB)
            || (e.GetA().GetIndex() == vertexB && e.GetB().GetIndex() == vertexA));

        if (edge != null)
        {
            this._output.WriteLine($"\nEdge {vertexA}-{vertexB} analysis:");
            this._output.WriteLine($"  Edge index: {edge.GetIndex()}");
            this._output.WriteLine($"  Is constrained: {edge.IsConstrained()}");
            this._output.WriteLine($"  Line index: {edge.GetConstraintLineIndex()}");
            this._output.WriteLine($"  Border index: {edge.GetConstraintBorderIndex()}");
        }
        else
        {
            this._output.WriteLine($"\nEdge {vertexA}-{vertexB}: NOT FOUND");
        }
    }

    private void LogConstrainedEdges(IncrementalTin tin, string phase)
    {
        var constrainedEdges = tin.GetEdges().Where((IQuadEdge e) => !e.GetB().IsNullVertex() && e.IsConstrained()).ToList();

        this._output.WriteLine($"{phase}: {constrainedEdges.Count} constrained edges");
        foreach (var edge in constrainedEdges)
        {
            var va = edge.GetA();
            var vb = edge.GetB();
            this._output.WriteLine(
                $"  Edge {edge.GetIndex()}: {va.GetIndex()}-{vb.GetIndex()} "
                + $"[({va.X:F0},{va.Y:F0})-({vb.X:F0},{vb.Y:F0})] Line:{edge.GetConstraintLineIndex()}");
        }
    }
}