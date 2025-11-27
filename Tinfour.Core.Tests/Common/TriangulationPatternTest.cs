namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

public class TriangulationPatternTest
{
    private readonly ITestOutputHelper _output;

    public TriangulationPatternTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void TestDifferentPointPatterns()
    {
        this._output.WriteLine("=== Testing Random Points vs Grid ===");

        // Test 1: Random points (25 points)
        this._output.WriteLine("--- Random 25 Points ---");
        this.TestRandomPoints(25);

        // Test 2: Perfect square (25 points in 5x5 grid)
        this._output.WriteLine(string.Empty);
        this._output.WriteLine("--- Perfect 5x5 Grid ---");
        this.TestGrid(5);

        // Test 3: Smaller grid (16 points in 4x4 grid)
        this._output.WriteLine(string.Empty);
        this._output.WriteLine("--- Perfect 4x4 Grid ---");
        this.TestGrid(4);
    }

    private void TestGrid(int size)
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();

        // Create perfect grid
        for (var i = 0; i < size; i++)
        for (var j = 0; j < size; j++)
            vertices.Add(new Vertex(i, j, i + j));

        var expectedTriangles = 2 * (size - 1) * (size - 1);
        tin.Add(vertices);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        var triangleCount = tin.CountTriangles();

        this._output.WriteLine(
            $"{size}x{size} grid: {triangles.Count} triangles (expected: {expectedTriangles}), {triangleCount.ValidTriangles} valid");

        if (triangles.Count != expectedTriangles)
            this._output.WriteLine($"  *** Missing {expectedTriangles - triangles.Count} triangle(s) ***");
    }

    private void TestRandomPoints(int count)
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>();
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < count; i++)
        {
            var x = random.NextDouble() * 4.0;
            var y = random.NextDouble() * 4.0;
            vertices.Add(new Vertex(x, y, x + y, i));
        }

        tin.Add(vertices);

        var triangles = tin.GetTriangles().Where((SimpleTriangle t) => !t.IsGhost()).ToList();
        var triangleCount = tin.CountTriangles();

        this._output.WriteLine($"Random points: {triangles.Count} triangles, {triangleCount.ValidTriangles} valid");
    }
}