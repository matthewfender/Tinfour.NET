using System.Globalization;
using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;
using Tinfour.Core.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Tinfour.Core.Tests.Refinement;

public class EscapeRootCauseTest
{
    private readonly ITestOutputHelper _output;

    public EscapeRootCauseTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TraceEscapeMechanism_StepByStep()
    {
        var vertices = LoadTrailDataAsUtm();
        var (minX, maxX, minY, maxY) = GetBounds(vertices);
        var nominalSpacing = Math.Sqrt((maxX - minX) * (maxY - minY) / vertices.Count);

        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(vertices.Cast<IVertex>().ToList());

        // Create rectangle constraint
        var buffer = (maxX - minX) * 0.01;
        var rect = new PolygonConstraint(new List<IVertex>
        {
            new Vertex(minX - buffer, minY - buffer, double.NaN),
            new Vertex(maxX + buffer, minY - buffer, double.NaN),
            new Vertex(maxX + buffer, maxY + buffer, double.NaN),
            new Vertex(minX - buffer, maxY + buffer, double.NaN)
        });
        tin.AddConstraints(new IConstraint[] { rect }, true);

        // Get constraint vertices for PIP testing
        var constraintVerts = rect.GetVertices();

        // === CHECK FLAG INTEGRITY IMMEDIATELY AFTER AddConstraints ===
        _output.WriteLine("=== PRE-REFINEMENT FLAG INTEGRITY CHECK ===");
        var preReport = ConstraintLeakDetector.Detect(tin, rect);
        _output.WriteLine($"BEFORE refinement: {preReport.LeakedCount} leaked Steiner, {preReport.Divergences.Count} divergences");

        // Check ALL edges (not just Steiner) for flag vs geometry consistency
        int totalEdgesChecked = 0;
        int exteriorWithInteriorFlag = 0;
        int interiorWithNoFlag = 0;
        foreach (var edge in tin.GetEdges())
        {
            if (edge.GetA().IsNullVertex() || edge.GetB().IsNullVertex()) continue;
            totalEdgesChecked++;

            var midX = (edge.GetA().X + edge.GetB().X) / 2.0;
            var midY = (edge.GetA().Y + edge.GetB().Y) / 2.0;
            var pip = Polyside.IsPointInPolygon(constraintVerts, midX, midY);

            if (pip == Polyside.Result.Outside && edge.IsConstraintRegionInterior())
            {
                exteriorWithInteriorFlag++;
                if (exteriorWithInteriorFlag <= 5)
                    _output.WriteLine($"  STALE FLAG pre-refinement: edge midpoint ({midX:F2},{midY:F2}) is OUTSIDE but flagged interior");
            }
            else if (pip == Polyside.Result.Inside && !edge.IsConstraintRegionMember())
            {
                interiorWithNoFlag++;
                if (interiorWithNoFlag <= 5)
                    _output.WriteLine($"  MISSING FLAG pre-refinement: edge midpoint ({midX:F2},{midY:F2}) is INSIDE but not flagged");
            }
        }
        _output.WriteLine($"Pre-refinement edges checked: {totalEdgesChecked}");
        _output.WriteLine($"  Exterior with interior flag: {exteriorWithInteriorFlag}");
        _output.WriteLine($"  Interior with no flag: {interiorWithNoFlag}");
        _output.WriteLine("");

        var options = new RuppertOptions(25.0);
        options.MaxIterations = 500;
        var refiner = new RuppertRefiner(tin, options, null);

        int totalInsertions = 0;
        int outsideInsertions = 0;
        int firstEscapeAt = -1;

        _output.WriteLine("=== STEP-BY-STEP REFINEMENT TRACE ===");
        _output.WriteLine($"Initial vertices: {tin.GetVertices().Count}");
        _output.WriteLine($"Constraint: rectangle ({minX - buffer:F1},{minY - buffer:F1}) to ({maxX + buffer:F1},{maxY + buffer:F1})");
        _output.WriteLine("");

        for (int i = 0; i < 500; i++)
        {
            var preCount = tin.GetVertices().Count(v => v.IsSynthetic());
            var result = refiner.RefineOnce();
            if (result == null) break;

            var postCount = tin.GetVertices().Count(v => v.IsSynthetic());
            if (postCount <= preCount) continue; // No new Steiner point

            totalInsertions++;

            // Check if the new Steiner point is outside the constraint
            var newSteiner = tin.GetVertices()
                .Where(v => v.IsSynthetic())
                .OrderByDescending(v => v.GetIndex())
                .First();

            var pip = Polyside.IsPointInPolygon(constraintVerts, newSteiner.X, newSteiner.Y);
            bool isOutside = pip == Polyside.Result.Outside;

            if (isOutside)
            {
                outsideInsertions++;
                if (firstEscapeAt < 0)
                {
                    firstEscapeAt = totalInsertions;
                    _output.WriteLine($"*** FIRST ESCAPE at insertion #{totalInsertions} ***");
                    _output.WriteLine($"  Steiner point: ({newSteiner.X:F2}, {newSteiner.Y:F2}), index={newSteiner.GetIndex()}");
                    _output.WriteLine($"  PIP result: {pip}");

                    // Check what triangle this point is in and its edge flags
                    var nav = tin.GetNavigator();
                    var nearEdge = nav.GetNeighborEdge(newSteiner.X, newSteiner.Y);
                    if (nearEdge != null)
                    {
                        var eA = nearEdge;
                        var eB = nearEdge.GetForward();
                        var eC = eB.GetForward();
                        _output.WriteLine($"  Containing triangle edges:");
                        _output.WriteLine($"    eA: member={eA.IsConstraintRegionMember()}, border={eA.IsConstraintRegionBorder()}, interior={eA.IsConstraintRegionInterior()}, constrained={eA.IsConstrained()}");
                        _output.WriteLine($"    eB: member={eB.IsConstraintRegionMember()}, border={eB.IsConstraintRegionBorder()}, interior={eB.IsConstraintRegionInterior()}, constrained={eB.IsConstrained()}");
                        _output.WriteLine($"    eC: member={eC.IsConstraintRegionMember()}, border={eC.IsConstraintRegionBorder()}, interior={eC.IsConstraintRegionInterior()}, constrained={eC.IsConstrained()}");

                        // Check vertices of the triangle
                        _output.WriteLine($"    Triangle vertices:");
                        _output.WriteLine($"      A: ({eA.GetA().X:F2}, {eA.GetA().Y:F2}) synthetic={eA.GetA().IsSynthetic()}");
                        _output.WriteLine($"      B: ({eA.GetB().X:F2}, {eA.GetB().Y:F2}) synthetic={eA.GetB().IsSynthetic()}");
                        _output.WriteLine($"      C: ({eB.GetB().X:F2}, {eB.GetB().Y:F2}) synthetic={eB.GetB().IsSynthetic()}");

                        // Check PIP for each vertex of the triangle
                        var vA = eA.GetA();
                        var vB = eA.GetB();
                        var vC = eB.GetB();
                        _output.WriteLine($"    Vertex PIP:");
                        _output.WriteLine($"      A: {Polyside.IsPointInPolygon(constraintVerts, vA.X, vA.Y)}");
                        _output.WriteLine($"      B: {Polyside.IsPointInPolygon(constraintVerts, vB.X, vB.Y)}");
                        _output.WriteLine($"      C: {Polyside.IsPointInPolygon(constraintVerts, vC.X, vC.Y)}");
                    }
                    _output.WriteLine("");
                }

                // Classify: is this a boundary point or a real escape?
                var distFromRect = DistanceOutsideRect(newSteiner.X, newSteiner.Y, minX - buffer, maxX + buffer, minY - buffer, maxY + buffer);
                var category = distFromRect < 0.01 ? "BOUNDARY" : $"REAL ESCAPE (dist={distFromRect:F2}m)";

                if (outsideInsertions <= 30)
                {
                    _output.WriteLine($"  Escape #{outsideInsertions} at insertion #{totalInsertions}: ({newSteiner.X:F2}, {newSteiner.Y:F2}) {category}");
                }
            }

            if (totalInsertions % 50 == 0)
            {
                _output.WriteLine($"  ... {totalInsertions} insertions, {outsideInsertions} outside ({100.0 * outsideInsertions / totalInsertions:F1}%)");
            }
        }

        _output.WriteLine("");
        _output.WriteLine($"=== SUMMARY ===");
        _output.WriteLine($"Total Steiner insertions: {totalInsertions}");
        _output.WriteLine($"Outside constraint: {outsideInsertions} ({100.0 * outsideInsertions / Math.Max(totalInsertions, 1):F1}%)");
        _output.WriteLine($"First escape at insertion: #{firstEscapeAt}");

        // Now run full leak detector for comparison
        var report = ConstraintLeakDetector.Detect(tin, rect);
        _output.WriteLine($"Leak detector: {report.LeakedCount} leaked, {report.Divergences.Count} divergences");
        _output.WriteLine($"  First 5 divergences:");
        foreach (var d in report.Divergences.Take(5))
        {
            _output.WriteLine($"    ({d.X:F2}, {d.Y:F2}) geometry={d.GeometryInside}, flags={d.FlagStateInside}");
        }

        Assert.True(totalInsertions > 0, "Expected some Steiner point insertions");
    }

    private static double DistanceOutsideRect(double x, double y, double minX, double maxX, double minY, double maxY)
    {
        double dx = 0, dy = 0;
        if (x < minX) dx = minX - x;
        else if (x > maxX) dx = x - maxX;
        if (y < minY) dy = minY - y;
        else if (y > maxY) dy = y - maxY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static List<Vertex> LoadTrailDataAsUtm()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "RuppertsTestData", "TestTrail.csv");
        var lines = File.ReadAllLines(csvPath);
        var vertices = new List<Vertex>();
        int index = 0;
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            var lat = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var lon = double.Parse(parts[1], CultureInfo.InvariantCulture);
            var depth = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var (x, y, _) = UtmConverter.LatLonToUtm(lat, lon);
            vertices.Add(new Vertex(x, y, depth, index++));
        }
        return vertices;
    }

    private static (double minX, double maxX, double minY, double maxY) GetBounds(List<Vertex> vertices)
    {
        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);
        return (minX, maxX, minY, maxY);
    }
}
