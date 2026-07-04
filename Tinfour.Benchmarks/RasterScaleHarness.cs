namespace Tinfour.Benchmarks;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     One-shot, stopwatch-based raster-fill timings at ReefMaster scale. Introduced for
///     ticket 830 (dynamic row partitioning in <see cref="TinRasterizer" />): run the same
///     command on the pre-/post-830 code for directly comparable numbers. Includes a padded
///     bounds case (raster extent 2x the data extent, ~75 % all-NaN cells concentrated in the
///     outer rows) which is exactly the shape that load-imbalances static row bands.
///     The reported NoData counts double as an output-equality check between runs.
/// </summary>
public static class RasterScaleHarness
{
    /// <summary>
    ///     Runs the harness. Args: optional vertex counts (default "1000000").
    ///     Example: <c>Tinfour.Benchmarks raster-scale 1000000 5000000</c>.
    /// </summary>
    public static void Run(string[] args)
    {
        var counts = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 1_000_000 };

        // ~2 m cells over the 10 km domain at 5 M vertices — mirrors the RM depth-label
        // grid (4,339x5,431 @ 2 m). Kept fixed across vertex counts for comparability.
        const int gridWidth = 4_339;
        const int gridHeight = 5_431;

        Console.WriteLine("Raster scale harness — synthetic bathymetry, grid 4,339x5,431 (RM depth-label shape)");
        Console.WriteLine("bounds=data: full-extent fill; bounds=padded: 2x extent, outer ~75% NaN (imbalance case)");
        Console.WriteLine("Two timed passes per case: pass 1 includes JIT; pass 2 is the comparable number.");
        Console.WriteLine($"{"vertices",12} {"interp",18} {"bounds",8} {"pass",6} {"fill s",8} {"noData",12}");

        foreach (var count in counts)
        {
            var vertices = SyntheticBathymetry.GenerateSurveyPoints(count);

            using var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(vertices.Count));
            tin.PreAllocateForVertices(vertices.Count);
            tin.Add(vertices, VertexOrder.Hilbert);

            var dataBounds = (Left: 0.0, Top: 0.0, Width: SyntheticBathymetry.DomainSize, Height: SyntheticBathymetry.DomainSize);
            var pad = SyntheticBathymetry.DomainSize / 2.0;
            var paddedBounds = (Left: -pad, Top: -pad, Width: 2.0 * SyntheticBathymetry.DomainSize, Height: 2.0 * SyntheticBathymetry.DomainSize);

            foreach (var interpolationType in new[] { InterpolationType.NaturalNeighbor, InterpolationType.TriangularFacet })
            {
                RunCase(tin, count, interpolationType, "data", dataBounds, gridWidth, gridHeight);
                RunCase(tin, count, interpolationType, "padded", paddedBounds, gridWidth, gridHeight);
            }
        }
    }

    private static void RunCase(
        IIncrementalTin tin,
        int count,
        InterpolationType interpolationType,
        string boundsLabel,
        (double Left, double Top, double Width, double Height) bounds,
        int gridWidth,
        int gridHeight)
    {
        // MaxInterpolationDistance mirrors RM's depth-grid default (50 m): cells far from
        // any data become NaN, which is what creates the imbalance in the padded case.
        var options = new InterpolatorOptions { MaxInterpolationDistance = 50.0 };
        var rasterizer = new TinRasterizer(tin, interpolationType, options);

        for (var pass = 1; pass <= 2; pass++)
        {
            var sink = new Float32RasterData(gridWidth, gridHeight);
            var sw = Stopwatch.StartNew();
            var result = rasterizer.CreateRaster(sink, bounds);
            sw.Stop();

            Console.WriteLine(
                $"{count,12:N0} {interpolationType,18} {boundsLabel,8} {pass,6} {sw.Elapsed.TotalSeconds,8:F2} {result.NoDataCount,12:N0}");
        }
    }
}
