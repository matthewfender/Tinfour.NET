namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Running;

public class Program
{
    public static void Main(string[] args)
    {
        // Check if user wants to run specific benchmarks
        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "tin":
                case "triangulation":
                    BenchmarkRunner.Run<IncrementalTinBenchmarks>();
                    break;
                case "raster":
                case "rasterizer":
                    BenchmarkRunner.Run<TinRasterizerBenchmarks>();
                    break;
                case "utilities":
                case "util":
                case "extract":
                    BenchmarkRunner.Run<TinUtilitiesBenchmarks>();
                    break;
                case "refinement":
                case "refine":
                case "ruppert":
                    Console.WriteLine("Running Ruppert refinement benchmarks...");
                    Console.WriteLine("Use 'refinement-detailed' for scalability tests or 'refinement-interp' for interpolation impact tests.");
                    BenchmarkRunner.Run<RuppertRefinementBenchmarks>();
                    break;
                case "refinement-detailed":
                case "refine-detailed":
                    Console.WriteLine("Running detailed Ruppert refinement benchmarks (scalability)...");
                    BenchmarkRunner.Run<RuppertRefinementDetailedBenchmarks>();
                    break;
                case "refinement-interp":
                case "refine-interp":
                    Console.WriteLine("Running Ruppert refinement interpolation impact benchmarks...");
                    BenchmarkRunner.Run<RuppertInterpolationImpactBenchmarks>();
                    break;
                case "refinement-realworld":
                case "refine-realworld":
                case "lake":
                    Console.WriteLine("Running real-world American Lake survey refinement benchmarks...");
                    BenchmarkRunner.Run<RuppertRealWorldBenchmarks>();
                    break;
                case "test-resources":
                    TestResourceLoading.Test();
                    break;
                case "test-tin":
                    TestResourceLoading.TestTinBuilding();
                    break;
                case "refinement-all":
                case "refine-all":
                    Console.WriteLine("Running all Ruppert refinement benchmarks...");
                    BenchmarkRunner.Run<RuppertRefinementBenchmarks>();
                    BenchmarkRunner.Run<RuppertRefinementDetailedBenchmarks>();
                    BenchmarkRunner.Run<RuppertInterpolationImpactBenchmarks>();
                    BenchmarkRunner.Run<RuppertRealWorldBenchmarks>();
                    break;
                case "all":
                    BenchmarkRunner.Run<IncrementalTinBenchmarks>();
                    BenchmarkRunner.Run<InterpolationBenchmarks>();
                    BenchmarkRunner.Run<TinUtilitiesBenchmarks>();
                    BenchmarkRunner.Run<RuppertRefinementBenchmarks>();
                    break;
                default:
                    PrintUsage();
                    break;
            }
        }
        else
        {
            // Default: run interpolation benchmarks (most relevant for current development)
            Console.WriteLine("Running interpolation benchmarks (use 'all' argument to run all benchmarks)");
            BenchmarkRunner.Run<InterpolationBenchmarks>();
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Tinfour.Benchmarks <benchmark-type>");
        Console.WriteLine();
        Console.WriteLine("Benchmark types:");
        Console.WriteLine("  tin               - Run TIN construction benchmarks");
        Console.WriteLine("  interpolation     - Run triangular facet interpolation benchmarks");
        Console.WriteLine("  utilities         - Run TIN data extraction benchmarks");
        Console.WriteLine();
        Console.WriteLine("Ruppert Refinement Benchmarks:");
        Console.WriteLine("  refinement        - Run main Ruppert refinement benchmarks (various scenarios)");
        Console.WriteLine("  refinement-detailed - Run scalability benchmarks (varying vertex counts)");
        Console.WriteLine("  refinement-interp - Run Z-interpolation impact benchmarks");
        Console.WriteLine("  refinement-realworld / lake - Real-world American Lake beam survey (17K points)");
        Console.WriteLine("  refinement-all    - Run all refinement benchmarks");
        Console.WriteLine();
        Console.WriteLine("  all               - Run all benchmarks");
    }
}
