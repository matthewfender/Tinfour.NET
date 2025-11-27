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
                case "all":
                    BenchmarkRunner.Run<IncrementalTinBenchmarks>();
                    BenchmarkRunner.Run<InterpolationBenchmarks>();
                    BenchmarkRunner.Run<TinUtilitiesBenchmarks>();
                    break;
                default:
                    Console.WriteLine("Usage: Tinfour.Benchmarks [tin|interpolation|utilities|all]");
                    Console.WriteLine("  tin          - Run TIN construction benchmarks");
                    Console.WriteLine("  interpolation - Run triangular facet interpolation benchmarks");
                    Console.WriteLine("  utilities     - Run TIN data extraction benchmarks");
                    Console.WriteLine("  all          - Run all benchmarks");
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
}