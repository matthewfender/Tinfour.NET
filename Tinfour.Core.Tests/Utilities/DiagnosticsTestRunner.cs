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

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     Simple test runner to verify diagnostics are working correctly.
/// </summary>
public static class DiagnosticsTestRunner
{
    public static void RunSimpleTest()
    {
        Console.WriteLine("=== Simple Diagnostics Test ===");

        // Create a small TIN
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0),
                               new Vertex(10, 0, 5),
                               new Vertex(10, 10, 10),
                               new Vertex(0, 10, 5),
                               new Vertex(5, 5, 7.5),
                               new Vertex(2, 2, 2),
                               new Vertex(8, 8, 8),
                               new Vertex(3, 7, 6)
                           };

        tin.Add(vertices);
        Console.WriteLine($"Created TIN with {vertices.Count} vertices");

        // Create interpolator and rasterizer
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        Console.WriteLine("Performing 20x20 rasterization...");
        var stopwatch = Stopwatch.StartNew();
        var result = rasterizer.CreateRaster(20, 20);
        stopwatch.Stop();

        Console.WriteLine($"Rasterization completed in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Coverage: {result.CoveragePercent:F1}%");

        // Try to get diagnostics from interpolator's navigator
        // try
        // {
        // var interpolatorType = interpolator.GetType();
        // var navigatorField = interpolatorType.GetField("_navigator", 
        // System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // if (navigatorField?.GetValue(interpolator) is IncrementalTinNavigator interpolatorNavigator)
        // {
        // Console.WriteLine("Successfully accessed interpolator's navigator");

        // var walkerField = interpolatorNavigator.GetType().GetField("_walker", 
        // System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // if (walkerField?.GetValue(interpolatorNavigator) is var walker && walker != null)
        // {
        // Console.WriteLine($"Walker type: {walker.GetType().Name}");

        // var getDiagnostics = walker.GetType().GetMethod("GetDiagnostics");
        // if (getDiagnostics?.Invoke(walker, null) is var diagnostics && diagnostics != null)
        // {
        // Console.WriteLine($"\nWalk Diagnostics: {diagnostics}");

        // var diagType = diagnostics.GetType();
        // var walks = diagType.GetProperty("NumberOfWalks")?.GetValue(diagnostics);
        // var tests = diagType.GetProperty("NumberOfTests")?.GetValue(diagnostics);

        // if (walks is int walkCount && tests is int testCount && walkCount > 0)
        // {
        // Console.WriteLine($"Total walks: {walkCount}");
        // Console.WriteLine($"Total tests: {testCount}");
        // Console.WriteLine($"Tests per walk: {(double)testCount / walkCount:F2}");
        // Console.WriteLine($"Walks per interpolation: {(double)walkCount / 400:F3}");
        // }
        // else
        // {
        // Console.WriteLine("No walk data available or walks = 0");
        // }
        // }
        // else
        // {
        // Console.WriteLine("GetDiagnostics returned null");
        // }
        // }
        // else
        // {
        // Console.WriteLine("Could not access walker field");
        // }
        // }
        // else
        // {
        // Console.WriteLine("Could not access navigator field from interpolator");
        // }
        // }
        // catch (Exception ex)
        // {
        // Console.WriteLine($"Error accessing diagnostics: {ex.Message}");
        // }
        Console.WriteLine("\nTest complete.");
    }
}