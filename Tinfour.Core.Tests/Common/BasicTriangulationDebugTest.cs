namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

public class BasicTriangulationDebugTest
{
    private readonly ITestOutputHelper _output;

    public BasicTriangulationDebugTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void DebugLargerTriangulation()
    {
        // Create a 5x5 grid of vertices (25 vertices total)
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();

        for (var i = 0; i < 5; i++)
        for (var j = 0; j < 5; j++)
            vertices.Add(new Vertex(i, j, i + j));

        this._output.WriteLine($"Adding {vertices.Count} vertices to TIN");

        // Act
        tin.Add(vertices);

        // Debug output
        this._output.WriteLine($"Bootstrapped: {tin.IsBootstrapped()}");
        this._output.WriteLine($"Vertex count: {tin.GetVertices().Count}");
        this._output.WriteLine($"Edge count: {tin.GetEdges().Count}");

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        this._output.WriteLine($"Non-ghost triangles: {triangles.Count}");

        var triangleCount = tin.CountTriangles();
        this._output.WriteLine($"Valid triangles: {triangleCount.ValidTriangles}");
        this._output.WriteLine($"Ghost triangles: {triangleCount.GhostTriangles}");

        // Check if there are any edges with strange indices
        var edges = tin.GetEdges().ToList();
        var negativeIndexEdges = edges.Where((IQuadEdge e) => e.GetIndex() < 0).ToList();
        if (negativeIndexEdges.Any())
        {
            this._output.WriteLine($"WARNING: Found {negativeIndexEdges.Count} edges with negative indices!");
            foreach (var edge in negativeIndexEdges.Take(5)) // Show first 5
                this._output.WriteLine(
                    $"  Edge {edge.GetIndex()}: ({edge.GetA().X},{edge.GetA().Y}) -> ({edge.GetB().X},{edge.GetB().Y})");
        }

        var bounds = tin.GetBounds();
        if (bounds.HasValue) this._output.WriteLine($"Bounds: {bounds.Value}");
        else this._output.WriteLine("Bounds: null");
    }
}