// Temporary test file to verify resource loading
namespace Tinfour.Benchmarks;

using System.Diagnostics;
using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;

public static class TestResourceLoading
{
    public static void Test()
    {
        var assembly = typeof(TestResourceLoading).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        Console.WriteLine($"Found {resourceNames.Length} embedded resources:");
        foreach (var name in resourceNames)
        {
            Console.WriteLine($"  - {name}");
        }

        var trackPointsResource = resourceNames.FirstOrDefault(n => n.Contains("AllAmLake"));
        var shorelineResource = resourceNames.FirstOrDefault(n => n.Contains("AMLake"));

        Console.WriteLine();
        Console.WriteLine($"Track points resource: {trackPointsResource ?? "NOT FOUND"}");
        Console.WriteLine($"Shoreline resource: {shorelineResource ?? "NOT FOUND"}");

        if (trackPointsResource != null)
        {
            using var stream = assembly.GetManifestResourceStream(trackPointsResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var lineCount = 0;
                while (reader.ReadLine() != null) lineCount++;
                Console.WriteLine($"Track points file has {lineCount} lines");
            }
        }

        if (shorelineResource != null)
        {
            using var stream = assembly.GetManifestResourceStream(shorelineResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var lineCount = 0;
                while (reader.ReadLine() != null) lineCount++;
                Console.WriteLine($"Shoreline file has {lineCount} lines");
            }
        }
    }

    public static void TestTinBuilding()
    {
        Console.WriteLine("\n=== Testing TIN Building ===");
        var sw = Stopwatch.StartNew();

        // Load track points
        var assembly = typeof(TestResourceLoading).Assembly;
        var trackPointsResource = assembly.GetManifestResourceNames()
            .First(n => n.Contains("AllAmLake"));
        var shorelineResource = assembly.GetManifestResourceNames()
            .First(n => n.Contains("AMLake"));

        var trackPoints = new List<IVertex>();
        using (var stream = assembly.GetManifestResourceStream(trackPointsResource)!)
        using (var reader = new StreamReader(stream))
        {
            var idx = 0;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(',');
                if (parts.Length >= 3 &&
                    double.TryParse(parts[0], out var lat) &&
                    double.TryParse(parts[1], out var lon) &&
                    double.TryParse(parts[2], out var depth))
                {
                    trackPoints.Add(new Vertex(lon, lat, depth, idx++));
                }
            }
        }
        Console.WriteLine($"Loaded {trackPoints.Count} track points in {sw.ElapsedMilliseconds}ms");

        // Load shoreline (index,lat,lon format - index > 0 means hole)
        var shorelinePolygons = new List<(int Index, List<IVertex> Vertices)>();
        var currentPolygon = new List<IVertex>();
        var currentIndex = -1;
        var vertexIdx = trackPoints.Count;

        using (var stream = assembly.GetManifestResourceStream(shorelineResource)!)
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(',');
                if (parts.Length >= 3 &&
                    int.TryParse(parts[0], out var polyIdx) &&
                    double.TryParse(parts[1], out var lat) &&
                    double.TryParse(parts[2], out var lon))
                {
                    if (polyIdx != currentIndex && currentPolygon.Count > 0)
                    {
                        shorelinePolygons.Add((currentIndex, currentPolygon));
                        currentPolygon = new List<IVertex>();
                    }
                    currentIndex = polyIdx;
                    currentPolygon.Add(new Vertex(lon, lat, 0, vertexIdx++));
                }
            }
            if (currentPolygon.Count > 0)
                shorelinePolygons.Add((currentIndex, currentPolygon));
        }
        Console.WriteLine($"Loaded {shorelinePolygons.Count} shoreline polygons with {shorelinePolygons.Sum(p => p.Vertices.Count)} total vertices");

        // Build TIN
        sw.Restart();
        var minX = trackPoints.Min(v => v.X);
        var maxX = trackPoints.Max(v => v.X);
        var minY = trackPoints.Min(v => v.Y);
        var maxY = trackPoints.Max(v => v.Y);
        var nominalSpacing = Math.Max(maxX - minX, maxY - minY) / 100.0;
        Console.WriteLine($"Bounds: X=[{minX:F6}, {maxX:F6}], Y=[{minY:F6}, {maxY:F6}]");
        Console.WriteLine($"Nominal spacing: {nominalSpacing:E4}");

        var tin = new IncrementalTin(nominalSpacing);
        tin.Add(trackPoints);
        Console.WriteLine($"Built TIN with {tin.GetVertices().Count} vertices in {sw.ElapsedMilliseconds}ms");

        var triangleCount = tin.CountTriangles();
        Console.WriteLine($"Triangles: {triangleCount.ValidTriangles} valid, {triangleCount.GhostTriangles} ghost");

        // Add constraints (index > 0 = hole, needs reversed vertices and isHole=true)
        sw.Restart();
        var constraints = new List<IConstraint>();
        foreach (var (polyIndex, vertices) in shorelinePolygons)
        {
            if (vertices.Count < 3) continue;

            // Check if polygon is closed
            var first = vertices[0];
            var last = vertices[^1];
            if (Math.Abs(first.X - last.X) > 1e-9 || Math.Abs(first.Y - last.Y) > 1e-9)
            {
                vertices.Add(new Vertex(first.X, first.Y, first.GetZ(), vertexIdx++));
            }

            // Index > 0 means it's a hole - reverse vertices
            var isHole = polyIndex > 0;
            if (isHole)
            {
                vertices.Reverse();
            }

            var constraint = new PolygonConstraint(vertices, definesRegion: true, isHole: isHole);
            constraint.SetDefaultZ(0.0);
            constraints.Add(constraint);
        }

        tin.AddConstraints(constraints, true);
        Console.WriteLine($"Added {constraints.Count} constraints ({constraints.Count(c => c is PolygonConstraint pc && pc.IsHole())} holes) in {sw.ElapsedMilliseconds}ms");

        triangleCount = tin.CountTriangles();
        Console.WriteLine($"After constraints: {triangleCount.ValidTriangles} valid triangles");

        // Test refinement with 20 degrees (full run)
        Console.WriteLine("\n=== Testing Refinement at 20° (full run, max 500K iterations) ===");
        sw.Restart();

        var options = new RuppertOptions
        {
            MinimumAngleDegrees = 20.0,
            InterpolateZ = false,
            RefineOnlyInsideConstraints = true,
            MaxIterations = 500_000
        };

        var refiner = new RuppertRefiner(tin, options);
        var completed = refiner.Refine();

        Console.WriteLine($"Refinement completed: {completed}");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalSeconds:F1}s)");
        Console.WriteLine($"Final vertices: {tin.GetVertices().Count}");
        Console.WriteLine($"Final triangles: {tin.CountTriangles().ValidTriangles}");
        Console.WriteLine($"Vertices added: {tin.GetVertices().Count - 16857}");
    }
}
