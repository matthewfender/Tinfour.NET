namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     Result structure for interpolation benchmarks.
/// </summary>
public struct InterpolationResult
{
    public int ValidCount { get; set; }

    public int NanCount { get; set; }

    public double AverageValue { get; set; }

    public double MinValue { get; set; }

    public double MaxValue { get; set; }

    public int TotalPoints { get; set; }

    public override string ToString()
    {
        return
            $"Valid={this.ValidCount}, NaN={this.NanCount}, Avg={this.AverageValue:F2}, Range=[{this.MinValue:F2}, {this.MaxValue:F2}]";
    }
}

/// <summary>
///     Benchmarks for TIN interpolation operations, measuring performance across different
///     TIN sizes, vertex counts, and interpolation types.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
[RPlotExporter]
public class InterpolationBenchmarks
{
    // Interpolation type
    [Params(
        InterpolationType.TriangularFacet,
        InterpolationType.NaturalNeighbor)] // , InterpolationType.InverseDistanceWeighting)]
    public InterpolationType InterpolationType;

    // TIN size parameters (bounds of the triangulation area)
    [Params( /*1000,*/ 2500 /*10000*/)] // Comment out largest for now
    public int TinBounds;

    // Number of vertices in the TIN
    [Params(100_000, 250_000)] // Comment out largest for now  
    public int VertexCount;

    private IInterpolatorOverTin _interpolator = null!;

    private IncrementalTin _tin = null!;

    /// <summary>
    ///     Generates a TIN with sinusoidal terrain data matching the visualizer pattern.
    /// </summary>
    public static IncrementalTin GenerateTerrainTin(int vertexCount, double width, double height, int seed = 42)
    {
        var vertices = new List<IVertex>(vertexCount);
        var random = new Random(seed);

        // Generate vertices with sinusoidal elevation pattern
        for (var i = 0; i < vertexCount; i++)
        {
            var x = random.NextDouble() * width;
            var y = random.NextDouble() * height;

            // Generate terrain with multiple frequency components (from TriangulationGenerator)
            var z = 50 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01) + // Large hills
                    20 * Math.Sin(x * 0.03) * Math.Sin(y * 0.03) + // Medium features
                    5 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1) + // Small features
                    random.NextDouble() * 2; // Noise

            vertices.Add(new Vertex(x, y, z, i));
        }

        // Create and populate TIN with optimal settings
        var nominalSpacing = Math.Sqrt(width * height / vertexCount);
        var tin = new IncrementalTin(nominalSpacing);
        tin.PreAllocateForVertices(vertexCount);

        var success = tin.AddSorted(vertices); // Use Hilbert sorting for better performance

        if (!success) throw new InvalidOperationException("Failed to bootstrap triangulation");

        return tin;
    }

    [Benchmark(Description = "Interpolate Full Grid")]
    public InterpolationResult InterpolateFullGrid()
    {
        var totalValue = 0.0;
        var validCount = 0;
        var nanCount = 0;
        var minValue = double.MaxValue;
        var maxValue = double.MinValue;

        // Interpolate at regular intervals within the TIN bounds
        for (var y = 0; y < this.TinBounds; y++)
        for (var x = 0; x < this.TinBounds; x++)
        {
            var z = this._interpolator.Interpolate(x, y, null);

            if (double.IsNaN(z))
            {
                nanCount++;
            }
            else
            {
                totalValue += z;
                validCount++;
                if (z < minValue) minValue = z;
                if (z > maxValue) maxValue = z;
            }
        }

        return new InterpolationResult
                   {
                       ValidCount = validCount,
                       NanCount = nanCount,
                       AverageValue = validCount > 0 ? totalValue / validCount : 0.0,
                       MinValue = validCount > 0 ? minValue : 0.0,
                       MaxValue = validCount > 0 ? maxValue : 0.0,
                       TotalPoints = this.TinBounds * this.TinBounds
                   };
    }

    // [Benchmark(Description = "Interpolate Random Points")]
    public InterpolationResult InterpolateRandomPoints()
    {
        var totalValue = 0.0;
        var validCount = 0;
        var nanCount = 0;
        var minValue = double.MaxValue;
        var maxValue = double.MinValue;

        // Number of random points to sample
        var sampleCount = 10000;
        var random = new Random(42); // Fixed seed for reproducibility

        // Interpolate at random locations within the TIN bounds
        for (var i = 0; i < sampleCount; i++)
        {
            var x = random.NextDouble() * this.TinBounds;
            var y = random.NextDouble() * this.TinBounds;

            var z = this._interpolator.Interpolate(x, y, null);

            if (double.IsNaN(z))
            {
                nanCount++;
            }
            else
            {
                totalValue += z;
                validCount++;
                if (z < minValue) minValue = z;
                if (z > maxValue) maxValue = z;
            }
        }

        return new InterpolationResult
                   {
                       ValidCount = validCount,
                       NanCount = nanCount,
                       AverageValue = validCount > 0 ? totalValue / validCount : 0.0,
                       MinValue = validCount > 0 ? minValue : 0.0,
                       MaxValue = validCount > 0 ? maxValue : 0.0,
                       TotalPoints = sampleCount
                   };
    }

    [Benchmark(Description = "Parallel Interpolate Full Grid")]
    public InterpolationResult ParallelInterpolateFullGrid()
    {
        // Thread-local accumulators that will be aggregated at the end
        long totalValidCount = 0;
        long totalNanCount = 0;
        var totalSum = 0.0;
        var globalMin = double.MaxValue;
        var globalMax = double.MinValue;

        // Determine how many threads to use - one per processor core is typically optimal
        var processorCount = Environment.ProcessorCount;

        // Create an array of interpolators - one per thread
        var interpolators = new IInterpolatorOverTin[processorCount];
        for (var i = 0; i < processorCount; i++)

            // Create a new interpolator for each thread based on type
            interpolators[i] = this.InterpolationType switch
                {
                    InterpolationType.TriangularFacet => new TriangularFacetInterpolator(this._tin),
                    InterpolationType.NaturalNeighbor => new NaturalNeighborInterpolator(this._tin),
                    InterpolationType.InverseDistanceWeighting => new InverseDistanceWeightingInterpolator(this._tin),
                    _ => throw new ArgumentException($"Unsupported interpolation type: {this.InterpolationType}")
                };

        // Calculate rows per thread (ensure each thread gets contiguous rows)
        var rowsPerThread = this.TinBounds / processorCount;

        // Process the grid in parallel, with each thread handling contiguous rows
        Parallel.For(
            0,
            processorCount,
            (int threadIndex) =>
                {
                    // Calculate the range of rows for this thread
                    var startRow = threadIndex * rowsPerThread;
                    var endRow = threadIndex == processorCount - 1
                                     ? this.TinBounds // Last thread takes any remaining rows
                                     : startRow + rowsPerThread;

                    // Thread-local accumulators
                    var validCount = 0;
                    var nanCount = 0;
                    var totalValue = 0.0;
                    var minValue = double.MaxValue;
                    var maxValue = double.MinValue;

                    // Get the interpolator for this thread
                    var interpolator = interpolators[threadIndex];

                    // Process assigned rows
                    for (var y = startRow; y < endRow; y++)
                    for (var x = 0; x < this.TinBounds; x++)
                    {
                        var z = interpolator.Interpolate(x, y, null);

                        if (double.IsNaN(z))
                        {
                            nanCount++;
                        }
                        else
                        {
                            validCount++;
                            totalValue += z;
                            if (z < minValue) minValue = z;
                            if (z > maxValue) maxValue = z;
                        }
                    }

                    // Safely aggregate results
                    lock (interpolators)
                    {
                        totalValidCount += validCount;
                        totalNanCount += nanCount;
                        totalSum += totalValue;

                        if (validCount > 0)
                        {
                            if (minValue < globalMin) globalMin = minValue;
                            if (maxValue > globalMax) globalMax = maxValue;
                        }
                    }
                });

        // Calculate final statistics
        var avgValue = totalValidCount > 0 ? totalSum / totalValidCount : 0.0;
        if (totalValidCount == 0)
        {
            globalMin = 0.0;
            globalMax = 0.0;
        }

        return new InterpolationResult
                   {
                       ValidCount = (int)totalValidCount,
                       NanCount = (int)totalNanCount,
                       AverageValue = avgValue,
                       MinValue = globalMin,
                       MaxValue = globalMax,
                       TotalPoints = this.TinBounds * this.TinBounds
                   };
    }

    [GlobalSetup]
    public void Setup()
    {
        // Generate TIN with sinusoidal terrain data
        this._tin = GenerateTerrainTin(this.VertexCount, this.TinBounds, this.TinBounds);

        // Create interpolator based on type
        this._interpolator = InterpolatorFactory.Create(this._tin, this.InterpolationType);
    }
}