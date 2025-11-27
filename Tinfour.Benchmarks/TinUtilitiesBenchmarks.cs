namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

/// <summary>
///     Benchmarks for TIN utility operations, measuring performance of data extraction
///     from triangulated irregular networks across different vertex counts.
/// </summary>
/// <remarks>
///     This benchmark class focuses on testing the performance of extracting geometric
///     data (edges, triangles, vertices) from pre-built TINs. Over time, this will be
///     extended to test new approaches and optimizations for TIN data extraction.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 2, 5)]
[RPlotExporter]
public class TinUtilitiesBenchmarks
{
    private const double TinHeight = 5000.0;

    // Fixed bounds for all tests
    private const double TinWidth = 5000.0;

    // Number of vertices in the TIN
    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int VertexCount;

    private IncrementalTin _tin = null!;

    [GlobalCleanup]
    public void Cleanup()
    {
        this._tin?.Dispose();
    }

    [Benchmark(Description = "Count Triangles")]
    public TriangleCountResult CountTriangles()
    {
        var triangleCount = this._tin.CountTriangles();

        return new TriangleCountResult
                   {
                       ValidTriangles = triangleCount.ValidTriangles,
                       GhostTriangles = triangleCount.GhostTriangles,
                       ConstrainedTriangles = triangleCount.ConstrainedTriangles,
                       TotalTriangles = triangleCount.TotalTriangles
                   };
    }

    [Benchmark(Description = "Extract All Edges")]
    public EdgeExtractionResult ExtractAllEdges()
    {
        var edges = this._tin.GetEdges();

        var totalEdges = edges.Count;
        var ghostEdges = 0;
        var constrainedEdges = 0;
        var totalLength = 0.0;

        foreach (var edge in edges)
        {
            // Check if edge is a ghost edge (has null vertex)
            if (edge.GetA().IsNullVertex() || edge.GetB().IsNullVertex()) ghostEdges++;
            else totalLength += edge.GetLength();

            // Check if edge is constrained
            if (edge.IsConstrained()) constrainedEdges++;
        }

        return new EdgeExtractionResult
                   {
                       TotalEdges = totalEdges,
                       GhostEdges = ghostEdges,
                       ConstrainedEdges = constrainedEdges,
                       ValidEdges = totalEdges - ghostEdges,
                       TotalLength = totalLength,
                       AverageLength = totalEdges > ghostEdges ? totalLength / (totalEdges - ghostEdges) : 0.0
                   };
    }

    [Benchmark(Description = "Extract All Triangles")]
    public TriangleExtractionResult ExtractAllTriangles()
    {
        var triangles = this._tin.GetTriangles().ToList();

        var totalTriangles = triangles.Count;
        var totalArea = 0.0;
        var minArea = double.MaxValue;
        var maxArea = double.MinValue;
        var degenerateTriangles = 0;

        foreach (var triangle in triangles)
        {
            var area = Math.Abs(triangle.GetArea());

            if (area < 1e-10)
            {
                // Consider very small triangles as degenerate
                degenerateTriangles++;
            }
            else
            {
                totalArea += area;
                minArea = Math.Min(minArea, area);
                maxArea = Math.Max(maxArea, area);
            }
        }

        return new TriangleExtractionResult
                   {
                       TotalTriangles = totalTriangles,
                       DegenerateTriangles = degenerateTriangles,
                       ValidTriangles = totalTriangles - degenerateTriangles,
                       TotalArea = totalArea,
                       MinArea = minArea == double.MaxValue ? 0.0 : minArea,
                       MaxArea = maxArea == double.MinValue ? 0.0 : maxArea,
                       AverageArea = totalTriangles > degenerateTriangles
                                         ? totalArea / (totalTriangles - degenerateTriangles)
                                         : 0.0
                   };
    }

    [Benchmark(Description = "Extract All Vertices")]
    public VertexExtractionResult ExtractAllVertices()
    {
        var vertices = this._tin.GetVertices();

        var totalVertices = vertices.Count;
        var nullVertices = 0;
        var syntheticVertices = 0;
        var constraintVertices = 0;

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        double minZ = double.MaxValue, maxZ = double.MinValue;

        foreach (var vertex in vertices)
        {
            if (vertex.IsNullVertex())
            {
                nullVertices++;
                continue;
            }

            if (vertex.IsSynthetic()) syntheticVertices++;

            // Check constraint membership only for Vertex structs
            if (vertex is Vertex v && v.IsConstraintMember()) constraintVertices++;

            // Update bounds
            var x = vertex.GetX();
            var y = vertex.GetY();
            var z = vertex.GetZ();

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
            minZ = Math.Min(minZ, z);
            maxZ = Math.Max(maxZ, z);
        }

        return new VertexExtractionResult
                   {
                       TotalVertices = totalVertices,
                       NullVertices = nullVertices,
                       SyntheticVertices = syntheticVertices,
                       ConstraintVertices = constraintVertices,
                       ValidVertices = totalVertices - nullVertices,
                       BoundsX = (minX, maxX),
                       BoundsY = (minY, maxY),
                       BoundsZ = (minZ, maxZ)
                   };
    }

    [Benchmark(Description = "Extract Perimeter")]
    public PerimeterExtractionResult ExtractPerimeter()
    {
        var perimeterEdges = this._tin.GetPerimeter();

        var totalPerimeterEdges = perimeterEdges.Count;
        var totalPerimeterLength = 0.0;
        var vertices = new HashSet<IVertex>();

        foreach (var edge in perimeterEdges)
        {
            totalPerimeterLength += edge.GetLength();

            // Collect unique vertices from perimeter
            var a = edge.GetA();
            var b = edge.GetB();

            if (!a.IsNullVertex()) vertices.Add(a);
            if (!b.IsNullVertex()) vertices.Add(b);
        }

        return new PerimeterExtractionResult
                   {
                       PerimeterEdges = totalPerimeterEdges,
                       PerimeterLength = totalPerimeterLength,
                       PerimeterVertices = vertices.Count,
                       AverageEdgeLength = totalPerimeterEdges > 0 ? totalPerimeterLength / totalPerimeterEdges : 0.0
                   };
    }

    [GlobalSetup]
    public void Setup()
    {
        // Generate TIN with specified vertex count
        this._tin = GenerateTerrainTin(this.VertexCount, TinWidth, TinHeight);

        // Verify TIN was created successfully
        if (!this._tin.IsBootstrapped())
            throw new InvalidOperationException($"Failed to bootstrap TIN with {this.VertexCount} vertices");

        Console.WriteLine($"TIN setup complete: {this.VertexCount} vertices, " + $"bounds: {TinWidth}x{TinHeight}");
    }

    /// <summary>
    ///     Generates a TIN with terrain data for benchmarking purposes.
    /// </summary>
    /// <param name="vertexCount">Number of vertices to generate</param>
    /// <param name="width">Width of the terrain bounds</param>
    /// <param name="height">Height of the terrain bounds</param>
    /// <param name="seed">Random seed for reproducible results</param>
    /// <returns>A bootstrapped IncrementalTin</returns>
    private static IncrementalTin GenerateTerrainTin(int vertexCount, double width, double height, int seed = 42)
    {
        var vertices = new List<IVertex>(vertexCount);
        var random = new Random(seed);

        // Generate vertices with realistic terrain elevation pattern
        for (var i = 0; i < vertexCount; i++)
        {
            var x = random.NextDouble() * width;
            var y = random.NextDouble() * height;

            // Generate terrain with multiple frequency components
            var z = 100 * Math.Sin(x * 0.001) * Math.Cos(y * 0.001) + // Large terrain features
                    50 * Math.Sin(x * 0.003) * Math.Sin(y * 0.003) + // Medium hills
                    20 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01) + // Small features
                    5 * Math.Sin(x * 0.05) * Math.Sin(y * 0.05) + // Fine detail
                    random.NextDouble() * 2; // Noise

            vertices.Add(new Vertex(x, y, z, i));
        }

        // Create TIN with optimal settings for the vertex count
        var nominalSpacing = Math.Sqrt(width * height / vertexCount);
        var tin = new IncrementalTin(nominalSpacing);

        // Pre-allocate for better performance
        tin.PreAllocateForVertices(vertexCount);

        // Use sorted insertion for better performance on large datasets
        var success = tin.AddSorted(vertices);

        if (!success)
            throw new InvalidOperationException($"Failed to bootstrap triangulation with {vertexCount} vertices");

        return tin;
    }
}

/// <summary>
///     Result structure for edge extraction benchmarks.
/// </summary>
public struct EdgeExtractionResult
{
    public int TotalEdges { get; set; }

    public int ValidEdges { get; set; }

    public int GhostEdges { get; set; }

    public int ConstrainedEdges { get; set; }

    public double TotalLength { get; set; }

    public double AverageLength { get; set; }

    public override string ToString()
    {
        return
            $"Edges: {this.TotalEdges} (Valid: {this.ValidEdges}, Ghost: {this.GhostEdges}, Constrained: {this.ConstrainedEdges}), "
            + $"Avg Length: {this.AverageLength:F2}";
    }
}

/// <summary>
///     Result structure for triangle extraction benchmarks.
/// </summary>
public struct TriangleExtractionResult
{
    public int TotalTriangles { get; set; }

    public int ValidTriangles { get; set; }

    public int DegenerateTriangles { get; set; }

    public double TotalArea { get; set; }

    public double MinArea { get; set; }

    public double MaxArea { get; set; }

    public double AverageArea { get; set; }

    public override string ToString()
    {
        return
            $"Triangles: {this.TotalTriangles} (Valid: {this.ValidTriangles}, Degenerate: {this.DegenerateTriangles}), "
            + $"Avg Area: {this.AverageArea:F2}";
    }
}

/// <summary>
///     Result structure for vertex extraction benchmarks.
/// </summary>
public struct VertexExtractionResult
{
    public int TotalVertices { get; set; }

    public int ValidVertices { get; set; }

    public int NullVertices { get; set; }

    public int SyntheticVertices { get; set; }

    public int ConstraintVertices { get; set; }

    public (double Min, double Max) BoundsX { get; set; }

    public (double Min, double Max) BoundsY { get; set; }

    public (double Min, double Max) BoundsZ { get; set; }

    public override string ToString()
    {
        return $"Vertices: {this.TotalVertices} (Valid: {this.ValidVertices}, Null: {this.NullVertices}, "
               + $"Synthetic: {this.SyntheticVertices}, Constraint: {this.ConstraintVertices})";
    }
}

/// <summary>
///     Result structure for perimeter extraction benchmarks.
/// </summary>
public struct PerimeterExtractionResult
{
    public int PerimeterEdges { get; set; }

    public int PerimeterVertices { get; set; }

    public double PerimeterLength { get; set; }

    public double AverageEdgeLength { get; set; }

    public override string ToString()
    {
        return $"Perimeter: {this.PerimeterEdges} edges, {this.PerimeterVertices} vertices, "
               + $"Length: {this.PerimeterLength:F2}, Avg Edge: {this.AverageEdgeLength:F2}";
    }
}

/// <summary>
///     Result structure for triangle counting benchmarks.
/// </summary>
public struct TriangleCountResult
{
    public int ValidTriangles { get; set; }

    public int GhostTriangles { get; set; }

    public int ConstrainedTriangles { get; set; }

    public int TotalTriangles { get; set; }

    public override string ToString()
    {
        return $"Count: {this.TotalTriangles} (Valid: {this.ValidTriangles}, Ghost: {this.GhostTriangles}, "
               + $"Constrained: {this.ConstrainedTriangles})";
    }
}