namespace Tinfour.Benchmarks;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     One-shot, stopwatch-based TIN build timings at ReefMaster scale (1 M / 5 M vertices).
///     Complements <see cref="IncrementalTinBenchmarks" />: BenchmarkDotNet gives rigorous
///     statistics but a full 5 M matrix takes a long while; this harness produces a single
///     directly comparable table (including AddConstraints per-phase attribution) in minutes.
/// </summary>
public static class TinScaleHarness
{
    /// <summary>
    ///     Runs the harness. Args: optional vertex counts (default "1000000").
    ///     Example: <c>Tinfour.Benchmarks tin-scale 1000000 5000000</c>.
    /// </summary>
    public static void Run(string[] args)
    {
        var counts = args.Length > 0
            ? args.Select(int.Parse).ToArray()
            : new[] { 1_000_000 };

        Console.WriteLine("TIN scale harness — synthetic bathymetry (boustrophedon tracks, island shoreline constraint)");
        Console.WriteLine("Orderings: hilbert = sorted; track = raw survey-line order; shuffled = seeded random");
        Console.WriteLine("(shuffled approximates the HashSet snapshot order the pre-826 copy-TIN build inserted in)");
        Console.WriteLine($"{"vertices",12} {"ordering",10} {"constraints",12} {"build s",10} {"constraint s",13}");

        foreach (var count in counts)
        {
            var vertices = SyntheticBathymetry.GenerateSurveyPoints(count);
            var ring = SyntheticBathymetry.CreateShorelineRing();

            var shuffled = new List<IVertex>(vertices);
            Shuffle(shuffled, new Random(97531));

            RunCase(count, "hilbert", vertices, ring, hilbert: true, withConstraints: false);
            RunCase(count, "track", vertices, ring, hilbert: false, withConstraints: false);
            RunCase(count, "shuffled", shuffled, ring, hilbert: false, withConstraints: false);
            RunCase(count, "hilbert", vertices, ring, hilbert: true, withConstraints: true);
        }
    }

    private static void RunCase(
        int count,
        string ordering,
        List<IVertex> vertices,
        List<IVertex> ring,
        bool hilbert,
        bool withConstraints)
    {
        var (buildSeconds, constraintSeconds, timings) = BuildOnce(vertices, ring, hilbert, withConstraints);
        Console.WriteLine(
            $"{count,12:N0} {ordering,10} {withConstraints,12} {buildSeconds,10:F2} {constraintSeconds,13:F2}");

        if (timings != null)
        {
            Console.WriteLine($"{"",12}   seed non-NaN constraint vertices: {timings.SeedConstraintVertices.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   interpolation copy-TIN build ({timings.InterpolationTinVertexCount:N0} vertices): {timings.InterpolationTinBuild.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   interpolate+insert constraint vertices: {timings.InterpolateAndInsertVertices.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   process constraints: {timings.ProcessConstraints.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   restore conformity ({timings.RestoreConformitySyntheticVertices:N0} synthetic vertices): {timings.RestoreConformity.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   flood fill: {timings.FloodFill.TotalSeconds:F2} s");
        }
    }

    private static void Shuffle(List<IVertex> list, Random rnd)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static (double BuildSeconds, double ConstraintSeconds, Core.Diagnostics.AddConstraintsTimings? Timings)
        BuildOnce(List<IVertex> vertices, List<IVertex> ring, bool hilbert, bool withConstraints)
    {
        using var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(vertices.Count));
        tin.PreAllocateForVertices(vertices.Count);

        var sw = Stopwatch.StartNew();
        if (hilbert) tin.Add(vertices, VertexOrder.Hilbert);
        else tin.Add(vertices);
        var buildSeconds = sw.Elapsed.TotalSeconds;

        var constraintSeconds = 0.0;
        if (withConstraints)
        {
            var shoreline = new PolygonConstraint(ring, definesRegion: true);
            sw.Restart();
            tin.AddConstraints(
                new List<IConstraint> { shoreline },
                restoreConformity: true,
                preInterpolateZ: true,
                InterpolationType.TriangularFacet);
            constraintSeconds = sw.Elapsed.TotalSeconds;
        }

        return (buildSeconds, constraintSeconds, tin.LastAddConstraintsTimings);
    }
}
