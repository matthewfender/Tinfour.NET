using System.Globalization;
using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;
using Tinfour.Core.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Tinfour.Core.Tests.Refinement;

public class ConvexHullConstraintOptionTests
{
    private readonly ITestOutputHelper _output;

    public ConvexHullConstraintOptionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AddConvexHullConstraint_WithTrailData_ShouldRefineWithinHull()
    {
        var vertices = LoadTrailDataAsUtm();
        var nominalSpacing = ComputeNominalSpacing(vertices);

        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(vertices.Cast<IVertex>().ToList());

        // Ruppert insertion counts are order-sensitive; the SoA edge store (#832)
        // changed edge enumeration/allocation order, moving convergence for this
        // dataset from ~4.9k to ~5.3k Steiner points. The cap guards against
        // non-termination, not a specific count.
        var options = new RuppertOptions(25.0)
        {
            AddConvexHullConstraint = true,
            MaxIterations = 20000
        };
        var refiner = new RuppertRefiner(tin, options, null);
        var success = refiner.Refine();

        _output.WriteLine($"Refinement success: {success}");
        _output.WriteLine($"Vertices: {tin.GetVertices().Count}");
        _output.WriteLine($"Steiner: {tin.GetVertices().Count(v => v.IsSynthetic())}");
        _output.WriteLine($"Triangles: {tin.CountTriangles().ValidTriangles}");

        // Find the hull constraint that was added
        var constraints = tin.GetConstraints();
        _output.WriteLine($"Constraints: {constraints.Count}");
        Assert.True(constraints.Count > 0, "Expected convex hull constraint to be added");

        var hullConstraint = constraints[0];
        var report = ConstraintLeakDetector.Detect(tin, hullConstraint);
        _output.WriteLine($"Leaked: {report.LeakedCount}");
        _output.WriteLine($"Divergences: {report.Divergences.Count}");

        Assert.True(success, "Refinement should converge");
        Assert.Equal(0, report.LeakedCount);
    }

    [Fact]
    public void AddConvexHullConstraint_ShouldConvergeWithZeroLeaks()
    {
        var vertices = LoadTrailDataAsUtm();
        var nominalSpacing = ComputeNominalSpacing(vertices);

        // Run with convex hull
        var tinHull = new IncrementalTin(nominalSpacing);
        tinHull.Add(vertices.Cast<IVertex>().ToList());
        // Cap raised for order-sensitive convergence; see comment in the test above.
        var hullOptions = new RuppertOptions(25.0) { AddConvexHullConstraint = true, MaxIterations = 20000 };
        var success = new RuppertRefiner(tinHull, hullOptions, null).Refine();
        var hullSteiner = tinHull.GetVertices().Count(v => v.IsSynthetic());

        // Run with bounding box for comparison
        var tinBox = new IncrementalTin(nominalSpacing);
        tinBox.Add(vertices.Cast<IVertex>().ToList());
        var boxOptions = new RuppertOptions(25.0) { AddBoundingBoxConstraint = true, MaxIterations = 20000 };
        new RuppertRefiner(tinBox, boxOptions, null).Refine();
        var boxSteiner = tinBox.GetVertices().Count(v => v.IsSynthetic());

        _output.WriteLine($"Hull Steiner points: {hullSteiner}");
        _output.WriteLine($"Box Steiner points:  {boxSteiner}");

        Assert.True(success, "Hull refinement should converge");

        var hullConstraint = tinHull.GetConstraints()[0];
        var report = ConstraintLeakDetector.Detect(tinHull, hullConstraint);
        Assert.Equal(0, report.LeakedCount);
    }

    private static List<Vertex> LoadTrailDataAsUtm()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "RuppertsTestData", "TestTrail.csv");
        var lines = File.ReadAllLines(csvPath);
        var verts = new List<Vertex>();
        int index = 0;
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            var lat = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var lon = double.Parse(parts[1], CultureInfo.InvariantCulture);
            var depth = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var (x, y, _) = UtmConverter.LatLonToUtm(lat, lon);
            verts.Add(new Vertex(x, y, depth, index++));
        }
        return verts;
    }

    private static double ComputeNominalSpacing(List<Vertex> vertices)
    {
        var minX = vertices.Min(v => v.X);
        var maxX = vertices.Max(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxY = vertices.Max(v => v.Y);
        return Math.Sqrt((maxX - minX) * (maxY - minY) / vertices.Count);
    }
}
