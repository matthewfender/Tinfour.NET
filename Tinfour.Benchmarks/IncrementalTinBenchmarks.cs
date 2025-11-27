namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

// Run in Release, single thread, with memory diagnoser
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
public class IncrementalTinBenchmarks
{
    [Params(1_000, 10_000, 100_000 /*, 1_000_000*/)]
    public int VertexCount;

    private List<IVertex> _vertices = null!;

    [Benchmark(Description = "Build IncrementalTin")]
    public TinResult BuildIncrementalTin()
    {
        var tin = new IncrementalTin(1.0);
        tin.PreAllocateForVertices(this.VertexCount);
        tin.AddSorted(this._vertices);

        var edgeCount = 0; // tin.GetEdges().Count;
        var bounds = tin.GetBounds();

        return new TinResult { Vertices = this._vertices.Count, Edges = edgeCount, Triangles = 0, Bounds = bounds };
    }

    [GlobalSetup]
    public void Setup()
    {
        // Generate random vertices with uniform distribution in unit square
        var rnd = new Random(12345);
        this._vertices = new List<IVertex>(this.VertexCount);
        for (var i = 0; i < this.VertexCount; i++)
        {
            var x = rnd.NextDouble() * 1000.0;
            var y = rnd.NextDouble() * 1000.0;
            this._vertices.Add(new Vertex(x, y, 0.0, i));
        }
    }

    public struct TinResult
    {
        public int Vertices { get; set; }

        public int Edges { get; set; }

        public int Triangles { get; set; }

        public (double Left, double Top, double Width, double Height)? Bounds { get; set; }

        public override string ToString()
        {
            return $"V={this.Vertices}, E={this.Edges}, T={this.Triangles}, B={this.Bounds}";
        }
    }
}