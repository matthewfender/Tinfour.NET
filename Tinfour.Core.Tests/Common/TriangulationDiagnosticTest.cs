namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

public class TriangulationDiagnosticTest
{
    private readonly ITestOutputHelper _output;

    public TriangulationDiagnosticTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void DiagnoseDifferencesBetween31And32Triangles()
    {
        // Test both a 4x4 grid (should have 18 triangles) and 5x5 grid (should have 32 triangles)
        this._output.WriteLine("=== 4x4 Grid Test ===");
        this.TestGrid(4, 18);

        this._output.WriteLine(string.Empty);
        this._output.WriteLine("=== 5x5 Grid Test ===");
        this.TestGrid(5, 32);
    }

    private void TestGrid(int size, int expectedTriangles)
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();

        // Create a grid of vertices
        for (var i = 0; i < size; i++)
        for (var j = 0; j < size; j++)
            vertices.Add(new Vertex(i, j, i + j));

        this._output.WriteLine($"Adding {vertices.Count} vertices to {size}x{size} grid");
        tin.Add(vertices);

        this._output.WriteLine($"Bootstrapped: {tin.IsBootstrapped()}");
        this._output.WriteLine($"Vertex count: {tin.GetVertices().Count}");

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        this._output.WriteLine($"Triangle count: {triangles.Count} (expected: {expectedTriangles})");

        var triangleCount = tin.CountTriangles();
        this._output.WriteLine($"Valid triangles: {triangleCount.ValidTriangles}");
        this._output.WriteLine($"Ghost triangles: {triangleCount.GhostTriangles}");

        // Check for any constrained edges (should be none in basic triangulation)
        var edges = tin.GetEdges().ToList();
        var constrainedEdges = edges.Where((IQuadEdge e) => e.IsConstrained()).ToList();
        this._output.WriteLine($"Constrained edges found: {constrainedEdges.Count}");

        if (constrainedEdges.Any())
        {
            this._output.WriteLine("WARNING: Found constrained edges in basic triangulation!");
            foreach (var edge in constrainedEdges.Take(5))
                this._output.WriteLine(
                    $"  Edge {edge.GetIndex()}: ({edge.GetA().X},{edge.GetA().Y}) -> ({edge.GetB().X},{edge.GetB().Y})");
        }
    }
}