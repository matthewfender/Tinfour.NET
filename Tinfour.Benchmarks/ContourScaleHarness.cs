namespace Tinfour.Benchmarks;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Standard;

/// <summary>
///     One-shot, stopwatch-based contour extraction timings at ReefMaster scale
///     (1 M / 5 M vertices, 36 levels — the 200-islands protocol shape). Introduced for
///     ticket 827 (single bracketing sweep in <see cref="ContourBuilderForTin" />): run the
///     same command on the pre-/post-827 code to get directly comparable numbers, and use
///     the reported contour/point counts to confirm output equality between the two.
/// </summary>
public static class ContourScaleHarness
{
    /// <summary>
    ///     Runs the harness. Args: optional vertex counts (default "1000000").
    ///     Example: <c>Tinfour.Benchmarks contour-scale 1000000 5000000</c>.
    /// </summary>
    public static void Run(string[] args)
    {
        var counts = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 1_000_000 };

        const int levelCount = 36;

        Console.WriteLine("Contour scale harness — synthetic bathymetry, 36 levels (RM 200-islands protocol shape)");
        Console.WriteLine("Two timed passes per TIN: pass 1 includes JIT; pass 2 is the comparable number.");
        Console.WriteLine($"{"vertices",12} {"pass",6} {"contour s",10} {"contours",10} {"points",12}");

        foreach (var count in counts)
        {
            var vertices = SyntheticBathymetry.GenerateSurveyPoints(count);

            using var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(vertices.Count));
            tin.PreAllocateForVertices(vertices.Count);
            tin.Add(vertices, VertexOrder.Hilbert);

            // 36 levels strictly inside the synthetic depth range (about -25 .. -1).
            var zMin = vertices.Min(v => v.GetZ());
            var zMax = vertices.Max(v => v.GetZ());
            var levels = new double[levelCount];
            var step = (zMax - zMin) / (levelCount + 1);
            for (var i = 0; i < levelCount; i++) levels[i] = zMin + (i + 1) * step;

            for (var pass = 1; pass <= 2; pass++)
            {
                var sw = Stopwatch.StartNew();
                var builder = new ContourBuilderForTin(tin, null, levels, buildRegions: false);
                var contours = builder.GetContours();
                sw.Stop();

                var points = contours.Sum(c => c.Size());
                Console.WriteLine(
                    $"{count,12:N0} {pass,6} {sw.Elapsed.TotalSeconds,10:F2} {contours.Count,10:N0} {points,12:N0}");
            }
        }
    }
}
