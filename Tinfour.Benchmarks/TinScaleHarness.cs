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
    ///     Runs the harness. Args: optional vertex counts (default "1000000") and an
    ///     optional <c>--constraints-only</c> flag that skips the unconstrained build
    ///     cases (the 5 M shuffled build alone takes ~25 minutes).
    ///     Example: <c>Tinfour.Benchmarks tin-scale --constraints-only 1000000 5000000</c>.
    /// </summary>
    public static void Run(string[] args)
    {
        var constraintsOnly = args.Any(a => a.Equals("--constraints-only", StringComparison.OrdinalIgnoreCase));
        var countArgs = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        var counts = countArgs.Length > 0
            ? countArgs.Select(int.Parse).ToArray()
            : new[] { 1_000_000 };

        Console.WriteLine("TIN scale harness — synthetic bathymetry (boustrophedon tracks, island shoreline constraint)");
        Console.WriteLine("Orderings: hilbert = sorted; track = raw survey-line order; shuffled = seeded random");
        Console.WriteLine("(shuffled approximates the HashSet snapshot order the pre-826 copy-TIN build inserted in)");
        Console.WriteLine("Constraint-step memory: alloc = GC allocated bytes during AddConstraints (transient churn);");
        Console.WriteLine("retained = settled managed-heap delta across AddConstraints (new edges + retained interpolation surface)");
        Console.WriteLine($"{"vertices",12} {"ordering",10} {"constraints",12} {"build s",10} {"constraint s",13} {"alloc MB",10} {"retained MB",12}");

        foreach (var count in counts)
        {
            var vertices = SyntheticBathymetry.GenerateSurveyPoints(count);
            var ring = SyntheticBathymetry.CreateShorelineRing();

            if (!constraintsOnly)
            {
                var shuffled = new List<IVertex>(vertices);
                Shuffle(shuffled, new Random(97531));

                RunCase(count, "hilbert", vertices, ring, hilbert: true, withConstraints: false);
                RunCase(count, "track", vertices, ring, hilbert: false, withConstraints: false);
                RunCase(count, "shuffled", shuffled, ring, hilbert: false, withConstraints: false);
            }

            RunCase(count, "hilbert", vertices, ring, hilbert: true, withConstraints: true);

            // Attribution control: the same constraint work WITHOUT an
            // interpolation surface. The shoreline ring is all-NaN-Z, so here its
            // vertices stay NaN and conformity splits compute linear-of-NaN instead
            // of draped Z — but the split set and constraint processing are the same
            // (identical synthetic-vertex counts, near-identical walls), so the
            // memory delta between this case and the one above isolates the
            // interpolation-surface share of the constraint-step transient from the
            // common constraint-processing costs (edge wrappers, conformity churn).
            RunCase(count, "hilbert", vertices, ring, hilbert: true, withConstraints: true, preInterpolateZ: false);
        }
    }

    private static void RunCase(
        int count,
        string ordering,
        List<IVertex> vertices,
        List<IVertex> ring,
        bool hilbert,
        bool withConstraints,
        bool preInterpolateZ = true)
    {
        var (buildSeconds, constraintSeconds, constraintAllocBytes, constraintRetainedBytes, timings) =
            BuildOnce(vertices, ring, hilbert, withConstraints, preInterpolateZ);
        var mode = !withConstraints ? "False" : preInterpolateZ ? "True" : "True(noZ)";
        var allocMb = withConstraints ? $"{constraintAllocBytes / (1024.0 * 1024.0),10:N0}" : $"{"-",10}";
        var retainedMb = withConstraints ? $"{constraintRetainedBytes / (1024.0 * 1024.0),12:N0}" : $"{"-",12}";
        Console.WriteLine(
            $"{count,12:N0} {ordering,10} {mode,12} {buildSeconds,10:F2} {constraintSeconds,13:F2} {allocMb} {retainedMb}");

        if (timings != null)
        {
            Console.WriteLine($"{"",12}   seed non-NaN constraint vertices: {timings.SeedConstraintVertices.TotalSeconds:F2} s");
            Console.WriteLine($"{"",12}   interpolation surface build ({timings.InterpolationTinVertexCount:N0} vertices): {timings.InterpolationTinBuild.TotalSeconds:F2} s");
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

    private static (double BuildSeconds, double ConstraintSeconds, long ConstraintAllocBytes,
        long ConstraintRetainedBytes, Core.Diagnostics.AddConstraintsTimings? Timings)
        BuildOnce(List<IVertex> vertices, List<IVertex> ring, bool hilbert, bool withConstraints,
            bool preInterpolateZ = true)
    {
        using var tin = new IncrementalTin(SyntheticBathymetry.DomainSize / Math.Sqrt(vertices.Count));
        tin.PreAllocateForVertices(vertices.Count);

        var sw = Stopwatch.StartNew();
        if (hilbert) tin.Add(vertices, VertexOrder.Hilbert);
        else tin.Add(vertices);
        var buildSeconds = sw.Elapsed.TotalSeconds;

        var constraintSeconds = 0.0;
        var constraintAllocBytes = 0L;
        var constraintRetainedBytes = 0L;
        if (withConstraints)
        {
            var shoreline = new PolygonConstraint(ring, definesRegion: true);

            // Memory probes bracket only the AddConstraints call: settle the heap
            // before starting the stopwatch so the forced collections are not
            // charged to the constraint wall.
            var settledBefore = GC.GetTotalMemory(forceFullCollection: true);
            var allocBefore = GC.GetTotalAllocatedBytes(precise: true);

            sw.Restart();
            tin.AddConstraints(
                new List<IConstraint> { shoreline },
                restoreConformity: true,
                preInterpolateZ,
                InterpolationType.TriangularFacet);
            constraintSeconds = sw.Elapsed.TotalSeconds;

            constraintAllocBytes = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
            constraintRetainedBytes = GC.GetTotalMemory(forceFullCollection: true) - settledBefore;
        }

        return (buildSeconds, constraintSeconds, constraintAllocBytes, constraintRetainedBytes,
            tin.LastAddConstraintsTimings);
    }
}
