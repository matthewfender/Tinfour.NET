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

using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests for constraint region leakage during Ruppert refinement.
/// </summary>
public class RuppertConstraintLeakageTest
{
    private readonly ITestOutputHelper _output;

    public RuppertConstraintLeakageTest(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Creates a donut (annulus) constraint with specified inner and outer radii.
    ///     Returns both the outer constraint and the hole constraint.
    /// </summary>
    private static (PolygonConstraint outer, PolygonConstraint hole) CreateDonutConstraints(
        double centerX, double centerY,
        double outerRadius, double innerRadius,
        int numPoints)
    {
        var outerRing = CreateCircleVertices(centerX, centerY, outerRadius, numPoints, false);
        var innerRing = CreateCircleVertices(centerX, centerY, innerRadius, numPoints, true);

        var outer = new PolygonConstraint(outerRing, definesRegion: true, isHole: false);
        var hole = new PolygonConstraint(innerRing, definesRegion: true, isHole: true);
        return (outer, hole);
    }

    /// <summary>
    ///     Creates circle vertices. CCW for outer ring, CW for holes.
    /// </summary>
    private static List<IVertex> CreateCircleVertices(
        double cx, double cy, double radius, int n, bool clockwise)
    {
        var vertices = new List<IVertex>(n);
        for (var i = 0; i < n; i++)
        {
            var angle = 2.0 * Math.PI * i / n;
            if (clockwise) angle = -angle;
            var x = cx + radius * Math.Cos(angle);
            var y = cy + radius * Math.Sin(angle);
            vertices.Add(new Vertex(x, y, 0, i));
        }
        return vertices;
    }

    /// <summary>
    ///     Checks if a point is inside the donut (between inner and outer circles).
    /// </summary>
    private static bool IsInsideDonut(double x, double y, double cx, double cy,
        double outerRadius, double innerRadius)
    {
        var dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        return dist <= outerRadius && dist >= innerRadius;
    }

    [Fact]
    public void DonutConstraint_AfterRefinement_ShouldNotLeakOutside()
    {
        // Create a TIN with corner vertices
        var tin = new IncrementalTin(1.0);
        var corners = new IVertex[]
        {
            new Vertex(-50, -50, 0),
            new Vertex(50, -50, 0),
            new Vertex(50, 50, 0),
            new Vertex(-50, 50, 0)
        };
        tin.Add(corners);

        // Create donut constraint centered at origin
        const double outerRadius = 30.0;
        const double innerRadius = 15.0;
        const int numPoints = 32;

        var (outer, hole) = CreateDonutConstraints(0, 0, outerRadius, innerRadius, numPoints);

        // Add constraint vertices to TIN first
        foreach (var v in outer.GetVertices())
            tin.Add(v);
        foreach (var v in hole.GetVertices())
            tin.Add(v);

        // Add the constraints
        tin.AddConstraints(new IConstraint[] { outer, hole }, true);

        _output.WriteLine("=== BEFORE REFINEMENT ===");
        AnalyzeConstraintLeakage(tin, 0, 0, outerRadius, innerRadius);

        // Apply Ruppert refinement
        var options = new RuppertOptions(30.0) // 30 degree minimum angle
        {
            MaxIterations = 500,
            MinimumTriangleArea = 0.1
        };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();

        _output.WriteLine($"\n=== AFTER REFINEMENT (success={success}) ===");
        var (leakedEdges, totalInteriorEdges) = AnalyzeConstraintLeakage(tin, 0, 0, outerRadius, innerRadius);

        _output.WriteLine($"\nLeaked edges: {leakedEdges} out of {totalInteriorEdges} interior edges");

        // The test: no edges outside the donut should be marked as interior
        Assert.Equal(0, leakedEdges);
    }

    [Fact]
    public void SimpleCircle_AfterRefinement_ShouldNotLeakOutside()
    {
        // Simpler test with just a circle (no hole)
        var tin = new IncrementalTin(1.0);
        var corners = new IVertex[]
        {
            new Vertex(-50, -50, 0),
            new Vertex(50, -50, 0),
            new Vertex(50, 50, 0),
            new Vertex(-50, 50, 0)
        };
        tin.Add(corners);

        // Create simple circle constraint
        const double radius = 25.0;
        const int numPoints = 24;
        var circleVertices = CreateCircleVertices(0, 0, radius, numPoints, false);
        var circle = new PolygonConstraint(circleVertices);

        // Add constraint vertices to TIN first
        foreach (var v in circleVertices)
            tin.Add(v);

        // Add the constraint
        tin.AddConstraints(new[] { circle }, true);

        _output.WriteLine("=== BEFORE REFINEMENT ===");
        AnalyzeCircleLeakage(tin, 0, 0, radius);

        // Apply Ruppert refinement
        var options = new RuppertOptions(30.0)
        {
            MaxIterations = 300,
            MinimumTriangleArea = 0.1
        };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();

        _output.WriteLine($"\n=== AFTER REFINEMENT (success={success}) ===");
        var (leakedEdges, totalInteriorEdges) = AnalyzeCircleLeakage(tin, 0, 0, radius);

        _output.WriteLine($"\nLeaked edges: {leakedEdges} out of {totalInteriorEdges} interior edges");

        Assert.Equal(0, leakedEdges);
    }

    [Fact]
    public void SimpleCircle_DetailedLeakageAnalysis()
    {
        // Detailed test to understand exactly where leakage occurs
        var tin = new IncrementalTin(1.0);
        var corners = new IVertex[]
        {
            new Vertex(-50, -50, 0),
            new Vertex(50, -50, 0),
            new Vertex(50, 50, 0),
            new Vertex(-50, 50, 0)
        };
        tin.Add(corners);

        // Create simple circle constraint
        const double radius = 25.0;
        const int numPoints = 16; // Fewer points for easier analysis
        var circleVertices = CreateCircleVertices(0, 0, radius, numPoints, false);
        var circle = new PolygonConstraint(circleVertices);

        // Add constraint vertices to TIN first
        foreach (var v in circleVertices)
            tin.Add(v);

        // Add the constraint
        tin.AddConstraints(new[] { circle }, true);

        _output.WriteLine("=== BEFORE REFINEMENT ===");
        AnalyzeAllEdges(tin, 0, 0, radius);

        // Apply Ruppert refinement with MORE iterations to stress test
        var options = new RuppertOptions(30.0)
        {
            MaxIterations = 500,
            MinimumTriangleArea = 0.1
        };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();

        _output.WriteLine($"\n=== AFTER REFINEMENT (success={success}) ===");
        var (leakedOut, missingIn) = AnalyzeAllEdges(tin, 0, 0, radius);

        _output.WriteLine($"\nSummary:");
        _output.WriteLine($"  Edges marked interior but OUTSIDE circle: {leakedOut}");
        _output.WriteLine($"  Edges NOT marked interior but INSIDE circle: {missingIn}");

        Assert.Equal(0, leakedOut);
    }

    [Fact]
    public void Donut_DetailedLeakageAnalysis()
    {
        // Detailed test with donut (annulus) constraint
        var tin = new IncrementalTin(1.0);
        var corners = new IVertex[]
        {
            new Vertex(-50, -50, 0),
            new Vertex(50, -50, 0),
            new Vertex(50, 50, 0),
            new Vertex(-50, 50, 0)
        };
        tin.Add(corners);

        const double outerRadius = 30.0;
        const double innerRadius = 15.0;
        const int numPoints = 32;

        var (outer, hole) = CreateDonutConstraints(0, 0, outerRadius, innerRadius, numPoints);

        // Add constraint vertices to TIN first
        foreach (var v in outer.GetVertices())
            tin.Add(v);
        foreach (var v in hole.GetVertices())
            tin.Add(v);

        // Add the constraints
        tin.AddConstraints(new IConstraint[] { outer, hole }, true);

        _output.WriteLine("=== BEFORE REFINEMENT ===");
        AnalyzeDonutEdges(tin, 0, 0, outerRadius, innerRadius);

        // Apply Ruppert refinement
        var options = new RuppertOptions(30.0)
        {
            MaxIterations = 500,
            MinimumTriangleArea = 0.1
        };
        var refiner = new RuppertRefiner(tin, options);
        var success = refiner.Refine();

        _output.WriteLine($"\n=== AFTER REFINEMENT (success={success}) ===");
        var (leakedOut, leakedIn, missingIn) = AnalyzeDonutEdges(tin, 0, 0, outerRadius, innerRadius);

        _output.WriteLine($"\nSummary:");
        _output.WriteLine($"  Edges marked interior but OUTSIDE outer circle: {leakedOut}");
        _output.WriteLine($"  Edges marked interior but INSIDE inner circle (hole): {leakedIn}");
        _output.WriteLine($"  Edges NOT marked interior but INSIDE donut: {missingIn}");

        Assert.Equal(0, leakedOut);
        Assert.Equal(0, leakedIn);
    }

    private (int leakedOut, int leakedIn, int missingIn) AnalyzeDonutEdges(
        IIncrementalTin tin, double cx, double cy, double outerR, double innerR)
    {
        var allEdges = tin.GetEdges().Where(e => !e.GetB().IsNullVertex()).ToList();
        var interiorEdges = allEdges.Where(e => e.IsConstraintRegionInterior()).ToList();
        var borderEdges = allEdges.Where(e => e.IsConstraintRegionBorder()).ToList();
        var unmarkedEdges = allEdges.Where(e => !e.IsConstraintRegionMember() && !e.IsConstrained()).ToList();

        _output.WriteLine($"Total edges: {allEdges.Count}");
        _output.WriteLine($"Border edges: {borderEdges.Count}");
        _output.WriteLine($"Interior edges: {interiorEdges.Count}");
        _output.WriteLine($"Unmarked edges: {unmarkedEdges.Count}");

        // Check for edges marked as interior but outside the donut
        var leakedOut = 0;
        var leakedIn = 0;
        _output.WriteLine($"\n--- Interior edges OUTSIDE donut (leakage) ---");
        foreach (var edge in interiorEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            var dist = Math.Sqrt(midX * midX + midY * midY);

            if (dist > outerR)
            {
                leakedOut++;
                _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid dist={dist:F1} OUTSIDE OUTER");

                var forward = edge.GetForward();
                var dualForward = edge.GetDual().GetForward();
                _output.WriteLine($"    Forward: border={forward.IsConstraintRegionBorder()}, interior={forward.IsConstraintRegionInterior()}, borderIdx={forward.GetConstraintBorderIndex()}");
                _output.WriteLine($"    DualForward: border={dualForward.IsConstraintRegionBorder()}, interior={dualForward.IsConstraintRegionInterior()}, borderIdx={dualForward.GetConstraintBorderIndex()}");
            }
            else if (dist < innerR)
            {
                leakedIn++;
                _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid dist={dist:F1} INSIDE HOLE");
            }
        }
        if (leakedOut == 0 && leakedIn == 0) _output.WriteLine("  (none)");

        // Check for edges inside the donut but not marked as interior
        var missingIn = 0;
        _output.WriteLine($"\n--- Unmarked edges INSIDE donut (missing interior) ---");
        foreach (var edge in unmarkedEdges.Take(20)) // Limit output
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            var dist = Math.Sqrt(midX * midX + midY * midY);

            if (dist < outerR * 0.95 && dist > innerR * 1.05) // Clearly inside donut
            {
                missingIn++;
                _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid dist={dist:F1}");
            }
        }
        if (missingIn == 0) _output.WriteLine("  (none)");

        return (leakedOut, leakedIn, missingIn);
    }

    private (int leakedOut, int missingIn) AnalyzeAllEdges(IIncrementalTin tin, double cx, double cy, double radius)
    {
        var allEdges = tin.GetEdges().Where(e => !e.GetB().IsNullVertex()).ToList();
        var interiorEdges = allEdges.Where(e => e.IsConstraintRegionInterior()).ToList();
        var borderEdges = allEdges.Where(e => e.IsConstraintRegionBorder()).ToList();
        var unmarkedEdges = allEdges.Where(e => !e.IsConstraintRegionMember() && !e.IsConstrained()).ToList();

        _output.WriteLine($"Total edges: {allEdges.Count}");
        _output.WriteLine($"Border edges: {borderEdges.Count}");
        _output.WriteLine($"Interior edges: {interiorEdges.Count}");
        _output.WriteLine($"Unmarked edges: {unmarkedEdges.Count}");

        // Check for edges marked as interior but outside the circle
        var leakedOut = 0;
        _output.WriteLine($"\n--- Interior edges OUTSIDE circle (leakage) ---");
        foreach (var edge in interiorEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            var dist = Math.Sqrt(midX * midX + midY * midY);

            if (dist > radius)
            {
                leakedOut++;
                // Get information about this edge's neighbors
                var forward = edge.GetForward();
                var reverse = edge.GetReverse();
                var dual = edge.GetDual();
                var dualForward = dual.GetForward();

                _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid dist={dist:F1}");
                _output.WriteLine($"    Forward edge {forward.GetIndex()}: border={forward.IsConstraintRegionBorder()}, interior={forward.IsConstraintRegionInterior()}");
                _output.WriteLine($"    Reverse edge {reverse.GetIndex()}: border={reverse.IsConstraintRegionBorder()}, interior={reverse.IsConstraintRegionInterior()}");
                _output.WriteLine($"    DualForward edge {dualForward.GetIndex()}: border={dualForward.IsConstraintRegionBorder()}, interior={dualForward.IsConstraintRegionInterior()}");

                // Check if either endpoint is on the border
                var aOnBorder = Math.Abs(Math.Sqrt(a.X * a.X + a.Y * a.Y) - radius) < 0.1;
                var bOnBorder = Math.Abs(Math.Sqrt(b.X * b.X + b.Y * b.Y) - radius) < 0.1;
                _output.WriteLine($"    A on border: {aOnBorder}, B on border: {bOnBorder}");
            }
        }
        if (leakedOut == 0) _output.WriteLine("  (none)");

        // Check for edges inside the circle but not marked as interior
        var missingIn = 0;
        _output.WriteLine($"\n--- Unmarked edges INSIDE circle (missing interior) ---");
        foreach (var edge in unmarkedEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;
            var dist = Math.Sqrt(midX * midX + midY * midY);

            // Check if both endpoints are inside the circle
            var aDist = Math.Sqrt(a.X * a.X + a.Y * a.Y);
            var bDist = Math.Sqrt(b.X * b.X + b.Y * b.Y);

            if (aDist < radius && bDist < radius && dist < radius * 0.95) // Clearly inside
            {
                missingIn++;
                _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid dist={dist:F1}");

                var forward = edge.GetForward();
                var dualForward = edge.GetDual().GetForward();
                _output.WriteLine($"    Forward: border={forward.IsConstraintRegionBorder()}, interior={forward.IsConstraintRegionInterior()}");
                _output.WriteLine($"    DualForward: border={dualForward.IsConstraintRegionBorder()}, interior={dualForward.IsConstraintRegionInterior()}");
            }
        }
        if (missingIn == 0) _output.WriteLine("  (none)");

        // Show border edges for context
        _output.WriteLine($"\n--- Border edges ---");
        foreach (var edge in borderEdges.Take(10))
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var borderIdx = edge.GetConstraintBorderIndex();
            _output.WriteLine($"  Edge {edge.GetIndex()}: ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) borderIdx={borderIdx}");
        }
        if (borderEdges.Count > 10) _output.WriteLine($"  ... and {borderEdges.Count - 10} more");

        return (leakedOut, missingIn);
    }

    private (int leaked, int total) AnalyzeConstraintLeakage(
        IIncrementalTin tin, double cx, double cy, double outerR, double innerR)
    {
        var allEdges = tin.GetEdges().Where(e => !e.GetB().IsNullVertex()).ToList();
        var interiorEdges = allEdges.Where(e => e.IsConstraintRegionInterior()).ToList();
        var borderEdges = allEdges.Where(e => e.IsConstraintRegionBorder()).ToList();

        _output.WriteLine($"Total edges: {allEdges.Count}");
        _output.WriteLine($"Border edges: {borderEdges.Count}");
        _output.WriteLine($"Interior edges: {interiorEdges.Count}");

        var leaked = 0;
        var leakedDetails = new List<string>();

        foreach (var edge in interiorEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;

            if (!IsInsideDonut(midX, midY, cx, cy, outerR, innerR))
            {
                leaked++;
                var dist = Math.Sqrt(midX * midX + midY * midY);
                var location = dist > outerR ? "OUTSIDE outer" : "INSIDE inner";
                leakedDetails.Add($"  ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid=({midX:F1},{midY:F1}) dist={dist:F1} {location}");
            }
        }

        if (leaked > 0)
        {
            _output.WriteLine($"\nLeaked interior edges ({leaked}):");
            foreach (var detail in leakedDetails.Take(20))
                _output.WriteLine(detail);
            if (leakedDetails.Count > 20)
                _output.WriteLine($"  ... and {leakedDetails.Count - 20} more");
        }

        return (leaked, interiorEdges.Count);
    }

    private (int leaked, int total) AnalyzeCircleLeakage(
        IIncrementalTin tin, double cx, double cy, double radius)
    {
        var allEdges = tin.GetEdges().Where(e => !e.GetB().IsNullVertex()).ToList();
        var interiorEdges = allEdges.Where(e => e.IsConstraintRegionInterior()).ToList();
        var borderEdges = allEdges.Where(e => e.IsConstraintRegionBorder()).ToList();

        _output.WriteLine($"Total edges: {allEdges.Count}");
        _output.WriteLine($"Border edges: {borderEdges.Count}");
        _output.WriteLine($"Interior edges: {interiorEdges.Count}");

        var leaked = 0;
        var leakedDetails = new List<string>();

        foreach (var edge in interiorEdges)
        {
            var a = edge.GetA();
            var b = edge.GetB();
            var midX = (a.X + b.X) / 2;
            var midY = (a.Y + b.Y) / 2;

            var dist = Math.Sqrt((midX - cx) * (midX - cx) + (midY - cy) * (midY - cy));
            if (dist > radius)
            {
                leaked++;
                leakedDetails.Add($"  ({a.X:F1},{a.Y:F1})->({b.X:F1},{b.Y:F1}) mid=({midX:F1},{midY:F1}) dist={dist:F1} OUTSIDE");
            }
        }

        if (leaked > 0)
        {
            _output.WriteLine($"\nLeaked interior edges ({leaked}):");
            foreach (var detail in leakedDetails.Take(20))
                _output.WriteLine(detail);
            if (leakedDetails.Count > 20)
                _output.WriteLine($"  ... and {leakedDetails.Count - 20} more");
        }

        return (leaked, interiorEdges.Count);
    }
}
