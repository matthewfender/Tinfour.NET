/*
 * Copyright 2025 M. Fender
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

namespace Tinfour.Core.Tests.Refinement;

using System.Globalization;

using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Tinfour.Core.Tests.Helpers;
using Tinfour.Core.Utils;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Diagnostic tests that reproduce the Ruppert refinement escape bug using
///     real trail data converted to UTM. These tests confirm the bug EXISTS by
///     asserting leakedCount > 0. After Phase 2 fixes, flip to Assert.Equal(0, ...).
/// </summary>
public class RuppertEscapeDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public RuppertEscapeDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     DIAG-03: Rectangle constraint with trail data should detect Steiner escape.
    /// </summary>
    [Fact]
    public void RectangleConstraint_WithTrailData_ShouldDetectEscape()
    {
        // 1. Load trail data and convert to UTM
        var vertices = LoadTrailDataAsUtm();
        Assert.True(vertices.Count > 0, "Trail data should contain vertices");

        // 2. Compute bounds
        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);

        // 3. Compute nominal point spacing
        var nominalSpacing = Math.Sqrt((maxX - minX) * (maxY - minY) / vertices.Count);

        // 4. Create TIN and add vertices
        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(vertices);

        // 5. Create bounding rectangle with 1% buffer
        var bufferX = (maxX - minX) * 0.01;
        var bufferY = (maxY - minY) * 0.01;
        var rectVertices = new List<Vertex>
        {
            new Vertex(minX - bufferX, minY - bufferY, double.NaN, 900_000),
            new Vertex(maxX + bufferX, minY - bufferY, double.NaN, 900_001),
            new Vertex(maxX + bufferX, maxY + bufferY, double.NaN, 900_002),
            new Vertex(minX - bufferX, maxY + bufferY, double.NaN, 900_003)
        };
        var rect = new PolygonConstraint(rectVertices);

        // 6. Add constraint
        tin.AddConstraints(new IConstraint[] { rect }, true);

        // 7. Run refinement
        var options = new RuppertOptions(25.0) { MaxIterations = 5000 };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();
        _output.WriteLine($"Refinement completed: success={success}");

        // 8. Run leak detection
        var report = ConstraintLeakDetector.Detect(tin, rect);

        // 9. Log statistics
        LogMeshStatistics(tin, report);

        // 10. Assert escape bug is reproduced (leakedCount > 0)
        Assert.True(report.LeakedCount > 0,
            "Expected escape bug to produce leaked Steiner points with rectangle constraint");
    }

    /// <summary>
    ///     DIAG-04: Convex hull constraint with trail data should detect Steiner escape.
    /// </summary>
    [Fact]
    public void ConvexHullConstraint_WithTrailData_ShouldDetectEscape()
    {
        // 1. Load trail data and convert to UTM
        var vertices = LoadTrailDataAsUtm();
        Assert.True(vertices.Count > 0, "Trail data should contain vertices");

        // 2. Compute nominal point spacing
        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);
        var nominalSpacing = Math.Sqrt((maxX - minX) * (maxY - minY) / vertices.Count);

        // 3. Create TIN and add vertices
        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(vertices);

        // 4. Extract convex hull BEFORE adding constraints using GetPerimeter()
        var perimeter = tin.GetPerimeter();
        _output.WriteLine($"TIN bootstrapped: {tin.IsBootstrapped()}, perimeter edges: {perimeter.Count}");

        // Build hull vertices from perimeter edges
        var hullVertices = new List<Vertex>();
        var hullIndex = 1_000_000;
        foreach (var edge in perimeter)
        {
            var a = edge.GetA();
            if (!a.IsNullVertex())
                hullVertices.Add(new Vertex(a.X, a.Y, a.GetZ(), hullIndex++));
        }
        _output.WriteLine($"Hull vertices: {hullVertices.Count}");
        Assert.True(hullVertices.Count >= 3, $"Expected at least 3 hull vertices, got {hullVertices.Count}");

        var hullConstraint = new PolygonConstraint(hullVertices);

        // 5. Add the hull constraint
        tin.AddConstraints(new IConstraint[] { hullConstraint }, true);

        // 6. Run refinement
        var options = new RuppertOptions(25.0) { MaxIterations = 5000 };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();
        _output.WriteLine($"Refinement completed: success={success}");

        // 7. Run leak detection
        var report = ConstraintLeakDetector.Detect(tin, hullConstraint);

        // 8. Log statistics
        LogMeshStatistics(tin, report);

        // 9. Assert escape bug is reproduced (leakedCount > 0)
        Assert.True(report.LeakedCount > 0,
            "Expected escape bug to produce leaked Steiner points with convex hull constraint");
    }

    /// <summary>
    ///     Loads trail CSV data and converts each point from WGS84 to UTM coordinates.
    /// </summary>
    private static List<Vertex> LoadTrailDataAsUtm()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "RuppertsTestData", "TestTrail.csv");
        Assert.True(File.Exists(csvPath), $"Trail data file not found at: {csvPath}");

        var lines = File.ReadAllLines(csvPath);
        var vertices = new List<Vertex>();
        var index = 0;

        // Skip header line
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 3)
                continue;

            var latitude = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var longitude = double.Parse(parts[1], CultureInfo.InvariantCulture);
            var depth = double.Parse(parts[2], CultureInfo.InvariantCulture);

            var (x, y, _) = UtmConverter.LatLonToUtm(latitude, longitude);
            vertices.Add(new Vertex(x, y, depth, index++));
        }

        return vertices;
    }

    /// <summary>
    ///     Logs mesh statistics including vertex count, triangle count, leaked points,
    ///     divergences, and minimum angle.
    /// </summary>
    private void LogMeshStatistics(IIncrementalTin tin, ConstraintLeakDetector.LeakReport report)
    {
        var triangleCount = tin.CountTriangles();

        _output.WriteLine("=== Mesh Statistics ===");
        _output.WriteLine($"  Total vertices:       {tin.GetVertices().Count}");
        _output.WriteLine($"  Steiner points:       {report.TotalSteinerPoints}");
        _output.WriteLine($"  Valid triangles:      {triangleCount.ValidTriangles}");
        _output.WriteLine($"  Leaked Steiner count: {report.LeakedCount}");
        _output.WriteLine($"  Divergence count:     {report.Divergences.Count}");

        // Log first 10 leaked point coordinates
        var leakedToLog = report.LeakedPoints.Take(10).ToList();
        if (leakedToLog.Count > 0)
        {
            _output.WriteLine($"  First {leakedToLog.Count} leaked points:");
            foreach (var lp in leakedToLog)
            {
                _output.WriteLine($"    Vertex[{lp.VertexIndex}]: ({lp.X:F2}, {lp.Y:F2})");
            }
            if (report.LeakedCount > 10)
                _output.WriteLine($"    ... and {report.LeakedCount - 10} more");
        }

        // Log first 10 divergences
        var divergencesToLog = report.Divergences.Take(10).ToList();
        if (divergencesToLog.Count > 0)
        {
            _output.WriteLine($"  First {divergencesToLog.Count} divergences:");
            foreach (var dp in divergencesToLog)
            {
                _output.WriteLine(
                    $"    Vertex[{dp.VertexIndex}]: ({dp.X:F2}, {dp.Y:F2}) " +
                    $"geometry={dp.GeometryInside}, flags={dp.FlagStateInside}");
            }
            if (report.Divergences.Count > 10)
                _output.WriteLine($"    ... and {report.Divergences.Count - 10} more");
        }

        // Compute minimum angle across all non-ghost triangles
        var minAngle = double.MaxValue;
        foreach (var triangle in tin.GetTriangles())
        {
            if (triangle.IsGhost())
                continue;

            var area = triangle.GetArea();
            if (Math.Abs(area) < 1e-20)
                continue; // skip degenerate triangles

            var vA = triangle.GetVertexA();
            var vB = triangle.GetVertexB();
            var vC = triangle.GetVertexC();

            // Compute all three angles using law of cosines
            var ax = vA.X; var ay = vA.Y;
            var bx = vB.X; var by = vB.Y;
            var cx = vC.X; var cy = vC.Y;

            var abSq = (bx - ax) * (bx - ax) + (by - ay) * (by - ay);
            var bcSq = (cx - bx) * (cx - bx) + (cy - by) * (cy - by);
            var caSq = (ax - cx) * (ax - cx) + (ay - cy) * (ay - cy);

            var ab = Math.Sqrt(abSq);
            var bc = Math.Sqrt(bcSq);
            var ca = Math.Sqrt(caSq);

            if (ab < 1e-20 || bc < 1e-20 || ca < 1e-20)
                continue; // degenerate edge

            // Angle at A (opposite side bc)
            var cosA = (abSq + caSq - bcSq) / (2 * ab * ca);
            cosA = Math.Clamp(cosA, -1.0, 1.0);
            var angleA = Math.Acos(cosA) * 180.0 / Math.PI;

            // Angle at B (opposite side ca)
            var cosB = (abSq + bcSq - caSq) / (2 * ab * bc);
            cosB = Math.Clamp(cosB, -1.0, 1.0);
            var angleB = Math.Acos(cosB) * 180.0 / Math.PI;

            // Angle at C (opposite side ab)
            var cosC = (bcSq + caSq - abSq) / (2 * bc * ca);
            cosC = Math.Clamp(cosC, -1.0, 1.0);
            var angleC = Math.Acos(cosC) * 180.0 / Math.PI;

            var triMin = Math.Min(angleA, Math.Min(angleB, angleC));
            if (triMin < minAngle)
                minAngle = triMin;
        }

        _output.WriteLine($"  Minimum angle:        {minAngle:F2} degrees");
        _output.WriteLine("========================");
    }
}
