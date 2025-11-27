namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Debug the vertex replacement logic in constraint processing to understand
///     why vertex 301 might be getting replaced with vertex 7.
/// </summary>
public class VertexReplacementDebugTest
{
    private readonly ITestOutputHelper _output;

    public VertexReplacementDebugTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void DebugPinwheelSearchDetail()
    {
        this._output.WriteLine("=== PINWHEEL SEARCH DETAIL DEBUG ===");

        // Set up the exact scenario and manually trace through pinwheel search
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
        var vertex300 = new Vertex(width - inset * 2, inset * 2, 0, 300); // (740, 60)  
        var vertex301 = new Vertex(inset * 2, height - inset * 2, 0, 301); // (60, 540)

        tin.Add(vertex300);
        tin.Add(vertex301);

        this._output.WriteLine(
            $"Constraint segment: vertex {vertex300.GetIndex()} ({vertex300.X:F0},{vertex300.Y:F0}) -> vertex {vertex301.GetIndex()} ({vertex301.X:F0},{vertex301.Y:F0})");

        // Find an edge incident to vertex 300
        var edgeFrom300 = tin.GetEdges().FirstOrDefault((IQuadEdge e) => !e.GetB().IsNullVertex() && e.GetA().GetIndex() == 300);

        if (edgeFrom300 != null)
        {
            this._output.WriteLine($"\nStarting pinwheel search from edge {edgeFrom300.GetIndex()}");
            this._output.WriteLine($"Edge: {edgeFrom300.GetA().GetIndex()}-{edgeFrom300.GetB().GetIndex()}");

            // Manually perform pinwheel search
            var e = edgeFrom300;
            var stepCount = 0;
            do
            {
                var b = e.GetB();
                this._output.WriteLine(
                    $"Step {stepCount}: Edge {e.GetIndex()} connects to vertex {b.GetIndex()} at ({b.X:F0},{b.Y:F0})");

                if (b.IsNullVertex())
                {
                    this._output.WriteLine("  -> Ghost vertex");
                }
                else
                {
                    var isMatch = ReferenceEquals(b, vertex301);
                    this._output.WriteLine($"  -> ReferenceEquals(b, vertex301) = {isMatch}");

                    if (isMatch)
                    {
                        this._output.WriteLine("  -> *** FOUND MATCH - would mark edge as constrained ***");
                        break;
                    }
                }

                e = e.GetDualFromReverse()!;
                stepCount++;

                if (stepCount > 10)
                {
                    // Safety break
                    this._output.WriteLine($"  -> Breaking after {stepCount} steps (safety)");
                    break;
                }
            }
            while (!ReferenceEquals(e, edgeFrom300));

            if (stepCount <= 10 && !ReferenceEquals(e, edgeFrom300))
                this._output.WriteLine("Pinwheel search completed, found match");
            else this._output.WriteLine("Pinwheel search completed without finding direct edge to vertex 301");
        }
    }

    [Fact]
    public void DebugVertexReplacementLogic()
    {
        this._output.WriteLine("=== VERTEX REPLACEMENT DEBUG ===");

        // Set up the exact same scenario
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

        this._output.WriteLine("Constraint vertices before processing:");
        foreach (var vertex in constraintVertices)
            this._output.WriteLine($"  Vertex {vertex.GetIndex()}: ({vertex.X:F0}, {vertex.Y:F0})");

        // Now simulate the vertex replacement logic manually
        this._output.WriteLine("\n=== TESTING VERTEX REPLACEMENT LOGIC ===");

        var walker = new StochasticLawsonsWalk();
        var thresholds = new Thresholds();

        foreach (var constraintVertex in constraintVertices)
        {
            this._output.WriteLine(
                $"\nTesting vertex {constraintVertex.GetIndex()} at ({constraintVertex.X:F0},{constraintVertex.Y:F0}):");

            // Find matching vertex using the same logic as FindMatchingVertexInTin
            var startEdge = tin.GetEdges().FirstOrDefault((IQuadEdge e) => !e.GetB().IsNullVertex());
            if (startEdge != null)
            {
                var nearEdge = walker.FindAnEdgeFromEnclosingTriangle(
                    startEdge,
                    constraintVertex.X,
                    constraintVertex.Y);

                // Check the vertices of the enclosing triangle
                var vertices_near = new[] { nearEdge.GetA(), nearEdge.GetB(), nearEdge.GetForward().GetB() };

                var tolerance = thresholds.GetVertexTolerance();
                this._output.WriteLine($"  Tolerance: {tolerance}");
                this._output.WriteLine("  Enclosing triangle vertices:");

                foreach (var vertex in vertices_near)
                    if (!vertex.IsNullVertex())
                    {
                        var distance = Math.Sqrt(
                            Math.Pow(constraintVertex.X - vertex.X, 2) + Math.Pow(constraintVertex.Y - vertex.Y, 2));
                        var near = distance <= tolerance;
                        this._output.WriteLine(
                            $"    Vertex {vertex.GetIndex()}: ({vertex.X:F0},{vertex.Y:F0}) distance={distance:F1} near={near}");

                        if (near)
                            this._output.WriteLine(
                                $"    *** MATCH FOUND: Constraint vertex {constraintVertex.GetIndex()} would be replaced with vertex {vertex.GetIndex()} ***");
                    }
            }
        }

        // Now create constraints and test actual replacement
        this._output.WriteLine("\n=== TESTING ACTUAL CONSTRAINT PROCESSING ===");

        var diagonal2 = new LinearConstraint(
            [
                constraintVertices[2], // vertex 300 (740, 60)
                constraintVertices[3] // vertex 301 (60, 540)
            ]);

        this._output.WriteLine("Original constraint vertices:");
        foreach (var vertex in diagonal2.GetVertices())
            this._output.WriteLine($"  Vertex {vertex.GetIndex()}: ({vertex.X:F0}, {vertex.Y:F0})");

        // Add the constraint and see what gets replaced
        tin.AddConstraints([diagonal2], true);

        this._output.WriteLine("\nAfter constraint processing:");
        var constrainedEdges = tin.GetEdges().Where((IQuadEdge e) =>
            !e.GetB().IsNullVertex() && e.IsConstrained()
                                     && e.GetConstraintLineIndex() == diagonal2.GetConstraintIndex()).ToList();

        foreach (var edge in constrainedEdges)
        {
            var va = edge.GetA();
            var vb = edge.GetB();
            this._output.WriteLine(
                $"  Constrained edge: {va.GetIndex()}-{vb.GetIndex()} [({va.X:F0},{va.Y:F0})-({vb.X:F0},{vb.Y:F0})]");
        }
    }
}