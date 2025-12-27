namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;

/// <summary>
///     Result structure for refinement benchmarks.
/// </summary>
public struct RefinementResult
{
    public int InitialVertices { get; set; }

    public int FinalVertices { get; set; }

    public int InitialTriangles { get; set; }

    public int FinalTriangles { get; set; }

    public bool Completed { get; set; }

    public override string ToString()
    {
        var vertexIncrease = this.FinalVertices - this.InitialVertices;
        var triangleIncrease = this.FinalTriangles - this.InitialTriangles;
        return $"V: {this.InitialVertices} -> {this.FinalVertices} (+{vertexIncrease}), " +
               $"T: {this.InitialTriangles} -> {this.FinalTriangles} (+{triangleIncrease}), " +
               $"Complete: {this.Completed}";
    }
}

/// <summary>
///     Benchmarks for Ruppert's Delaunay refinement algorithm.
///     Tests constrained scenarios representing real-world usage.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
public class RuppertRefinementBenchmarks
{
    /// <summary>
    ///     Scenario type for the benchmark.
    /// </summary>
    public enum ScenarioType
    {
        /// <summary>Simple square constraint with sparse interior vertices.</summary>
        SimpleSquare,

        /// <summary>Donut shape (square with rectangular hole).</summary>
        DonutConstraint,

        /// <summary>Complex polygon with many edges.</summary>
        ComplexPolygon,

        /// <summary>Terrain with bounding box constraint auto-added.</summary>
        TerrainWithBoundingBox
    }

    [Params(ScenarioType.SimpleSquare, ScenarioType.DonutConstraint, ScenarioType.ComplexPolygon, ScenarioType.TerrainWithBoundingBox)]
    public ScenarioType Scenario;

    [Params(20.0, 25.0, 30.0)]
    public double MinimumAngle;

    [Params(false, true)]
    public bool InterpolateZ;

    private IncrementalTin _tin = null!;
    private RuppertOptions _options = null!;
    private int _initialVertexCount;
    private int _initialTriangleCount;

    [GlobalSetup]
    public void Setup()
    {
        // Create the appropriate scenario
        this._tin = this.Scenario switch
        {
            ScenarioType.SimpleSquare => CreateSimpleSquareScenario(),
            ScenarioType.DonutConstraint => CreateDonutScenario(),
            ScenarioType.ComplexPolygon => CreateComplexPolygonScenario(),
            ScenarioType.TerrainWithBoundingBox => CreateTerrainWithBoundingBoxScenario(),
            _ => throw new ArgumentException($"Unknown scenario: {this.Scenario}")
        };

        this._initialVertexCount = this._tin.GetVertices().Count;
        this._initialTriangleCount = this._tin.CountTriangles().ValidTriangles;

        // Configure options
        this._options = new RuppertOptions
        {
            MinimumAngleDegrees = this.MinimumAngle,
            InterpolateZ = this.InterpolateZ,
            InterpolationType = InterpolationType.TriangularFacet, // Faster than NaturalNeighbor
            RefineOnlyInsideConstraints = true,
            SkipSeditiousTriangles = true,
            IgnoreSeditiousEncroachments = true,
            MaxIterations = 100_000
        };

        // For terrain scenario, add bounding box constraint
        if (this.Scenario == ScenarioType.TerrainWithBoundingBox)
        {
            this._options.AddBoundingBoxConstraint = true;
            this._options.BoundingBoxBufferPercent = 1.0;
            this._options.RefineOnlyInsideConstraints = true;
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Re-create the TIN for each iteration since refinement modifies it
        this._tin = this.Scenario switch
        {
            ScenarioType.SimpleSquare => CreateSimpleSquareScenario(),
            ScenarioType.DonutConstraint => CreateDonutScenario(),
            ScenarioType.ComplexPolygon => CreateComplexPolygonScenario(),
            ScenarioType.TerrainWithBoundingBox => CreateTerrainWithBoundingBoxScenario(),
            _ => throw new ArgumentException($"Unknown scenario: {this.Scenario}")
        };
    }

    [Benchmark(Description = "Ruppert Refinement")]
    public RefinementResult RefineConstrained()
    {
        var refiner = new RuppertRefiner(this._tin, this._options);
        var completed = refiner.Refine();

        return new RefinementResult
        {
            InitialVertices = this._initialVertexCount,
            FinalVertices = this._tin.GetVertices().Count,
            InitialTriangles = this._initialTriangleCount,
            FinalTriangles = this._tin.CountTriangles().ValidTriangles,
            Completed = completed
        };
    }

    /// <summary>
    ///     Creates a simple square constraint with sparse interior vertices.
    ///     Large area with few points creates many skinny triangles requiring refinement.
    /// </summary>
    private static IncrementalTin CreateSimpleSquareScenario()
    {
        const double size = 1000.0;  // Large area
        const int interiorPoints = 5000;  // Sparse points = skinny triangles
        var tin = new IncrementalTin(size / 50.0);

        var random = new Random(42);
        var vertices = new List<IVertex>();
        var idx = 0;

        // Corner vertices for constraint
        var corners = new List<IVertex>
        {
            new Vertex(0, 0, 0, idx++),
            new Vertex(size, 0, 0, idx++),
            new Vertex(size, size, 0, idx++),
            new Vertex(0, size, 0, idx++)
        };

        foreach (var c in corners)
            vertices.Add(c);

        // Interior vertices with terrain-like Z - clustered to create skinny triangles
        for (var i = 0; i < interiorPoints; i++)
        {
            var x = random.NextDouble() * size * 0.9 + size * 0.05;
            var y = random.NextDouble() * size * 0.9 + size * 0.05;
            var z = 10 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01);
            vertices.Add(new Vertex(x, y, z, idx++));
        }

        tin.Add(vertices);

        // Add square constraint
        var constraint = new PolygonConstraint(corners, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }

    /// <summary>
    ///     Creates a donut-shaped constraint (outer square with inner hole).
    ///     Large area with sparse points tests refinement with multiple constraint boundaries.
    /// </summary>
    private static IncrementalTin CreateDonutScenario()
    {
        const double outerSize = 1000.0;  // Large area
        const double innerSize = 400.0;
        const double innerOffset = 300.0;
        const int interiorPoints = 3000;  // Sparse for skinny triangles
        var tin = new IncrementalTin(outerSize / 50.0);

        var random = new Random(42);
        var vertices = new List<IVertex>();
        var idx = 0;

        // Outer boundary
        var outerCorners = new List<IVertex>
        {
            new Vertex(0, 0, 0, idx++),
            new Vertex(outerSize, 0, 0, idx++),
            new Vertex(outerSize, outerSize, 0, idx++),
            new Vertex(0, outerSize, 0, idx++)
        };

        // Inner hole (counter-clockwise for hole)
        var innerCorners = new List<IVertex>
        {
            new Vertex(innerOffset, innerOffset, 0, idx++),
            new Vertex(innerOffset, innerOffset + innerSize, 0, idx++),
            new Vertex(innerOffset + innerSize, innerOffset + innerSize, 0, idx++),
            new Vertex(innerOffset + innerSize, innerOffset, 0, idx++)
        };

        foreach (var c in outerCorners)
            vertices.Add(c);
        foreach (var c in innerCorners)
            vertices.Add(c);

        // Interior vertices (outside the hole)
        for (var i = 0; i < interiorPoints; i++)
        {
            double x, y;
            do
            {
                x = random.NextDouble() * outerSize;
                y = random.NextDouble() * outerSize;
            } while (x > innerOffset && x < innerOffset + innerSize &&
                     y > innerOffset && y < innerOffset + innerSize);

            var z = 10 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01);
            vertices.Add(new Vertex(x, y, z, idx++));
        }

        tin.Add(vertices);

        // Add constraints - inner is a hole
        var outerConstraint = new PolygonConstraint(outerCorners, true);
        var innerConstraint = new PolygonConstraint(innerCorners, true, isHole: true);
        tin.AddConstraints(new IConstraint[] { outerConstraint, innerConstraint }, true);

        return tin;
    }

    /// <summary>
    ///     Creates a complex polygon constraint with many edges (approximating a circle).
    ///     This tests refinement with many constraint segments.
    /// </summary>
    private static IncrementalTin CreateComplexPolygonScenario()
    {
        const double radius = 50.0;
        const double centerX = 50.0;
        const double centerY = 50.0;
        const int polygonSides = 32;
        const int interiorPoints = 1500;
        var tin = new IncrementalTin(radius / 5.0);

        var random = new Random(42);
        var vertices = new List<IVertex>();
        var idx = 0;

        // Create polygon approximating a circle
        var polygonVertices = new List<IVertex>();
        for (var i = 0; i < polygonSides; i++)
        {
            var angle = 2 * Math.PI * i / polygonSides;
            var x = centerX + radius * Math.Cos(angle);
            var y = centerY + radius * Math.Sin(angle);
            var v = new Vertex(x, y, 0, idx++);
            polygonVertices.Add(v);
            vertices.Add(v);
        }

        // Interior vertices
        for (var i = 0; i < interiorPoints; i++)
        {
            // Random point inside the circle
            var r = random.NextDouble() * radius * 0.9;
            var angle = random.NextDouble() * 2 * Math.PI;
            var x = centerX + r * Math.Cos(angle);
            var y = centerY + r * Math.Sin(angle);
            var z = 10 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1);
            vertices.Add(new Vertex(x, y, z, idx++));
        }

        tin.Add(vertices);

        // Add polygon constraint
        var constraint = new PolygonConstraint(polygonVertices, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }

    /// <summary>
    ///     Creates a terrain-like TIN without explicit constraints.
    ///     Uses AddBoundingBoxConstraint option to auto-create boundary.
    ///     This tests the realistic scenario of refining arbitrary point clouds.
    /// </summary>
    private static IncrementalTin CreateTerrainWithBoundingBoxScenario()
    {
        const double size = 100.0;
        const int vertexCount = 1000;
        var tin = new IncrementalTin(size / 20.0);

        var random = new Random(42);
        var vertices = new List<IVertex>(vertexCount);

        // Generate terrain-like data
        for (var i = 0; i < vertexCount; i++)
        {
            var x = random.NextDouble() * size;
            var y = random.NextDouble() * size;

            // Multi-frequency terrain
            var z = 50 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01) +
                    20 * Math.Sin(x * 0.03) * Math.Sin(y * 0.03) +
                    5 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1) +
                    random.NextDouble() * 2;

            vertices.Add(new Vertex(x, y, z, i));
        }

        tin.Add(vertices);

        // Note: The bounding box constraint will be added by RuppertRefiner
        // when AddBoundingBoxConstraint option is true
        return tin;
    }
}

/// <summary>
///     Focused benchmarks for specific refinement performance aspects.
///     Uses smaller iteration counts for quick profiling.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 2)]
public class RuppertRefinementDetailedBenchmarks
{
    private IncrementalTin _tin = null!;
    private RuppertOptions _options = null!;

    [Params(1000, 5000, 10000)]
    public int InitialVertexCount;

    [Params(20.0, 25.0)]
    public double MinimumAngle;

    [GlobalSetup]
    public void Setup()
    {
        this._options = new RuppertOptions
        {
            MinimumAngleDegrees = this.MinimumAngle,
            InterpolateZ = false,
            RefineOnlyInsideConstraints = true,
            MaxIterations = 200_000
        };
    }

    [IterationSetup]
    public void IterationSetup()
    {
        this._tin = CreateScalableConstrainedTin(this.InitialVertexCount);
    }

    [Benchmark(Description = "Constrained Refinement (Scalable)")]
    public RefinementResult RefineScalable()
    {
        var initialVertices = this._tin.GetVertices().Count;
        var initialTriangles = this._tin.CountTriangles().ValidTriangles;

        var refiner = new RuppertRefiner(this._tin, this._options);
        var completed = refiner.Refine();

        return new RefinementResult
        {
            InitialVertices = initialVertices,
            FinalVertices = this._tin.GetVertices().Count,
            InitialTriangles = initialTriangles,
            FinalTriangles = this._tin.CountTriangles().ValidTriangles,
            Completed = completed
        };
    }

    /// <summary>
    ///     Creates a constrained TIN with specified vertex count.
    /// </summary>
    private static IncrementalTin CreateScalableConstrainedTin(int vertexCount)
    {
        const double size = 100.0;
        var tin = new IncrementalTin(size / Math.Sqrt(vertexCount));

        var random = new Random(42);
        var vertices = new List<IVertex>();
        var idx = 0;

        // Corner vertices for constraint
        var corners = new List<IVertex>
        {
            new Vertex(0, 0, 0, idx++),
            new Vertex(size, 0, 0, idx++),
            new Vertex(size, size, 0, idx++),
            new Vertex(0, size, 0, idx++)
        };

        foreach (var c in corners)
            vertices.Add(c);

        // Interior vertices
        var interiorCount = Math.Max(0, vertexCount - 4);
        for (var i = 0; i < interiorCount; i++)
        {
            var x = random.NextDouble() * size * 0.98 + size * 0.01;
            var y = random.NextDouble() * size * 0.98 + size * 0.01;
            var z = 10 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1);
            vertices.Add(new Vertex(x, y, z, idx++));
        }

        tin.Add(vertices);

        // Add square constraint
        var constraint = new PolygonConstraint(corners, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }
}

/// <summary>
///     Z-interpolation mode for benchmarks.
/// </summary>
public enum ZInterpolationMode
{
    /// <summary>Triangular facet (linear) interpolation.</summary>
    TriangularFacet,

    /// <summary>Natural Neighbor (Sibson) interpolation - slowest but smoothest.</summary>
    NaturalNeighbor
}

/// <summary>
///     Benchmarks comparing refinement with different Z interpolation methods.
///     Uses sparse vertex distributions in large areas to create many skinny triangles
///     that require significant refinement work. Targets 5-30 second run times.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
public class RuppertInterpolationImpactBenchmarks
{
    private IncrementalTin _tin = null!;
    private int _initialVertexCount;

    // Sparse vertex counts in a very large area - creates many skinny triangles
    // Tuned for 5-30 second runs with substantial refinement work
    [Params(10000, 20000, 50000)]
    public int VertexCount;

    [Params(20.0, 30.0)]
    public double MinimumAngle;

    [Params(ZInterpolationMode.TriangularFacet, ZInterpolationMode.NaturalNeighbor)]
    public ZInterpolationMode ZInterpolation;

    [IterationSetup]
    public void IterationSetup()
    {
        this._tin = CreateSparseClusteredTin(this.VertexCount);
        this._initialVertexCount = this._tin.GetVertices().Count;
    }

    [Benchmark(Description = "Refinement with Z Interpolation")]
    public RefinementResult RefineWithInterpolation()
    {
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = this.MinimumAngle,
            InterpolateZ = true,
            InterpolationType = this.ZInterpolation switch
            {
                ZInterpolationMode.NaturalNeighbor => InterpolationType.NaturalNeighbor,
                _ => InterpolationType.TriangularFacet
            },
            RefineOnlyInsideConstraints = true,
            MaxIterations = 100_000
        };

        var refiner = new RuppertRefiner(this._tin, options);
        var completed = refiner.Refine();

        return new RefinementResult
        {
            InitialVertices = this._initialVertexCount,
            FinalVertices = this._tin.GetVertices().Count,
            Completed = completed
        };
    }

    /// <summary>
    ///     Creates a TIN with sparse, clustered vertices in a very large area.
    ///     The clusters create many long, skinny triangles between them that
    ///     require significant refinement to meet angle criteria.
    ///     Area sized to produce 5-30 second benchmark runs.
    /// </summary>
    private static IncrementalTin CreateSparseClusteredTin(int vertexCount)
    {
        const double size = 10000.0; // Very large area with sparse points for substantial work
        var tin = new IncrementalTin(size / 50.0);

        var random = new Random(42);
        var vertices = new List<IVertex>();
        var idx = 0;

        // Corner vertices for constraint
        var corners = new List<IVertex>
        {
            new Vertex(0, 0, 0, idx++),
            new Vertex(size, 0, 0, idx++),
            new Vertex(size, size, 0, idx++),
            new Vertex(0, size, 0, idx++)
        };

        foreach (var c in corners)
            vertices.Add(c);

        // Create clustered vertices - generates long skinny triangles between clusters
        var clusterCount = Math.Max(4, vertexCount / 10);
        var pointsPerCluster = Math.Max(1, (vertexCount - 4) / clusterCount);

        for (var cluster = 0; cluster < clusterCount; cluster++)
        {
            // Random cluster center
            var cx = random.NextDouble() * size * 0.8 + size * 0.1;
            var cy = random.NextDouble() * size * 0.8 + size * 0.1;
            var clusterRadius = size * 0.01; // Very small tight clusters for skinny triangles

            for (var i = 0; i < pointsPerCluster && vertices.Count < vertexCount; i++)
            {
                var angle = random.NextDouble() * 2 * Math.PI;
                var r = random.NextDouble() * clusterRadius;
                var x = Math.Clamp(cx + r * Math.Cos(angle), 1, size - 1);
                var y = Math.Clamp(cy + r * Math.Sin(angle), 1, size - 1);

                var z = 50 * Math.Sin(x * 0.001) * Math.Cos(y * 0.001) +
                        10 * Math.Sin(x * 0.005) * Math.Sin(y * 0.005);
                vertices.Add(new Vertex(x, y, z, idx++));
            }
        }

        // Fill remaining with random points
        while (vertices.Count < vertexCount)
        {
            var x = random.NextDouble() * size * 0.98 + size * 0.01;
            var y = random.NextDouble() * size * 0.98 + size * 0.01;
            var z = 50 * Math.Sin(x * 0.001) * Math.Cos(y * 0.001);
            vertices.Add(new Vertex(x, y, z, idx++));
        }

        tin.Add(vertices);

        // Add square constraint
        var constraint = new PolygonConstraint(corners, true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        return tin;
    }
}

/// <summary>
///     Real-world benchmark using actual beam survey data from American Lake.
///     This represents a realistic hydrographic survey scenario with track points
///     creating many skinny triangles inside a shoreline constraint.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
public class RuppertRealWorldBenchmarks
{
    private IncrementalTin _tin = null!;
    private int _initialVertexCount;
    private int _initialTriangleCount;

    // Cached data loaded once
    private static List<IVertex>? _cachedTrackPoints;
    private static List<(int Index, List<IVertex> Vertices)>? _cachedShorelinePolygons;
    private static bool _dataLoaded;

    [Params(20.0, 25.0, 30.0)]
    public double MinimumAngle;

    [Params(ZInterpolationMode.TriangularFacet, ZInterpolationMode.NaturalNeighbor)]
    public ZInterpolationMode ZInterpolation;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Load data once for all iterations
        if (!_dataLoaded)
        {
            LoadTestData();
            _dataLoaded = true;
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        this._tin = CreateAmericanLakeTin();
        this._initialVertexCount = this._tin.GetVertices().Count;
        this._initialTriangleCount = this._tin.CountTriangles().ValidTriangles;
    }

    [Benchmark(Description = "American Lake Survey Refinement")]
    public RefinementResult RefineRealWorld()
    {
        var options = new RuppertOptions
        {
            MinimumAngleDegrees = this.MinimumAngle,
            InterpolateZ = true,
            InterpolationType = this.ZInterpolation switch
            {
                ZInterpolationMode.NaturalNeighbor => InterpolationType.NaturalNeighbor,
                _ => InterpolationType.TriangularFacet
            },
            RefineOnlyInsideConstraints = true,
            SkipSeditiousTriangles = true,
            IgnoreSeditiousEncroachments = true,
            MaxIterations = 100_000
        };

        var refiner = new RuppertRefiner(this._tin, options);
        var completed = refiner.Refine();

        return new RefinementResult
        {
            InitialVertices = this._initialVertexCount,
            FinalVertices = this._tin.GetVertices().Count,
            InitialTriangles = this._initialTriangleCount,
            FinalTriangles = this._tin.CountTriangles().ValidTriangles,
            Completed = completed
        };
    }

    private static void LoadTestData()
    {
        var assembly = typeof(RuppertRealWorldBenchmarks).Assembly;

        // Load track points (lat,lon,depth format)
        var trackPointsResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.Contains("AllAmLakeTrackPointsCSV"));

        if (trackPointsResourceName == null)
            throw new InvalidOperationException("Could not find track points resource. Available: " +
                string.Join(", ", assembly.GetManifestResourceNames()));

        _cachedTrackPoints = new List<IVertex>();
        using (var stream = assembly.GetManifestResourceStream(trackPointsResourceName)!)
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
                    // Use lat/lon directly (small scale, geographic coords)
                    _cachedTrackPoints.Add(new Vertex(lon, lat, depth, idx++));
                }
            }
        }

        // Load shoreline constraints (index,lat,lon format)
        var shorelineResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.Contains("AMLakeShorelines"));

        if (shorelineResourceName == null)
            throw new InvalidOperationException("Could not find shoreline resource");

        _cachedShorelinePolygons = new List<(int Index, List<IVertex> Vertices)>();
        var currentPolygon = new List<IVertex>();
        var currentIndex = -1;
        var vertexIdx = _cachedTrackPoints.Count;

        using (var stream = assembly.GetManifestResourceStream(shorelineResourceName)!)
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
                        _cachedShorelinePolygons.Add((currentIndex, currentPolygon));
                        currentPolygon = new List<IVertex>();
                    }
                    currentIndex = polyIdx;
                    currentPolygon.Add(new Vertex(lon, lat, 0, vertexIdx++));
                }
            }

            if (currentPolygon.Count > 0)
                _cachedShorelinePolygons.Add((currentIndex, currentPolygon));
        }
    }

    private static IncrementalTin CreateAmericanLakeTin()
    {
        if (_cachedTrackPoints == null || _cachedShorelinePolygons == null)
            throw new InvalidOperationException("Test data not loaded");

        // Calculate bounds for nominal spacing
        var minX = _cachedTrackPoints.Min(v => v.X);
        var maxX = _cachedTrackPoints.Max(v => v.X);
        var minY = _cachedTrackPoints.Min(v => v.Y);
        var maxY = _cachedTrackPoints.Max(v => v.Y);
        var nominalSpacing = Math.Max(maxX - minX, maxY - minY) / 100.0;

        var tin = new IncrementalTin(nominalSpacing);

        // Clone vertices to avoid mutation issues across iterations
        var trackVertices = _cachedTrackPoints
            .Select((v, i) => (IVertex)new Vertex(v.X, v.Y, v.GetZ(), i))
            .ToList();

        tin.Add(trackVertices);

        // Clone and add constraints
        var vertexIdx = trackVertices.Count;
        var constraints = new List<IConstraint>();

        foreach (var (polyIndex, polygon) in _cachedShorelinePolygons)
        {
            var clonedPolygon = polygon
                .Select(v => (IVertex)new Vertex(v.X, v.Y, v.GetZ(), vertexIdx++))
                .ToList();

            if (clonedPolygon.Count >= 3)
            {
                // Check if polygon is closed (first and last points match)
                var first = clonedPolygon[0];
                var last = clonedPolygon[^1];
                if (Math.Abs(first.X - last.X) > 1e-9 || Math.Abs(first.Y - last.Y) > 1e-9)
                {
                    // Close the polygon by adding first vertex again
                    clonedPolygon.Add(new Vertex(first.X, first.Y, first.GetZ(), vertexIdx++));
                }

                // Index > 0 means it's a hole - reverse vertices
                var isHole = polyIndex > 0;
                if (isHole)
                {
                    clonedPolygon.Reverse();
                }

                var constraint = new PolygonConstraint(clonedPolygon, definesRegion: true, isHole: isHole);
                constraint.SetDefaultZ(0.0);
                constraints.Add(constraint);
            }
        }

        if (constraints.Count > 0)
        {
            tin.AddConstraints(constraints, true);
        }

        return tin;
    }
}
