/*
 * Copyright 2025 G.W. Lucas
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Tinfour.Core.Tests.Utilities;

using System.Diagnostics;
using System.Reflection;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     Utility class to run diagnostic tests and output performance metrics
///     for rasterization optimization analysis.
/// </summary>
public static class RasterizerDiagnosticsRunner
{
    public static void RunDiagnostics()
    {
        Console.WriteLine("=== TIN Rasterization Performance Diagnostics ===");
        Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // Test 1: Small TIN for basic validation
        // Console.WriteLine("\n--- Test 1: Basic TIN (25x25 = 625 vertices) ---");
        // RunSingleTest(500, 5000);

        // Test 2: Medium TIN for performance analysis
        Console.WriteLine("\n--- Test 2: Medium TIN (1000x1000, 10_000 vertices) ---");
        RunSingleTest(1000, 10000);

        // Test 3: Large TIN for optimization insights
        // Console.WriteLine("\n--- Test 3: Large TIN (100x100 = 10000 vertices) ---");
        // RunSingleTest(2500, 100_000);
        Console.WriteLine("\n=== Diagnostics Complete ===");
    }

    private static void RunSingleTest(int bounds, int numVertices)
    {
        // Create TIN
        var tin = new IncrementalTin();
        var random = new Random(42);
        var vertices = new List<IVertex>();

        Console.WriteLine($"Creating {bounds}x{bounds} TIN...");
        var tinStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < numVertices; i++)
        {
            var x = random.NextDouble() * bounds;
            var y = random.NextDouble() * bounds;
            var z = Math.Sin(x * 0.1) * Math.Cos(y * 0.1) * 10.0;
            vertices.Add(new Vertex(x, y, z));
        }

        tin.Add(vertices);
        tinStopwatch.Stop();

        Console.WriteLine($"TIN created in {tinStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Vertices: {vertices.Count}, Edges: {tin.GetEdges().Count}");

        // Test TriangularFacet interpolator
        Console.WriteLine($"\nRasterizing to {bounds}x{bounds}...");
        var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor);

        var rasterStopwatch = Stopwatch.StartNew();
        var result = rasterizer.CreateRaster(bounds, bounds);
        rasterStopwatch.Stop();

        var totalPoints = bounds * bounds;
        Console.WriteLine($"Rasterization completed in {rasterStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine(
            $"Performance: {totalPoints * 1000.0 / rasterStopwatch.ElapsedMilliseconds:F0} interpolations/sec");
        Console.WriteLine($"Coverage: {result.CoveragePercent:F2}%");

        // Get walk diagnostics from the interpolator's navigator (not the TIN's navigator)
        try
        {
            // todo: this diagnostic method won't work any more since we are now parallelizing the rasterization and there are multiple navigators
            IInterpolatorOverTin interpolator = null;

            // Access the navigator that was created by the TriangularFacetInterpolator
            var interpolatorType = interpolator.GetType();
            var navigatorField = interpolatorType.GetField(
                "_navigator",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (navigatorField?.GetValue(interpolator) is IncrementalTinNavigator interpolatorNavigator)
            {
                Console.WriteLine("Accessing walker from interpolator's navigator...");

                var walkerField = interpolatorNavigator.GetType().GetField(
                    "_walker",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (walkerField?.GetValue(interpolatorNavigator) is var walker && walker != null)
                {
                    var getDiagnostics = walker.GetType().GetMethod("GetDiagnostics");
                    if (getDiagnostics?.Invoke(walker, null) is var diagnostics && diagnostics != null)
                    {
                        Console.WriteLine($"\nWalk Diagnostics: {diagnostics}");

                        var diagType = diagnostics.GetType();
                        var walks = diagType.GetProperty("NumberOfWalks")?.GetValue(diagnostics);
                        var tests = diagType.GetProperty("NumberOfTests")?.GetValue(diagnostics);
                        var avgSteps = diagType.GetProperty("AverageStepsToCompletion")?.GetValue(diagnostics);
                        var extendedPrecision = diagType.GetProperty("NumberOfExtendedPrecisionCalls")
                            ?.GetValue(diagnostics);
                        var exteriorWalks = diagType.GetProperty("NumberOfExteriorWalks")?.GetValue(diagnostics);

                        if (walks is int walkCount && walkCount > 0 && tests is int testCount)
                        {
                            Console.WriteLine("Efficiency Metrics:");
                            Console.WriteLine($"  Total walks: {walkCount:N0}");
                            Console.WriteLine($"  Total tests: {testCount:N0}");
                            Console.WriteLine($"  Walks per interpolation: {(double)walkCount / totalPoints:F3}");
                            Console.WriteLine($"  Tests per walk: {(double)testCount / walkCount:F2}");
                            Console.WriteLine($"  Average steps per walk: {avgSteps:F2}");
                            Console.WriteLine(
                                $"  Exterior walks: {exteriorWalks} ({(double)(int)exteriorWalks / walkCount * 100:F1}%)");
                            Console.WriteLine($"  Extended precision calls: {extendedPrecision:N0}");
                            Console.WriteLine(
                                $"  Extended precision ratio: {(long)extendedPrecision * 100.0 / testCount:F2}%");
                            Console.WriteLine(
                                $"  Walk rate: {walkCount * 1000.0 / rasterStopwatch.ElapsedMilliseconds:F0} walks/sec");
                            Console.WriteLine(
                                $"  Test rate: {testCount * 1000.0 / rasterStopwatch.ElapsedMilliseconds:F0} tests/sec");

                            // Performance assessment
                            var testsPerWalk = (double)testCount / walkCount;
                            var extPrecisionRatio = (long)extendedPrecision * 100.0 / testCount;

                            Console.WriteLine("Performance Assessment:");
                            if (testsPerWalk < 3.0)
                                Console.WriteLine($"  ✅ Excellent walk efficiency (avg {testsPerWalk:F1} tests/walk)");
                            else if (testsPerWalk < 5.0)
                                Console.WriteLine($"  ✓ Good walk efficiency (avg {testsPerWalk:F1} tests/walk)");
                            else
                                Console.WriteLine($"  ⚠ Poor walk efficiency (avg {testsPerWalk:F1} tests/walk)");

                            if (extPrecisionRatio < 1.0)
                                Console.WriteLine($"  ✅ Low extended precision usage ({extPrecisionRatio:F2}%)");
                            else if (extPrecisionRatio < 5.0)
                                Console.WriteLine($"  ✓ Moderate extended precision usage ({extPrecisionRatio:F2}%)");
                            else
                                Console.WriteLine($"  ⚠ High extended precision usage ({extPrecisionRatio:F2}%)");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Walker found but GetDiagnostics method not available or returned null");
                    }
                }
                else
                {
                    Console.WriteLine("Could not access _walker field from interpolator's navigator");
                }
            }
            else
            {
                Console.WriteLine("Could not access _navigator field from TriangularFacetInterpolator");

                // Fallback: try the TIN's navigator as before
                Console.WriteLine("Trying TIN's navigator as fallback...");
                var navigator = tin.GetNavigator();
                if (navigator is IncrementalTinNavigator tinNavigator)
                {
                    var walkerField = tinNavigator.GetType().GetField(
                        "_walker",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (walkerField?.GetValue(tinNavigator) is var walker && walker != null)
                    {
                        var getDiagnostics = walker.GetType().GetMethod("GetDiagnostics");
                        if (getDiagnostics?.Invoke(walker, null) is var diagnostics && diagnostics != null)
                            Console.WriteLine($"TIN Navigator Walk Diagnostics: {diagnostics}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not access walker diagnostics: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        var stats = result.GetStatistics();
        Console.WriteLine($"\nValue Statistics: Min={stats.Min:F2}, Max={stats.Max:F2}, Mean={stats.Mean:F2}");
    }
}