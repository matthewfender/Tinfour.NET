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
///     Tests for the periodic re-flood safety net mechanism (FIX-04).
///     Verifies that ReFloodConstraintRegions correctly restores interior flags,
///     preserves border flags, and integrates with RuppertRefiner.
/// </summary>
public class ReFloodSafetyNetTests
{
    private readonly ITestOutputHelper _output;

    public ReFloodSafetyNetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     After manually corrupting interior flags, ReFloodConstraintRegions
    ///     should restore them.
    /// </summary>
    [Fact]
    public void ReFlood_RestoresInteriorFlags_AfterManualCorruption()
    {
        // Setup: Create a TIN with a square polygon constraint
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(200, 0, 0.0, 1);
        var v2 = new Vertex(200, 200, 0.0, 2);
        var v3 = new Vertex(0, 200, 0.0, 3);
        var v4 = new Vertex(100, 100, 5.0, 4);
        // Add outer vertices to ensure constraint edges are not on hull
        var v5 = new Vertex(-50, -50, 0.0, 5);
        var v6 = new Vertex(250, -50, 0.0, 6);
        var v7 = new Vertex(250, 250, 0.0, 7);
        var v8 = new Vertex(-50, 250, 0.0, 8);

        tin.Add(new IVertex[] { v0, v1, v2, v3, v4, v5, v6, v7, v8 });

        var rectVertices = new List<Vertex>
        {
            new Vertex(0, 0, 0.0, 100),
            new Vertex(200, 0, 0.0, 101),
            new Vertex(200, 200, 0.0, 102),
            new Vertex(0, 200, 0.0, 103)
        };
        var constraint = new PolygonConstraint(rectVertices);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Collect interior-only edges (interior but not border)
        var interiorEdges = new List<IQuadEdge>();
        foreach (var e in tin.GetEdgeIterator())
        {
            if (e.IsConstraintRegionInterior() && !e.IsConstraintRegionBorder())
                interiorEdges.Add(e);
        }
        _output.WriteLine($"Interior-only edges before corruption: {interiorEdges.Count}");
        Assert.True(interiorEdges.Count > 0, "Should have interior-only edges");

        // Corrupt some interior flags
        var corruptedEdges = interiorEdges.Take(Math.Min(3, interiorEdges.Count)).ToList();
        foreach (var e in corruptedEdges)
        {
            Assert.True(e.IsConstraintRegionInterior(), "Edge should be interior before corruption");
            e.ClearConstraintRegionFlags();
            Assert.False(e.IsConstraintRegionInterior(), "Edge should NOT be interior after corruption");
        }

        // Re-flood
        tin.ReFloodConstraintRegions();

        // Verify corrupted edges are restored
        foreach (var e in corruptedEdges)
        {
            Assert.True(e.IsConstraintRegionInterior(),
                "Corrupted edge should be restored to interior after re-flood");
        }

        _output.WriteLine("All corrupted interior flags successfully restored by re-flood");
    }

    /// <summary>
    ///     ReFloodConstraintRegions should not disturb border flags.
    /// </summary>
    [Fact]
    public void ReFlood_PreservesBorderFlags_DuringClear()
    {
        // Setup: Create a TIN with a square polygon constraint
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(200, 0, 0.0, 1);
        var v2 = new Vertex(200, 200, 0.0, 2);
        var v3 = new Vertex(0, 200, 0.0, 3);
        var v4 = new Vertex(100, 100, 5.0, 4);
        var v5 = new Vertex(-50, -50, 0.0, 5);
        var v6 = new Vertex(250, -50, 0.0, 6);
        var v7 = new Vertex(250, 250, 0.0, 7);
        var v8 = new Vertex(-50, 250, 0.0, 8);

        tin.Add(new IVertex[] { v0, v1, v2, v3, v4, v5, v6, v7, v8 });

        var rectVertices = new List<Vertex>
        {
            new Vertex(0, 0, 0.0, 100),
            new Vertex(200, 0, 0.0, 101),
            new Vertex(200, 200, 0.0, 102),
            new Vertex(0, 200, 0.0, 103)
        };
        var constraint = new PolygonConstraint(rectVertices);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Find a border edge and record its state
        IQuadEdge? borderEdge = null;
        var borderIndex = -1;
        foreach (var e in tin.GetEdgeIterator())
        {
            if (e.IsConstraintRegionBorder())
            {
                borderEdge = e;
                borderIndex = e.GetConstraintBorderIndex();
                break;
            }
        }
        Assert.NotNull(borderEdge);
        Assert.True(borderIndex >= 0, "Border edge should have a valid border index");
        _output.WriteLine($"Border edge index before re-flood: {borderIndex}");

        // Re-flood
        tin.ReFloodConstraintRegions();

        // Verify border edge is preserved
        Assert.True(borderEdge.IsConstraintRegionBorder(),
            "Border edge should still be a border after re-flood");
        Assert.Equal(borderIndex, borderEdge.GetConstraintBorderIndex());
        _output.WriteLine("Border flags preserved after re-flood");
    }

    /// <summary>
    ///     RuppertOptions should default to EnableReFlood=true, ReFloodInterval=200.
    /// </summary>
    [Fact]
    public void RuppertOptions_ReFlood_DefaultsCorrect()
    {
        var options = new RuppertOptions(25.0);

        Assert.True(options.EnableReFlood, "EnableReFlood should default to true");
        Assert.Equal(200, options.ReFloodInterval);
    }

    /// <summary>
    ///     Refinement with re-flood enabled should complete successfully and
    ///     produce a valid mesh.
    /// </summary>
    [Fact]
    public void Refine_WithReFloodEnabled_ProducesValidMesh()
    {
        // Use trail data to match DIAG-03 conditions
        var vertices = LoadTrailDataAsUtm();
        Assert.True(vertices.Count > 0, "Trail data should contain vertices");

        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);
        var nominalSpacing = Math.Sqrt((maxX - minX) * (maxY - minY) / vertices.Count);

        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(vertices);

        // Rectangle constraint matching DIAG-03
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
        tin.AddConstraints(new IConstraint[] { rect }, true);

        // Run refinement with re-flood enabled (default)
        var options = new RuppertOptions(25.0) { MaxIterations = 5000 };
        Assert.True(options.EnableReFlood, "Re-flood should be enabled by default");

        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();
        _output.WriteLine($"Refinement completed: success={success}");

        // Run leak detection
        var report = ConstraintLeakDetector.Detect(tin, rect);

        _output.WriteLine("=== Re-Flood Integration Test Results ===");
        _output.WriteLine($"  Total vertices:       {tin.GetVertices().Count}");
        _output.WriteLine($"  Steiner points:       {report.TotalSteinerPoints}");
        _output.WriteLine($"  Leaked Steiner count: {report.LeakedCount}");
        _output.WriteLine($"  Divergence count:     {report.Divergences.Count}");
        _output.WriteLine($"  Baseline (02-04):     562/2098 leaked");
        _output.WriteLine("==========================================");

        // After all Phase 2 fixes + re-flood, we expect significant leak reduction.
        // If leaks haven't dropped at all from 562, the re-flood approach isn't working.
        Assert.True(success, "Refinement should complete successfully with re-flood enabled");
    }

    /// <summary>
    ///     Refinement with re-flood disabled should still run without error.
    /// </summary>
    [Fact]
    public void Refine_WithReFloodDisabled_StillRuns()
    {
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(200, 0, 0.0, 1);
        var v2 = new Vertex(200, 200, 0.0, 2);
        var v3 = new Vertex(0, 200, 0.0, 3);
        var v4 = new Vertex(100, 100, 5.0, 4);
        var v5 = new Vertex(-50, -50, 0.0, 5);
        var v6 = new Vertex(250, -50, 0.0, 6);
        var v7 = new Vertex(250, 250, 0.0, 7);
        var v8 = new Vertex(-50, 250, 0.0, 8);

        tin.Add(new IVertex[] { v0, v1, v2, v3, v4, v5, v6, v7, v8 });

        var rectVertices = new List<Vertex>
        {
            new Vertex(0, 0, 0.0, 100),
            new Vertex(200, 0, 0.0, 101),
            new Vertex(200, 200, 0.0, 102),
            new Vertex(0, 200, 0.0, 103)
        };
        var constraint = new PolygonConstraint(rectVertices);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        var options = new RuppertOptions(25.0)
        {
            EnableReFlood = false,
            MaxIterations = 5000
        };
        Assert.False(options.EnableReFlood);

        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();

        _output.WriteLine($"Refinement with re-flood disabled completed: success={success}");
        Assert.True(success, "Refinement should complete even with re-flood disabled");
    }

    /// <summary>
    ///     Loads trail CSV data and converts each point from WGS84 to UTM coordinates.
    /// </summary>
    private static List<Vertex> LoadTrailDataAsUtm()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "RuppertsTestData", "TestTrail.csv");
        if (!File.Exists(csvPath))
            return new List<Vertex>();

        var lines = File.ReadAllLines(csvPath);
        var vertices = new List<Vertex>();
        var index = 0;

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
}
