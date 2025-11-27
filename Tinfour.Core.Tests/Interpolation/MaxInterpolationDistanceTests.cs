/*
 * Copyright 2025 Gary W. Lucas / ReefMaster Software Ltd.
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

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Tests for MaxInterpolationDistance functionality across all interpolator types.
/// </summary>
public class MaxInterpolationDistanceTests
{
    /// <summary>
    ///     Creates a simple TIN with known vertex positions for testing.
    ///     Vertices form a 100x100 grid with depth = x + y.
    /// </summary>
    private static IIncrementalTin CreateTestTin()
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(100, 0, 100),
            new Vertex(100, 100, 200),
            new Vertex(0, 100, 100),
            new Vertex(50, 50, 100) // Center point
        };
        tin.Add(vertices);
        return tin;
    }

    #region TriangularFacetInterpolator Tests

    [Fact]
    public void TriangularFacet_WithNullMaxDistance_ReturnsValueAnywhere()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin);

        // MaxInterpolationDistance is null by default

        // Act - interpolate at various points
        var result1 = interpolator.Interpolate(50, 50, null);
        var result2 = interpolator.Interpolate(25, 75, null);

        // Assert - should return valid values
        Assert.False(double.IsNaN(result1));
        Assert.False(double.IsNaN(result2));
    }

    [Fact]
    public void TriangularFacet_WithinMaxDistance_ReturnsValue()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 60.0 // Center vertex is at (50,50)
        };

        // Act - interpolate near the center vertex (within 60 units)
        var result = interpolator.Interpolate(55, 55, null);

        // Assert - should return a valid value
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void TriangularFacet_BeyondMaxDistance_ReturnsNaN()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 10.0 // Very small distance
        };

        // Act - interpolate at center point (far from any vertex with this small limit)
        // The center vertex is at (50,50), so if we query at (50,50) distance is 0
        // But if we query at (30,30), nearest vertex is (50,50) at distance ~28.3
        var result = interpolator.Interpolate(30, 30, null);

        // Assert - should return NaN because distance > 10
        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void TriangularFacet_AtExactMaxDistance_ReturnsValue()
    {
        // Arrange
        using var tin = CreateTestTin();

        // Distance from (60,50) to center vertex (50,50) is exactly 10
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 10.0
        };

        // Act - interpolate at point exactly 10 units from center vertex
        var result = interpolator.Interpolate(60, 50, null);

        // Assert - should return a valid value (at boundary, not beyond)
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void TriangularFacet_SettingMaxDistanceThenNull_RestoresUnlimitedBehavior()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 5.0 // Very restrictive
        };

        // Act - this should return NaN
        var restrictedResult = interpolator.Interpolate(30, 30, null);

        // Now remove the restriction
        interpolator.MaxInterpolationDistance = null;
        var unrestrictedResult = interpolator.Interpolate(30, 30, null);

        // Assert
        Assert.True(double.IsNaN(restrictedResult));
        Assert.False(double.IsNaN(unrestrictedResult));
    }

    #endregion

    #region NaturalNeighborInterpolator Tests

    [Fact]
    public void NaturalNeighbor_WithNullMaxDistance_ReturnsValueAnywhere()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new NaturalNeighborInterpolator(tin);

        // Act
        var result = interpolator.Interpolate(50, 50, null);

        // Assert
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void NaturalNeighbor_WithinMaxDistance_ReturnsValue()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new NaturalNeighborInterpolator(tin)
        {
            MaxInterpolationDistance = 60.0
        };

        // Act
        var result = interpolator.Interpolate(55, 55, null);

        // Assert
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void NaturalNeighbor_BeyondMaxDistance_ReturnsNaN()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new NaturalNeighborInterpolator(tin)
        {
            MaxInterpolationDistance = 10.0
        };

        // Act - point at (30,30) is ~28.3 units from nearest vertex (50,50)
        var result = interpolator.Interpolate(30, 30, null);

        // Assert
        Assert.True(double.IsNaN(result));
    }

    #endregion

    #region InverseDistanceWeightingInterpolator Tests

    [Fact]
    public void IDW_WithNullMaxDistance_ReturnsValueAnywhere()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act
        var result = interpolator.Interpolate(50, 50, null);

        // Assert
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void IDW_WithinMaxDistance_ReturnsValue()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new InverseDistanceWeightingInterpolator(tin)
        {
            MaxInterpolationDistance = 60.0
        };

        // Act
        var result = interpolator.Interpolate(55, 55, null);

        // Assert
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void IDW_BeyondMaxDistance_ReturnsNaN()
    {
        // Arrange
        using var tin = CreateTestTin();
        var interpolator = new InverseDistanceWeightingInterpolator(tin)
        {
            MaxInterpolationDistance = 10.0
        };

        // Act
        var result = interpolator.Interpolate(30, 30, null);

        // Assert
        Assert.True(double.IsNaN(result));
    }

    #endregion

    #region InterpolatorFactory Tests

    [Fact]
    public void InterpolatorFactory_WithOptions_SetsMaxDistance()
    {
        // Arrange
        using var tin = CreateTestTin();
        var options = new InterpolatorOptions
        {
            MaxInterpolationDistance = 25.0
        };

        // Act
        var interpolator = InterpolatorFactory.Create(tin, InterpolationType.TriangularFacet, options);

        // Assert
        Assert.Equal(25.0, interpolator.MaxInterpolationDistance);
    }

    [Fact]
    public void InterpolatorFactory_WithNullOptions_LeavesMaxDistanceNull()
    {
        // Arrange
        using var tin = CreateTestTin();

        // Act
        var interpolator = InterpolatorFactory.Create(tin, InterpolationType.TriangularFacet, null);

        // Assert
        Assert.Null(interpolator.MaxInterpolationDistance);
    }

    [Theory]
    [InlineData(InterpolationType.TriangularFacet)]
    [InlineData(InterpolationType.NaturalNeighbor)]
    [InlineData(InterpolationType.InverseDistanceWeighting)]
    public void InterpolatorFactory_AllTypes_SupportMaxDistance(InterpolationType type)
    {
        // Arrange
        using var tin = CreateTestTin();
        var options = new InterpolatorOptions
        {
            MaxInterpolationDistance = 100.0
        };

        // Act
        var interpolator = InterpolatorFactory.Create(tin, type, options);

        // Assert
        Assert.Equal(100.0, interpolator.MaxInterpolationDistance);

        // Verify interpolation works within distance
        var result = interpolator.Interpolate(50, 50, null);
        Assert.False(double.IsNaN(result));
    }

    #endregion

    #region InterpolatorOptions Tests

    [Fact]
    public void InterpolatorOptions_Clone_CopiesMaxDistance()
    {
        // Arrange
        var original = new InterpolatorOptions
        {
            MaxInterpolationDistance = 150.0,
            ConstrainedRegionsOnly = true
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(150.0, clone.MaxInterpolationDistance);
        Assert.True(clone.ConstrainedRegionsOnly);
    }

    [Fact]
    public void InterpolatorOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new InterpolatorOptions();

        // Assert
        Assert.Null(options.MaxInterpolationDistance);
        Assert.False(options.ConstrainedRegionsOnly);
        Assert.Equal(2.0, options.IdwPower);
        Assert.False(options.IdwUseDistanceWeighting);
    }

    #endregion

    #region TinRasterizer with MaxInterpolationDistance Tests

    [Fact]
    public void TinRasterizer_WithMaxDistance_ProducesNoDataInSparseAreas()
    {
        // Arrange - create a TIN with a large sparse area
        using var tin = new IncrementalTin();
        var vertices = new List<IVertex>
        {
            // Cluster in bottom-left corner
            new Vertex(0, 0, 10),
            new Vertex(10, 0, 10),
            new Vertex(10, 10, 10),
            new Vertex(0, 10, 10),

            // Single point far away in top-right
            new Vertex(90, 90, 50)
        };
        tin.Add(vertices);

        var options = new InterpolatorOptions
        {
            MaxInterpolationDistance = 20.0 // Only 20 units from nearest vertex
        };

        var rasterizer = new TinRasterizer(tin, InterpolationType.NaturalNeighbor, options);

        // Act - create a raster covering the full area
        var bounds = (Left: 0.0, Top: 0.0, Width: 100.0, Height: 100.0);
        var result = rasterizer.CreateRaster(20, 20, RasterDataType.Float32, bounds);

        // Assert - there should be NaN values in the sparse center area
        Assert.True(result.NoDataCount > 0, "Expected some NaN values in sparse areas");

        // The corner should have valid data
        var cornerValue = result.RasterData.GetValue(0, 0);
        Assert.False(double.IsNaN(cornerValue), "Bottom-left corner should have valid data");
    }

    [Fact]
    public void TinRasterizer_WithoutMaxDistance_InterpolatesWithinConvexHull()
    {
        // Arrange - create a TIN that covers the raster area with convex hull
        using var tin = new IncrementalTin();
        var vertices = new List<IVertex>
        {
            // Cover corners and center
            new Vertex(0, 0, 10),
            new Vertex(100, 0, 20),
            new Vertex(100, 100, 30),
            new Vertex(0, 100, 40),
            new Vertex(50, 50, 25)
        };
        tin.Add(vertices);

        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act - raster within TIN bounds
        var bounds = (Left: 0.0, Top: 0.0, Width: 100.0, Height: 100.0);
        var result = rasterizer.CreateRaster(20, 20, RasterDataType.Float32, bounds);

        // Assert - all cells should have valid data since raster is within convex hull
        Assert.Equal(0, result.NoDataCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MaxInterpolationDistance_ZeroValue_ReturnsNaNExceptAtVertex()
    {
        // Zero distance means only points exactly at a vertex position are valid
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 0.0
        };

        // At vertex (50,50) the distance to nearest vertex is 0, so this passes
        var atVertex = interpolator.Interpolate(50, 50, null);
        Assert.False(double.IsNaN(atVertex), "At exact vertex position, distance=0 should pass");

        // Away from vertex, distance > 0, so this fails
        var awayFromVertex = interpolator.Interpolate(51, 51, null);
        Assert.True(double.IsNaN(awayFromVertex), "Away from vertices, distance > 0 should fail");
    }

    [Fact]
    public void MaxInterpolationDistance_NegativeValue_BehavesLikePositive()
    {
        // Negative values get squared to positive, so -10 behaves like 10
        // This is documented behavior - negative values are NOT recommended
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = -10.0
        };

        // (-10)² = 100, so this behaves like MaxInterpolationDistance = 10
        // Distance from (55, 55) to (50, 50) = ~7.07, which is < 10
        var result = interpolator.Interpolate(55, 55, null);
        Assert.False(double.IsNaN(result), "Negative distance gets squared, behaves like positive");
    }

    [Fact]
    public void MaxInterpolationDistance_VeryLargeValue_BehavesLikeNoLimit()
    {
        // Very large values should effectively behave like no limit
        using var tin = CreateTestTin();
        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 1e10
        };

        // Act
        var result = interpolator.Interpolate(50, 50, null);

        // Assert
        Assert.False(double.IsNaN(result));
    }

    [Fact]
    public void MaxInterpolationDistance_ChecksNearestVertexOfTriangle()
    {
        // Verify that distance is checked against the nearest vertex of the containing triangle
        using var tin = new IncrementalTin();

        // Create a triangle where vertices are at different distances from the query point
        var vertices = new List<IVertex>
        {
            new Vertex(0, 0, 0),     // Distance from (5,5): ~7.07
            new Vertex(100, 0, 100), // Distance from (5,5): ~95.13
            new Vertex(0, 100, 100)  // Distance from (5,5): ~95.13
        };
        tin.Add(vertices);

        var interpolator = new TriangularFacetInterpolator(tin)
        {
            MaxInterpolationDistance = 10.0 // Just enough to reach (0,0) from (5,5)
        };

        // Act - query at (5,5) which is ~7.07 from (0,0)
        var result = interpolator.Interpolate(5, 5, null);

        // Assert - should succeed because nearest vertex is within distance
        Assert.False(double.IsNaN(result));

        // Now set a smaller limit
        interpolator.MaxInterpolationDistance = 5.0;
        var result2 = interpolator.Interpolate(5, 5, null);

        // Assert - should fail because even nearest vertex is too far
        Assert.True(double.IsNaN(result2));
    }

    #endregion
}
