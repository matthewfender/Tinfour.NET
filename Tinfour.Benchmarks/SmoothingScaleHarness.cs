namespace Tinfour.Benchmarks;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;

/// <summary>
///     One-shot, stopwatch-based <see cref="SmoothingFilter" /> construction timings at
///     ReefMaster scale (1 M / 5 M vertices, 6 passes — the 200-islands protocol shape).
///     Introduced for ticket 829: run the same command on the pre-/post-829 code to get
///     directly comparable numbers, and use the reported fingerprint (an order-independent
///     XOR of the smoothed-Z bit patterns) to confirm bit-identical output between the two.
/// </summary>
public static class SmoothingScaleHarness
{
    private const int Passes = 6;

    /// <summary>
    ///     Runs the harness. Args: optional vertex counts (default "1000000").
    ///     Example: <c>Tinfour.Benchmarks smoothing-scale 1000000 5000000</c>.
    /// </summary>
    public static void Run(string[] args)
    {
        var counts = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 1_000_000 };

        Console.WriteLine($"Smoothing scale harness — synthetic bathymetry + shoreline constraint, {Passes} passes (RM 200-islands protocol shape)");
        Console.WriteLine("Two timed constructions per TIN: pass 1 includes JIT; pass 2 is the comparable number.");
        Console.WriteLine("Fingerprint = XOR of smoothed-Z bit patterns over all TIN vertices (order-independent, bit-exact).");

        foreach (var count in counts)
        {
            var vertices = SyntheticBathymetry.GenerateSurveyPoints(count);
            var shoreline = SyntheticBathymetry.CreateShorelineConstraint();

            using var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(vertices.Count));
            tin.PreAllocateForVertices(vertices.Count);
            tin.Add(vertices, VertexOrder.Hilbert);
            tin.AddConstraints(
                new List<IConstraint> { shoreline },
                restoreConformity: true,
                preInterpolateZ: true,
                InterpolationType.TriangularFacet);

            var tinVertices = tin.GetVertices();

            for (var run = 1; run <= 2; run++)
            {
                var sw = Stopwatch.StartNew();
                var filter = new SmoothingFilter(tin, Passes);
                sw.Stop();

                var fingerprint = 0L;
                foreach (var v in tinVertices)
                    fingerprint ^= BitConverter.DoubleToInt64Bits(filter.Value(v));

                Console.WriteLine();
                Console.WriteLine($"{count,12:N0} vertices, run {run}: construct {sw.Elapsed.TotalSeconds:F2} s");
                Console.WriteLine($"{"",12}   {filter.Timings}");
                Console.WriteLine($"{"",12}   minZ={filter.MinZ:F6} maxZ={filter.MaxZ:F6} fingerprint={fingerprint:X16}");
            }
        }
    }
}
