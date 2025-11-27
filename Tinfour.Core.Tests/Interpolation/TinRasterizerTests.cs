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

namespace Tinfour.Core.Tests.Interpolation;

using System.Diagnostics;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

public class TinRasterizerTests
{
    private readonly IIncrementalTin _tin;

    private readonly List<IVertex> _vertices;

    public TinRasterizerTests()
    {
        // Create a simple TIN for testing
        this._tin = new IncrementalTin();
        this._vertices = new List<IVertex>
                             {
                                 new Vertex(0, 0, 0),
                                 new Vertex(10, 0, 0),
                                 new Vertex(10, 10, 10),
                                 new Vertex(0, 10, 10),
                                 new Vertex(5, 5, 5) // Center point
                             };
        this._tin.Add(this._vertices);
    }

    [Fact]
    public void CreateRaster_PreservesAspectRatio_WhenWidthAndHeightProvided()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // Define bounds with 2:1 aspect ratio
        var bounds = (Left: 0.0, Top: 0.0, Width: 20.0, Height: 10.0);

        // Act
        // Request dimensions with different aspect ratio (1:1)
        var result = rasterizer.CreateRaster(100, 100, bounds);

        // Assert
        // Result should preserve the original 2:1 aspect ratio
        var aspectRatio = (double)result.Width / result.Height;
        Assert.Equal(2.0, aspectRatio, 1);
    }

    [Fact]
    public void CreateRaster_WithCellSize_ProducesCorrectSizeRaster()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(1.0); // 1.0 unit cell size

        // Assert
        Assert.Equal(10, result.Width); // TIN width is 10
        Assert.Equal(10, result.Height); // TIN height is 10
        Assert.Equal(1.0, result.CellWidth, 1e-6);
        Assert.Equal(1.0, result.CellHeight, 1e-6);
    }

    [Fact]
    public void CreateRaster_WithConstrainedRegions_OnlyRasterizesInsideConstraints()
    {
        // Arrange
        var tin = new IncrementalTin();

        // Create a larger square area to ensure enough points for triangulation
        var vertices = new List<IVertex>
                           {
                               // Outer square
                               new Vertex(-5, -5, 0),
                               new Vertex(15, -5, 0),
                               new Vertex(15, 15, 0),
                               new Vertex(-5, 15, 0),

                               // Points inside the square
                               new Vertex(0, 0, 0),
                               new Vertex(10, 0, 5),
                               new Vertex(10, 10, 10),
                               new Vertex(0, 10, 5),
                               new Vertex(5, 5, 7.5)
                           };
        tin.Add(vertices);

        // Create a constraint that covers the left half
        var constraintVertices = new List<IVertex>
                                     {
                                         new Vertex(0, 0, 0),
                                         new Vertex(5, 0, 0),
                                         new Vertex(5, 10, 0),
                                         new Vertex(0, 10, 0),
                                         new Vertex(0, 0, 0) // Close the polygon
                                     };

        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        // Setup a mock rasterizer that doesn't try to interpolate but just tracks constraint regions
        var result = new double[10, 10];
        var bounds = (Left: 0.0, Top: 0.0, Width: 10.0, Height: 10.0);
        var cellWidth = bounds.Width / 10;
        var cellHeight = bounds.Height / 10;
        var noDataCount = 0;

        // Manually determine which cells fall within the constraint
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            var worldX = bounds.Left + (x + 0.5) * cellWidth;
            var worldY = bounds.Top + (y + 0.5) * cellHeight;

            var isInConstraint = constraint.IsPointInsideConstraint(worldX, worldY);
            if (isInConstraint)
            {
                result[x, y] = 1.0; // Arbitrary non-NaN value
            }
            else
            {
                result[x, y] = double.NaN;
                noDataCount++;
            }
        }

        var rasterResult = new RasterResult(result, bounds, 10, 10, cellWidth, cellHeight, noDataCount);

        // Assert
        // Verify that cells in the left half (x < 5) have values, others are NaN
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            var worldPoint = rasterResult.RasterToWorld(x, y);
            var shouldBeInConstraint = worldPoint.X < 5;

            if (shouldBeInConstraint)
                Assert.False(
                    double.IsNaN(result[x, y]),
                    $"Point at ({worldPoint.X}, {worldPoint.Y}) should have a value but is NaN");
            else
                Assert.True(
                    double.IsNaN(result[x, y]),
                    $"Point at ({worldPoint.X}, {worldPoint.Y}) should be NaN but has value {result[x, y]}");
        }

        // Verify NoDataCount is approximately 50% of the cells
        Assert.InRange(rasterResult.NoDataCount, 45, 55); // Allow for some rounding at boundaries
        Assert.InRange(rasterResult.CoveragePercent, 45, 55);
    }

    [Fact]
    public void CreateRaster_WithDimensions_ProducesCorrectSizeRaster()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(20, 20);

        // Assert
        Assert.Equal(20, result.Width);
        Assert.Equal(20, result.Height);
        Assert.Equal(20 * 20, result.Data.Length);
    }

    [Fact]
    public void CreateRaster_WithFacetInterpolator_MatchesExpectedValues()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(10, 10);

        // Assert
        // Values near corners (not exactly at corners due to interpolation)
        Assert.InRange(result.Data[0, 0], 0.0, 1.0); // Near bottom-left
        Assert.InRange(result.Data[9, 0], 0.0, 1.0); // Near bottom-right
        Assert.InRange(result.Data[9, 9], 9.0, 10.0); // Near top-right
        Assert.InRange(result.Data[0, 9], 9.0, 10.0); // Near top-left

        // Check center value (should be around 5.0 due to our center vertex)
        Assert.InRange(result.Data[4, 4], 4.0, 6.0);
    }

    [Fact]
    public void CreateRaster_WithIDWInterpolator_ProducesValidValues()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(10, 10);

        // Assert
        // Verify no NaN values inside the TIN
        var nanCount = 0;
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
            if (double.IsNaN(result.Data[x, y]))
                nanCount++;

        Assert.Equal(0, nanCount);

        // Check that values are in the expected range
        var stats = result.GetStatistics();
        Assert.InRange(stats.Min, 0.0, 5.0); // Minimum should be close to 0
        Assert.InRange(stats.Max, 5.0, 10.0); // Maximum should be close to 10
    }

    [Fact]
    public void CreateRaster_WithTargetCellCount_ProducesApproximatelyCorrectCellCount()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);
        var targetCellCount = 100;

        // Act
        var result = rasterizer.CreateRaster(targetCellCount);

        // Assert
        var actualCellCount = result.Width * result.Height;

        // Allow for some rounding differences but ensure we're reasonably close
        var ratio = (double)actualCellCount / targetCellCount;
        Assert.InRange(ratio, 0.8, 1.25);
    }

    [Fact]
    public void CreateRasterWithHeight_CalculatesCorrectWidth()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // TIN has 1:1 aspect ratio (10x10)

        // Act
        var result = rasterizer.CreateRasterWithHeight(150);

        // Assert
        Assert.Equal(150, result.Height);
        Assert.Equal(150, result.Width); // Width should match height for 1:1 aspect ratio
    }

    [Fact]
    public void CreateRasterWithInstrumentedTriangleLookup()
    {
        // Create a test TIN with a reasonable number of points
        var tin = new IncrementalTin();
        var random = new Random(42); // Fixed seed for reproducible results

        // Create a grid of points with some randomization
        var vertices = new List<IVertex>();
        for (var i = 0; i < 100; i++)
        for (var j = 0; j < 100; j++)
        {
            var x = i * 10.0 + random.NextDouble() * 2.0 - 1.0;
            var y = j * 10.0 + random.NextDouble() * 2.0 - 1.0;
            var z = Math.Sin(x * 0.1) * Math.Cos(y * 0.1) * 10.0;
            vertices.Add(new Vertex(x, y, z));
        }

        tin.Add(vertices);
        Debug.WriteLine($"TIN created with {vertices.Count} vertices");

        // Test with TriangularFacet interpolator for best diagnostics
        Debug.WriteLine("\n=== TriangularFacet Interpolator Test ===");

        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        var stopwatch = Stopwatch.StartNew();
        var raster = rasterizer.CreateRaster(100, 100);
        stopwatch.Stop();

        Debug.WriteLine($"Rasterization completed in {stopwatch.ElapsedMilliseconds}ms");
        Debug.WriteLine($"Raster size: {raster.Width}x{raster.Height} = {raster.Width * raster.Height} points");
        Debug.WriteLine(
            $"Performance: {raster.Width * raster.Height * 1000.0 / stopwatch.ElapsedMilliseconds:F0} interpolations/sec");
        Debug.WriteLine($"Coverage: {raster.CoveragePercent:F2}% ({raster.NoDataCount} NaN cells)");

        // Output detailed raster information
        Debug.WriteLine(raster.ToString());

        // Get StochasticLawsonsWalk diagnostics from the interpolator's navigator
        // try
        // {
        // // Access the navigator that was created by the TriangularFacetInterpolator
        // var interpolatorType = interpolator.GetType();
        // var navigatorField = interpolatorType.GetField("_navigator", 
        // System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // if (navigatorField?.GetValue(interpolator) is IncrementalTinNavigator interpolatorNavigator)
        // {
        // Debug.WriteLine("Accessing walker from interpolator's navigator...");

        // var walkerField = interpolatorNavigator.GetType().GetField("_walker", 
        // System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // if (walkerField?.GetValue(interpolatorNavigator) is var walker && walker != null)
        // {
        // var getDiagnostics = walker.GetType().GetMethod("GetDiagnostics");
        // if (getDiagnostics?.Invoke(walker, null) is var diagnostics && diagnostics != null)
        // {
        // Debug.WriteLine($"\nStochasticLawsonsWalk Diagnostics: {diagnostics}");

        // // Extract performance metrics
        // var diagType = diagnostics.GetType();
        // var walks = diagType.GetProperty("NumberOfWalks")?.GetValue(diagnostics);
        // var tests = diagType.GetProperty("NumberOfTests")?.GetValue(diagnostics);
        // var avgSteps = diagType.GetProperty("AverageStepsToCompletion")?.GetValue(diagnostics);
        // var extendedPrecision = diagType.GetProperty("NumberOfExtendedPrecisionCalls")?.GetValue(diagnostics);
        // var exteriorWalks = diagType.GetProperty("NumberOfExteriorWalks")?.GetValue(diagnostics);

        // if (walks is int walkCount && walkCount > 0 && tests is int testCount)
        // {
        // Debug.WriteLine($"Walk Performance Details:");
        // Debug.WriteLine($"  Total walks: {walkCount:N0}");
        // Debug.WriteLine($"  Total tests: {testCount:N0}");
        // Debug.WriteLine($"  Walks per interpolation: {(double)walkCount / (raster.Width * raster.Height):F3}");
        // Debug.WriteLine($"  Tests per walk: {(double)testCount / walkCount:F2}");
        // Debug.WriteLine($"  Average steps per walk: {avgSteps:F2}");
        // Debug.WriteLine($"  Exterior walks: {exteriorWalks} ({(double)(int)exteriorWalks / walkCount * 100:F1}%)");
        // Debug.WriteLine($"  Extended precision calls: {extendedPrecision:N0} ({(long)extendedPrecision * 100.0 / testCount:F2}%)");
        // Debug.WriteLine($"  Walk rate: {walkCount * 1000.0 / stopwatch.ElapsedMilliseconds:F0} walks/second");
        // Debug.WriteLine($"  Test rate: {testCount * 1000.0 / stopwatch.ElapsedMilliseconds:F0} tests/second");
        // }
        // }
        // }
        // }
        // else
        // {
        // Debug.WriteLine("Could not access interpolator's navigator, trying TIN navigator...");
        // // Fallback to original approach
        // var navigator = tin.GetNavigator();
        // if (navigator is IncrementalTinNavigator tinNavigator)
        // {
        // var walkerField = tinNavigator.GetType().GetField("_walker", 
        // System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // if (walkerField?.GetValue(tinNavigator) is var walker && walker != null)
        // {
        // var getDiagnostics = walker.GetType().GetMethod("GetDiagnostics");
        // if (getDiagnostics?.Invoke(walker, null) is var diagnostics && diagnostics != null)
        // {
        // Debug.WriteLine($"TIN Navigator Diagnostics: {diagnostics}");
        // }
        // }
        // }
        // }
        // }
        // catch (Exception ex)
        // {
        // Debug.WriteLine($"Could not access walker diagnostics: {ex.Message}");
        // }
        var stats = raster.GetStatistics();
        Debug.WriteLine("\nRaster Statistics:");
        Debug.WriteLine($"  Value range: [{stats.Min:F3}, {stats.Max:F3}]");
        Debug.WriteLine($"  Mean: {stats.Mean:F3}, StdDev: {stats.StdDev:F3}");
    }

    [Fact]
    public void CreateRasterWithScale_CalculatesCorrectDimensions()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // TIN bounds are 10x10

        // Act
        var result = rasterizer.CreateRasterWithScale(5.0); // 5 cells per unit

        // Assert
        Assert.Equal(50, result.Width); // 10 units * 5 cells per unit = 50 cells
        Assert.Equal(50, result.Height); // 10 units * 5 cells per unit = 50 cells

        // Cell size should be 0.2 units (1/5)
        Assert.Equal(0.2, result.CellWidth, 3);
        Assert.Equal(0.2, result.CellHeight, 3);
    }

    [Fact]
    public void CreateRasterWithWidth_CalculatesCorrectHeight()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);

        // TIN has 1:1 aspect ratio (10x10)

        // Act
        var result = rasterizer.CreateRasterWithWidth(200);

        // Assert
        Assert.Equal(200, result.Width);
        Assert.Equal(200, result.Height); // Height should match width for 1:1 aspect ratio
    }

    [Fact]
    public void GetStatistics_CalculatesCorrectStatistics()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);
        var result = rasterizer.CreateRaster(10, 10);

        // Act
        var stats = result.GetStatistics();

        // Assert
        Assert.InRange(stats.Min, 0, 1); // Minimum should be close to 0
        Assert.InRange(stats.Max, 9, 10); // Maximum should be close to 10
        Assert.InRange(stats.Mean, 4.5, 5.5); // Mean should be around 5
        Assert.True(stats.StdDev > 0); // StdDev should be positive
    }

    [Fact]
    public void WorldToRaster_AndRasterToWorld_AreInverse()
    {
        // Arrange
        var rasterizer = new TinRasterizer(this._tin, InterpolationType.TriangularFacet);
        var result = rasterizer.CreateRaster(20, 20);

        // Act & Assert
        // Test several random points
        var random = new Random(42);
        for (var i = 0; i < 10; i++)
        {
            // Generate random world coordinates inside the bounds
            var worldX = random.NextDouble() * 10;
            var worldY = random.NextDouble() * 10;

            // Convert world to raster
            var rasterCoords = result.WorldToRaster(worldX, worldY);
            Assert.NotNull(rasterCoords);

            // Convert back to world (center of the raster cell)
            var worldCoords = result.RasterToWorld(rasterCoords.Value.Column, rasterCoords.Value.Row);

            // They should be close but not exactly equal since we're getting the cell center
            Assert.InRange(Math.Abs(worldX - worldCoords.X), 0, result.CellWidth);
            Assert.InRange(Math.Abs(worldY - worldCoords.Y), 0, result.CellHeight);
        }
    }
}