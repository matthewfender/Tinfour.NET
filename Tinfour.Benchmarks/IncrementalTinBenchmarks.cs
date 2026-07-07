namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

// Run in Release, single thread, with memory diagnoser. One invocation per iteration:
// the large builds run for seconds, so BenchmarkDotNet's pilot/unroll stages would
// multiply a multi-minute matrix for no accuracy gain.
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3, invocationCount: 1)]
public class IncrementalTinBenchmarks
{
    /// <summary>
    ///     100 k = legacy quick check; 1 M / 5 M = ReefMaster-scale bathymetry
    ///     (the 200-islands protocol triangulates ~5.16 M vertices).
    /// </summary>
    [Params(100_000, 1_000_000, 5_000_000)]
    public int VertexCount;

    /// <summary>
    ///     Hilbert-sorted vs raw insertion order for the primary build. Unsorted
    ///     incremental insertion walks ~O(n^1.5); Hilbert keeps it O(n)-amortized.
    /// </summary>
    [Params(true, false)]
    public bool UseHilbertOrdering;

    /// <summary>
    ///     When true, adds an island shoreline polygon constraint with
    ///     preInterpolateZ (the drape path that internally rebuilds the full
    ///     vertex set into a transient interpolation TIN) and conformity restoration —
    ///     mirroring ReefMaster's map-generation call.
    /// </summary>
    [Params(true, false)]
    public bool WithConstraints;

    private List<IVertex> _vertices = null!;

    private List<IVertex> _shorelineVertices = null!;

    [Benchmark(Description = "Build IncrementalTin")]
    public TinResult BuildIncrementalTin()
    {
        var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(this.VertexCount));
        tin.PreAllocateForVertices(this.VertexCount);
        if (this.UseHilbertOrdering) tin.Add(this._vertices, VertexOrder.Hilbert);
        else tin.Add(this._vertices);

        if (this.WithConstraints)
        {
            // Constraints are stateful (Complete/SetConstraintIndex), so build a fresh
            // instance per invocation from the cached vertex ring.
            var shoreline = new PolygonConstraint(this._shorelineVertices, definesRegion: true);
            tin.AddConstraints(
                new List<IConstraint> { shoreline },
                restoreConformity: true,
                preInterpolateZ: true,
                InterpolationType.TriangularFacet);
        }

        var bounds = tin.GetBounds();
        var result = new TinResult
        {
            Vertices = this._vertices.Count,
            Edges = 0,
            Triangles = 0,
            Bounds = bounds,
        };

        tin.Dispose();
        return result;
    }

    [GlobalSetup]
    public void Setup()
    {
        this._vertices = SyntheticBathymetry.GenerateSurveyPoints(this.VertexCount);
        this._shorelineVertices = SyntheticBathymetry.CreateShorelineRing();
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
